using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TransportManager.Core;
using TransportManager.Save;
using TransportManager.Systems.Notifications;
using TransportManager.UI.Common;

namespace TransportManager.UI.Notifications
{
    /// <summary>
    /// Journal des notifications : regroupé par jour (Aujourd'hui / Hier / date), chaque entrée
    /// avec heure, pastille colorée selon le type, titre et texte complet. L'ouverture marque tout lu.
    /// </summary>
    public class NotificationsPopupView : MonoBehaviour
    {
        private const int TitleBarH = 56;

        private static readonly Color32 BgOverlay = new Color32(0x0A, 0x0C, 0x10, 170);
        private static readonly Color32 BgPanel   = new Color32(0x22, 0x26, 0x2E, 255);
        private static readonly Color32 BgCard    = new Color32(0x2C, 0x30, 0x38, 255);
        private static readonly Color32 TextPri   = new Color32(0xEC, 0xEE, 0xF5, 255);
        private static readonly Color32 TextSec   = new Color32(0x9A, 0xA6, 0xB8, 255);
        private static readonly Color32 TextDim   = new Color32(0x66, 0x70, 0x82, 255);

        private Sprite _r12, _r8;
        private RectTransform _content;
        private NotificationSystem _sys;

        public static void Show()
        {
            if (FindFirstObjectByType<NotificationsPopupView>() != null) return;
            new GameObject("NotificationsPopup", typeof(RectTransform)).AddComponent<NotificationsPopupView>();
        }

        private void Awake()
        {
            _sys = ServiceLocator.Get<NotificationSystem>();
            if (_sys == null) { Destroy(gameObject); return; }
            _r12 = MakeRounded(12); _r8 = MakeRounded(8);

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 530;
            UiScale.Configure(gameObject.AddComponent<CanvasScaler>());
            gameObject.AddComponent<GraphicRaycaster>();

            var overlay = MakeImg("Overlay", transform, BgOverlay);
            Fill(overlay.rectTransform);
            overlay.gameObject.AddComponent<Button>().onClick.AddListener(Close);

            // Panneau à droite, pleine hauteur (comme un centre de notifications).
            Vector4 ins = SafeAreaUtil.Insets(canvas);
            var panel = MakeImg("Panel", transform, BgPanel);
            var pRt = panel.rectTransform;
            pRt.anchorMin = new Vector2(1f, 0f); pRt.anchorMax = new Vector2(1f, 1f);
            pRt.pivot = new Vector2(1f, 0.5f);
            pRt.offsetMin = new Vector2(-(520f + ins.y), 0f); pRt.offsetMax = Vector2.zero;

            PopupHeader.Build(panel.transform, "Notifications", Close, TitleBarH, _r8);
            BuildScroll(panel.transform, ins);
            Rebuild();

            _sys.MarkAllRead();
        }

        private void BuildScroll(Transform parent, Vector4 ins)
        {
            var scrollGo = new GameObject("Scroll", typeof(RectTransform));
            scrollGo.transform.SetParent(parent, false);
            var sRt = (RectTransform)scrollGo.transform;
            sRt.anchorMin = Vector2.zero; sRt.anchorMax = Vector2.one;
            sRt.offsetMin = new Vector2(0, ins.w); sRt.offsetMax = new Vector2(-ins.y, -(TitleBarH + 1));
            var hit = scrollGo.AddComponent<Image>(); hit.color = new Color(0, 0, 0, 0);
            scrollGo.AddComponent<RectMask2D>();
            var scroll = scrollGo.AddComponent<ScrollRect>();
            scroll.horizontal = false; scroll.scrollSensitivity = 36f;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.viewport = sRt;

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(scrollGo.transform, false);
            _content = (RectTransform)content.transform;
            _content.anchorMin = new Vector2(0, 1); _content.anchorMax = new Vector2(1, 1);
            _content.pivot = new Vector2(0.5f, 1f);
            _content.offsetMin = Vector2.zero; _content.offsetMax = Vector2.zero;
            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(14, 14, 10, 18);
            vlg.spacing = 8;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = _content;
        }

        private void Rebuild()
        {
            for (int i = _content.childCount - 1; i >= 0; i--) Destroy(_content.GetChild(i).gameObject);

            var list = _sys.Entries;
            if (list.Count == 0)
            {
                var empty = MakeTMP("Empty", _content, "Aucune notification pour l'instant.\nLes livraisons, accidents et départs de conducteurs apparaîtront ici.",
                                    13, FontStyles.Normal, TextSec);
                empty.alignment = TextAlignmentOptions.Center;
                empty.textWrappingMode = TextWrappingModes.Normal;
                empty.gameObject.AddComponent<LayoutElement>().minHeight = 120;
                return;
            }

            string lastDay = null;
            foreach (var e in list)
            {
                var local = new DateTime(e.timeTicks, DateTimeKind.Utc).ToLocalTime();
                string day = DayLabel(local);
                if (day != lastDay)
                {
                    var h = MakeTMP("Day", _content, day.ToUpperInvariant(), 10, FontStyles.Bold, TextDim);
                    h.characterSpacing = 2f;
                    h.gameObject.AddComponent<LayoutElement>().preferredHeight = 26;
                    lastDay = day;
                }
                BuildRow(e, local);
            }
        }

        private void BuildRow(NotificationEntry e, DateTime local)
        {
            Color32 col = KindColor(e.kind);

            var row = MakeImg("Row", _content, BgCard);
            row.sprite = _r12; row.type = Image.Type.Sliced; row.raycastTarget = false;
            var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(12, 14, 12, 12);
            hlg.spacing = 12;
            hlg.childAlignment = TextAnchor.UpperLeft;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
            hlg.childControlWidth = true; hlg.childControlHeight = true;

            // Pastille colorée avec initiale du type
            var dot = MakeImg("Kind", row.transform, new Color32(col.r, col.g, col.b, 46));
            dot.sprite = _r8; dot.type = Image.Type.Sliced;
            var dLe = dot.gameObject.AddComponent<LayoutElement>();
            dLe.preferredWidth = 36; dLe.preferredHeight = 36; dLe.minWidth = 36;
            var glyph = MakeTMP("G", dot.transform, KindGlyph(e.kind), 15, FontStyles.Bold, col);
            glyph.alignment = TextAlignmentOptions.Center;
            Fill(glyph.rectTransform);

            var col2 = new GameObject("Text", typeof(RectTransform));
            col2.transform.SetParent(row.transform, false);
            var vlg = col2.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 4;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            col2.AddComponent<LayoutElement>().flexibleWidth = 1;

            var top = new GameObject("Top", typeof(RectTransform));
            top.transform.SetParent(col2.transform, false);
            var thlg = top.AddComponent<HorizontalLayoutGroup>();
            thlg.spacing = 8; thlg.childAlignment = TextAnchor.MiddleLeft;
            thlg.childForceExpandWidth = false; thlg.childForceExpandHeight = false;
            thlg.childControlWidth = true; thlg.childControlHeight = true;

            var title = MakeTMP("Title", top.transform, e.title, 14, FontStyles.Bold, TextPri);
            title.textWrappingMode = TextWrappingModes.Normal;
            title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            var time = MakeTMP("Time", top.transform, local.ToString("HH:mm"), 11, FontStyles.Bold, TextDim);
            time.alignment = TextAlignmentOptions.TopRight;
            time.textWrappingMode = TextWrappingModes.NoWrap;

            var body = MakeTMP("Body", col2.transform, e.body, 12, FontStyles.Normal, TextSec);
            body.textWrappingMode = TextWrappingModes.Normal;
        }

        private static string DayLabel(DateTime local)
        {
            var today = DateTime.Now.Date;
            if (local.Date == today) return "Aujourd'hui";
            if (local.Date == today.AddDays(-1)) return "Hier";
            return local.ToString("dddd d MMMM", new System.Globalization.CultureInfo("fr-FR"));
        }

        private static Color32 KindColor(string kind)
        {
            switch (kind)
            {
                case NotificationKind.Delivered:   return new Color32(0x3D, 0xC9, 0x6E, 255);
                case NotificationKind.Late:        return new Color32(0xF7, 0xA6, 0x24, 255);
                case NotificationKind.Accident:    return new Color32(0xF7, 0xA6, 0x24, 255);
                case NotificationKind.Fatal:       return new Color32(0xFF, 0x5A, 0x5A, 255);
                case NotificationKind.Resigned:    return new Color32(0xFF, 0x6B, 0x6B, 255);
                case NotificationKind.LevelUp:     return new Color32(0x5A, 0x8E, 0xF5, 255);
                case NotificationKind.Maintenance: return new Color32(0xF7, 0xA6, 0x24, 255);
                default:                           return new Color32(0x9A, 0xA6, 0xB8, 255);
            }
        }

        // Caractères présents dans la police (pas d'emoji).
        private static string KindGlyph(string kind)
        {
            switch (kind)
            {
                case NotificationKind.Delivered:   return "$";
                case NotificationKind.Late:        return "!";
                case NotificationKind.Accident:    return "!";
                case NotificationKind.Fatal:       return "×";
                case NotificationKind.Resigned:    return "–";
                case NotificationKind.LevelUp:     return "★";
                case NotificationKind.Maintenance: return "!";
                case NotificationKind.Fuel:        return "L";
                default:                           return "•";
            }
        }

        private void Close() => Destroy(gameObject);

        // ── Helpers ──────────────────────────────────────────────────────────
        private static Image MakeImg(string name, Transform parent, Color32 c)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>(); img.color = c;
            return img;
        }

        private static TMP_Text MakeTMP(string name, Transform parent, string text, float size, FontStyles style, Color32 color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.fontStyle = style; t.color = color;
            t.alignment = TextAlignmentOptions.TopLeft; t.raycastTarget = false;
            return t;
        }

        private static void Fill(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
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
