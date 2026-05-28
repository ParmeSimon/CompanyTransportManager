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
        private Button _hrBtn;
        private Button _shopBtn;

        private Image _mapBg;
        private Image _depotBg;
        private Image _vehiclesBg;
        private Image _hrBg;
        private Image _shopBg;

        private Sprite _sprR8, _sprR16;

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

            EnsureRoundedSprites();

            // Position: left side, centered vertically, fit-content height
            var rt = GetComponent<RectTransform>();
            if (rt == null) rt = gameObject.AddComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0, 0.5f);
            rt.anchorMax        = new Vector2(0, 0.5f);
            rt.pivot            = new Vector2(0, 0.5f);
            rt.anchoredPosition = new Vector2(12, 0);
            rt.sizeDelta        = new Vector2(110, 0);

            // Rounded background + drop shadow
            var bg = GetComponent<Image>();
            if (bg == null) bg = gameObject.AddComponent<Image>();
            bg.color         = new Color32(0x2C, 0x30, 0x38, 255);
            bg.sprite        = _sprR16;
            bg.type          = Image.Type.Sliced;
            bg.raycastTarget = true;
            var shadow = GetComponent<Shadow>();
            if (shadow == null) shadow = gameObject.AddComponent<Shadow>();
            shadow.effectColor    = new Color(0f, 0f, 0f, 0.5f);
            shadow.effectDistance = new Vector2(3f, -4f);

            var fitter = GetComponent<ContentSizeFitter>();
            if (fitter == null) fitter = gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            // VerticalLayoutGroup
            var vlg = GetComponent<VerticalLayoutGroup>();
            if (vlg == null) vlg = gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding               = new RectOffset(8, 8, 10, 10);
            vlg.spacing               = 4;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;
            vlg.childAlignment        = TextAnchor.UpperCenter;

            (_mapBtn, _mapBg)           = AddTab("Carte",     "UI/Icons/icons/map");
            (_depotBtn, _depotBg)       = AddTab("Dépôt",     "UI/Icons/icons/warehouse");
            (_vehiclesBtn, _vehiclesBg) = AddTab("Véhicules", "UI/Icons/icons/truck");
            (_hrBtn, _hrBg)             = AddTab("Pilote",    "UI/Icons/icons/driver");
            (_shopBtn, _shopBg)         = AddTab("Magasin",   "UI/Icons/icons/store");

            _mapBtn.onClick.AddListener(() => SelectTab(TabType.Map));
            _depotBtn.onClick.AddListener(() => SelectTab(TabType.Depot));
            _vehiclesBtn.onClick.AddListener(() => SelectTab(TabType.Vehicles));
            _hrBtn.onClick.AddListener(() => SelectTab(TabType.Hr));
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
            bg.sprite = _sprR8;
            bg.type   = Image.Type.Sliced;

            var btn = go.AddComponent<Button>();
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor      = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.85f);
            colors.pressedColor     = new Color(0.75f, 0.75f, 0.75f);
            btn.colors        = colors;
            btn.targetGraphic = bg;

            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 80;
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(80, 80);

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
            SetTabActive(_hrBg, _hrBtn, _activeTab == TabType.Hr);
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

        // ── Rounded sprite factory (9-slice) ───────────────────────────────────
        private void EnsureRoundedSprites()
        {
            if (_sprR16 != null) return;
            _sprR8  = MakeRoundedSprite(8);
            _sprR16 = MakeRoundedSprite(16);
        }

        private static Sprite MakeRoundedSprite(int radius)
        {
            const int size = 64;
            int r = Mathf.Clamp(radius, 1, size / 2);
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode   = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    pixels[y * size + x] = new Color(1f, 1f, 1f, RoundedAlpha(x, y, size, r));
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                                 SpriteMeshType.FullRect, new Vector4(r, r, r, r));
        }

        private static float RoundedAlpha(int x, int y, int size, int r)
        {
            int cx = -1, cy = -1;
            if      (x < r         && y < r)         { cx = r;        cy = r;        }
            else if (x >= size - r && y < r)         { cx = size - r; cy = r;        }
            else if (x < r         && y >= size - r) { cx = r;        cy = size - r; }
            else if (x >= size - r && y >= size - r) { cx = size - r; cy = size - r; }
            if (cx < 0) return 1f;
            float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
            return Mathf.Clamp01(r - d + 0.5f);
        }
    }
}
