using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using TransportManager.Core;
using TransportManager.Save;
using TransportManager.Systems.Analytics;
using TransportManager.UI.Common;
using UnityEngine;
using UnityEngine.UI;

namespace TransportManager.UI.Analytics
{
    public class AnalyticsPopupView : MonoBehaviour
    {
        private static readonly Color32 BgOverlay = new Color32(0x00, 0x00, 0x00, 180);
        private static readonly Color32 BgPanel   = new Color32(0x16, 0x19, 0x1F, 255);
        private static readonly Color32 BgCard    = new Color32(0x1F, 0x23, 0x2B, 255);
        private static readonly Color32 BgTab     = new Color32(0x2C, 0x32, 0x3C, 255);
        private static readonly Color32 TextPri   = new Color32(0xEC, 0xEF, 0xF5, 255);
        private static readonly Color32 TextSec   = new Color32(0x9A, 0xA5, 0xB8, 255);
        private static readonly Color32 TextMuted = new Color32(0x55, 0x63, 0x78, 255);
        private static readonly Color32 DivColor  = new Color32(0x28, 0x2D, 0x38, 255);

        private static readonly Color32[] TabColors =
        {
            new Color32(0x3D, 0xC9, 0x6E, 255), // Argent
            new Color32(0xF2, 0xD9, 0x66, 255), // Or
            new Color32(0xFA, 0xC0, 0x24, 255), // XP
            new Color32(0x35, 0x8E, 0xF5, 255), // Contrats
        };

        private static readonly string[] TabNames    = { "Argent", "Or", "XP", "Contrats" };
        private static readonly string[] TabSuffixes = { "$", "G", "XP", "" };

        private const int TitleBarH = 56;

        private StatTrackerSystem _tracker;
        private LineChartUI       _chart;
        private TMP_Text          _lblDateRange;
        private TMP_Text          _lblNoData;
        private TMP_Text          _lblMin, _lblAvg, _lblMax;
        private TMP_Text          _lblTotalContracts, _lblVehicles, _lblTotalKm;
        private Image[]           _tabBgs;
        private TMP_Text[]        _tabLabels;
        private TMP_Text[]        _gridLabels;
        private int               _activeTab;
        private Coroutine         _gridCoroutine;

        private Sprite _sprRound12;
        private Sprite _sprRound8;

        // ── Entry point ───────────────────────────────────────────────────────

        public static void Show()
        {
            if (FindObjectOfType<AnalyticsPopupView>() != null) return;
            new GameObject("AnalyticsPopup", typeof(RectTransform)).AddComponent<AnalyticsPopupView>();
        }

        private void Awake()
        {
            _tracker    = ServiceLocator.Get<StatTrackerSystem>();
            _sprRound12 = MakeRoundedSprite(12);
            _sprRound8  = MakeRoundedSprite(8);
            BuildUI();
            RefreshQuickStats();
            SelectTab(0);
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
            var panelRt = panelGo.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.04f, 0.06f);
            panelRt.anchorMax = new Vector2(0.96f, 0.94f);
            panelRt.offsetMin = Vector2.zero;
            panelRt.offsetMax = Vector2.zero;

            PopupHeader.Build(panelGo.transform, "Statistiques", Close, TitleBarH, _sprRound8);
            BuildContent(panelGo.transform);
        }

        private void BuildContent(Transform panel)
        {
            var content   = MakeGO("Content", panel);
            var contentRt = content.GetComponent<RectTransform>();
            contentRt.anchorMin = Vector2.zero;
            contentRt.anchorMax = Vector2.one;
            contentRt.offsetMin = Vector2.zero;
            contentRt.offsetMax = new Vector2(0f, -(TitleBarH + 1f));

            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.padding               = new RectOffset(12, 12, 8, 16);
            vlg.spacing               = 0;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth      = true;
            vlg.childControlHeight     = true;

            BuildTabBar(content.transform);
            AddHDivider(content.transform);
            BuildDateRow(content.transform);
            BuildChartArea(content.transform);
            AddHDivider(content.transform);
            BuildStatsRow(content.transform);
            AddHDivider(content.transform);
            BuildQuickRow(content.transform);
        }

        private void BuildTabBar(Transform parent)
        {
            var bar = MakeGO("TabBar", parent);
            bar.AddComponent<LayoutElement>().preferredHeight = 52;

            var hlg = bar.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing               = 6;
            hlg.padding               = new RectOffset(0, 0, 6, 6);
            hlg.childForceExpandWidth  = true;
            hlg.childForceExpandHeight = true;
            hlg.childControlWidth      = true;
            hlg.childControlHeight     = true;

            _tabBgs    = new Image[4];
            _tabLabels = new TMP_Text[4];

            for (int i = 0; i < 4; i++)
            {
                int idx    = i;
                var tabGo  = MakeGO("Tab_" + TabNames[i], bar.transform);
                var tabImg = tabGo.AddComponent<Image>();
                tabImg.sprite = _sprRound8;
                tabImg.type   = Image.Type.Sliced;
                tabImg.color  = BgTab;
                var btn = tabGo.AddComponent<Button>();
                btn.targetGraphic = tabImg;
                btn.onClick.AddListener(() => SelectTab(idx));

                var lbl = AddTMP("Lbl", tabGo.transform, TabNames[i], 14, FontStyles.Bold, TextSec);
                lbl.alignment = TextAlignmentOptions.Center;
                FillParent(lbl.GetComponent<RectTransform>());

                _tabBgs[i]    = tabImg;
                _tabLabels[i] = lbl;
            }
        }

        private void BuildDateRow(Transform parent)
        {
            _lblDateRange = AddTMP("DateRange", parent, "", 11, FontStyles.Normal, TextMuted);
            _lblDateRange.alignment          = TextAlignmentOptions.Center;
            _lblDateRange.textWrappingMode   = TextWrappingModes.NoWrap;
            _lblDateRange.gameObject.AddComponent<LayoutElement>().preferredHeight = 28;
        }

        private void BuildChartArea(Transform parent)
        {
            var container = MakeGO("ChartArea", parent);
            var img       = container.AddComponent<Image>();
            img.color         = BgCard;
            img.raycastTarget = false;
            container.AddComponent<RectMask2D>();
            var le = container.AddComponent<LayoutElement>();
            le.preferredHeight = 240;
            le.flexibleHeight  = 1;

            // LineChart fills the container
            var chartGo = MakeGO("LineChart", container.transform);
            FillParent(chartGo.GetComponent<RectTransform>());
            _chart = chartGo.AddComponent<LineChartUI>();
            _chart.color         = Color.white;
            _chart.raycastTarget = false;

            // "No data" message centered in chart area
            _lblNoData = AddTMP("NoData", container.transform,
                "Reviens demain pour voir\nton évolution !", 17, FontStyles.Normal, TextSec);
            _lblNoData.alignment          = TextAlignmentOptions.Center;
            _lblNoData.enableWordWrapping = true;
            var ndRt = _lblNoData.GetComponent<RectTransform>();
            ndRt.anchorMin = new Vector2(0.1f, 0.3f);
            ndRt.anchorMax = new Vector2(0.9f, 0.7f);
            ndRt.offsetMin = Vector2.zero;
            ndRt.offsetMax = Vector2.zero;

            // Grid value labels (positioned dynamically via coroutine)
            _gridLabels = new TMP_Text[5];
            for (int i = 0; i < _gridLabels.Length; i++)
            {
                _gridLabels[i] = AddTMP($"GridLbl{i}", container.transform, "",
                                         10, FontStyles.Normal, new Color32(0x70, 0x80, 0x98, 200));
                _gridLabels[i].alignment = TextAlignmentOptions.Left;
                _gridLabels[i].gameObject.SetActive(false);
                var rt = _gridLabels[i].GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = Vector2.zero;
                rt.pivot     = new Vector2(0f, 0.5f);
                rt.sizeDelta = new Vector2(72f, 16f);
            }
        }

        private void BuildStatsRow(Transform parent)
        {
            var row = MakeGO("StatsRow", parent);
            row.AddComponent<LayoutElement>().preferredHeight = 46;

            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.padding               = new RectOffset(4, 4, 4, 4);
            hlg.spacing               = 2;
            hlg.childForceExpandWidth  = true;
            hlg.childForceExpandHeight = false;
            hlg.childControlWidth      = true;
            hlg.childControlHeight     = false;
            hlg.childAlignment         = TextAnchor.MiddleCenter;

            _lblMin = AddStatCell(row.transform, "MIN", "—");
            _lblAvg = AddStatCell(row.transform, "MOYENNE", "—");
            _lblMax = AddStatCell(row.transform, "MAX", "—");
        }

        private void BuildQuickRow(Transform parent)
        {
            var row = MakeGO("QuickRow", parent);
            row.AddComponent<LayoutElement>().preferredHeight = 46;

            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.padding               = new RectOffset(4, 4, 4, 4);
            hlg.spacing               = 2;
            hlg.childForceExpandWidth  = true;
            hlg.childForceExpandHeight = false;
            hlg.childControlWidth      = true;
            hlg.childControlHeight     = false;
            hlg.childAlignment         = TextAnchor.MiddleCenter;

            _lblTotalContracts = AddStatCell(row.transform, "CONTRATS", "—");
            _lblVehicles       = AddStatCell(row.transform, "VÉHICULES", "—");
            _lblTotalKm        = AddStatCell(row.transform, "KM TOTAL", "—");
        }

        private TMP_Text AddStatCell(Transform parent, string header, string value)
        {
            var cell   = MakeGO("Cell_" + header, parent);
            var cellLe = cell.AddComponent<LayoutElement>();
            cellLe.preferredHeight = 38;

            var vlg = cell.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment         = TextAnchor.MiddleCenter;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth      = true;
            vlg.childControlHeight     = false;
            vlg.spacing = 1;

            var valueLbl = AddTMP("Value", cell.transform, value, 15, FontStyles.Bold, TextPri);
            valueLbl.alignment        = TextAlignmentOptions.Center;
            valueLbl.textWrappingMode = TextWrappingModes.NoWrap;
            valueLbl.gameObject.AddComponent<LayoutElement>().preferredHeight = 19;

            var headerLbl = AddTMP("Header", cell.transform, header, 9, FontStyles.Bold, TextMuted);
            headerLbl.alignment        = TextAlignmentOptions.Center;
            headerLbl.characterSpacing = 80;
            headerLbl.gameObject.AddComponent<LayoutElement>().preferredHeight = 11;

            return valueLbl;
        }

        private void AddHDivider(Transform parent)
        {
            var go  = MakeGO("HDivider", parent);
            var img = go.AddComponent<Image>();
            img.color         = DivColor;
            img.raycastTarget = false;
            go.AddComponent<LayoutElement>().preferredHeight = 1;
        }

        // ── Logic ─────────────────────────────────────────────────────────────

        private void SelectTab(int index)
        {
            _activeTab = index;

            for (int i = 0; i < 4; i++)
            {
                bool active = i == index;
                if (_tabBgs[i] != null)
                    _tabBgs[i].color = active ? (Color)TabColors[i] : (Color)BgTab;
                if (_tabLabels[i] != null)
                    _tabLabels[i].color = active ? (Color)TextPri : (Color)TextSec;
            }

            // Hide grid labels immediately while waiting for layout
            if (_gridLabels != null)
                foreach (var lbl in _gridLabels) if (lbl) lbl.gameObject.SetActive(false);

            var snaps    = _tracker?.Snapshots;
            float[] data = BuildDataArray(index, snaps);
            bool hasData = data != null && data.Length >= 2;

            _chart.gameObject.SetActive(hasData);
            _lblNoData.gameObject.SetActive(!hasData);

            if (hasData)
            {
                _chart.SetData(data, TabColors[index]);
                UpdateMinAvgMax(data, index);
                _lblDateRange.text = FormatDateRange(snaps);

                if (_gridCoroutine != null) StopCoroutine(_gridCoroutine);
                _gridCoroutine = StartCoroutine(PositionGridLabels(index));
            }
            else
            {
                _lblMin.text = _lblAvg.text = _lblMax.text = "—";
                _lblDateRange.text = "";
            }
        }

        private IEnumerator PositionGridLabels(int tabIndex)
        {
            yield return null; // wait one frame for layout rebuild

            if (_chart == null || _gridLabels == null) yield break;

            float[] gridVals = LineChartUI.ComputeNiceGridValues(_chart.VizMin, _chart.VizMax, 4);
            string suf = TabSuffixes[tabIndex].Length > 0 ? " " + TabSuffixes[tabIndex] : "";

            for (int i = 0; i < _gridLabels.Length; i++)
            {
                if (i < gridVals.Length)
                {
                    float nY  = _chart.GetNormalizedRectY(gridVals[i]);
                    var   rt  = _gridLabels[i].GetComponent<RectTransform>();
                    rt.anchorMin        = new Vector2(0f, nY);
                    rt.anchorMax        = new Vector2(0f, nY);
                    rt.pivot            = new Vector2(0f, 0.5f);
                    rt.sizeDelta        = new Vector2(72f, 16f);
                    rt.anchoredPosition = new Vector2(6f, 0f);
                    _gridLabels[i].text = FormatNum(gridVals[i]) + suf;
                    _gridLabels[i].gameObject.SetActive(true);
                }
                else
                {
                    _gridLabels[i].gameObject.SetActive(false);
                }
            }
        }

        private static float[] BuildDataArray(int tabIndex, IReadOnlyList<StatSnapshot> snaps)
        {
            if (snaps == null || snaps.Count < 2) return null;
            var data = new float[snaps.Count];
            for (int i = 0; i < snaps.Count; i++)
            {
                switch (tabIndex)
                {
                    case 0: data[i] = snaps[i].dollars;            break;
                    case 1: data[i] = snaps[i].goldIngots;         break;
                    case 2: data[i] = snaps[i].companyXp;          break;
                    case 3: data[i] = snaps[i].contractsCompleted; break;
                }
            }
            return data;
        }

        private void UpdateMinAvgMax(float[] data, int tabIndex)
        {
            float min = data[0], max = data[0], sum = 0f;
            foreach (float v in data)
            {
                if (v < min) min = v;
                if (v > max) max = v;
                sum += v;
            }
            float avg = sum / data.Length;
            string suf = TabSuffixes[tabIndex].Length > 0 ? " " + TabSuffixes[tabIndex] : "";
            _lblMin.text = FormatNum(min) + suf;
            _lblAvg.text = FormatNum(avg) + suf;
            _lblMax.text = FormatNum(max) + suf;
        }

        private void RefreshQuickStats()
        {
            var gm = GameManager.Instance;
            if (gm?.Save == null) return;
            var save = gm.Save;

            int contracts = 0;
            if (save.hiredDrivers != null)
                foreach (var d in save.hiredDrivers) contracts += d.contractsCompleted;

            int km = 0;
            if (save.vehicles != null)
                foreach (var v in save.vehicles) km += v.totalKilometers;

            if (_lblTotalContracts != null) _lblTotalContracts.text = contracts.ToString("N0");
            if (_lblVehicles != null)       _lblVehicles.text       = (save.vehicles?.Count ?? 0).ToString();
            if (_lblTotalKm != null)        _lblTotalKm.text        = FormatNum(km) + " km";
        }

        private static string FormatDateRange(IReadOnlyList<StatSnapshot> snaps)
        {
            if (snaps == null || snaps.Count == 0) return "";
            var first = new DateTime(snaps[0].utcTicks, DateTimeKind.Utc).ToLocalTime();
            var last  = new DateTime(snaps[snaps.Count - 1].utcTicks, DateTimeKind.Utc).ToLocalTime();
            string[] mo = { "jan", "fév", "mar", "avr", "mai", "juin", "juil", "aoû", "sep", "oct", "nov", "déc" };
            if (snaps.Count <= 1)
                return $"{last.Day} {mo[last.Month - 1]} {last.Year}";
            int days = (int)(last - first).TotalDays + 1;
            return $"{first.Day} {mo[first.Month - 1]} → {last.Day} {mo[last.Month - 1]} {last.Year}  ·  {days}j";
        }

        private static string FormatNum(float v)
        {
            if (v >= 1_000_000f) return $"{v / 1_000_000f:F1}M";
            if (v >= 1_000f)     return $"{v / 1_000f:F1}K";
            return $"{(int)v}";
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
            return Sprite.Create(tex, new Rect(0, 0, size, size),
                                 new Vector2(0.5f, 0.5f), 100f, 0,
                                 SpriteMeshType.FullRect, new Vector4(r, r, r, r));
        }

        private static float RoundedAlpha(int x, int y, int size, int r)
        {
            int cx = -1, cy = -1;
            if      (x < r         && y < r)          { cx = r;        cy = r;        }
            else if (x >= size - r && y < r)           { cx = size - r; cy = r;        }
            else if (x < r         && y >= size - r)   { cx = r;        cy = size - r; }
            else if (x >= size - r && y >= size - r)   { cx = size - r; cy = size - r; }
            if (cx < 0) return 1f;
            float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
            return Mathf.Clamp01(r - d + 0.5f);
        }
    }
}
