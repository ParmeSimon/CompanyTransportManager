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

            // Position: bottom bar floating with margin
            var rt = GetComponent<RectTransform>();
            if (rt == null) rt = gameObject.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.offsetMin = new Vector2(10, 8);
            rt.offsetMax = new Vector2(-10, 62);

            // Background pill
            var bg = GetComponent<Image>();
            if (bg == null) bg = gameObject.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.72f);
            bg.sprite = null;
            bg.type = Image.Type.Simple;

            // HorizontalLayoutGroup for tabs
            var hlg = GetComponent<HorizontalLayoutGroup>();
            if (hlg == null) hlg = gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(6, 6, 6, 6);
            hlg.spacing = 4;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;
            hlg.childAlignment = TextAnchor.MiddleCenter;

            (_mapBtn, _mapBg)      = AddTab("Carte",    "🗺");
            (_depotBtn, _depotBg)  = AddTab("Dépôt",   "🏭");
            (_vehiclesBtn, _vehiclesBg) = AddTab("Véhicules", "🚛");
            (_shopBtn, _shopBg)    = AddTab("Magasin",  "🛒");

            _mapBtn.onClick.AddListener(() => SelectTab(TabType.Map));
            _depotBtn.onClick.AddListener(() => SelectTab(TabType.Depot));
            _vehiclesBtn.onClick.AddListener(() => SelectTab(TabType.Vehicles));
            _shopBtn.onClick.AddListener(() => SelectTab(TabType.Shop));

            UpdateVisuals();
        }

        private (Button, Image) AddTab(string label, string emoji)
        {
            var go = new GameObject(label, typeof(RectTransform));
            go.transform.SetParent(transform, false);

            var bg = go.AddComponent<Image>();
            bg.color = Color.clear;
            bg.sprite = null;
            bg.type = Image.Type.Simple;

            var btn = go.AddComponent<Button>();
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.85f);
            colors.pressedColor = new Color(0.75f, 0.75f, 0.75f);
            btn.colors = colors;
            btn.targetGraphic = bg;

            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.spacing = 2;
            vlg.padding = new RectOffset(4, 4, 6, 6);

            // Emoji icon
            var iconGo = new GameObject("Icon", typeof(RectTransform));
            iconGo.transform.SetParent(go.transform, false);
            var iconTmp = iconGo.AddComponent<TextMeshProUGUI>();
            iconTmp.text = emoji;
            iconTmp.fontSize = 18;
            iconTmp.alignment = TextAlignmentOptions.Center;
            iconTmp.raycastTarget = false;
            iconGo.AddComponent<LayoutElement>().preferredHeight = 22;

            // Label
            var lblGo = new GameObject("Label", typeof(RectTransform));
            lblGo.transform.SetParent(go.transform, false);
            var lblTmp = lblGo.AddComponent<TextMeshProUGUI>();
            lblTmp.text = label;
            lblTmp.fontSize = 10;
            lblTmp.fontStyle = FontStyles.Bold;
            lblTmp.alignment = TextAlignmentOptions.Center;
            lblTmp.color = new Color(0.65f, 0.65f, 0.70f);
            lblTmp.raycastTarget = false;
            lblGo.AddComponent<LayoutElement>().preferredHeight = 14;

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

            // Update label color
            var lbl = btn.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
            if (lbl) lbl.color = active ? Color.white : new Color(0.55f, 0.55f, 0.60f);
            var icon = btn.transform.Find("Icon")?.GetComponent<TextMeshProUGUI>();
            if (icon) icon.color = active ? Color.white : new Color(0.55f, 0.55f, 0.60f);
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
