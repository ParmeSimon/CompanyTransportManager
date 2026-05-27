using System;

namespace TransportManager.Entities.Drivers
{
    [Serializable]
    public class DriverInstance
    {
        public string instanceId;
        public string firstName;
        public string lastName;
        public string nationality;

        public int xp;
        public int contractsCompleted;
        public long hiredAtUtcTicks;
        public bool hired;

        public DriverStats stats = new DriverStats();

        public int assignedWagePerContract;
        public int desiredWagePerContract;

        public string assignedVehicleInstanceId;

        // Fatigue : 0 = reposé, 100 = épuisé. Persiste entre les contrats.
        public float currentFatigue;
        public long lastContractEndUtcTicks;

        public string FullName => $"{firstName} {lastName}";
    }
}
