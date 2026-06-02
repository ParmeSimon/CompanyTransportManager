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
        private TMP_Text   _activeCountLbl;
        private Image      _activeCountPill;
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
        private Transform[]          _genInfoHosts    = new Transform[3];
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
            Le(newBtnGo, h: 46f);
            // Ombre verte diffuse pour faire « flotter » le CTA
            var newBtnSh = newBtnGo.AddComponent<Shadow>();
            newBtnSh.effectColor    = new Color(AccentGreen.r, AccentGreen.g, AccentGreen.b, 0.45f);
            newBtnSh.effectDistance = new Vector2(0f, -3f);
            var newBtnComp = newBtnGo.AddComponent<Button>();
            newBtnComp.targetGraphic = newBtnImg;
            newBtnComp.transition    = Selectable.Transition.None;
            newBtnComp.onClick.AddListener(OpenGenerationModal);
            var newBtnLbl = MakeTMP("L", newBtnGo.transform, "+   Nouveau contrat", 13.5f, FontStyles.Bold, Color.white);
            newBtnLbl.characterSpacing = 0.8f;
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

            // Compteur (pill arrondi teinté de l'accent) — mis à jour dans Refresh()
            var countPill = MakeGO("Count", hdrGo.transform);
            _activeCountPill = countPill.AddComponent<Image>();
            _activeCountPill.sprite        = _sprR8;
            _activeCountPill.type          = Image.Type.Sliced;
            _activeCountPill.color         = new Color(accent.r, accent.g, accent.b, 0.16f);
            _activeCountPill.raycastTarget = false;
            Le(countPill, w: 22f, h: 18f);
            _activeCountLbl = MakeTMP("N", countPill.transform, "0", 10.5f, FontStyles.Bold, accent);
            _activeCountLbl.alignment        = TextAlignmentOptions.Center;
            _activeCountLbl.textWrappingMode = TextWrappingModes.NoWrap;
            Stretch(_activeCountLbl.GetComponent<RectTransform>());

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

            if (_activeCountLbl != null)  _activeCountLbl.text = activeCount.ToString();
            if (_activeCountPill != null) _activeCountPill.gameObject.SetActive(activeCount > 0);

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
            var cardOl = cardGo.AddComponent<Outline>();
            cardOl.effectColor    = BorderFaint;
            cardOl.effectDistance = new Vector2(1f, -1f);

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
                RouteSummary(def),
                14.5f, FontStyles.Bold, TextPrime);
            routeLbl.textWrappingMode = TextWrappingModes.NoWrap;
            routeLbl.overflowMode     = TextOverflowModes.Ellipsis;
            Le(routeLbl.gameObject, flexW: true, h: 22f);
            if (def.isMultiStop)
                RoundedPill(topRow, $"⇆ {def.StopCount} arrêts", AccentAmber, 78f);
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
            // Boîte subtile, en pointillé visuel (fond très léger arrondi), contenu centré.
            var go = MakeGO("Empty", parent);
            var img = go.AddComponent<Image>();
            img.sprite        = _sprR12;
            img.type          = Image.Type.Sliced;
            img.color         = BgSubtle;
            img.raycastTarget = false;
            Le(go, h: 72f);

            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.padding               = new RectOffset(12, 12, 14, 14);
            vlg.spacing               = 4f;
            vlg.childAlignment        = TextAnchor.MiddleCenter;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth      = true;
            vlg.childControlHeight     = true;

            var glyph = MakeTMP("Glyph", go.transform, "·  ·  ·", 18f, FontStyles.Bold, TextDim);
            glyph.alignment        = TextAlignmentOptions.Center;
            glyph.characterSpacing = 2f;
            Le(glyph.gameObject, h: 22f);

            var lbl = MakeTMP("Lbl", go.transform, msg, 11.5f, FontStyles.Normal, TextDim);
            lbl.alignment        = TextAlignmentOptions.Center;
            lbl.textWrappingMode = TextWrappingModes.NoWrap;
            Le(lbl.gameObject, h: 16f);
        }

        // ── Popup ─────────────────────────────────────────────────────────────────

        /// Rouvre le popup d'un contrat à partir de sa définition (utilisé par le
        /// bouton « Rouvrir le contrat » de la carte, après prévisualisation de route).
        /// Si le contrat est déjà en cours, on rouvre la fiche « trajet en cours » ;
        /// sinon la fiche « contrat disponible » (avec sélection de camion / démarrage).
        public void ShowContractByDefinition(ContractData def)
        {
            if (def == null) return;
            var contracts = ServiceLocator.Get<ContractSystem>();
            ContractInstance inst = null;
            if (contracts != null)
                foreach (var c in contracts.Active)
                    if (c.status == ContractStatus.InProgress && c.definition == def) { inst = c; break; }
            OpenPopup(inst == null ? def : null, inst);
        }

        private void OpenPopup(ContractData def, ContractInstance inst)
        {
            ClosePopup();
            _popupDef  = def ?? inst?.definition;
            _popupInst = inst;
            _selectedVehicleIdx = -1;
            _vehicleRowBgs.Clear();
            _selectableVehicles.Clear();

            bool isActive = inst != null;
            EnsureRoundedSprites();

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

            var d = _popupDef;
            var accentColor = isActive ? AccentBlue : DiffColors[(int)d.difficulty];

            // ── Carte arrondie centrée, dimensionnée par son contenu ────────────────
            var cardGo  = MakeGO("Card", _popup.transform);
            var cardImg = cardGo.AddComponent<Image>();
            cardImg.sprite = _sprR16;
            cardImg.type   = Image.Type.Sliced;
            cardImg.color  = BgDeep;
            var cardSh = cardGo.AddComponent<Shadow>();
            cardSh.effectColor    = new Color(0f, 0f, 0f, 0.55f);
            cardSh.effectDistance = new Vector2(0f, -8f);
            var cardOl = cardGo.AddComponent<Outline>();
            cardOl.effectColor    = BorderFaint;
            cardOl.effectDistance = new Vector2(1f, -1f);
            var cardRt = cardGo.GetComponent<RectTransform>();
            cardRt.anchorMin = new Vector2(0.5f, 0.5f);
            cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardRt.pivot     = new Vector2(0.5f, 0.5f);
            cardRt.sizeDelta = new Vector2(440f, 0f);

            var cardVlg = cardGo.AddComponent<VerticalLayoutGroup>();
            cardVlg.padding               = new RectOffset(0, 0, 0, 18);
            cardVlg.spacing               = 0f;
            cardVlg.childAlignment        = TextAnchor.UpperCenter;
            cardVlg.childForceExpandWidth  = true;
            cardVlg.childForceExpandHeight = false;
            cardVlg.childControlWidth      = true;
            cardVlg.childControlHeight     = true;
            cardGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // ── 1 — Header : eyebrow + statut, pastille difficulté, fermeture ───────
            var cHdrGo = MakeGO("CHdr", cardGo.transform);
            var cHdrImg = cHdrGo.AddComponent<Image>();
            cHdrImg.color         = new Color(0, 0, 0, 0);
            cHdrImg.raycastTarget = false;
            Le(cHdrGo, h: 60f);
            var cHhlg = cHdrGo.AddComponent<HorizontalLayoutGroup>();
            cHhlg.padding               = new RectOffset(22, 14, 18, 8);
            cHhlg.spacing               = 10f;
            cHhlg.childAlignment        = TextAnchor.MiddleLeft;
            cHhlg.childForceExpandWidth  = false;
            cHhlg.childForceExpandHeight = false;
            cHhlg.childControlWidth      = true;
            cHhlg.childControlHeight     = true;

            var eyebrow = MakeTMP("Eye", cHdrGo.transform,
                isActive ? "TRAJET EN COURS" : "CONTRAT DISPONIBLE",
                9f, FontStyles.Bold, TextDim);
            eyebrow.characterSpacing = 2.5f;
            eyebrow.textWrappingMode = TextWrappingModes.NoWrap;
            Le(eyebrow.gameObject, flexW: true, h: 34f);

            if (isActive)
                RoundedPill(cHdrGo.transform, FormatRemaining(_popupInst), AccentBlue, 64f);
            else
                RoundedPill(cHdrGo.transform, DiffNames[(int)d.difficulty], accentColor, 78f);

            var closeX = IconBtn(cHdrGo.transform, "minus", TextSecond, 16f);
            Le(closeX.gameObject, w: 26f, h: 34f);
            closeX.onClick.AddListener(ClosePopup);

            // ── 2 — Hero trajet : origine → destination + marchandise ───────────────
            var routeBlock = MakeGO("RouteBlock", cardGo.transform);
            var rbImg = routeBlock.AddComponent<Image>();
            rbImg.color         = new Color(0, 0, 0, 0);
            rbImg.raycastTarget = false;
            var rbVlg = routeBlock.AddComponent<VerticalLayoutGroup>();
            rbVlg.padding               = new RectOffset(22, 22, 6, 16);
            rbVlg.spacing               = 6f;
            rbVlg.childForceExpandWidth  = true;
            rbVlg.childForceExpandHeight = false;
            rbVlg.childControlWidth      = true;
            rbVlg.childControlHeight     = true;
            routeBlock.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Toutes les villes du trajet : origine → (escales) → destination.
            var stopNames = new System.Collections.Generic.List<string> { CityName(d.originCityId) };
            if (d.isMultiStop && d.viaCityIds != null)
                foreach (var vc in d.viaCityIds) stopNames.Add(CityName(vc));
            stopNames.Add(CityName(d.destinationCityId));

            if (stopNames.Count > 2)
            {
                // Tournée : on empile toutes les villes avec un repère coloré (extrémités = dépôt/arrivée).
                for (int s = 0; s < stopNames.Count; s++)
                {
                    bool end = (s == 0 || s == stopNames.Count - 1);
                    var row = HRow(routeBlock.transform, 26f);
                    var dotGo = MakeGO("D" + s, row);
                    var dImg = dotGo.AddComponent<Image>();
                    dImg.sprite = _sprR8; dImg.type = Image.Type.Sliced;
                    dImg.color  = end ? accentColor : new Color(accentColor.r, accentColor.g, accentColor.b, 0.5f);
                    dImg.raycastTarget = false;
                    Le(dotGo, w: 8f, h: 8f);
                    var lbl = MakeTMP("S" + s, row, stopNames[s], 18f, FontStyles.Bold, end ? TextPrime : TextSecond);
                    lbl.alignment        = TextAlignmentOptions.Left;
                    lbl.textWrappingMode = TextWrappingModes.NoWrap;
                    lbl.overflowMode     = TextOverflowModes.Ellipsis;
                    Le(lbl.gameObject, flexW: true, h: 24f);
                }
            }
            else
            {
                var originLbl = MakeTMP("From", routeBlock.transform,
                    stopNames[0], 21f, FontStyles.Bold, TextPrime);
                originLbl.alignment = TextAlignmentOptions.Left;
                Le(originLbl.gameObject, h: 28f);

                var arrowRow = HRow(routeBlock.transform, 14f);
                var arrowDot = MakeGO("ODot", arrowRow);
                var aDotImg = arrowDot.AddComponent<Image>();
                aDotImg.sprite = _sprR8; aDotImg.type = Image.Type.Sliced;
                aDotImg.color  = accentColor;
                Le(arrowDot, w: 7f, h: 7f);
                var arrowLine = MakeGO("Line", arrowRow);
                arrowLine.AddComponent<Image>().color = new Color(accentColor.r, accentColor.g, accentColor.b, 0.45f);
                Le(arrowLine, h: 2f, flexW: true);
                var arrowLbl = MakeTMP("Arrow", arrowRow, "→", 13f, FontStyles.Bold, accentColor);
                arrowLbl.textWrappingMode = TextWrappingModes.NoWrap;
                Le(arrowLbl.gameObject, w: 16f, h: 14f);

                var destLbl = MakeTMP("To", routeBlock.transform,
                    stopNames[1], 21f, FontStyles.Bold, TextPrime);
                destLbl.alignment = TextAlignmentOptions.Right;
                Le(destLbl.gameObject, h: 28f);
            }

            var cargoRow = HRow(routeBlock.transform, 18f);
            var cargoTag = MakeTMP("CT", cargoRow, "MARCHANDISE", 8f, FontStyles.Bold, TextDim);
            cargoTag.characterSpacing = 1.5f;
            cargoTag.textWrappingMode = TextWrappingModes.NoWrap;
            Le(cargoTag.gameObject, w: 86f, h: 16f);
            var cargoLbl = MakeTMP("Cargo", cargoRow,
                string.IsNullOrEmpty(d.cargoLabel) ? "Marchandises diverses" : d.cargoLabel,
                12f, FontStyles.Normal, TextSecond);
            cargoLbl.textWrappingMode = TextWrappingModes.NoWrap;
            cargoLbl.overflowMode     = TextOverflowModes.Ellipsis;
            Le(cargoLbl.gameObject, flexW: true, h: 16f);

            // ── 3 — Cartouches statistiques (arrondis) ──────────────────────────────
            var gridWrap = MakeGO("StatsWrap", cardGo.transform);
            var gwImg = gridWrap.AddComponent<Image>();
            gwImg.color         = new Color(0, 0, 0, 0);
            gwImg.raycastTarget = false;
            var gwVlg = gridWrap.AddComponent<VerticalLayoutGroup>();
            gwVlg.padding               = new RectOffset(16, 16, 0, 0);
            gwVlg.childForceExpandWidth  = true;
            gwVlg.childControlWidth      = true;
            gwVlg.childControlHeight     = true;
            gridWrap.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var gridGo = MakeGO("StatsGrid", gridWrap.transform);
            var ggImg = gridGo.AddComponent<Image>();
            ggImg.color         = new Color(0, 0, 0, 0);
            ggImg.raycastTarget = false;
            var gridHlg = gridGo.AddComponent<HorizontalLayoutGroup>();
            gridHlg.spacing               = 8f;
            gridHlg.childForceExpandWidth  = true;
            gridHlg.childForceExpandHeight = false;
            gridHlg.childControlWidth      = true;
            gridHlg.childControlHeight     = true;
            gridGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            RoundedStatCell(gridGo.transform, $"{d.distanceKm:F0} km", "DISTANCE");
            RoundedStatCell(gridGo.transform, $"+{d.baseReward:N0} $", "RÉCOMPENSE", AccentGreen);
            RoundedStatCell(gridGo.transform, $"{d.cargoTons} t", "CHARGEMENT");
            if (isActive)
                RoundedStatCell(gridGo.transform, FormatRemaining(_popupInst), "RESTANT", AccentBlue);
            else
                RoundedStatCell(gridGo.transform, DiffNames[(int)d.difficulty], "DIFFICULTÉ", accentColor);

            // ── Note ponctualité (E1) ───────────────────────────────────────────────
            string punctNote;
            if (isActive)
            {
                bool onTime = _popupInst.deadlineTimeUtcTicks <= 0
                              || _popupInst.completionTimeUtcTicks <= _popupInst.deadlineTimeUtcTicks;
                punctNote = onTime
                    ? "<color=#3DC96E>Livraison à l'heure — bonus +15 %</color>"
                    : "<color=#ff6b6b>Livraison en retard — pénalité -20 %</color>";
            }
            else
            {
                punctNote = "Livré à temps : <color=#3DC96E>+15 %</color>   ·   en retard : <color=#ff6b6b>-20 %</color>";
            }
            var pn = MakeTMP("Punct", cardGo.transform, punctNote, 11.5f, FontStyles.Normal, TextSecond);
            pn.alignment = TextAlignmentOptions.Center;
            Le(pn.gameObject, h: 20f);

            // ── 4 — Sélecteur de camion (contrats disponibles uniquement) ───────────
            if (!isActive)
            {
                var pickBlock = MakeGO("Picker", cardGo.transform);
                var pickImg = pickBlock.AddComponent<Image>();
                pickImg.color         = new Color(0, 0, 0, 0);
                pickImg.raycastTarget = false;
                var pbVlg = pickBlock.AddComponent<VerticalLayoutGroup>();
                pbVlg.padding               = new RectOffset(16, 16, 14, 0);
                pbVlg.spacing               = 7f;
                pbVlg.childForceExpandWidth  = true;
                pbVlg.childForceExpandHeight = false;
                pbVlg.childControlWidth      = true;
                pbVlg.childControlHeight     = true;
                pickBlock.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                var pickLbl = MakeTMP("Lbl", pickBlock.transform, "AFFECTER UN CAMION",
                    8f, FontStyles.Bold, TextDim);
                pickLbl.characterSpacing = 2f;
                Le(pickLbl.gameObject, h: 16f);

                BuildVehiclePicker(pickBlock.transform, d);
            }

            // ── 5 — Actions (pied de carte) ─────────────────────────────────────────
            var footer = MakeGO("Footer", cardGo.transform);
            var footImg = footer.AddComponent<Image>();
            footImg.color         = new Color(0, 0, 0, 0);
            footImg.raycastTarget = false;
            var footVlg = footer.AddComponent<VerticalLayoutGroup>();
            footVlg.padding               = new RectOffset(16, 16, 14, 0);
            footVlg.spacing               = 9f;
            footVlg.childForceExpandWidth  = true;
            footVlg.childForceExpandHeight = false;
            footVlg.childControlWidth      = true;
            footVlg.childControlHeight     = true;
            footer.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            if (!isActive)
            {
                _startBtn = RoundedSolidBtn(footer.transform, "Démarrer le trajet  →", AccentGreen);
                Le(_startBtn.gameObject, flexW: true, h: 48f);
                _startBtn.onClick.AddListener(OnStartContract);
                _startBtn.interactable = false;
                SetBtnEnabled(_startBtn, false);
            }

            var captureDef = _popupDef;
            var routeMapBtn = RoundedGhostBtn(footer.transform, "Voir la route sur la carte", AccentBlue);
            Le(routeMapBtn.gameObject, flexW: true, h: 42f);
            routeMapBtn.onClick.AddListener(() => { ClosePopup(); GameEvents.RaiseShowContractRoute(captureDef); });
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
                var box = MakeGO("NoVeh", parent);
                var boxImg = box.AddComponent<Image>();
                boxImg.sprite        = _sprR8;
                boxImg.type          = Image.Type.Sliced;
                boxImg.color         = new Color(AccentAmber.r, AccentAmber.g, AccentAmber.b, 0.12f);
                boxImg.raycastTarget = false;
                var boxVlg = box.AddComponent<VerticalLayoutGroup>();
                boxVlg.padding               = new RectOffset(13, 13, 11, 11);
                boxVlg.spacing               = 3f;
                boxVlg.childForceExpandWidth  = true;
                boxVlg.childControlWidth      = true;
                boxVlg.childControlHeight     = true;
                box.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                var t = MakeTMP("T", box.transform, "Aucun camion prêt", 11.5f, FontStyles.Bold, AccentAmber);
                t.textWrappingMode = TextWrappingModes.NoWrap;
                Le(t.gameObject, h: 16f);

                if (gm.Save.vehicles.Count == 0)
                {
                    var m = MakeTMP("M", box.transform,
                        "Tu n'as aucun camion. Achète-en un au Magasin, puis affecte-lui un conducteur (onglet Véhicules).",
                        10f, FontStyles.Normal, TextSecond);
                    m.textWrappingMode = TextWrappingModes.Normal;
                    Le(m.gameObject, flexW: true, h: 30f);
                }
                else
                {
                    var m = MakeTMP("M", box.transform, "Pourquoi tes camions ne conviennent pas :",
                        10f, FontStyles.Normal, TextSecond);
                    m.textWrappingMode = TextWrappingModes.Normal;
                    Le(m.gameObject, flexW: true, h: 14f);

                    foreach (var v in gm.Save.vehicles)
                    {
                        var data   = catalog?.GetById(v.vehicleDataId);
                        string why = VehicleIneligibleReason(v, data, def);
                        if (why == null) continue;
                        string name = data?.displayName ?? v.vehicleDataId;
                        var line = MakeTMP("R", box.transform, $"• {name} — {why}", 10f, FontStyles.Normal, TextDim);
                        line.textWrappingMode = TextWrappingModes.Normal;
                        Le(line.gameObject, flexW: true, h: 14f);
                    }
                }
                return;
            }

            for (int i = 0; i < _selectableVehicles.Count; i++)
            {
                int idx  = i;
                var v    = _selectableVehicles[i];
                var data = catalog?.GetById(v.vehicleDataId);

                var rowGo  = MakeGO("VRow", parent);
                var rowImg = rowGo.AddComponent<Image>();
                rowImg.sprite = _sprR8;
                rowImg.type   = Image.Type.Sliced;
                rowImg.color  = BgElevated;
                _vehicleRowBgs.Add(rowImg);
                Le(rowGo, h: 40f);

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

        // Raison pour laquelle un camion n'est pas éligible à ce contrat (null = éligible).
        private static string VehicleIneligibleReason(VehicleInstance v, VehicleData data, ContractData def)
        {
            if (data == null) return "modèle inconnu";
            if (string.IsNullOrEmpty(v.assignedDriverInstanceId)) return "sans conducteur (affecte-en un dans Véhicules)";
            switch (v.status)
            {
                case VehicleStatus.OnContract:    return "déjà en mission";
                case VehicleStatus.InMaintenance: return "en maintenance";
                case VehicleStatus.Immobilized:   return "immobilisé";
            }
            if (data.capacity < def.requiredCapacity)
                return $"capacité {data.capacity} t < {def.requiredCapacity} t requis";
            return null;
        }

        private void SelectVehicle(int idx)
        {
            _selectedVehicleIdx = idx;
            for (int i = 0; i < _vehicleRowBgs.Count; i++)
                _vehicleRowBgs[i].color = i == idx
                    ? new Color(AccentGreen.r, AccentGreen.g, AccentGreen.b, 0.20f)
                    : BgElevated;
            if (_startBtn != null) { _startBtn.interactable = true; SetBtnEnabled(_startBtn, true); }
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
            _genInfoHosts    = new Transform[3];
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
            // Liseré teinté de la difficulté pour distinguer les cartes au premier coup d'œil
            var cardOl = card.AddComponent<Outline>();
            cardOl.effectColor    = new Color(accent.r, accent.g, accent.b, 0.30f);
            cardOl.effectDistance = new Vector2(1.4f, -1.4f);

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

            // Row 4 — puces d'infos (distance · tonnage · durée), remplies à la réception
            var infoHost = MakeGO("Info", card.transform);
            var ihImg = infoHost.AddComponent<Image>();
            ihImg.color         = new Color(0, 0, 0, 0);
            ihImg.raycastTarget = false;
            var ihHlg = infoHost.AddComponent<HorizontalLayoutGroup>();
            ihHlg.spacing               = 8f;
            ihHlg.childAlignment        = TextAnchor.MiddleLeft;
            ihHlg.childForceExpandWidth  = false;
            ihHlg.childForceExpandHeight = false;
            ihHlg.childControlWidth      = true;
            ihHlg.childControlHeight     = true;
            Le(infoHost, h: 30f);
            _genInfoHosts[slotIdx] = infoHost.transform;

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

            _genRouteLabels[slotIdx].text      = RouteSummary(def);
            _genRouteLabels[slotIdx].color     = TextPrime;
            _genRouteLabels[slotIdx].fontStyle = FontStyles.Bold;

            if (_genCargoLabels[slotIdx] != null)
            {
                _genCargoLabels[slotIdx].text  = string.IsNullOrEmpty(def.cargoLabel) ? "Marchandises diverses" : def.cargoLabel;
                _genCargoLabels[slotIdx].color = TextSecond;
            }

            FillGenInfoChips(slotIdx, def);

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
            if (_genInfoHosts[slotIdx] != null)
            {
                ClearChildren(_genInfoHosts[slotIdx]);
                var hint = MakeTMP("Hint", _genInfoHosts[slotIdx],
                    "Vérifie ta connexion, puis réessaie.", 13f, FontStyles.Italic, TextDim);
                hint.textWrappingMode = TextWrappingModes.NoWrap;
                Le(hint.gameObject, flexW: true, h: 20f);
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
            if (_genInfoHosts[slotIdx]    != null) ClearChildren(_genInfoHosts[slotIdx]);
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
        // Collecte TOUT le parc (quel que soit le statut / conducteur) pour dimensionner
        // les contrats à générer. Le check Idle + conducteur n'a lieu qu'au démarrage.
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

            // Les contrats premium ne sont proposés qu'avec le capstone RH débloqué.
            bool premiumUnlocked = (ServiceLocator.Get<Systems.Progression.SkillTreeSystem>()?
                                        .Flat(SkillEffectType.PremiumContractsUnlocked) ?? 0) > 0;

            var d0 = ContractDifficulty.Easy;
            var d1 = maxCap >= 5  ? ContractDifficulty.Medium : ContractDifficulty.Easy;
            var d2 = (maxCap >= 30 && premiumUnlocked) ? ContractDifficulty.Premium :
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

        // Affiché dans la modale uniquement quand le joueur n'a encore aucun camion.
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

            var title = MakeTMP("T", box.transform, "Aucun camion dans la flotte", 22f, FontStyles.Bold, TextPrime);
            title.alignment = TextAlignmentOptions.Center;
            Le(title.gameObject, h: 32f);

            var msg = MakeTMP("M", box.transform,
                "Achète ton premier camion dans le Magasin pour pouvoir générer et accepter des contrats.",
                16f, FontStyles.Normal, TextDim);
            msg.alignment        = TextAlignmentOptions.Center;
            msg.textWrappingMode = TextWrappingModes.Normal;
            Le(msg.gameObject, flexW: true, h: 56f);
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

        // Texte de route : chaîne complète à escales pour une tournée, sinon « origine → destination ».
        private static string RouteSummary(ContractData def)
        {
            if (def != null && def.isMultiStop && !string.IsNullOrEmpty(def.displayName))
                return def.displayName;
            return $"{CityName(def.originCityId)}  →  {CityName(def.destinationCityId)}";
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

        // Petite puce arrondie dimensionnée à son contenu (fond léger + texte coloré).
        private void Chip(Transform parent, string text, Color color)
        {
            var go  = MakeGO("Chip", parent);
            var img = go.AddComponent<Image>();
            img.sprite        = _sprR8;
            img.type          = Image.Type.Sliced;
            img.color         = new Color(1f, 1f, 1f, 0.07f);
            img.raycastTarget = false;

            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.padding               = new RectOffset(11, 11, 5, 5);
            hlg.childAlignment        = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth  = false;
            hlg.childForceExpandHeight = false;
            hlg.childControlWidth      = true;
            hlg.childControlHeight     = true;

            var csf = go.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
            go.AddComponent<LayoutElement>().minHeight = 28f;

            var lbl = MakeTMP("L", go.transform, text, 13.5f, FontStyles.Bold, color);
            lbl.textWrappingMode = TextWrappingModes.NoWrap;
            lbl.alignment        = TextAlignmentOptions.Center;
        }

        // Remplit l'hôte d'infos d'un slot de génération avec des puces.
        private void FillGenInfoChips(int slotIdx, ContractData def)
        {
            var host = _genInfoHosts[slotIdx];
            if (host == null) return;
            ClearChildren(host);
            Chip(host, $"{def.distanceKm:F0} km", TextSecond);
            Chip(host, $"{def.cargoTons} t", TextSecond);
            if (def.baseDurationSeconds > 0f)
                Chip(host, FormatDuration(def.baseDurationSeconds), TextSecond);
        }

        private static string FormatDuration(float seconds)
        {
            int total = Mathf.Max(0, Mathf.RoundToInt(seconds));
            int h = total / 3600;
            int m = (total % 3600) / 60;
            return h >= 1 ? $"{h}h {m:D2}" : $"{Mathf.Max(1, m)} min";
        }

        // Link-style button: transparent bg, colored text
        // Bouton transparent avec une icône centrée (Resources/UI/Icons/icons/<iconName>).
        private static Button IconBtn(Transform parent, string iconName, Color color, float iconSize = 16f)
        {
            var go  = MakeGO("IconBtn", parent);
            go.AddComponent<Image>().color = new Color(0, 0, 0, 0);
            var btn = go.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;

            var icGo = MakeGO("Ic", go.transform);
            var img  = icGo.AddComponent<Image>();
            var spr  = Resources.Load<Sprite>($"UI/Icons/icons/{iconName}");
            img.sprite         = spr;
            img.enabled        = spr != null;   // pas de carré si l'icône manque
            img.color          = color;
            img.preserveAspect = true;
            img.raycastTarget  = false;
            var rt = icGo.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.sizeDelta        = new Vector2(iconSize, iconSize);
            rt.anchoredPosition = Vector2.zero;
            return btn;
        }

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

        // Cartouche stat arrondi (valeur + libellé), prend une part égale de la largeur.
        private void RoundedStatCell(Transform parent, string value, string key, Color? valueColor = null)
        {
            var cell    = MakeGO("Cell", parent);
            var cellImg = cell.AddComponent<Image>();
            cellImg.sprite        = _sprR12;
            cellImg.type          = Image.Type.Sliced;
            cellImg.color         = BgElevated;
            cellImg.raycastTarget = false;
            var cvlg = cell.AddComponent<VerticalLayoutGroup>();
            cvlg.padding               = new RectOffset(11, 10, 11, 10);
            cvlg.spacing               = 3f;
            cvlg.childAlignment        = TextAnchor.MiddleCenter;
            cvlg.childForceExpandWidth  = true;
            cvlg.childForceExpandHeight = false;
            cvlg.childControlWidth      = true;
            cvlg.childControlHeight     = true;
            cell.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            Le(cell, flexW: true);

            var valLbl = MakeTMP("V", cell.transform, value, 14.5f, FontStyles.Bold, valueColor ?? TextPrime);
            valLbl.alignment        = TextAlignmentOptions.Center;
            valLbl.textWrappingMode = TextWrappingModes.NoWrap;
            valLbl.overflowMode     = TextOverflowModes.Ellipsis;
            Le(valLbl.gameObject, h: 20f);

            var keyLbl = MakeTMP("K", cell.transform, key, 7.5f, FontStyles.Bold, TextDim);
            keyLbl.characterSpacing = 1.2f;
            keyLbl.alignment        = TextAlignmentOptions.Center;
            keyLbl.textWrappingMode = TextWrappingModes.NoWrap;
            Le(keyLbl.gameObject, h: 11f);
        }

        // Bouton plein arrondi (CTA principal).
        private Button RoundedSolidBtn(Transform parent, string label, Color color)
        {
            var go  = MakeGO("RSBtn", parent);
            var img = go.AddComponent<Image>();
            img.sprite = _sprR12;
            img.type   = Image.Type.Sliced;
            img.color  = color;
            var sh = go.AddComponent<Shadow>();
            sh.effectColor    = new Color(color.r, color.g, color.b, 0.40f);
            sh.effectDistance = new Vector2(0f, -3f);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition    = Selectable.Transition.None;
            var lbl = MakeTMP("L", go.transform, label, 14.5f, FontStyles.Bold, Color.white);
            lbl.alignment        = TextAlignmentOptions.Center;
            lbl.textWrappingMode = TextWrappingModes.NoWrap;
            Stretch(lbl.GetComponent<RectTransform>());
            return btn;
        }

        // Bouton « fantôme » arrondi : fond discret, contour + texte teintés.
        private Button RoundedGhostBtn(Transform parent, string label, Color color)
        {
            var go  = MakeGO("RGBtn", parent);
            var img = go.AddComponent<Image>();
            img.sprite = _sprR12;
            img.type   = Image.Type.Sliced;
            img.color  = new Color(color.r, color.g, color.b, 0.14f);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition    = Selectable.Transition.None;
            var lbl = MakeTMP("L", go.transform, label, 12.5f, FontStyles.Bold, color);
            lbl.alignment        = TextAlignmentOptions.Center;
            lbl.textWrappingMode = TextWrappingModes.NoWrap;
            Stretch(lbl.GetComponent<RectTransform>());
            return btn;
        }

        // Active/désactive visuellement le CTA principal (utilisé pour « Démarrer »).
        private static void SetBtnEnabled(Button btn, bool on)
        {
            if (btn == null) return;
            if (btn.targetGraphic is Image img)
                img.color = on ? AccentGreen : new Color(0.28f, 0.32f, 0.38f, 1f);
            var lbl = btn.GetComponentInChildren<TMP_Text>();
            if (lbl != null) lbl.color = on ? Color.white : new Color(1f, 1f, 1f, 0.40f);
        }

        // Solid filled button
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
