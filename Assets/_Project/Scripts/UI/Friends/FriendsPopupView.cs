using System;
using System.Collections.Generic;
using TMPro;
using TransportManager.Core;
using TransportManager.Events;
using TransportManager.Social;
using TransportManager.Systems.Daily;
using TransportManager.Systems.Social;
using TransportManager.UI.Common;
using UnityEngine;
using UnityEngine.UI;

namespace TransportManager.UI.Friends
{
    public class FriendsPopupView : MonoBehaviour
    {
        // Palette partagée (Header / Navbar / ContractsPanel / VehiclesTab)
        private static readonly Color32 BgOverlay  = new Color32(0x00, 0x00, 0x00, 200);
        private static readonly Color32 BgPanel    = new Color32(0x2C, 0x30, 0x38, 255);
        private static readonly Color32 BgCard     = new Color32(0x34, 0x38, 0x42, 255);
        private static readonly Color32 BgPill     = new Color32(0x1A, 0x1D, 0x24, 230);
        private static readonly Color32 Accent     = new Color32(0x3D, 0xC9, 0x6E, 255);
        private static readonly Color32 AccentBlue = new Color32(0x35, 0x8E, 0xF5, 255);
        private static readonly Color32 TextPri    = new Color32(0xEC, 0xEE, 0xF5, 255);
        private static readonly Color32 TextSec    = new Color32(0x7A, 0x8F, 0xA6, 255);
        private static readonly Color32 TextMuted  = new Color32(0x5A, 0x65, 0x77, 255);
        private static readonly Color32 Divider    = new Color32(0x3A, 0x3F, 0x4A, 150);

        private Sprite    _sprRound12;
        private Sprite    _sprRound8;
        private Sprite    _sprPill;
        private Transform _listContent;
        private readonly System.Collections.Generic.Dictionary<string, bool> _routeVisible =
            new System.Collections.Generic.Dictionary<string, bool>();

        private const int TitleBarH = 56;
        private const int TabBarH   = 48;

        private int      _activeTab; // 0 = Amis, 1 = Classement
        private Image    _tabAmisBg, _tabRankBg;
        private TMP_Text _tabAmisLbl, _tabRankLbl;

        // ── Entry point ───────────────────────────────────────────────────────

        public static void Show()
        {
            if (FindObjectOfType<FriendsPopupView>() != null) return;
            new GameObject("FriendsPopup", typeof(RectTransform)).AddComponent<FriendsPopupView>();
        }

        private void Awake()
        {
            _sprRound12 = MakeRoundedSprite(12);
            _sprRound8  = MakeRoundedSprite(8);
            _sprPill    = MakeRoundedSprite(32);
            BuildUI();
        }

        // ── Build ─────────────────────────────────────────────────────────────

        private void BuildUI()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight  = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            var overlay = MakeImg("Overlay", transform, BgOverlay);
            overlay.raycastTarget = true;
            overlay.gameObject.AddComponent<Button>().onClick.AddListener(Close);
            FillParent(overlay.GetComponent<RectTransform>());

            var panelGo  = MakeGO("Panel", transform);
            var panelImg = panelGo.AddComponent<Image>();
            panelImg.sprite        = _sprRound12;
            panelImg.type          = Image.Type.Sliced;
            panelImg.color         = BgPanel;
            panelImg.raycastTarget = true;
            var panelShadow = panelGo.AddComponent<Shadow>();
            panelShadow.effectColor    = new Color(0f, 0f, 0f, 0.5f);
            panelShadow.effectDistance = new Vector2(0f, -4f);
            var panelRt = panelGo.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.05f, 0.06f);
            panelRt.anchorMax = new Vector2(0.95f, 0.94f);
            panelRt.offsetMin = Vector2.zero;
            panelRt.offsetMax = Vector2.zero;

            PopupHeader.Build(panelGo.transform, "Social", Close, TitleBarH, _sprRound8);
            BuildTabBar(panelGo.transform);
            BuildScrollArea(panelGo.transform);
            SetTab(0);
        }

        // ── Onglets (Amis | Classement) ───────────────────────────────────────

        private void BuildTabBar(Transform parent)
        {
            int top = TitleBarH + 1;
            var bar   = MakeGO("TabBar", parent);
            var barRt = bar.GetComponent<RectTransform>();
            barRt.anchorMin = new Vector2(0, 1);
            barRt.anchorMax = new Vector2(1, 1);
            barRt.pivot     = new Vector2(0.5f, 1);
            barRt.offsetMin = new Vector2(14, -(top + TabBarH));
            barRt.offsetMax = new Vector2(-14, -top);

            var hlg = bar.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing                = 8;
            hlg.padding                = new RectOffset(0, 0, 6, 6);
            hlg.childAlignment         = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth  = true;  hlg.childControlWidth  = true;
            hlg.childForceExpandHeight = true;  hlg.childControlHeight = true;

            (_tabAmisBg, _tabAmisLbl) = BuildTab(bar.transform, "Amis", 0);
            (_tabRankBg, _tabRankLbl) = BuildTab(bar.transform, "Classement", 1);
        }

        private (Image, TMP_Text) BuildTab(Transform parent, string label, int index)
        {
            var go  = MakeGO("Tab" + index, parent);
            var img = go.AddComponent<Image>();
            img.sprite = _sprRound8; img.type = Image.Type.Sliced; img.color = BgPill;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => SetTab(index));
            var lbl = AddTMP("Lbl", go.transform, label, 14, FontStyles.Bold, TextSec);
            lbl.alignment = TextAlignmentOptions.Center; lbl.raycastTarget = false;
            FillParent(lbl.GetComponent<RectTransform>());
            return (img, lbl);
        }

        private void SetTab(int index)
        {
            _activeTab = index;
            if (_tabAmisBg) { _tabAmisBg.color = index == 0 ? AccentBlue : BgPill; _tabAmisLbl.color = index == 0 ? TextPri : TextSec; }
            if (_tabRankBg) { _tabRankBg.color = index == 1 ? AccentBlue : BgPill; _tabRankLbl.color = index == 1 ? TextPri : TextSec; }
            RefreshContent();
        }

        private void BuildInviteButton()
        {
            var go  = MakeGO("BtnInvite", _listContent);
            var img = go.AddComponent<Image>();
            img.sprite = _sprRound8; img.type = Image.Type.Sliced; img.color = Accent;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(OnInviteFriend);
            go.AddComponent<LayoutElement>().preferredHeight = 50;
            var lbl = AddTMP("Lbl", go.transform, "Inviter un ami", 15, FontStyles.Bold, TextPri);
            lbl.alignment = TextAlignmentOptions.Center; lbl.raycastTarget = false;
            FillParent(lbl.GetComponent<RectTransform>());
            MakeGO("SpInv", _listContent).AddComponent<LayoutElement>().preferredHeight = 6;
        }

        // ── Scroll area (friends list) ────────────────────────────────────────

        private void BuildScrollArea(Transform parent)
        {
            int scrollTopOffset = TitleBarH + 1 + TabBarH;

            var scrollGo = MakeGO("Scroll", parent);
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.anchorMin = new Vector2(0, 0);
            scrollRt.anchorMax = new Vector2(1, 1);
            scrollRt.offsetMin = new Vector2(0, 0);
            scrollRt.offsetMax = new Vector2(0, -scrollTopOffset);

            var scrollRect = scrollGo.AddComponent<ScrollRect>();
            scrollRect.horizontal        = false;
            scrollRect.vertical          = true;
            scrollRect.scrollSensitivity = 50;
            scrollRect.movementType      = ScrollRect.MovementType.Elastic;

            var viewport = MakeGO("Viewport", scrollGo.transform);
            FillParent(viewport.GetComponent<RectTransform>());
            viewport.AddComponent<RectMask2D>();
            scrollRect.viewport = viewport.GetComponent<RectTransform>();

            var content   = MakeGO("Content", viewport.transform);
            var contentRt = content.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot     = new Vector2(0.5f, 1f);
            contentRt.sizeDelta = Vector2.zero;

            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.padding               = new RectOffset(14, 14, 10, 16);
            vlg.spacing               = 8;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth      = true;
            vlg.childControlHeight     = true;
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scrollRect.content = contentRt;

            _listContent = content.transform;
        }

        // ── Contenu selon l'onglet ────────────────────────────────────────────

        private void RefreshContent()
        {
            if (_listContent == null) return;
            for (int i = _listContent.childCount - 1; i >= 0; i--) Destroy(_listContent.GetChild(i).gameObject);
            if (_activeTab == 0) BuildAmisContent();
            else                 BuildClassementContent();
        }

        private void BuildAmisContent()
        {
            BuildInviteButton();

            var sectionLbl = AddTMP("LblSection", _listContent, "MES AMIS", 10, FontStyles.Bold, TextMuted);
            sectionLbl.characterSpacing = 120;
            sectionLbl.gameObject.AddComponent<LayoutElement>().preferredHeight = 16;

            var friends = FriendsData.LoadAll();
#if UNITY_EDITOR
            if (friends.Count == 0)
            {
                friends.Add(new FriendEntry { uid = "test_001", companyName = "Dupont Transport",  level = 7,  vehicleCount = 4 });
                friends.Add(new FriendEntry { uid = "test_002", companyName = "Martin Logistics",  level = 12, vehicleCount = 9 });
                friends.Add(new FriendEntry { uid = "test_003", companyName = "Express du Sud",    level = 3,  vehicleCount = 1 });
            }
#endif
            if (friends.Count == 0)
                BuildEmptyState();
            else
                foreach (var f in friends)
                    BuildFriendCard(f);
        }

        // ── Classement (par km) ───────────────────────────────────────────────

        private void BuildClassementContent()
        {
            var daily = ServiceLocator.Get<DailySystem>();
            if (daily != null && daily.LeaguePending) BuildLeagueRewardBanner(daily);

            var world  = Leaderboard.BuildWorld();
            var player = Leaderboard.PlayerEntry(world);
            int li     = Leaderboard.LeagueIndex(player?.km ?? 0);
            Color32 lc = Leaderboard.LeagueColor(li);
            string  ln = Leaderboard.LeagueName(li);

            // En-tête : ligue + rang mondial + reset
            var hero = LbCard();
            var hr   = LbRowGO(hero.transform, 30);
            var badge = AddTMP("Badge", hr.transform, $"◆ LIGUE {ln.ToUpper()}", 14, FontStyles.Bold, lc);
            badge.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            var rk = AddTMP("Rank", hr.transform, $"Mondial #{player?.worldRank ?? 0}", 14, FontStyles.Bold, TextPri);
            rk.alignment = TextAlignmentOptions.MidlineRight;
            rk.gameObject.AddComponent<LayoutElement>().preferredWidth = 150;
            var sub = AddTMP("Sub", hero.transform, $"Classement de la semaine · réinitialise dans {Leaderboard.DaysUntilReset} j", 11.5f, FontStyles.Normal, TextMuted);
            sub.gameObject.AddComponent<LayoutElement>().preferredHeight = 16;

            // Le 1er de ta ligue (+ ta position dans la ligue)
            LbSection($"TA LIGUE — {ln.ToUpper()}");
            var leader = Leaderboard.LeagueLeader(world, li);
            if (leader != null) LbRow("1er", leader);
            if (player != null && player != leader)
                LbRow(Leaderboard.LeagueRank(world, player) + "e", player);

            // Les premiers mondiaux
            LbSection("TOP MONDIAL");
            bool playerInTop = false;
            foreach (var e in Leaderboard.WorldTop(world, 6))
            {
                LbRow("#" + e.worldRank, e);
                if (e.isPlayer) playerInTop = true;
            }
            if (!playerInTop && player != null) LbRow("#" + player.worldRank, player);

            // Tes amis
            LbSection("TES AMIS");
            foreach (var e in Leaderboard.FriendsBoard()) LbRow("#" + e.worldRank, e);
        }

        private void BuildLeagueRewardBanner(DailySystem daily)
        {
            int li = daily.LeaguePendingLeague;
            Color32 lc = Leaderboard.LeagueColor(li);

            var card = LbCard();
            card.GetComponent<Image>().color = new Color(lc.r / 255f, lc.g / 255f, lc.b / 255f, 0.16f);
            var ol = card.AddComponent<Outline>();
            ol.effectColor = new Color(lc.r / 255f, lc.g / 255f, lc.b / 255f, 0.7f);
            ol.effectDistance = new Vector2(1.5f, -1.5f);

            var t = AddTMP("Lt", card.transform, $"RÉCOMPENSE DE LIGUE · {Leaderboard.LeagueName(li).ToUpper()}", 13, FontStyles.Bold, lc);
            t.gameObject.AddComponent<LayoutElement>().preferredHeight = 18;
            AddTMP("Ld", card.transform, $"Fini #{daily.LeaguePendingRank} de ta ligue la semaine dernière.", 12, FontStyles.Normal, TextSec)
                .gameObject.AddComponent<LayoutElement>().preferredHeight = 16;

            var row = LbRowGO(card.transform, 34);
            string reward = $"+{daily.LeaguePendingDollars:N0} $" + (daily.LeaguePendingGold > 0 ? $"   +{daily.LeaguePendingGold} ◆" : "");
            var rl = AddTMP("Lr", row.transform, reward, 15, FontStyles.Bold, new Color32(0xF2, 0xD9, 0x66, 255));
            rl.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

            var b  = MakeGO("LbClaim", row.transform);
            var bi = b.AddComponent<Image>();
            bi.sprite = _sprRound8; bi.type = Image.Type.Sliced; bi.color = Accent;
            var bt = b.AddComponent<Button>();
            bt.targetGraphic = bi;
            var ble = b.AddComponent<LayoutElement>();
            ble.preferredWidth = 130; ble.preferredHeight = 34;
            var bl = AddTMP("Lbl", b.transform, "Récupérer", 13, FontStyles.Bold, TextPri);
            bl.alignment = TextAlignmentOptions.Center; bl.raycastTarget = false;
            FillParent(bl.GetComponent<RectTransform>());
            bt.onClick.AddListener(() => { if (daily.ClaimLeague()) RefreshContent(); });
        }

        private GameObject LbCard()
        {
            var c = MakeGO("LbCard", _listContent);
            var img = c.AddComponent<Image>();
            img.sprite = _sprRound12; img.type = Image.Type.Sliced; img.color = BgCard;
            var v = c.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(14, 14, 12, 12); v.spacing = 4;
            v.childForceExpandWidth = true; v.childControlWidth = true;
            v.childForceExpandHeight = false; v.childControlHeight = true;
            c.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return c;
        }

        private GameObject LbRowGO(Transform parent, int minHeight)
        {
            var go = MakeGO("R", parent);
            var h = go.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 10; h.childAlignment = TextAnchor.MiddleLeft;
            h.childForceExpandWidth = false; h.childControlWidth = true;
            h.childForceExpandHeight = false; h.childControlHeight = true;
            go.AddComponent<LayoutElement>().minHeight = minHeight;
            return go;
        }

        private void LbSection(string text)
        {
            var t = AddTMP("Sec", _listContent, text, 10, FontStyles.Bold, TextMuted);
            t.characterSpacing = 120;
            t.gameObject.AddComponent<LayoutElement>().preferredHeight = 18;
        }

        private void LbRow(string rankText, LeaderboardEntry e)
        {
            var go  = MakeGO("Lb", _listContent);
            var img = go.AddComponent<Image>();
            img.sprite = _sprRound8; img.type = Image.Type.Sliced;
            img.color  = e.isPlayer ? new Color(AccentBlue.r / 255f, AccentBlue.g / 255f, AccentBlue.b / 255f, 0.22f) : (Color)(Color32)BgPill;
            if (e.isPlayer) { var ol = go.AddComponent<Outline>(); ol.effectColor = AccentBlue; ol.effectDistance = new Vector2(1.5f, -1.5f); }
            go.AddComponent<LayoutElement>().preferredHeight = 44;
            var h = go.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(12, 12, 0, 0); h.spacing = 10; h.childAlignment = TextAnchor.MiddleLeft;
            h.childForceExpandWidth = false; h.childControlWidth = true;
            h.childForceExpandHeight = true; h.childControlHeight = true;

            Color32 rankCol = e.worldRank == 1 ? new Color32(0xF2, 0xD9, 0x66, 255) : TextSec;
            var rkLbl = AddTMP("Rk", go.transform, rankText, 14, FontStyles.Bold, rankCol);
            rkLbl.alignment = TextAlignmentOptions.Center;
            rkLbl.gameObject.AddComponent<LayoutElement>().preferredWidth = 46;

            string tag = e.isPlayer ? "  <size=10><color=#5fa8ff>(vous)</color></size>"
                       : e.isFriend ? "  <size=10><color=#3DC96E>(ami)</color></size>" : "";
            var nm = AddTMP("Nm", go.transform, e.name + tag, 14, e.isPlayer ? FontStyles.Bold : FontStyles.Normal, TextPri);
            nm.richText = true; nm.textWrappingMode = TextWrappingModes.NoWrap; nm.overflowMode = TextOverflowModes.Ellipsis;
            nm.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

            var kmLbl = AddTMP("Km", go.transform, $"{e.km:N0} km", 13, FontStyles.Bold, TextSec);
            kmLbl.alignment = TextAlignmentOptions.MidlineRight;
            kmLbl.gameObject.AddComponent<LayoutElement>().preferredWidth = 120;
        }

        private void BuildEmptyState()
        {
            var go = MakeGO("Empty", _listContent);
            go.AddComponent<LayoutElement>().preferredHeight = 160;

            var lbl = AddTMP("Lbl", go.transform,
                             "Aucun ami pour l'instant.\nInvite tes amis pour voir\nleurs entreprises !",
                             14, FontStyles.Normal, TextMuted);
            lbl.alignment        = TextAlignmentOptions.Center;
            lbl.textWrappingMode = TextWrappingModes.Normal;
            FillParent(lbl.GetComponent<RectTransform>());
        }

        private void BuildFriendCard(FriendEntry friend)
        {
            var card    = MakeGO("Card_" + friend.uid, _listContent);
            var cardImg = card.AddComponent<Image>();
            cardImg.sprite        = _sprRound12;
            cardImg.type          = Image.Type.Sliced;
            cardImg.color         = BgCard;
            cardImg.raycastTarget = false;
            card.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var vlg = card.AddComponent<VerticalLayoutGroup>();
            vlg.padding               = new RectOffset(14, 14, 12, 12);
            vlg.spacing               = 10;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth      = true;
            vlg.childControlHeight     = true;

            // ── Info row ──
            var infoRow = MakeGO("InfoRow", card.transform);
            infoRow.AddComponent<LayoutElement>().preferredHeight = 44;
            var infoHlg = infoRow.AddComponent<HorizontalLayoutGroup>();
            infoHlg.childAlignment        = TextAnchor.MiddleLeft;
            infoHlg.spacing               = 10;
            infoHlg.childForceExpandWidth  = false;
            infoHlg.childForceExpandHeight = true;
            infoHlg.childControlWidth      = false;
            infoHlg.childControlHeight     = true;

            // Avatar circle with initial
            var avatarGo  = MakeGO("Avatar", infoRow.transform);
            var avatarImg = avatarGo.AddComponent<Image>();
            avatarImg.sprite = _sprPill;
            avatarImg.type   = Image.Type.Sliced;
            avatarImg.color  = new Color32(0x35, 0x8E, 0xF5, 50);
            var avatarLe = avatarGo.AddComponent<LayoutElement>();
            avatarLe.preferredWidth  = 40;
            avatarLe.preferredHeight = 40;

            string initial = friend.companyName.Length > 0
                ? friend.companyName[0].ToString().ToUpper()
                : "?";
            var initLbl = AddTMP("Initial", avatarGo.transform, initial, 18, FontStyles.Bold, AccentBlue);
            initLbl.alignment = TextAlignmentOptions.Center;
            FillParent(initLbl.GetComponent<RectTransform>());

            // Name + level column
            var infoCol = MakeGO("InfoCol", infoRow.transform);
            infoCol.AddComponent<LayoutElement>().flexibleWidth = 1;
            var infoColVlg = infoCol.AddComponent<VerticalLayoutGroup>();
            infoColVlg.childAlignment        = TextAnchor.MiddleLeft;
            infoColVlg.childForceExpandWidth  = true;
            infoColVlg.childForceExpandHeight = false;
            infoColVlg.childControlWidth      = true;
            infoColVlg.childControlHeight     = true;

            var nameLbl = AddTMP("Name", infoCol.transform, friend.companyName, 15, FontStyles.Bold, TextPri);
            nameLbl.textWrappingMode = TextWrappingModes.NoWrap;
            nameLbl.overflowMode     = TextOverflowModes.Ellipsis;
            nameLbl.gameObject.AddComponent<LayoutElement>().preferredHeight = 20;

            string truckLabel = friend.vehicleCount > 1 ? "camions" : "camion";
            var levelLbl = AddTMP("Level", infoCol.transform,
                                   $"Niveau {friend.level} · {friend.vehicleCount} {truckLabel}",
                                   12, FontStyles.Normal, TextSec);
            levelLbl.gameObject.AddComponent<LayoutElement>().preferredHeight = 16;

            // ── Divider ──
            var div    = MakeGO("Div", card.transform);
            var divImg = div.AddComponent<Image>();
            divImg.color         = Divider;
            divImg.raycastTarget = false;
            div.AddComponent<LayoutElement>().preferredHeight = 1;

            // ── Action buttons row ──
            var actRow = MakeGO("ActRow", card.transform);
            actRow.AddComponent<LayoutElement>().preferredHeight = 36;
            var actHlg = actRow.AddComponent<HorizontalLayoutGroup>();
            actHlg.spacing               = 6;
            actHlg.childAlignment        = TextAnchor.MiddleCenter;
            actHlg.childForceExpandWidth  = true;
            actHlg.childForceExpandHeight = true;
            actHlg.childControlWidth      = true;
            actHlg.childControlHeight     = true;

            string uid = friend.uid;
            AddActionBtn(actRow.transform, "Dépôt",  "warehouse", () => OnViewDepot(uid));
            AddActionBtn(actRow.transform, "Camions", "truck", () => OnViewTrucks(uid));
            AddToggleRouteBtn(actRow.transform, uid);
        }

        private void AddToggleRouteBtn(Transform parent, string uid)
        {
            var go  = MakeGO("Btn_Trajets", parent);
            var img = go.AddComponent<Image>();
            img.sprite = _sprRound8;
            img.type   = Image.Type.Sliced;
            img.color  = BgPill;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment        = TextAnchor.MiddleCenter;
            hlg.spacing               = 5;
            hlg.padding               = new RectOffset(6, 6, 4, 4);
            hlg.childForceExpandWidth  = false;
            hlg.childForceExpandHeight = false;
            hlg.childControlWidth      = false;
            hlg.childControlHeight     = false;

            var iconGo  = MakeGO("Icon", go.transform);
            var iconImg = iconGo.AddComponent<Image>();
            iconImg.preserveAspect = true;
            iconImg.raycastTarget  = false;
            iconGo.GetComponent<RectTransform>().sizeDelta = new Vector2(14, 14);

            var lbl = AddTMP("Lbl", go.transform, "Trajets", 12, FontStyles.Normal, TextSec);
            lbl.raycastTarget = false;

            bool isOn = _routeVisible.TryGetValue(uid, out bool v) && v;
            RefreshRouteBtn(iconImg, lbl, isOn);

            btn.onClick.AddListener(() =>
            {
                bool nowOn = !(_routeVisible.TryGetValue(uid, out bool cur) && cur);
                _routeVisible[uid] = nowOn;
                RefreshRouteBtn(iconImg, lbl, nowOn);
                if (nowOn) GameEvents.RaiseFriendRoutesRequested(uid);
                else       GameEvents.RaiseFriendRoutesHidden(uid);
            });
        }

        private static void RefreshRouteBtn(Image iconImg, TMP_Text lbl, bool isOn)
        {
            UiIcons.Apply(iconImg, isOn ? "UI/Icons/icons/eye" : "UI/Icons/icons/eye-off");
            iconImg.color  = isOn ? new Color32(0x3D, 0xC9, 0x6E, 255) : new Color32(0x7A, 0x8F, 0xA6, 255);
            lbl.text       = isOn ? "Masquer" : "Trajets";
            lbl.color      = isOn ? new Color32(0x3D, 0xC9, 0x6E, 255) : new Color32(0x7A, 0x8F, 0xA6, 255);
        }

        private void AddActionBtn(Transform parent, string label, string iconName, Action onClick)
        {
            var go  = MakeGO("Btn_" + label, parent);
            var img = go.AddComponent<Image>();
            img.sprite = _sprRound8;
            img.type   = Image.Type.Sliced;
            img.color  = BgPill;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => onClick?.Invoke());

            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment        = TextAnchor.MiddleCenter;
            hlg.spacing               = 5;
            hlg.padding               = new RectOffset(6, 6, 4, 4);
            hlg.childForceExpandWidth  = false;
            hlg.childForceExpandHeight = false;
            hlg.childControlWidth      = false;
            hlg.childControlHeight     = false;

            var iconGo  = MakeGO("Icon", go.transform);
            var iconImg = iconGo.AddComponent<Image>();
            UiIcons.Apply(iconImg, $"UI/Icons/icons/{iconName}");
            iconImg.color          = TextSec;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget  = false;
            iconGo.GetComponent<RectTransform>().sizeDelta = new Vector2(14, 14);

            var lbl = AddTMP("Lbl", go.transform, label, 12, FontStyles.Normal, TextSec);
            lbl.raycastTarget = false;
        }

        // ── Actions ───────────────────────────────────────────────────────────

        private void OnInviteFriend()
        {
            var gm = GameManager.Instance;
            string companyName = gm?.Save?.company?.companyName ?? "Mon Entreprise";
            string link        = FriendsData.GenerateInviteLink();
            string message     = $"{companyName} t'invite à rejoindre Transport Manager ! {link}";
            NativeShare.ShareText(message);
        }

        private void OnViewDepot(string uid)
        {
            Debug.Log($"[Friends] Voir dépôt de {uid}");
            GameEvents.RaiseFriendDepotRequested(uid);
            Close();
        }

        private void OnViewTrucks(string uid)
        {
            Debug.Log($"[Friends] Voir camions de {uid}");
            GameEvents.RaiseFriendTrucksRequested(uid);
            Close();
        }

        private void Close() => Destroy(gameObject);

        // ── Helpers ───────────────────────────────────────────────────────────

        private static GameObject MakeGO(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static Image MakeImg(string name, Transform parent, Color32 color)
        {
            var go  = MakeGO(name, parent);
            var img = go.AddComponent<Image>();
            img.color = color;
            return img;
        }

        private static TMP_Text AddTMP(string name, Transform parent, string text,
                                       float size, FontStyles style, Color32 color)
        {
            var go  = MakeGO(name, parent);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text          = text;
            tmp.fontSize      = size;
            tmp.fontStyle     = style;
            tmp.color         = color;
            tmp.alignment     = TextAlignmentOptions.MidlineLeft;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static void FillParent(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static Sprite MakeRoundedSprite(int radius)
        {
            const int size = 64;
            int r   = Mathf.Clamp(radius, 1, size / 2);
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
            return Sprite.Create(tex, new Rect(0, 0, size, size),
                                 new Vector2(0.5f, 0.5f), 100f, 0,
                                 SpriteMeshType.FullRect, new Vector4(r, r, r, r));
        }

        private static float RoundedAlpha(int x, int y, int size, int r)
        {
            int cx = -1, cy = -1;
            if      (x < r         && y < r)          { cx = r;        cy = r;        }
            else if (x >= size - r && y < r)          { cx = size - r; cy = r;        }
            else if (x < r         && y >= size - r)  { cx = r;        cy = size - r; }
            else if (x >= size - r && y >= size - r)  { cx = size - r; cy = size - r; }
            if (cx < 0) return 1f;
            float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
            return Mathf.Clamp01(r - d + 0.5f);
        }
    }
}
