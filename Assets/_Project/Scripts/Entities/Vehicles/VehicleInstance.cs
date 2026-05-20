using System;
using TransportManager.Enums;

namespace TransportManager.Entities.Vehicles
{
    [Serializable]
    public class VehicleInstance
    {
        public string instanceId;
        public string vehicleDataId;
        public int totalKilometers;
        public float currentFuelLiters;
        public VehicleStatus status;
        public string activeContractInstanceId;
        public string assignedDriverInstanceId;
        public bool maintenanceDueAfterContract;

        public float WearRatio(int maxKilometers)
        {
            if (maxKilometers <= 0) return 0f;
            return (float)totalKilometers / maxKilometers;
        }

        public float CurrentRangeKm(VehicleData data)
        {
            if (data == null || data.fuelConsumptionLPer100Km <= 0f) return 0f;
            return currentFuelLiters * 100f / data.fuelConsumptionLPer100Km;
        }
    }
}
