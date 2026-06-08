using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TransportManager.Audio;
using TransportManager.Core;
using TransportManager.Save;
using TransportManager.Systems.Daily;
using TransportManager.Systems.Events;
using TransportManager.Systems.Social;
using TransportManager.UI.Common;

namespace TransportManager.UI.Daily
{
    /// Popup « Quotidien » : événement du jour + récompense de connexion + missions.
    public class DailyHubPopupView : MonoBehaviour
    {
        private static readonly Color32 BgOverlay = new Color32(0x00, 0x00, 0x00, 200);
        private static readonly Color32 BgPanel   = new Color32(0x2C, 0x30, 0x38, 255);
        private static readonly Color32 BgCard    = new Color32(0x34, 0x38, 0x42, 255);
        private static readonly Color32 BgInset   = new Color32(0x1A, 0x1D, 0x24, 255);
        private static readonly Color32 TextPri   = new Color32(0xEC, 0xEE, 0xF5, 255);
        private static readonly Color32 TextSec   = new Color32(0x7A, 0x8F, 0xA6, 255);
        private static readonly Color32 TextMuted = new Color32(0x5A, 0x65, 0x77, 255);
        private static readonly Color32 Green     = new Color32(0x3D, 0xC9, 0x6E, 255);
        private static readonly Color32 Gold      = new Color32(0xF2, 0xD9, 0x66, 255);
        private static readonly Color32 Blue      = new Color32(0x35, 0x8E, 0xF5, 255);

        private const int TitleBarH = 56;

        private Sprite _r12, _r8, _pill;
        private DailySystem _daily;
        private RectTransform _content;

        public static void Show()
        {
            if (FindObjectOfType<DailyHubPopupView>() != null) return;
            new GameObject("DailyHubPopup", typeof(RectTransform)).AddComponent<DailyHubPopupView>();
        }

        /// Auto-affichage au lancement s'il y a quelque chose à réclamer.
        public static void ShowIfPending()
        {
            var d = ServiceLocator.Get<DailySystem>();
            if (d == null) return;
            bool claimable = d.LoginPending || d.LeaguePending;
            if (!claimable)
                foreach (var m in d.Missions) if (!m.claimed && DailySystem.IsComplete(m)) { claimable = true; break; }
            if (claimable) Show();
        }

        private void Awake()
        {
            _r12  = MakeRounded(12);
            _r8   = MakeRounded(8);
            _pill = MakeRounded(32);
            _daily = ServiceLocator.Get<DailySystem>();
            BuildUI();
        }

        private void BuildUI()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 520;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight  = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            var overlay = MakeImg("Overlay", transform, BgOverlay);
            Fill(overlay.rectTransform);
            overlay.gameObject.AddComponent<Button>().onClick.AddListener(Close);

            var panel = MakeGO("Panel", transform);
            var pImg = panel.AddComponent<Image>();
            pImg.sprite = _r12; pImg.type = Image.Type.Sliced; pImg.color = BgPanel;
            var pRt = panel.GetComponent<RectTransform>();
            pRt.anchorMin = new Vector2(0f, 0.08f); pRt.anchorMax = new Vector2(0.92f, 0.92f);
            pRt.offsetMin = new Vector2(132f, 0f); pRt.offsetMax = Vector2.zero;   // dégage la sidebar

            PopupHeader.Build(panel.transform, "Quotidien", Close, TitleBarH, _r8);
            BuildScroll(panel.transform);
            Rebuild();
        }

        private void BuildScroll(Transform parent)
        {
            var scrollGo = MakeGO("Scroll", parent);
            var sRt = scrollGo.GetComponent<RectTransform>();
            sRt.anchorMin = Vector2.zero; sRt.anchorMax = Vector2.one;
            sRt.offsetMin = Vector2.zero; sRt.offsetMax = new Vector2(0, -(TitleBarH + 1));
            var scroll = scrollGo.AddComponent<ScrollRect>();
            scroll.horizontal = false; scroll.vertical = true; scroll.scrollSensitivity = 50;

            var vp = MakeGO("Viewport", scrollGo.transform);
            Fill(vp.GetComponent<RectTransform>());
            vp.AddComponent<RectMask2D>();
            scroll.viewport = vp.GetComponent<RectTransform>();

            var content = MakeGO("Content", vp.transform);
            _content = content.GetComponent<RectTransform>();
            _content.anchorMin = new Vector2(0, 1); _content.anchorMax = new Vector2(1, 1);
            _content.pivot = new Vector2(0.5f, 1f); _content.sizeDelta = Vector2.zero;
            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(16, 16, 16, 20); vlg.spacing = 12;
            vlg.childForceExpandWidth = true; vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false; vlg.childControlHeight = true;
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = _content;
        }

        private void Rebuild()
        {
            for (int i = _content.childCount - 1; i >= 0; i--) Destroy(_content.GetChild(i).gameObject);
            if (_daily != null && _daily.LeaguePending) BuildLeagueRewardCard();
            BuildEventBanner();
            SectionLabel("RÉCOMPENSE DE CONNEXION");
            BuildLoginCard();
            SectionLabel("MISSIONS DU JOUR");
            var list = _daily?.Missions;
            if (list != null) for (int i = 0; i < list.Count; i++) BuildMissionCard(i, list[i]);
        }

        // ── Récompense de ligue (fin de semaine) ─────────────────────────────────────
        private void BuildLeagueRewardCard()
        {
            int li  = _daily.LeaguePendingLeague;
            var col = (Color)Leaderboard.LeagueColor(li);

            var card = Card("League", new Color(col.r, col.g, col.b, 0.16f));
            var ol = card.AddComponent<Outline>();
            ol.effectColor = new Color(col.r, col.g, col.b, 0.7f);
            ol.effectDistance = new Vector2(1.5f, -1.5f);

            var top = Row(card.transform, 8, 22);
            var t = Label(top, $"RÉCOMPENSE DE LIGUE · {Leaderboard.LeagueName(li).ToUpper()}", 14, Leaderboard.LeagueColor(li), true, TextAlignmentOptions.MidlineLeft);
            t.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

            Label(card.transform, $"Tu as fini <b>#{_daily.LeaguePendingRank}</b> de ta ligue la semaine dernière.", 12.5f, TextSec, false, TextAlignmentOptions.MidlineLeft);

            var rewardRow = Row(card.transform, 8, 32);
            string reward = $"+{_daily.LeaguePendingDollars:N0} $" + (_daily.LeaguePendingGold > 0 ? $"    +{_daily.LeaguePendingGold} ◆" : "");
            var rl = Label(rewardRow, reward, 16, Gold, true, TextAlignmentOptions.MidlineLeft);
            rl.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            var btn = Button(rewardRow, "Récupérer", Green, 38);
            btn.gameObject.GetComponent<LayoutElement>().preferredWidth = 140;
            btn.onClick.AddListener(() => { if (_daily.ClaimLeague()) { Sfx.Cash(); Haptics.Success(); Rebuild(); } });
        }

        // ── Événement du jour ────────────────────────────────────────────────────────
        private void BuildEventBanner()
        {
            var ev = LiveEvents.Current;
            if (ev == null) return;
            var accent = (Color)ev.color;

            var card = Card("Event", new Color(accent.r, accent.g, accent.b, 0.16f));
            var ol = card.AddComponent<Outline>();
            ol.effectColor = new Color(accent.r, accent.g, accent.b, 0.7f);
            ol.effectDistance = new Vector2(1.5f, -1.5f);

            var top = Row(card.transform, 8, 22);
            var t = Label(top, $"ÉVÉNEMENT · {ev.title.ToUpper()}", 14, ev.color, true, TextAlignmentOptions.MidlineLeft);
            t.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            var pct = Label(top, $"+{ev.BonusPercent}%", 17, ev.color, true, TextAlignmentOptions.MidlineRight);
            pct.gameObject.AddComponent<LayoutElement>().preferredWidth = 80;

            Label(card.transform, ev.description, 12.5f, TextSec, false, TextAlignmentOptions.MidlineLeft);
        }

        // ── Connexion (streak) ───────────────────────────────────────────────────────
        private void BuildLoginCard()
        {
            var card = Card("Login", BgCard);
            int cur = _daily?.PendingCycleDay() ?? 1;
            bool pending = _daily != null && _daily.LoginPending;

            var strip = Row(card.transform, 6, 66);
            var sLe = strip.GetComponent<LayoutElement>();
            sLe.preferredHeight = 66;
            var sHlg = strip.GetComponent<HorizontalLayoutGroup>();
            sHlg.childForceExpandWidth = true; sHlg.childForceExpandHeight = true;
            for (int day = 1; day <= 7; day++)
            {
                bool isToday = pending && day == cur;
                bool past    = pending && day < cur;
                var (kind, amount) = DailySystem.LoginRewardForDay(day);
                BuildDayCell(strip.transform, day, kind, amount, isToday, past);
            }

            var btn = Button(card.transform,
                pending ? "Récupérer" : "Revenez demain",
                pending ? Green : BgInset, 46);
            SetInteractable(btn, pending, pending ? Green : BgInset);
            if (pending)
                btn.onClick.AddListener(() => { if (_daily != null && _daily.ClaimLogin()) { Sfx.Cash(); Haptics.Success(); Rebuild(); } });
        }

        private void BuildDayCell(Transform parent, int day, string kind, int amount, bool isToday, bool past)
        {
            var cell = MakeGO("Day" + day, parent);
            var img = cell.AddComponent<Image>();
            img.sprite = _r8; img.type = Image.Type.Sliced;
            img.color = isToday ? new Color((float)Green.r / 255, (float)Green.g / 255, (float)Green.b / 255, 0.22f)
                       : past   ? new Color(1, 1, 1, 0.04f) : (Color)(Color32)BgInset;
            if (isToday) { var ol = cell.AddComponent<Outline>(); ol.effectColor = Green; ol.effectDistance = new Vector2(1.5f, -1.5f); }

            var v = cell.AddComponent<VerticalLayoutGroup>();
            v.childAlignment = TextAnchor.MiddleCenter; v.spacing = 1; v.padding = new RectOffset(2, 2, 6, 6);
            v.childForceExpandWidth = true; v.childControlWidth = true;
            v.childForceExpandHeight = false; v.childControlHeight = true;

            Label(cell.transform, $"J{day}", 11, past ? TextMuted : TextSec, false, TextAlignmentOptions.Center);
            Label(cell.transform, ShortReward(kind, amount), 13, day == 7 ? Gold : TextPri, true, TextAlignmentOptions.Center);
            if (past) Label(cell.transform, "✓", 11, Green, true, TextAlignmentOptions.Center);
        }

        // ── Missions ────────────────────────────────────────────────────────────────
        private void BuildMissionCard(int index, MissionState m)
        {
            var card = Card("M" + index, BgCard);

            var top = Row(card.transform, 8, 22);
            var lbl = Label(top, DailySystem.MissionLabel(m), 15, TextPri, true, TextAlignmentOptions.MidlineLeft);
            lbl.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            var rew = Label(top, DailySystem.RewardLabel(m.rewardKind, m.rewardAmount), 14,
                            m.rewardKind == "skill" ? Blue : m.rewardKind == "ingots" ? Gold : Green, true, TextAlignmentOptions.MidlineRight);
            rew.gameObject.AddComponent<LayoutElement>().preferredWidth = 96;

            // Barre de progression
            float ratio = m.target > 0 ? Mathf.Clamp01((float)m.progress / m.target) : 1f;
            var bar = MakeGO("Bar", card.transform);
            bar.AddComponent<LayoutElement>().preferredHeight = 10;
            var bg = bar.AddComponent<Image>(); bg.sprite = _pill; bg.type = Image.Type.Sliced; bg.color = BgInset;
            var fillGo = MakeGO("Fill", bar.transform);
            var fill = fillGo.AddComponent<Image>(); fill.sprite = _pill; fill.type = Image.Type.Sliced;
            fill.color = m.claimed ? Green : Blue;
            var fr = fillGo.GetComponent<RectTransform>();
            fr.anchorMin = Vector2.zero; fr.anchorMax = new Vector2(ratio, 1f);
            fr.offsetMin = Vector2.zero; fr.offsetMax = Vector2.zero;

            var bottom = Row(card.transform, 8, 36);
            var prog = Label(bottom, $"{Mathf.Min(m.progress, m.target):N0} / {m.target:N0}", 12, TextSec, false, TextAlignmentOptions.MidlineLeft);
            prog.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

            bool complete = DailySystem.IsComplete(m);
            string txt = m.claimed ? "Réclamé ✓" : complete ? "Récupérer" : "En cours";
            Color32 col = m.claimed ? BgInset : complete ? Green : BgInset;
            var btn = Button(bottom, txt, col, 36);
            btn.gameObject.GetComponent<LayoutElement>().preferredWidth = 150;
            bool can = complete && !m.claimed;
            SetInteractable(btn, can, col);
            if (can)
                btn.onClick.AddListener(() => { if (_daily != null && _daily.ClaimMission(index)) { Sfx.Cash(); Haptics.Success(); Rebuild(); } });
        }

        // ── Helpers UI ────────────────────────────────────────────────────────────────
        private static string ShortReward(string kind, int amount) => kind switch
        {
            "dollars" => amount >= 1000 ? $"{amount / 1000f:0.#}k$" : $"{amount}$",
            "ingots"  => $"{amount}◆",
            "skill"   => $"{amount}pt",
            _         => amount.ToString(),
        };

        private void SectionLabel(string text)
        {
            var t = Label(_content, text, 11, TextMuted, true, TextAlignmentOptions.MidlineLeft);
            t.characterSpacing = 2.5f;
            t.gameObject.AddComponent<LayoutElement>().preferredHeight = 16;
        }

        private GameObject Card(string name, Color bg)
        {
            var c = MakeGO("Card_" + name, _content);
            var img = c.AddComponent<Image>();
            img.sprite = _r12; img.type = Image.Type.Sliced; img.color = bg;
            var vlg = c.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(14, 14, 12, 12); vlg.spacing = 8;
            vlg.childForceExpandWidth = true; vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false; vlg.childControlHeight = true;
            c.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return c;
        }

        private Transform Row(Transform parent, int spacing, int minHeight)
        {
            var go = MakeGO("Row", parent);
            var h = go.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing; h.childAlignment = TextAnchor.MiddleLeft;
            h.childForceExpandWidth = false; h.childControlWidth = true;
            h.childForceExpandHeight = false; h.childControlHeight = true;
            go.AddComponent<LayoutElement>().minHeight = minHeight;
            return go.transform;
        }

        private Button Button(Transform parent, string label, Color32 color, float height)
        {
            var go = MakeGO("Btn", parent);
            var img = go.AddComponent<Image>();
            img.sprite = _r8; img.type = Image.Type.Sliced; img.color = color;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            go.AddComponent<LayoutElement>().preferredHeight = height;
            var lbl = Label(go.transform, label, 14, TextPri, true, TextAlignmentOptions.Center);
            Fill(lbl.rectTransform);
            return btn;
        }

        private static void SetInteractable(Button btn, bool on, Color32 baseCol)
        {
            btn.interactable = on;
            if (!on && btn.targetGraphic is Image img)
                img.color = new Color((float)baseCol.r / 255, (float)baseCol.g / 255, (float)baseCol.b / 255, 0.5f);
        }

        private void Close() => Destroy(gameObject);

        private static GameObject MakeGO(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }
        private static Image MakeImg(string name, Transform parent, Color32 color)
        {
            var img = MakeGO(name, parent).AddComponent<Image>(); img.color = color; return img;
        }
        private static TMP_Text Label(Transform parent, string text, float size, Color32 color, bool bold, TextAlignmentOptions align)
        {
            var tmp = MakeGO("T", parent).AddComponent<TextMeshProUGUI>();
            tmp.text = text; tmp.fontSize = size; tmp.color = color;
            tmp.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            tmp.alignment = align; tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            return tmp;
        }
        private static void Fill(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        private static Sprite MakeRounded(int radius)
        {
            const int size = 64; int r = Mathf.Clamp(radius, 1, size / 2);
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
                    float a = 1f;
                    if (cx >= 0) a = Mathf.Clamp01(r - Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) + 0.5f);
                    px[y * size + x] = new Color(1, 1, 1, a);
                }
            tex.SetPixels(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
        }
    }
}
