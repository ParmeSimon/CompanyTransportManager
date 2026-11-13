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

        /// <summary>
        /// Niveau utilisé pour le LOOK du bâtiment (sprite « _lvlN »). 0 = en ruine tant
        /// qu'il n'est pas réparé ; ensuite il suit le vrai niveau de jeu du bâtiment :
        /// hangar = niveau du dépôt (emplacements), cuve = palier de pompe.
        /// </summary>
        public int GetVisualLevel(string building)
        {
            // En ruine uniquement si le bâtiment n'a jamais été réparé ET ne sert pas encore :
            // une partie déjà avancée (camions, pompe améliorée, conducteurs) ne montre pas de ruines.
            if (GetLevel(building) <= 0 && !InUse(building)) return 0;
            switch (building)
            {
                case BuildingVisuals.Hangar:   return System.Math.Max(1, _save.depot.level);
                case BuildingVisuals.FuelTank: return System.Math.Max(1, _save.fuelStation.pumpLevel);
                case BuildingVisuals.Office:
                {
                    // Le bureau grandit avec l'équipe : 1-2 conducteurs → cabine, 3-5 → immeuble, 6+ → QG.
                    int crew = _save.hiredDrivers?.Count ?? 0;
                    int byCrew = crew >= 6 ? 3 : crew >= 3 ? 2 : 1;
                    return System.Math.Max(byCrew, System.Math.Max(1, GetLevel(building)));
                }
                default: return System.Math.Max(1, GetLevel(building));
            }
        }

        private bool InUse(string building)
        {
            switch (building)
            {
                case BuildingVisuals.Hangar:   return _save.depot.level > 1 || (_save.vehicles?.Count ?? 0) > 0;
                case BuildingVisuals.FuelTank: return _save.fuelStation.pumpLevel > 1 || _save.fuelStation.currentLiters > 0;
                case BuildingVisuals.Office:   return (_save.hiredDrivers?.Count ?? 0) > 0;
                default:                       return false;
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
