using TransportManager.Core;
using TransportManager.Entities.Progression;
using TransportManager.Events;
using TransportManager.Save;

namespace TransportManager.Systems.Progression
{
    public class XpSystem
    {
        private readonly GameSaveData _save;

        public XpSystem(GameSaveData save) { _save = save; }

        public int CompanyXp => _save.companyXp;

        public int CompanyLevel => XpCurve.CompanyLevelFromXp(CompanyXp);

        public int XpForNextCompanyLevel
        {
            get
            {
                int next = CompanyLevel + 1;
                int needed = XpCurve.XpForCompanyLevel(next) - CompanyXp;
                return needed < 0 ? 0 : needed;
            }
        }

        public bool IsVehicleUnlocked(int minLevelRequired) => CompanyLevel >= minLevelRequired;

        public void AddCompanyXpForContract(float distanceKm)
        {
            int oldLevel = CompanyLevel;
            int gain = XpCurve.ContractXpReward(distanceKm);
            _save.companyXp += gain;
            int newLevel = CompanyLevel;

            GameEvents.RaiseCompanyXpChanged(CompanyXp, newLevel);

            if (newLevel > oldLevel)
            {
                // Montée de niveau → points de compétence + notification.
                ServiceLocator.Get<SkillTreeSystem>()?.OnCompanyLevelChanged(oldLevel, newLevel);
                GameEvents.RaiseCompanyLevelUp(oldLevel, newLevel);
            }
        }

        public void NotifyChanged()
        {
            GameEvents.RaiseCompanyXpChanged(CompanyXp, CompanyLevel);
        }
    }
}
