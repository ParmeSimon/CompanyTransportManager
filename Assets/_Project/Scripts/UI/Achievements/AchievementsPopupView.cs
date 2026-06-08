using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TransportManager.Audio;
using TransportManager.Core;
using TransportManager.Systems.Achievements;
using TransportManager.UI.Common;

namespace TransportManager.UI.Achievements
{
    /// <summary>
    /// Popup « Succès » (G1) : liste défilante de tous les succès, avec progression,
    /// état (verrouillé / débloqué / réclamé) et bouton de réclamation de récompense.
    /// </summary>
    public class AchievementsPopupView : MonoBehaviour
    {
        private static readonly Color32 BgOverlay = new Color32(0x00, 0x00, 0x00, 200);
        private static readonly Color32 BgPanel   = new Color32(0x2C, 0x30, 0x38, 255);
        private static readonly Color32 BgCard    = new Color32(0x34, 0x38, 0x42, 255);
        private static readonly Color32 BgInset   = new Color32(0x1A, 0x1D, 0x24, 255);
        private static readonly Color32 TextPri   = new Color32(0xEC, 0xEE, 0xF5, 255);
        private static readonly Color32 TextSec   = new Color32(0x7A, 0x8F, 0xA6, 255);
        private static readonly Color32 TextMuted = new Color32(0x5A, 0x65, 0x77, 255);
        private static readonly Color   Green     = new Color(0.30f, 0.86f, 0.50f);
        private static readonly Color   Gold      = new Color(0.97f, 0.82f, 0.36f);

        private const int TitleBarH = 56;

        private Sprite _r12, _r8, _bar;
        private AchievementSystem _sys;
        private RectTransform _content;

        public static void Show()
        {
            if (FindObjectOfType<AchievementsPopupView>() != null) return;
            new GameObject("AchievementsPopup", typeof(RectTransform)).AddComponent<AchievementsPopupView>();
        }

        private void Awake()
        {
            _r12 = MakeRounded(12); _r8 = MakeRounded(8); _bar = MakeRounded(3);
            _sys = ServiceLocator.Get<AchievementSystem>();
            if (_sys == null) { Destroy(gameObject); return; }
            BuildUI();
        }

        private void BuildUI()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 525;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            var overlay = MakeImg("Overlay", transform, BgOverlay);
            Fill(overlay.rectTransform);
            overlay.gameObject.AddComponent<Button>().onClick.AddListener(Close);

            var panel = MakeGO("Panel", transform);
            var pImg = panel.AddComponent<Image>(); pImg.sprite = _r12; pImg.type = Image.Type.Sliced; pImg.color = BgPanel;
            var pRt = panel.GetComponent<RectTransform>();
            pRt.anchorMin = new Vector2(0f, 0.08f); pRt.anchorMax = new Vector2(0.92f, 0.92f);
            pRt.offsetMin = new Vector2(132f, 0f); pRt.offsetMax = Vector2.zero;   // dégage la sidebar

            PopupHeader.Build(panel.transform, "Succès", Close, TitleBarH, _r8);
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
            vlg.padding = new RectOffset(16, 16, 16, 20); vlg.spacing = 10;
            vlg.childForceExpandWidth = true; vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false; vlg.childControlHeight = true;
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = _content;
        }

        private void Rebuild()
        {
            for (int i = _content.childCount - 1; i >= 0; i--) Destroy(_content.GetChild(i).gameObject);

            BuildSummary();
            foreach (var def in _sys.All) BuildCard(def);
        }

        // ── En-tête récap : compteur global + barre de progression ───────────────────
        private void BuildSummary()
        {
            var card = Card("Summary", BgInset);
            var top = HRow(card.transform);
            var t = Tmp(top, "DÉBLOQUÉS", 13, TextSec, true); t.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            var n = Tmp(top, $"{_sys.UnlockedCount} / {_sys.TotalCount}", 16, Gold, true);
            n.alignment = TextAlignmentOptions.MidlineRight; n.gameObject.AddComponent<LayoutElement>().preferredWidth = 110;

            float ratio = _sys.TotalCount > 0 ? (float)_sys.UnlockedCount / _sys.TotalCount : 0f;
            ProgressBar(card.transform, ratio, Gold);

            int claimable = _sys.ClaimableCount();
            if (claimable > 0)
                Tmp(card.transform, $"{claimable} récompense(s) à réclamer ci-dessous", 11.5f, Green, true);
        }

        // ── Carte d'un succès ────────────────────────────────────────────────────────
        private void BuildCard(AchievementDef def)
        {
            bool unlocked = _sys.IsUnlocked(def.id);
            bool claimed  = _sys.IsClaimed(def.id);
            bool toClaim  = unlocked && !claimed;

            var card = Card("Ach_" + def.id, BgCard);
            if (toClaim)
            {
                var ol = card.AddComponent<Outline>();
                ol.effectColor = new Color(Green.r, Green.g, Green.b, 0.8f);
                ol.effectDistance = new Vector2(1.5f, -1.5f);
            }

            // Ligne titre + état
            var top = HRow(card.transform);
            var title = Tmp(top, def.title, 14.5f, unlocked ? TextPri : (Color)TextSec, true);
            title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

            string reward = AchievementSystem.RewardLabel(def.rewardKind, def.rewardAmount);
            var rew = Tmp(top, reward, 13, unlocked ? Gold : (Color)TextMuted, true);
            rew.alignment = TextAlignmentOptions.MidlineRight;
            rew.gameObject.AddComponent<LayoutElement>().preferredWidth = 90;

            // Description
            Tmp(card.transform, def.description, 11.5f, TextSec, false);

            // Progression (barre + valeurs) ou état réclamé
            long cur = _sys.CurrentValue(def);
            float ratio = _sys.Progress01(def);
            ProgressBar(card.transform, ratio, unlocked ? Green : new Color(0.40f, 0.55f, 0.95f));

            var bottom = HRow(card.transform);
            string progressTxt = ShowNumeric(def.metric)
                ? $"{Mathf.Min(cur, def.target):N0} / {def.target:N0}"
                : (unlocked ? "Objectif atteint" : "Non atteint");
            var pl = Tmp(bottom, progressTxt, 11, TextMuted, false);
            pl.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

            if (claimed)
            {
                Tmp(bottom, "✓ Réclamé", 12, Green, true).alignment = TextAlignmentOptions.MidlineRight;
            }
            else if (toClaim)
            {
                var btn = MakeButton(bottom, "Récupérer", Green, 36);
                btn.gameObject.GetComponent<LayoutElement>().preferredWidth = 130;
                btn.onClick.AddListener(() =>
                {
                    if (_sys.Claim(def.id))
                    {
                        Sfx.Cash(); Haptics.Success();
                        HeaderView.NotifyAchievementsChanged();
                        Rebuild();
                    }
                });
            }
            else
            {
                Tmp(bottom, "🔒 Verrouillé", 11.5f, TextMuted, false).alignment = TextAlignmentOptions.MidlineRight;
            }
        }

        private static bool ShowNumeric(AchievementMetric m) =>
            m != AchievementMetric.ReputationTier && m != AchievementMetric.VehicleCategory;

        // ── Helpers UI ───────────────────────────────────────────────────────────────
        private void ProgressBar(Transform parent, float ratio, Color fill)
        {
            var track = MakeGO("Track", parent);
            var ti = track.AddComponent<Image>(); ti.sprite = _bar; ti.type = Image.Type.Sliced;
            ti.color = new Color(1f, 1f, 1f, 0.08f); ti.raycastTarget = false;
            track.AddComponent<LayoutElement>().preferredHeight = 6;

            var fillGo = MakeGO("Fill", track.transform);
            var fi = fillGo.AddComponent<Image>(); fi.sprite = _bar; fi.type = Image.Type.Sliced;
            fi.color = fill; fi.raycastTarget = false;
            var frt = fillGo.GetComponent<RectTransform>();
            frt.anchorMin = new Vector2(0f, 0f); frt.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1f);
            frt.offsetMin = Vector2.zero; frt.offsetMax = Vector2.zero;
        }

        private GameObject Card(string name, Color32 bg)
        {
            var c = MakeGO("Card_" + name, _content);
            var img = c.AddComponent<Image>(); img.sprite = _r12; img.type = Image.Type.Sliced; img.color = bg;
            var v = c.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(14, 14, 12, 12); v.spacing = 7;
            v.childForceExpandWidth = true; v.childControlWidth = true;
            v.childForceExpandHeight = false; v.childControlHeight = true;
            c.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return c;
        }

        private Transform HRow(Transform parent)
        {
            var go = MakeGO("Row", parent);
            var h = go.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 8; h.childAlignment = TextAnchor.MiddleLeft;
            h.childForceExpandWidth = false; h.childControlWidth = true;
            h.childForceExpandHeight = false; h.childControlHeight = true;
            go.AddComponent<LayoutElement>().minHeight = 22;
            return go.transform;
        }

        private Button MakeButton(Transform parent, string label, Color color, int height)
        {
            var go = MakeGO("Btn", parent);
            var img = go.AddComponent<Image>(); img.sprite = _r8; img.type = Image.Type.Sliced; img.color = color;
            var btn = go.AddComponent<Button>(); btn.targetGraphic = img;
            go.AddComponent<LayoutElement>().preferredHeight = height;
            var lbl = Tmp(go.transform, label, 13.5f, new Color32(0x10, 0x16, 0x12, 255), true);
            lbl.alignment = TextAlignmentOptions.Center; lbl.raycastTarget = false; Fill(lbl.rectTransform);
            return btn;
        }

        private void Close() => Destroy(gameObject);

        private static GameObject MakeGO(string n, Transform p) { var g = new GameObject(n, typeof(RectTransform)); g.transform.SetParent(p, false); return g; }
        private static Image MakeImg(string n, Transform p, Color32 c) { var i = MakeGO(n, p).AddComponent<Image>(); i.color = c; return i; }

        private static TMP_Text Tmp(Transform p, string t, float s, Color c, bool bold)
        {
            var tmp = MakeGO("T", p).AddComponent<TextMeshProUGUI>();
            tmp.text = t; tmp.fontSize = s; tmp.color = c; tmp.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            tmp.alignment = TextAlignmentOptions.MidlineLeft; tmp.raycastTarget = false; tmp.richText = true;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            return tmp;
        }

        private static void Fill(RectTransform rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero; }

        private static Sprite MakeRounded(int radius)
        {
            const int size = 64; int r = Mathf.Clamp(radius, 1, size / 2);
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                int cx = -1, cy = -1;
                if (x < r && y < r) { cx = r; cy = r; } else if (x >= size - r && y < r) { cx = size - r; cy = r; }
                else if (x < r && y >= size - r) { cx = r; cy = size - r; } else if (x >= size - r && y >= size - r) { cx = size - r; cy = size - r; }
                float a = 1f; if (cx >= 0) a = Mathf.Clamp01(r - Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) + 0.5f);
                px[y * size + x] = new Color(1, 1, 1, a);
            }
            tex.SetPixels(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
        }
    }
}
