using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TransportManager.Core;
using TransportManager.Events;
using TransportManager.UI.Analytics;
using TransportManager.UI.Fleet;
using TransportManager.UI.Friends;
using TransportManager.UI.Settings;
using TransportManager.UI.Skills;

namespace TransportManager.UI.Common
{
    public class HeaderView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _companyNameLabel;
        [SerializeField] private TMP_Text _locationLabel;
        [SerializeField] private TMP_Text _xpLabel;
        [SerializeField] private TMP_Text _dollarsLabel;
        [SerializeField] private TMP_Text _goldIngotsLabel;

        private Sprite _sprR8, _sprR16;

        private void Awake() => Build();

#if UNITY_EDITOR
        [UnityEditor.MenuItem("CONTEXT/HeaderView/Build Header")]
        private static void BuildFromMenu(UnityEditor.MenuCommand cmd)
        {
            if (Application.isPlaying) { Debug.LogWarning("Stop Play Mode before building the Header."); return; }
            var h = (HeaderView)cmd.context;
            h.Build();
            UnityEditor.EditorUtility.SetDirty(h.gameObject);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(h.gameObject.scene);
        }
#endif

        private void Build()
        {
            // Clear all children
            var children = new System.Collections.Generic.List<GameObject>();
            foreach (Transform c in transform) children.Add(c.gameObject);
            foreach (var c in children)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying) DestroyImmediate(c);
                else Destroy(c);
#else
                Destroy(c);
#endif
            }

            // Header background removed
            var existingBg = GetComponent<Image>();
#if UNITY_EDITOR
            if (existingBg != null) { if (!Application.isPlaying) DestroyImmediate(existingBg); else Destroy(existingBg); }
#else
            if (existingBg != null) Destroy(existingBg);
#endif

            EnsureRoundedSprites();

            var rt = GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot     = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(0, -58);
            rt.offsetMax = new Vector2(0, 0);

            // ===== LEFT SIDE =====
            var left = MakeGO("LeftSide", transform);
            var leftRt = left.GetComponent<RectTransform>();
            leftRt.anchorMin = new Vector2(0, 0);
            leftRt.anchorMax = new Vector2(0, 1);
            leftRt.pivot     = new Vector2(0, 0.5f);
            leftRt.offsetMin = new Vector2(10, -80);
            leftRt.offsetMax = new Vector2(0, -10);

            var leftBg = left.AddComponent<Image>();
            leftBg.sprite        = _sprR16;
            leftBg.type          = Image.Type.Sliced;
            leftBg.color         = new Color32(0x2C, 0x30, 0x38, 255);
            leftBg.raycastTarget = false;
            var leftShadow = left.AddComponent<Shadow>();
            leftShadow.effectColor    = new Color(0f, 0f, 0f, 0.5f);
            leftShadow.effectDistance = new Vector2(0f, -4f);

            // LeftSide = Logo | ColonneInfo côte à côte
            var leftHlg = left.AddComponent<HorizontalLayoutGroup>();
            leftHlg.childAlignment        = TextAnchor.MiddleCenter;
            leftHlg.spacing               = 8;
            leftHlg.padding               = new RectOffset(8, 12, 5, 5);
            leftHlg.childForceExpandWidth  = false;
            leftHlg.childForceExpandHeight = false;
            leftHlg.childControlWidth      = false;
            leftHlg.childControlHeight     = false;

            var leftFitter = left.AddComponent<ContentSizeFitter>();
            leftFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            leftFitter.verticalFit   = ContentSizeFitter.FitMode.Unconstrained;

            // Logo
            var logoGo  = MakeGO("Logo", left.transform);
            var logoImg = logoGo.AddComponent<Image>();
            logoImg.sprite         = Resources.Load<Sprite>("UI/Logo/LogoFull") ?? Resources.Load<Sprite>("UI/Logo/Logo");
            logoImg.preserveAspect = true;
            logoImg.raycastTarget  = false;
            SetLayout(logoGo, 48, 48);

            // Colonne droite du logo : Name / PillsRow / Location
            var col    = MakeGO("InfoCol", left.transform);
            var colVlg = col.AddComponent<VerticalLayoutGroup>();
            colVlg.childAlignment        = TextAnchor.MiddleCenter;
            colVlg.spacing               = 0;
            colVlg.childForceExpandWidth  = false;
            colVlg.childForceExpandHeight = false;
            colVlg.childControlWidth      = false;
            colVlg.childControlHeight     = false;

            var colFitter = col.AddComponent<ContentSizeFitter>();
            colFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            colFitter.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

            // Ligne 1 : Name
            _companyNameLabel = MakeTMP("Name", col.transform, "Mon Entreprise", 20, FontStyles.Bold, new Color32(0xEC, 0xEE, 0xF5, 255));
            _companyNameLabel.textWrappingMode = TextWrappingModes.NoWrap;
            _companyNameLabel.overflowMode       = TextOverflowModes.Ellipsis;
            _companyNameLabel.alignment          = TextAlignmentOptions.Center;
            SetLayout(_companyNameLabel.gameObject, 160, 16);

            // Ligne 2 : Pills ($, G, XP) en ligne
            var pillsRow    = MakeGO("PillsRow", col.transform);
            var pillsHlg    = pillsRow.AddComponent<HorizontalLayoutGroup>();
            pillsHlg.childAlignment        = TextAnchor.MiddleCenter;
            pillsHlg.spacing               = 4;
            pillsHlg.childForceExpandWidth  = false;
            pillsHlg.childForceExpandHeight = false;
            pillsHlg.childControlWidth      = false;
            pillsHlg.childControlHeight     = false;
            var pillsFitter = pillsRow.AddComponent<ContentSizeFitter>();
            pillsFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            pillsFitter.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

            _dollarsLabel    = MakePill(pillsRow.transform, "PillDollars", "dollars", new Color32(0x3D, 0xC9, 0x6E, 255));
            _goldIngotsLabel = MakePill(pillsRow.transform, "PillGold",    "gold",   new Color32(0xF2, 0xD9, 0x66, 255));
            _xpLabel         = MakePill(pillsRow.transform, "PillXP",      "xp",     new Color32(0xFA, 0xC0, 0x24, 255));

            // Ligne 3 : Location (ville uniquement)
            _locationLabel = MakeTMP("Location", col.transform, "—", 11, FontStyles.Normal, new Color32(0x7A, 0x8F, 0xA6, 255));
            _locationLabel.textWrappingMode = TextWrappingModes.NoWrap;
            _locationLabel.overflowMode     = TextOverflowModes.Ellipsis;
            _locationLabel.alignment        = TextAlignmentOptions.Center;
            SetLayout(_locationLabel.gameObject, 160, 13);

            // ===== RIGHT SIDE =====
            var right   = MakeGO("RightSide", transform);
            var rightRt = right.GetComponent<RectTransform>();
            rightRt.anchorMin = new Vector2(1, 0);
            rightRt.anchorMax = new Vector2(1, 1);
            rightRt.pivot     = new Vector2(1, 0.5f);
            rightRt.offsetMin = new Vector2(0, -10);
            rightRt.offsetMax = new Vector2(-10, -10);

            var rightBg = right.AddComponent<Image>();
            rightBg.sprite        = _sprR16;
            rightBg.type          = Image.Type.Sliced;
            rightBg.color         = new Color32(0x2C, 0x30, 0x38, 255);
            rightBg.raycastTarget = false;
            var rightShadow = right.AddComponent<Shadow>();
            rightShadow.effectColor    = new Color(0f, 0f, 0f, 0.5f);
            rightShadow.effectDistance = new Vector2(0f, -4f);

            var rightFitter = right.AddComponent<ContentSizeFitter>();
            rightFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            rightFitter.verticalFit   = ContentSizeFitter.FitMode.Unconstrained;

            var rightHlg = right.AddComponent<HorizontalLayoutGroup>();
            rightHlg.childAlignment        = TextAnchor.MiddleCenter;
            rightHlg.spacing               = 10;
            rightHlg.padding               = new RectOffset(10, 10, 9, 9);
            rightHlg.childForceExpandWidth  = false;
            rightHlg.childForceExpandHeight = false;
            rightHlg.childControlWidth      = false;
            rightHlg.childControlHeight     = false;

            var analyticsBtn = MakeIconButton(right.transform, "BtnAnalytics", "chart");
            MakeDivider(right.transform);
            var listBtn      = MakeIconButton(right.transform, "btnList",      "list");
            MakeDivider(right.transform);
            var friendsBtn   = MakeIconButton(right.transform, "BtnFriends",   "users");
            MakeDivider(right.transform);
            // Placeholder : icône "add" réutilisée. Déposer un sprite dédié dans
            // Resources/UI/Icons/icons/ et changer le nom ici pour une vraie icône.
            var skillsBtn    = MakeIconButton(right.transform, "BtnSkills",   "add");
            MakeDivider(right.transform);
            var settingsBtn  = MakeIconButton(right.transform, "BtnSettings",  "settings");

            analyticsBtn.onClick.AddListener(AnalyticsPopupView.Show);
            listBtn.onClick.AddListener(FleetListPopupView.Show);
            friendsBtn.onClick.AddListener(FriendsPopupView.Show);
            skillsBtn.onClick.AddListener(SkillTreePopupView.Show);
            settingsBtn.onClick.AddListener(SettingsPopupView.Show);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static GameObject MakeGO(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void SetLayout(GameObject go, float w, float h)
        {
            var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            le.preferredWidth  = w;
            le.preferredHeight = h;
        }

        private static TMP_Text MakeTMP(string name, Transform parent, string text, float size, FontStyles style, Color32 color)
        {
            var go  = MakeGO(name, parent);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text           = text;
            tmp.fontSize       = size;
            tmp.fontStyle      = style;
            tmp.color          = color;
            tmp.alignment      = TextAlignmentOptions.MidlineLeft;
            tmp.raycastTarget  = false;
            return tmp;
        }

        private TMP_Text MakePill(Transform parent, string name, string iconSpriteName, Color32 iconColor)
        {
            var pill    = MakeGO(name, parent);
            var pillImg = pill.AddComponent<Image>();
            pillImg.sprite        = _sprR8;
            pillImg.type          = Image.Type.Sliced;
            pillImg.color         = new Color32(0x1A, 0x1D, 0x24, 230);
            pillImg.raycastTarget = false;

            var hlg = pill.AddComponent<HorizontalLayoutGroup>();
            hlg.padding               = new RectOffset(6, 8, 4, 4);
            hlg.spacing               = 3;
            hlg.childAlignment        = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth  = false;
            hlg.childForceExpandHeight = false;
            hlg.childControlWidth      = true;
            hlg.childControlHeight     = true;

            var fitter = pill.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

            var pillLe = pill.AddComponent<LayoutElement>();
            pillLe.preferredHeight = 26;

            // Icon
            var iconGo  = MakeGO("Image", pill.transform);
            var iconImg = iconGo.AddComponent<Image>();
            iconImg.sprite         = Resources.Load<Sprite>($"UI/Icons/Infos/{iconSpriteName}");
            iconImg.color          = iconColor;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget  = false;
            var iconLe = iconGo.AddComponent<LayoutElement>();
            iconLe.minWidth       = 50;
            iconLe.preferredWidth = 50;
            iconLe.minHeight      = 24;

            // Label valeur — largeur auto via TMP preferred size
            var labelTmp = MakeTMP("Label", pill.transform, "0", 18, FontStyles.Bold, new Color32(0xEC, 0xEE, 0xF5, 255));
            labelTmp.textWrappingMode = TextWrappingModes.NoWrap;
            labelTmp.overflowMode      = TextOverflowModes.Overflow;
            var labelFitter = labelTmp.gameObject.AddComponent<ContentSizeFitter>();
            labelFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            var labelLe = labelTmp.gameObject.AddComponent<LayoutElement>();
            labelLe.preferredHeight = 24;

            return labelTmp;
        }

        private Button MakeIconButton(Transform parent, string name, string iconName)
        {
            var go  = MakeGO(name, parent);
            var img = go.AddComponent<Image>();
            img.sprite        = _sprR8;
            img.type          = Image.Type.Sliced;
            img.color         = new Color(1f, 1f, 1f, 0.05f);
            img.raycastTarget = true;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            SetLayout(go, 44, 44);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(44, 44);

            var iconGo  = MakeGO("Icon", go.transform);
            var iconImg = iconGo.AddComponent<Image>();
            iconImg.sprite         = Resources.Load<Sprite>($"UI/Icons/icons/{iconName}");
            iconImg.color          = new Color32(0xC8, 0xD0, 0xE0, 255);
            iconImg.preserveAspect = true;
            iconImg.raycastTarget  = false;
            var iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin        = new Vector2(0.5f, 0.5f);
            iconRt.anchorMax        = new Vector2(0.5f, 0.5f);
            iconRt.sizeDelta        = new Vector2(24, 24);
            iconRt.anchoredPosition = Vector2.zero;

            return btn;
        }

        private static void MakeDivider(Transform parent)
        {
            var go  = MakeGO("Divider", parent);
            var img = go.AddComponent<Image>();
            img.color         = new Color32(0x3A, 0x3F, 0x4A, 150);
            img.raycastTarget = false;
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth  = 1;
            le.preferredHeight = 22;
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(1, 22);
        }

        // ── Events ───────────────────────────────────────────────────────────

        private void OnEnable()
        {
            GameEvents.OnDollarsChanged       += UpdateDollars;
            GameEvents.OnGoldIngotsChanged    += UpdateGoldIngots;
            GameEvents.OnCompanyXpChanged     += UpdateXp;
            GameEvents.OnCompanyProfileChanged += UpdateCompany;
        }

        private void OnDisable()
        {
            GameEvents.OnDollarsChanged       -= UpdateDollars;
            GameEvents.OnGoldIngotsChanged    -= UpdateGoldIngots;
            GameEvents.OnCompanyXpChanged     -= UpdateXp;
            GameEvents.OnCompanyProfileChanged -= UpdateCompany;
        }

        private void Start()
        {
            // Les boutons sont câblés dans Build(). Ici on initialise les valeurs.
            var gm = GameManager.Instance;
            if (gm == null || gm.Save == null) return;
            UpdateCompany();
            UpdateDollars(gm.Save.dollars);
            UpdateGoldIngots(gm.Save.goldIngots);
            var xp = ServiceLocator.Get<TransportManager.Systems.Progression.XpSystem>();
            if (xp != null) UpdateXp(xp.CompanyXp, xp.CompanyLevel);
        }

        private void UpdateCompany()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Save == null) return;
            var c = gm.Save.company;
            if (_companyNameLabel) _companyNameLabel.text = string.IsNullOrWhiteSpace(c.companyName) ? "Mon Entreprise" : c.companyName;
            if (_locationLabel)    _locationLabel.text    = ExtractCity(c.location);
        }

        // Nominatim returns "2 bis, Impasse Vège, ..., Bordeaux, ..."
        // If the first segment starts with a digit (house number), prepend it to the street name.
        private static string ExtractCity(string location)
        {
            if (string.IsNullOrWhiteSpace(location)) return "—";
            var parts = location.Split(',');
            if (parts.Length == 0) return "—";
            string first = parts[0].Trim();
            if (parts.Length > 1 && first.Length > 0 && char.IsDigit(first[0]))
                return first + " " + parts[1].Trim();
            return first;
        }

        private void UpdateDollars(int value)     { if (_dollarsLabel)    _dollarsLabel.text    = $"{value:N0}"; }
        private void UpdateGoldIngots(int value)  { if (_goldIngotsLabel) _goldIngotsLabel.text = $"{value:N0}"; }
        private void UpdateXp(int xp, int level)  { if (_xpLabel)         _xpLabel.text         = $"{xp:N0}"; }

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
