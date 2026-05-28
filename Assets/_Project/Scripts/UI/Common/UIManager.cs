using UnityEngine;
using TransportManager.Enums;
using TransportManager.Events;
using TransportManager.UI.Tabs;

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

        private void Awake() => EnsureHrTab();

        private void OnEnable() => GameEvents.OnTabChanged += ShowTab;
        private void OnDisable() => GameEvents.OnTabChanged -= ShowTab;

        private void Start() => GameEvents.RaiseTabChanged(defaultTab);

        // Crée le tab Pilote (HrTabView) s'il n'a pas été placé dans la scène,
        // pour qu'il soit disponible derrière le bouton "Pilote" de la navbar.
        private void EnsureHrTab()
        {
            if (hrTab != null) return;

            Transform parent = vehiclesTab != null ? vehiclesTab.transform.parent : transform;

            var go = new GameObject("HrTab", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            go.AddComponent<HrTabView>();
            go.SetActive(false);
            hrTab = go;
        }

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
