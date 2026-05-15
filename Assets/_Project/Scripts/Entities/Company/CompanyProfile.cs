using System;

namespace TransportManager.Entities.Company
{
    [Serializable]
    public class CompanyProfile
    {
        public string companyName;
        public long createdAtUtcTicks;
        public bool onboardingCompleted;
    }
}
