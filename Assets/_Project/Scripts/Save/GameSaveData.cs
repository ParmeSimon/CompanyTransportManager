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

        public CompanyProfile company = new CompanyProfile();
        public int dollars = 15000;
        public int goldIngots = 10;

        public DepotState depot = new DepotState();
        public FuelStationState fuelStation = new FuelStationState();

        public List<VehicleInstance> vehicles = new List<VehicleInstance>();
        public List<ContractData> availableContracts = new List<ContractData>();
        public List<ContractInstance> activeContracts = new List<ContractInstance>();

        public List<DriverInstance> hiredDrivers = new List<DriverInstance>();
        public List<DriverInstance> recruitmentPool = new List<DriverInstance>();
        public long lastHrRefreshUtcTicks;
    }
}
