using System;
using UnityEngine;
using TransportManager.Entities.Drivers;
using TransportManager.Entities.Progression;

namespace TransportManager.Systems.Hr
{
    public static class DriverGenerator
    {
        public const float BaseWagePerContract = 20f;

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

        public static DriverInstance Generate()
        {
            int level = UnityEngine.Random.Range(1, 5);   // 1..4 (entry-level pool)
            int xp = XpCurve.XpForDriverLevel(level) + UnityEngine.Random.Range(0, 30);

            var stats = new DriverStats
            {
                speedBonus          = Mathf.Lerp(-0.10f, 0.20f, UnityEngine.Random.value),
                fuelEfficiencyBonus = Mathf.Lerp(-0.05f, 0.15f, UnityEngine.Random.value),
                safetyScore         = UnityEngine.Random.Range(40f, 100f),
                salaryDemandFactor  = Mathf.Lerp(0f, 0.5f, UnityEngine.Random.value),
                concentration       = UnityEngine.Random.Range(20f, 100f),
                dodge               = UnityEngine.Random.Range(20f, 100f),
                endurance           = UnityEngine.Random.Range(20f, 100f)
            };

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
                stats              = stats,
                assignedWagePerContract = desiredWage,
                desiredWagePerContract  = desiredWage
            };
        }

        public static int ComputeDesiredWage(int level, DriverStats stats)
        {
            float wage = BaseWagePerContract * level
                * (1f + stats.salaryDemandFactor);
            return Mathf.RoundToInt(wage);
        }
    }
}