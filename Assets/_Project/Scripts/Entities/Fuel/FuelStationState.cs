using System;

namespace TransportManager.Entities.Fuel
{
    [Serializable]
    public class FuelStationState
    {
        public int pumpLevel = 1;
        public float currentLiters;

        public bool refillInProgress;
        public long refillCompleteUtcTicks;
        public float pendingRefillLiters;

        // Capstone Essence « Citerne autonome » : horodatage du dernier crédit de
        // remplissage automatique (0 = comptabilité non démarrée / capstone inactif).
        public long lastAutoRefillUtcTicks;
    }
}
