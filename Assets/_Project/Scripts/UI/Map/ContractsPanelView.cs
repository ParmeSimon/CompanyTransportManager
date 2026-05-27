using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TransportManager.Core;
using TransportManager.Entities.Contracts;
using TransportManager.Entities.Vehicles;
using TransportManager.Enums;
using TransportManager.Events;
using TransportManager.Systems.Contracts;
using TransportManager.Systems.Map;

namespace TransportManager.UI.Map
{
    public class ContractsPanelView : MonoBehaviour
    {
        // ── Palette (fond identique au Header #2C3038) ────────────────────────────
        private static readonly Color32 HeaderColor = new Color32(0x2C, 0x30, 0x38, 255);   // identique au header
        private static readonly Color BgDeep     = new Color(0x2C / 255f, 0x30 / 255f, 0x38 / 255f, 1f);
        private static readonly Color BgCard     = new Color(1f, 1f, 1f, 0.07f);            // transparent sur BgDeep
        private static readonly Color BgElevated = new Color(0x34 / 255f, 0x38 / 255f, 0x42 / 255f, 1f); // légèrement plus clair
        private static readonly Color BgSubtle   = new Color(1f, 1f, 1f, 0.04f);
        private static readonly Color BorderFaint= new Color(1f, 1f, 1f, 0.08f);
        private static readonly Color TextPrime  = Color.white;
        private static readonly Color TextSecond = new Color(0.60f, 0.65f, 0.72f, 1f);
        private static readonly Color TextDim    = new Color(0.40f, 0.44f, 0.52f, 1f);
        private static readonly Color AccentBlue = new Color(0.22f, 0.52f, 1.00f, 1f);
        private static readonly Color AccentGreen= new Color(0.14f, 0.80f, 0.44f, 1f);
        private static readonly Color AccentAmber= new Color(0.97f, 0.65f, 0.14f, 1f);
        private static readonly Color DangerRed  = new Color(0.95f, 0.28f, 0.28f, 1f);
        private static readonly Color ScrimColor = new Color(0f, 0f, 0f, 0.88f);

        private static readonly Color[] DiffColors =
        {
            new Color(0.14f, 0.80f, 0.44f, 1f), // Easy    – vert
            new Color(0.97f, 0.65f, 0.14f, 1f), // Medium  – ambre
            new Color(0.95f, 0.28f, 0.28f, 1f), // Hard    – rouge
            new Color(0.62f, 0.36f, 0.96f, 1f), // Premium – violet
        };
        private static readonly string[] DiffNames = { "FACILE", "MOYEN", "DIFFICILE", "PREMIUM" };

        private const float PanelWidth   = 218f;
        private const float Margin      = 10f;
        private const float TopOffset   = 150f;

        // ── State ─────────────────────────────────────────────────────────────────
        private Transform  _activeRows;
        private Transform  _availableRows;
        private GameObject _popup;
        private Coroutine  _tickCoroutine;

        private ContractData          _popupDef;
        private ContractInstance      _popupInst;
        private List<VehicleInstance> _selectableVehicles = new List<VehicleInstance>();
        private int                   _selectedVehicleIdx = -1;
        private List<Image>           _vehicleRowBgs      = new List<Image>();
        private Button                _startBtn;

        // Generation modal state
        private GameObject            _genModal;
        private TMP_Text[]            _genRouteLabels = new TMP_Text[3];
        private TMP_Text[]            _genInfoLabels  = new TMP_Text[3];
        private Button[]              _genTakeBtns    = new Button[3];
        private ContractData[]        _genResults     = new ContractData[3];
        private ContractDifficulty[]  _sessionDiffs   = new ContractDifficulty[3];
        private VehicleRoutingProfile _sessionProfile;
        private int                   _genSessionId;

        // ── Lifecycle ─────────────────────────────────────────────────────────────
        private void OnEnable()
        {
            GameEvents.OnContractStarted   += _ => Refresh();
            GameEvents.OnContractCompleted += _ => Refresh();
            _tickCoroutine = StartCoroutine(TickEvery10s());
        }

        private void OnDisable()
        {
            GameEvents.OnContractStarted   -= _ => Refresh();
            GameEvents.OnContractCompleted -= _ => Refresh();
            if (_tickCoroutine != null) StopCoroutine(_tickCoroutine);
            CloseGenModal();
        }

        // ── Build ─────────────────────────────────────────────────────────────────
        public void Build()
        {
            var rt = GetComponent<RectTransform>();
            rt.anchorMin        = new Vector2(1f, 1f);
            rt.anchorMax        = new Vector2(1f, 1f);
            rt.pivot            = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-Margin, -(TopOffset));
            rt.sizeDelta        = new Vector2(PanelWidth, 0f);

            var bg = gameObject.AddComponent<Image>();
            bg.color         = BgDeep;
            bg.raycastTarget = true;

            gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var vlg = gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding               = new RectOffset(0, 0, 0, 12);
            vlg.spacing               = 0f;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth      = true;
            vlg.childControlHeight     = false;
            vlg.childAlignment        = TextAnchor.UpperCenter;

            // 1 — Header
            var hdrGo = MakeGO("Header", transform);
            hdrGo.AddComponent<Image>().color = BgDeep;
            Le(hdrGo, h: 54f);
            var hdrHlg = hdrGo.AddComponent<HorizontalLayoutGroup>();
            hdrHlg.padding               = new RectOffset(16, 12, 0, 0);
            hdrHlg.spacing               = 8f;
            hdrHlg.childAlignment        = TextAnchor.MiddleLeft;
            hdrHlg.childForceExpandWidth  = false;
            hdrHlg.childForceExpandHeight = false;
            hdrHlg.childControlWidth      = true;
            hdrHlg.childControlHeight     = true;

            var titleStack = MakeGO("TitleStack", hdrGo.transform);
            var tsVlg = titleStack.AddComponent<VerticalLayoutGroup>();
            tsVlg.childForceExpandWidth  = true;
            tsVlg.childForceExpandHeight = false;
            tsVlg.childControlWidth      = true;
            tsVlg.childControlHeight     = false;
            tsVlg.spacing = 2f;
            Le(titleStack, flexW: true, h: 54f);

            var eyebrow = MakeTMP("Eye", titleStack.transform, "TABLEAU DE BORD", 11f, FontStyles.Bold, TextDim);
            eyebrow.characterSpacing = 2.5f;
            Le(eyebrow.gameObject, h: 18f);

            var titleLbl = MakeTMP("Title", titleStack.transform, "Contrats", 20f, FontStyles.Bold, TextPrime);
            Le(titleLbl.gameObject, h: 28f);

            // 2 — TopLine sous le Header (10 px)
            var topLine = MakeGO("TopLine", transform);
            topLine.AddComponent<Image>().color = AccentBlue;
            Le(topLine, h: 10f);
            topLine.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 10f);

            // 3 — Sections (sans spacers ni separateurs)
            _activeRows    = BuildSection(transform, "EN COURS",   AccentBlue);
            _availableRows = BuildSection(transform, "EN ATTENTE", AccentAmber);

            // 4 — NewBtn tout en bas
            var newBtnGo  = MakeGO("NewBtn", transform);
            var newBtnImg = newBtnGo.AddComponent<Image>();
            newBtnImg.color = new Color(AccentGreen.r, AccentGreen.g, AccentGreen.b, 0.12f);
            Le(newBtnGo, h: 38f);
            var newBtnComp = newBtnGo.AddComponent<Button>();
            newBtnComp.targetGraphic = newBtnImg;
            newBtnComp.transition    = Selectable.Transition.None;
            newBtnComp.onClick.AddListener(OpenGenerationModal);
            var newBtnLbl = MakeTMP("L", newBtnGo.transform, "+  Nouveau contrat", 13f, FontStyles.Bold, AccentGreen);
            newBtnLbl.characterSpacing = 0.5f;
            newBtnLbl.alignment = TextAlignmentOptions.Center;
            Stretch(newBtnLbl.GetComponent<RectTransform>());

            Refresh();
        }

        private Transform BuildSection(Transform parent, string title, Color accent)
        {
            // Label row (padding top intégré dans la hauteur)
            var hdrGo = MakeGO("SHdr_" + title, parent);
            hdrGo.AddComponent<Image>().color = BgDeep;
            Le(hdrGo, h: 28f);
            var hhlg = hdrGo.AddComponent<HorizontalLayoutGroup>();
            hhlg.padding               = new RectOffset(16, 16, 8, 0);
            hhlg.spacing               = 7f;
            hhlg.childAlignment        = TextAnchor.MiddleLeft;
            hhlg.childForceExpandWidth  = false;
            hhlg.childForceExpandHeight = false;
            hhlg.childControlWidth      = true;
            hhlg.childControlHeight     = true;

            var dot = MakeGO("Dot", hdrGo.transform);
            dot.AddComponent<Image>().color = accent;
            Le(dot, w: 5f, h: 5f);

            var sLbl = MakeTMP("Lbl", hdrGo.transform, title, 12f, FontStyles.Bold, TextDim);
            sLbl.characterSpacing = 2f;
            Le(sLbl.gameObject, flexW: true, h: 20f);

            // Cards container — hauteur fixée par les cartes enfants
            var cGo = MakeGO("Cards_" + title, parent);
            var cvlg = cGo.AddComponent<VerticalLayoutGroup>();
            cvlg.spacing               = 1f;
            cvlg.childForceExpandWidth  = true;
            cvlg.childForceExpandHeight = false;
            cvlg.childControlWidth      = true;
            cvlg.childControlHeight     = false;
            cGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            return cGo.transform;
        }

        // ── Refresh ───────────────────────────────────────────────────────────────
        public void Refresh()
        {
            if (_activeRows == null) return;
            ClearChildren(_activeRows);
            ClearChildren(_availableRows);

            var contracts = ServiceLocator.Get<ContractSystem>();
            if (contracts == null) return;

            int activeCount = 0;
            foreach (var inst in contracts.Active)
                if (inst.status == ContractStatus.InProgress)
                { BuildActiveRow(inst); activeCount++; }

            if (activeCount == 0)
                EmptyRow(_activeRows, "Aucun trajet en cours");

            int availCount = 0;
            foreach (var def in contracts.Available)
            { BuildAvailableRow(def); availCount++; }

            if (availCount == 0)
                EmptyRow(_availableRows, "Aucun contrat disponible");
        }

        private void BuildActiveRow(ContractInstance inst)
        {
            var def = inst.definition;
            if (def == null) return;

            var cardGo  = MakeGO("ActiveCard", _activeRows);
            var cardImg = cardGo.AddComponent<Image>();
            cardImg.color = BgCard;
            Le(cardGo, h: 80f);

            var cardBtn = cardGo.AddComponent<Button>();
            cardBtn.targetGraphic = cardImg;
            cardBtn.transition    = Selectable.Transition.None;
            cardBtn.onClick.AddListener(() => OpenPopup(null, inst));

            // Left accent bar — hors layout
            var bar = MakeGO("Bar", cardGo.transform);
            bar.AddComponent<Image>().color = AccentBlue;
            bar.AddComponent<LayoutElement>().ignoreLayout = true;
            var bRt = bar.GetComponent<RectTransform>();
            bRt.anchorMin        = new Vector2(0f, 0.1f);
            bRt.anchorMax        = new Vector2(0f, 0.9f);
            bRt.pivot            = new Vector2(0f, 0.5f);
            bRt.sizeDelta        = new Vector2(3f, 0f);
            bRt.anchoredPosition = new Vector2(0f, 0f);

            // Content centré verticalement
            var content = MakeGO("Content", cardGo.transform);
            var cVlg = content.AddComponent<VerticalLayoutGroup>();
            cVlg.padding               = new RectOffset(18, 12, 17, 17); // (80 - 24 - 6 - 13) / 2 ≈ 18.5
            cVlg.spacing               = 6f;
            cVlg.childAlignment        = TextAnchor.MiddleLeft;
            cVlg.childForceExpandWidth  = true;
            cVlg.childForceExpandHeight = false;
            cVlg.childControlWidth      = true;
            cVlg.childControlHeight     = false;
            var cRt = content.GetComponent<RectTransform>();
            cRt.anchorMin = Vector2.zero;
            cRt.anchorMax = Vector2.one;
            cRt.offsetMin = cRt.offsetMax = Vector2.zero;

            // Row 1 : route + time pill
            var topRow = HRow(content.transform, 24f);
            var routeLbl = MakeTMP("R", topRow,
                $"{CityName(def.originCityId)}  →  {CityName(def.destinationCityId)}",
                14f, FontStyles.Bold, TextPrime);
            routeLbl.textWrappingMode = TextWrappingModes.NoWrap;
            routeLbl.overflowMode     = TextOverflowModes.Ellipsis;
            Le(routeLbl.gameObject, flexW: true, h: 24f);
            Pill(topRow, FormatRemaining(inst), AccentBlue, 56f);

            // Row 2 : meta + reward
            var botRow = HRow(content.transform, 18f);
            var distLbl = MakeTMP("D", botRow,
                $"{def.distanceKm:F0} km  ·  {def.requiredCapacity} t",
                12f, FontStyles.Normal, TextSecond);
            distLbl.textWrappingMode = TextWrappingModes.NoWrap;
            Le(distLbl.gameObject, flexW: true, h: 18f);
            var rewLbl = MakeTMP("$", botRow, $"+{def.baseReward:N0}$", 13f, FontStyles.Bold, AccentGreen);
            rewLbl.textWrappingMode = TextWrappingModes.NoWrap;
            rewLbl.alignment = TextAlignmentOptions.MidlineRight;
            Le(rewLbl.gameObject, w: 56f, h: 18f);
        }

        private void BuildAvailableRow(ContractData def)
        {
            var accentColor = DiffColors[(int)def.difficulty];

            var cardGo  = MakeGO("AvailCard", _availableRows);
            var cardImg = cardGo.AddComponent<Image>();
            cardImg.color = BgCard;
            Le(cardGo, h: 80f);

            var cardBtn = cardGo.AddComponent<Button>();
            cardBtn.targetGraphic = cardImg;
            cardBtn.transition    = Selectable.Transition.None;
            cardBtn.onClick.AddListener(() => OpenPopup(def, null));

            // Left accent bar — hors layout
            var bar = MakeGO("Bar", cardGo.transform);
            bar.AddComponent<Image>().color = accentColor;
            bar.AddComponent<LayoutElement>().ignoreLayout = true;
            var bRt = bar.GetComponent<RectTransform>();
            bRt.anchorMin        = new Vector2(0f, 0.1f);
            bRt.anchorMax        = new Vector2(0f, 0.9f);
            bRt.pivot            = new Vector2(0f, 0.5f);
            bRt.sizeDelta        = new Vector2(3f, 0f);
            bRt.anchoredPosition = new Vector2(0f, 0f);

            // Content centré verticalement
            var content = MakeGO("Content", cardGo.transform);
            var cVlg = content.AddComponent<VerticalLayoutGroup>();
            cVlg.padding               = new RectOffset(18, 12, 17, 17);
            cVlg.spacing               = 6f;
            cVlg.childAlignment        = TextAnchor.MiddleLeft;
            cVlg.childForceExpandWidth  = true;
            cVlg.childForceExpandHeight = false;
            cVlg.childControlWidth      = true;
            cVlg.childControlHeight     = false;
            var cRt = content.GetComponent<RectTransform>();
            cRt.anchorMin = Vector2.zero;
            cRt.anchorMax = Vector2.one;
            cRt.offsetMin = cRt.offsetMax = Vector2.zero;

            // Row 1 : route + difficulty pill
            var topRow = HRow(content.transform, 24f);
            var routeLbl = MakeTMP("R", topRow,
                $"{CityName(def.originCityId)}  →  {CityName(def.destinationCityId)}",
                14f, FontStyles.Bold, TextPrime);
            routeLbl.textWrappingMode = TextWrappingModes.NoWrap;
            routeLbl.overflowMode     = TextOverflowModes.Ellipsis;
            Le(routeLbl.gameObject, flexW: true, h: 24f);
            Pill(topRow, DiffNames[(int)def.difficulty], accentColor, 56f);

            // Row 2 : meta + reward
            var botRow = HRow(content.transform, 18f);
            var distLbl = MakeTMP("D", botRow,
                $"{def.distanceKm:F0} km  ·  {def.requiredCapacity} t",
                12f, FontStyles.Normal, TextSecond);
            distLbl.textWrappingMode = TextWrappingModes.NoWrap;
            Le(distLbl.gameObject, flexW: true, h: 18f);
            var rewLbl = MakeTMP("$", botRow, $"+{def.baseReward:N0}$", 13f, FontStyles.Bold, AccentGreen);
            rewLbl.textWrappingMode = TextWrappingModes.NoWrap;
            rewLbl.alignment = TextAlignmentOptions.MidlineRight;
            Le(rewLbl.gameObject, w: 56f, h: 18f);
        }

        private void EmptyRow(Transform parent, string msg)
        {
            var go = MakeGO("Empty", parent);
            go.AddComponent<Image>().color = new Color(0, 0, 0, 0);
            Le(go, h: 38f);
            var lbl = MakeTMP("Lbl", go.transform, msg, 12f, FontStyles.Italic, TextDim);
            lbl.alignment = TextAlignmentOptions.MidlineLeft;
            Stretch(lbl.GetComponent<RectTransform>(), new Vector2(16f, 0f), Vector2.zero);
        }

        // ── Popup ─────────────────────────────────────────────────────────────────
        private void OpenPopup(ContractData def, ContractInstance inst)
        {
            ClosePopup();
            _popupDef  = def ?? inst?.definition;
            _popupInst = inst;
            _selectedVehicleIdx = -1;
            _vehicleRowBgs.Clear();
            _selectableVehicles.Clear();

            bool isActive = inst != null;

            _popup = new GameObject("ContractPopup", typeof(RectTransform));
            _popup.transform.SetParent(transform.parent, false);
            var canvas = _popup.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            var scaler = _popup.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight  = 0.5f;
            _popup.AddComponent<GraphicRaycaster>();
            Stretch(_popup.GetComponent<RectTransform>());

            // Scrim
            var scrimGo  = MakeGO("Scrim", _popup.transform);
            scrimGo.AddComponent<Image>().color = ScrimColor;
            Stretch(scrimGo.GetComponent<RectTransform>());
            var scrimBtn = scrimGo.AddComponent<Button>();
            scrimBtn.transition = Selectable.Transition.None;
            scrimBtn.onClick.AddListener(ClosePopup);

            // Card
            var cardGo  = MakeGO("Card", _popup.transform);
            cardGo.AddComponent<Image>().color = BgDeep;
            var cardRt = cardGo.GetComponent<RectTransform>();
            cardRt.anchorMin = new Vector2(0.5f, 0.5f);
            cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardRt.pivot     = new Vector2(0.5f, 0.5f);
            cardRt.sizeDelta = new Vector2(420f, 0f);

            var cardVlg = cardGo.AddComponent<VerticalLayoutGroup>();
            cardVlg.padding               = new RectOffset(0, 0, 0, 0);
            cardVlg.spacing               = 0f;
            cardVlg.childAlignment        = TextAnchor.UpperCenter;
            cardVlg.childForceExpandWidth  = true;
            cardVlg.childForceExpandHeight = false;
            cardVlg.childControlWidth      = true;
            cardVlg.childControlHeight     = false;
            cardGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var d = _popupDef;
            var accentColor = isActive ? AccentBlue : DiffColors[(int)d.difficulty];

            // Top accent strip
            var strip = MakeGO("Strip", cardGo.transform);
            strip.AddComponent<Image>().color = accentColor;
            Le(strip, h: 3f);

            // Card header row
            var cHdrGo = MakeGO("CHdr", cardGo.transform);
            cHdrGo.AddComponent<Image>().color = BgElevated;
            Le(cHdrGo, h: 48f);
            var cHhlg = cHdrGo.AddComponent<HorizontalLayoutGroup>();
            cHhlg.padding               = new RectOffset(20, 16, 0, 0);
            cHhlg.spacing               = 10f;
            cHhlg.childAlignment        = TextAnchor.MiddleLeft;
            cHhlg.childForceExpandWidth  = false;
            cHhlg.childForceExpandHeight = false;
            cHhlg.childControlWidth      = true;
            cHhlg.childControlHeight     = true;

            var statusLbl = MakeTMP("Status", cHdrGo.transform,
                isActive ? "CONTRAT EN COURS" : "CONTRAT DISPONIBLE",
                8.5f, FontStyles.Bold, TextDim);
            statusLbl.characterSpacing = 2f;
            Le(statusLbl.gameObject, flexW: true, h: 48f);

            if (!isActive)
            {
                var diffPill = MakeGO("DiffPill", cHdrGo.transform);
                diffPill.AddComponent<Image>().color = new Color(accentColor.r, accentColor.g, accentColor.b, 0.15f);
                Le(diffPill, w: 64f, h: 22f);
                var dLbl = MakeTMP("D", diffPill.transform, DiffNames[(int)d.difficulty],
                    8f, FontStyles.Bold, accentColor);
                dLbl.characterSpacing = 0.5f;
                dLbl.alignment = TextAlignmentOptions.Center;
                Stretch(dLbl.GetComponent<RectTransform>());
            }

            var closeX = LinkBtn(cHdrGo.transform, "✕", TextSecond);
            Le(closeX.gameObject, w: 28f, h: 48f);
            closeX.onClick.AddListener(ClosePopup);

            // Route hero block
            var routeBlock = MakeGO("RouteBlock", cardGo.transform);
            routeBlock.AddComponent<Image>().color = BgDeep;
            var rbVlg = routeBlock.AddComponent<VerticalLayoutGroup>();
            rbVlg.padding               = new RectOffset(20, 20, 18, 18);
            rbVlg.spacing               = 4f;
            rbVlg.childForceExpandWidth  = true;
            rbVlg.childForceExpandHeight = false;
            rbVlg.childControlWidth      = true;
            rbVlg.childControlHeight     = false;
            routeBlock.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var originLbl = MakeTMP("From", routeBlock.transform,
                CityName(d.originCityId), 20f, FontStyles.Bold, TextPrime);
            originLbl.alignment = TextAlignmentOptions.Left;
            Le(originLbl.gameObject, h: 28f);

            var arrowRow = HRow(routeBlock.transform, 14f);
            var arrowLine = MakeGO("Line", arrowRow);
            arrowLine.AddComponent<Image>().color = new Color(accentColor.r, accentColor.g, accentColor.b, 0.4f);
            Le(arrowLine, h: 1f, flexW: true);
            var arrowLbl = MakeTMP("Arrow", arrowRow, " → ", 10f, FontStyles.Normal, accentColor);
            arrowLbl.textWrappingMode = TextWrappingModes.NoWrap;
            Le(arrowLbl.gameObject, h: 14f);
            var arrowLine2 = MakeGO("Line2", arrowRow);
            arrowLine2.AddComponent<Image>().color = new Color(accentColor.r, accentColor.g, accentColor.b, 0.4f);
            Le(arrowLine2, h: 1f, flexW: true);

            var destLbl = MakeTMP("To", routeBlock.transform,
                CityName(d.destinationCityId), 20f, FontStyles.Bold, TextPrime);
            destLbl.alignment = TextAlignmentOptions.Right;
            Le(destLbl.gameObject, h: 28f);

            // Stats grid
            var gridSep = MakeGO("GridSep", cardGo.transform);
            gridSep.AddComponent<Image>().color = BorderFaint;
            Le(gridSep, h: 1f);

            var gridGo = MakeGO("StatsGrid", cardGo.transform);
            gridGo.AddComponent<Image>().color = new Color(0,0,0,0);
            var gridHlg = gridGo.AddComponent<HorizontalLayoutGroup>();
            gridHlg.spacing               = 1f;
            gridHlg.childForceExpandWidth  = true;
            gridHlg.childForceExpandHeight = false;
            gridHlg.childControlWidth      = true;
            gridHlg.childControlHeight     = false;
            gridGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            StatCell(gridGo.transform, $"{d.distanceKm:F0} km", "DISTANCE");
            StatCell(gridGo.transform, $"+{d.baseReward:N0} $", "RÉCOMPENSE", AccentGreen);
            StatCell(gridGo.transform, $"{d.requiredCapacity} t", "CAPACITÉ MIN");
            if (isActive)
                StatCell(gridGo.transform, FormatRemaining(_popupInst), "TEMPS RESTANT", AccentBlue);
            else
                StatCell(gridGo.transform, DiffNames[(int)d.difficulty], "DIFFICULTÉ", accentColor);

            // Vehicle picker
            if (!isActive)
            {
                var vSep = MakeGO("VSep", cardGo.transform);
                vSep.AddComponent<Image>().color = BorderFaint;
                Le(vSep, h: 1f);

                var pickBlock = MakeGO("Picker", cardGo.transform);
                pickBlock.AddComponent<Image>().color = BgDeep;
                var pbVlg = pickBlock.AddComponent<VerticalLayoutGroup>();
                pbVlg.padding               = new RectOffset(20, 20, 12, 12);
                pbVlg.spacing               = 6f;
                pbVlg.childForceExpandWidth  = true;
                pbVlg.childForceExpandHeight = false;
                pbVlg.childControlWidth      = true;
                pbVlg.childControlHeight     = false;
                pickBlock.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                var pickLbl = MakeTMP("Lbl", pickBlock.transform, "AFFECTER UN CAMION",
                    7.5f, FontStyles.Bold, TextDim);
                pickLbl.characterSpacing = 2f;
                Le(pickLbl.gameObject, h: 16f);

                BuildVehiclePicker(pickBlock.transform, d);
            }

            // Route button
            var rSep = MakeGO("RSep", cardGo.transform);
            rSep.AddComponent<Image>().color = BorderFaint;
            Le(rSep, h: 1f);

            var routeBtnRow = MakeGO("RouteBtnRow", cardGo.transform);
            routeBtnRow.AddComponent<Image>().color = new Color(0, 0, 0, 0);
            Le(routeBtnRow, h: 44f);
            var rHlg = routeBtnRow.AddComponent<HorizontalLayoutGroup>();
            rHlg.padding               = new RectOffset(16, 16, 8, 8);
            rHlg.childAlignment        = TextAnchor.MiddleCenter;
            rHlg.childForceExpandWidth  = false;
            rHlg.childForceExpandHeight = false;
            rHlg.childControlWidth      = true;
            rHlg.childControlHeight     = true;

            var routeMapBtn = SolidBtn(routeBtnRow.transform, "Voir la route sur la carte", AccentBlue);
            Le(routeMapBtn.gameObject, flexW: true, h: 28f);
            var captureDef = _popupDef;
            routeMapBtn.onClick.AddListener(() => { ClosePopup(); GameEvents.RaiseShowContractRoute(captureDef); });

            // Bottom actions
            var btnSep = MakeGO("BtnSep", cardGo.transform);
            btnSep.AddComponent<Image>().color = BorderFaint;
            Le(btnSep, h: 1f);

            var btnRow = MakeGO("BtnRow", cardGo.transform);
            btnRow.AddComponent<Image>().color = BgElevated;
            Le(btnRow, h: 54f);
            var bHlg = btnRow.AddComponent<HorizontalLayoutGroup>();
            bHlg.padding               = new RectOffset(16, 16, 10, 10);
            bHlg.spacing               = 8f;
            bHlg.childAlignment        = TextAnchor.MiddleCenter;
            bHlg.childForceExpandWidth  = false;
            bHlg.childForceExpandHeight = false;
            bHlg.childControlWidth      = true;
            bHlg.childControlHeight     = true;

            var closeBtn = SolidBtn(btnRow.transform, "Fermer", BgElevated);
            Le(closeBtn.gameObject, flexW: true, h: 34f);
            closeBtn.onClick.AddListener(ClosePopup);

            if (!isActive)
            {
                _startBtn = SolidBtn(btnRow.transform, "Démarrer  →", AccentBlue);
                Le(_startBtn.gameObject, flexW: true, h: 34f);
                _startBtn.onClick.AddListener(OnStartContract);
                _startBtn.interactable = false;
            }
        }

        private void BuildVehiclePicker(Transform parent, ContractData def)
        {
            var catalog = ServiceLocator.Get<VehicleCatalog>();
            var gm      = GameManager.Instance;
            if (gm?.Save == null) return;

            foreach (var v in gm.Save.vehicles)
            {
                if (v.status != VehicleStatus.Idle) continue;
                if (string.IsNullOrEmpty(v.assignedDriverInstanceId)) continue;
                var data = catalog?.GetById(v.vehicleDataId);
                if (data == null || data.capacity < def.requiredCapacity) continue;
                _selectableVehicles.Add(v);
            }

            if (_selectableVehicles.Count == 0)
            {
                var noLbl = MakeTMP("No", parent,
                    "Aucun camion disponible avec conducteur et capacité suffisante.",
                    9.5f, FontStyles.Italic, DangerRed);
                noLbl.textWrappingMode = TextWrappingModes.Normal;
                Le(noLbl.gameObject, h: 36f);
                return;
            }

            for (int i = 0; i < _selectableVehicles.Count; i++)
            {
                int idx  = i;
                var v    = _selectableVehicles[i];
                var data = catalog?.GetById(v.vehicleDataId);

                var rowGo  = MakeGO("VRow", parent);
                var rowImg = rowGo.AddComponent<Image>();
                rowImg.color = BgElevated;
                _vehicleRowBgs.Add(rowImg);
                Le(rowGo, h: 38f);

                var rowBtn = rowGo.AddComponent<Button>();
                rowBtn.targetGraphic = rowImg;
                rowBtn.transition    = Selectable.Transition.None;
                rowBtn.onClick.AddListener(() => SelectVehicle(idx));

                var rowHlg = rowGo.AddComponent<HorizontalLayoutGroup>();
                rowHlg.padding               = new RectOffset(14, 14, 0, 0);
                rowHlg.spacing               = 8f;
                rowHlg.childAlignment        = TextAnchor.MiddleLeft;
                rowHlg.childForceExpandWidth  = false;
                rowHlg.childForceExpandHeight = false;
                rowHlg.childControlWidth      = true;
                rowHlg.childControlHeight     = true;

                // Dot indicator
                var dot = MakeGO("Dot", rowGo.transform);
                dot.AddComponent<Image>().color = BorderFaint;
                Le(dot, w: 6f, h: 6f);

                var nameLbl = MakeTMP("N", rowGo.transform,
                    data?.displayName ?? v.vehicleDataId, 11f, FontStyles.Bold, TextPrime);
                nameLbl.textWrappingMode = TextWrappingModes.NoWrap;
                Le(nameLbl.gameObject, flexW: true, h: 38f);

                var capLbl = MakeTMP("C", rowGo.transform,
                    $"{data?.capacity ?? 0} t", 10f, FontStyles.Bold, TextSecond);
                capLbl.alignment = TextAlignmentOptions.MidlineRight;
                Le(capLbl.gameObject, w: 36f, h: 38f);
            }
        }

        private void SelectVehicle(int idx)
        {
            _selectedVehicleIdx = idx;
            for (int i = 0; i < _vehicleRowBgs.Count; i++)
                _vehicleRowBgs[i].color = i == idx
                    ? new Color(AccentBlue.r, AccentBlue.g, AccentBlue.b, 0.18f)
                    : BgElevated;
            if (_startBtn != null) _startBtn.interactable = true;
        }

        private void OnStartContract()
        {
            if (_popupDef == null || _selectedVehicleIdx < 0 || _selectedVehicleIdx >= _selectableVehicles.Count) return;
            var catalog   = ServiceLocator.Get<VehicleCatalog>();
            var contracts = ServiceLocator.Get<ContractSystem>();
            var gm        = GameManager.Instance;
            if (catalog == null || contracts == null || gm == null) return;

            var vInst = _selectableVehicles[_selectedVehicleIdx];
            var vData = catalog.GetById(vInst.vehicleDataId);
            if (vData == null) return;

            var result = contracts.StartContract(_popupDef, vInst, vData);
            if (result != null) { gm.SaveNow(); GameEvents.RaiseContractStarted(result); }
            ClosePopup();
        }

        private void ClosePopup()
        {
            if (_popup != null) { Destroy(_popup); _popup = null; }
        }

        // ── Generation Modal ──────────────────────────────────────────────────────
        private void OpenGenerationModal()
        {
            ClosePopup();
            CloseGenModal();

            var fleet = CollectAvailableFleet(out _sessionProfile);
            _sessionDiffs = SelectDifficultiesForFleet(fleet);

            _genResults     = new ContractData[3];
            _genRouteLabels = new TMP_Text[3];
            _genInfoLabels  = new TMP_Text[3];
            _genTakeBtns    = new Button[3];
            _genSessionId++;

            _genModal = new GameObject("GenModal", typeof(RectTransform));
            _genModal.transform.SetParent(transform.parent, false);
            var canvas = _genModal.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            var scaler = _genModal.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight  = 0.5f;
            _genModal.AddComponent<GraphicRaycaster>();
            Stretch(_genModal.GetComponent<RectTransform>());

            // Scrim
            var scrimGo = MakeGO("Scrim", _genModal.transform);
            scrimGo.AddComponent<Image>().color = ScrimColor;
            Stretch(scrimGo.GetComponent<RectTransform>());
            var scrimBtn = scrimGo.AddComponent<Button>();
            scrimBtn.transition = Selectable.Transition.None;
            scrimBtn.onClick.AddListener(CloseGenModal);

            // Card
            var cardGo  = MakeGO("Card", _genModal.transform);
            cardGo.AddComponent<Image>().color = BgDeep;
            var cardRt = cardGo.GetComponent<RectTransform>();
            cardRt.anchorMin = new Vector2(0.5f, 0.5f);
            cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardRt.pivot     = new Vector2(0.5f, 0.5f);
            cardRt.sizeDelta = new Vector2(420f, 0f);
            var cardVlg = cardGo.AddComponent<VerticalLayoutGroup>();
            cardVlg.padding               = new RectOffset(0, 0, 0, 0);
            cardVlg.spacing               = 0f;
            cardVlg.childAlignment        = TextAnchor.UpperCenter;
            cardVlg.childForceExpandWidth  = true;
            cardVlg.childForceExpandHeight = false;
            cardVlg.childControlWidth      = true;
            cardVlg.childControlHeight     = false;
            cardGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Top accent strip
            var topStrip = MakeGO("Strip", cardGo.transform);
            topStrip.AddComponent<Image>().color = AccentGreen;
            Le(topStrip, h: 3f);

            // Header
            var cHdrGo = MakeGO("CHdr", cardGo.transform);
            cHdrGo.AddComponent<Image>().color = BgElevated;
            Le(cHdrGo, h: 52f);
            var cHhlg = cHdrGo.AddComponent<HorizontalLayoutGroup>();
            cHhlg.padding               = new RectOffset(20, 16, 0, 0);
            cHhlg.spacing               = 10f;
            cHhlg.childAlignment        = TextAnchor.MiddleLeft;
            cHhlg.childForceExpandWidth  = false;
            cHhlg.childForceExpandHeight = false;
            cHhlg.childControlWidth      = true;
            cHhlg.childControlHeight     = true;

            var titleStack = MakeGO("TS", cHdrGo.transform);
            var tsVlg = titleStack.AddComponent<VerticalLayoutGroup>();
            tsVlg.childForceExpandWidth  = true;
            tsVlg.childForceExpandHeight = false;
            tsVlg.childControlWidth      = true;
            tsVlg.childControlHeight     = false;
            tsVlg.spacing = 2f;
            Le(titleStack, flexW: true, h: 52f);

            var eyebrowLbl = MakeTMP("Eye", titleStack.transform, "GÉNÉRER", 7.5f, FontStyles.Bold, TextDim);
            eyebrowLbl.characterSpacing = 2.5f;
            Le(eyebrowLbl.gameObject, h: 14f);

            var titleLbl2 = MakeTMP("T", titleStack.transform, "Nouveaux contrats", 13f, FontStyles.Bold, TextPrime);
            Le(titleLbl2.gameObject, h: 20f);

            var closeXBtn = LinkBtn(cHdrGo.transform, "✕", TextSecond);
            Le(closeXBtn.gameObject, w: 32f, h: 52f);
            closeXBtn.onClick.AddListener(CloseGenModal);

            // Slots
            var slotSep = MakeGO("SSep", cardGo.transform);
            slotSep.AddComponent<Image>().color = BorderFaint;
            Le(slotSep, h: 1f);

            for (int i = 0; i < 3; i++)
            {
                BuildGenSlot(cardGo.transform, i);
                if (i < 2)
                {
                    var sep2 = MakeGO("Sep", cardGo.transform);
                    sep2.AddComponent<Image>().color = BorderFaint;
                    Le(sep2, h: 1f);
                }
            }

            var padGo = MakeGO("Pad", cardGo.transform);
            Le(padGo, h: 10f);

            int sid = _genSessionId;
            for (int i = 0; i < 3; i++)
                GenerateSingleAsync(i, sid);
        }

        private void BuildGenSlot(Transform parent, int slotIdx)
        {
            var diff   = _sessionDiffs[slotIdx];
            var accent = DiffColors[(int)diff];
            var label  = DiffNames[(int)diff];

            var slotGo = MakeGO("Slot" + slotIdx, parent);
            slotGo.AddComponent<Image>().color = BgDeep;
            var vlg = slotGo.AddComponent<VerticalLayoutGroup>();
            vlg.padding               = new RectOffset(20, 20, 14, 14);
            vlg.spacing               = 6f;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth      = true;
            vlg.childControlHeight     = false;
            slotGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Difficulty badge row
            var badgeRow = HRow(slotGo.transform, 18f);
            var diffPill = MakeGO("Pill", badgeRow);
            diffPill.AddComponent<Image>().color = new Color(accent.r, accent.g, accent.b, 0.14f);
            Le(diffPill, w: 60f, h: 16f);
            var diffTxt = MakeTMP("D", diffPill.transform, label, 7.5f, FontStyles.Bold, accent);
            diffTxt.characterSpacing = 0.5f;
            diffTxt.alignment = TextAlignmentOptions.Center;
            Stretch(diffTxt.GetComponent<RectTransform>());
            Le(badgeRow.gameObject, h: 18f);

            // Route label
            var routeLbl = MakeTMP("Route", slotGo.transform, "Génération en cours…",
                11.5f, FontStyles.Normal, TextSecond);
            routeLbl.textWrappingMode = TextWrappingModes.NoWrap;
            routeLbl.overflowMode     = TextOverflowModes.Ellipsis;
            routeLbl.fontStyle        = FontStyles.Italic;
            Le(routeLbl.gameObject, h: 18f);
            _genRouteLabels[slotIdx] = routeLbl;

            // Info row + take button
            var infoRow = HRow(slotGo.transform, 22f);
            var infoLbl = MakeTMP("I", infoRow, "", 9f, FontStyles.Normal, TextSecond);
            infoLbl.textWrappingMode = TextWrappingModes.NoWrap;
            Le(infoLbl.gameObject, flexW: true, h: 22f);
            _genInfoLabels[slotIdx] = infoLbl;

            int idx = slotIdx;
            var takeBtn = SolidBtn(infoRow, "Prendre  →", accent);
            Le(takeBtn.gameObject, w: 84f, h: 22f);
            takeBtn.interactable = false;
            takeBtn.onClick.AddListener(() => OnTakeGeneratedContract(idx));
            _genTakeBtns[slotIdx] = takeBtn;
        }

        private async void GenerateSingleAsync(int slotIdx, int sessionId)
        {
            var gen = ServiceLocator.Get<ContractGenerator>();
            if (gen == null) { MarkGenError(slotIdx); return; }

            var profile = _sessionProfile;
            var diff    = _sessionDiffs[slotIdx];
            var result  = await gen.GenerateAsync(profile, diff);

            if (_genModal == null || _genSessionId != sessionId) return;
            _genResults[slotIdx] = result;
            UpdateGenSlot(slotIdx, result);
        }

        private void UpdateGenSlot(int slotIdx, ContractData def)
        {
            if (_genRouteLabels[slotIdx] == null) return;
            if (def == null) { MarkGenError(slotIdx); return; }

            _genRouteLabels[slotIdx].text      = $"{CityName(def.originCityId)}  →  {CityName(def.destinationCityId)}";
            _genRouteLabels[slotIdx].color     = TextPrime;
            _genRouteLabels[slotIdx].fontStyle = FontStyles.Bold;
            _genInfoLabels[slotIdx].text       = $"{def.distanceKm:F0} km  ·  {def.requiredCapacity} t  ·  +{def.baseReward:N0} $";
            if (_genTakeBtns[slotIdx] != null) _genTakeBtns[slotIdx].interactable = true;
        }

        private void MarkGenError(int slotIdx)
        {
            if (_genRouteLabels[slotIdx] != null)
            {
                _genRouteLabels[slotIdx].text      = "Échec de la génération";
                _genRouteLabels[slotIdx].color     = DangerRed;
                _genRouteLabels[slotIdx].fontStyle = FontStyles.Italic;
            }
        }

        private void OnTakeGeneratedContract(int slotIdx)
        {
            var def = _genResults[slotIdx];
            if (def == null) return;

            ServiceLocator.Get<ContractSystem>()?.AddToPool(def);
            GameManager.Instance?.SaveNow();
            CloseGenModal();
            Refresh();
            OpenPopup(def, null);
        }

        private void CloseGenModal()
        {
            if (_genModal != null) { Destroy(_genModal); _genModal = null; }
        }

        // ── Fleet analysis ────────────────────────────────────────────────────────
        private static List<VehicleData> CollectAvailableFleet(out VehicleRoutingProfile profile)
        {
            var result = new List<VehicleData>();
            profile    = VehicleRoutingProfile.HeavyGoodsVehicle;

            var catalog = ServiceLocator.Get<VehicleCatalog>();
            var gm      = GameManager.Instance;
            if (catalog == null || gm?.Save == null) return result;

            bool anyHgv = false;
            foreach (var v in gm.Save.vehicles)
            {
                if (v.status != VehicleStatus.Idle) continue;
                if (string.IsNullOrEmpty(v.assignedDriverInstanceId)) continue;
                var data = catalog.GetById(v.vehicleDataId);
                if (data == null) continue;
                result.Add(data);
                if (data.routingProfile == VehicleRoutingProfile.HeavyGoodsVehicle) anyHgv = true;
            }

            profile = anyHgv ? VehicleRoutingProfile.HeavyGoodsVehicle : VehicleRoutingProfile.Car;
            return result;
        }

        private static ContractDifficulty[] SelectDifficultiesForFleet(List<VehicleData> fleet)
        {
            int maxCap = 0;
            foreach (var data in fleet)
                if (data.capacity > maxCap) maxCap = data.capacity;

            var d0 = ContractDifficulty.Easy;
            var d1 = maxCap >= 5  ? ContractDifficulty.Medium : ContractDifficulty.Easy;
            var d2 = maxCap >= 30 ? ContractDifficulty.Premium :
                     maxCap >= 15 ? ContractDifficulty.Hard    :
                     maxCap >=  5 ? ContractDifficulty.Medium  : ContractDifficulty.Easy;

            return new[] { d0, d1, d2 };
        }

        private IEnumerator TickEvery10s()
        {
            while (true) { yield return new WaitForSecondsRealtime(10f); Refresh(); }
        }

        // ── City helpers ──────────────────────────────────────────────────────────
        private static string CityName(string id)
        {
            var c = ServiceLocator.Get<MapSystem>()?.Catalog;
            return c?.GetById(id)?.displayName ?? id;
        }

        private static string FullCity(string id)
        {
            var c = ServiceLocator.Get<MapSystem>()?.Catalog;
            if (c == null) return id;
            var e = c.GetById(id);
            return e != null ? $"{e.displayName}, {e.country}" : id;
        }

        private static string FormatRemaining(ContractInstance inst)
        {
            var rem = inst.CompletionTimeUtc - DateTime.UtcNow;
            if (rem.TotalSeconds <= 0) return "Prêt !";
            if (rem.TotalHours >= 1)   return $"{(int)rem.TotalHours}h {rem.Minutes:D2}m";
            return $"{rem.Minutes}m {rem.Seconds:D2}s";
        }

        // ── UI primitives ─────────────────────────────────────────────────────────

        // Returns the VLG transform of a contract card (left border + padding)
        private static Transform ContractCard(Transform parent, Color accentColor)
        {
            var cardGo  = MakeGO("Card", parent);
            var cardImg = cardGo.AddComponent<Image>();
            cardImg.color = BgCard;

            var cvlg = cardGo.AddComponent<VerticalLayoutGroup>();
            cvlg.padding               = new RectOffset(16, 14, 10, 10);
            cvlg.spacing               = 5f;
            cvlg.childForceExpandWidth  = true;
            cvlg.childForceExpandHeight = false;
            cvlg.childControlWidth      = true;
            cvlg.childControlHeight     = false;
            Le(cardGo, h: 86f);

            // Left border strip
            var bar = MakeGO("Bar", cardGo.transform);
            bar.AddComponent<Image>().color = accentColor;
            var bRt = bar.GetComponent<RectTransform>();
            bRt.anchorMin        = new Vector2(0f, 0.12f);
            bRt.anchorMax        = new Vector2(0f, 0.88f);
            bRt.pivot            = new Vector2(0f, 0.5f);
            bRt.sizeDelta        = new Vector2(2f, 0f);
            bRt.anchoredPosition = new Vector2(0f, 0f);

            return cardGo.transform;
        }

        // Colored label pill (transparent bg + colored text)
        private static Button Pill(Transform parent, string text, Color color, float width)
        {
            var go  = MakeGO("Pill", parent);
            go.AddComponent<Image>().color = new Color(color.r, color.g, color.b, 0.14f);
            Le(go, w: width, h: 16f);
            var lbl = MakeTMP("L", go.transform, text, 11f, FontStyles.Bold, color);
            lbl.characterSpacing  = 0.3f;
            lbl.alignment         = TextAlignmentOptions.Center;
            lbl.textWrappingMode  = TextWrappingModes.NoWrap;
            Stretch(lbl.GetComponent<RectTransform>());
            var btn = go.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            return btn;
        }

        // Link-style button: transparent bg, colored text
        private static Button LinkBtn(Transform parent, string label, Color color)
        {
            var go  = MakeGO("LBtn", parent);
            go.AddComponent<Image>().color = new Color(0, 0, 0, 0);
            var btn = go.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            var lbl = MakeTMP("L", go.transform, label, 13f, FontStyles.Bold, color);
            lbl.alignment        = TextAlignmentOptions.MidlineRight;
            lbl.textWrappingMode = TextWrappingModes.NoWrap;
            Stretch(lbl.GetComponent<RectTransform>());
            return btn;
        }

        // Solid filled button
        private static Button SolidBtn(Transform parent, string label, Color color)
        {
            var go  = MakeGO("SBtn", parent);
            var img = go.AddComponent<Image>();
            img.color = color;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition    = Selectable.Transition.None;
            var lbl = MakeTMP("L", go.transform, label, 13f, FontStyles.Bold, Color.white);
            lbl.alignment = TextAlignmentOptions.Center;
            Stretch(lbl.GetComponent<RectTransform>());
            return btn;
        }

        private static void StatCell(Transform parent, string value, string key, Color? valueColor = null)
        {
            var cell = MakeGO("Cell", parent);
            cell.AddComponent<Image>().color = BgElevated;
            var cvlg = cell.AddComponent<VerticalLayoutGroup>();
            cvlg.padding               = new RectOffset(14, 14, 12, 12);
            cvlg.spacing               = 3f;
            cvlg.childForceExpandWidth  = true;
            cvlg.childForceExpandHeight = false;
            cvlg.childControlWidth      = true;
            cvlg.childControlHeight     = false;
            cell.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            Le(cell, flexW: true);

            var valLbl = MakeTMP("V", cell.transform, value, 14f, FontStyles.Bold,
                valueColor ?? TextPrime);
            valLbl.textWrappingMode = TextWrappingModes.NoWrap;
            valLbl.overflowMode     = TextOverflowModes.Ellipsis;
            Le(valLbl.gameObject, h: 20f);

            var keyLbl = MakeTMP("K", cell.transform, key, 7.5f, FontStyles.Bold, TextDim);
            keyLbl.characterSpacing = 1.5f;
            Le(keyLbl.gameObject, h: 12f);
        }

        private static Transform HRow(Transform parent, float h)
        {
            var go  = MakeGO("HR", parent);
            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment        = TextAnchor.MiddleLeft;
            hlg.spacing               = 6f;
            hlg.childForceExpandWidth  = false;
            hlg.childForceExpandHeight = false;
            hlg.childControlWidth      = true;
            hlg.childControlHeight     = true;
            Le(go, h: h);
            return go.transform;
        }

        private static GameObject MakeGO(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static TMP_Text MakeTMP(string name, Transform parent, string text,
            float size, FontStyles style, Color color)
        {
            var go  = MakeGO(name, parent);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text          = text;
            tmp.fontSize      = size;
            tmp.fontStyle     = style;
            tmp.color         = color;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static void Le(GameObject go, float w = -1f, float h = -1f, bool flexW = false)
        {
            var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            if (w >= 0f) { le.preferredWidth = w; le.minWidth = w; }
            if (h >= 0f) { le.preferredHeight = h; le.minHeight = h; }
            if (flexW)   le.flexibleWidth = 1f;
        }

        private static void Stretch(RectTransform rt, Vector2? min = null, Vector2? max = null)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = min ?? Vector2.zero;
            rt.offsetMax = max ?? Vector2.zero;
        }

        private static void ClearChildren(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
                Destroy(t.GetChild(i).gameObject);
        }
    }
}
