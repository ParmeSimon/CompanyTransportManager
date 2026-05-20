using System;
using UnityEngine;

namespace TransportManager.Entities.Drivers
{
    [Serializable]
    public class DriverStats
    {
        [Tooltip("Speed multiplier bonus. -0.10 to +0.20 = -10% to +20% trip speed.")]
        public float speedBonus;

        [Tooltip("Fuel efficiency bonus. -0.05 to +0.15 = -5% to +15% less consumption.")]
        public float fuelEfficiencyBonus;

        [Tooltip("Safety score 0-100. Higher reduces vehicle wear.")]
        public float safetyScore;

        [Tooltip("Salary demand factor 0.0-0.5. Higher = more expensive driver.")]
        public float salaryDemandFactor;
    }
}
