using System;
using System.Collections.Generic;
using TransportManager.Entities.Company;
using TransportManager.Entities.Contracts;
using TransportManager.Entities.Depot;
using TransportManager.Entities.Drivers;
using TransportManager.Entities.Fuel;
using TransportManager.Entities.Vehicles;

namespace TransportManager.Save
{
    [Serializable]
    public class GameSaveData
    {
        public int saveVersion = 1;
        public long lastSaveUtcTicks;
        public long installDateUtcTicks;

        public CompanyProfile company = new CompanyProfile();
        public int dollars = 15000;
        public int goldIngots = 10;
        public int energyDrinks = 0;
        public int companyXp = 0;
        public int reputation = 0;     // réputation (E4) : livraisons à l'heure ↑, accidents/retards ↓

        public DepotState depot = new DepotState();
        public FuelStationState fuelStation = new FuelStationState();

        public List<VehicleInstance> vehicles = new List<VehicleInstance>();
        public List<ContractData> availableContracts = new List<ContractData>();
        public List<ContractInstance> activeContracts = new List<ContractInstance>();

        public List<DriverInstance> hiredDrivers = new List<DriverInstance>();
        public List<DriverInstance> recruitmentPool = new List<DriverInstance>();
        public long lastHrRefreshUtcTicks;
        // Refresh payant : nb de skips payés aujourd'hui (escalade du coût Dollars) + jour de référence (minuit UTC).
        public int  hrPaidRefreshesToday;
        public long hrRefreshCounterDayUtcTicks;

        public TutorialState tutorial = new TutorialState();
        public BuildingLevels buildingLevels = new BuildingLevels();

        public List<StatSnapshot> snapshots = new List<StatSnapshot>();

        public ShopState shop = new ShopState();

        public SkillTreeState skillTree = new SkillTreeState();

        public DailyState daily = new DailyState();
    }

    [Serializable]
    public class TutorialState
    {
        public bool completed;
        public string currentStepId = "company_create";
    }

    [Serializable]
    public class BuildingLevels
    {
        public int hangar;
        public int office;
        public int fuelTank;
    }
}
