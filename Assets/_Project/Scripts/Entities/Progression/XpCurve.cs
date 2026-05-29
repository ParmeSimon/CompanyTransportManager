using UnityEngine;
using TransportManager.Enums;

namespace TransportManager.Entities.Progression
{
    public static class XpCurve
    {
        // ── Constantes d'équilibrage (réglage facile) ──────────────────────────────
        // Courbe carrée : XP cumulée pour atteindre le niveau L = (L-1)² * base.
        private const float DriverXpBase  = 50f;   // conducteurs : montée rapide
        private const float CompanyXpBase = 200f;  // entreprise : L2≈200, L3=800, L4=1800, L5=3200…

        // Récompense d'XP d'un contrat livré.
        private const float DriverXpPerKm  = 1f / 10f; // conducteur : distance / 10
        private const float CompanyXpPerKm = 1f / 8f;  // entreprise : distance / 8, puis × difficulté

        // ── Conducteurs : niveau 1 à 0 xp, 2 à 50, 3 à 200, 4 à 450… ────────────────
        public static int DriverLevelFromXp(int xp)
        {
            if (xp < 0) return 1;
            return Mathf.FloorToInt(Mathf.Sqrt(xp / DriverXpBase)) + 1;
        }

        public static int XpForDriverLevel(int level)
        {
            if (level <= 1) return 0;
            return Mathf.RoundToInt((level - 1) * (level - 1) * DriverXpBase);
        }

        // ── Entreprise : plus lente que les conducteurs ─────────────────────────────
        public static int CompanyLevelFromXp(int xp)
        {
            if (xp < 0) return 1;
            return Mathf.FloorToInt(Mathf.Sqrt(xp / CompanyXpBase)) + 1;
        }

        public static int XpForCompanyLevel(int level)
        {
            if (level <= 1) return 0;
            return Mathf.RoundToInt((level - 1) * (level - 1) * CompanyXpBase);
        }

        // XP attribuée au CONDUCTEUR pour un contrat livré (distance / 10, min 1).
        public static int ContractXpReward(float distanceKm)
        {
            return Mathf.Max(1, Mathf.RoundToInt(distanceKm * DriverXpPerKm));
        }

        // XP attribuée à l'ENTREPRISE pour un contrat livré : distance pondérée par la
        // difficulté (les contrats plus durs/longs font progresser plus vite).
        public static int CompanyXpReward(float distanceKm, ContractDifficulty difficulty)
        {
            return Mathf.Max(1, Mathf.RoundToInt(distanceKm * CompanyXpPerKm * DifficultyMult(difficulty)));
        }

        private static float DifficultyMult(ContractDifficulty difficulty)
        {
            switch (difficulty)
            {
                case ContractDifficulty.Medium:  return 1.3f;
                case ContractDifficulty.Hard:    return 1.7f;
                case ContractDifficulty.Premium: return 2.2f;
                default:                         return 1.0f; // Easy
            }
        }
    }
}
