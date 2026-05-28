using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using TransportManager.Core;
using TransportManager.Entities.Contracts;
using TransportManager.Entities.Drivers;
using TransportManager.Entities.Progression;
using TransportManager.Entities.Vehicles;
using TransportManager.Enums;
using TransportManager.Events;
using TransportManager.Systems.Contracts;
using TransportManager.Systems.Fleet;
using TransportManager.Systems.Hr;
using TransportManager.Systems.Map;
using TransportManager.Systems.Map.Visualization;
using TransportManager.UI.Common;
using UnityEngine;
using UnityEngine.UI;

namespace TransportManager.UI.Fleet
{
    public class FleetListPopupView : MonoBehaviour
    {
        // Palette partagée (Header / Navbar / ContractsPanel / VehiclesTab)
        private static readonly Color32 BgOverlay = new Color32(0x00, 0x00, 0x00, 200);
        private static readonly Color32 BgPanel   = new Color32(0x2C, 0x30, 0x38, 255);
        private static readonly Color32 BgCard    = new Color32(0x34, 0x38, 0x42, 255);
        private static readonly Color32 TextPri   = new Color32(0xEC, 0xEE, 0xF5, 255);
        private static readonly Color32 TextSec   = new Color32(0x7A, 0x8F, 0xA6, 255);
        private static readonly Color32 TextMuted = new Color32(0x5A, 0x65, 0x77, 255);
        private static readonly Color32 ColGreen  = new Color32(0x3D, 0xC9, 0x6E, 255);
        private static readonly Color32 ColRed    = new Color32(0xE0, 0x52, 0x52, 255);
        private static readonly Color32 ColOrange = new Color32(0xFA, 0xC0, 0x24, 255);
        private static readonly Color32 ColBlue   = new Color32(0x35, 0x8E, 0xF5, 255);
        private static readonly Color32 ColGray   = new Color32(0x55, 0x63, 0x78, 255);

        private const int TitleBarH   = 56;
        private const int ColHeaderH  = 40;
        private const int BadgeH      = 26;
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
            var panelShadow = panelGo.AddComponent<Shadow>();
            panelShadow.effectColor    = new Color(0f, 0f, 0f, 0.5f);
            panelShadow.effectDistance = new Vector2(0f, -4f);
            var panelRt = panelGo.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.04f, 0.06f);
            panelRt.anchorMax = new Vector2(0.96f, 0.94f);
            panelRt.offsetMin = Vector2.zero;
            panelRt.offsetMax = Vector2.zero;

            PopupHeader.Build(panelGo.transform, "Flotte", Close, TitleBarH, _sprRound8);
            BuildBody(panelGo.transform);
        }

        private void BuildBody(Transform panel)
        {
            var body    = MakeGO("Body", panel);
            var bodyRt  = body.GetComponent<RectTransform>();
            bodyRt.anchorMin = Vector2.zero;
            bodyRt.anchorMax = Vector2.one;
            bodyRt.offsetMin = Vector2.zero;
            bodyRt.offsetMax = new Vector2(0f, -(TitleBarH + 1f));

            // Colonne gauche : véhicules ── Colonne droite : conducteurs
            var leftContent  = BuildColumn(body.transform, "VÉHICULES",    ColBlue,  0f,   0.5f, 14f, -7f);
            var rightContent = BuildColumn(body.transform, "CONDUCTEURS", ColGreen, 0.5f, 1f,    7f, -14f);

            BuildVehicleCards(leftContent);
            BuildDriverCards(rightContent);
        }

        // Construit une colonne (titre + séparateur + zone de scroll) et renvoie le Content de la liste.
        private Transform BuildColumn(Transform parent, string title, Color32 accent,
                                      float anchorXMin, float anchorXMax, float padLeft, float padRight)
        {
            var col   = MakeGO("Col_" + title, parent);
            var colRt = col.GetComponent<RectTransform>();
            colRt.anchorMin = new Vector2(anchorXMin, 0f);
            colRt.anchorMax = new Vector2(anchorXMax, 1f);
            colRt.offsetMin = new Vector2(padLeft, 12f);
            colRt.offsetMax = new Vector2(padRight, -12f);

            // ── En-tête de colonne (barre d'accent + titre) ──
            var header   = MakeGO("Header", col.transform);
            var headerRt = header.GetComponent<RectTransform>();
            headerRt.anchorMin = new Vector2(0f, 1f);
            headerRt.anchorMax = new Vector2(1f, 1f);
            headerRt.pivot     = new Vector2(0.5f, 1f);
            headerRt.offsetMin = new Vector2(0f, -ColHeaderH);
            headerRt.offsetMax = Vector2.zero;

            var hlg = header.AddComponent<HorizontalLayoutGroup>();
            hlg.padding               = new RectOffset(6, 6, 0, 0);
            hlg.spacing               = 10;
            hlg.childAlignment        = TextAnchor.MiddleLeft;
            hlg.childForceExpandWidth  = false;
            hlg.childForceExpandHeight = false;
            hlg.childControlWidth      = true;
            hlg.childControlHeight     = true;

            var accentGo  = MakeGO("Accent", header.transform);
            var accentImg = accentGo.AddComponent<Image>();
            accentImg.sprite        = _sprRound8;
            accentImg.type          = Image.Type.Sliced;
            accentImg.color         = accent;
            accentImg.raycastTarget = false;
            var accentLe = accentGo.AddComponent<LayoutElement>();
            accentLe.preferredWidth  = 3;
            accentLe.preferredHeight = 22;

            var titleLbl = AddTMP("Title", header.transform, title, 15, FontStyles.Bold, TextPri);
            titleLbl.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

            // ── Séparateur sous l'en-tête ──
            var sep   = MakeImg("Sep", col.transform, new Color32(0xFF, 0xFF, 0xFF, 20));
            sep.raycastTarget = false;
            var sepRt = sep.GetComponent<RectTransform>();
            sepRt.anchorMin = new Vector2(0f, 1f);
            sepRt.anchorMax = new Vector2(1f, 1f);
            sepRt.pivot     = new Vector2(0.5f, 1f);
            sepRt.offsetMin = new Vector2(4f, -ColHeaderH - 1f);
            sepRt.offsetMax = new Vector2(-4f, -ColHeaderH);

            // ── Zone de scroll ──
            var scrollGo = MakeGO("Scroll", col.transform);
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.anchorMin = Vector2.zero;
            scrollRt.anchorMax = Vector2.one;
            scrollRt.offsetMin = Vector2.zero;
            scrollRt.offsetMax = new Vector2(0f, -(ColHeaderH + 4f));

            var scrollRect = scrollGo.AddComponent<ScrollRect>();
            scrollRect.horizontal        = false;
            scrollRect.vertical          = true;
            scrollRect.scrollSensitivity = 40;
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
            vlg.padding               = new RectOffset(4, 4, 6, 12);
            vlg.spacing               = 8;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth      = true;
            vlg.childControlHeight     = true;
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            return content.transform;
        }

        // ── Cartes véhicules ──────────────────────────────────────────────────

        private void BuildVehicleCards(Transform parent)
        {
            var fleet     = ServiceLocator.Get<FleetSystem>();
            var contracts = ServiceLocator.Get<ContractSystem>();
            var catalog   = ServiceLocator.Get<VehicleCatalog>();
            var mapSys    = ServiceLocator.Get<MapSystem>();

            if (fleet == null || fleet.Vehicles.Count == 0)
            {
                AddEmpty(parent, "Aucun véhicule dans la flotte.");
                return;
            }

            foreach (var vehicle in fleet.Vehicles)
            {
                VehicleData      data     = catalog?.GetById(vehicle.vehicleDataId);
                ContractInstance contract = FindActiveContract(vehicle.instanceId, contracts);
                BuildVehicleCard(parent, vehicle, data, contract, mapSys);
            }
        }

        private void BuildVehicleCard(Transform parent, VehicleInstance vehicle, VehicleData data,
                                      ContractInstance contract, MapSystem mapSys)
        {
            var card = MakeCard(parent, "Veh_" + vehicle.instanceId);

            // ── Ligne 1 : nom + badge statut ──
            var headerRow = MakeRow(card.transform, "Header", 26);
            var nameLbl = AddTMP("Name", headerRow.transform,
                data?.displayName ?? vehicle.vehicleDataId, 15, FontStyles.Bold, TextPri);
            nameLbl.textWrappingMode = TextWrappingModes.NoWrap;
            nameLbl.overflowMode     = TextOverflowModes.Ellipsis;
            nameLbl.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

            BuildOutlineBadge(headerRow.transform, StatusLabel(vehicle.status), StatusColor(vehicle.status), 96);

            // ── Description véhicule ──
            string desc = data != null
                ? $"{data.category}  ·  {data.capacity} t  ·  {data.speedKmh:0} km/h  ·  {vehicle.totalKilometers:N0} km"
                : $"{vehicle.totalKilometers:N0} km";
            var descLbl = AddTMP("Desc", card.transform, desc, 12, FontStyles.Normal, TextSec);
            descLbl.textWrappingMode = TextWrappingModes.NoWrap;
            descLbl.overflowMode     = TextOverflowModes.Ellipsis;
            descLbl.gameObject.AddComponent<LayoutElement>().preferredHeight = 18;

            // ── Contrat en cours ──
            BuildContractSection(card.transform, contract, mapSys);
        }

        // ── Cartes conducteurs ────────────────────────────────────────────────

        private void BuildDriverCards(Transform parent)
        {
            var hr        = ServiceLocator.Get<HrSystem>();
            var fleet     = ServiceLocator.Get<FleetSystem>();
            var contracts = ServiceLocator.Get<ContractSystem>();
            var catalog   = ServiceLocator.Get<VehicleCatalog>();
            var mapSys    = ServiceLocator.Get<MapSystem>();

            if (hr == null || hr.HiredDrivers.Count == 0)
            {
                AddEmpty(parent, "Aucun conducteur embauché.");
                return;
            }

            foreach (var driver in hr.HiredDrivers)
            {
                ContractInstance contract = string.IsNullOrEmpty(driver.assignedVehicleInstanceId)
                    ? null
                    : FindActiveContract(driver.assignedVehicleInstanceId, contracts);
                BuildDriverCard(parent, driver, contract, fleet, catalog, mapSys);
            }
        }

        private void BuildDriverCard(Transform parent, DriverInstance driver, ContractInstance contract,
                                     FleetSystem fleet, VehicleCatalog catalog, MapSystem mapSys)
        {
            var card = MakeCard(parent, "Drv_" + driver.instanceId);

            // ── Ligne 1 : nom + badge niveau ──
            var headerRow = MakeRow(card.transform, "Header", 26);
            var nameLbl = AddTMP("Name", headerRow.transform, driver.FullName, 15, FontStyles.Bold, TextPri);
            nameLbl.textWrappingMode = TextWrappingModes.NoWrap;
            nameLbl.overflowMode     = TextOverflowModes.Ellipsis;
            nameLbl.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

            int level = XpCurve.DriverLevelFromXp(driver.xp);
            BuildOutlineBadge(headerRow.transform, $"Niv. {level}", ColBlue, 66);

            // ── Description conducteur ──
            string vehicleName = "Sans véhicule";
            if (!string.IsNullOrEmpty(driver.assignedVehicleInstanceId))
            {
                var veh  = fleet?.GetById(driver.assignedVehicleInstanceId);
                var veh_d = veh != null ? catalog?.GetById(veh.vehicleDataId) : null;
                vehicleName = veh_d?.displayName ?? "Véhicule assigné";
            }

            string nat  = string.IsNullOrEmpty(driver.nationality) ? "—" : driver.nationality;
            string desc = $"{nat}  ·  ${driver.assignedWagePerContract}/contrat  ·  {vehicleName}";
            var descLbl = AddTMP("Desc", card.transform, desc, 12, FontStyles.Normal, TextSec);
            descLbl.textWrappingMode = TextWrappingModes.NoWrap;
            descLbl.overflowMode     = TextOverflowModes.Ellipsis;
            descLbl.gameObject.AddComponent<LayoutElement>().preferredHeight = 18;

            // ── Contrat en cours ──
            BuildContractSection(card.transform, contract, mapSys);
        }

        // ── Section contrat (partagée véhicule / conducteur) ───────────────────

        private void BuildContractSection(Transform card, ContractInstance contract, MapSystem mapSys)
        {
            if (contract == null)
            {
                var none = AddTMP("NoContract", card, "Aucun contrat en cours", 11,
                                  FontStyles.Italic, TextMuted);
                none.gameObject.AddComponent<LayoutElement>().preferredHeight = 16;
                return;
            }

            // Trajet origine → destination
            var routeLbl = AddTMP("Route", card, ContractRouteLabel(contract, mapSys), 12, FontStyles.Bold, ColGreen);
            routeLbl.textWrappingMode = TextWrappingModes.NoWrap;
            routeLbl.overflowMode     = TextOverflowModes.Ellipsis;
            routeLbl.gameObject.AddComponent<LayoutElement>().preferredHeight = 18;

            // Ligne : progression % + temps restant + bouton œil
            var infoRow  = MakeRow(card, "Contract", 22);
            var infoLbl  = AddTMP("Info", infoRow.transform, "—", 12, FontStyles.Normal, TextSec);
            infoLbl.textWrappingMode = TextWrappingModes.NoWrap;
            infoLbl.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

            BuildEyeButton(infoRow.transform, contract, mapSys);

            _liveRefs.Add(new ContractCardRefs { infoLabel = infoLbl, contract = contract });
            UpdateContractCard(_liveRefs[_liveRefs.Count - 1]);
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
            btnLe.preferredWidth  = 32;
            btnLe.minWidth        = 32;

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

        private void BuildOutlineBadge(Transform parent, string label, Color32 col, int width)
        {
            // Outer (contour coloré)
            var outer    = MakeGO("BadgeOuter", parent);
            var outerImg = outer.AddComponent<Image>();
            outerImg.sprite = _sprRound8;
            outerImg.type   = Image.Type.Sliced;
            outerImg.color  = col;
            var outerLe = outer.AddComponent<LayoutElement>();
            outerLe.preferredWidth  = width;
            outerLe.minWidth        = width;
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

        private static string StatusLabel(VehicleStatus status)
        {
            switch (status)
            {
                case VehicleStatus.OnContract:    return "En route";
                case VehicleStatus.Immobilized:   return "Immobilisé";
                case VehicleStatus.InMaintenance: return "Maintenance";
                default:                          return "Disponible";
            }
        }

        private static Color32 StatusColor(VehicleStatus status)
        {
            switch (status)
            {
                case VehicleStatus.OnContract:    return ColGreen;
                case VehicleStatus.Immobilized:   return ColRed;
                case VehicleStatus.InMaintenance: return ColOrange;
                default:                          return ColGray;
            }
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

        private string ContractRouteLabel(ContractInstance contract, MapSystem mapSys)
        {
            string o = contract.definition.originCityId;
            string d = contract.definition.destinationCityId;
            if (mapSys?.Catalog != null)
            {
                var oc = mapSys.Catalog.GetById(o);
                var dc = mapSys.Catalog.GetById(d);
                if (oc != null) o = oc.displayName;
                if (dc != null) d = dc.displayName;
            }
            return $"{o} → {d}";
        }

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

        // Carte conteneur : fond arrondi + layout vertical auto-dimensionné.
        private GameObject MakeCard(Transform parent, string name)
        {
            var card    = MakeGO(name, parent);
            var cardImg = card.AddComponent<Image>();
            cardImg.sprite        = _sprRound8;
            cardImg.type          = Image.Type.Sliced;
            cardImg.color         = BgCard;
            cardImg.raycastTarget = false;

            var vlg = card.AddComponent<VerticalLayoutGroup>();
            vlg.padding               = new RectOffset(14, 12, 10, 10);
            vlg.spacing               = 5;
            vlg.childAlignment        = TextAnchor.UpperLeft;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth      = true;
            vlg.childControlHeight     = true;
            card.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return card;
        }

        private static GameObject MakeRow(Transform parent, string name, float height)
        {
            var row = MakeGO(name, parent);
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing               = 8;
            hlg.childAlignment        = TextAnchor.MiddleLeft;
            hlg.childForceExpandWidth  = false;
            hlg.childForceExpandHeight = true;
            hlg.childControlWidth      = true;
            hlg.childControlHeight     = true;
            row.AddComponent<LayoutElement>().preferredHeight = height;
            return row;
        }

        private void AddEmpty(Transform parent, string message)
        {
            var empty = AddTMP("Empty", parent, message, 13, FontStyles.Normal, TextSec);
            empty.alignment = TextAlignmentOptions.Center;
            empty.gameObject.AddComponent<LayoutElement>().preferredHeight = 70;
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
