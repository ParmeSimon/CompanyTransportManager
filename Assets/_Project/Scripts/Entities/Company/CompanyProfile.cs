using System;

namespace TransportManager.Entities.Company
{
    [Serializable]
    public class CompanyProfile
    {
        public string companyName;
        public string location;
        public double locationLatitude;
        public double locationLongitude;
        public bool hasLocationCoordinates;
        public long createdAtUtcTicks;
        public bool onboardingCompleted;

        // Logo personnalisable affiché dans le header.
        public bool   logoIsCustom; // true = photo importée (cf. CompanyLogoStore), sinon logo par défaut
        public string logoIconId;   // (déprécié — conservé pour compatibilité des sauvegardes)
        public string logoColorHex; // (déprécié — conservé pour compatibilité des sauvegardes)
    }
}
