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
    }
}
