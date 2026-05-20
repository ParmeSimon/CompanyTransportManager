using TransportManager.Events;
using TransportManager.Save;
using TransportManager.Systems.Economy;
using TransportManager.Enums;

namespace TransportManager.Systems.Buildings
{
    public class BuildingService
    {
        private readonly GameSaveData _save;
        private readonly WalletSystem _wallet;

        public BuildingService(GameSaveData save, WalletSystem wallet)
        {
            _save = save;
            _wallet = wallet;
        }

        public int GetLevel(string building)
        {
            switch (building)
            {
                case BuildingVisuals.Hangar:   return _save.buildingLevels.hangar;
                case BuildingVisuals.Office:   return _save.buildingLevels.office;
                case BuildingVisuals.FuelTank: return _save.buildingLevels.fuelTank;
                default: return 0;
            }
        }

        public int GetRepairCost(string building)
        {
            int level = GetLevel(building);
            // Repair from ruin (0 -> 1) is free for tutorial flow on the first three buildings.
            if (level == 0) return 0;
            return 500 * level; // upgrades scale linearly for now
        }

        public bool CanRepair(string building) => _wallet.Dollars >= GetRepairCost(building);

        public bool Repair(string building)
        {
            int cost = GetRepairCost(building);
            if (cost > 0 && !_wallet.TrySpend(CurrencyType.Dollar, cost)) return false;
            int newLevel = GetLevel(building) + 1;
            SetLevel(building, newLevel);
            GameEvents.RaiseBuildingRepaired(building, newLevel);
            return true;
        }

        private void SetLevel(string building, int level)
        {
            switch (building)
            {
                case BuildingVisuals.Hangar:   _save.buildingLevels.hangar = level; break;
                case BuildingVisuals.Office:   _save.buildingLevels.office = level; break;
                case BuildingVisuals.FuelTank: _save.buildingLevels.fuelTank = level; break;
            }
        }
    }
}
