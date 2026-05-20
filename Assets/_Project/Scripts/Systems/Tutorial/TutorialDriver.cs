using TransportManager.Enums;
using TransportManager.Events;
using TransportManager.Entities.Vehicles;
using TransportManager.Entities.Drivers;
using TransportManager.Entities.Contracts;
using TransportManager.Systems.Buildings;

namespace TransportManager.Systems.Tutorial
{
    public class TutorialDriver
    {
        private readonly TutorialSystem _tutorial;

        public TutorialDriver(TutorialSystem tutorial)
        {
            _tutorial = tutorial;
            Subscribe();
        }

        private void Subscribe()
        {
            GameEvents.OnCompanyCreated += HandleCompanyCreated;
            GameEvents.OnTabChanged += HandleTabChanged;
            GameEvents.OnBuildingRepaired += HandleBuildingRepaired;
            GameEvents.OnVehicleAdded += HandleVehicleAdded;
            GameEvents.OnHrPanelOpened += HandleHrPanelOpened;
            GameEvents.OnDriverHired += HandleDriverHired;
            GameEvents.OnFuelPanelOpened += HandleFuelPanelOpened;
            GameEvents.OnFuelRefillStarted += HandleFuelRefill;
            GameEvents.OnFuelRefillCompleted += HandleFuelRefill;
            GameEvents.OnContractStarted += HandleContractStarted;
        }

        public void Unsubscribe()
        {
            GameEvents.OnCompanyCreated -= HandleCompanyCreated;
            GameEvents.OnTabChanged -= HandleTabChanged;
            GameEvents.OnBuildingRepaired -= HandleBuildingRepaired;
            GameEvents.OnVehicleAdded -= HandleVehicleAdded;
            GameEvents.OnHrPanelOpened -= HandleHrPanelOpened;
            GameEvents.OnDriverHired -= HandleDriverHired;
            GameEvents.OnFuelPanelOpened -= HandleFuelPanelOpened;
            GameEvents.OnFuelRefillStarted -= HandleFuelRefill;
            GameEvents.OnFuelRefillCompleted -= HandleFuelRefill;
            GameEvents.OnContractStarted -= HandleContractStarted;
        }

        private void HandleCompanyCreated() => _tutorial.Advance(TutorialStep.CompanyCreate);

        private void HandleTabChanged(TabType tab)
        {
            var cur = _tutorial.CurrentStepId;
            if (cur == TutorialStep.GoToDepot && tab == TabType.Depot) _tutorial.Advance(cur);
            else if (cur == TutorialStep.GoToVehicles && tab == TabType.Vehicles) _tutorial.Advance(cur);
            else if (cur == TutorialStep.ReturnToDepot1 && tab == TabType.Depot) _tutorial.Advance(cur);
            else if (cur == TutorialStep.ReturnToDepot2 && tab == TabType.Depot) _tutorial.Advance(cur);
            else if (cur == TutorialStep.GoToMap && tab == TabType.Map) _tutorial.Advance(cur);
        }

        private void HandleBuildingRepaired(string building, int newLevel)
        {
            var cur = _tutorial.CurrentStepId;
            if (cur == TutorialStep.RepairHangar && building == BuildingVisuals.Hangar) _tutorial.Advance(cur);
            else if (cur == TutorialStep.RepairOffice && building == BuildingVisuals.Office) _tutorial.Advance(cur);
            else if (cur == TutorialStep.RepairFuelTank && building == BuildingVisuals.FuelTank) _tutorial.Advance(cur);
        }

        private void HandleVehicleAdded(VehicleInstance v)
        {
            if (_tutorial.CurrentStepId == TutorialStep.BuyFirstVehicle) _tutorial.Advance(TutorialStep.BuyFirstVehicle);
        }

        private void HandleHrPanelOpened(string source)
        {
            if (_tutorial.CurrentStepId == TutorialStep.OpenHr) _tutorial.Advance(TutorialStep.OpenHr);
        }

        private void HandleDriverHired(DriverInstance d)
        {
            if (_tutorial.CurrentStepId == TutorialStep.HireFirstDriver) _tutorial.Advance(TutorialStep.HireFirstDriver);
        }

        private void HandleFuelPanelOpened(string source)
        {
            if (_tutorial.CurrentStepId == TutorialStep.OpenFuel) _tutorial.Advance(TutorialStep.OpenFuel);
        }

        private void HandleFuelRefill(TransportManager.Entities.Fuel.FuelStationState s)
        {
            if (_tutorial.CurrentStepId == TutorialStep.FillFuel) _tutorial.Advance(TutorialStep.FillFuel);
        }

        private void HandleContractStarted(ContractInstance c)
        {
            if (_tutorial.CurrentStepId == TutorialStep.AcceptFirstContract) _tutorial.Advance(TutorialStep.AcceptFirstContract);
        }
    }
}
