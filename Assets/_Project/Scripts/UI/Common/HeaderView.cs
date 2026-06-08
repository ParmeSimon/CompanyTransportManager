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
        [SerializeField] private Image    _logoBg;
        [SerializeField] private Image    _logoIcon;
        [SerializeField] private TMP_Text _reputationLabel;
        private Image[] _starFills;   // étoiles à remplissage partiel (null si PNG absent → repli texte)

        // Dépose un PNG d'étoile ici (importé en Sprite) pour des étoiles graphiques remplissables.
        private const string StarSpritePath = "UI/Icons/icons/star";
        private static readonly Color32 StarEmpty = new Color32(0x4A, 0x52, 0x60, 255);
        private static readonly Color32 StarGold  = new Color32(0xF2, 0xD9, 0x66, 255);

        private Sprite _sprR8, _sprR16;

        // Bouton Succès : badge « X récompenses à réclamer »
        private static HeaderView _instance;
        private GameObject _achBadge;
        private TMP_Text   _achBadgeLabel;

        private const float HeaderHeight = 58f;
        private Rect _lastSafeArea;
        private Canvas _canvas;

        private void Awake() { _instance = this; Build(); }

        // Décale la barre du haut sous la safe area (encoche/Dynamic Island + coins arrondis).
        private void ApplySafeArea()
        {
            if (_canvas == null) _canvas = GetComponentInParent<Canvas>();
            Vector4 ins = SafeAreaUtil.Insets(_canvas);
            var rt = GetComponent<RectTransform>();
            rt.offsetMax = new Vector2(-ins.y, -ins.z);                 // marge droite + haut
            rt.offsetMin = new Vector2(ins.x, -ins.z - HeaderHeight);   // marge gauche + hauteur barre
            _lastSafeArea = Screen.safeArea;
        }

        // Réapplique si la safe area change (rotation gauche/droite → l'encoche change de côté).
        private void LateUpdate()
        {
            if (Screen.safeArea != _lastSafeArea) ApplySafeArea();
        }

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
            ApplySafeArea();   // sous l'encoche + marges latérales (coins arrondis / Dynamic Island)

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

            // Logo d'entreprise (cadre arrondi masqué : photo importée OU symbole+couleur)
            var logoGo  = MakeGO("Logo", left.transform);
            _logoBg = logoGo.AddComponent<Image>();
            _logoBg.sprite        = _sprR16;
            _logoBg.type          = Image.Type.Sliced;
            _logoBg.color         = new Color32(0x1A, 0x1D, 0x24, 255);
            _logoBg.raycastTarget = false;
            var logoMask = logoGo.AddComponent<Mask>();   // coins arrondis pour la photo
            logoMask.showMaskGraphic = true;
            SetLayout(logoGo, 48, 48);

            var logoIconGo  = MakeGO("Icon", logoGo.transform);
            _logoIcon = logoIconGo.AddComponent<Image>();
            _logoIcon.color          = Color.white;
            _logoIcon.preserveAspect = true;
            _logoIcon.raycastTarget  = false;
            var logoIconRt = logoIconGo.GetComponent<RectTransform>();
            logoIconRt.pivot = new Vector2(0.5f, 0.5f);

            // État initial (sera rafraîchi par UpdateCompany)
            CompanyLogo.ApplyTo(_logoBg, _logoIcon, GameManager.Instance?.Save?.company);

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

            // Ligne 1bis : Réputation (étoiles à remplissage partiel, ou texte en repli)
            BuildReputationStars(col.transform);

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
            var skillsBtn    = MakeIconButton(right.transform, "BtnSkills",   "research");
            MakeDivider(right.transform);
            var achBtn       = MakeIconButtonSprite(right.transform, "BtnAchievements", MakeTrophySprite());
            _achBadge        = MakeBadge(achBtn.transform, out _achBadgeLabel);
            MakeDivider(right.transform);
            var dailyBtn     = MakeIconButtonSprite(right.transform, "BtnDaily", MakeTargetSprite());
            MakeDivider(right.transform);
            var settingsBtn  = MakeIconButton(right.transform, "BtnSettings",  "settings");

            analyticsBtn.onClick.AddListener(AnalyticsPopupView.Show);
            listBtn.onClick.AddListener(FleetListPopupView.Show);
            friendsBtn.onClick.AddListener(FriendsPopupView.Show);
            skillsBtn.onClick.AddListener(SkillTreePopupView.Show);
            achBtn.onClick.AddListener(UI.Achievements.AchievementsPopupView.Show);
            dailyBtn.onClick.AddListener(UI.Daily.DailyHubPopupView.Show);
            settingsBtn.onClick.AddListener(SettingsPopupView.Show);
            RefreshAchievementsBadge();
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
            UiIcons.Apply(iconImg, $"UI/Icons/Infos/{iconSpriteName}");
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
            UiIcons.Apply(iconImg, $"UI/Icons/icons/{iconName}");
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

        // Variante de MakeIconButton avec un sprite fourni (icône procédurale, sans asset).
        private Button MakeIconButtonSprite(Transform parent, string name, Sprite sprite)
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
            iconImg.sprite         = sprite;
            iconImg.color          = new Color32(0xC8, 0xD0, 0xE0, 255);
            iconImg.preserveAspect = true;
            iconImg.raycastTarget  = false;
            var iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin = iconRt.anchorMax = new Vector2(0.5f, 0.5f);
            iconRt.sizeDelta        = new Vector2(24, 24);
            iconRt.anchoredPosition = Vector2.zero;
            return btn;
        }

        // Icône « cible » procédurale (objectifs/missions) — bandes concentriques.
        private static Sprite _targetSprite;
        private static Sprite MakeTargetSprite()
        {
            if (_targetSprite != null) return _targetSprite;
            const int sz = 64;
            var tex = new Texture2D(sz, sz, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            float c = (sz - 1) / 2f, rmax = sz / 2f - 1f;
            for (int y = 0; y < sz; y++)
                for (int x = 0; x < sz; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / rmax; // 0 centre → 1 bord
                    bool ink = (d <= 0.22f) || (d >= 0.45f && d <= 0.62f) || (d >= 0.82f && d <= 1.0f);
                    // anti-alias léger sur les bords des bandes
                    float a = ink ? 1f : 0f;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            _targetSprite = Sprite.Create(tex, new Rect(0, 0, sz, sz), new Vector2(0.5f, 0.5f));
            return _targetSprite;
        }

        // Icône « trophée/étoile » procédurale (succès) — étoile à 5 branches pleine.
        private static Sprite _trophySprite;
        private static Sprite MakeTrophySprite()
        {
            if (_trophySprite != null) return _trophySprite;
            const int sz = 64;
            var tex = new Texture2D(sz, sz, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            Vector2 c = new Vector2((sz - 1) / 2f, (sz - 1) / 2f);
            float rOut = sz / 2f - 2f, rIn = rOut * 0.42f;

            // Sommets de l'étoile (5 externes + 5 internes), pointe vers le haut.
            var pts = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float ang = Mathf.Deg2Rad * (90f + i * 36f);
                float r = (i % 2 == 0) ? rOut : rIn;
                pts[i] = c + new Vector2(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r);
            }

            for (int y = 0; y < sz; y++)
                for (int x = 0; x < sz; x++)
                {
                    bool inside = PointInPoly(new Vector2(x, y), pts);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, inside ? 1f : 0f));
                }
            tex.Apply();
            _trophySprite = Sprite.Create(tex, new Rect(0, 0, sz, sz), new Vector2(0.5f, 0.5f));
            return _trophySprite;
        }

        private static bool PointInPoly(Vector2 p, Vector2[] poly)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                if (((poly[i].y > p.y) != (poly[j].y > p.y)) &&
                    (p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x))
                    inside = !inside;
            }
            return inside;
        }

        // Petite pastille rouge (compteur) ancrée en haut-droite d'un bouton d'icône.
        private GameObject MakeBadge(Transform parent, out TMP_Text label)
        {
            var go  = MakeGO("Badge", parent);
            var img = go.AddComponent<Image>();
            img.sprite        = _sprR8;
            img.type          = Image.Type.Sliced;
            img.color         = new Color32(0xE5, 0x3E, 0x3E, 255);
            img.raycastTarget = false;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin        = new Vector2(1f, 1f);
            rt.anchorMax        = new Vector2(1f, 1f);
            rt.pivot            = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-2f, -2f);
            rt.sizeDelta        = new Vector2(18f, 18f);

            label = MakeTMP("N", go.transform, "0", 11f, FontStyles.Bold, new Color32(0xFF, 0xFF, 0xFF, 255));
            label.alignment       = TextAlignmentOptions.Center;
            label.raycastTarget   = false;
            var lrt = label.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;

            go.SetActive(false);
            return go;
        }

        /// Met à jour la pastille du bouton Succès (nb de récompenses à réclamer).
        public void RefreshAchievementsBadge()
        {
            if (_achBadge == null) return;
            int n = ServiceLocator.Get<TransportManager.Systems.Achievements.AchievementSystem>()?.ClaimableCount() ?? 0;
            _achBadge.SetActive(n > 0);
            if (n > 0 && _achBadgeLabel != null) _achBadgeLabel.text = n > 9 ? "9+" : n.ToString();
        }

        /// Appelé par la popup Succès après une réclamation pour rafraîchir la pastille.
        public static void NotifyAchievementsChanged() => _instance?.RefreshAchievementsBadge();

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
            GameEvents.OnReputationChanged    += UpdateReputation;
            GameEvents.OnAchievementUnlocked  += OnAchievementUnlocked;
        }

        private void OnDisable()
        {
            GameEvents.OnDollarsChanged       -= UpdateDollars;
            GameEvents.OnGoldIngotsChanged    -= UpdateGoldIngots;
            GameEvents.OnCompanyXpChanged     -= UpdateXp;
            GameEvents.OnCompanyProfileChanged -= UpdateCompany;
            GameEvents.OnReputationChanged    -= UpdateReputation;
            GameEvents.OnAchievementUnlocked  -= OnAchievementUnlocked;
        }

        private void OnAchievementUnlocked(TransportManager.Systems.Achievements.AchievementDef _) => RefreshAchievementsBadge();

        private void UpdateReputation(int reputation, int tier)
        {
            var r = ServiceLocator.Get<TransportManager.Systems.Progression.ReputationSystem>();
            if (r == null) return;
            if (_starFills != null)
            {
                float s = r.Stars;
                for (int i = 0; i < _starFills.Length; i++)
                    if (_starFills[i] != null) _starFills[i].fillAmount = Mathf.Clamp01(s - i);
            }
            else if (_reputationLabel != null)
            {
                _reputationLabel.text = r.StarString();
            }
        }

        // Construit 5 étoiles **pleines** (générées en code) remplissables horizontalement.
        private void BuildReputationStars(Transform parent)
        {
            // Étoile pleine procédurale : le remplissage gauche→droite colore une vraie surface
            // (un PNG en contour ne colorerait que le bord).
            var star = MakeStarSprite();

            var row = MakeGO("Reputation", parent);
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter; hlg.spacing = 2;
            // Marge basse → les étoiles se centrent un peu plus haut (remontent vers le titre)
            // sans déplacer le nom de l'entreprise.
            hlg.padding = new RectOffset(0, 0, 0, 6);
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
            hlg.childControlWidth = false; hlg.childControlHeight = false;
            var fitter = row.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

            _starFills = new Image[5];
            for (int i = 0; i < 5; i++)
            {
                var slot = MakeGO("Star" + i, row.transform);
                slot.GetComponent<RectTransform>().sizeDelta = new Vector2(13, 13);
                var bg = slot.AddComponent<Image>();
                bg.sprite = star; bg.color = StarEmpty; bg.raycastTarget = false;

                var fillGo = MakeGO("Fill", slot.transform);
                var fRt = fillGo.GetComponent<RectTransform>();
                fRt.anchorMin = Vector2.zero; fRt.anchorMax = Vector2.one; fRt.offsetMin = Vector2.zero; fRt.offsetMax = Vector2.zero;
                var fill = fillGo.AddComponent<Image>();
                fill.sprite = star; fill.color = StarGold; fill.raycastTarget = false;
                fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal;
                fill.fillOrigin = (int)Image.OriginHorizontal.Left; fill.fillAmount = 0f;
                _starFills[i] = fill;
            }
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
            UpdateReputation(0, 0);
            RefreshAchievementsBadge();

            // Auto-ouverture du hub quotidien au lancement (une fois, après l'onboarding)
            // s'il y a une récompense de connexion ou une mission à réclamer.
            if (!_dailyAutoShown && (gm.Save.tutorial?.completed ?? false))
            {
                _dailyAutoShown = true;
                UI.Daily.DailyHubPopupView.ShowIfPending();
            }
        }

        private static bool _dailyAutoShown;

        private void UpdateCompany()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Save == null) return;
            var c = gm.Save.company;
            if (_companyNameLabel) _companyNameLabel.text = string.IsNullOrWhiteSpace(c.companyName) ? "Mon Entreprise" : c.companyName;
            if (_locationLabel)    _locationLabel.text    = ExtractCity(c.location);
            CompanyLogo.ApplyTo(_logoBg, _logoIcon, c);
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

        // ── Étoile pleine procédurale (pour la réputation) ─────────────────────
        private static Sprite _starSprite;
        private static Sprite MakeStarSprite()
        {
            if (_starSprite != null) return _starSprite;
            const int size = 64;
            float cx = size / 2f, cy = size / 2f;
            float rOut = size * 0.46f, rIn = size * 0.20f;

            // 10 sommets de l'étoile (outer/inner alternés), pointe en haut.
            var pts = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float ang = -Mathf.PI / 2f + i * Mathf.PI / 5f;   // pas de 36°
                float rad = (i % 2 == 0) ? rOut : rIn;
                pts[i] = new Vector2(cx + Mathf.Cos(ang) * rad, cy + Mathf.Sin(ang) * rad);
            }

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[size * size];
            const int SS = 3;   // anti-aliasing par sur-échantillonnage 3×3
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int inside = 0;
                    for (int sy = 0; sy < SS; sy++)
                        for (int sx = 0; sx < SS; sx++)
                            if (PointInPoly(x + (sx + 0.5f) / SS, y + (sy + 0.5f) / SS, pts)) inside++;
                    px[y * size + x] = new Color(1f, 1f, 1f, inside / (float)(SS * SS));
                }
            tex.SetPixels(px); tex.Apply();
            _starSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return _starSprite;
        }

        private static bool PointInPoly(float x, float y, Vector2[] p)
        {
            bool inside = false;
            for (int i = 0, j = p.Length - 1; i < p.Length; j = i++)
            {
                if (((p[i].y > y) != (p[j].y > y)) &&
                    (x < (p[j].x - p[i].x) * (y - p[i].y) / (p[j].y - p[i].y) + p[i].x))
                    inside = !inside;
            }
            return inside;
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
