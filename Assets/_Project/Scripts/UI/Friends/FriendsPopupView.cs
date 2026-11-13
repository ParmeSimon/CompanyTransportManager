using System.Collections.Generic;
using TMPro;
using TransportManager.Core;
using TransportManager.Social;
using TransportManager.Systems.Daily;
using TransportManager.Systems.Social;
using UnityEngine;
using UnityEngine.UI;

namespace TransportManager.UI.Friends
{
    /// <summary>
    /// Écran Social (plein écran, « Retour ») :
    ///   gauche  → ma ligue (rang, km de la semaine, progression vers la ligue suivante) + mes amis
    ///   droite  → classement filtrable (Ma ligue / Monde / Amis), podium pour le top 3.
    /// Visiter un ami (dépôt, camions, trajets) nécessite un serveur : boutons « Bientôt ».
    /// </summary>
    public class FriendsPopupView : MonoBehaviour
    {
        private static readonly Color32 BgPage    = new Color32(0x1A, 0x1D, 0x24, 255);
        private static readonly Color32 BgBar     = new Color32(0x22, 0x26, 0x2E, 255);
        private static readonly Color32 BgCard    = new Color32(0x2C, 0x30, 0x38, 255);
        private static readonly Color32 BgRow     = new Color32(0x34, 0x38, 0x42, 255);
        private static readonly Color32 Green     = new Color32(0x3D, 0xC9, 0x6E, 255);
        private static readonly Color32 Blue      = new Color32(0x38, 0x85, 0xFF, 255);
        private static readonly Color32 Gold      = new Color32(0xF2, 0xD9, 0x66, 255);
        private static readonly Color32 Silver    = new Color32(0xC0, 0xC8, 0xD4, 255);
        private static readonly Color32 Bronze    = new Color32(0xCD, 0x7F, 0x4B, 255);
        private static readonly Color32 TextPri   = new Color32(0xEC, 0xEE, 0xF5, 255);
        private static readonly Color32 TextSec   = new Color32(0x9A, 0xA6, 0xB8, 255);
        private static readonly Color32 TextDim   = new Color32(0x66, 0x70, 0x82, 255);

        private const float TopBarH = 64f;

        private Sprite _r16, _r12, _r8, _circle;
        private Transform _leftContent, _boardContent;
        private int _boardFilter;   // 0 = ma ligue, 1 = monde, 2 = amis
        private readonly List<(Image bg, TMP_Text lbl)> _filterTabs = new List<(Image, TMP_Text)>();

        public static void Show()
        {
            if (FindFirstObjectByType<FriendsPopupView>() != null) return;
            new GameObject("SocialScreen", typeof(RectTransform)).AddComponent<FriendsPopupView>();
        }

        private void Awake()
        {
            _r16 = MakeRounded(16); _r12 = MakeRounded(12); _r8 = MakeRounded(8); _circle = MakeRounded(32);
            Build();
        }

        private void Update()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;   // bouton retour Android / Échap
            if (kb != null && kb.escapeKey.wasPressedThisFrame) Close();
        }

        // ── Structure ────────────────────────────────────────────────────────────────
        private void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 800;   // écran plein, au-dessus du HUD
            UiScale.Configure(gameObject.AddComponent<CanvasScaler>());
            gameObject.AddComponent<GraphicRaycaster>();

            var bg = Img("Background", transform, BgPage);
            Fill(bg.rectTransform);

            Vector4 ins = SafeAreaUtil.Insets(canvas);
            var root = Go("Safe", transform);
            var rRt = (RectTransform)root.transform;
            rRt.anchorMin = Vector2.zero; rRt.anchorMax = Vector2.one;
            rRt.offsetMin = new Vector2(ins.x, ins.w); rRt.offsetMax = new Vector2(-ins.y, -ins.z);

            BuildTopBar(root.transform);

            // Deux colonnes sous la barre
            var body = Go("Body", root.transform);
            var bRt = (RectTransform)body.transform;
            bRt.anchorMin = Vector2.zero; bRt.anchorMax = Vector2.one;
            bRt.offsetMin = new Vector2(20, 16); bRt.offsetMax = new Vector2(-20, -(TopBarH + 16));
            var hlg = body.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 20;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = true;
            hlg.childControlWidth = true; hlg.childControlHeight = true;

            _leftContent  = Column(body.transform, "Left", 0.44f);
            var right = Go("Right", body.transform);
            var rLe = right.AddComponent<LayoutElement>(); rLe.flexibleWidth = 0.56f;
            var rv = right.AddComponent<VerticalLayoutGroup>();
            rv.spacing = 12; rv.childForceExpandWidth = true; rv.childForceExpandHeight = false;
            rv.childControlWidth = true; rv.childControlHeight = true;
            BuildBoardHeader(right.transform);
            _boardContent = ScrollList(right.transform, "Board");

            RefreshLeft();
            RefreshBoard();
        }

        private void BuildTopBar(Transform parent)
        {
            var bar = Img("TopBar", parent, BgBar);
            var rt = bar.rectTransform;
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0.5f, 1);
            rt.offsetMin = new Vector2(0, -TopBarH); rt.offsetMax = Vector2.zero;
            var hlg = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(12, 16, 10, 10); hlg.spacing = 16;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
            hlg.childControlWidth = true; hlg.childControlHeight = true;

            TransportManager.UI.Common.BackButton.Create(bar.transform, Close);
            var title = Txt("Title", bar.transform, "Social", 22, FontStyles.Bold, TextPri);
            title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            MakeButton(bar.transform, "+  Inviter un ami", Green, Color.white, 190, OnInviteFriend);
        }

        // ── Colonne gauche : ma ligue + mes amis ─────────────────────────────────────
        private void RefreshLeft()
        {
            Clear(_leftContent);
            var daily = ServiceLocator.Get<DailySystem>();
            if (daily != null && daily.LeaguePending) BuildLeagueReward(daily);
            BuildLeagueHero();

            var friends = LoadFriends();
            Section(_leftContent, $"MES AMIS  ·  {friends.Count}");
            if (friends.Count == 0) { BuildNoFriends(); return; }
            foreach (var f in friends) BuildFriendRow(f);
            var note = Txt("Note", _leftContent,
                "Visiter le dépôt, la flotte et les trajets de tes amis arrive avec le mode en ligne.",
                11, FontStyles.Italic, TextDim);
            note.textWrappingMode = TextWrappingModes.Normal;
        }

        private void BuildLeagueHero()
        {
            var world  = Leaderboard.BuildWorld();
            var player = Leaderboard.PlayerEntry(world);
            long km    = player?.km ?? 0;
            int li     = Leaderboard.LeagueIndex(km);
            Color32 lc = Leaderboard.LeagueColor(li);

            var card = Card(_leftContent, BgCard);
            var top = Row(card.transform, 8);
            var badge = Pill(top.transform, $"LIGUE {Leaderboard.LeagueName(li).ToUpperInvariant()}", lc);
            var spacer = Go("Sp", top.transform); spacer.AddComponent<LayoutElement>().flexibleWidth = 1;
            Txt("Reset", top.transform, $"Fin dans {Leaderboard.DaysUntilReset} j", 11, FontStyles.Bold, TextDim);

            var big = Row(card.transform, 16);
            var rank = Txt("Rank", big.transform, $"#{(player != null ? Leaderboard.LeagueRank(world, player) : 0)}", 38, FontStyles.Bold, lc);
            rank.textWrappingMode = TextWrappingModes.NoWrap;
            var col = Go("Col", big.transform);
            var cv = col.AddComponent<VerticalLayoutGroup>();
            cv.childForceExpandWidth = true; cv.childForceExpandHeight = false;
            cv.childControlWidth = true; cv.childControlHeight = true; cv.spacing = 2;
            col.AddComponent<LayoutElement>().flexibleWidth = 1;
            Txt("Km", col.transform, $"{km:N0} km cette semaine", 16, FontStyles.Bold, TextPri);
            Txt("World", col.transform, $"Rang mondial #{player?.worldRank ?? 0}", 12, FontStyles.Normal, TextSec);

            // Progression vers la ligue suivante
            if (li < Leaderboard.LeagueCount - 1)
            {
                long from = Leaderboard.LeagueMinKm(li), to = Leaderboard.LeagueMinKm(li + 1);
                float t = to > from ? Mathf.Clamp01((km - from) / (float)(to - from)) : 1f;
                ProgressBar(card.transform, t, Leaderboard.LeagueColor(li + 1));
                Txt("Next", card.transform,
                    $"Encore {Mathf.Max(0, to - km):N0} km pour la ligue {Leaderboard.LeagueName(li + 1)}",
                    11, FontStyles.Normal, TextSec);
            }
            else Txt("Top", card.transform, "Ligue maximale atteinte", 11, FontStyles.Bold, lc);
        }

        private void BuildLeagueReward(DailySystem daily)
        {
            int li = daily.LeaguePendingLeague;
            Color32 lc = Leaderboard.LeagueColor(li);
            var card = Card(_leftContent, new Color32(lc.r, lc.g, lc.b, 40));
            Txt("T", card.transform, $"Récompense de ligue {Leaderboard.LeagueName(li)}", 14, FontStyles.Bold, lc);
            Txt("D", card.transform, $"Tu as fini #{daily.LeaguePendingRank} de ta ligue la semaine dernière.", 12, FontStyles.Normal, TextSec);
            var row = Row(card.transform, 10);
            string reward = $"+{daily.LeaguePendingDollars:N0} $" + (daily.LeaguePendingGold > 0 ? $"   +{daily.LeaguePendingGold} lingots" : "");
            var rl = Txt("R", row.transform, reward, 16, FontStyles.Bold, Gold);
            rl.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            MakeButton(row.transform, "Récupérer", Green, Color.white, 140, () => { if (daily.ClaimLeague()) RefreshLeft(); });
        }

        private void BuildFriendRow(FriendEntry f)
        {
            var card = Card(_leftContent, BgCard);
            var top = Row(card.transform, 12);
            Avatar(top.transform, f.companyName, Blue);
            var col = Go("Col", top.transform);
            var cv = col.AddComponent<VerticalLayoutGroup>();
            cv.childForceExpandWidth = true; cv.childForceExpandHeight = false;
            cv.childControlWidth = true; cv.childControlHeight = true; cv.spacing = 2;
            col.AddComponent<LayoutElement>().flexibleWidth = 1;
            var nm = Txt("Name", col.transform, f.companyName, 15, FontStyles.Bold, TextPri);
            nm.textWrappingMode = TextWrappingModes.NoWrap; nm.overflowMode = TextOverflowModes.Ellipsis;
            Txt("Sub", col.transform, $"Niveau {f.level}  ·  {f.vehicleCount} camion{(f.vehicleCount > 1 ? "s" : "")}", 12, FontStyles.Normal, TextSec);

            var acts = Row(card.transform, 8);
            foreach (var label in new[] { "Dépôt", "Camions", "Trajets" })
            {
                var b = Img("Act_" + label, acts.transform, BgRow);
                b.sprite = _r8; b.type = Image.Type.Sliced;
                var le = b.gameObject.AddComponent<LayoutElement>(); le.flexibleWidth = 1; le.preferredHeight = 38;
                var bl = Txt("L", b.transform, $"{label}  <size=75%><color=#666F82>bientôt</color></size>", 12, FontStyles.Bold, TextDim);
                bl.alignment = TextAlignmentOptions.Center; Fill(bl.rectTransform);
                bl.textWrappingMode = TextWrappingModes.NoWrap;
                bl.enableAutoSizing = true; bl.fontSizeMin = 9; bl.fontSizeMax = 12;
            }
        }

        private void BuildNoFriends()
        {
            var card = Card(_leftContent, BgCard);
            var t = Txt("T", card.transform, "Pas encore d'amis", 15, FontStyles.Bold, TextPri);
            t.alignment = TextAlignmentOptions.Center;
            var d = Txt("D", card.transform, "Invite tes amis : comparez vos kilomètres chaque semaine et grimpez les ligues ensemble.",
                        12, FontStyles.Normal, TextSec);
            d.alignment = TextAlignmentOptions.Center; d.textWrappingMode = TextWrappingModes.Normal;
            MakeButton(card.transform, "+  Inviter un ami", Green, Color.white, 0, OnInviteFriend);
        }

        // ── Colonne droite : classement ──────────────────────────────────────────────
        private void BuildBoardHeader(Transform parent)
        {
            var row = Row(parent, 12);
            var rowLe = row.GetComponent<LayoutElement>();
            rowLe.preferredHeight = 44; rowLe.minHeight = 44; rowLe.flexibleHeight = 0;   // hauteur fixe : la liste prend le reste
            var title = Txt("T", row.transform, "Classement", 18, FontStyles.Bold, TextPri);
            title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

            // Filtre segmenté
            var seg = Img("Seg", row.transform, BgCard);
            seg.sprite = _r12; seg.type = Image.Type.Sliced;
            var sh = seg.gameObject.AddComponent<HorizontalLayoutGroup>();
            sh.padding = new RectOffset(4, 4, 4, 4); sh.spacing = 4;
            sh.childForceExpandWidth = false; sh.childForceExpandHeight = true;
            sh.childControlWidth = true; sh.childControlHeight = true;
            var segLe = seg.gameObject.AddComponent<LayoutElement>();
            segLe.preferredHeight = 44; segLe.flexibleHeight = 0;
            string[] names = { "Ma ligue", "Monde", "Amis" };
            for (int i = 0; i < names.Length; i++)
            {
                int idx = i;
                var tab = Img("Tab" + i, seg.transform, BgCard);
                tab.sprite = _r8; tab.type = Image.Type.Sliced;
                tab.gameObject.AddComponent<LayoutElement>().preferredWidth = 104;
                var b = tab.gameObject.AddComponent<Button>(); b.targetGraphic = tab;
                b.onClick.AddListener(() => { _boardFilter = idx; RefreshBoard(); });
                var l = Txt("L", tab.transform, names[i], 13, FontStyles.Bold, TextSec);
                l.alignment = TextAlignmentOptions.Center; Fill(l.rectTransform);
                _filterTabs.Add((tab, l));
            }
        }

        private void RefreshBoard()
        {
            for (int i = 0; i < _filterTabs.Count; i++)
            {
                bool on = i == _boardFilter;
                _filterTabs[i].bg.color = on ? (Color)(Color32)Blue : (Color)(Color32)BgCard;
                _filterTabs[i].lbl.color = on ? Color.white : (Color)(Color32)TextSec;
            }
            Clear(_boardContent);

            var world  = Leaderboard.BuildWorld();
            var player = Leaderboard.PlayerEntry(world);
            List<LeaderboardEntry> rows;
            bool rankInLeague = false;
            if (_boardFilter == 0)
            {
                int li = Leaderboard.LeagueIndex(player?.km ?? 0);
                rows = world.FindAll(e => Leaderboard.LeagueIndex(e.km) == li);
                rankInLeague = true;
            }
            else if (_boardFilter == 1) rows = Leaderboard.WorldTop(world, 50);
            else rows = Leaderboard.FriendsBoard();

            bool playerShown = false;
            for (int i = 0; i < rows.Count && i < 50; i++)
            {
                int rank = rankInLeague ? i + 1 : rows[i].worldRank;
                BoardRow(rank, rows[i]);
                if (rows[i].isPlayer) playerShown = true;
            }
            if (!playerShown && player != null)
            {
                Section(_boardContent, "TA POSITION");
                BoardRow(player.worldRank, player);
            }
        }

        private void BoardRow(int rank, LeaderboardEntry e)
        {
            Color32 medal = rank == 1 ? Gold : rank == 2 ? Silver : rank == 3 ? Bronze : new Color32(0, 0, 0, 0);
            bool podium = rank <= 3;

            var row = Img("Row", _boardContent, e.isPlayer ? new Color32(Blue.r, Blue.g, Blue.b, 50) : BgCard);
            row.sprite = _r12; row.type = Image.Type.Sliced;
            if (e.isPlayer) { var ol = row.gameObject.AddComponent<Outline>(); ol.effectColor = Blue; ol.effectDistance = new Vector2(1.5f, -1.5f); }
            var le = row.gameObject.AddComponent<LayoutElement>(); le.preferredHeight = 56; le.minHeight = 56;
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(12, 18, 6, 6); h.spacing = 14; h.childAlignment = TextAnchor.MiddleLeft;
            h.childForceExpandWidth = false; h.childForceExpandHeight = false;
            h.childControlWidth = true; h.childControlHeight = true;

            // Rang : médaille ronde pour le podium, sinon numéro
            var rk = Img("Rank", row.transform, podium ? new Color32(medal.r, medal.g, medal.b, 60) : new Color32(0, 0, 0, 0));
            rk.sprite = _circle; rk.type = Image.Type.Sliced;
            var rle = rk.gameObject.AddComponent<LayoutElement>(); rle.preferredWidth = 40; rle.preferredHeight = 40; rle.minWidth = 40;
            var rl = Txt("N", rk.transform, rank.ToString(), podium ? 16 : 14, FontStyles.Bold, podium ? medal : TextSec);
            rl.alignment = TextAlignmentOptions.Center; Fill(rl.rectTransform);

            Avatar(row.transform, e.name, e.isPlayer ? Blue : e.isFriend ? Green : new Color32(0x7A, 0x8F, 0xA6, 255));

            var col = Go("Col", row.transform);
            var cv = col.AddComponent<VerticalLayoutGroup>();
            cv.childForceExpandWidth = true; cv.childForceExpandHeight = false;
            cv.childControlWidth = true; cv.childControlHeight = true;
            col.AddComponent<LayoutElement>().flexibleWidth = 1;
            string tag = e.isPlayer ? "  <size=80%><color=#5FA8FF>toi</color></size>" : e.isFriend ? "  <size=80%><color=#3DC96E>ami</color></size>" : "";
            var nm = Txt("Name", col.transform, e.name + tag, 14, e.isPlayer ? FontStyles.Bold : FontStyles.Normal, TextPri);
            nm.textWrappingMode = TextWrappingModes.NoWrap; nm.overflowMode = TextOverflowModes.Ellipsis;
            Txt("Lvl", col.transform, $"Niveau {e.level}", 11, FontStyles.Normal, TextDim);

            var km = Txt("Km", row.transform, $"{e.km:N0} km", 15, FontStyles.Bold, podium ? medal : TextPri);
            km.alignment = TextAlignmentOptions.MidlineRight; km.textWrappingMode = TextWrappingModes.NoWrap;
        }

        // ── Données ──────────────────────────────────────────────────────────────────
        private static List<FriendEntry> LoadFriends()
        {
            var friends = FriendsData.LoadAll();
#if UNITY_EDITOR
            if (friends.Count == 0)   // aperçu en éditeur uniquement
            {
                friends.Add(new FriendEntry { uid = "test_001", companyName = "Dupont Transport", level = 7,  vehicleCount = 4 });
                friends.Add(new FriendEntry { uid = "test_002", companyName = "Martin Logistics", level = 12, vehicleCount = 9 });
                friends.Add(new FriendEntry { uid = "test_003", companyName = "Express du Sud",   level = 3,  vehicleCount = 1 });
            }
#endif
            return friends;
        }

        private void OnInviteFriend()
        {
            var gm = GameManager.Instance;
            string companyName = gm?.Save?.company?.companyName ?? "Mon Entreprise";
            NativeShare.ShareText($"{companyName} t'invite à rejoindre Transport Manager ! {FriendsData.GenerateInviteLink()}");
        }

        private void Close() => Destroy(gameObject);

        // ── Briques d'interface ──────────────────────────────────────────────────────
        private Transform Column(Transform parent, string name, float flex)
        {
            var col = Go(name, parent);
            col.AddComponent<LayoutElement>().flexibleWidth = flex;
            var v = col.AddComponent<VerticalLayoutGroup>();
            v.childForceExpandWidth = true; v.childForceExpandHeight = true;
            v.childControlWidth = true; v.childControlHeight = true;
            return ScrollList(col.transform, name + "List");
        }

        private Transform ScrollList(Transform parent, string name)
        {
            var sv = Go(name, parent);
            sv.AddComponent<LayoutElement>().flexibleHeight = 1;
            var hit = sv.AddComponent<Image>(); hit.color = new Color(0, 0, 0, 0);
            sv.AddComponent<RectMask2D>();
            var sr = sv.AddComponent<ScrollRect>();
            sr.horizontal = false; sr.scrollSensitivity = 36f;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.viewport = (RectTransform)sv.transform;
            var content = Go("Content", sv.transform);
            var cRt = (RectTransform)content.transform;
            cRt.anchorMin = new Vector2(0, 1); cRt.anchorMax = new Vector2(1, 1); cRt.pivot = new Vector2(0.5f, 1);
            cRt.offsetMin = Vector2.zero; cRt.offsetMax = Vector2.zero;
            var v = content.AddComponent<VerticalLayoutGroup>();
            v.spacing = 10; v.padding = new RectOffset(0, 0, 0, 12);
            v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            v.childControlWidth = true; v.childControlHeight = true;
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            sr.content = cRt;
            return content.transform;
        }

        private GameObject Card(Transform parent, Color32 color)
        {
            var img = Img("Card", parent, color);
            img.sprite = _r16; img.type = Image.Type.Sliced;
            var v = img.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(18, 18, 16, 16); v.spacing = 10;
            v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            v.childControlWidth = true; v.childControlHeight = true;
            return img.gameObject;
        }

        private GameObject Row(Transform parent, float spacing)
        {
            var go = Go("Row", parent);
            var h = go.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing; h.childAlignment = TextAnchor.MiddleLeft;
            h.childForceExpandWidth = false; h.childForceExpandHeight = false;
            h.childControlWidth = true; h.childControlHeight = true;
            go.AddComponent<LayoutElement>();
            return go;
        }

        private void Section(Transform parent, string text)
        {
            var t = Txt("Section", parent, text, 11, FontStyles.Bold, TextDim);
            t.characterSpacing = 2f;
            t.gameObject.AddComponent<LayoutElement>().preferredHeight = 28;
            t.alignment = TextAlignmentOptions.BottomLeft;
        }

        private TMP_Text Pill(Transform parent, string text, Color32 c)
        {
            var p = Img("Pill", parent, new Color32(c.r, c.g, c.b, 46));
            p.sprite = _r8; p.type = Image.Type.Sliced;
            var h = p.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(10, 10, 3, 3);
            h.childControlWidth = true; h.childControlHeight = true;
            h.childForceExpandWidth = false; h.childForceExpandHeight = false;
            var t = Txt("L", p.transform, text, 11, FontStyles.Bold, c);
            t.characterSpacing = 1.5f; t.textWrappingMode = TextWrappingModes.NoWrap;
            return t;
        }

        private void Avatar(Transform parent, string name, Color32 c)
        {
            var a = Img("Avatar", parent, new Color32(c.r, c.g, c.b, 50));
            a.sprite = _circle; a.type = Image.Type.Sliced;
            var le = a.gameObject.AddComponent<LayoutElement>(); le.preferredWidth = 40; le.preferredHeight = 40; le.minWidth = 40;
            string initial = string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1).ToUpperInvariant();
            var t = Txt("I", a.transform, initial, 16, FontStyles.Bold, c);
            t.alignment = TextAlignmentOptions.Center; Fill(t.rectTransform);
        }

        private void ProgressBar(Transform parent, float t, Color32 c)
        {
            var track = Img("Track", parent, new Color32(255, 255, 255, 22));
            track.sprite = _r8; track.type = Image.Type.Sliced;
            track.gameObject.AddComponent<LayoutElement>().preferredHeight = 8;
            var fill = Img("Fill", track.transform, c);
            fill.sprite = _r8; fill.type = Image.Type.Sliced;
            var rt = fill.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = new Vector2(Mathf.Max(0.02f, t), 1);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        private void MakeButton(Transform parent, string label, Color32 color, Color textColor, float width,
                            UnityEngine.Events.UnityAction onClick)
        {
            var b = Img("Btn", parent, color);
            b.sprite = _r12; b.type = Image.Type.Sliced;
            var btn = b.gameObject.AddComponent<UnityEngine.UI.Button>(); btn.targetGraphic = b;
            btn.onClick.AddListener(onClick);
            var le = b.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 44; le.minHeight = 44;
            if (width > 0) { le.preferredWidth = width; le.minWidth = width; }
            var t = Txt("L", b.transform, label, 14, FontStyles.Bold, textColor);
            t.alignment = TextAlignmentOptions.Center; Fill(t.rectTransform);
        }

        private static GameObject Go(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static Image Img(string name, Transform parent, Color32 c)
        {
            var img = Go(name, parent).AddComponent<Image>();
            img.color = c;
            return img;
        }

        private static TMP_Text Txt(string name, Transform parent, string text, float size, FontStyles style, Color color)
        {
            var t = Go(name, parent).AddComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.fontStyle = style; t.color = color;
            t.alignment = TextAlignmentOptions.MidlineLeft; t.raycastTarget = false;
            return t;
        }

        private static void Fill(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        private static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                var c = t.GetChild(i).gameObject;
                c.transform.SetParent(null, false);
                Destroy(c);
            }
        }

        private static Sprite MakeRounded(int r)
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int cx = -1, cy = -1;
                    if (x < r && y < r) { cx = r; cy = r; }
                    else if (x >= size - r && y < r) { cx = size - r; cy = r; }
                    else if (x < r && y >= size - r) { cx = r; cy = size - r; }
                    else if (x >= size - r && y >= size - r) { cx = size - r; cy = size - r; }
                    float a = cx < 0 ? 1f : Mathf.Clamp01(r - Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) + 0.5f);
                    px[y * size + x] = new Color(1, 1, 1, a);
                }
            tex.SetPixels(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                                 SpriteMeshType.FullRect, new Vector4(r, r, r, r));
        }
    }
}
