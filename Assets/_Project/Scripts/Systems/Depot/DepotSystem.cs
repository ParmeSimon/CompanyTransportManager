using System;
using TransportManager.Core;
using TransportManager.Entities.Depot;
using TransportManager.Enums;
using TransportManager.Events;
using TransportManager.Save;
using TransportManager.Systems.Economy;
using TransportManager.Systems.Fleet;

namespace TransportManager.Systems.Depot
{
    public class DepotSystem
    {
        public const int BaseUpgradeCost = 5000;
        public const float UpgradeGrowthFactor = 2.5f;

        private readonly GameSaveData _save;

        public DepotSystem(GameSaveData save) { _save = save; }

        public DepotState State => _save.depot;
        public int Level => _save.depot.level;

        // Max vehicles the depot can host. Level 1 = 1 truck, level N = N trucks.
        public int MaxVehicleSlots => _save.depot.level;

        public int GetUsedSlots()
        {
            var fleet = ServiceLocator.Get<FleetSystem>();
            return fleet != null ? fleet.Vehicles.Count : 0;
        }

        public bool HasRoomForOneMore() => GetUsedSlots() < MaxVehicleSlots;

        public int GetNextUpgradeCost()
        {
            return (int)Math.Round(BaseUpgradeCost * Math.Pow(UpgradeGrowthFactor, _save.depot.level - 1));
        }

        public bool CanUpgrade()
        {
            var wallet = ServiceLocator.Get<WalletSystem>();
            return wallet != null && wallet.CanAfford(CurrencyType.Dollar, GetNextUpgradeCost());
        }

        public bool TryUpgrade()
        {
            var wallet = ServiceLocator.Get<WalletSystem>();
            if (wallet == null) return false;
            int cost = GetNextUpgradeCost();
            if (!wallet.TrySpend(CurrencyType.Dollar, cost)) return false;
            _save.depot.level++;
            GameEvents.RaiseDockUnlocked(_save.depot.level);
            return true;
        }
    }
}
