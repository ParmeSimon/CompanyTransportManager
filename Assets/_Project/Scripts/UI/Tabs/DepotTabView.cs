using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TransportManager.Core;
using TransportManager.Events;
using TransportManager.Systems.Depot;
using TransportManager.Systems.Fleet;

namespace TransportManager.UI.Tabs
{
    public class DepotTabView : MonoBehaviour
    {
        [Header("Labels")]
        [SerializeField] private TMP_Text levelLabel;
        [SerializeField] private TMP_Text usageLabel;
        [SerializeField] private TMP_Text nextCostLabel;

        [Header("Action")]
        [SerializeField] private Button upgradeButton;

        private void OnEnable()
        {
            if (upgradeButton) upgradeButton.onClick.AddListener(OnUpgradeClicked);
            GameEvents.OnDockUnlocked += OnDocksChanged;
            GameEvents.OnDollarsChanged += OnDollarsChanged;
            GameEvents.OnVehicleAdded += OnVehicleChanged;
            Refresh();
        }

        private void OnDisable()
        {
            if (upgradeButton) upgradeButton.onClick.RemoveListener(OnUpgradeClicked);
            GameEvents.OnDockUnlocked -= OnDocksChanged;
            GameEvents.OnDollarsChanged -= OnDollarsChanged;
            GameEvents.OnVehicleAdded -= OnVehicleChanged;
        }

        private void OnDocksChanged(int _) => Refresh();
        private void OnDollarsChanged(int _) => Refresh();
        private void OnVehicleChanged(Entities.Vehicles.VehicleInstance _) => Refresh();

        private void OnUpgradeClicked()
        {
            var depot = ServiceLocator.Get<DepotSystem>();
            if (depot == null) return;
            if (!depot.TryUpgrade())
            {
                Debug.LogWarning("[Depot] Upgrade failed (insufficient funds).");
            }
        }

        private void Refresh()
        {
            var depot = ServiceLocator.Get<DepotSystem>();
            var fleet = ServiceLocator.Get<FleetSystem>();
            if (depot == null) return;

            int used = fleet != null ? fleet.Vehicles.Count : 0;
            int max = depot.MaxVehicleSlots;

            if (levelLabel) levelLabel.text = $"Dépôt - Niveau {depot.Level}";
            if (usageLabel) usageLabel.text = $"{used}/{max} camion{(max > 1 ? "s" : "")}";

            int cost = depot.GetNextUpgradeCost();
            if (nextCostLabel) nextCostLabel.text = $"Améliorer : ${cost:N0} (+1 emplacement)";

            if (upgradeButton) upgradeButton.interactable = depot.CanUpgrade();
        }
    }
}
