using System;
using UnityEngine;
using TransportManager.Core;
using TransportManager.Enums;
using TransportManager.Entities.Drivers;
using TransportManager.Systems.Progression;

namespace TransportManager.Systems.Accidents
{
    /// <summary>
    /// Gère la fatigue des conducteurs et le calcul des accidents en fin de contrat.
    ///
    /// Fatigue (0–100) :
    ///   - Gain  : heures de conduite × Lerp(9, 4, endurance/100)  (repos : 10–20 pts/h)
    ///   - Récup : (5 + concentration × 0.10) fatigue/heure de repos
    ///
    /// Probabilité accident = (1 − effectiveDodge/100) × distanceKm × 0.0005
    ///   effectiveDodge = dodge × (1 − fatigue/100)
    ///
    /// Accident fatal : chance = 0.20 × (1 − concentration/100)
    ///
    /// Coût de réparation × Lerp(1.0, 0.5, safety/100)  (sécurité élevée = moins cher)
    /// </summary>
    public static class AccidentSystem
    {
        private const float BaseAccidentRatePerKm = 0.0005f;
        private const float BaseFatalChance       = 0.20f;

        // ── Fatigue ────────────────────────────────────────────────────────────

        /// <summary>Fatigue récupérée pendant le repos entre deux contrats.</summary>
        public static float ComputeRestRecovery(long lastEndTicks, long contractStartTicks, float concentration)
        {
            if (lastEndTicks == 0) return 100f; // premier contrat → conducteur reposé
            float hours    = (float)((contractStartTicks - lastEndTicks) / (double)TimeSpan.TicksPerHour);
            hours          = Mathf.Max(0f, hours);
            float ratePerH = 10f + concentration * 0.10f; // 10–20 fatigue/heure de repos (une nuit = reposé)
            return hours * ratePerH;
        }

        /// <summary>
        /// Fatigue gagnée en conduisant <paramref name="driveHours"/> heures : 9 pts/h (endurance 0)
        /// à 4 pts/h (endurance 100). ~9 h de route = une journée complète pour un conducteur moyen.
        /// (Auparavant au km : tout trajet > ~200 km finissait à 100 %.)
        /// </summary>
        public static float ComputeFatigueGain(float driveHours, float endurance)
        {
            float perHour = Mathf.Lerp(9f, 4f, endurance / 100f);
            float reduction = ServiceLocator.Get<SkillTreeSystem>()?.Pct(SkillEffectType.FatigueReduction) ?? 0f;
            return Mathf.Max(0f, driveHours) * perHour * Mathf.Clamp01(1f - reduction);
        }

        /// <summary>
        /// Calcul pur (sans mutation) de la fatigue en fin de trajet.
        /// Utilisé à la signature du contrat pour pré-calculer l'accident.
        /// </summary>
        public static float ComputeFatigueAtEnd(DriverInstance driver, long contractStartTicks, float driveHours,
                                                float drinkProgress = 0f, float drinkRelief = 0f)
            => ComputeFatigueDuringTrip(driver, contractStartTicks, driveHours, 1f, drinkProgress, drinkRelief);

        /// <summary>Fatigue au départ du trajet (fatigue restante après le repos).</summary>
        public static float ComputeFatigueAtStart(DriverInstance driver, long contractStartTicks)
        {
            float recovery = ComputeRestRecovery(driver.lastContractEndUtcTicks, contractStartTicks, driver.stats.concentration);
            return Mathf.Max(0f, driver.currentFatigue - recovery);
        }

        /// <summary>
        /// Fatigue à une fraction (0–1) du trajet : elle monte avec les km parcourus.
        /// La fatigue du pilote n'est persistée qu'en fin de contrat, donc ce calcul reste
        /// valable en cours de route (et reflète une energy drink donnée entre-temps).
        /// </summary>
        /// Energy drink (drinkRelief > 0) : la fatigue, plafonnée à 100, chute de drinkRelief au
        /// moment drinkProgress, puis remonte avec les km parcourus ensuite.
        public static float ComputeFatigueDuringTrip(DriverInstance driver, long contractStartTicks, float driveHours, float progress,
                                                     float drinkProgress = 0f, float drinkRelief = 0f)
        {
            progress = Mathf.Clamp01(progress);
            float start = ComputeFatigueAtStart(driver, contractStartTicks);
            float endurance = driver.stats.endurance;

            if (drinkRelief <= 0f || progress < drinkProgress)
                return Mathf.Min(100f, start + ComputeFatigueGain(driveHours * progress, endurance));

            float atDrink = Mathf.Min(100f, start + ComputeFatigueGain(driveHours * drinkProgress, endurance));
            float after   = Mathf.Max(0f, atDrink - drinkRelief)
                          + ComputeFatigueGain(driveHours * (progress - drinkProgress), endurance);
            return Mathf.Min(100f, after);
        }

        /// <summary>Probabilité (0–0,95) d'accident sur un trajet, selon l'esquive et la fatigue.</summary>
        public static float AccidentChance(DriverInstance driver, float fatigue, float distanceKm)
        {
            float effectiveDodge = driver.stats.dodge * Mathf.Clamp01(1f - fatigue / 100f);
            float chance = (1f - effectiveDodge / 100f) * distanceKm * BaseAccidentRatePerKm;
            chance *= Mathf.Clamp01(1f - SkillTreeSystem.PctOf(SkillEffectType.AccidentChanceReduction));
            return Mathf.Clamp(chance, 0f, 0.95f);
        }

        /// <summary>
        /// Applique le cycle repos + trajet et persiste l'état dans le driver.
        /// À appeler uniquement en fin de contrat (ou au moment de l'accident).
        /// </summary>
        public static float ApplyFatigueCycle(DriverInstance driver, long contractStartTicks, float driveHours,
                                              float drinkProgress = 0f, float drinkRelief = 0f)
        {
            float fatigueEnd = ComputeFatigueAtEnd(driver, contractStartTicks, driveHours, drinkProgress, drinkRelief);
            driver.currentFatigue          = fatigueEnd;
            driver.lastContractEndUtcTicks = DateTime.UtcNow.Ticks;
            return fatigueEnd;
        }

        // ── Planification temporelle de l'accident ─────────────────────────────

        /// <summary>
        /// Choisit un timestamp aléatoire pour l'accident.
        /// Biaisé vers la seconde moitié du trajet (fatigue croissante).
        /// </summary>
        public static long ScheduleAccidentTime(long startTicks, long endTicks)
        {
            float t = Mathf.Lerp(0.30f, 1.00f, UnityEngine.Random.value);
            return startTicks + (long)((endTicks - startTicks) * t);
        }

        // ── Accident ───────────────────────────────────────────────────────────

        public static AccidentResult Roll(DriverInstance driver, float fatigueAtEnd,
                                          float distanceKm, int vehiclePurchasePrice)
        {
            float accidentChance  = AccidentChance(driver, fatigueAtEnd, distanceKm);

            if (UnityEngine.Random.value > accidentChance)
                return new AccidentResult { severity = AccidentSeverity.None };

            // Mortel ? (« Secourisme » réduit ce risque)
            float fatalChance = BaseFatalChance * (1f - driver.stats.concentration / 100f)
                              * Mathf.Clamp01(1f - SkillTreeSystem.PctOf(SkillEffectType.FatalAccidentReduction));
            if (UnityEngine.Random.value < fatalChance)
                return new AccidentResult { severity = AccidentSeverity.Fatal, description = "Accident mortel" };

            // Non mortel : gravité et coût de réparation
            float roll = UnityEngine.Random.value;
            AccidentSeverity severity;
            float repairFactor;
            string desc;

            if (roll < 0.50f)
            {
                severity     = AccidentSeverity.Minor;
                repairFactor = UnityEngine.Random.Range(0.05f, 0.15f);
                desc         = "Accrochage mineur";
            }
            else if (roll < 0.85f)
            {
                severity     = AccidentSeverity.Moderate;
                repairFactor = UnityEngine.Random.Range(0.15f, 0.30f);
                desc         = "Accident modéré";
            }
            else
            {
                severity     = AccidentSeverity.Severe;
                repairFactor = UnityEngine.Random.Range(0.30f, 0.50f);
                desc         = "Accident grave";
            }

            // La sécurité du conducteur réduit le coût de réparation (jusqu'à -50%).
            repairFactor *= driver.stats.RepairCostFactor;

            return new AccidentResult
            {
                severity          = severity,
                vehicleRepairCost = Mathf.RoundToInt(vehiclePurchasePrice * repairFactor),
                description       = desc
            };
        }
    }
}
