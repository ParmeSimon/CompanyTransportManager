using UnityEngine;
using UnityEngine.UI;
using TransportManager.Enums;
using TransportManager.Events;

namespace TransportManager.UI.Common
{
    public class TabNavigator : MonoBehaviour
    {
        [SerializeField] private Button mapButton;
        [SerializeField] private Button depotButton;
        [SerializeField] private Button vehiclesButton;
        [SerializeField] private Button shopButton;

        private void OnEnable()
        {
            if (mapButton) mapButton.onClick.AddListener(GoToMap);
            if (depotButton) depotButton.onClick.AddListener(GoToDepot);
            if (vehiclesButton) vehiclesButton.onClick.AddListener(GoToVehicles);
            if (shopButton) shopButton.onClick.AddListener(GoToShop);
        }

        private void OnDisable()
        {
            if (mapButton) mapButton.onClick.RemoveListener(GoToMap);
            if (depotButton) depotButton.onClick.RemoveListener(GoToDepot);
            if (vehiclesButton) vehiclesButton.onClick.RemoveListener(GoToVehicles);
            if (shopButton) shopButton.onClick.RemoveListener(GoToShop);
        }

        private void GoToMap() => GameEvents.RaiseTabChanged(TabType.Map);
        private void GoToDepot() => GameEvents.RaiseTabChanged(TabType.Depot);
        private void GoToVehicles() => GameEvents.RaiseTabChanged(TabType.Vehicles);
        private void GoToShop() => GameEvents.RaiseTabChanged(TabType.Shop);
    }
}
