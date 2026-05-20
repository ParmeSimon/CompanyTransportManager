using System;

namespace TransportManager.Entities.Fuel
{
    [Serializable]
    public class FuelPumpTier
    {
        public int level;
        public string displayName;
        public float storageCapacityLiters;
        public int upgradeCostDollars;

        // Time the delivery truck needs to fully fill the tank from empty.
        // Partial refills scale proportionally.
        public float fullRefillDurationSeconds;

        // Gold ingots needed for an instant full refill at this tier.
        // Partial instant refills scale proportionally.
        public int instantRefillIngotCost;
    }
}
