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

        public const float HeaderHeight = 64f;   // barre collée au bord haut (cf. SafeAreaUtil.HudTopHeight)
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

            // ===== BARRE DU HAUT (pleine largeur) =====
            // [Profil : logo · nom · niveau + XP]  ……  [$ +] [lingots +]  ……  [Réglages]
            Vector4 insets = SafeAreaUtil.Insets(GetComponentInParent<Canvas>());
            var bar = MakeGO("Bar", transform);
            var barRt = bar.GetComponent<RectTransform>();
            barRt.anchorMin = Vector2.zero; barRt.anchorMax = Vector2.one;
            barRt.offsetMin = new Vector2(-insets.x, 0); barRt.offsetMax = new Vector2(insets.y, insets.z);
            var barBg = bar.AddComponent<Image>();
            barBg.color = new Color32(0x22, 0x26, 0x2E, 255);
            barBg.raycastTarget = true;
            // Pas d ombre portée (effet dur) : un simple filet clair en bas (Hairline).

            var hair = MakeGO("Hairline", bar.transform);
            var hairImg = hair.AddComponent<Image>();
            hairImg.color = new Color(1f, 1f, 1f, 0.06f); hairImg.raycastTarget = false;
            var hairRt = hair.GetComponent<RectTransform>();
            hairRt.anchorMin = new Vector2(0, 0); hairRt.anchorMax = new Vector2(1, 0);
            hairRt.pivot = new Vector2(0.5f, 0f); hairRt.sizeDelta = new Vector2(0, 1);
            hair.AddComponent<LayoutElement>().ignoreLayout = true;

            var row = bar.AddComponent<HorizontalLayoutGroup>();
            row.padding = new RectOffset((int)insets.x + 10, (int)insets.y + 10, (int)insets.z + 6, 6);
            row.spacing = 14;
            row.childAlignment = TextAnchor.MiddleLeft;
            row.childForceExpandWidth = false; row.childForceExpandHeight = false;
            row.childControlWidth = true; row.childControlHeight = true;

            // 1 — Profil (bouton : ouvre le menu Stats / Flotte / Compétences / Objectifs / Succès / Amis)
            var profile = MakeGO("Profile", bar.transform);
            var profImg = profile.AddComponent<Image>();
            profImg.sprite = _sprR8; profImg.type = Image.Type.Sliced;
            profImg.color = new Color(1f, 1f, 1f, 0.04f);
            var profBtn = profile.AddComponent<Button>();
            profBtn.targetGraphic = profImg;
            profBtn.onClick.AddListener(ToggleProfileMenu);
            var profHlg = profile.AddComponent<HorizontalLayoutGroup>();
            profHlg.padding = new RectOffset(4, 12, 2, 2);
            profHlg.spacing = 10;
            profHlg.childAlignment = TextAnchor.MiddleLeft;
            profHlg.childForceExpandWidth = false; profHlg.childForceExpandHeight = false;
            profHlg.childControlWidth = true; profHlg.childControlHeight = true;
            var profLe = profile.AddComponent<LayoutElement>();
            profLe.preferredWidth = 310; profLe.minWidth = 260; profLe.minHeight = 52;

            var logoGo = MakeGO("Logo", profile.transform);
            _logoBg = logoGo.AddComponent<Image>();
            _logoBg.sprite = _sprR16; _logoBg.type = Image.Type.Sliced;
            _logoBg.color = new Color32(0x1A, 0x1D, 0x24, 255);
            _logoBg.raycastTarget = false;
            var logoMask = logoGo.AddComponent<Mask>();
            logoMask.showMaskGraphic = true;
            var logoLe = logoGo.AddComponent<LayoutElement>();
            logoLe.preferredWidth = 48; logoLe.preferredHeight = 48; logoLe.minWidth = 48;
            var logoIconGo = MakeGO("Icon", logoGo.transform);
            _logoIcon = logoIconGo.AddComponent<Image>();
            _logoIcon.color = Color.white; _logoIcon.preserveAspect = true; _logoIcon.raycastTarget = false;
            logoIconGo.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
            CompanyLogo.ApplyTo(_logoBg, _logoIcon, GameManager.Instance?.Save?.company);


            var col = MakeGO("InfoCol", profile.transform);
            var colVlg = col.AddComponent<VerticalLayoutGroup>();
            colVlg.spacing = 3;
            colVlg.childAlignment = TextAnchor.MiddleLeft;
            colVlg.childForceExpandWidth = true; colVlg.childForceExpandHeight = false;
            colVlg.childControlWidth = true; colVlg.childControlHeight = true;
            col.AddComponent<LayoutElement>().flexibleWidth = 1;

            _companyNameLabel = MakeTMP("Name", col.transform, "Mon Entreprise", 16, FontStyles.Bold, new Color32(0xEC, 0xEE, 0xF5, 255));
            _companyNameLabel.textWrappingMode = TextWrappingModes.NoWrap;
            _companyNameLabel.overflowMode = TextOverflowModes.Ellipsis;
            var nameLe = _companyNameLabel.gameObject.AddComponent<LayoutElement>();
            nameLe.preferredHeight = 22; nameLe.minHeight = 22;

            // Niveau + réputation (petites étoiles) sur une ligne, barre d'XP dessous.
            var lvlRow = MakeGO("LevelRow", col.transform);
            var lvlHlg = lvlRow.AddComponent<HorizontalLayoutGroup>();
            lvlHlg.spacing = 8; lvlHlg.childAlignment = TextAnchor.MiddleLeft;
            lvlHlg.childForceExpandWidth = false; lvlHlg.childForceExpandHeight = false;
            lvlHlg.childControlWidth = true; lvlHlg.childControlHeight = true;
            var lvlLe = lvlRow.AddComponent<LayoutElement>();
            lvlLe.preferredHeight = 16; lvlLe.minHeight = 16;
            _levelLabel = MakeTMP("Level", lvlRow.transform, "Niv. 1", 11, FontStyles.Bold, new Color32(0x5A, 0x8E, 0xF5, 255));
            _levelLabel.textWrappingMode = TextWrappingModes.NoWrap;
            BuildReputationStars(lvlRow.transform);
            var starsRow = lvlRow.transform.Find("Reputation");
            if (starsRow != null)
            {
                var sLe = starsRow.gameObject.AddComponent<LayoutElement>();
                sLe.preferredWidth = 5 * 13 + 4 * 2; sLe.preferredHeight = 13; sLe.minWidth = sLe.preferredWidth;
            }

            var xpTrack = MakeGO("XpTrack", col.transform);
            var xtImg = xpTrack.AddComponent<Image>();
            xtImg.sprite = _sprR8; xtImg.type = Image.Type.Sliced;
            xtImg.color = new Color(1f, 1f, 1f, 0.10f); xtImg.raycastTarget = false;
            var xtLe = xpTrack.AddComponent<LayoutElement>();
            xtLe.preferredHeight = 6; xtLe.minHeight = 6;
            var xpFillGo = MakeGO("Fill", xpTrack.transform);
            var xfImg = xpFillGo.AddComponent<Image>();
            xfImg.sprite = _sprR8; xfImg.type = Image.Type.Sliced;
            xfImg.color = new Color32(0x5A, 0x8E, 0xF5, 255); xfImg.raycastTarget = false;
            _xpFill = xpFillGo.GetComponent<RectTransform>();
            _xpFill.anchorMin = Vector2.zero; _xpFill.anchorMax = new Vector2(0f, 1f);
            _xpFill.offsetMin = Vector2.zero; _xpFill.offsetMax = Vector2.zero;

            // Affordance : icône « menu » (3 traits) — on comprend que le profil s'ouvre.
            var burger = MakeGO("MenuIcon", profile.transform);
            var bLe = burger.AddComponent<LayoutElement>();
            bLe.preferredWidth = 22; bLe.minWidth = 22; bLe.preferredHeight = 18;
            for (int i = 0; i < 3; i++)
            {
                var line = MakeGO("L" + i, burger.transform);
                var lImg = line.AddComponent<Image>();
                lImg.sprite = _sprR8; lImg.type = Image.Type.Sliced;
                lImg.color = new Color32(0xC8, 0xD0, 0xE0, 255); lImg.raycastTarget = false;
                var lRt = line.GetComponent<RectTransform>();
                lRt.anchorMin = new Vector2(0, 0.5f); lRt.anchorMax = new Vector2(1, 0.5f);
                lRt.sizeDelta = new Vector2(0, 3); lRt.anchoredPosition = new Vector2(0, 7 - i * 7);
            }
            _profileDot = MakeDot(burger.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(2f, 2f));

            // 2 — Monnaies au centre, chacune avec « + » → Shop
            Spacer(bar.transform);
            _dollarsLabel    = MakePill(bar.transform, "PillDollars", "dollars", new Color32(0x3D, 0xC9, 0x6E, 255));
            _goldIngotsLabel = MakePill(bar.transform, "PillGold",    "gold",    new Color32(0xF2, 0xD9, 0x66, 255));
            Spacer(bar.transform);

            // 3 — Réglages seul à droite
            // Cloche : journal des notifications (livraisons, accidents, démissions…) + compteur non lus.
            var bellBtn = MakeIconButtonSprite(bar.transform, "BtnNotifications", MakeBellSprite());
            bellBtn.onClick.AddListener(TransportManager.UI.Notifications.NotificationsPopupView.Show);
            _bellBadge = MakeCountBadge(bellBtn.transform, out _bellBadgeLbl, inLayout: false);
            var bellRt = _bellBadge.GetComponent<RectTransform>();
            bellRt.anchoredPosition = new Vector2(-2f, -2f);

            var settingsBtn = MakeIconButton(bar.transform, "BtnSettings", "settings");
            settingsBtn.onClick.AddListener(SettingsPopupView.Show);

            // Menu et dock vivent à la racine du canvas : en jeu uniquement (pas dans la scène sauvegardée).
            if (Application.isPlaying) BuildProfileMenu();
            GameEvents.OnTabChanged -= OnTabChangedCloseMenu;
            GameEvents.OnTabChanged += OnTabChangedCloseMenu;
            TransportManager.Systems.Notifications.NotificationSystem.Changed -= RefreshBell;
            TransportManager.Systems.Notifications.NotificationSystem.Changed += RefreshBell;
            RefreshBell();
            RefreshBadges();
        }

        // ── Nouvelles pièces du HUD ──────────────────────────────────────────

        private TMP_Text      _levelLabel;
        private RectTransform _xpFill;
        private GameObject    _profileDot;
        private GameObject    _menuBlocker;
        private TMP_Text      _menuAchLbl, _menuDailyLbl, _menuSkillsLbl;
        private GameObject    _menuAchBadge, _menuDailyBadge, _menuSkillsBadge;
        private float         _nextBadgeRefresh;

        private static void Spacer(Transform parent)
        {
            var go = MakeGO("Spacer", parent);
            go.AddComponent<LayoutElement>().flexibleWidth = 1;
        }

        // Petit point rouge en haut à droite d'un élément.
        private GameObject MakeDot(RectTransform target, Vector2 anchor, Vector2 pos)
        {
            var go = MakeGO("Dot", target);
            var img = go.AddComponent<Image>();
            img.sprite = _sprR8; img.type = Image.Type.Sliced;
            img.color = new Color32(0xE5, 0x3E, 0x3E, 255); img.raycastTarget = false;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(14f, 14f);
            go.AddComponent<LayoutElement>().ignoreLayout = true;
            go.SetActive(false);
            return go;
        }

        // Ouvre l'onglet Shop (bouton « + » des monnaies).
        private static void OpenShop() => GameEvents.RaiseTabChanged(TransportManager.Enums.TabType.Shop);

        // ── Menu profil : toutes les fonctions secondaires, avec libellés ─────
        private void BuildProfileMenu()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            _menuBlocker = MakeGO("ProfileMenu", canvas.transform);
            var bRt = _menuBlocker.GetComponent<RectTransform>();
            bRt.anchorMin = Vector2.zero; bRt.anchorMax = Vector2.one;
            bRt.offsetMin = Vector2.zero; bRt.offsetMax = Vector2.zero;
            var blockImg = _menuBlocker.AddComponent<Image>();
            blockImg.color = new Color(0f, 0f, 0f, 0.35f);
            var blockBtn = _menuBlocker.AddComponent<Button>();
            blockBtn.transition = Selectable.Transition.None;
            blockBtn.onClick.AddListener(CloseProfileMenu);

            var menu = MakeGO("Menu", _menuBlocker.transform);
            var mRt = menu.GetComponent<RectTransform>();
            mRt.anchorMin = mRt.anchorMax = new Vector2(0f, 1f);
            mRt.pivot = new Vector2(0f, 1f);
            Vector4 ins = SafeAreaUtil.Insets(canvas);
            mRt.anchoredPosition = new Vector2(ins.x + 10f, -(ins.z + HeaderHeight + 6f));
            mRt.sizeDelta = new Vector2(320f, 0f);
            var mImg = menu.AddComponent<Image>();
            mImg.sprite = _sprR16; mImg.type = Image.Type.Sliced;
            mImg.color = new Color32(0x2C, 0x30, 0x38, 255);
            var mSh = menu.AddComponent<Shadow>();
            mSh.effectColor = new Color(0f, 0f, 0f, 0.55f); mSh.effectDistance = new Vector2(0f, -6f);
            var mVlg = menu.AddComponent<VerticalLayoutGroup>();
            mVlg.padding = new RectOffset(8, 8, 8, 8);
            mVlg.spacing = 4;
            mVlg.childForceExpandWidth = true; mVlg.childForceExpandHeight = false;
            mVlg.childControlWidth = true; mVlg.childControlHeight = true;
            menu.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            MenuRow(menu.transform, "chart",    null,               "Statistiques",     AnalyticsPopupView.Show, out _, out _);
            MenuRow(menu.transform, "list",     null,               "Ma flotte",        FleetListPopupView.Show, out _, out _);
            MenuRow(menu.transform, "research", null,               "Compétences",      SkillTreePopupView.Show, out _menuSkillsBadge, out _menuSkillsLbl);
            MenuRow(menu.transform, null,       MakeTargetSprite(), "Objectifs du jour", UI.Daily.DailyHubPopupView.Show, out _menuDailyBadge, out _menuDailyLbl);
            MenuRow(menu.transform, null,       MakeTrophySprite(), "Succès",           UI.Achievements.AchievementsPopupView.Show, out _menuAchBadge, out _menuAchLbl);
            MenuRow(menu.transform, "users",    null,               "Amis",             FriendsPopupView.Show, out _, out _);

            _menuBlocker.SetActive(false);
        }

        private void MenuRow(Transform parent, string iconName, Sprite sprite, string label,
                             UnityEngine.Events.UnityAction open, out GameObject badge, out TMP_Text badgeLbl)
        {
            var go = MakeGO("Row_" + label, parent);
            var img = go.AddComponent<Image>();
            img.sprite = _sprR8; img.type = Image.Type.Sliced;
            img.color = new Color(1f, 1f, 1f, 0.04f);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => { CloseProfileMenu(); open(); });
            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(12, 12, 0, 0);
            hlg.spacing = 12;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 52; le.minHeight = 52;   // cible tactile ≥ 44 pt

            var icon = MakeGO("Icon", go.transform);
            var iImg = icon.AddComponent<Image>();
            if (sprite != null) iImg.sprite = sprite; else UiIcons.Apply(iImg, $"UI/Icons/icons/{iconName}");
            iImg.color = new Color32(0xC8, 0xD0, 0xE0, 255);
            iImg.preserveAspect = true; iImg.raycastTarget = false;
            var iLe = icon.AddComponent<LayoutElement>();
            iLe.preferredWidth = 24; iLe.preferredHeight = 24; iLe.minWidth = 24;

            var lbl = MakeTMP("Label", go.transform, label, 14, FontStyles.Bold, new Color32(0xEC, 0xEE, 0xF5, 255));
            lbl.textWrappingMode = TextWrappingModes.NoWrap;
            lbl.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

            badge = MakeCountBadge(go.transform, out badgeLbl, inLayout: true);
        }

        // Compteur rouge (dans une rangée, ou posé en coin d'un bouton).
        private GameObject MakeCountBadge(Transform parent, out TMP_Text label, bool inLayout)
        {
            var go = MakeGO("Badge", parent);
            var img = go.AddComponent<Image>();
            img.sprite = _sprR8; img.type = Image.Type.Sliced;
            img.color = new Color32(0xE5, 0x3E, 0x3E, 255); img.raycastTarget = false;
            var le = go.AddComponent<LayoutElement>();
            if (inLayout) { le.preferredWidth = 26; le.preferredHeight = 22; le.minWidth = 26; }
            else
            {
                le.ignoreLayout = true;
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(-6f, -6f);
                rt.sizeDelta = new Vector2(26f, 22f);
            }
            label = MakeTMP("N", go.transform, "0", 11, FontStyles.Bold, new Color32(0xFF, 0xFF, 0xFF, 255));
            label.alignment = TextAlignmentOptions.Center;
            var lrt = label.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            go.SetActive(false);
            return go;
        }

        private void ToggleProfileMenu()
        {
            if (_menuBlocker == null) return;
            bool open = !_menuBlocker.activeSelf;
            _menuBlocker.SetActive(open);
            if (open) { _menuBlocker.transform.SetAsLastSibling(); RefreshBadges(); }
        }

        private void CloseProfileMenu() { if (_menuBlocker != null) _menuBlocker.SetActive(false); }

        private void OnTabChangedCloseMenu(TransportManager.Enums.TabType _) => CloseProfileMenu();

        // Compteurs partagés (menu profil + rail) : « quelque chose à faire ».
        public static int DailyPendingCount()
        {
            int daily = 0;
            var d = ServiceLocator.Get<TransportManager.Systems.Daily.DailySystem>();
            if (d == null) return 0;
            if (d.LoginPending) daily++;
            if (d.LeaguePending) daily++;
            foreach (var m in d.Missions)
                if (!m.claimed && TransportManager.Systems.Daily.DailySystem.IsComplete(m)) daily++;
            return daily;
        }
        public static int SkillPointsCount()
            => ServiceLocator.Get<TransportManager.Systems.Progression.SkillTreeSystem>()?.AvailablePoints ?? 0;
        public static int AchievementsClaimableCount()
            => ServiceLocator.Get<TransportManager.Systems.Achievements.AchievementSystem>()?.ClaimableCount() ?? 0;

        private void RefreshBadges()
        {
            int daily = DailyPendingCount(), skills = SkillPointsCount(), ach = AchievementsClaimableCount();
            RefreshBell();
            SetBadge(_menuDailyBadge, _menuDailyLbl, daily);
            SetBadge(_menuSkillsBadge, _menuSkillsLbl, skills);
            SetBadge(_menuAchBadge, _menuAchLbl, ach);
            if (_profileDot != null) _profileDot.SetActive(daily + skills + ach > 0);
        }

        private static void SetBadge(GameObject badge, TMP_Text lbl, int n)
        {
            if (badge == null) return;
            badge.SetActive(n > 0);
            if (n > 0 && lbl != null) lbl.text = n > 9 ? "9+" : n.ToString();
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextBadgeRefresh) return;
            _nextBadgeRefresh = Time.unscaledTime + 1.5f;
            RefreshBadges();
        }

        private void OnDestroy()
        {
            GameEvents.OnTabChanged -= OnTabChangedCloseMenu;
            TransportManager.Systems.Notifications.NotificationSystem.Changed -= RefreshBell;
        }

        // ── Cloche des notifications ─────────────────────────────────────────
        private GameObject _bellBadge;
        private TMP_Text   _bellBadgeLbl;

        private void RefreshBell()
        {
            int n = ServiceLocator.Get<TransportManager.Systems.Notifications.NotificationSystem>()?.UnreadCount ?? 0;
            SetBadge(_bellBadge, _bellBadgeLbl, n);
        }

        // Cloche procédurale (pas d asset) : dôme + bord + battant.
        private static Sprite _bellSprite;
        private static Sprite MakeBellSprite()
        {
            if (_bellSprite != null) return _bellSprite;
            const int sz = 64;
            var tex = new Texture2D(sz, sz, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[sz * sz];
            const int SS = 3;
            float c = sz / 2f;
            for (int y = 0; y < sz; y++)
                for (int x = 0; x < sz; x++)
                {
                    int hit = 0;
                    for (int sy = 0; sy < SS; sy++)
                        for (int sx = 0; sx < SS; sx++)
                        {
                            float fx = x + (sx + 0.5f) / SS, fy = y + (sy + 0.5f) / SS;
                            bool ink = false;
                            if (fy >= 20f && fy <= 52f)                       // dôme : s élargit vers le bas
                            {
                                float t = (52f - fy) / 32f;
                                float hw = 7f + 15f * Mathf.Sqrt(t);
                                ink = Mathf.Abs(fx - c) <= hw;
                            }
                            if (fy >= 14f && fy < 20f && Mathf.Abs(fx - c) <= 25f) ink = true;   // bord
                            if ((fx - c) * (fx - c) + (fy - 8f) * (fy - 8f) <= 30f) ink = true;     // battant
                            if ((fx - c) * (fx - c) + (fy - 55f) * (fy - 55f) <= 14f) ink = true;   // anneau
                            if (ink) hit++;
                        }
                    px[y * sz + x] = new Color(1f, 1f, 1f, hit / (float)(SS * SS));
                }
            tex.SetPixels(px); tex.Apply();
            _bellSprite = Sprite.Create(tex, new Rect(0, 0, sz, sz), new Vector2(0.5f, 0.5f));
            return _bellSprite;
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
            hlg.padding               = new RectOffset(6, 7, 4, 4);
            hlg.spacing               = 6;
            hlg.childAlignment        = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth  = false;
            hlg.childForceExpandHeight = false;
            hlg.childControlWidth      = true;
            hlg.childControlHeight     = true;

            var fitter = pill.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

            var pillLe = pill.AddComponent<LayoutElement>();
            pillLe.preferredHeight = 44; pillLe.minHeight = 44;
            var pillBtn = pill.AddComponent<Button>();      // toute la pastille ouvre le Shop
            pillBtn.targetGraphic = pillImg;
            pillBtn.onClick.AddListener(OpenShop);
            pillImg.raycastTarget = true;

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

            // « + » : acheter cette monnaie
            var plus = MakeGO("Plus", pill.transform);
            var plusImg = plus.AddComponent<Image>();
            plusImg.sprite = _sprR8; plusImg.type = Image.Type.Sliced;
            plusImg.color = new Color32(0x22, 0xC5, 0x6E, 255); plusImg.raycastTarget = false;
            var plusLe = plus.AddComponent<LayoutElement>();
            plusLe.preferredWidth = 30; plusLe.preferredHeight = 30; plusLe.minWidth = 30;
            var plusLbl = MakeTMP("P", plus.transform, "+", 18, FontStyles.Bold, new Color32(0xFF, 0xFF, 0xFF, 255));
            plusLbl.alignment = TextAlignmentOptions.Center;
            var plRt = plusLbl.GetComponent<RectTransform>();
            plRt.anchorMin = Vector2.zero; plRt.anchorMax = Vector2.one; plRt.offsetMin = Vector2.zero; plRt.offsetMax = Vector2.zero;

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
            SetLayout(go, 48, 48);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(48, 48);

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
            SetLayout(go, 48, 48);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(48, 48);

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
        public static Sprite TargetSprite() => MakeTargetSprite();   // utilisée par le rail (Objectifs)
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
            RefreshBadges();
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
            hlg.padding = new RectOffset(0, 0, 0, 0);
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
            if (string.IsNullOrWhiteSpace(location)) return "-";
            var parts = location.Split(',');
            if (parts.Length == 0) return "-";
            string first = parts[0].Trim();
            if (parts.Length > 1 && first.Length > 0 && char.IsDigit(first[0]))
                return first + " " + parts[1].Trim();
            return first;
        }

        private void UpdateDollars(int value)     { if (_dollarsLabel)    _dollarsLabel.text    = $"{value:N0}"; }
        private void UpdateGoldIngots(int value)  { if (_goldIngotsLabel) _goldIngotsLabel.text = $"{value:N0}"; }
        private void UpdateXp(int xp, int level)
        {
            if (_levelLabel) _levelLabel.text = $"Niv. {level}";
            if (_xpFill)
            {
                int floor = TransportManager.Entities.Progression.XpCurve.XpForCompanyLevel(level);
                int next  = TransportManager.Entities.Progression.XpCurve.XpForCompanyLevel(level + 1);
                float t = next > floor ? Mathf.Clamp01((xp - floor) / (float)(next - floor)) : 1f;
                _xpFill.anchorMax = new Vector2(Mathf.Max(0.02f, t), 1f);
            }
        }

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
