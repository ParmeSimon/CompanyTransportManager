using System.Collections.Generic;
using UnityEngine;

namespace TransportManager.Entities.Fuel
{
    [CreateAssetMenu(fileName = "FuelStationConfig", menuName = "TransportManager/Fuel Station Config")]
    public class FuelStationConfig : ScriptableObject
    {
        [Header("Pump tiers")]
        public List<FuelPumpTier> tiers = new List<FuelPumpTier>();

        [Header("Refill economy")]
        [Tooltip("Dollar cost per liter delivered by the truck.")]
        public float dollarsPerLiter = 1.5f;

        public FuelPumpTier GetTier(int level)
        {
            for (int i = 0; i < tiers.Count; i++)
            {
                if (tiers[i].level == level) return tiers[i];
            }
            return null;
        }

        public FuelPumpTier GetNextTier(int currentLevel) => GetTier(currentLevel + 1);
        public bool HasNextTier(int currentLevel) => GetNextTier(currentLevel) != null;
    }
}
