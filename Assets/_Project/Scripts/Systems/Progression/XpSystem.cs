using TransportManager.Entities.Progression;
using TransportManager.Events;
using TransportManager.Save;

namespace TransportManager.Systems.Progression
{
    public class XpSystem
    {
        private readonly GameSaveData _save;

        public XpSystem(GameSaveData save) { _save = save; }

        public int CompanyXp
        {
            get
            {
                int sum = 0;
                for (int i = 0; i < _save.hiredDrivers.Count; i++) sum += _save.hiredDrivers[i].xp;
                return sum;
            }
        }

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

        public void NotifyChanged()
        {
            GameEvents.RaiseCompanyXpChanged(CompanyXp, CompanyLevel);
        }
    }
}
