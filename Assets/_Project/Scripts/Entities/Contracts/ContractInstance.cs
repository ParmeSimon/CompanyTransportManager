using System;
using TransportManager.Enums;

namespace TransportManager.Entities.Contracts
{
    [Serializable]
    public class ContractInstance
    {
        public string instanceId;
        public ContractData definition;
        public string assignedVehicleInstanceId;
        public long startTimeUtcTicks;
        public long completionTimeUtcTicks;
        public ContractStatus status;

        public DateTime StartTimeUtc => new DateTime(startTimeUtcTicks, DateTimeKind.Utc);
        public DateTime CompletionTimeUtc => new DateTime(completionTimeUtcTicks, DateTimeKind.Utc);

        public bool IsReadyToComplete => DateTime.UtcNow.Ticks >= completionTimeUtcTicks;
    }
}
