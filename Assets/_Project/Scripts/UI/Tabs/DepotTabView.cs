using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TransportManager.Core;
using TransportManager.Enums;
using TransportManager.Events;
using TransportManager.Systems.Buildings;
using TransportManager.Systems.Depot;
using TransportManager.Systems.Fleet;
using TransportManager.Systems.Fuel;
using TransportManager.Systems.Tutorial;

namespace TransportManager.UI.Tabs
{
    public class DepotTabView : MonoBehaviour
    {
        private RawImage _depotBgImage;
        private RectTransform _depotBgRect;
        private Texture2D _depotBgTex;
        private Vector2 _lastBgSize;

        private readonly System.Collections.Generic.Dictionary<string, Image> _buildingSprites = new System.Collections.Generic.Dictionary<string, Image>();

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

            // --- Background image (depot map) ---
            var bgGo = new GameObject("Background", typeof(RectTransform));
            bgGo.transform.SetParent(transform, false);
            var bgImg = bgGo.AddComponent<RawImage>();
            var bgTex = Resources.Load<Texture2D>("UI/depotMap") ?? Resources.Load<Texture2D>("UI/DepotBackground");
            bgImg.texture = bgTex;
            bgImg.color = Color.white;
            bgImg.raycastTarget = false;
            _depotBgImage = bgImg;
            _depotBgTex = bgTex;
            var bgRt = bgGo.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            _depotBgRect = bgRt;
            UpdateBackgroundUVs();

            // --- Building sprites layer (above the map, under overlays) ---
            BuildBuildingSprite(BuildingVisuals.Hangar, new Vector2(0.50f, 0.62f), new Vector2(170, 170));
            BuildBuildingSprite(BuildingVisuals.Office, new Vector2(0.22f, 0.35f), new Vector2(120, 120));
            BuildBuildingSprite(BuildingVisuals.FuelTank, new Vector2(0.78f, 0.32f), new Vector2(110, 110));

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

        private GameObject _hangarPanel, _fuelPanel, _hrPanel;

        private void BuildHangarOverlay(GameObject panel)
        {
            _hangarPanel = panel;
            TutorialTargetRegistry.Register("building:hangar", panel.GetComponent<RectTransform>());
            RebuildHangarOverlay();
        }

        private void RebuildHangarOverlay()
        {
            var panel = _hangarPanel;
            ClearChildren(panel);
            var buildings = ServiceLocator.Get<BuildingService>();
            int level = buildings != null ? buildings.GetLevel(BuildingVisuals.Hangar) : 1;

            if (level <= 0)
            {
                AddOverlayTitle(panel, "Hangar en ruine");
                AddOverlaySubtext(panel, "Réparez pour stocker des camions.");
                var repair = AddOverlayButton(panel, "Réparer", new Color(0.95f, 0.55f, 0.20f));
                repair.onClick.AddListener(() => OnRepair(BuildingVisuals.Hangar));
            }
            else
            {
                AddOverlayTitle(panel, "Hangar");
                _hangarLevelLabel = AddOverlayValue(panel, "Niveau " + level);
                _hangarSlotsLabel = AddOverlaySubtext(panel, "0 / 1 camions");
                _hangarUpgradeCostLabel = AddOverlaySubtext(panel, "$ 5 000");
                _hangarUpgradeButton = AddOverlayButton(panel, "Améliorer", new Color(0.29f, 0.56f, 0.89f));
                _hangarUpgradeButton.onClick.AddListener(OnHangarUpgrade);
            }
        }

        // ---- Fuel overlay ----

        private void BuildFuelOverlay(GameObject panel)
        {
            _fuelPanel = panel;
            TutorialTargetRegistry.Register("building:fuel_tank", panel.GetComponent<RectTransform>());
            RebuildFuelOverlay();
        }

        private void RebuildFuelOverlay()
        {
            var panel = _fuelPanel;
            ClearChildren(panel);
            var buildings = ServiceLocator.Get<BuildingService>();
            int level = buildings != null ? buildings.GetLevel(BuildingVisuals.FuelTank) : 1;

            if (level <= 0)
            {
                AddOverlayTitle(panel, "Cuve en ruine");
                AddOverlaySubtext(panel, "Réparez pour stocker du carburant.");
                var repair = AddOverlayButton(panel, "Réparer", new Color(0.95f, 0.55f, 0.20f));
                repair.onClick.AddListener(() => OnRepair(BuildingVisuals.FuelTank));
            }
            else
            {
                AddOverlayTitle(panel, "Carburant");
                _fuelLitersLabel = AddOverlayValue(panel, "0 L");
                _fuelCapacityLabel = AddOverlaySubtext(panel, "/ 500 L");
                _fuelSlider = AddMiniSlider(panel, new Color(0.20f, 0.65f, 0.45f));
                _fuelNavButton = AddOverlayButton(panel, "Gérer", new Color(0.20f, 0.65f, 0.45f));
                _fuelNavButton.onClick.AddListener(OnOpenFuel);
                TutorialTargetRegistry.Register("btn:open_fuel", _fuelNavButton.GetComponent<RectTransform>());
            }
        }

        // ---- HR overlay ----

        private void BuildHrOverlay(GameObject panel)
        {
            _hrPanel = panel;
            TutorialTargetRegistry.Register("building:office", panel.GetComponent<RectTransform>());
            RebuildHrOverlay();
        }

        private void RebuildHrOverlay()
        {
            var panel = _hrPanel;
            ClearChildren(panel);
            var buildings = ServiceLocator.Get<BuildingService>();
            int level = buildings != null ? buildings.GetLevel(BuildingVisuals.Office) : 1;

            if (level <= 0)
            {
                AddOverlayTitle(panel, "Bureau en ruine");
                AddOverlaySubtext(panel, "Réparez pour embaucher.");
                var repair = AddOverlayButton(panel, "Réparer", new Color(0.95f, 0.55f, 0.20f));
                repair.onClick.AddListener(() => OnRepair(BuildingVisuals.Office));
            }
            else
            {
                AddOverlayTitle(panel, "Ressources Humaines");
                _hrHiredLabel = AddOverlayValue(panel, "0 conducteurs");
                _hrPoolLabel = AddOverlaySubtext(panel, "5 candidats");
                _hrNavButton = AddOverlayButton(panel, "Recruter", new Color(0.75f, 0.40f, 0.80f));
                _hrNavButton.onClick.AddListener(OnOpenHr);
                TutorialTargetRegistry.Register("btn:open_hr", _hrNavButton.GetComponent<RectTransform>());
            }
        }

        private void OnRepair(string building)
        {
            var bs = ServiceLocator.Get<BuildingService>();
            if (bs == null) return;
            if (!bs.Repair(building)) return;
            if (building == BuildingVisuals.Hangar) RebuildHangarOverlay();
            else if (building == BuildingVisuals.FuelTank) RebuildFuelOverlay();
            else if (building == BuildingVisuals.Office) RebuildHrOverlay();
            RefreshBuildingSprite(building);
            Refresh();
        }

        private void BuildBuildingSprite(string building, Vector2 normalizedPos, Vector2 size)
        {
            var go = new GameObject($"Sprite_{building}", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var img = go.AddComponent<Image>();
            img.preserveAspect = true;
            img.raycastTarget = false;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = normalizedPos;
            rt.anchorMax = normalizedPos;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = Vector2.zero;
            _buildingSprites[building] = img;
            RefreshBuildingSprite(building);
        }

        private void RefreshBuildingSprite(string building)
        {
            if (!_buildingSprites.TryGetValue(building, out var img) || img == null) return;
            var bs = ServiceLocator.Get<BuildingService>();
            int level = bs != null ? bs.GetLevel(building) : 0;
            var sprite = BuildingVisuals.GetSprite(building, level);
            img.enabled = sprite != null;
            img.sprite = sprite;
        }

        private void OnOpenFuel()
        {
            GameEvents.RaiseFuelPanelOpened("depot");
            GoToFuel();
        }

        private void OnOpenHr()
        {
            GameEvents.RaiseHrPanelOpened("depot");
            GoToHr();
        }

        private static void ClearChildren(GameObject panel)
        {
            var toDestroy = new System.Collections.Generic.List<GameObject>();
            foreach (Transform child in panel.transform) toDestroy.Add(child.gameObject);
            foreach (var go in toDestroy)
#if UNITY_EDITOR
                DestroyImmediate(go);
#else
                Destroy(go);
#endif
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

        private void Update()
        {
            if (_depotBgRect == null) return;
            var size = _depotBgRect.rect.size;
            if (size != _lastBgSize)
            {
                _lastBgSize = size;
                UpdateBackgroundUVs();
            }
        }

        private void UpdateBackgroundUVs()
        {
            if (_depotBgImage == null || _depotBgTex == null || _depotBgRect == null) return;

            float containerAspect = _depotBgRect.rect.width / Mathf.Max(1f, _depotBgRect.rect.height);
            float texAspect = (float)_depotBgTex.width / Mathf.Max(1, _depotBgTex.height);

            float u = 0f, v = 0f, uw = 1f, vh = 1f;
            if (containerAspect > texAspect)
            {
                // Container plus large que la texture: crop le haut/bas
                vh = texAspect / containerAspect;
                v = (1f - vh) * 0.5f;
            }
            else
            {
                // Container plus haut que la texture: crop gauche/droite
                uw = containerAspect / texAspect;
                u = (1f - uw) * 0.5f;
            }
            _depotBgImage.uvRect = new Rect(u, v, uw, vh);
        }
    }
}
