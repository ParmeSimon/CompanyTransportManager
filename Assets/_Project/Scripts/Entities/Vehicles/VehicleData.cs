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

        public float MaxRangeKm()
        {
            return fuelConsumptionLPer100Km > 0f
                ? fuelTankCapacityLiters * 100f / fuelConsumptionLPer100Km
                : 0f;
        }

        public float FuelNeededFor(float distanceKm)
        {
            if (fuelConsumptionLPer100Km <= 0f) return 0f;
            return distanceKm / 100f * fuelConsumptionLPer100Km;
        }
    }
}
