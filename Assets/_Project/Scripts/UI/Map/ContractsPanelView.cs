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

        private const float PanelWidth   = 256f;
        private const float Margin      = 12f;
        private const float TopOffset   = 150f;

        // ── State ─────────────────────────────────────────────────────────────────
        private Transform  _activeRows;
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
        private TMP_Text[]            _genRouteLabels  = new TMP_Text[3];
        private TMP_Text[]            _genCargoLabels  = new TMP_Text[3];
        private TMP_Text[]            _genInfoLabels   = new TMP_Text[3];
        private TMP_Text[]            _genRewardLabels = new TMP_Text[3];
        private Button[]              _genTakeBtns     = new Button[3];
        private TMP_Text[]            _genBtnLabels    = new TMP_Text[3];
        private Image[]               _genBtnImgs      = new Image[3];
        private ContractData[]        _genResults      = new ContractData[3];
        private ContractDifficulty[]  _sessionDiffs    = new ContractDifficulty[3];
        private VehicleRoutingProfile _sessionProfile;
        private float                 _sessionMaxRangeKm = float.MaxValue;
        private const float           FleetRangeSafetyFactor = 0.9f;   // marge 10% : routes > vol d'oiseau
        private int                   _genSessionId;

        // Rounded sprites (lazy, shared across the modal)
        private Sprite _sprR8, _sprR12, _sprR16;

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

            EnsureRoundedSprites();

            var bg = gameObject.AddComponent<Image>();
            bg.sprite        = _sprR16;
            bg.type          = Image.Type.Sliced;
            bg.color         = BgDeep;
            bg.raycastTarget = true;
            var panelShadow = gameObject.AddComponent<Shadow>();
            panelShadow.effectColor    = new Color(0f, 0f, 0f, 0.5f);
            panelShadow.effectDistance = new Vector2(-3f, -5f);

            gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var vlg = gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding               = new RectOffset(0, 0, 0, 6);
            vlg.spacing               = 0f;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth      = true;
            vlg.childControlHeight     = true;
            vlg.childAlignment        = TextAnchor.UpperCenter;

            // 1 — Header
            var hdrGo = MakeGO("Header", transform);
            var hdrBgImg = hdrGo.AddComponent<Image>();
            hdrBgImg.color         = new Color(0, 0, 0, 0);
            hdrBgImg.raycastTarget = false;
            Le(hdrGo, h: 64f);
            var hdrHlg = hdrGo.AddComponent<HorizontalLayoutGroup>();
            hdrHlg.padding               = new RectOffset(18, 16, 16, 6);
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
            tsVlg.childControlHeight     = true;
            tsVlg.childAlignment         = TextAnchor.MiddleLeft;
            tsVlg.spacing = 3f;
            Le(titleStack, flexW: true, h: 48f);

            var eyebrow = MakeTMP("Eye", titleStack.transform, "TABLEAU DE BORD", 9f, FontStyles.Bold, TextDim);
            eyebrow.characterSpacing = 3f;
            Le(eyebrow.gameObject, h: 13f);

            var titleLbl = MakeTMP("Title", titleStack.transform, "Contrats", 22f, FontStyles.Bold, TextPrime);
            Le(titleLbl.gameObject, h: 30f);

            // 2 — Hairline divider
            var divWrap = MakeGO("DivWrap", transform);
            var divWrapImg = divWrap.AddComponent<Image>();
            divWrapImg.color = new Color(0, 0, 0, 0);
            divWrapImg.raycastTarget = false;
            Le(divWrap, h: 1f);
            var divHlg = divWrap.AddComponent<HorizontalLayoutGroup>();
            divHlg.padding              = new RectOffset(18, 18, 0, 0);
            divHlg.childForceExpandWidth = true;
            divHlg.childControlWidth     = true;
            var divider = MakeGO("Divider", divWrap.transform);
            divider.AddComponent<Image>().color = BorderFaint;
            Le(divider, h: 1f);

            // 3 — Section (uniquement les contrats en cours)
            _activeRows = BuildSection(transform, "EN COURS", AccentBlue);

            // 4 — CTA "Nouveau contrat" (arrondi, marges latérales)
            var ctaWrap = MakeGO("CtaWrap", transform);
            var ctaWrapImg = ctaWrap.AddComponent<Image>();
            ctaWrapImg.color = new Color(0, 0, 0, 0);
            ctaWrapImg.raycastTarget = false;
            Le(ctaWrap, h: 58f);
            var ctaVlg = ctaWrap.AddComponent<VerticalLayoutGroup>();
            ctaVlg.padding               = new RectOffset(12, 12, 14, 0);
            ctaVlg.childForceExpandWidth  = true;
            ctaVlg.childForceExpandHeight = false;
            ctaVlg.childControlWidth      = true;
            ctaVlg.childControlHeight     = true;

            var newBtnGo  = MakeGO("NewBtn", ctaWrap.transform);
            var newBtnImg = newBtnGo.AddComponent<Image>();
            newBtnImg.sprite = _sprR12;
            newBtnImg.type   = Image.Type.Sliced;
            newBtnImg.color  = AccentGreen;
            Le(newBtnGo, h: 44f);
            var newBtnComp = newBtnGo.AddComponent<Button>();
            newBtnComp.targetGraphic = newBtnImg;
            newBtnComp.transition    = Selectable.Transition.None;
            newBtnComp.onClick.AddListener(OpenGenerationModal);
            var newBtnLbl = MakeTMP("L", newBtnGo.transform, "+  Nouveau contrat", 13.5f, FontStyles.Bold, Color.white);
            newBtnLbl.characterSpacing = 0.5f;
            newBtnLbl.alignment = TextAlignmentOptions.Center;
            Stretch(newBtnLbl.GetComponent<RectTransform>());

            Refresh();
        }

        private Transform BuildSection(Transform parent, string title, Color accent)
        {
            // Section label row
            var hdrGo = MakeGO("SHdr_" + title, parent);
            var shImg = hdrGo.AddComponent<Image>();
            shImg.color         = new Color(0, 0, 0, 0);
            shImg.raycastTarget = false;
            Le(hdrGo, h: 30f);
            var hhlg = hdrGo.AddComponent<HorizontalLayoutGroup>();
            hhlg.padding               = new RectOffset(18, 16, 12, 4);
            hhlg.spacing               = 7f;
            hhlg.childAlignment        = TextAnchor.MiddleLeft;
            hhlg.childForceExpandWidth  = false;
            hhlg.childForceExpandHeight = false;
            hhlg.childControlWidth      = true;
            hhlg.childControlHeight     = true;

            var dot = MakeGO("Dot", hdrGo.transform);
            var dotImg = dot.AddComponent<Image>();
            dotImg.sprite = _sprR8;
            dotImg.type   = Image.Type.Sliced;
            dotImg.color  = accent;
            Le(dot, w: 6f, h: 6f);

            var sLbl = MakeTMP("Lbl", hdrGo.transform, title, 11f, FontStyles.Bold, TextDim);
            sLbl.characterSpacing = 2.5f;
            Le(sLbl.gameObject, flexW: true, h: 16f);

            // Cards container — hauteur fixée par les cartes enfants
            var cGo = MakeGO("Cards_" + title, parent);
            var cImg = cGo.AddComponent<Image>();
            cImg.color         = new Color(0, 0, 0, 0);
            cImg.raycastTarget = false;
            var cvlg = cGo.AddComponent<VerticalLayoutGroup>();
            cvlg.padding               = new RectOffset(12, 12, 0, 0);
            cvlg.spacing               = 10f;
            cvlg.childForceExpandWidth  = true;
            cvlg.childForceExpandHeight = false;
            cvlg.childControlWidth      = true;
            cvlg.childControlHeight     = true;
            cGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            return cGo.transform;
        }

        // ── Refresh ───────────────────────────────────────────────────────────────
        public void Refresh()
        {
            if (_activeRows == null) return;
            ClearChildren(_activeRows);

            var contracts = ServiceLocator.Get<ContractSystem>();
            if (contracts == null) return;

            int activeCount = 0;
            foreach (var inst in contracts.Active)
                if (inst.status == ContractStatus.InProgress)
                { BuildActiveRow(inst); activeCount++; }

            if (activeCount == 0)
                EmptyRow(_activeRows, "Aucun trajet en cours");
        }

        private void BuildActiveRow(ContractInstance inst)
        {
            var def = inst.definition;
            if (def == null) return;

            // Rounded card + soft shadow
            var cardGo  = MakeGO("ActiveCard", _activeRows);
            var cardImg = cardGo.AddComponent<Image>();
            cardImg.sprite = _sprR12;
            cardImg.type   = Image.Type.Sliced;
            cardImg.color  = BgElevated;
            var cardSh = cardGo.AddComponent<Shadow>();
            cardSh.effectColor    = new Color(0f, 0f, 0f, 0.40f);
            cardSh.effectDistance = new Vector2(0f, -3f);

            var cardVlg = cardGo.AddComponent<VerticalLayoutGroup>();
            cardVlg.padding               = new RectOffset(14, 14, 13, 13);
            cardVlg.spacing               = 9f;
            cardVlg.childAlignment        = TextAnchor.UpperLeft;
            cardVlg.childForceExpandWidth  = true;
            cardVlg.childForceExpandHeight = false;
            cardVlg.childControlWidth      = true;
            cardVlg.childControlHeight     = true;
            cardGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var cardBtn = cardGo.AddComponent<Button>();
            cardBtn.targetGraphic = cardImg;
            cardBtn.transition    = Selectable.Transition.None;
            cardBtn.onClick.AddListener(() => OpenPopup(null, inst));

            // Row 1 : route + ETA pill
            var topRow = HRow(cardGo.transform, 22f);
            var routeLbl = MakeTMP("R", topRow,
                $"{CityName(def.originCityId)}  →  {CityName(def.destinationCityId)}",
                14.5f, FontStyles.Bold, TextPrime);
            routeLbl.textWrappingMode = TextWrappingModes.NoWrap;
            routeLbl.overflowMode     = TextOverflowModes.Ellipsis;
            Le(routeLbl.gameObject, flexW: true, h: 22f);
            RoundedPill(topRow, FormatRemaining(inst), AccentBlue, 60f);

            // Row 2 : cargo description
            if (!string.IsNullOrEmpty(def.cargoLabel))
            {
                var cargoLbl = MakeTMP("Cargo", cardGo.transform, def.cargoLabel, 11.5f, FontStyles.Normal, TextSecond);
                cargoLbl.textWrappingMode = TextWrappingModes.NoWrap;
                cargoLbl.overflowMode     = TextOverflowModes.Ellipsis;
                Le(cargoLbl.gameObject, h: 16f);
            }

            // Row 3 : progress bar (time elapsed)
            BuildProgressBar(cardGo.transform, ProgressOf(inst), AccentBlue);

            // Row 4 : distance · tonnage + reward
            var botRow = HRow(cardGo.transform, 16f);
            var distLbl = MakeTMP("D", botRow,
                $"{def.distanceKm:F0} km   ·   {def.cargoTons} t",
                11.5f, FontStyles.Normal, TextDim);
            distLbl.textWrappingMode = TextWrappingModes.NoWrap;
            Le(distLbl.gameObject, flexW: true, h: 16f);
            var rewLbl = MakeTMP("$", botRow, $"+{def.baseReward:N0} $", 12.5f, FontStyles.Bold, AccentGreen);
            rewLbl.textWrappingMode = TextWrappingModes.NoWrap;
            rewLbl.alignment = TextAlignmentOptions.MidlineRight;
            Le(rewLbl.gameObject, w: 70f, h: 16f);
        }

        private static float ProgressOf(ContractInstance inst)
        {
            long total = inst.completionTimeUtcTicks - inst.startTimeUtcTicks;
            if (total <= 0) return 1f;
            long elapsed = DateTime.UtcNow.Ticks - inst.startTimeUtcTicks;
            return Mathf.Clamp01((float)elapsed / total);
        }

        private void BuildProgressBar(Transform parent, float progress, Color accent)
        {
            var track = MakeGO("Track", parent);
            var trackImg = track.AddComponent<Image>();
            trackImg.sprite        = _sprR8;
            trackImg.type          = Image.Type.Sliced;
            trackImg.color         = new Color(1f, 1f, 1f, 0.08f);
            trackImg.raycastTarget = false;
            Le(track, h: 4f);

            var fill = MakeGO("Fill", track.transform);
            var fillImg = fill.AddComponent<Image>();
            fillImg.sprite        = _sprR8;
            fillImg.type          = Image.Type.Sliced;
            fillImg.color         = accent;
            fillImg.raycastTarget = false;
            var fRt = fill.GetComponent<RectTransform>();
            fRt.anchorMin = new Vector2(0f, 0f);
            fRt.anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);
            fRt.offsetMin = Vector2.zero;
            fRt.offsetMax = Vector2.zero;
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

            // Cargo line
            var cargoLbl = MakeTMP("Cargo", routeBlock.transform,
                $"Marchandise : {(string.IsNullOrEmpty(d.cargoLabel) ? "Marchandises diverses" : d.cargoLabel)}",
                12f, FontStyles.Normal, TextSecond);
            cargoLbl.alignment        = TextAlignmentOptions.Left;
            cargoLbl.textWrappingMode = TextWrappingModes.NoWrap;
            cargoLbl.overflowMode     = TextOverflowModes.Ellipsis;
            Le(cargoLbl.gameObject, h: 18f);

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
            StatCell(gridGo.transform, $"{d.cargoTons} t", "CHARGEMENT");
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
            EnsureRoundedSprites();

            var fleet = CollectAvailableFleet(out _sessionProfile);
            bool hasFleet = fleet.Count > 0;
            _sessionDiffs = SelectDifficultiesForFleet(fleet);

            // Marge de sécurité : la route réelle est plus longue que la distance à vol
            // d'oiseau, donc on ne cherche que des contrats sous portéeMax × 0.9.
            float rawRange = MaxFleetRangeKm(fleet);
            _sessionMaxRangeKm = rawRange > 0f ? rawRange * FleetRangeSafetyFactor : float.MaxValue;

            _genResults      = new ContractData[3];
            _genRouteLabels  = new TMP_Text[3];
            _genCargoLabels  = new TMP_Text[3];
            _genInfoLabels   = new TMP_Text[3];
            _genRewardLabels = new TMP_Text[3];
            _genTakeBtns     = new Button[3];
            _genBtnLabels    = new TMP_Text[3];
            _genBtnImgs      = new Image[3];
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

            // Full-screen backdrop — transparent black scrim covering everything
            var page = MakeGO("Page", _genModal.transform);
            var pageImg = page.AddComponent<Image>();
            pageImg.color         = new Color(0f, 0f, 0f, 0.82f);
            pageImg.raycastTarget = true;
            Stretch(page.GetComponent<RectTransform>());
            var pageBtn = page.AddComponent<Button>();
            pageBtn.transition = Selectable.Transition.None;
            pageBtn.onClick.AddListener(CloseGenModal);

            // Top header bar — title centered, close at the right
            var hdrRow = MakeGO("Hdr", page.transform);
            var hdrImg = hdrRow.AddComponent<Image>();
            hdrImg.color         = new Color(0, 0, 0, 0);
            hdrImg.raycastTarget = false;
            var hdrRt = hdrRow.GetComponent<RectTransform>();
            hdrRt.anchorMin        = new Vector2(0f, 1f);
            hdrRt.anchorMax        = new Vector2(1f, 1f);
            hdrRt.pivot            = new Vector2(0.5f, 1f);
            hdrRt.sizeDelta        = new Vector2(0f, 100f);
            hdrRt.anchoredPosition = Vector2.zero;

            // Title stack — centered horizontally over the cards
            var titleStack = MakeGO("TS", hdrRow.transform);
            var tsRt = titleStack.GetComponent<RectTransform>();
            tsRt.anchorMin        = new Vector2(0.5f, 0.5f);
            tsRt.anchorMax        = new Vector2(0.5f, 0.5f);
            tsRt.pivot            = new Vector2(0.5f, 0.5f);
            tsRt.sizeDelta        = new Vector2(600f, 100f);
            tsRt.anchoredPosition = Vector2.zero;
            var tsVlg = titleStack.AddComponent<VerticalLayoutGroup>();
            tsVlg.childForceExpandWidth  = true;
            tsVlg.childForceExpandHeight = false;
            tsVlg.childControlWidth      = true;
            tsVlg.childControlHeight     = false;
            tsVlg.childAlignment         = TextAnchor.MiddleCenter;
            tsVlg.spacing = 3f;

            var eyebrowLbl = MakeTMP("Eye", titleStack.transform, "GÉNÉRER", 11f, FontStyles.Bold, TextDim);
            eyebrowLbl.characterSpacing = 3f;
            eyebrowLbl.alignment = TextAlignmentOptions.Center;
            Le(eyebrowLbl.gameObject, h: 18f);

            var titleLbl2 = MakeTMP("T", titleStack.transform, "Nouveaux contrats", 26f, FontStyles.Bold, TextPrime);
            titleLbl2.alignment = TextAlignmentOptions.Center;
            Le(titleLbl2.gameObject, h: 36f);

            // Close — anchored top-right of the page
            var closeXBtn = LinkBtn(hdrRow.transform, "✕", TextSecond);
            var clRt = (RectTransform)closeXBtn.transform;
            clRt.anchorMin        = new Vector2(1f, 1f);
            clRt.anchorMax        = new Vector2(1f, 1f);
            clRt.pivot            = new Vector2(1f, 1f);
            clRt.sizeDelta        = new Vector2(52f, 100f);
            clRt.anchoredPosition = new Vector2(-24f, 0f);
            closeXBtn.onClick.AddListener(CloseGenModal);

            // Header separator
            var hdrSep = MakeGO("HSep", page.transform);
            hdrSep.AddComponent<Image>().color = BorderFaint;
            var sepRt = hdrSep.GetComponent<RectTransform>();
            sepRt.anchorMin        = new Vector2(0f, 1f);
            sepRt.anchorMax        = new Vector2(1f, 1f);
            sepRt.pivot            = new Vector2(0.5f, 1f);
            sepRt.sizeDelta        = new Vector2(0f, 1f);
            sepRt.anchoredPosition = new Vector2(0f, -100f);

            // No available vehicle → nothing to generate; tell the player to buy one.
            if (!hasFleet) { BuildNoFleetMessage(page.transform); return; }

            // Row of 3 large cards — centered on the page, side by side
            var cardsRow = MakeGO("CardsRow", page.transform);
            var crImg = cardsRow.AddComponent<Image>();
            crImg.color         = new Color(0, 0, 0, 0);
            crImg.raycastTarget = false;
            var crRt = cardsRow.GetComponent<RectTransform>();
            crRt.anchorMin        = new Vector2(0.5f, 0.5f);
            crRt.anchorMax        = new Vector2(0.5f, 0.5f);
            crRt.pivot            = new Vector2(0.5f, 0.5f);
            crRt.sizeDelta        = new Vector2(1380f, 0f);
            crRt.anchoredPosition = new Vector2(0f, -16f);
            var crHlg = cardsRow.AddComponent<HorizontalLayoutGroup>();
            crHlg.spacing               = 24f;
            crHlg.childAlignment        = TextAnchor.MiddleCenter;
            crHlg.childForceExpandWidth  = true;
            crHlg.childForceExpandHeight = false;
            crHlg.childControlWidth      = true;
            crHlg.childControlHeight     = true;
            cardsRow.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            for (int i = 0; i < 3; i++)
                BuildGenSlot(cardsRow.transform, i);

            int sid = _genSessionId;
            for (int i = 0; i < 3; i++)
                GenerateSingleAsync(i, sid);
        }

        private void BuildGenSlot(Transform parent, int slotIdx)
        {
            var diff   = _sessionDiffs[slotIdx];
            var accent = DiffColors[(int)diff];
            var label  = DiffNames[(int)diff];

            // Card — rounded background + drop shadow
            var card    = MakeGO("GenCard" + slotIdx, parent);
            var cardImg = card.AddComponent<Image>();
            cardImg.sprite        = _sprR16;
            cardImg.type          = Image.Type.Sliced;
            cardImg.color         = BgElevated;
            cardImg.raycastTarget = true;
            var shadow = card.AddComponent<Shadow>();
            shadow.effectColor    = new Color(0f, 0f, 0f, 0.55f);
            shadow.effectDistance = new Vector2(0f, -5f);

            var cardVlg = card.AddComponent<VerticalLayoutGroup>();
            cardVlg.padding               = new RectOffset(28, 28, 26, 26);
            cardVlg.spacing               = 18f;
            cardVlg.childAlignment        = TextAnchor.UpperLeft;
            cardVlg.childForceExpandWidth  = true;
            cardVlg.childForceExpandHeight = false;
            cardVlg.childControlWidth      = true;
            cardVlg.childControlHeight     = true;
            card.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            Le(card, flexW: true);

            // Row 1 — difficulty badge + reward
            var topRow = HRow(card.transform, 30f);
            var badge  = MakeGO("Badge", topRow);
            var badgeImg = badge.AddComponent<Image>();
            badgeImg.sprite        = _sprR8;
            badgeImg.type          = Image.Type.Sliced;
            badgeImg.color         = new Color(accent.r, accent.g, accent.b, 0.16f);
            badgeImg.raycastTarget = false;
            Le(badge, w: 104f, h: 28f);
            var badgeLbl = MakeTMP("B", badge.transform, label, 12.5f, FontStyles.Bold, accent);
            badgeLbl.characterSpacing = 1f;
            badgeLbl.alignment        = TextAlignmentOptions.Center;
            Stretch(badgeLbl.GetComponent<RectTransform>());

            var spacer = MakeGO("Sp", topRow);
            var spImg = spacer.AddComponent<Image>();
            spImg.color = new Color(0, 0, 0, 0);
            spImg.raycastTarget = false;
            Le(spacer, flexW: true, h: 30f);

            var rewardLbl = MakeTMP("Rew", topRow, "", 22f, FontStyles.Bold, AccentGreen);
            rewardLbl.alignment        = TextAlignmentOptions.MidlineRight;
            rewardLbl.textWrappingMode = TextWrappingModes.NoWrap;
            Le(rewardLbl.gameObject, w: 150f, h: 30f);
            _genRewardLabels[slotIdx] = rewardLbl;

            // Row 2 — route
            var routeLbl = MakeTMP("Route", card.transform, "Génération en cours…",
                23f, FontStyles.Italic, TextSecond);
            routeLbl.textWrappingMode = TextWrappingModes.NoWrap;
            routeLbl.overflowMode     = TextOverflowModes.Ellipsis;
            Le(routeLbl.gameObject, h: 32f);
            _genRouteLabels[slotIdx] = routeLbl;

            // Row 3 — cargo description (goods to haul)
            var cargoLbl = MakeTMP("Cargo", card.transform, "", 16f, FontStyles.Normal, TextPrime);
            cargoLbl.textWrappingMode = TextWrappingModes.NoWrap;
            cargoLbl.overflowMode     = TextOverflowModes.Ellipsis;
            Le(cargoLbl.gameObject, h: 22f);
            _genCargoLabels[slotIdx] = cargoLbl;

            // Row 4 — stats
            var infoLbl = MakeTMP("I", card.transform, "", 15f, FontStyles.Normal, TextDim);
            infoLbl.textWrappingMode = TextWrappingModes.NoWrap;
            Le(infoLbl.gameObject, h: 20f);
            _genInfoLabels[slotIdx] = infoLbl;

            // Row 5 — full-width action button (loading → take / retry)
            var btnGo  = MakeGO("Btn", card.transform);
            var btnImg = btnGo.AddComponent<Image>();
            btnImg.sprite = _sprR12;
            btnImg.type   = Image.Type.Sliced;
            btnImg.color  = new Color(accent.r, accent.g, accent.b, 0.45f);
            var btn = btnGo.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            btn.transition    = Selectable.Transition.None;
            btn.interactable  = false;
            Le(btnGo, flexW: true, h: 56f);
            var btnLbl = MakeTMP("L", btnGo.transform, "Génération…", 17f, FontStyles.Bold, Color.white);
            btnLbl.alignment = TextAlignmentOptions.Center;
            Stretch(btnLbl.GetComponent<RectTransform>());

            _genTakeBtns[slotIdx]  = btn;
            _genBtnLabels[slotIdx] = btnLbl;
            _genBtnImgs[slotIdx]   = btnImg;
        }

        private async void GenerateSingleAsync(int slotIdx, int sessionId)
        {
            var gen = ServiceLocator.Get<ContractGenerator>();
            if (gen == null) { MarkGenError(slotIdx); return; }

            var profile = _sessionProfile;
            var diff    = _sessionDiffs[slotIdx];

            ContractData result = null;
            try { result = await gen.GenerateAsync(profile, diff, _sessionMaxRangeKm); }
            catch (Exception e) { Debug.LogWarning($"[Contracts] gen slot {slotIdx} failed: {e.Message}"); }

            if (_genModal == null || _genSessionId != sessionId) return;
            _genResults[slotIdx] = result;
            UpdateGenSlot(slotIdx, result);
        }

        private void UpdateGenSlot(int slotIdx, ContractData def)
        {
            if (_genRouteLabels[slotIdx] == null) return;
            if (def == null) { MarkGenError(slotIdx); return; }

            var accent = DiffColors[(int)_sessionDiffs[slotIdx]];

            _genRouteLabels[slotIdx].text      = $"{CityName(def.originCityId)}  →  {CityName(def.destinationCityId)}";
            _genRouteLabels[slotIdx].color     = TextPrime;
            _genRouteLabels[slotIdx].fontStyle = FontStyles.Bold;

            if (_genCargoLabels[slotIdx] != null)
            {
                _genCargoLabels[slotIdx].text  = string.IsNullOrEmpty(def.cargoLabel) ? "Marchandises diverses" : def.cargoLabel;
                _genCargoLabels[slotIdx].color = TextSecond;
            }

            _genInfoLabels[slotIdx].text  = $"{def.distanceKm:F0} km     ·     {def.cargoTons} t";
            _genInfoLabels[slotIdx].color = TextDim;

            if (_genRewardLabels[slotIdx] != null)
            {
                _genRewardLabels[slotIdx].text  = $"+{def.baseReward:N0} $";
                _genRewardLabels[slotIdx].color = AccentGreen;
            }

            int idx = slotIdx;
            var btn = _genTakeBtns[slotIdx];
            if (btn != null)
            {
                btn.interactable = true;
                if (_genBtnImgs[slotIdx]   != null) _genBtnImgs[slotIdx].color = accent;
                if (_genBtnLabels[slotIdx] != null) _genBtnLabels[slotIdx].text = "Prendre  →";
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => OnTakeGeneratedContract(idx));
            }
        }

        private void MarkGenError(int slotIdx)
        {
            if (_genRouteLabels[slotIdx] != null)
            {
                _genRouteLabels[slotIdx].text      = "Génération indisponible";
                _genRouteLabels[slotIdx].color     = DangerRed;
                _genRouteLabels[slotIdx].fontStyle = FontStyles.Italic;
            }
            if (_genCargoLabels[slotIdx] != null) _genCargoLabels[slotIdx].text = "";
            if (_genInfoLabels[slotIdx] != null)
            {
                _genInfoLabels[slotIdx].text  = "Vérifie ta connexion, puis réessaie.";
                _genInfoLabels[slotIdx].color = TextDim;
            }
            if (_genRewardLabels[slotIdx] != null) _genRewardLabels[slotIdx].text = "";

            int idx = slotIdx;
            var btn = _genTakeBtns[slotIdx];
            if (btn != null)
            {
                btn.interactable = true;
                if (_genBtnImgs[slotIdx]   != null) _genBtnImgs[slotIdx].color = new Color(0.30f, 0.34f, 0.40f, 1f);
                if (_genBtnLabels[slotIdx] != null) _genBtnLabels[slotIdx].text = "Réessayer";
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => RetryGenSlot(idx));
            }
        }

        private void RetryGenSlot(int slotIdx)
        {
            if (_genRouteLabels[slotIdx] == null) return;
            var accent = DiffColors[(int)_sessionDiffs[slotIdx]];

            _genResults[slotIdx] = null;
            _genRouteLabels[slotIdx].text      = "Génération en cours…";
            _genRouteLabels[slotIdx].color     = TextSecond;
            _genRouteLabels[slotIdx].fontStyle = FontStyles.Italic;
            if (_genCargoLabels[slotIdx]  != null) _genCargoLabels[slotIdx].text = "";
            if (_genInfoLabels[slotIdx]   != null) _genInfoLabels[slotIdx].text = "";
            if (_genRewardLabels[slotIdx] != null) _genRewardLabels[slotIdx].text = "";

            var btn = _genTakeBtns[slotIdx];
            if (btn != null)
            {
                btn.interactable = false;
                if (_genBtnImgs[slotIdx]   != null) _genBtnImgs[slotIdx].color = new Color(accent.r, accent.g, accent.b, 0.45f);
                if (_genBtnLabels[slotIdx] != null) _genBtnLabels[slotIdx].text = "Génération…";
                btn.onClick.RemoveAllListeners();
            }

            GenerateSingleAsync(slotIdx, _genSessionId);
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

        // Portée max parmi les camions disponibles : on ne génère que des contrats
        // qu'au moins un véhicule dispo peut tenir avec un plein. Même formule que
        // ContractSystem.CanAttempt (autonomie de base, sans bonus conducteur).
        // Renvoie 0 si la flotte est vide (le caller traite ce cas séparément).
        private static float MaxFleetRangeKm(List<VehicleData> fleet)
        {
            float maxRange = 0f;
            foreach (var data in fleet)
            {
                if (data.fuelConsumptionLPer100Km <= 0f) continue;
                float range = data.fuelTankCapacityLiters * 100f / data.fuelConsumptionLPer100Km;
                if (range > maxRange) maxRange = range;
            }
            return maxRange;
        }

        // Affiché dans la modale quand aucun camion n'est disponible.
        private void BuildNoFleetMessage(Transform parent)
        {
            var box = MakeGO("NoFleet", parent);
            var rt  = box.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta        = new Vector2(720f, 200f);
            rt.anchoredPosition = new Vector2(0f, -16f);
            var vlg = box.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment        = TextAnchor.MiddleCenter;
            vlg.childForceExpandWidth  = true;
            vlg.childControlWidth      = true;
            vlg.childControlHeight     = false;
            vlg.spacing = 12f;

            var title = MakeTMP("T", box.transform, "Aucun camion disponible", 22f, FontStyles.Bold, TextPrime);
            title.alignment = TextAlignmentOptions.Center;
            Le(title.gameObject, h: 32f);

            var msg = MakeTMP("M", box.transform,
                "Achète un camion pour pouvoir générer et accepter des contrats.",
                16f, FontStyles.Normal, TextDim);
            msg.alignment        = TextAlignmentOptions.Center;
            msg.textWrappingMode = TextWrappingModes.Normal;
            Le(msg.gameObject, flexW: true, h: 48f);
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

        // Rounded colored label pill (soft bg + colored text)
        private Button RoundedPill(Transform parent, string text, Color color, float width)
        {
            var go  = MakeGO("Pill", parent);
            var img = go.AddComponent<Image>();
            img.sprite = _sprR8;
            img.type   = Image.Type.Sliced;
            img.color  = new Color(color.r, color.g, color.b, 0.16f);
            Le(go, w: width, h: 18f);
            var lbl = MakeTMP("L", go.transform, text, 10.5f, FontStyles.Bold, color);
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

        // ── Rounded sprite factory (9-slice, generated once) ───────────────────────
        private void EnsureRoundedSprites()
        {
            if (_sprR16 != null) return;
            _sprR8  = MakeRoundedSprite(8);
            _sprR12 = MakeRoundedSprite(12);
            _sprR16 = MakeRoundedSprite(16);
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
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                                 SpriteMeshType.FullRect, new Vector4(r, r, r, r));
        }

        private static float RoundedAlpha(int x, int y, int size, int r)
        {
            int cx = -1, cy = -1;
            if      (x < r         && y < r)         { cx = r;        cy = r;        }
            else if (x >= size - r && y < r)         { cx = size - r; cy = r;        }
            else if (x < r         && y >= size - r) { cx = r;        cy = size - r; }
            else if (x >= size - r && y >= size - r) { cx = size - r; cy = size - r; }
            if (cx < 0) return 1f;
            float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
            return Mathf.Clamp01(r - d + 0.5f);
        }
    }
}
