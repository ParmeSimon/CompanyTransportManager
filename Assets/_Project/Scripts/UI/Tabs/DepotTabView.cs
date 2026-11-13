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
using TransportManager.UI.Depot;

namespace TransportManager.UI.Tabs
{
    public class DepotTabView : MonoBehaviour
    {
        // Dépôt 3D (DepotScene3D) rendu dans une texture, affichée plein écran derrière les panneaux.
        private DepotScene3D _scene;
        private RawImage _view;
        private RenderTexture _rt;
        private Vector2Int _rtSize;
        private Vector2 _framedSize;
        private RectTransform _overlayLayer;
        private readonly System.Collections.Generic.Dictionary<string, RectTransform> _overlays = new System.Collections.Generic.Dictionary<string, RectTransform>();

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
        private Button _fuelUpgradeButton;
        private TMP_Text _fuelUpgradeCostLabel;

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

            // --- Rendu du dépôt 3D : terrain, parkings, route et les 3 bâtiments, construits en code. ---
            _scene = new DepotScene3D();
            var viewGo = new GameObject("DepotView", typeof(RectTransform));
            viewGo.transform.SetParent(transform, false);
            _view = viewGo.AddComponent<RawImage>();
            _view.color = Color.white;
            _view.raycastTarget = false;
            StretchRect(viewGo.GetComponent<RectTransform>());

            // --- Panneaux d'info : chacun suit son emplacement à l'écran (voir PlaceOverlays). ---
            var layerGo = new GameObject("Overlays", typeof(RectTransform));
            layerGo.transform.SetParent(transform, false);
            _overlayLayer = layerGo.GetComponent<RectTransform>();
            StretchRect(_overlayLayer);

            var hangarOverlay = CreateOverlayPanel("HangarOverlay", _overlayLayer);
            _overlays[BuildingVisuals.Hangar] = hangarOverlay.GetComponent<RectTransform>();
            BuildHangarOverlay(hangarOverlay);

            var fuelOverlay = CreateOverlayPanel("FuelOverlay", _overlayLayer);
            _overlays[BuildingVisuals.FuelTank] = fuelOverlay.GetComponent<RectTransform>();
            BuildFuelOverlay(fuelOverlay);

            var hrOverlay = CreateOverlayPanel("HrOverlay", _overlayLayer);
            _overlays[BuildingVisuals.Office] = hrOverlay.GetComponent<RectTransform>();
            BuildHrOverlay(hrOverlay);

            foreach (var o in _overlays.Values) o.pivot = new Vector2(0.5f, 1f);
#if UNITY_EDITOR
            BuildPreviewBar();
#endif
            RefreshBuildingModel(BuildingVisuals.Hangar);
            RefreshBuildingModel(BuildingVisuals.FuelTank);
            RefreshBuildingModel(BuildingVisuals.Office);
        }

        // ── Rendu 3D : texture à la taille de l'écran, cadrage hors HUD, panneaux ─────────

        private Canvas RootCanvas
        {
            get { var c = GetComponentInParent<Canvas>(); return c != null ? c.rootCanvas : null; }
        }

        // Partie de l'écran NON cachée par le HUD (rail à gauche, barre en haut), en fraction du rendu.
        private Rect FreeArea(Vector2 view)
        {
            var canvas = RootCanvas;
            float left = SafeAreaUtil.SidebarReserve(canvas, 8f) / view.x;
            float top = SafeAreaUtil.HudTopReserve(canvas) / view.y;
            const float pad = 0.02f;
            return Rect.MinMaxRect(left + pad, pad, 1f - pad, 1f - top - pad);
        }

        private void LateUpdate()
        {
            if (_scene == null) return;
            Vector2 view = ((RectTransform)transform).rect.size;
            if (view.x <= 1f || view.y <= 1f) return;

            // Texture de rendu à la résolution réelle de l'écran (recréée si la taille change).
            var canvas = RootCanvas;
            float sf = canvas != null && canvas.scaleFactor > 0.0001f ? canvas.scaleFactor : 1f;
            var px = new Vector2Int(Mathf.Clamp(Mathf.RoundToInt(view.x * sf), 64, 2560), Mathf.Clamp(Mathf.RoundToInt(view.y * sf), 64, 2560));
            if (_rt == null || px != _rtSize)
            {
                ReleaseTexture();
                _rt = new RenderTexture(px.x, px.y, 24, RenderTextureFormat.ARGB32)
                {
                    antiAliasing = Application.isMobilePlatform ? 2 : 4,
                    name = "DepotView",
                };
                _rt.Create();
                _rtSize = px;
                _scene.Camera.targetTexture = _rt;
                _view.texture = _rt;
            }
            if (view != _framedSize)
            {
                _scene.Frame(view.x / view.y, FreeArea(view));
                _framedSize = view;
            }
            _scene.Tick(Time.unscaledDeltaTime);
            PlaceOverlays(view);
        }

        // Chaque panneau est accroché au coin avant de son emplacement, puis ramené dans
        // la zone visible s'il en déborde (jamais sous le rail ni sous la barre du haut).
        private void PlaceOverlays(Vector2 view)
        {
            var canvas = RootCanvas;
            float minX = SafeAreaUtil.SidebarReserve(canvas, 8f);
            float maxY = view.y - SafeAreaUtil.HudTopReserve(canvas);
            foreach (var kv in _overlays)
            {
                var rt = kv.Value;
                if (rt == null) continue;
                Vector3 vp = _scene.Camera.WorldToViewportPoint(_scene.PanelAnchor(kv.Key));
                rt.anchorMin = rt.anchorMax = Vector2.zero;
                Vector2 size = rt.rect.size;
                float x = Mathf.Clamp(vp.x * view.x, minX + size.x * 0.5f, view.x - 8f - size.x * 0.5f);
                float y = Mathf.Clamp(vp.y * view.y + size.y * 0.35f, size.y + 8f, maxY);
                rt.anchoredPosition = new Vector2(x, y);
            }
        }

        private void ReleaseTexture()
        {
            if (_rt == null) return;
            if (_scene != null) _scene.Camera.targetTexture = null;
            if (_view != null) _view.texture = null;
            _rt.Release();
            Destroy(_rt);
            _rt = null;
        }

        private void OnDestroy()
        {
            ReleaseTexture();
            _scene?.Dispose();
            _scene = null;
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
                _fuelUpgradeCostLabel = AddOverlaySubtext(panel, "");
                _fuelUpgradeButton = AddOverlayButton(panel, "Améliorer", new Color(0.29f, 0.56f, 0.89f));
                _fuelUpgradeButton.onClick.AddListener(OnFuelUpgrade);
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
            RefreshBuildingModel(building);
            Refresh();
            GameManager.Instance?.SaveNow();
        }

        // Le modèle 3D suit le niveau visuel du bâtiment ; petit « pop » s'il change à l'écran.
        private void RefreshBuildingModel(string building)
        {
            if (_scene == null) return;
            var bs = ServiceLocator.Get<BuildingService>();
            int level = bs != null ? bs.GetVisualLevel(building) : 0;
            if (_previewLevels.TryGetValue(building, out int forced)) level = forced;
            bool changed = _shownLevels.TryGetValue(building, out int previous) && previous != level;
            _shownLevels[building] = level;
            _scene.SetLevel(building, level, changed && isActiveAndEnabled);
        }

        private readonly System.Collections.Generic.Dictionary<string, int> _shownLevels = new System.Collections.Generic.Dictionary<string, int>();

        // ── Aperçu des designs (éditeur uniquement) : force le niveau AFFICHÉ, sans toucher la sauvegarde ──
        private readonly System.Collections.Generic.Dictionary<string, int> _previewLevels = new System.Collections.Generic.Dictionary<string, int>();

        private void BuildPreviewBar()
        {
            var bar = new GameObject("DesignPreview", typeof(RectTransform));
            bar.transform.SetParent(transform, false);
            var rt = bar.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-10f, 10f);
            bar.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.12f, 0.85f);
            var vlg = bar.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(10, 10, 8, 8);
            vlg.spacing = 4;
            vlg.childForceExpandHeight = false;
            var csf = bar.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            AddOverlaySubtext(bar, "Aperçu des designs (éditeur)");
            AddPreviewRow(bar, BuildingVisuals.Hangar, "Garage", 6);
            AddPreviewRow(bar, BuildingVisuals.FuelTank, "Station", 10);
            AddPreviewRow(bar, BuildingVisuals.Office, "RH", 3);
        }

        private void AddPreviewRow(GameObject bar, string building, string name, int max)
        {
            var row = new GameObject("Row_" + name, typeof(RectTransform));
            row.transform.SetParent(bar.transform, false);
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 6;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            row.AddComponent<LayoutElement>().preferredHeight = 30;

            var lblGo = new GameObject("Label", typeof(RectTransform));
            lblGo.transform.SetParent(row.transform, false);
            var lbl = lblGo.AddComponent<TextMeshProUGUI>();
            lbl.fontSize = 13;
            lbl.color = Color.white;
            lbl.alignment = TextAlignmentOptions.MidlineLeft;
            var le = lblGo.AddComponent<LayoutElement>();
            le.preferredWidth = 120;
            le.preferredHeight = 30;

            void Show()
            {
                lbl.text = _previewLevels.TryGetValue(building, out int l) ? $"{name} : niv. {l}" : $"{name} : réel";
                RefreshBuildingModel(building);
            }
            void Step(int delta)
            {
                int cur = _previewLevels.TryGetValue(building, out int l) ? l
                        : (ServiceLocator.Get<BuildingService>()?.GetVisualLevel(building) ?? 0);
                int next = Mathf.Clamp(cur + delta, 0, max);
                _previewLevels[building] = next;
                Show();
            }

            PreviewButton(row, "<", () => Step(-1));
            PreviewButton(row, ">", () => Step(+1));
            PreviewButton(row, "Réel", () => { _previewLevels.Remove(building); Show(); });
            Show();
        }

        private static void PreviewButton(GameObject row, string label, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Btn_" + label, typeof(RectTransform));
            go.transform.SetParent(row.transform, false);
            go.AddComponent<Image>().color = new Color(0.29f, 0.56f, 0.89f);
            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(onClick);
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = label.Length > 1 ? 52 : 34;
            le.preferredHeight = 30;
            var txtGo = new GameObject("Label", typeof(RectTransform));
            txtGo.transform.SetParent(go.transform, false);
            var txt = txtGo.AddComponent<TextMeshProUGUI>();
            txt.text = label;
            txt.fontSize = 14;
            txt.fontStyle = FontStyles.Bold;
            txt.color = Color.white;
            txt.alignment = TextAlignmentOptions.Center;
            StretchRect(txtGo.GetComponent<RectTransform>());
        }

        private static void StretchRect(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private void OnOpenFuel()
        {
            // Popup uniquement : on NE change PAS de page → on reste sur le Dépôt à la fermeture.
            GameEvents.RaiseFuelPanelOpened("depot");
            FuelOrderPopupView.Show();
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
            GameEvents.OnDockUnlocked += OnIntEvent;
            GameEvents.OnDollarsChanged += OnIntEvent;
            GameEvents.OnVehicleAdded += OnVehicleEvent;
            GameEvents.OnStationFuelChanged += OnFloatEvent;
            GameEvents.OnDriverHired += OnDriverEvent;
            GameEvents.OnDriverFired += OnDriverEvent;
            GameEvents.OnDriverResigned += OnDriverEvent;
            GameEvents.OnDriverDied += OnDriverEvent;
            GameEvents.OnPumpUpgraded += OnIntEvent;
            _scene?.SetActive(true);
            _framedSize = Vector2.zero;   // recadrer à la prochaine image
            Refresh();
        }

        private void OnDisable()
        {
            GameEvents.OnDockUnlocked -= OnIntEvent;
            GameEvents.OnDollarsChanged -= OnIntEvent;
            GameEvents.OnVehicleAdded -= OnVehicleEvent;
            GameEvents.OnStationFuelChanged -= OnFloatEvent;
            GameEvents.OnDriverHired -= OnDriverEvent;
            GameEvents.OnDriverFired -= OnDriverEvent;
            GameEvents.OnDriverResigned -= OnDriverEvent;
            GameEvents.OnDriverDied -= OnDriverEvent;
            GameEvents.OnPumpUpgraded -= OnIntEvent;
            _scene?.SetActive(false);   // plus de rendu 3D quand l'onglet est caché
        }

        // Méthodes nommées : un lambda ne peut pas être désabonné (un « -= _ => … » crée
        // un nouveau délégué et ne retire rien), les handlers s'empilaient à chaque ouverture.
        private void OnIntEvent(int _) => Refresh();
        private void OnFloatEvent(float _) => Refresh();
        private void OnVehicleEvent(Entities.Vehicles.VehicleInstance _) => Refresh();
        private void OnDriverEvent(Entities.Drivers.DriverInstance _) => Refresh();

        private void OnHangarUpgrade()
        {
            var depot = ServiceLocator.Get<DepotSystem>();
            if (depot != null && depot.TryUpgrade()) GameManager.Instance?.SaveNow();
        }

        private void GoToFuel() => GameEvents.RaiseTabChanged(TabType.Fuel);
        private void GoToHr() => GameEvents.RaiseTabChanged(TabType.Hr);

        private void Refresh()
        {
            // Le look suit le niveau : chaque modèle 3D est mis à jour.
            RefreshBuildingModel(BuildingVisuals.Hangar);
            RefreshBuildingModel(BuildingVisuals.FuelTank);
            RefreshBuildingModel(BuildingVisuals.Office);
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
                if (lbl) lbl.text = canUpgrade ? "Améliorer" : "Fonds insuffisants";
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

            // Amélioration de la cuve (palier de pompe suivant) → change aussi son look.
            var next = fuel.Config != null ? fuel.Config.GetNextTier(fuel.State.pumpLevel) : null;
            if (_fuelUpgradeCostLabel)
                _fuelUpgradeCostLabel.text = next != null ? $"Niv. {next.level} : $ {next.upgradeCostDollars:N0}" : "Niveau max";
            if (_fuelUpgradeButton)
            {
                _fuelUpgradeButton.gameObject.SetActive(next != null);
                bool canUpgrade = fuel.CanUpgradePump();
                _fuelUpgradeButton.interactable = canUpgrade;
                var lbl = _fuelUpgradeButton.GetComponentInChildren<TMP_Text>();
                if (lbl) lbl.text = canUpgrade ? "Améliorer" : "Fonds insuffisants";
            }
        }

        private void OnFuelUpgrade()
        {
            var fuel = ServiceLocator.Get<FuelSystem>();
            if (fuel != null && fuel.TryUpgradePump()) GameManager.Instance?.SaveNow();
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
