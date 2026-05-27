using System;

namespace TransportManager.Entities.Drivers
{
    public enum AccidentSeverity { None, Minor, Moderate, Severe, Fatal }

    [Serializable]
    public class AccidentResult
    {
        public AccidentSeverity severity;
        public int vehicleRepairCost;
        public string description;

        public bool IsAccident => severity != AccidentSeverity.None;
        public bool IsFatal    => severity == AccidentSeverity.Fatal;
    }
}
