using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TransportManager.Core;
using TransportManager.Systems.Fuel;
using TransportManager.UI.Analytics;
using TransportManager.UI.Common;

namespace TransportManager.UI.Tabs
{
    /// Popup de commande de carburant : prix courant + fluctuation (historique/prévision
    /// selon les skills) + curseur pour choisir la quantité de litres.
    public class FuelOrderPopupView : MonoBehaviour
    {
        private static readonly Color32 BgOverlay = new Color32(0x00, 0x00, 0x00, 200);
        private static readonly Color32 BgPanel   = new Color32(0x2C, 0x30, 0x38, 255);
        private static readonly Color32 BgCard    = new Color32(0x34, 0x38, 0x42, 255);
        private static readonly Color32 BgInset   = new Color32(0x1A, 0x1D, 0x24, 255);
        private static readonly Color32 TextPri   = new Color32(0xEC, 0xEE, 0xF5, 255);
        private static readonly Color32 TextSec   = new Color32(0x7A, 0x8F, 0xA6, 255);
        private static readonly Color32 TextMuted = new Color32(0x5A, 0x65, 0x77, 255);
        private static readonly Color32 Accent    = new Color32(0x3D, 0xC9, 0x6E, 255);
        private static readonly Color   ChartLine = new Color(0.30f, 0.86f, 0.50f, 1f);
        private static readonly Color   ChartFut  = new Color(0.55f, 0.70f, 0.95f, 0.9f);

        private const int TitleBarH = 56;

        private Sprite _r12, _r8, _bar, _circle;
        private FuelSystem _fuel;
        private Slider _slider;
        private TMP_Text _costLabel, _litersLabel;

        public static void Show()
        {
            if (FindObjectOfType<FuelOrderPopupView>() != null) return;
            new GameObject("FuelOrderPopup", typeof(RectTransform)).AddComponent<FuelOrderPopupView>();
        }

        private void Awake()
        {
            _r12 = MakeRounded(12); _r8 = MakeRounded(8); _bar = MakeRounded(3); _circle = MakeRounded(32);
            _fuel = ServiceLocator.Get<FuelSystem>();
            if (_fuel == null) { Destroy(gameObject); return; }
            BuildUI();
        }

        private void BuildUI()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 540;
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
            pRt.anchorMin = new Vector2(0f, 0.18f); pRt.anchorMax = new Vector2(0.93f, 0.82f);
            pRt.offsetMin = new Vector2(132f, 0f); pRt.offsetMax = Vector2.zero;   // dégage la sidebar

            PopupHeader.Build(panel.transform, "Ravitaillement", Close, TitleBarH, _r8);

            var body = MakeGO("Body", panel.transform);
            var bRt = body.GetComponent<RectTransform>();
            bRt.anchorMin = Vector2.zero; bRt.anchorMax = Vector2.one;
            bRt.offsetMin = new Vector2(16, 16); bRt.offsetMax = new Vector2(-16, -(TitleBarH + 6));
            var vlg = body.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 12; vlg.childForceExpandWidth = true; vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false; vlg.childControlHeight = true;

            BuildPriceHeader(body.transform);
            BuildChart(body.transform);
            BuildSliderRow(body.transform);
            BuildConfirm(body.transform);
        }

        // ── Prix courant + tendance ──────────────────────────────────────────────────
        private void BuildPriceHeader(Transform parent)
        {
            var card = Card(parent, BgCard, 50);
            var h = HRow(card.transform);
            float price = _fuel.CurrentDollarsPerLiter;
            int trend = _fuel.FuelPriceTrend;
            string arrow = trend > 0 ? "<color=#ff6b6b>▲ en hausse</color>"
                         : trend < 0 ? "<color=#3DC96E>▼ en baisse</color>" : "<color=#7A8FA6>▬ stable</color>";
            var big = Tmp(h, $"{price:0.00} $/L", 24, TextPri, true); big.alignment = TextAlignmentOptions.MidlineLeft;
            big.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            var tr = Tmp(h, arrow, 14, TextSec, true); tr.alignment = TextAlignmentOptions.MidlineRight;
            tr.gameObject.AddComponent<LayoutElement>().preferredWidth = 150;
        }

        // ── Graphe de fluctuation ────────────────────────────────────────────────────
        private void BuildChart(Transform parent)
        {
            // Cours réels → on n'affiche que le PASSÉ (pas de prévision). Profondeur = skills.
            int histDays = _fuel.HistoryDaysVisible;
            bool hist = histDays > 0;
            float pastHours = hist ? histDays * 24f : 6f;
            float total = pastHours;   // « maintenant » est le point le plus à droite

            // Conteneur à positionnement absolu (PAS de VerticalLayoutGroup).
            var card = MakeGO("ChartCard", parent);
            var cImg = card.AddComponent<Image>(); cImg.sprite = _r12; cImg.type = Image.Type.Sliced; cImg.color = BgInset;
            card.AddComponent<LayoutElement>().preferredHeight = 168;

            var chartHost = MakeGO("ChartHost", card.transform);
            var chRt = chartHost.GetComponent<RectTransform>();
            chRt.anchorMin = Vector2.zero; chRt.anchorMax = Vector2.one;
            chRt.offsetMin = new Vector2(12, 28); chRt.offsetMax = new Vector2(-12, -28);

            var chart = chartHost.AddComponent<LineChartUI>();
            const int N = 40;
            var pts = new float[N];
            for (int i = 0; i < N; i++)
            {
                float off = -pastHours + total * (i / (float)(N - 1));
                pts[i] = _fuel.DollarsPerLiterAt(off);
            }
            chart.SetData(pts, ChartLine);

            // Repère « maintenant »
            float nowFrac = total > 0 ? pastHours / total : 0f;
            var now = MakeGO("Now", chartHost.transform);
            var nImg = now.AddComponent<Image>(); nImg.color = new Color(1, 1, 1, 0.25f); nImg.raycastTarget = false;
            var nRt = now.GetComponent<RectTransform>();
            nRt.anchorMin = new Vector2(nowFrac, 0); nRt.anchorMax = new Vector2(nowFrac, 1);
            nRt.pivot = new Vector2(0.5f, 0.5f); nRt.sizeDelta = new Vector2(2, 0); nRt.anchoredPosition = Vector2.zero;

            // Min/max + légende
            var top = Tmp(card.transform, $"{chart.VizMax:0.00} $", 10, TextMuted, false);
            var tRt = top.rectTransform; tRt.anchorMin = new Vector2(0, 1); tRt.anchorMax = new Vector2(0, 1);
            tRt.pivot = new Vector2(0, 1); tRt.anchoredPosition = new Vector2(12, -6); tRt.sizeDelta = new Vector2(80, 14);
            var bot = Tmp(card.transform, $"{chart.VizMin:0.00} $", 10, TextMuted, false);
            var bRt2 = bot.rectTransform; bRt2.anchorMin = new Vector2(0, 0); bRt2.anchorMax = new Vector2(0, 0);
            bRt2.pivot = new Vector2(0, 0); bRt2.anchoredPosition = new Vector2(12, 6); bRt2.sizeDelta = new Vector2(80, 14);

            string source = _fuel.UsesRealMarket ? "cours réel" : "simulé";
            string legend = !hist
                ? "Historique verrouillé — skill « Analyse de marché »"
                : $"Historique {histDays} j  ·  {source}";
            var leg = Tmp(card.transform, legend, 10.5f, TextMuted, false);
            leg.alignment = TextAlignmentOptions.Center;
            var lRt = leg.rectTransform; lRt.anchorMin = new Vector2(0, 0); lRt.anchorMax = new Vector2(1, 0);
            lRt.pivot = new Vector2(0.5f, 0); lRt.anchoredPosition = new Vector2(0, 6); lRt.sizeDelta = new Vector2(0, 14);
        }

        // ── Curseur litres ───────────────────────────────────────────────────────────
        private void BuildSliderRow(Transform parent)
        {
            float remaining = _fuel.RemainingCapacity;

            var card = Card(parent, BgCard, 0);
            var head = HRow(card.transform);
            Tmp(head, "Quantité à commander", 13, TextSec, true).gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            _litersLabel = Tmp(head, "", 14, TextPri, true); _litersLabel.alignment = TextAlignmentOptions.MidlineRight;
            _litersLabel.gameObject.AddComponent<LayoutElement>().preferredWidth = 110;

            _slider = BuildSlider(card.transform, Mathf.Max(1f, remaining), remaining);

            var costRow = HRow(card.transform);
            Tmp(costRow, "Coût total", 13, TextSec, false).gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            _costLabel = Tmp(costRow, "", 16, Accent, true); _costLabel.alignment = TextAlignmentOptions.MidlineRight;
            _costLabel.gameObject.AddComponent<LayoutElement>().preferredWidth = 130;

            _slider.onValueChanged.AddListener(_ => RefreshCost());
            RefreshCost();
        }

        private void RefreshCost()
        {
            float liters = _slider != null ? _slider.value : 0f;
            int cost = Mathf.CeilToInt(liters * _fuel.CurrentDollarsPerLiter);
            if (_litersLabel) _litersLabel.text = $"{liters:0} L";
            if (_costLabel)   _costLabel.text = $"$ {cost:N0}";
        }

        private void BuildConfirm(Transform parent)
        {
            var go = MakeGO("Commander", parent);
            var img = go.AddComponent<Image>(); img.sprite = _r8; img.type = Image.Type.Sliced; img.color = new Color(0.20f, 0.65f, 0.45f);
            var btn = go.AddComponent<Button>(); btn.targetGraphic = img;
            go.AddComponent<LayoutElement>().preferredHeight = 52;
            var lbl = Tmp(go.transform, "Commander", 16, TextPri, true); lbl.alignment = TextAlignmentOptions.Center; Fill(lbl.rectTransform);
            btn.onClick.AddListener(() =>
            {
                float liters = _slider != null ? _slider.value : 0f;
                if (liters > 0f) _fuel.TryStartTruckRefill(liters);
                Close();
            });
        }

        // ── Helpers ──────────────────────────────────────────────────────────────────
        private Slider BuildSlider(Transform parent, float max, float value)
        {
            var go = MakeGO("Slider", parent);
            go.AddComponent<LayoutElement>().preferredHeight = 26;
            var slider = go.AddComponent<Slider>();
            slider.minValue = 0f; slider.maxValue = max; slider.value = value;

            var bg = MakeGO("Bg", go.transform);
            var bgi = bg.AddComponent<Image>(); bgi.sprite = _bar; bgi.type = Image.Type.Sliced; bgi.color = BgInset;
            var bgrt = bg.GetComponent<RectTransform>();
            bgrt.anchorMin = new Vector2(0, 0.5f); bgrt.anchorMax = new Vector2(1, 0.5f); bgrt.pivot = new Vector2(0.5f, 0.5f); bgrt.sizeDelta = new Vector2(0, 6);
            slider.targetGraphic = bgi;

            var fa = MakeGO("FillArea", go.transform);
            var fart = fa.GetComponent<RectTransform>();
            fart.anchorMin = new Vector2(0, 0.5f); fart.anchorMax = new Vector2(1, 0.5f); fart.pivot = new Vector2(0.5f, 0.5f); fart.sizeDelta = new Vector2(-16, 6);
            var fill = MakeGO("Fill", fa.transform);
            var fimg = fill.AddComponent<Image>(); fimg.sprite = _bar; fimg.type = Image.Type.Sliced; fimg.color = Accent;
            var frt = fill.GetComponent<RectTransform>();
            frt.anchorMin = Vector2.zero; frt.anchorMax = new Vector2(max > 0 ? value / max : 0f, 1f); frt.offsetMin = Vector2.zero; frt.offsetMax = Vector2.zero;
            slider.fillRect = frt;

            var ha = MakeGO("HandleArea", go.transform);
            var hart = ha.GetComponent<RectTransform>();
            hart.anchorMin = new Vector2(0, 0.5f); hart.anchorMax = new Vector2(1, 0.5f); hart.pivot = new Vector2(0.5f, 0.5f); hart.sizeDelta = new Vector2(-18, 18);
            var handle = MakeGO("Handle", ha.transform);
            var himg = handle.AddComponent<Image>(); himg.sprite = _circle; himg.type = Image.Type.Simple; himg.color = Accent;
            var hrt = handle.GetComponent<RectTransform>(); hrt.pivot = new Vector2(0.5f, 0.5f); hrt.sizeDelta = new Vector2(18, 0);
            slider.handleRect = hrt;
            return slider;
        }

        private GameObject Card(Transform parent, Color32 bg, int minH)
        {
            var c = MakeGO("Card", parent);
            var img = c.AddComponent<Image>(); img.sprite = _r12; img.type = Image.Type.Sliced; img.color = bg;
            var v = c.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(14, 14, 12, 12); v.spacing = 8;
            v.childForceExpandWidth = true; v.childControlWidth = true; v.childForceExpandHeight = false; v.childControlHeight = true;
            if (minH > 0) c.AddComponent<LayoutElement>().preferredHeight = minH;
            else c.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return c;
        }

        private Transform HRow(Transform parent)
        {
            var go = MakeGO("Row", parent);
            var h = go.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 8; h.childAlignment = TextAnchor.MiddleLeft;
            h.childForceExpandWidth = false; h.childControlWidth = true; h.childForceExpandHeight = false; h.childControlHeight = true;
            go.AddComponent<LayoutElement>().minHeight = 24;
            return go.transform;
        }

        private void Close() => Destroy(gameObject);

        private static GameObject MakeGO(string n, Transform p) { var g = new GameObject(n, typeof(RectTransform)); g.transform.SetParent(p, false); return g; }
        private static Image MakeImg(string n, Transform p, Color32 c) { var i = MakeGO(n, p).AddComponent<Image>(); i.color = c; return i; }
        private static TMP_Text Tmp(Transform p, string t, float s, Color32 c, bool bold)
        {
            var tmp = MakeGO("T", p).AddComponent<TextMeshProUGUI>();
            tmp.text = t; tmp.fontSize = s; tmp.color = c; tmp.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            tmp.alignment = TextAlignmentOptions.MidlineLeft; tmp.raycastTarget = false; tmp.richText = true;
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
