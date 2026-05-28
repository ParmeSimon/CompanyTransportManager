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
    ///   - Gain  : distanceKm × Lerp(0.50, 0.10, endurance/100)
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
            float ratePerH = 5f + concentration * 0.10f; // 5–15 fatigue/heure
            return hours * ratePerH;
        }

        /// <summary>Fatigue gagnée sur un trajet (ou une portion de trajet).</summary>
        public static float ComputeFatigueGain(float distanceKm, float endurance)
        {
            float factor = Mathf.Lerp(0.50f, 0.10f, endurance / 100f);
            float reduction = ServiceLocator.Get<SkillTreeSystem>()?.Pct(SkillEffectType.FatigueReduction) ?? 0f;
            return distanceKm * factor * Mathf.Clamp01(1f - reduction);
        }

        /// <summary>
        /// Calcul pur (sans mutation) de la fatigue en fin de trajet.
        /// Utilisé à la signature du contrat pour pré-calculer l'accident.
        /// </summary>
        public static float ComputeFatigueAtEnd(DriverInstance driver, long contractStartTicks, float distanceKm)
        {
            float recovery     = ComputeRestRecovery(driver.lastContractEndUtcTicks, contractStartTicks, driver.stats.concentration);
            float fatigueStart = Mathf.Max(0f, driver.currentFatigue - recovery);
            float gain         = ComputeFatigueGain(distanceKm, driver.stats.endurance);
            return Mathf.Min(100f, fatigueStart + gain);
        }

        /// <summary>
        /// Applique le cycle repos + trajet et persiste l'état dans le driver.
        /// À appeler uniquement en fin de contrat (ou au moment de l'accident).
        /// </summary>
        public static float ApplyFatigueCycle(DriverInstance driver, long contractStartTicks, float distanceKm)
        {
            float fatigueEnd = ComputeFatigueAtEnd(driver, contractStartTicks, distanceKm);
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
            float effectiveDodge  = driver.stats.dodge * Mathf.Clamp01(1f - fatigueAtEnd / 100f);
            float accidentChance  = (1f - effectiveDodge / 100f) * distanceKm * BaseAccidentRatePerKm;
            accidentChance        = Mathf.Clamp(accidentChance, 0f, 0.95f);

            if (UnityEngine.Random.value > accidentChance)
                return new AccidentResult { severity = AccidentSeverity.None };

            // Mortel ?
            float fatalChance = BaseFatalChance * (1f - driver.stats.concentration / 100f);
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
