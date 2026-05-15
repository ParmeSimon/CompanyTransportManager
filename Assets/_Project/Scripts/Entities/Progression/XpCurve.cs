using UnityEngine;

namespace TransportManager.Entities.Progression
{
    public static class XpCurve
    {
        // Driver curve : level 1 at 0 xp, level 2 at 50 xp, level 3 at 200 xp, level 4 at 450, etc.
        public static int DriverLevelFromXp(int xp)
        {
            if (xp < 0) return 1;
            return Mathf.FloorToInt(Mathf.Sqrt(xp / 50f)) + 1;
        }

        public static int XpForDriverLevel(int level)
        {
            if (level <= 1) return 0;
            return (level - 1) * (level - 1) * 50;
        }

        // Company curve : slower than drivers (250 vs 50 base).
        public static int CompanyLevelFromXp(int xp)
        {
            if (xp < 0) return 1;
            return Mathf.FloorToInt(Mathf.Sqrt(xp / 250f)) + 1;
        }

        public static int XpForCompanyLevel(int level)
        {
            if (level <= 1) return 0;
            return (level - 1) * (level - 1) * 250;
        }

        // XP awarded to the driver for a delivered contract (distance / 10, min 1).
        public static int ContractXpReward(float distanceKm)
        {
            return Mathf.Max(1, Mathf.RoundToInt(distanceKm / 10f));
        }
    }
}
