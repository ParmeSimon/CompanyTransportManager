using TransportManager.Save;
using TransportManager.Events;
using TransportManager.Enums;

namespace TransportManager.Systems.Economy
{
    public class WalletSystem
    {
        private readonly GameSaveData _save;

        public WalletSystem(GameSaveData save) { _save = save; }

        public int Dollars => _save.dollars;
        public int GoldIngots => _save.goldIngots;

        public bool CanAfford(CurrencyType type, int amount)
        {
            return type == CurrencyType.Dollar ? _save.dollars >= amount : _save.goldIngots >= amount;
        }

        public bool TrySpend(CurrencyType type, int amount)
        {
            if (amount < 0) return false;
            if (!CanAfford(type, amount)) return false;
            if (type == CurrencyType.Dollar)
            {
                _save.dollars -= amount;
                GameEvents.RaiseDollarsChanged(_save.dollars);
            }
            else
            {
                _save.goldIngots -= amount;
                GameEvents.RaiseGoldIngotsChanged(_save.goldIngots);
            }
            return true;
        }

        public void Add(CurrencyType type, int amount)
        {
            if (amount < 0) return;
            if (type == CurrencyType.Dollar)
            {
                _save.dollars += amount;
                GameEvents.RaiseDollarsChanged(_save.dollars);
            }
            else
            {
                _save.goldIngots += amount;
                GameEvents.RaiseGoldIngotsChanged(_save.goldIngots);
            }
        }
    }
}
