using UnityEngine;
using TransportManager.Enums;
using TransportManager.Events;

namespace TransportManager.UI.Common
{
    public class UIManager : MonoBehaviour
    {
        [SerializeField] private GameObject mapTab;
        [SerializeField] private GameObject depotTab;
        [SerializeField] private GameObject vehiclesTab;
        [SerializeField] private GameObject shopTab;
        [SerializeField] private GameObject fuelTab;
        [SerializeField] private GameObject hrTab;
        [SerializeField] private TabType defaultTab = TabType.Map;

        private void OnEnable() => GameEvents.OnTabChanged += ShowTab;
        private void OnDisable() => GameEvents.OnTabChanged -= ShowTab;

        private void Start() => ShowTab(defaultTab);

        private void ShowTab(TabType tab)
        {
            if (mapTab) mapTab.SetActive(tab == TabType.Map);
            if (depotTab) depotTab.SetActive(tab == TabType.Depot);
            if (vehiclesTab) vehiclesTab.SetActive(tab == TabType.Vehicles);
            if (shopTab) shopTab.SetActive(tab == TabType.Shop);
            if (fuelTab) fuelTab.SetActive(tab == TabType.Fuel);
            if (hrTab) hrTab.SetActive(tab == TabType.Hr);
        }
    }
}
