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

        // Genre ("m" / "f") et famille d'origine (cf. DriverOrigins) : fixent prénom, nom,
        // nationalité et portrait de façon cohérente.
        public string gender;
        public string origin;

        public int xp;
        public int contractsCompleted;

        // Graine de « talent » : détermine de façon déterministe les points forts/faibles
        // du pilote. Les stats se recalculent à partir de (niveau + graine), donc elles
        // montent quand le pilote gagne des niveaux, tout en gardant sa personnalité.
        public int statSeed;
        public long hiredAtUtcTicks;
        public bool hired;

        public DriverStats stats = new DriverStats();

        public int assignedWagePerContract;
        public int desiredWagePerContract;

        public string assignedVehicleInstanceId;

        // Fatigue : 0 = reposé, 100 = épuisé. Persiste entre les contrats.
        public float currentFatigue;

        // Demande d'augmentation (au lieu d'une démission surprise) :
        public long raiseRequestDeadlineTicks;   // 0 = aucune demande en cours ; sinon heure limite UTC
        public int  raiseRequestedWage;          // salaire demandé
        public int  raiseRefusals;               // refus cumulés (3 → départ)
        public long raiseNextAllowedTicks;       // pas de nouvelle demande avant (après un refus)
        public bool quitPending;                 // part à la fin de sa livraison en cours
        public long lastContractEndUtcTicks;

        public string FullName => $"{firstName} {lastName}";
    }
}
