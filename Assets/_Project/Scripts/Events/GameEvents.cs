using System;
using TransportManager.Entities.Contracts;
using TransportManager.Entities.Drivers;
using TransportManager.Entities.Fuel;
using TransportManager.Entities.Vehicles;
using TransportManager.Enums;

namespace TransportManager.Events
{
    public static class GameEvents
    {
        // Economy
        public static event Action<int> OnDollarsChanged;
        public static event Action<int> OnGoldIngotsChanged;
        public static void RaiseDollarsChanged(int v) => OnDollarsChanged?.Invoke(v);
        public static void RaiseGoldIngotsChanged(int v) => OnGoldIngotsChanged?.Invoke(v);

        // Fleet
        public static event Action<VehicleInstance> OnVehicleAdded;
        public static event Action<VehicleInstance> OnVehicleStatusChanged;
        public static event Action<VehicleInstance> OnVehicleFuelChanged;
        public static void RaiseVehicleAdded(VehicleInstance v) => OnVehicleAdded?.Invoke(v);
        public static void RaiseVehicleStatusChanged(VehicleInstance v) => OnVehicleStatusChanged?.Invoke(v);
        public static void RaiseVehicleFuelChanged(VehicleInstance v) => OnVehicleFuelChanged?.Invoke(v);

        // Contracts
        public static event Action<ContractInstance> OnContractStarted;
        public static event Action<ContractInstance> OnContractCompleted;
        public static void RaiseContractStarted(ContractInstance c) => OnContractStarted?.Invoke(c);
        public static void RaiseContractCompleted(ContractInstance c) => OnContractCompleted?.Invoke(c);

        // Maintenance
        public static event Action<VehicleInstance> OnMaintenanceDue;
        public static void RaiseMaintenanceDue(VehicleInstance v) => OnMaintenanceDue?.Invoke(v);

        // Depot
        public static event Action<int> OnDockUnlocked;
        public static void RaiseDockUnlocked(int totalUnlocked) => OnDockUnlocked?.Invoke(totalUnlocked);

        // Fuel
        public static event Action<float> OnStationFuelChanged;
        public static event Action<FuelStationState> OnFuelRefillStarted;
        public static event Action<FuelStationState> OnFuelRefillCompleted;
        public static event Action<int> OnPumpUpgraded;
        public static void RaiseStationFuelChanged(float l) => OnStationFuelChanged?.Invoke(l);
        public static void RaiseFuelRefillStarted(FuelStationState s) => OnFuelRefillStarted?.Invoke(s);
        public static void RaiseFuelRefillCompleted(FuelStationState s) => OnFuelRefillCompleted?.Invoke(s);
        public static void RaisePumpUpgraded(int level) => OnPumpUpgraded?.Invoke(level);

        // HR / Drivers
        public static event Action<DriverInstance> OnDriverHired;
        public static event Action<DriverInstance> OnDriverFired;
        public static event Action<DriverInstance> OnDriverResigned;
        public static event Action<DriverInstance> OnDriverAssigned;
        public static event Action<DriverInstance> OnDriverXpChanged;
        public static event Action<DriverInstance> OnDriverWageChanged;
        public static event Action<DriverInstance, Entities.Drivers.AccidentResult> OnDriverAccident;
        public static event Action<DriverInstance> OnDriverDied;
        public static void RaiseDriverHired(DriverInstance d) => OnDriverHired?.Invoke(d);
        public static void RaiseDriverFired(DriverInstance d) => OnDriverFired?.Invoke(d);
        public static void RaiseDriverResigned(DriverInstance d) => OnDriverResigned?.Invoke(d);
        public static void RaiseDriverAssigned(DriverInstance d) => OnDriverAssigned?.Invoke(d);
        public static void RaiseDriverXpChanged(DriverInstance d) => OnDriverXpChanged?.Invoke(d);
        public static void RaiseDriverWageChanged(DriverInstance d) => OnDriverWageChanged?.Invoke(d);
        public static void RaiseDriverAccident(DriverInstance d, Entities.Drivers.AccidentResult r) => OnDriverAccident?.Invoke(d, r);
        public static void RaiseDriverDied(DriverInstance d) => OnDriverDied?.Invoke(d);

        // Progression
        public static event Action<int, int> OnCompanyXpChanged;
        public static void RaiseCompanyXpChanged(int xp, int level) => OnCompanyXpChanged?.Invoke(xp, level);

        // UI
        public static event Action<TabType> OnTabChanged;
        public static void RaiseTabChanged(TabType t) => OnTabChanged?.Invoke(t);

        // Map — visualisation de route
        public static event Action<ContractData> OnShowContractRoute;
        public static void RaiseShowContractRoute(ContractData def) => OnShowContractRoute?.Invoke(def);

        // Tutorial / Buildings
        public static event Action OnCompanyCreated;
        public static event Action<string, int> OnBuildingRepaired;
        public static event Action<string> OnFuelPanelOpened;
        public static event Action<string> OnHrPanelOpened;
        public static void RaiseCompanyCreated() => OnCompanyCreated?.Invoke();
        public static void RaiseBuildingRepaired(string building, int newLevel) => OnBuildingRepaired?.Invoke(building, newLevel);
        public static void RaiseFuelPanelOpened(string source) => OnFuelPanelOpened?.Invoke(source);
        public static void RaiseHrPanelOpened(string source) => OnHrPanelOpened?.Invoke(source);

        public static event Action OnCompanyProfileChanged;
        public static void RaiseCompanyProfileChanged() => OnCompanyProfileChanged?.Invoke();

        // Shop
        public static event Action OnDailyOfferClaimed;
        public static void RaiseDailyOfferClaimed() => OnDailyOfferClaimed?.Invoke();

        // Social / Friends
        public static event Action<string> OnFriendRoutesRequested;
        public static event Action<string> OnFriendDepotRequested;
        public static event Action<string> OnFriendTrucksRequested;
        public static event Action<string> OnFriendRoutesHidden;
        public static void RaiseFriendRoutesRequested(string uid) => OnFriendRoutesRequested?.Invoke(uid);
        public static void RaiseFriendRoutesHidden(string uid)    => OnFriendRoutesHidden?.Invoke(uid);
        public static void RaiseFriendDepotRequested(string uid)  => OnFriendDepotRequested?.Invoke(uid);
        public static void RaiseFriendTrucksRequested(string uid) => OnFriendTrucksRequested?.Invoke(uid);
    }
}
