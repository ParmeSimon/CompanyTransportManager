using System;
using UnityEngine;
using TransportManager.Core;
using TransportManager.Enums;
using TransportManager.Entities.Drivers;
using TransportManager.Entities.Progression;
using TransportManager.Systems.Progression;

namespace TransportManager.Systems.Hr
{
    public static class DriverGenerator
    {
        public const float BaseWagePerContract = 20f;

        // ── Échelle stats ↔ niveau ───────────────────────────────────────────────
        // La moyenne des stats part de ~quasi zéro au niveau 1 et grimpe doucement,
        // palier après palier, jusqu'à plafonner près de 92. Étalée sur ~18 niveaux,
        // la progression reste intéressante longtemps. Chaque stat est ensuite
        // décalée de ±StatSpread autour de cette moyenne selon la « graine de talent »
        // du pilote (points forts/faibles stables d'un niveau à l'autre).
        private const float StatMeanAtLevel1     = 3f;    // moyenne au niveau 1 → proche de zéro
        private const float StatMeanGrowthPerLvl = 5f;    // gain de moyenne par niveau
        private const float StatMeanCap          = 92f;   // plafond de la moyenne (atteint vers niv. 19)
        private const float StatSpread           = 14f;   // décalage aléatoire par stat (±)
        private const float StatFloor            = 0f;    // plancher
        private const float StatCeil             = 100f;  // plafond

        // Plage de niveaux des candidats du vivier de recrutement.
        private const int PoolMinLevel = 1;
        private const int PoolMaxLevel = 6;

        private static readonly string[] FirstNames =
        {
            "Jean", "Pierre", "Marc", "Luc", "Paul", "Émile", "Antoine", "Hugo", "Théo",
            "Marie", "Sophie", "Camille", "Léa", "Julie", "Claire", "Anne", "Élise",
            "Mohamed", "Karim", "Hakim", "Yasmine", "Amira", "Samir",
            "Pavel", "Andrei", "Ivan", "Nikolai", "Boris",
            "Klaus", "Hans", "Stefan", "Greta", "Lukas"
        };

        private static readonly string[] LastNames =
        {
            "Martin", "Bernard", "Dubois", "Thomas", "Robert", "Richard",
            "Petit", "Durand", "Leroy", "Moreau", "Simon", "Laurent",
            "Lefebvre", "Michel", "Garcia", "David", "Bertrand",
            "Roux", "Vincent", "Fournier", "Morel", "Girard", "Rousseau",
            "Mercier", "Blanc", "Faure", "Dupuis", "Carpentier"
        };

        private static readonly string[] Nationalities =
        {
            "Français", "Belge", "Suisse", "Espagnol", "Portugais",
            "Italien", "Allemand", "Polonais", "Roumain",
            "Algérien", "Marocain", "Tunisien", "Turc", "Russe"
        };

        /// <summary>Génère un candidat de niveau aléatoire (biaisé vers le bas) pour le vivier.</summary>
        public static DriverInstance Generate() => Generate(RandomPoolLevel());

        /// <summary>Génère un candidat d'un niveau donné : stats corrélées au niveau + talent aléatoire.</summary>
        public static DriverInstance Generate(int level)
        {
            level = Mathf.Max(1, level);

            // XP placée quelque part dans la tranche du niveau (sans déborder au suivant).
            int xpFloor = XpCurve.XpForDriverLevel(level);
            int xpBand  = Mathf.Max(1, XpCurve.XpForDriverLevel(level + 1) - xpFloor);
            int xp      = xpFloor + UnityEngine.Random.Range(0, xpBand);

            int seed = Guid.NewGuid().GetHashCode();
            if (seed == 0) seed = 1; // 0 réservé aux pilotes d'anciennes sauvegardes

            var stats = new DriverStats();
            ApplyStatsForLevel(stats, level, seed);

            int desiredWage = ComputeDesiredWage(level, stats);

            return new DriverInstance
            {
                instanceId         = Guid.NewGuid().ToString(),
                firstName          = FirstNames[UnityEngine.Random.Range(0, FirstNames.Length)],
                lastName           = LastNames[UnityEngine.Random.Range(0, LastNames.Length)],
                nationality        = Nationalities[UnityEngine.Random.Range(0, Nationalities.Length)],
                xp                 = xp,
                contractsCompleted = 0,
                hired              = false,
                currentFatigue     = 0f,
                statSeed           = seed,
                stats              = stats,
                assignedWagePerContract = desiredWage,
                desiredWagePerContract  = desiredWage
            };
        }

        /// <summary>
        /// Recalcule les 6 stats à partir du niveau et de la graine de talent du pilote.
        /// Pour une même graine, les décalages par stat sont identiques : la personnalité
        /// du pilote (ses points forts/faibles) reste stable, mais ses stats montent
        /// à chaque niveau gagné. Appelé à la génération et à chaque montée de niveau.
        /// </summary>
        public static void ApplyStatsForLevel(DriverStats stats, int level, int seed)
        {
            if (stats == null) return;
            var rng = new System.Random(seed);
            float mean = LevelStatMean(level);

            stats.speed          = RollStat(mean, rng);
            stats.fuelEfficiency = RollStat(mean, rng);
            stats.safety         = RollStat(mean, rng);
            stats.concentration  = RollStat(mean, rng);
            stats.dodge          = RollStat(mean, rng);
            stats.endurance      = RollStat(mean, rng);
        }

        // Salaire demandé : dérivé du niveau et de la note générale.
        // Un meilleur conducteur (général élevé) coûte plus cher.
        public static int ComputeDesiredWage(int level, DriverStats stats)
        {
            float wage = BaseWagePerContract * level
                * (1f + stats.General / 100f);
            return Mathf.RoundToInt(wage);
        }

        // Moyenne des stats visée pour un niveau donné.
        private static float LevelStatMean(int level) =>
            Mathf.Min(StatMeanAtLevel1 + (level - 1) * StatMeanGrowthPerLvl, StatMeanCap);

        // Niveau pondéré vers le bas : les bons candidats sont plus rares.
        private static int RandomPoolLevel()
        {
            float t = UnityEngine.Random.value;
            t *= t; // biais quadratique → favorise les petits niveaux
            int level = PoolMinLevel + Mathf.FloorToInt(t * (PoolMaxLevel - PoolMinLevel + 1));

            // Compétence RH « Cabinet de recrutement » : relève le niveau des candidats.
            int bonus = ServiceLocator.Get<SkillTreeSystem>()?.Flat(SkillEffectType.RecruitmentLevelBonus) ?? 0;
            return level + bonus;
        }

        // Stat 0-100 centrée sur la moyenne du niveau, ±StatSpread (déterministe via rng).
        private static float RollStat(float mean, System.Random rng)
        {
            float offset = ((float)rng.NextDouble() * 2f - 1f) * StatSpread; // -spread..+spread
            return Mathf.Round(Mathf.Clamp(mean + offset, StatFloor, StatCeil));
        }
    }
}
