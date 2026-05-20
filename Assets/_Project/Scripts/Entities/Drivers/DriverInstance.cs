using System;

namespace TransportManager.Entities.Drivers
{
    [Serializable]
    public class DriverInstance
    {
        public string instanceId;
        public string firstName;
        public string lastName;

        public int xp;
        public int contractsCompleted;
        public long hiredAtUtcTicks;
        public bool hired;

        public DriverStats stats = new DriverStats();

        public int assignedWagePerContract;
        public int desiredWagePerContract;

        public string assignedVehicleInstanceId;

        public string FullName => $"{firstName} {lastName}";
    }
}
