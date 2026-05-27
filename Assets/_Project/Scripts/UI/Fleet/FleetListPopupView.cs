using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using TransportManager.Core;
using TransportManager.Entities.Contracts;
using TransportManager.Entities.Vehicles;
using TransportManager.Enums;
using TransportManager.Events;
using TransportManager.Systems.Contracts;
using TransportManager.Systems.Fleet;
using TransportManager.Systems.Map;
using TransportManager.Systems.Map.Visualization;
using TransportManager.UI.Common;
using UnityEngine;
using UnityEngine.UI;

namespace TransportManager.UI.Fleet
{
    public class FleetListPopupView : MonoBehaviour
    {
        private static readonly Color32 BgOverlay = new Color32(0x00, 0x00, 0x00, 180);
        private static readonly Color32 BgPanel   = new Color32(0x16, 0x19, 0x1F, 255);
        private static readonly Color32 BgCard    = new Color32(0x1F, 0x23, 0x2B, 255);
        private static readonly Color32 TextPri   = new Color32(0xEC, 0xEF, 0xF5, 255);
        private static readonly Color32 TextSec   = new Color32(0x9A, 0xA5, 0xB8, 255);
        private static readonly Color32 TextMuted = new Color32(0x55, 0x63, 0x78, 255);
        private static readonly Color32 ColGreen  = new Color32(0x3D, 0xC9, 0x6E, 255);
        private static readonly Color32 ColRed    = new Color32(0xE0, 0x52, 0x52, 255);
        private static readonly Color32 ColOrange = new Color32(0xFA, 0xC0, 0x24, 255);
        private static readonly Color32 ColGray   = new Color32(0x55, 0x63, 0x78, 255);

        private const int TitleBarH   = 56;
        private const int CardH       = 54;
        private const int BadgeW      = 96;
        private const int BadgeH      = 28;
        private const float BorderPx  = 1.5f;

        private Sprite _sprRound8;
        private Sprite _sprRound12;

        private readonly List<ContractCardRefs> _liveRefs = new List<ContractCardRefs>();
        private Coroutine _refreshCoroutine;

        private class ContractCardRefs
        {
            public TMP_Text         infoLabel;
            public ContractInstance contract;
        }

        // ── Entry point ───────────────────────────────────────────────────────

        public static void Show()
        {
            if (FindObjectOfType<FleetListPopupView>() != null) return;
            new GameObject("FleetListPopup", typeof(RectTransform)).AddComponent<FleetListPopupView>();
        }

        private void Awake()
        {
            _sprRound8  = MakeRoundedSprite(8);
            _sprRound12 = MakeRoundedSprite(12);
            BuildUI();
            _refreshCoroutine = StartCoroutine(RefreshLoop());
        }

        private void OnDestroy()
        {
            if (_refreshCoroutine != null) StopCoroutine(_refreshCoroutine);
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

            PopupHeader.Build(panelGo.transform, "Flotte", Close, TitleBarH, _sprRound8);
            BuildScrollArea(panelGo.transform);
        }

        private void BuildScrollArea(Transform panel)
        {
            var scrollGo = MakeGO("Scroll", panel);
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.anchorMin = Vector2.zero;
            scrollRt.anchorMax = Vector2.one;
            scrollRt.offsetMin = Vector2.zero;
            scrollRt.offsetMax = new Vector2(0f, -(TitleBarH + 1f));

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
            scrollRect.content  = contentRt;

            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.padding               = new RectOffset(14, 14, 12, 16);
            vlg.spacing               = 8;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth      = true;
            vlg.childControlHeight     = true;
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            BuildCards(content.transform);
        }

        private void BuildCards(Transform parent)
        {
            var fleet    = ServiceLocator.Get<FleetSystem>();
            var contracts = ServiceLocator.Get<ContractSystem>();
            var catalog  = ServiceLocator.Get<VehicleCatalog>();
            var mapSys   = ServiceLocator.Get<MapSystem>();

            if (fleet == null || fleet.Vehicles.Count == 0)
            {
                var empty = AddTMP("Empty", parent, "Aucun véhicule dans la flotte.", 15, FontStyles.Normal, TextSec);
                empty.alignment = TextAlignmentOptions.Center;
                empty.gameObject.AddComponent<LayoutElement>().preferredHeight = 80;
                return;
            }

            foreach (var vehicle in fleet.Vehicles)
            {
                VehicleData      data     = catalog?.GetById(vehicle.vehicleDataId);
                ContractInstance contract = FindActiveContract(vehicle.instanceId, contracts);
                BuildCard(parent, vehicle, data, contract, mapSys);
            }
        }

        private void BuildCard(Transform parent, VehicleInstance vehicle, VehicleData data,
                               ContractInstance contract, MapSystem mapSys)
        {
            // ── Fond carte ────────────────────────────────────────────────────
            var card    = MakeGO("Card_" + vehicle.instanceId, parent);
            var cardImg = card.AddComponent<Image>();
            cardImg.sprite        = _sprRound8;
            cardImg.type          = Image.Type.Sliced;
            cardImg.color         = BgCard;
            cardImg.raycastTarget = false;
            card.AddComponent<LayoutElement>().preferredHeight = CardH;

            var hlg = card.AddComponent<HorizontalLayoutGroup>();
            hlg.padding               = new RectOffset(14, 12, 0, 0);
            hlg.spacing               = 10;
            hlg.childAlignment        = TextAnchor.MiddleLeft;
            hlg.childForceExpandWidth  = false;
            hlg.childForceExpandHeight = true;
            hlg.childControlWidth      = true;
            hlg.childControlHeight     = true;

            // ── Nom véhicule (flex) ───────────────────────────────────────────
            var nameLbl = AddTMP("Name", card.transform,
                data?.displayName ?? vehicle.vehicleDataId, 14, FontStyles.Bold, TextPri);
            nameLbl.textWrappingMode = TextWrappingModes.NoWrap;
            nameLbl.overflowMode     = TextOverflowModes.Ellipsis;
            nameLbl.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

            // ── Km total ──────────────────────────────────────────────────────
            var kmLbl = AddTMP("Km", card.transform,
                $"{vehicle.totalKilometers:N0} km", 11, FontStyles.Normal, TextMuted);
            kmLbl.textWrappingMode = TextWrappingModes.NoWrap;
            var kmLe = kmLbl.gameObject.AddComponent<LayoutElement>();
            kmLe.preferredWidth  = 78;
            kmLe.minWidth        = 78;

            // ── Contrat : % + temps restant ───────────────────────────────────
            TMP_Text infoLbl = null;
            if (contract != null)
            {
                infoLbl = AddTMP("ContractInfo", card.transform, "—", 12, FontStyles.Normal, TextSec);
                infoLbl.textWrappingMode = TextWrappingModes.NoWrap;
                infoLbl.alignment        = TextAlignmentOptions.MidlineRight;
                var infoLe = infoLbl.gameObject.AddComponent<LayoutElement>();
                infoLe.preferredWidth = 118;
                infoLe.minWidth       = 118;

                // Bouton œil (localiser)
                BuildEyeButton(card.transform, contract, mapSys);

                _liveRefs.Add(new ContractCardRefs { infoLabel = infoLbl, contract = contract });
                UpdateContractCard(_liveRefs[_liveRefs.Count - 1]);
            }
            else
            {
                // Espace réservé pour aligner le badge à droite
                var spacer = MakeGO("Spacer", card.transform);
                spacer.AddComponent<LayoutElement>().preferredWidth = 118 + 10 + 36;
            }

            // ── Badge statut (outline) ────────────────────────────────────────
            BuildStatusBadge(card.transform, vehicle.status);
        }

        private void BuildEyeButton(Transform parent, ContractInstance contract, MapSystem mapSys)
        {
            var btnGo  = MakeGO("EyeBtn", parent);
            var btnImg = btnGo.AddComponent<Image>();
            btnImg.color         = Color.clear;
            btnImg.raycastTarget = true;
            var btn = btnGo.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            var btnLe = btnGo.AddComponent<LayoutElement>();
            btnLe.preferredWidth  = 36;
            btnLe.minWidth        = 36;

            var iconGo  = MakeGO("Icon", btnGo.transform);
            var iconImg = iconGo.AddComponent<Image>();
            iconImg.sprite         = Resources.Load<Sprite>("UI/Icons/icons/eye");
            iconImg.color          = new Color32(0x35, 0x8E, 0xF5, 220);
            iconImg.preserveAspect = true;
            iconImg.raycastTarget  = false;
            var iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin        = new Vector2(0.5f, 0.5f);
            iconRt.anchorMax        = new Vector2(0.5f, 0.5f);
            iconRt.pivot            = new Vector2(0.5f, 0.5f);
            iconRt.anchoredPosition = Vector2.zero;
            iconRt.sizeDelta        = new Vector2(20, 20);

            btn.onClick.AddListener(() => LocateVehicle(contract, mapSys));
        }

        private void BuildStatusBadge(Transform parent, VehicleStatus status)
        {
            Color32 col;
            string  label;
            switch (status)
            {
                case VehicleStatus.OnContract:
                    col = ColGreen; label = "En route"; break;
                case VehicleStatus.Immobilized:
                    col = ColRed;   label = "Immobilisé"; break;
                case VehicleStatus.InMaintenance:
                    col = ColOrange; label = "Maintenance"; break;
                default:
                    col = ColGray;  label = "Disponible"; break;
            }

            // Outer (contour coloré)
            var outer    = MakeGO("BadgeOuter", parent);
            var outerImg = outer.AddComponent<Image>();
            outerImg.sprite = _sprRound8;
            outerImg.type   = Image.Type.Sliced;
            outerImg.color  = col;
            var outerLe = outer.AddComponent<LayoutElement>();
            outerLe.preferredWidth  = BadgeW;
            outerLe.minWidth        = BadgeW;
            outerLe.preferredHeight = BadgeH;

            // Inner (fond sombre = crée l'effet de contour)
            var inner    = MakeGO("BadgeInner", outer.transform);
            var innerImg = inner.AddComponent<Image>();
            innerImg.sprite        = _sprRound8;
            innerImg.type          = Image.Type.Sliced;
            innerImg.color         = BgCard;
            innerImg.raycastTarget = false;
            var innerRt = inner.GetComponent<RectTransform>();
            FillParent(innerRt);
            innerRt.offsetMin = new Vector2( BorderPx,  BorderPx);
            innerRt.offsetMax = new Vector2(-BorderPx, -BorderPx);

            // Label coloré par-dessus
            var lbl = AddTMP("Lbl", inner.transform, label, 11, FontStyles.Bold, col);
            lbl.alignment = TextAlignmentOptions.Center;
            FillParent(lbl.GetComponent<RectTransform>());
        }

        // ── Live refresh ──────────────────────────────────────────────────────

        private IEnumerator RefreshLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(1f);
                foreach (var r in _liveRefs) UpdateContractCard(r);
            }
        }

        private void UpdateContractCard(ContractCardRefs refs)
        {
            var contract = refs.contract;
            var now      = DateTime.UtcNow;

            long totalTicks = contract.completionTimeUtcTicks - contract.startTimeUtcTicks;
            float progress  = totalTicks > 0
                ? Mathf.Clamp01((float)(now.Ticks - contract.startTimeUtcTicks) / totalTicks)
                : 1f;

            int pct = Mathf.RoundToInt(progress * 100f);

            var remaining = contract.CompletionTimeUtc - now;
            if (remaining.TotalSeconds < 0) remaining = TimeSpan.Zero;

            string timeStr = pct >= 100 ? "Terminé" : FormatTimeRemaining(remaining);

            if (refs.infoLabel)
                refs.infoLabel.text = $"{pct}%  ·  {timeStr}";
        }

        // ── Locate ────────────────────────────────────────────────────────────

        private void LocateVehicle(ContractInstance contract, MapSystem mapSys)
        {
            var now = DateTime.UtcNow;
            long totalTicks = contract.completionTimeUtcTicks - contract.startTimeUtcTicks;
            float progress  = totalTicks > 0
                ? Mathf.Clamp01((float)(now.Ticks - contract.startTimeUtcTicks) / totalTicks)
                : 0f;

            GameEvents.RaiseTabChanged(TabType.Map);

            if (mapSys?.Catalog != null)
            {
                var orig = mapSys.Catalog.GetById(contract.definition.originCityId);
                var dest = mapSys.Catalog.GetById(contract.definition.destinationCityId);
                if (orig != null && dest != null)
                {
                    double lat = orig.location.latitude  + (dest.location.latitude  - orig.location.latitude)  * progress;
                    double lon = orig.location.longitude + (dest.location.longitude - orig.location.longitude) * progress;
                    var mapView = FindObjectOfType<SlippyMapView>(true);
                    if (mapView != null) mapView.SetView(lat, lon, 12);
                }
            }

            Destroy(gameObject);
        }

        private void Close() => Destroy(gameObject);

        // ── Helpers ───────────────────────────────────────────────────────────

        private static ContractInstance FindActiveContract(string vehicleId, ContractSystem contracts)
        {
            if (contracts == null) return null;
            foreach (var c in contracts.Active)
                if (c.assignedVehicleInstanceId == vehicleId) return c;
            return null;
        }

        private static string FormatTimeRemaining(TimeSpan t)
        {
            if (t.TotalSeconds <= 0) return "Terminé";
            if (t.TotalHours   >= 1) return $"{(int)t.TotalHours}h {t.Minutes:D2}m";
            if (t.TotalMinutes >= 1) return $"{(int)t.TotalMinutes}m {t.Seconds:D2}s";
            return $"{t.Seconds}s";
        }

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
