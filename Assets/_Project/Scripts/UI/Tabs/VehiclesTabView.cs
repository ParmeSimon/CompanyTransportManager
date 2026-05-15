using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TransportManager.Core;
using TransportManager.Entities.Drivers;
using TransportManager.Entities.Vehicles;
using TransportManager.Events;
using TransportManager.Systems.Depot;
using TransportManager.Systems.Economy;
using TransportManager.Systems.Fleet;
using TransportManager.Systems.Hr;
using TransportManager.Systems.Progression;

namespace TransportManager.UI.Tabs
{
    public class VehiclesTabView : MonoBehaviour
    {
        [Header("UI Containers")]
        [SerializeField] private RectTransform fleetContainer;
        [SerializeField] private RectTransform garageContainer;

        [Header("Optional Headers")]
        [SerializeField] private TMP_Text fleetHeaderLabel;
        [SerializeField] private TMP_Text garageHeaderLabel;

        private readonly VehiclePurchaseService _purchase = new VehiclePurchaseService();
        private readonly List<GameObject> _spawnedRows = new List<GameObject>();

        private void OnEnable()
        {
            GameEvents.OnVehicleAdded += OnVehicleEvent;
            GameEvents.OnVehicleStatusChanged += OnVehicleEvent;
            GameEvents.OnVehicleFuelChanged += OnVehicleEvent;
            GameEvents.OnDollarsChanged += OnIntEvent;
            GameEvents.OnDockUnlocked += OnIntEvent;
            GameEvents.OnDriverAssigned += OnDriverEvent;
            GameEvents.OnDriverHired += OnDriverEvent;
            GameEvents.OnDriverFired += OnDriverEvent;
            GameEvents.OnDriverResigned += OnDriverEvent;
            GameEvents.OnCompanyXpChanged += OnCompanyXp;
            Refresh();
        }

        private void OnDisable()
        {
            GameEvents.OnVehicleAdded -= OnVehicleEvent;
            GameEvents.OnVehicleStatusChanged -= OnVehicleEvent;
            GameEvents.OnVehicleFuelChanged -= OnVehicleEvent;
            GameEvents.OnDollarsChanged -= OnIntEvent;
            GameEvents.OnDockUnlocked -= OnIntEvent;
            GameEvents.OnDriverAssigned -= OnDriverEvent;
            GameEvents.OnDriverHired -= OnDriverEvent;
            GameEvents.OnDriverFired -= OnDriverEvent;
            GameEvents.OnDriverResigned -= OnDriverEvent;
            GameEvents.OnCompanyXpChanged -= OnCompanyXp;
        }

        private void OnVehicleEvent(VehicleInstance _) => Refresh();
        private void OnDriverEvent(DriverInstance _) => Refresh();
        private void OnIntEvent(int _) => Refresh();
        private void OnCompanyXp(int xp, int level) => Refresh();

        private void Refresh()
        {
            ClearSpawned();

            var catalog = ServiceLocator.Get<VehicleCatalog>();
            var fleet = ServiceLocator.Get<FleetSystem>();
            var depot = ServiceLocator.Get<DepotSystem>();
            var wallet = ServiceLocator.Get<WalletSystem>();
            var hr = ServiceLocator.Get<HrSystem>();
            var xp = ServiceLocator.Get<XpSystem>();
            if (catalog == null) return;

            int used = depot != null ? depot.GetUsedSlots() : (fleet?.Vehicles.Count ?? 0);
            int max = depot != null ? depot.MaxVehicleSlots : 0;
            int companyLevel = xp != null ? xp.CompanyLevel : 1;

            if (fleetHeaderLabel) fleetHeaderLabel.text = $"Ma flotte ({used}/{max})   |   Niv. entreprise: {companyLevel}";
            if (garageHeaderLabel) garageHeaderLabel.text = "Garage";

            if (fleetContainer != null && fleet != null)
            {
                foreach (var v in fleet.Vehicles)
                {
                    var data = catalog.GetById(v.vehicleDataId);
                    if (data == null) continue;
                    var driver = hr != null && !string.IsNullOrEmpty(v.assignedDriverInstanceId)
                        ? hr.GetHired(v.assignedDriverInstanceId)
                        : null;
                    SpawnFleetRow(fleetContainer, v, data, driver);
                }
            }

            if (garageContainer != null)
            {
                bool depotFull = depot != null && !depot.HasRoomForOneMore();
                foreach (var data in catalog.vehicles)
                {
                    bool unlocked = xp == null || xp.IsVehicleUnlocked(data.minCompanyLevelRequired);
                    bool canAfford = wallet != null && wallet.CanAfford(Enums.CurrencyType.Dollar, data.purchasePrice);
                    SpawnGarageRow(garageContainer, data, unlocked, canAfford, depotFull);
                }
            }
        }

        private void ClearSpawned()
        {
            foreach (var go in _spawnedRows)
                if (go != null) Destroy(go);
            _spawnedRows.Clear();
        }

        private void SpawnFleetRow(Transform parent, VehicleInstance v, VehicleData data, DriverInstance driver)
        {
            var row = MakeRow(parent, $"Fleet_{v.instanceId}");
            float fuelPct = data.fuelTankCapacityLiters > 0
                ? v.currentFuelLiters / data.fuelTankCapacityLiters * 100f : 0f;
            float wearPct = data.maxKilometers > 0
                ? (float)v.totalKilometers / data.maxKilometers * 100f : 0f;

            string driverLine = driver != null
                ? $"Conducteur: <b>{driver.FullName}</b> (Niv {Entities.Progression.XpCurve.DriverLevelFromXp(driver.xp)}, salaire ${driver.assignedWagePerContract}/mission)"
                : "<color=#ff8866>⚠ Aucun conducteur assigné</color>";

            string info = $"<b>{data.displayName}</b> [{v.status}]\n" +
                          $"KM: {v.totalKilometers:N0}/{data.maxKilometers:N0} ({wearPct:0}%)  |  " +
                          $"Essence: {v.currentFuelLiters:0}/{data.fuelTankCapacityLiters:0} L ({fuelPct:0}%)\n" +
                          driverLine;
            AddInfoLabel(row, info);
            _spawnedRows.Add(row.gameObject);
        }

        private void SpawnGarageRow(Transform parent, VehicleData data, bool unlocked, bool canAfford, bool depotFull)
        {
            var row = MakeRow(parent, $"Garage_{data.id}");
            string lockLine = unlocked
                ? string.Empty
                : $"\n<color=#cc8855>🔒 Requiert niveau entreprise {data.minCompanyLevelRequired}</color>";

            string info = $"<b>{data.displayName}</b>  ({data.category})\n" +
                          $"Cap: {data.capacity}t  |  Vitesse: {data.speedKmh}km/h  |  Max: {data.maxKilometers:N0} km\n" +
                          $"Tank: {data.fuelTankCapacityLiters:0} L  |  Conso: {data.fuelConsumptionLPer100Km:0.0} L/100km  |  Range: {data.MaxRangeKm():0} km" +
                          lockLine;
            AddInfoLabel(row, info);
            AddBuyButton(row, data, unlocked && canAfford && !depotFull);
            _spawnedRows.Add(row.gameObject);
        }

        private static RectTransform MakeRow(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;

            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.15f, 0.15f, 0.18f, 0.85f);
            bg.raycastTarget = false;

            var h = go.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(12, 12, 8, 8);
            h.spacing = 10;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childAlignment = TextAnchor.MiddleLeft;

            var le = go.AddComponent<LayoutElement>();
            le.minHeight = 160;
            le.preferredHeight = 160;

            return rt;
        }

        private static void AddInfoLabel(RectTransform row, string text)
        {
            var go = new GameObject("Info", typeof(RectTransform));
            go.transform.SetParent(row, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 24;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.enableWordWrapping = true;
            tmp.richText = true;
            tmp.raycastTarget = false;

            var le = go.AddComponent<LayoutElement>();
            le.flexibleWidth = 1;
            le.minHeight = 140;
        }

        private void AddBuyButton(RectTransform row, VehicleData data, bool enabled)
        {
            var go = new GameObject("BuyButton", typeof(RectTransform));
            go.transform.SetParent(row, false);

            var img = go.AddComponent<Image>();
            img.color = enabled ? new Color(0.18f, 0.55f, 0.20f) : new Color(0.35f, 0.35f, 0.35f);

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.interactable = enabled;
            btn.onClick.AddListener(() => OnBuyClicked(data));

            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = 220;
            le.minHeight = 140;

            var textGO = new GameObject("Text", typeof(RectTransform));
            textGO.transform.SetParent(go.transform, false);
            var trt = (RectTransform)textGO.transform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;

            var label = textGO.AddComponent<TextMeshProUGUI>();
            label.text = $"Acheter\n${data.purchasePrice:N0}";
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 26;
            label.color = Color.white;
            label.raycastTarget = false;
        }

        private void OnBuyClicked(VehicleData data)
        {
            if (_purchase.TryPurchase(data, out var instance, out var error))
            {
                Debug.Log($"[Garage] Bought {data.displayName} (instance {instance.instanceId})");
            }
            else
            {
                Debug.LogWarning($"[Garage] Cannot buy {data.displayName}: {error}");
            }
        }
    }
}
