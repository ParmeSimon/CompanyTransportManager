using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TransportManager.Enums;
using TransportManager.Events;

namespace TransportManager.UI.Common
{
    public class NavbarView : MonoBehaviour
    {
        private TabType _activeTab = TabType.Map;

        private Button _mapBtn;
        private Button _depotBtn;
        private Button _vehiclesBtn;
        private Button _shopBtn;

        private Image _mapBg;
        private Image _depotBg;
        private Image _vehiclesBg;
        private Image _shopBg;

        private void Awake() => Build();

#if UNITY_EDITOR
        [UnityEditor.MenuItem("CONTEXT/NavbarView/Build Navbar")]
        private static void BuildFromMenu(UnityEditor.MenuCommand cmd)
        {
            if (Application.isPlaying) { Debug.LogWarning("Stop Play Mode before building the Navbar."); return; }
            var n = (NavbarView)cmd.context;
            n.Build();
            UnityEditor.EditorUtility.SetDirty(n.gameObject);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(n.gameObject.scene);
        }
#endif

        private void Build()
        {
            var children = new System.Collections.Generic.List<GameObject>();
            foreach (Transform child in transform) children.Add(child.gameObject);
            foreach (var child in children)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying) DestroyImmediate(child);
                else Destroy(child);
#else
                Destroy(child);
#endif
            }

            // Remove old HorizontalLayoutGroup if present
            var oldHlg = GetComponent<HorizontalLayoutGroup>();
            if (oldHlg != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying) DestroyImmediate(oldHlg);
                else Destroy(oldHlg);
#else
                Destroy(oldHlg);
#endif
            }

            // Position: left side, centered vertically, fit-content height
            var rt = GetComponent<RectTransform>();
            if (rt == null) rt = gameObject.AddComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0, 0.5f);
            rt.anchorMax        = new Vector2(0, 0.5f);
            rt.pivot            = new Vector2(0, 0.5f);
            rt.anchoredPosition = new Vector2(10, 0);
            rt.sizeDelta        = new Vector2(90, 0);

            // Background fit-content
            var bg = GetComponent<Image>();
            if (bg == null) bg = gameObject.AddComponent<Image>();
            bg.color         = new Color32(0x2C, 0x30, 0x38, 255);
            bg.sprite        = null;
            bg.type          = Image.Type.Simple;
            bg.raycastTarget = false;

            var fitter = GetComponent<ContentSizeFitter>();
            if (fitter == null) fitter = gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            // VerticalLayoutGroup
            var vlg = GetComponent<VerticalLayoutGroup>();
            if (vlg == null) vlg = gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding               = new RectOffset(0, 0, 8, 8);
            vlg.spacing               = 0;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;
            vlg.childAlignment        = TextAnchor.UpperCenter;

            (_mapBtn, _mapBg)           = AddTab("Carte",     "UI/Icons/icons/map");
            (_depotBtn, _depotBg)       = AddTab("Dépôt",     "UI/Icons/icons/warehouse");
            (_vehiclesBtn, _vehiclesBg) = AddTab("Véhicules", "UI/Icons/icons/truck");
            (_shopBtn, _shopBg)         = AddTab("Magasin",   "UI/Icons/icons/store");

            _mapBtn.onClick.AddListener(() => SelectTab(TabType.Map));
            _depotBtn.onClick.AddListener(() => SelectTab(TabType.Depot));
            _vehiclesBtn.onClick.AddListener(() => SelectTab(TabType.Vehicles));
            _shopBtn.onClick.AddListener(() => SelectTab(TabType.Shop));

            UpdateVisuals();
        }

        private void AddHorizontalDivider()
        {
            var go  = new GameObject("Divider", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var img = go.AddComponent<Image>();
            img.color         = new Color32(0x3A, 0x3F, 0x4A, 160);
            img.raycastTarget = false;
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 1;
            le.preferredWidth  = 50;
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(50, 1);
        }

        private (Button, Image) AddTab(string label, string iconPath)
        {
            var go = new GameObject(label, typeof(RectTransform));
            go.transform.SetParent(transform, false);

            var bg = go.AddComponent<Image>();
            bg.color  = Color.clear;
            bg.sprite = null;
            bg.type   = Image.Type.Simple;

            var btn = go.AddComponent<Button>();
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor      = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.85f);
            colors.pressedColor     = new Color(0.75f, 0.75f, 0.75f);
            btn.colors        = colors;
            btn.targetGraphic = bg;

            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 90;
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(90, 90);

            var inner = go.AddComponent<VerticalLayoutGroup>();
            inner.childAlignment         = TextAnchor.MiddleCenter;
            inner.childForceExpandWidth  = false;
            inner.childForceExpandHeight = false;
            inner.childControlWidth      = false;
            inner.childControlHeight     = false;
            inner.spacing = 4;
            inner.padding = new RectOffset(4, 4, 8, 8);

            // Sprite icon
            var iconGo  = new GameObject("Icon", typeof(RectTransform));
            iconGo.transform.SetParent(go.transform, false);
            var iconImg = iconGo.AddComponent<Image>();
            iconImg.sprite         = Resources.Load<Sprite>(iconPath);
            iconImg.color          = new Color(0.55f, 0.55f, 0.60f);
            iconImg.preserveAspect = true;
            iconImg.raycastTarget  = false;
            var iconLe = iconGo.AddComponent<LayoutElement>();
            iconLe.minWidth        = 36;
            iconLe.preferredWidth  = 36;
            iconLe.minHeight       = 36;
            iconLe.preferredHeight = 36;
            iconGo.GetComponent<RectTransform>().sizeDelta = new Vector2(36, 36);

            // Label
            var lblGo  = new GameObject("Label", typeof(RectTransform));
            lblGo.transform.SetParent(go.transform, false);
            var lblTmp = lblGo.AddComponent<TextMeshProUGUI>();
            lblTmp.text          = label;
            lblTmp.fontSize      = 16;
            lblTmp.fontStyle     = FontStyles.Bold;
            lblTmp.alignment     = TextAlignmentOptions.Center;
            lblTmp.color         = new Color(0.55f, 0.55f, 0.60f);
            lblTmp.raycastTarget = false;
            var lblLe = lblGo.AddComponent<LayoutElement>();
            lblLe.preferredWidth  = 82;
            lblLe.preferredHeight = 22;
            lblGo.GetComponent<RectTransform>().sizeDelta = new Vector2(82, 22);

            return (btn, bg);
        }

        private void SelectTab(TabType tab)
        {
            _activeTab = tab;
            GameEvents.RaiseTabChanged(tab);
            UpdateVisuals();
        }

        private void UpdateVisuals()
        {
            SetTabActive(_mapBg, _mapBtn, _activeTab == TabType.Map);
            SetTabActive(_depotBg, _depotBtn, _activeTab == TabType.Depot);
            SetTabActive(_vehiclesBg, _vehiclesBtn, _activeTab == TabType.Vehicles);
            SetTabActive(_shopBg, _shopBtn, _activeTab == TabType.Shop);
        }

        private static void SetTabActive(Image bg, Button btn, bool active)
        {
            if (bg == null) return;
            bg.color = active ? new Color(1f, 1f, 1f, 0.12f) : Color.clear;

            var activeColor   = Color.white;
            var inactiveColor = new Color(0.55f, 0.55f, 0.60f);

            var lbl = btn.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
            if (lbl) lbl.color = active ? activeColor : inactiveColor;

            var icon = btn.transform.Find("Icon")?.GetComponent<Image>();
            if (icon) icon.color = active ? activeColor : inactiveColor;
        }

        private void OnEnable() => GameEvents.OnTabChanged += OnTabChanged;
        private void OnDisable() => GameEvents.OnTabChanged -= OnTabChanged;

        private void OnTabChanged(TabType tab)
        {
            _activeTab = tab;
            UpdateVisuals();
        }
    }
}
