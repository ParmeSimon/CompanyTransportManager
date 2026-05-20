using TransportManager.Core;
using TransportManager.Enums;
using TransportManager.Save;
using TransportManager.Systems.Economy;

namespace TransportManager.Systems.Shop
{
    public class ShopSystem
    {
        private readonly GameSaveData _save;

        public ShopSystem(GameSaveData save) { _save = save; }

        public void GrantGoldIngots(int amount)
        {
            if (amount <= 0) return;
            var wallet = ServiceLocator.Get<WalletSystem>();
            wallet?.Add(CurrencyType.GoldIngot, amount);
        }

        // TODO: define IAP product catalog (productId -> ingot amount) and validation flow.
    }
}
