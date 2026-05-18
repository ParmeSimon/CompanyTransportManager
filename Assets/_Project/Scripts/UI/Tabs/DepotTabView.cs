using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TransportManager.Core;
using TransportManager.Enums;
using TransportManager.Events;
using TransportManager.Systems.Depot;
using TransportManager.Systems.Fleet;
using TransportManager.Systems.Fuel;

namespace TransportManager.UI.Tabs
{
    public class DepotTabView : MonoBehaviour
    {
        // Hangar overlay
        private TMP_Text _hangarLevelLabel;
        private TMP_Text _hangarSlotsLabel;
        private Button _hangarUpgradeButton;
        private TMP_Text _hangarUpgradeCostLabel;

        // Fuel overlay
        private TMP_Text _fuelLitersLabel;
        private TMP_Text _fuelCapacityLabel;
        private Slider _fuelSlider;
        private Button _fuelNavButton;

        // HR overlay
        private TMP_Text _hrHiredLabel;
        private TMP_Text _hrPoolLabel;
        private Button _hrNavButton;

        private void Awake() => Build();

        private void Build()
        {
            foreach (Transform child in transform)
            {
#if UNITY_EDITOR
                DestroyImmediate(child.gameObject);
#else
                Destroy(child.gameObject);
#endif
            }

            // Stretch to fill parent
            var rt = GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            // --- Background image ---
            var bgGo = new GameObject("Background", typeof(RectTransform));
            bgGo.transform.SetParent(transform, false);
            var bgImg = bgGo.AddComponent<RawImage>();
            bgImg.texture = Resources.Load<Texture2D>("UI/DepotBackground");
            bgImg.color = Color.white;
            var bgRt = bgGo.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;

            // Semi-transparent dark scrim so overlays are readable
            var scrimGo = new GameObject("Scrim", typeof(RectTransform));
            scrimGo.transform.SetParent(transform, false);
            var scrimImg = scrimGo.AddComponent<Image>();
            scrimImg.color = new Color(0f, 0f, 0f, 0.18f);
            var scrimRt = scrimGo.GetComponent<RectTransform>();
            scrimRt.anchorMin = Vector2.zero;
            scrimRt.anchorMax = Vector2.one;
            scrimRt.offsetMin = Vector2.zero;
            scrimRt.offsetMax = Vector2.zero;

            // ---- Hangar overlay â€” top center ----
            // Anchored at ~50% x, ~72% y (warehouse building top area)
            var hangarOverlay = CreateOverlayPanel("HangarOverlay", transform);
            SetOverlayAnchor(hangarOverlay, new Vector2(0.50f, 0.72f));
            BuildHangarOverlay(hangarOverlay);

            // ---- Fuel overlay â€” right ----
            // Anchored at ~78% x, ~42% y (tank on the right)
            var fuelOverlay = CreateOverlayPanel("FuelOverlay", transform);
            SetOverlayAnchor(fuelOverlay, new Vector2(0.78f, 0.42f));
            BuildFuelOverlay(fuelOverlay);

            // ---- HR overlay â€” left ----
            // Anchored at ~22% x, ~45% y (admin building on the left)
            var hrOverlay = CreateOverlayPanel("HrOverlay", transform);
            SetOverlayAnchor(hrOverlay, new Vector2(0.22f, 0.45f));
            BuildHrOverlay(hrOverlay);
        }

        // ---- Overlay panel factory ----

        private GameObject CreateOverlayPanel(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var img = go.AddComponent<Image>();
            img.color = new Color(0.08f, 0.08f, 0.12f, 0.78f);
            img.sprite = null;
            img.type = Image.Type.Simple;

            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(12, 12, 10, 10);
            vlg.spacing = 6;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var csf = go.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            return go;
        }

        private void SetOverlayAnchor(GameObject overlay, Vector2 normalizedPos)
        {
            var rt = overlay.GetComponent<RectTransform>();
            rt.anchorMin = normalizedPos;
            rt.anchorMax = normalizedPos;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
        }

        // ---- Hangar overlay ----

        private void BuildHangarOverlay(GameObject panel)
        {
            AddOverlayTitle(panel, "Hangar");
            _hangarLevelLabel = AddOverlayValue(panel, "Niveau 1");
            _hangarSlotsLabel = AddOverlaySubtext(panel, "0 / 1 camions");
            _hangarUpgradeCostLabel = AddOverlaySubtext(panel, "$ 5 000");
            _hangarUpgradeButton = AddOverlayButton(panel, "AmÃ©liorer", new Color(0.29f, 0.56f, 0.89f));
            _hangarUpgradeButton.onClick.AddListener(OnHangarUpgrade);
        }

        // ---- Fuel overlay ----

        private void BuildFuelOverlay(GameObject panel)
        {
            AddOverlayTitle(panel, "Carburant");
            _fuelLitersLabel = AddOverlayValue(panel, "0 L");
            _fuelCapacityLabel = AddOverlaySubtext(panel, "/ 500 L");
            _fuelSlider = AddMiniSlider(panel, new Color(0.20f, 0.65f, 0.45f));
            _fuelNavButton = AddOverlayButton(panel, "GÃ©rer", new Color(0.20f, 0.65f, 0.45f));
            _fuelNavButton.onClick.AddListener(GoToFuel);
        }

        // ---- HR overlay ----

        private void BuildHrOverlay(GameObject panel)
        {
            AddOverlayTitle(panel, "Ressources Humaines");
            _hrHiredLabel = AddOverlayValue(panel, "0 conducteurs");
            _hrPoolLabel = AddOverlaySubtext(panel, "5 candidats");
            _hrNavButton = AddOverlayButton(panel, "Recruter", new Color(0.75f, 0.40f, 0.80f));
            _hrNavButton.onClick.AddListener(GoToHr);
        }

        // ---- Widget helpers ----

        private void AddOverlayTitle(GameObject panel, string text)
        {
            var go = new GameObject("Title", typeof(RectTransform));
            go.transform.SetParent(panel.transform, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text.ToUpper();
            tmp.fontSize = 10;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = new Color(0.7f, 0.7f, 0.75f);
            tmp.alignment = TextAlignmentOptions.Center;
            go.AddComponent<LayoutElement>().preferredHeight = 14;
        }

        private TMP_Text AddOverlayValue(GameObject panel, string text)
        {
            var go = new GameObject("Value", typeof(RectTransform));
            go.transform.SetParent(panel.transform, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 16;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            go.AddComponent<LayoutElement>().preferredHeight = 22;
            return tmp;
        }

        private TMP_Text AddOverlaySubtext(GameObject panel, string text)
        {
            var go = new GameObject("Sub", typeof(RectTransform));
            go.transform.SetParent(panel.transform, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 11;
            tmp.color = new Color(0.65f, 0.65f, 0.70f);
            tmp.alignment = TextAlignmentOptions.Center;
            go.AddComponent<LayoutElement>().preferredHeight = 16;
            return tmp;
        }

        private Slider AddMiniSlider(GameObject panel, Color fillColor)
        {
            var go = new GameObject("MiniSlider", typeof(RectTransform));
            go.transform.SetParent(panel.transform, false);
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 8;
            le.preferredWidth = 110;

            var bgImg = go.AddComponent<Image>();
            bgImg.color = new Color(0.25f, 0.25f, 0.30f);
            bgImg.sprite = null;
            bgImg.type = Image.Type.Simple;

            var fillAreaGo = new GameObject("FillArea", typeof(RectTransform));
            fillAreaGo.transform.SetParent(go.transform, false);
            var fillAreaRt = fillAreaGo.GetComponent<RectTransform>();
            fillAreaRt.anchorMin = Vector2.zero;
            fillAreaRt.anchorMax = Vector2.one;
            fillAreaRt.offsetMin = Vector2.zero;
            fillAreaRt.offsetMax = Vector2.zero;

            var fillGo = new GameObject("Fill", typeof(RectTransform));
            fillGo.transform.SetParent(fillAreaGo.transform, false);
            var fillImg = fillGo.AddComponent<Image>();
            fillImg.color = fillColor;
            fillImg.sprite = null;
            fillImg.type = Image.Type.Simple;
            var fillRt = fillGo.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;

            var slider = go.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0f;
            slider.interactable = false;
            slider.fillRect = fillRt;
            slider.targetGraphic = bgImg;

            return slider;
        }

        private Button AddOverlayButton(GameObject panel, string label, Color color)
        {
            var btnGo = new GameObject("Btn_" + label, typeof(RectTransform));
            btnGo.transform.SetParent(panel.transform, false);
            var btnImg = btnGo.AddComponent<Image>();
            btnImg.color = color;
            btnImg.sprite = null;
            btnImg.type = Image.Type.Simple;
            var btn = btnGo.AddComponent<Button>();
            var btnColors = btn.colors;
            btnColors.disabledColor = new Color(0.40f, 0.40f, 0.45f);
            btn.colors = btnColors;
            var le = btnGo.AddComponent<LayoutElement>();
            le.preferredHeight = 32;
            le.preferredWidth = 110;
            var hlg = btnGo.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            var lblGo = new GameObject("Label", typeof(RectTransform));
            lblGo.transform.SetParent(btnGo.transform, false);
            var tmp = lblGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 12;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;

            return btn;
        }

        // ---- Lifecycle ----

        private void OnEnable()
        {
            GameEvents.OnDockUnlocked += _ => Refresh();
            GameEvents.OnDollarsChanged += _ => Refresh();
            GameEvents.OnVehicleAdded += _ => Refresh();
            GameEvents.OnStationFuelChanged += _ => Refresh();
            GameEvents.OnDriverHired += _ => Refresh();
            GameEvents.OnDriverFired += _ => Refresh();
            GameEvents.OnDriverResigned += _ => Refresh();
            Refresh();
        }

        private void OnDisable()
        {
            GameEvents.OnDockUnlocked -= _ => Refresh();
            GameEvents.OnDollarsChanged -= _ => Refresh();
            GameEvents.OnVehicleAdded -= _ => Refresh();
            GameEvents.OnStationFuelChanged -= _ => Refresh();
            GameEvents.OnDriverHired -= _ => Refresh();
            GameEvents.OnDriverFired -= _ => Refresh();
            GameEvents.OnDriverResigned -= _ => Refresh();
        }

        private void OnHangarUpgrade()
        {
            var depot = ServiceLocator.Get<DepotSystem>();
            depot?.TryUpgrade();
        }

        private void GoToFuel() => GameEvents.RaiseTabChanged(TabType.Fuel);
        private void GoToHr() => GameEvents.RaiseTabChanged(TabType.Hr);

        private void Refresh()
        {
            RefreshHangar();
            RefreshFuel();
            RefreshHr();
        }

        private void RefreshHangar()
        {
            var depot = ServiceLocator.Get<DepotSystem>();
            var fleet = ServiceLocator.Get<FleetSystem>();
            if (depot == null) return;

            int used = fleet != null ? fleet.Vehicles.Count : 0;
            int max = depot.MaxVehicleSlots;
            int cost = depot.GetNextUpgradeCost();
            bool canUpgrade = depot.CanUpgrade();

            if (_hangarLevelLabel) _hangarLevelLabel.text = $"Niveau {depot.Level}";
            if (_hangarSlotsLabel) _hangarSlotsLabel.text = $"{used} / {max} camion{(max > 1 ? "s" : "")}";
            if (_hangarUpgradeCostLabel) _hangarUpgradeCostLabel.text = $"$ {cost:N0}";
            if (_hangarUpgradeButton)
            {
                _hangarUpgradeButton.interactable = canUpgrade;
                var lbl = _hangarUpgradeButton.GetComponentInChildren<TMP_Text>();
                if (lbl) lbl.text = canUpgrade ? "AmÃ©liorer" : "Fonds insuffisants";
            }
        }

        private void RefreshFuel()
        {
            var fuel = ServiceLocator.Get<FuelSystem>();
            if (fuel == null) return;

            float liters = fuel.CurrentLiters;
            float capacity = fuel.MaxCapacityLiters;
            float ratio = capacity > 0 ? liters / capacity : 0f;

            if (_fuelLitersLabel) _fuelLitersLabel.text = $"{liters:F0} L";
            if (_fuelCapacityLabel) _fuelCapacityLabel.text = $"/ {capacity:F0} L";
            if (_fuelSlider) _fuelSlider.value = ratio;
        }

        private void RefreshHr()
        {
            var hr = ServiceLocator.Get<TransportManager.Systems.Hr.HrSystem>();
            if (hr == null) return;

            int hired = hr.HiredDrivers.Count;
            int pool = hr.RecruitmentPool.Count;

            if (_hrHiredLabel) _hrHiredLabel.text = $"{hired} conducteur{(hired != 1 ? "s" : "")}";
            if (_hrPoolLabel) _hrPoolLabel.text = $"{pool} candidat{(pool != 1 ? "s" : "")}";
        }
    }
}
