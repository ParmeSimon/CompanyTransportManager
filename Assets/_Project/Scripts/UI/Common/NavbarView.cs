using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TransportManager.Core;
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

        private const float Bleed = 16f;
        private static readonly Color32 RailBg = new Color32(0x22, 0x26, 0x2E, 255);   // = barre du haut
        private static readonly Color32 Accent = new Color32(0x38, 0x85, 0xFF, 255);
        private Rect _lastSafeArea;
        private Canvas _canvas;

        private void Awake() => Build();

        // Décale la sidebar pour ne pas passer sous l'encoche / Dynamic Island (côté gauche en paysage).
        private void ApplySafeArea()
        {
            if (_canvas == null) _canvas = GetComponentInParent<Canvas>();
            Vector4 ins = SafeAreaUtil.Insets(_canvas);
            var rt = GetComponent<RectTransform>();
            // Rail collé aux bords gauche et bas, juste sous la barre du haut (débord de Bleed
            // hors écran → seul le coin haut-droit arrondi est visible).
            rt.offsetMin = new Vector2(-Bleed, -Bleed);
            rt.offsetMax = new Vector2(ins.x + SafeAreaUtil.NavbarWidth, -(ins.z + HeaderView.HeaderHeight));
            var vlgSa = GetComponent<VerticalLayoutGroup>();
            if (vlgSa != null)
                vlgSa.padding = new RectOffset((int)(Bleed + ins.x) + 6, 6, 12, (int)(Bleed + ins.w) + 10);
            _lastSafeArea = Screen.safeArea;
        }

        private void LateUpdate()
        {
            if (Screen.safeArea != _lastSafeArea) ApplySafeArea();
        }

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
            rt.anchorMin        = new Vector2(0, 0);
            rt.anchorMax        = new Vector2(0, 1);
            rt.pivot            = new Vector2(0, 0.5f);
            ApplySafeArea();   // décale la barre vers la droite si l'encoche est à gauche (paysage)

            // Fond droit (sans arrondi) : le rail prolonge la barre du haut + ombre portée
            var bg = GetComponent<Image>();
            if (bg == null) bg = gameObject.AddComponent<Image>();
            bg.color         = RailBg;
            bg.sprite        = null;
            bg.type          = Image.Type.Simple;
            bg.raycastTarget = true;
            // Pas d ombre portée (effet dur) : filet clair de 1 px sur le bord droit.
            var oldShadow = GetComponent<Shadow>();
            if (oldShadow != null) { if (Application.isPlaying) Destroy(oldShadow); else DestroyImmediate(oldShadow); }
            var edge = new GameObject("Edge", typeof(RectTransform));
            edge.transform.SetParent(transform, false);
            var edgeImg = edge.AddComponent<Image>();
            edgeImg.color = new Color(1f, 1f, 1f, 0.07f); edgeImg.raycastTarget = false;
            edge.AddComponent<LayoutElement>().ignoreLayout = true;
            var edgeRt = edge.GetComponent<RectTransform>();
            edgeRt.anchorMin = new Vector2(1, 0); edgeRt.anchorMax = new Vector2(1, 1);
            edgeRt.pivot = new Vector2(1, 0.5f); edgeRt.sizeDelta = new Vector2(1, 0);
            edgeRt.anchoredPosition = Vector2.zero;

            var fitter = GetComponent<ContentSizeFitter>();
            if (fitter == null) fitter = gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit   = ContentSizeFitter.FitMode.Unconstrained;   // hauteur = écran
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            // VerticalLayoutGroup
            var vlg = GetComponent<VerticalLayoutGroup>();
            if (vlg == null) vlg = gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing               = 6;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth      = true;
            vlg.childControlHeight     = true;
            vlg.childAlignment        = TextAnchor.UpperCenter;
            ApplySafeArea();   // marges intérieures (encoche / bas d'écran)

            (_mapBtn, _mapBg)           = AddTab("Carte",     "UI/Icons/icons/map");
            (_depotBtn, _depotBg)       = AddTab("Dépôt",     "UI/Icons/icons/warehouse");
            (_vehiclesBtn, _vehiclesBg) = AddTab("Camions",   "UI/Icons/icons/truck");
            (_hrBtn, _hrBg)             = AddTab("Pilote",    "UI/Icons/icons/driver");
            (_shopBtn, _shopBg)         = AddTab("Shop",      "UI/Icons/icons/store");

            _mapBtn.onClick.AddListener(() => SelectTab(TabType.Map));
            _depotBtn.onClick.AddListener(() => SelectTab(TabType.Depot));
            _vehiclesBtn.onClick.AddListener(() => SelectTab(TabType.Vehicles));
            _hrBtn.onClick.AddListener(() => SelectTab(TabType.Hr));
            _shopBtn.onClick.AddListener(() => SelectTab(TabType.Shop));

            // Espace libre, puis les accès « quotidiens » avec compteur rouge (toutes les pages).
            var spacer = new GameObject("Spacer", typeof(RectTransform));
            spacer.transform.SetParent(transform, false);
            spacer.AddComponent<LayoutElement>().flexibleHeight = 1;
            AddHorizontalDivider();
            (_dailyBadge, _dailyBadgeLbl) = AddUtility("Objectifs", null, HeaderView.TargetSprite(),
                TransportManager.UI.Daily.DailyHubPopupView.Show);
            (_skillsBadge, _skillsBadgeLbl) = AddUtility("Compétences", "UI/Icons/icons/research", null,
                TransportManager.UI.Skills.SkillTreePopupView.Show);

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
            le.preferredHeight = 68; le.minHeight = 68;
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(80, 68);

            var accent = new GameObject("Accent", typeof(RectTransform));
            accent.transform.SetParent(go.transform, false);
            var accImg = accent.AddComponent<Image>();
            accImg.sprite = _sprR8; accImg.type = Image.Type.Sliced;
            accImg.color = Accent; accImg.raycastTarget = false;
            accent.AddComponent<LayoutElement>().ignoreLayout = true;
            var accRt = accent.GetComponent<RectTransform>();
            accRt.anchorMin = new Vector2(0, 0.2f); accRt.anchorMax = new Vector2(0, 0.8f);
            accRt.pivot = new Vector2(0, 0.5f); accRt.sizeDelta = new Vector2(4, 0); accRt.anchoredPosition = new Vector2(-4, 0);
            accent.SetActive(false);

            var inner = go.AddComponent<VerticalLayoutGroup>();
            inner.childAlignment         = TextAnchor.MiddleCenter;
            inner.childForceExpandWidth  = false;
            inner.childForceExpandHeight = false;
            inner.childControlWidth      = false;
            inner.childControlHeight     = false;
            inner.spacing = 4;
            inner.padding = new RectOffset(4, 4, 6, 6);

            // Sprite icon
            var iconGo  = new GameObject("Icon", typeof(RectTransform));
            iconGo.transform.SetParent(go.transform, false);
            var iconImg = iconGo.AddComponent<Image>();
            UiIcons.Apply(iconImg, iconPath);
            iconImg.color          = new Color(0.55f, 0.55f, 0.60f);
            iconImg.preserveAspect = true;
            iconImg.raycastTarget  = false;
            var iconLe = iconGo.AddComponent<LayoutElement>();
            iconLe.minWidth        = 32;
            iconLe.preferredWidth  = 32;
            iconLe.minHeight       = 32;
            iconLe.preferredHeight = 32;
            iconGo.GetComponent<RectTransform>().sizeDelta = new Vector2(32, 32);

            // Label
            var lblGo  = new GameObject("Label", typeof(RectTransform));
            lblGo.transform.SetParent(go.transform, false);
            var lblTmp = lblGo.AddComponent<TextMeshProUGUI>();
            lblTmp.text          = label;
            lblTmp.fontSize      = 16;
            lblTmp.fontStyle     = FontStyles.Bold;
            lblTmp.alignment     = TextAlignmentOptions.Center;
            lblTmp.color         = new Color(0.55f, 0.55f, 0.60f);
            // Toujours sur une seule ligne : rétrécit un peu si le libellé est trop large.
            lblTmp.textWrappingMode = TextWrappingModes.NoWrap;
            lblTmp.enableAutoSizing = true;
            lblTmp.fontSizeMin      = 12;
            lblTmp.fontSizeMax      = 16;
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
            bg.color = active ? new Color(1f, 1f, 1f, 0.10f) : Color.clear;
            var acc = btn.transform.Find("Accent");
            if (acc != null) acc.gameObject.SetActive(active);

            var activeColor   = Color.white;   // + barre d'accent bleue
            var inactiveColor = new Color(0.55f, 0.55f, 0.60f);

            var lbl = btn.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
            if (lbl) lbl.color = active ? activeColor : inactiveColor;

            var icon = btn.transform.Find("Icon")?.GetComponent<Image>();
            if (icon) icon.color = active ? activeColor : inactiveColor;
        }

        private GameObject _dailyBadge, _skillsBadge;
        private TMP_Text   _dailyBadgeLbl, _skillsBadgeLbl;
        private float      _nextBadge;

        private (GameObject, TMP_Text) AddUtility(string label, string iconPath, Sprite sprite, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(label, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var bg = go.AddComponent<Image>();
            bg.sprite = _sprR8; bg.type = Image.Type.Sliced;
            bg.color = new Color(1f, 1f, 1f, 0.05f);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.onClick.AddListener(onClick);
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 60; le.minHeight = 60;

            var inner = go.AddComponent<VerticalLayoutGroup>();
            inner.childAlignment = TextAnchor.MiddleCenter;
            inner.childForceExpandWidth = false; inner.childForceExpandHeight = false;
            inner.childControlWidth = true; inner.childControlHeight = true;
            inner.spacing = 3; inner.padding = new RectOffset(2, 2, 5, 5);

            var iconGo = new GameObject("Icon", typeof(RectTransform));
            iconGo.transform.SetParent(go.transform, false);
            var iconImg = iconGo.AddComponent<Image>();
            if (sprite != null) iconImg.sprite = sprite; else UiIcons.Apply(iconImg, iconPath);
            iconImg.color = new Color32(0xEC, 0xEE, 0xF5, 255);
            iconImg.preserveAspect = true; iconImg.raycastTarget = false;
            var iLe = iconGo.AddComponent<LayoutElement>();
            iLe.preferredWidth = 26; iLe.preferredHeight = 26;

            var lblGo = new GameObject("Label", typeof(RectTransform));
            lblGo.transform.SetParent(go.transform, false);
            var lbl = lblGo.AddComponent<TextMeshProUGUI>();
            lbl.text = label; lbl.fontSize = 11; lbl.fontStyle = FontStyles.Bold;
            lbl.alignment = TextAlignmentOptions.Center; lbl.color = new Color32(0xEC, 0xEE, 0xF5, 255);
            lbl.textWrappingMode = TextWrappingModes.NoWrap;
            lbl.enableAutoSizing = true; lbl.fontSizeMin = 9; lbl.fontSizeMax = 11;
            lbl.raycastTarget = false;

            // Compteur rouge en coin
            var badge = new GameObject("Badge", typeof(RectTransform));
            badge.transform.SetParent(go.transform, false);
            var bImg = badge.AddComponent<Image>();
            bImg.sprite = _sprR8; bImg.type = Image.Type.Sliced;
            bImg.color = new Color32(0xE5, 0x3E, 0x3E, 255); bImg.raycastTarget = false;
            badge.AddComponent<LayoutElement>().ignoreLayout = true;
            var bRt = badge.GetComponent<RectTransform>();
            bRt.anchorMin = bRt.anchorMax = new Vector2(1f, 1f);
            bRt.pivot = new Vector2(1f, 1f); bRt.anchoredPosition = new Vector2(-2f, -2f);
            bRt.sizeDelta = new Vector2(26f, 22f);
            var nGo = new GameObject("N", typeof(RectTransform));
            nGo.transform.SetParent(badge.transform, false);
            var n = nGo.AddComponent<TextMeshProUGUI>();
            n.text = "0"; n.fontSize = 11; n.fontStyle = FontStyles.Bold; n.color = Color.white;
            n.alignment = TextAlignmentOptions.Center; n.raycastTarget = false;
            var nRt = nGo.GetComponent<RectTransform>();
            nRt.anchorMin = Vector2.zero; nRt.anchorMax = Vector2.one; nRt.offsetMin = Vector2.zero; nRt.offsetMax = Vector2.zero;
            badge.SetActive(false);
            return (badge, n);
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextBadge) return;
            _nextBadge = Time.unscaledTime + 1.5f;
            SetBadge(_dailyBadge, _dailyBadgeLbl, HeaderView.DailyPendingCount());
            SetBadge(_skillsBadge, _skillsBadgeLbl, HeaderView.SkillPointsCount());
        }

        private static void SetBadge(GameObject b, TMP_Text l, int count)
        {
            if (b == null) return;
            b.SetActive(count > 0);
            if (count > 0 && l != null) l.text = count > 9 ? "9+" : count.ToString();
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
