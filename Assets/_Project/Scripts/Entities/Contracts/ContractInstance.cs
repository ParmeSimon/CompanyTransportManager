using System;
using TransportManager.Entities.Drivers;
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
        public long deadlineTimeUtcTicks;   // livrer avant cette heure pour le bonus de ponctualité
        public ContractStatus status;

        // Accident pré-calculé à la signature du contrat (0 / None = aucun accident prévu)
        public long             scheduledAccidentTimeTicks;
        public AccidentSeverity scheduledAccidentSeverity;
        public int              scheduledAccidentRepairCost;
        public string           scheduledAccidentDescription;

        public DateTime StartTimeUtc      => new DateTime(startTimeUtcTicks,      DateTimeKind.Utc);
        public DateTime CompletionTimeUtc => new DateTime(completionTimeUtcTicks, DateTimeKind.Utc);

        public bool IsReadyToComplete => DateTime.UtcNow.Ticks >= completionTimeUtcTicks;

        public bool HasScheduledAccident =>
            scheduledAccidentSeverity != AccidentSeverity.None && scheduledAccidentTimeTicks > 0;

        public bool IsAccidentDue =>
            HasScheduledAccident && DateTime.UtcNow.Ticks >= scheduledAccidentTimeTicks;

        /// Progression du trajet au moment de l'accident (0–1).
        public float AccidentProgressRatio =>
            completionTimeUtcTicks > startTimeUtcTicks
                ? (float)(scheduledAccidentTimeTicks - startTimeUtcTicks)
                  / (float)(completionTimeUtcTicks   - startTimeUtcTicks)
                : 0f;
    }
}
