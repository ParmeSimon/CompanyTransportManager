using System;
using UnityEngine;
using TransportManager.Enums;

namespace TransportManager.Entities.Vehicles
{
    [Serializable]
    public class VehicleData
    {
        [Header("Identity")]
        public string id;
        public string displayName;
        public VehicleCategory category;
        public Sprite icon;

        [Header("Economy")]
        public int purchasePrice;
        public int maintenanceCost;

        [Header("Performance")]
        public float speedKmh;
        public int capacity;
        public int maxKilometers;

        [Header("Fuel")]
        public float fuelTankCapacityLiters;
        public float fuelConsumptionLPer100Km;

        [Header("Routing")]
        public VehicleRoutingProfile routingProfile = VehicleRoutingProfile.Car;

        [Header("Unlock")]
        [Tooltip("Minimum company level required to purchase this vehicle.")]
        public int minCompanyLevelRequired = 1;

        /// <summary>
        /// Niveau d'entreprise réellement requis : le maximum entre le niveau propre au
        /// véhicule et le plancher de sa classe (<see cref="VehicleClassUnlock"/>).
        /// </summary>
        public int RequiredCompanyLevel =>
            Mathf.Max(minCompanyLevelRequired, VehicleClassUnlock.ForCategory(category));

        /// <summary>
        /// Capacité RÉELLE du réservoir : capacité catalogue + bonus « Réservoirs XL »
        /// (arbre Essence). À utiliser partout dans le jeu à la place de fuelTankCapacityLiters.
        /// </summary>
        public float TankCapacity =>
            fuelTankCapacityLiters * (1f + Systems.Progression.SkillTreeSystem.PctOf(SkillEffectType.VehicleTankBonus));

        /// <summary>Prix d'achat RÉEL : prix catalogue - réduction « Concession partenaire » (arbre Dépôt).</summary>
        public int EffectivePrice =>
            Mathf.Max(0, Mathf.RoundToInt(purchasePrice
                * (1f - Systems.Progression.SkillTreeSystem.PctOf(SkillEffectType.VehiclePriceReduction))));

        public float MaxRangeKm()
        {
            return fuelConsumptionLPer100Km > 0f
                ? TankCapacity * 100f / fuelConsumptionLPer100Km
                : 0f;
        }

        public float FuelNeededFor(float distanceKm)
        {
            if (fuelConsumptionLPer100Km <= 0f) return 0f;
            return distanceKm / 100f * fuelConsumptionLPer100Km;
        }
    }
}
