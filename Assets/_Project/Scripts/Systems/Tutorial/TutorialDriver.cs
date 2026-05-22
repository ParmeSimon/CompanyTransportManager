using TransportManager.Enums;
using TransportManager.Events;
using TransportManager.Entities.Vehicles;

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
            GameEvents.OnVehicleAdded += HandleVehicleAdded;
            GameEvents.OnDailyOfferClaimed += HandleDailyOfferClaimed;
        }

        public void Unsubscribe()
        {
            GameEvents.OnCompanyCreated -= HandleCompanyCreated;
            GameEvents.OnTabChanged -= HandleTabChanged;
            GameEvents.OnVehicleAdded -= HandleVehicleAdded;
            GameEvents.OnDailyOfferClaimed -= HandleDailyOfferClaimed;
        }

        private void HandleCompanyCreated() => _tutorial.Advance(TutorialStep.CompanyCreate);

        private void HandleTabChanged(TabType tab)
        {
            var cur = _tutorial.CurrentStepId;
            if (cur == TutorialStep.GoToMap && tab == TabType.Map) _tutorial.Advance(cur);
            else if (cur == TutorialStep.GoToDepot && tab == TabType.Depot) _tutorial.Advance(cur);
            else if (cur == TutorialStep.GoToVehicles && tab == TabType.Vehicles) _tutorial.Advance(cur);
            else if (cur == TutorialStep.GoToShop && tab == TabType.Shop) _tutorial.Advance(cur);
        }

        private void HandleVehicleAdded(VehicleInstance v)
        {
            if (_tutorial.CurrentStepId == TutorialStep.BuyFirstVehicle) _tutorial.Advance(TutorialStep.BuyFirstVehicle);
        }

        private void HandleDailyOfferClaimed()
        {
            if (_tutorial.CurrentStepId == TutorialStep.ClaimDailyOffer) _tutorial.Advance(TutorialStep.ClaimDailyOffer);
        }
    }
}
