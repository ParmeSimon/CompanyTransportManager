using System;

namespace TransportManager.Save
{
    [Serializable]
    public class StatSnapshot
    {
        public long utcTicks;
        public int  dollars;
        public int  goldIngots;
        public int  companyXp;
        public int  contractsCompleted;
        public int  vehicleCount;
    }
}
