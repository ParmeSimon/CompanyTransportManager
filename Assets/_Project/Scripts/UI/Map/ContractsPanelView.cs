using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TransportManager.Core;
using TransportManager.Entities.Contracts;
using TransportManager.Entities.Drivers;
using TransportManager.Entities.Progression;
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
        private static readonly Color AccentRed  = new Color(1.00f, 0.42f, 0.42f, 1f);
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

        private const float PanelWidth   = 340f;   // textes agrandis (mobile)
        private const float Margin      = 12f;
        private static float TopOffset => SafeAreaUtil.HudTopHeight;
        private const float Bleed       = 16f;   // tiroir collé au bord droit : déborde hors écran (coins cachés)
        private const float CtaWrapH    = 74f;   // 10 + bouton 54 + 10
        // Hauteur fixe hors liste : en-tête 64 + filet 1 + titre de section 30 + CTA + marge 4.
        private const float FixedChromeH = 64f + 1f + 30f + CtaWrapH + 4f;

        private Canvas        _canvas;
        private ScrollRect    _cardsScroll;
        private LayoutElement _cardsScrollLe;
        private RectTransform _cardsContent;

        private float TopReserve() => TopOffset + SafeAreaUtil.Insets(_canvas).z;

        // Collé au bord droit : largeur visible = PanelWidth (+ encoche), contenu gardé dans la safe area.
        private void ApplyEdge(RectTransform rt, float top)
        {
            float insR = SafeAreaUtil.Insets(_canvas).y;
            var pos  = new Vector2(Bleed, -top);
            var size = new Vector2(PanelWidth + Bleed + insR, rt.sizeDelta.y);
            if ((rt.anchoredPosition - pos).sqrMagnitude > 0.25f) rt.anchoredPosition = pos;
            if (Mathf.Abs(rt.sizeDelta.x - size.x) > 0.5f) rt.sizeDelta = size;
            var vlg = GetComponent<VerticalLayoutGroup>();
            int padR = Mathf.RoundToInt(Bleed + insR);
            if (vlg != null && vlg.padding.right != padR) vlg.padding = new RectOffset(0, padR, 0, 4);
        }

        // Plafonne la liste à l'espace restant sous le HUD : avec des dizaines de camions,
        // les cartes défilent au lieu de pousser le panneau (et le bouton) hors de l'écran.
        private void LateUpdate()
        {
            if (_cardsScrollLe == null || _cardsContent == null) return;
            var parentRt = transform.parent as RectTransform;
            if (parentRt == null) return;

            var rt = (RectTransform)transform;
            float top = TopReserve();
            ApplyEdge(rt, top);

            float bottom = SafeAreaUtil.Insets(_canvas).w + Margin;
            float maxList = Mathf.Max(90f, parentRt.rect.height - top - bottom - FixedChromeH);
            float want = LayoutUtility.GetPreferredHeight(_cardsContent);
            float h = Mathf.Min(want, maxList);
            if (Mathf.Abs(_cardsScrollLe.preferredHeight - h) > 0.5f)
                _cardsScrollLe.preferredHeight = h;
            _cardsScroll.vertical = want > maxList + 0.5f;
        }

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
        private List<Image>           _rowFuelFillImgs    = new List<Image>();
        private List<TMP_Text>        _rowFuelTxts        = new List<TMP_Text>();
        private Button                _startBtn;
        private Button                _refuelBtn;
        private TMP_Text              _refuelLbl;

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
        private int                   _sessionMaxCapacityTons = int.MaxValue;
        private const float           FleetRangeSafetyFactor = 0.9f;   // marge 10% : routes > vol d'oiseau
        private int                   _genSessionId;

        // Rounded sprites (lazy, shared across the modal)
        private Sprite _sprR8, _sprR12, _sprR16;

        // ── Lifecycle ─────────────────────────────────────────────────────────────
        private void OnEnable()
        {
            GameEvents.OnContractStarted   += OnContractChanged;
            GameEvents.OnContractCompleted += OnContractChanged;
            _tickCoroutine = StartCoroutine(TickEvery10s());
        }

        private void OnContractChanged(ContractInstance _) => Refresh();

        private void OnDisable()
        {
            GameEvents.OnContractStarted   -= OnContractChanged;
            GameEvents.OnContractCompleted -= OnContractChanged;
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
            _canvas = GetComponentInParent<Canvas>();
            rt.anchoredPosition = new Vector2(Bleed, -TopReserve());
            rt.sizeDelta        = new Vector2(PanelWidth + Bleed, 0f);

            EnsureRoundedSprites();

            var bg = gameObject.AddComponent<Image>();
            bg.sprite        = _sprR16;
            bg.type          = Image.Type.Sliced;
            bg.color         = new Color32(0x22, 0x26, 0x2E, 255);   // = barre du haut / rail
            bg.raycastTarget = true;
            // Pas d ombre portée (effet dur) : filet clair de 1 px sur le bord gauche.
            var edge = new GameObject("Edge", typeof(RectTransform));
            edge.transform.SetParent(transform, false);
            var edgeImg = edge.AddComponent<Image>();
            edgeImg.color = new Color(1f, 1f, 1f, 0.07f); edgeImg.raycastTarget = false;
            edge.AddComponent<LayoutElement>().ignoreLayout = true;
            var edgeRt = edge.GetComponent<RectTransform>();
            edgeRt.anchorMin = new Vector2(0, 0); edgeRt.anchorMax = new Vector2(0, 1);
            edgeRt.pivot = new Vector2(0, 0.5f); edgeRt.sizeDelta = new Vector2(1, 0);
            edgeRt.anchoredPosition = Vector2.zero;

            gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var vlg = gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding               = new RectOffset(0, 0, 0, 4);
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

            // 4 — Repli : toucher l'en-tête masque la liste (le titre affiche alors « N en cours »).
            hdrBgImg.raycastTarget = true;
            var hdrBtn = hdrGo.AddComponent<Button>();
            hdrBtn.transition = Selectable.Transition.None;
            hdrBtn.onClick.AddListener(ToggleCollapsed);
            _titleLbl = titleLbl;
            _collapseLbl = MakeTMP("Fold", hdrGo.transform, "Réduire", 10f, FontStyles.Bold, TextDim);
            _collapseLbl.textWrappingMode = TextWrappingModes.NoWrap;
            _collapseLbl.alignment = TextAlignmentOptions.MidlineRight;

            // 5 — CTA « Nouveau contrat » (bas du tiroir)
            var ctaWrap = MakeGO("CtaWrap", transform);
            Le(ctaWrap, h: CtaWrapH);
            var ctaVlg = ctaWrap.AddComponent<VerticalLayoutGroup>();
            ctaVlg.padding               = new RectOffset(12, 12, 10, 10);
            ctaVlg.childForceExpandWidth  = true;
            ctaVlg.childForceExpandHeight = false;
            ctaVlg.childControlWidth      = true;
            ctaVlg.childControlHeight     = true;
            var newBtnGo  = MakeGO("NewBtn", ctaWrap.transform);
            var newBtnImg = newBtnGo.AddComponent<Image>();
            newBtnImg.sprite = _sprR12; newBtnImg.type = Image.Type.Sliced; newBtnImg.color = AccentGreen;
            Le(newBtnGo, h: 54f);
            var newBtnSh = newBtnGo.AddComponent<Shadow>();
            newBtnSh.effectColor    = new Color(AccentGreen.r, AccentGreen.g, AccentGreen.b, 0.40f);
            newBtnSh.effectDistance = new Vector2(0f, -3f);
            var newBtnComp = newBtnGo.AddComponent<Button>();
            newBtnComp.targetGraphic = newBtnImg;
            newBtnComp.onClick.AddListener(OpenGenerationModal);
            var newBtnLbl = MakeTMP("L", newBtnGo.transform, "+   Nouveau contrat", 15f, FontStyles.Bold, Color.white);
            newBtnLbl.alignment = TextAlignmentOptions.Center;
            Stretch(newBtnLbl.GetComponent<RectTransform>());

            Refresh();
        }

        // ── Repli du panneau ──────────────────────────────────────────────────────
        private bool       _collapsed;
        private TMP_Text   _titleLbl, _collapseLbl;
        private GameObject _sectionHdr;
        private int        _lastActiveCount;

        private void ToggleCollapsed()
        {
            _collapsed = !_collapsed;
            ApplyCollapsed();
        }

        private void ApplyCollapsed()
        {
            if (_sectionHdr != null) _sectionHdr.SetActive(!_collapsed);
            if (_cardsScroll != null) _cardsScroll.gameObject.SetActive(!_collapsed);
            if (_collapseLbl != null) _collapseLbl.text = _collapsed ? "Afficher" : "Réduire";
            if (_titleLbl != null)
                _titleLbl.text = _collapsed
                    ? $"Contrats  <size=60%><color=#5A8EF5>{_lastActiveCount} en cours</color></size>"
                    : "Contrats";
        }

        /// Ouvre le choix d'un nouveau contrat (bouton du dock en bas à droite).
        public void OpenNewContract() => OpenGenerationModal();

        private Transform BuildSection(Transform parent, string title, Color accent)
        {
            // Section label row
            var hdrGo = MakeGO("SHdr_" + title, parent);
            _sectionHdr = hdrGo;
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

            // Zone défilante : sa hauteur est plafonnée à l'espace dispo à l'écran (cf. LateUpdate),
            // au-delà les cartes défilent et le bouton « Nouveau contrat » reste visible.
            var scrollGo = MakeGO("CardsScroll_" + title, parent);
            var scrollImg = scrollGo.AddComponent<Image>();
            scrollImg.color = new Color(0, 0, 0, 0);          // capte le glisser pour défiler
            scrollGo.AddComponent<RectMask2D>();
            _cardsScrollLe = scrollGo.AddComponent<LayoutElement>();
            _cardsScrollLe.preferredHeight = 60f;
            _cardsScroll = scrollGo.AddComponent<ScrollRect>();
            _cardsScroll.horizontal        = false;
            _cardsScroll.movementType      = ScrollRect.MovementType.Clamped;
            _cardsScroll.scrollSensitivity = 30f;
            _cardsScroll.viewport          = (RectTransform)scrollGo.transform;

            // Contenu — hauteur fixée par les cartes enfants
            var cGo = MakeGO("Cards_" + title, scrollGo.transform);
            var cRt = cGo.GetComponent<RectTransform>();
            cRt.anchorMin = new Vector2(0f, 1f);
            cRt.anchorMax = new Vector2(1f, 1f);
            cRt.pivot     = new Vector2(0.5f, 1f);
            cRt.offsetMin = cRt.offsetMax = Vector2.zero;
            _cardsContent = cRt;
            _cardsScroll.content = cRt;
            var cImg = cGo.AddComponent<Image>();
            cImg.color         = new Color(0, 0, 0, 0);
            cImg.raycastTarget = false;
            var cvlg = cGo.AddComponent<VerticalLayoutGroup>();
            cvlg.padding               = new RectOffset(12, 12, 0, 6);
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

            _priorityDriverId = MostAtRiskDriverId();
            int activeCount = 0;
            foreach (var inst in contracts.Active)
                if (inst.status == ContractStatus.InProgress)
                { BuildActiveRow(inst); activeCount++; }

            if (_activeCountLbl != null)  _activeCountLbl.text = activeCount.ToString();
            _lastActiveCount = activeCount;
            if (_collapsed) ApplyCollapsed();
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
            var topRow = HRow(cardGo.transform, 26f);
            var routeLbl = MakeTMP("R", topRow,
                RouteSummary(def),
                14.5f, FontStyles.Bold, TextPrime);
            routeLbl.textWrappingMode = TextWrappingModes.NoWrap;
            routeLbl.overflowMode     = TextOverflowModes.Ellipsis;
            Le(routeLbl.gameObject, flexW: true, h: 22f);
            if (def.isMultiStop)
                RoundedPill(topRow, $"{def.StopCount} arrêts", AccentAmber, 78f);
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

            // Row 3b : conducteur + fatigue actuelle (+ repère pour l'energy drink)
            var drv = DriverOf(inst);
            if (drv != null)
            {
                float fNow = FatigueNow(inst, drv);
                string initial = string.IsNullOrEmpty(drv.lastName) ? "" : drv.lastName.Substring(0, 1) + ".";
                var drvRow = HRow(cardGo.transform, 24f);
                var drvLbl = MakeTMP("Drv", drvRow,
                    $"{drv.firstName} {initial}  ·  fatigue <color=#{ColorUtility.ToHtmlStringRGB(FatigueColor(fNow))}>{Mathf.RoundToInt(fNow)} %</color>",
                    11.5f, FontStyles.Normal, TextSecond);
                drvLbl.textWrappingMode = TextWrappingModes.NoWrap;
                drvLbl.overflowMode     = TextOverflowModes.Ellipsis;
                Le(drvLbl.gameObject, flexW: true, h: 16f);
                if (inst.energyDrinkUsed)
                    RoundedPill(drvRow, "Boosté", AccentAmber, 58f);
                else if (_priorityDriverId == drv.instanceId)
                    RoundedPill(drvRow, "Prioritaire", AccentRed, 72f);
            }

            // Row 4 : distance · tonnage + reward
            var botRow = HRow(cardGo.transform, 22f);
            var distLbl = MakeTMP("D", botRow,
                $"{def.distanceKm:F0} km  ·  {def.cargoTons} t",
                11.5f, FontStyles.Normal, TextDim);
            distLbl.textWrappingMode = TextWrappingModes.NoWrap;
            distLbl.overflowMode     = TextOverflowModes.Ellipsis;
            Le(distLbl.gameObject, flexW: true, h: 16f);
            var rewLbl = MakeTMP("$", botRow, $"+{def.baseReward:N0} $", 12.5f, FontStyles.Bold, AccentGreen);
            rewLbl.textWrappingMode = TextWrappingModes.NoWrap;
            rewLbl.alignment = TextAlignmentOptions.MidlineRight;
            Le(rewLbl.gameObject, h: 16f);
            rewLbl.GetComponent<LayoutElement>().minWidth = 96f;   // « +12 345 $ » en entier
        }

        // ── Conducteur & fatigue en cours de trajet ──────────────────────────────
        private string _priorityDriverId;

        private static DriverInstance DriverOf(ContractInstance inst)
        {
            if (inst == null) return null;
            var veh = ServiceLocator.Get<Systems.Fleet.FleetSystem>()?.GetById(inst.assignedVehicleInstanceId);
            if (veh == null || string.IsNullOrEmpty(veh.assignedDriverInstanceId)) return null;
            return ServiceLocator.Get<Systems.Hr.HrSystem>()?.GetHired(veh.assignedDriverInstanceId);
        }

        private static string DriverNameById(string id)
            => ServiceLocator.Get<Systems.Hr.HrSystem>()?.GetHired(id)?.firstName ?? "un autre";

        private static float FatigueNow(ContractInstance inst, DriverInstance drv)
            => Systems.Accidents.AccidentSystem.ComputeFatigueDuringTrip(drv, inst.startTimeUtcTicks, inst.DriveHours, ProgressOf(inst),
                                                                      inst.energyDrinkProgress, inst.energyDrinkRelief);

        private static float FatigueEnd(ContractInstance inst, DriverInstance drv)
            => Systems.Accidents.AccidentSystem.ComputeFatigueAtEnd(drv, inst.startTimeUtcTicks, inst.DriveHours,
                                                                 inst.energyDrinkProgress, inst.energyDrinkRelief);

        // Risque estimé sur le reste du trajet (fatigue d'arrivée, km restants).
        private static float RemainingRisk(ContractInstance inst, DriverInstance drv)
        {
            float kmLeft = inst.definition.distanceKm * (1f - ProgressOf(inst));
            return Systems.Accidents.AccidentSystem.AccidentChance(drv, FatigueEnd(inst, drv), kmLeft);
        }

        /// <summary>Conducteur en route le plus exposé (sans energy drink) : celui à booster en premier.</summary>
        private static string MostAtRiskDriverId()
        {
            var contracts = ServiceLocator.Get<ContractSystem>();
            if (contracts == null) return null;
            string best = null; float bestRisk = 0.01f;   // sous 1 % de risque : personne n'est prioritaire
            foreach (var inst in contracts.Active)
            {
                if (inst.status != ContractStatus.InProgress || inst.definition == null || inst.energyDrinkUsed) continue;
                var drv = DriverOf(inst);
                if (drv == null) continue;
                float r = RemainingRisk(inst, drv);
                if (r > bestRisk) { bestRisk = r; best = drv.instanceId; }
            }
            return best;
        }

        private static Color FatigueColor(float f)
            => f < 40f ? AccentGreen : f < 70f ? AccentAmber : AccentRed;

        private static string RiskLabel(float chance, out Color color)
        {
            if (chance < 0.03f) { color = AccentGreen; return "Risque faible"; }
            if (chance < 0.08f) { color = AccentAmber; return "Risque modéré"; }
            color = AccentRed; return "Risque élevé";
        }

        // Carte conducteur du popup : portrait, nom, fatigue actuelle puis à l'arrivée, risque.
        private void BuildDriverFatigueBlock(Transform parent, ContractInstance inst, DriverInstance drv)
        {
            float fNow = FatigueNow(inst, drv), fEnd = FatigueEnd(inst, drv);

            var box = MakeGO("DriverBox", parent);
            var boxImg = box.AddComponent<Image>();
            boxImg.sprite = _sprR12; boxImg.type = Image.Type.Sliced; boxImg.color = BgElevated;
            boxImg.raycastTarget = false;
            var hlg = box.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(12, 14, 10, 10);
            hlg.spacing = 12f;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
            hlg.childControlWidth = true; hlg.childControlHeight = true;

            // Portrait (ou pastille neutre si aucun portrait)
            var pic = MakeGO("Portrait", box.transform);
            var picImg = pic.AddComponent<Image>();
            var portrait = UI.Tabs.HrTabView.DriverPortrait(drv);
            picImg.sprite = portrait != null ? portrait : _sprR12;
            if (portrait == null) picImg.type = Image.Type.Sliced;
            picImg.color  = portrait != null ? Color.white : BgDeep;
            picImg.preserveAspect = portrait != null;
            picImg.raycastTarget  = false;
            Le(pic, w: 56f, h: 56f);

            // Nom + risque, barre de fatigue, valeurs
            var col = MakeGO("Col", box.transform);
            var colVlg = col.AddComponent<VerticalLayoutGroup>();
            colVlg.spacing = 8f;
            colVlg.childForceExpandWidth = true; colVlg.childForceExpandHeight = false;
            colVlg.childControlWidth = true; colVlg.childControlHeight = true;
            Le(col, flexW: true);

            var nameRow = HRow(col.transform, 26f);
            var nameLbl = MakeTMP("Name", nameRow, drv.FullName, 15f, FontStyles.Bold, TextPrime);
            nameLbl.textWrappingMode = TextWrappingModes.NoWrap;
            nameLbl.overflowMode     = TextOverflowModes.Ellipsis;
            Le(nameLbl.gameObject, flexW: true, h: 20f);
            if (inst.energyDrinkUsed) RoundedPill(nameRow, "Energy drink active", AccentAmber, 128f);
            else
            {
                string riskLbl = RiskLabel(RemainingRisk(inst, drv), out var riskCol);
                RoundedPill(nameRow, riskLbl, riskCol, 104f);
            }

            // Barre : partie pleine = fatigue actuelle, partie pâle = hausse à venir jusqu'à l'arrivée.
            var track = MakeGO("FTrack", col.transform);
            var tImg = track.AddComponent<Image>();
            tImg.sprite = _sprR8; tImg.type = Image.Type.Sliced; tImg.color = new Color(1f, 1f, 1f, 0.08f);
            tImg.raycastTarget = false;
            Le(track, h: 8f);
            Color fc = FatigueColor(fNow);
            AddFatigueFill(track.transform, fEnd, new Color(fc.r, fc.g, fc.b, 0.28f));
            AddFatigueFill(track.transform, Mathf.Max(fNow, 2f), fc);

            var fLbl = MakeTMP("Fat", col.transform,
                $"Fatigue <b><color=#{ColorUtility.ToHtmlStringRGB(fc)}>{Mathf.RoundToInt(fNow)} %</color></b>   ·   {Mathf.RoundToInt(fEnd)} % à l'arrivée",
                12f, FontStyles.Normal, TextSecond);
            fLbl.textWrappingMode = TextWrappingModes.NoWrap;
            Le(fLbl.gameObject, h: 16f);
        }

        private void AddFatigueFill(Transform track, float pct, Color c)
        {
            var f = MakeGO("F", track);
            var fi = f.AddComponent<Image>();
            fi.sprite = _sprR8; fi.type = Image.Type.Sliced; fi.color = c; fi.raycastTarget = false;
            var r = f.GetComponent<RectTransform>();
            r.anchorMin = Vector2.zero; r.anchorMax = new Vector2(Mathf.Clamp01(pct / 100f), 1f);
            r.offsetMin = r.offsetMax = Vector2.zero;
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
            _rowFuelFillImgs.Clear();
            _rowFuelTxts.Clear();
            _selectableVehicles.Clear();
            _refuelBtn = null;
            _refuelLbl = null;

            bool isActive = inst != null;
            EnsureRoundedSprites();

            _popup = new GameObject("ContractPopup", typeof(RectTransform));
            _popup.transform.SetParent(transform.parent, false);
            var canvas = _popup.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;   // canvas imbriqué : sinon l ordre du parent s applique (sous le HUD)
            canvas.sortingOrder = 700;       // au-dessus du HUD
            var scaler = _popup.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            TransportManager.Core.UiScale.Configure(scaler);
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
            cardRt.sizeDelta = new Vector2(600f, 0f);

            var cardVlg = cardGo.AddComponent<VerticalLayoutGroup>();
            cardVlg.padding               = new RectOffset(0, 0, 0, 18);
            cardVlg.spacing               = 0f;
            cardVlg.childAlignment        = TextAnchor.UpperCenter;
            cardVlg.childForceExpandWidth  = true;
            cardVlg.childForceExpandHeight = false;
            cardVlg.childControlWidth      = true;
            cardVlg.childControlHeight     = true;
            cardGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            // Mobile / nombreux camions dans le sélecteur : la carte défile si elle dépasse l'écran.
            UI.Common.ScrollFit.Wrap(cardRt, 20f);

            // ── 1 — Header : eyebrow + statut, pastille difficulté, fermeture ───────
            var cHdrGo = MakeGO("CHdr", cardGo.transform);
            var cHdrImg = cHdrGo.AddComponent<Image>();
            cHdrImg.color         = new Color(0, 0, 0, 0);
            cHdrImg.raycastTarget = false;
            Le(cHdrGo, h: 66f);   // bouton Retour 44 + marges
            var cHhlg = cHdrGo.AddComponent<HorizontalLayoutGroup>();
            cHhlg.padding               = new RectOffset(22, 12, 12, 8);
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
            Le(eyebrow.gameObject, h: 16f);
            eyebrow.textWrappingMode = TextWrappingModes.NoWrap;
            Le(eyebrow.gameObject, flexW: true, h: 34f);

            if (isActive)
                RoundedPill(cHdrGo.transform, FormatRemaining(_popupInst), AccentBlue, 64f);
            else
                RoundedPill(cHdrGo.transform, DiffNames[(int)d.difficulty], accentColor, 78f);

            // « Retour » en tête de la fiche (plus de croix).
            var back = UI.Common.BackButton.Create(cHdrGo.transform, ClosePopup);
            back.transform.SetAsFirstSibling();

            // ── 2 — Hero trajet : origine → destination + marchandise ───────────────
            var routeBlock = MakeGO("RouteBlock", cardGo.transform);
            var rbImg = routeBlock.AddComponent<Image>();
            rbImg.color         = new Color(0, 0, 0, 0);
            rbImg.raycastTarget = false;
            var rbVlg = routeBlock.AddComponent<VerticalLayoutGroup>();
            rbVlg.padding               = new RectOffset(22, 22, 6, 16);
            rbVlg.spacing               = 10f;
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
                var routeRow = HRow(routeBlock.transform, 66f);
                RouteEnd(routeRow, "DÉPART", stopNames[0], false);
                var arrowLbl = MakeTMP("Arrow", routeRow, "→", 26f, FontStyles.Bold, accentColor);
                arrowLbl.alignment = TextAlignmentOptions.Center;
                Le(arrowLbl.gameObject, w: 48f, h: 66f);
                RouteEnd(routeRow, "ARRIVÉE", stopNames[1], true);
            }

            // Progression du trajet en cours : barre + % + heure d'arrivée.
            if (isActive && _popupInst != null)
            {
                long total = _popupInst.completionTimeUtcTicks - _popupInst.startTimeUtcTicks;
                float ratio = total > 0 ? Mathf.Clamp01((float)(DateTime.UtcNow.Ticks - _popupInst.startTimeUtcTicks) / total) : 1f;
                var barBg = MakeGO("ProgBg", routeBlock.transform);
                var bbImg = barBg.AddComponent<Image>();
                bbImg.sprite = _sprR8; bbImg.type = Image.Type.Sliced; bbImg.color = BgElevated;
                Le(barBg, h: 10f);
                var fill = MakeGO("ProgFill", barBg.transform);
                var fImg = fill.AddComponent<Image>();
                fImg.sprite = _sprR8; fImg.type = Image.Type.Sliced; fImg.color = accentColor;
                var fRt = fill.GetComponent<RectTransform>();
                fRt.anchorMin = Vector2.zero; fRt.anchorMax = new Vector2(Mathf.Max(0.02f, ratio), 1f);
                fRt.offsetMin = Vector2.zero; fRt.offsetMax = Vector2.zero;
                string eta = _popupInst.CompletionTimeUtc.ToLocalTime().ToString("HH:mm");
                var progLbl = MakeTMP("Prog", routeBlock.transform,
                    $"{Mathf.RoundToInt(ratio * 100f)} % parcouru   ·   arrivée à {eta}", 13f, FontStyles.Bold, TextSecond);
                progLbl.alignment = TextAlignmentOptions.Center;
                Le(progLbl.gameObject, h: 20f);
            }

            var cargoRow = HRow(routeBlock.transform, 18f);
            var cargoTag = MakeTMP("CT", cargoRow, "MARCHANDISE", 10f, FontStyles.Bold, TextDim);
            cargoTag.characterSpacing = 1.5f;
            cargoTag.textWrappingMode = TextWrappingModes.NoWrap;
            Le(cargoTag.gameObject, h: 16f);   // largeur = texte (plus de chevauchement)
            var cargoLbl = MakeTMP("Cargo", cargoRow,
                string.IsNullOrEmpty(d.cargoLabel) ? "Marchandises diverses" : d.cargoLabel,
                14f, FontStyles.Normal, TextPrime);
            cargoLbl.textWrappingMode = TextWrappingModes.NoWrap;
            cargoLbl.overflowMode     = TextOverflowModes.Ellipsis;
            Le(cargoLbl.gameObject, flexW: true, h: 16f);
            cargoRow.GetComponent<HorizontalLayoutGroup>().spacing = 12f;

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

            // Une seule rangée de 4 chiffres (la difficulté est déjà dans la pastille d en-tête).
            RoundedStatCell(gridGo.transform, $"{d.distanceKm:N0} km", "DISTANCE");
            RoundedStatCell(gridGo.transform, $"{d.cargoTons} t", "CHARGE");
            RoundedStatCell(gridGo.transform, $"+{d.baseReward:N0} $", "RÉCOMPENSE", AccentGreen);
            RoundedStatCell(gridGo.transform, $"+{XpCurve.CompanyXpReward(d.distanceKm, d.difficulty)}", "XP", AccentBlue);

            var gridGo2 = MakeGO("StatsGrid2", gridWrap.transform);
            var gg2Img = gridGo2.AddComponent<Image>();
            gg2Img.color = new Color(0, 0, 0, 0); gg2Img.raycastTarget = false;
            var grid2 = gridGo2.AddComponent<HorizontalLayoutGroup>();
            grid2.spacing = 8f; grid2.childForceExpandWidth = true; grid2.childForceExpandHeight = false;
            grid2.childControlWidth = true; grid2.childControlHeight = true;
            gridGo2.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            gwVlg.spacing = 8f;

            if (isActive)
            {
                // Une fois parti, chargement et temps restant (déjà en en-tête) n'aident plus :
                // on montre plutôt l'état du conducteur pour décider qui reçoit une energy drink.
                gridGo2.SetActive(false);
                var drvActive = DriverOf(_popupInst);
                if (drvActive != null) BuildDriverFatigueBlock(gridWrap.transform, _popupInst, drvActive);
            }
            else gridGo2.SetActive(false);

            // ── Note ponctualité (E1) ───────────────────────────────────────────────
            string punctNote;
            if (isActive)
            {
                bool onTime = _popupInst.deadlineTimeUtcTicks <= 0
                              || _popupInst.completionTimeUtcTicks <= _popupInst.deadlineTimeUtcTicks;
                int bonusPct = Mathf.RoundToInt((0.15f + Systems.Progression.SkillTreeSystem.PctOf(SkillEffectType.OnTimeBonusIncrease)) * 100f);
                punctNote = onTime
                    ? $"<color=#3DC96E>Livraison à l'heure · bonus +{bonusPct} %</color>"
                    : "<color=#ff6b6b>Livraison en retard · pénalité -20 %</color>";
            }
            else
            {
                punctNote = "Livré à temps : <color=#3DC96E>+15 %</color>   ·   en retard : <color=#ff6b6b>-20 %</color>";
            }
            var pn = MakeTMP("Punct", cardGo.transform, punctNote, 13f, FontStyles.Normal, TextSecond);
            pn.alignment = TextAlignmentOptions.Center;
            Le(pn.gameObject, h: 28f);

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
                // Ravitaillement du camion sélectionné (n'apparaît que si nécessaire).
                _refuelBtn = RoundedGhostBtn(footer.transform, "⛽ Faire le plein du camion", AccentAmber);
                Le(_refuelBtn.gameObject, flexW: true, h: 42f);
                _refuelLbl = _refuelBtn.GetComponentInChildren<TMP_Text>();
                _refuelBtn.onClick.AddListener(OnRefuelSelected);
                _refuelBtn.gameObject.SetActive(false);

                _startBtn = RoundedSolidBtn(footer.transform, "Démarrer le trajet  →", AccentGreen);
                Le(_startBtn.gameObject, flexW: true, h: 48f);
                _startBtn.onClick.AddListener(OnStartContract);
                _startBtn.interactable = false;
                SetBtnEnabled(_startBtn, false);
            }

            // ── Energy drink : booste le conducteur en mission (réduit fatigue + annule l'accident) ──
            if (isActive)
            {
                var fleetSys = ServiceLocator.Get<Systems.Fleet.FleetSystem>();
                var veh      = fleetSys?.GetById(_popupInst.assignedVehicleInstanceId);
                string drvId = veh?.assignedDriverInstanceId;
                if (!string.IsNullOrEmpty(drvId))
                {
                    var shop      = ServiceLocator.Get<Systems.Shop.ShopSystem>();
                    var contracts = ServiceLocator.Get<ContractSystem>();
                    var eBtn = RoundedGhostBtn(footer.transform, "", AccentAmber);
                    Le(eBtn.gameObject, flexW: true, h: 42f);
                    var eLbl = eBtn.GetComponentInChildren<TMP_Text>();
                    void RenderEnergy()
                    {
                        int count  = shop?.EnergyDrinkCount ?? 0;
                        bool doped = contracts != null && contracts.DriverHasEnergyDrinkActive(drvId);
                        if (doped)       eLbl.text = "Conducteur dopé · accident évité";
                        else if (count <= 0) eLbl.text = "Aucune energy drink en stock";
                        else
                        {
                            var prio = MostAtRiskDriverId();
                            eLbl.text = prio != null && prio != drvId
                                ? $"Donner une energy drink  ({count})  ·  {DriverNameById(prio)} est plus exposé"
                                : $"Donner une energy drink  ({count})";
                        }
                        eBtn.interactable = !doped && count > 0;
                    }
                    RenderEnergy();
                    eBtn.onClick.AddListener(() =>
                    {
                        if (contracts != null && contracts.TryGiveEnergyDrinkToDriver(drvId))
                        {
                            var boostedInst = _popupInst;   // fatigue baissée → on redessine fiche + liste
                            Refresh();
                            OpenPopup(null, boostedInst);
                        }
                    });
                }
            }

            var captureDef = _popupDef;
            var routeMapBtn = RoundedGhostBtn(footer.transform, "Voir la route sur la carte", AccentBlue);
            Le(routeMapBtn.gameObject, flexW: true, h: 42f);
            routeMapBtn.onClick.AddListener(() => { ClosePopup(); GameEvents.RaiseShowContractRoute(captureDef); });

            // Rouverture de la même offre : on resélectionne le camion choisi auparavant.
            if (!isActive && _rememberedDef == d && !string.IsNullOrEmpty(_rememberedVehicleId))
            {
                int again = _selectableVehicles.FindIndex(v => v.instanceId == _rememberedVehicleId);
                if (again >= 0) SelectVehicle(again);
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
                        "Tu n'as aucun camion. Achète-en un dans l'onglet Camions, puis affecte-lui un conducteur (onglet Camions).",
                        10f, FontStyles.Normal, TextSecond);
                    m.textWrappingMode = TextWrappingModes.Normal;
                    Le(m.gameObject, flexW: true, h: 30f);
                }
                else
                {
                    // Regroupe par raison : « 12 camions — déjà en mission » plutôt qu'une ligne par camion.
                    var counts = new Dictionary<string, int>();
                    var order  = new List<string>();
                    foreach (var v in gm.Save.vehicles)
                    {
                        var data   = catalog?.GetById(v.vehicleDataId);
                        string why = VehicleIneligibleReason(v, data, def);
                        if (why == null) continue;
                        if (!counts.ContainsKey(why)) { counts[why] = 0; order.Add(why); }
                        counts[why]++;
                    }
                    order.Sort((x, y) => counts[y].CompareTo(counts[x]));

                    var sb = new System.Text.StringBuilder();
                    int shown = 0;
                    foreach (var why in order)
                    {
                        if (shown == 3) { sb.Append($"\n• et {order.Count - 3} autre(s) raison(s)"); break; }
                        int n = counts[why];
                        if (sb.Length > 0) sb.Append('\n');
                        sb.Append($"• {n} camion{(n > 1 ? "s" : "")} : {why}");
                        shown++;
                    }
                    var m = MakeTMP("M", box.transform, sb.ToString(), 10f, FontStyles.Normal, TextSecond);
                    m.textWrappingMode = TextWrappingModes.Normal;

                    bool allBusy = order.Count == 1 && order[0].Contains("mission");
                    var hint = MakeTMP("H", box.transform,
                        allBusy ? "Attends le retour d'un camion, ou achète-en un autre dans l'onglet Camions."
                                : "Gère tes camions et conducteurs dans les onglets Camions et Pilote.",
                        10f, FontStyles.Italic, TextDim);
                    hint.textWrappingMode = TextWrappingModes.Normal;
                }
                return;
            }

            Transform rowsParent = parent;
            if (_selectableVehicles.Count > 3)
            {
                var vp = MakeGO("VehScroll", parent);
                var vpImg = vp.AddComponent<Image>(); vpImg.color = new Color(0, 0, 0, 0);
                vp.AddComponent<RectMask2D>();
                Le(vp, h: 3.5f * 66f);   // 3 lignes et demie visibles : on devine qu'il y en a d'autres
                var sr = vp.AddComponent<ScrollRect>();
                sr.horizontal = false; sr.scrollSensitivity = 30f;
                sr.movementType = ScrollRect.MovementType.Clamped;
                sr.viewport = (RectTransform)vp.transform;
                var content = MakeGO("Rows", vp.transform);
                var cRt = content.GetComponent<RectTransform>();
                cRt.anchorMin = new Vector2(0, 1); cRt.anchorMax = new Vector2(1, 1);
                cRt.pivot = new Vector2(0.5f, 1f); cRt.offsetMin = Vector2.zero; cRt.offsetMax = Vector2.zero;
                var cVlg = content.AddComponent<VerticalLayoutGroup>();
                cVlg.spacing = 7f;
                cVlg.childForceExpandWidth = true; cVlg.childForceExpandHeight = false;
                cVlg.childControlWidth = true; cVlg.childControlHeight = true;
                content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                sr.content = cRt;
                rowsParent = content.transform;
            }

            for (int i = 0; i < _selectableVehicles.Count; i++)
            {
                int idx  = i;
                var v    = _selectableVehicles[i];
                var data = catalog?.GetById(v.vehicleDataId);

                float tank      = data != null ? data.TankCapacity : 0f;
                float fuelRatio = tank > 0f ? Mathf.Clamp01(v.currentFuelLiters / tank) : 0f;
                float fullRange = data != null ? data.MaxRangeKm() : 0f;
                bool  canReach  = fullRange >= def.distanceKm;
                var   fuelCol   = fuelRatio > 0.5f ? AccentGreen : fuelRatio > 0.2f ? AccentAmber : DangerRed;

                var rowGo  = MakeGO("VRow", rowsParent);
                var rowImg = rowGo.AddComponent<Image>();
                rowImg.sprite = _sprR8;
                rowImg.type   = Image.Type.Sliced;
                rowImg.color  = BgElevated;
                _vehicleRowBgs.Add(rowImg);
                Le(rowGo, h: 58f);

                var rowBtn = rowGo.AddComponent<Button>();
                rowBtn.targetGraphic = rowImg;
                rowBtn.transition    = Selectable.Transition.None;
                rowBtn.onClick.AddListener(() => SelectVehicle(idx));

                var rowVlg = rowGo.AddComponent<VerticalLayoutGroup>();
                rowVlg.padding               = new RectOffset(14, 14, 8, 8);
                rowVlg.spacing               = 5f;
                rowVlg.childAlignment        = TextAnchor.MiddleLeft;
                rowVlg.childForceExpandWidth  = true;
                rowVlg.childForceExpandHeight = false;
                rowVlg.childControlWidth      = true;
                rowVlg.childControlHeight     = true;

                // Ligne 1 : pastille + nom + capacité
                var top = HRow(rowGo.transform, 18f);
                var dot = MakeGO("Dot", top);
                dot.AddComponent<Image>().color = BorderFaint;
                Le(dot, w: 6f, h: 6f);

                var nameLbl = MakeTMP("N", top,
                    data?.displayName ?? v.vehicleDataId, 11.5f, FontStyles.Bold, TextPrime);
                nameLbl.textWrappingMode = TextWrappingModes.NoWrap;
                nameLbl.overflowMode     = TextOverflowModes.Ellipsis;
                Le(nameLbl.gameObject, flexW: true, h: 18f);

                var capLbl = MakeTMP("C", top, $"{data?.capacity ?? 0} t", 10.5f, FontStyles.Bold, TextSecond);
                capLbl.alignment = TextAlignmentOptions.MidlineRight;
                Le(capLbl.gameObject, w: 40f, h: 18f);

                // Ligne 2 : jauge carburant + autonomie
                var bot = HRow(rowGo.transform, 16f);
                var fuelFill = BuildMiniFuelBar(bot, fuelRatio, fuelCol, 84f);
                _rowFuelFillImgs.Add(fuelFill);

                var fuelTxt = MakeTMP("F", bot, $"{Mathf.RoundToInt(fuelRatio * 100f)}%", 10f, FontStyles.Bold, fuelCol);
                fuelTxt.textWrappingMode = TextWrappingModes.NoWrap;
                Le(fuelTxt.gameObject, w: 34f, h: 16f);
                _rowFuelTxts.Add(fuelTxt);

                var rangeTxt = MakeTMP("R", bot,
                    canReach ? $"autonomie {fullRange:F0} km" : $"⚠ {fullRange:F0} km < {def.distanceKm:F0} km",
                    10f, FontStyles.Normal, canReach ? TextDim : DangerRed);
                rangeTxt.textWrappingMode = TextWrappingModes.NoWrap;
                rangeTxt.overflowMode     = TextOverflowModes.Ellipsis;
                rangeTxt.alignment        = TextAlignmentOptions.MidlineRight;
                Le(rangeTxt.gameObject, flexW: true, h: 16f);
            }
        }

        // Petite jauge carburant horizontale (fond + remplissage proportionnel).
        // Renvoie l'Image de remplissage pour pouvoir la rafraîchir après ravitaillement.
        private Image BuildMiniFuelBar(Transform parent, float ratio, Color fill, float width)
        {
            var track = MakeGO("FuelTrack", parent);
            var trackImg = track.AddComponent<Image>();
            trackImg.sprite        = _sprR8;
            trackImg.type          = Image.Type.Sliced;
            trackImg.color         = new Color(1f, 1f, 1f, 0.10f);
            trackImg.raycastTarget = false;
            Le(track, w: width, h: 7f);

            var fillGo = MakeGO("Fill", track.transform);
            var fillImg = fillGo.AddComponent<Image>();
            fillImg.sprite        = _sprR8;
            fillImg.type          = Image.Type.Sliced;
            fillImg.color         = fill;
            fillImg.raycastTarget = false;
            var fRt = fillGo.GetComponent<RectTransform>();
            fRt.anchorMin = new Vector2(0f, 0f);
            fRt.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1f);
            fRt.offsetMin = Vector2.zero;
            fRt.offsetMax = Vector2.zero;
            return fillImg;
        }

        // Raison pour laquelle un camion n'est pas éligible à ce contrat (null = éligible).
        private static string VehicleIneligibleReason(VehicleInstance v, VehicleData data, ContractData def)
        {
            if (data == null) return "modèle inconnu";
            if (string.IsNullOrEmpty(v.assignedDriverInstanceId)) return "sans conducteur (affecte-en un dans Camions)";
            switch (v.status)
            {
                case VehicleStatus.OnContract:    return "déjà en mission";
                case VehicleStatus.InMaintenance: return "en maintenance";
                case VehicleStatus.Immobilized:   return "immobilisé";
            }
            if (data.capacity < def.requiredCapacity)
                return $"trop petit ({def.requiredCapacity} t requis)";
            return null;
        }

        // Camion choisi pour un contrat : retenu si on ferme la fiche (ex. « Voir la route ») puis la rouvre.
        private ContractData _rememberedDef;
        private string       _rememberedVehicleId;

        private void SelectVehicle(int idx)
        {
            _selectedVehicleIdx = idx;
            if (idx >= 0 && idx < _selectableVehicles.Count)
            {
                _rememberedDef       = _popupDef;
                _rememberedVehicleId = _selectableVehicles[idx].instanceId;
            }
            for (int i = 0; i < _vehicleRowBgs.Count; i++)
                _vehicleRowBgs[i].color = i == idx
                    ? new Color(AccentGreen.r, AccentGreen.g, AccentGreen.b, 0.20f)
                    : BgElevated;
            UpdateStartState();
        }

        // Active le bouton « Démarrer » UNIQUEMENT si le camion sélectionné peut
        // réellement faire le trajet (réservoir assez grand + carburant suffisant, en
        // tenant compte du complément possible depuis la station). Sinon il reste grisé
        // et le bouton de ravitaillement apparaît.
        private void UpdateStartState()
        {
            if (_startBtn == null) return;

            void Block()
            {
                _startBtn.interactable = false;
                SetBtnEnabled(_startBtn, false);
                if (_refuelBtn != null) _refuelBtn.gameObject.SetActive(false);
            }

            if (_selectedVehicleIdx < 0 || _selectedVehicleIdx >= _selectableVehicles.Count) { Block(); return; }

            var catalog   = ServiceLocator.Get<VehicleCatalog>();
            var contracts = ServiceLocator.Get<ContractSystem>();
            var fuel      = ServiceLocator.Get<Systems.Fuel.FuelSystem>();
            var v         = _selectableVehicles[_selectedVehicleIdx];
            var data      = catalog?.GetById(v.vehicleDataId);
            if (data == null || contracts == null || _popupDef == null) { Block(); return; }

            float needed   = contracts.FuelNeededForContract(_popupDef, v, data);
            float tankCap  = data.TankCapacity;
            bool  tankOk   = tankCap >= needed;                                   // réservoir assez grand une fois plein
            float headroom = Mathf.Max(0f, tankCap - v.currentFuelLiters);
            float station  = fuel?.CurrentLiters ?? 0f;
            float topUp    = Mathf.Min(headroom, station);
            bool  hasNow   = v.currentFuelLiters >= needed;
            bool  reachable = v.currentFuelLiters + topUp >= needed;             // après complément station auto

            bool startable = tankOk && reachable;
            _startBtn.interactable = startable;
            SetBtnEnabled(_startBtn, startable);

            // Le ravitaillement est proposé tant que le camion n'a pas déjà l'essence requise.
            if (_refuelBtn != null)
            {
                bool showRefuel = tankOk && !hasNow;
                _refuelBtn.gameObject.SetActive(showRefuel);
                if (showRefuel && _refuelLbl != null)
                {
                    if (station <= 0.01f)
                        _refuelLbl.text = "⛽ Station vide · commander du carburant";
                    else if (!reachable)
                        _refuelLbl.text = "⛽ Carburant station insuffisant · commander";
                    else
                        _refuelLbl.text = "⛽ Faire le plein du camion";
                }
            }
        }

        // Remplit le réservoir du camion sélectionné depuis le stock de la station
        // (carburant déjà payé). Station vide → on ouvre la commande de carburant.
        private void OnRefuelSelected()
        {
            if (_selectedVehicleIdx < 0 || _selectedVehicleIdx >= _selectableVehicles.Count) return;
            var catalog = ServiceLocator.Get<VehicleCatalog>();
            var fuel    = ServiceLocator.Get<Systems.Fuel.FuelSystem>();
            var v       = _selectableVehicles[_selectedVehicleIdx];
            var data    = catalog?.GetById(v.vehicleDataId);
            if (fuel == null || data == null) return;

            if (fuel.CurrentLiters <= 0.01f)
            {
                ClosePopup();
                UI.Tabs.FuelOrderPopupView.Show();
                return;
            }

            float before = v.currentFuelLiters;
            fuel.TryFillVehicleTank(v, data);
            if (v.currentFuelLiters > before + 0.01f)
            {
                GameManager.Instance?.SaveNow();
                RefreshSelectedVehicleFuel();
                UpdateStartState();
            }
        }

        // Met à jour la jauge + le pourcentage de carburant de la ligne sélectionnée.
        private void RefreshSelectedVehicleFuel()
        {
            int i = _selectedVehicleIdx;
            if (i < 0 || i >= _selectableVehicles.Count) return;
            var data = ServiceLocator.Get<VehicleCatalog>()?.GetById(_selectableVehicles[i].vehicleDataId);
            float tank  = data != null ? data.TankCapacity : 0f;
            float ratio = tank > 0f ? Mathf.Clamp01(_selectableVehicles[i].currentFuelLiters / tank) : 0f;
            var col = ratio > 0.5f ? AccentGreen : ratio > 0.2f ? AccentAmber : DangerRed;

            if (i < _rowFuelFillImgs.Count && _rowFuelFillImgs[i] != null)
            {
                var rt = _rowFuelFillImgs[i].GetComponent<RectTransform>();
                rt.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1f);
                _rowFuelFillImgs[i].color = col;
            }
            if (i < _rowFuelTxts.Count && _rowFuelTxts[i] != null)
            {
                _rowFuelTxts[i].text  = $"{Mathf.RoundToInt(ratio * 100f)}%";
                _rowFuelTxts[i].color = col;
            }
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

            // Capacité de charge du plus gros camion : on ne génère pas de contrat plus lourd.
            int maxCap = 0;
            foreach (var data in fleet) if (data.capacity > maxCap) maxCap = data.capacity;
            _sessionMaxCapacityTons = maxCap > 0 ? maxCap : int.MaxValue;

            // 3 contrats de base + « Carnet de commandes » (arbre Commercial).
            int slots = _sessionDiffs.Length;
            _genResults      = new ContractData[slots];
            _genRouteLabels  = new TMP_Text[slots];
            _genCargoLabels  = new TMP_Text[slots];
            _genInfoHosts    = new Transform[slots];
            _genRewardLabels = new TMP_Text[slots];
            _genTakeBtns     = new Button[slots];
            _genBtnLabels    = new TMP_Text[slots];
            _genBtnImgs      = new Image[slots];
            _genSessionId++;

            _genGroups   = new CanvasGroup[slots];
            _genCardBtns = new Button[slots];
            _genXpLabels = new TMP_Text[slots];

            _genModal = new GameObject("GenModal", typeof(RectTransform));
            _genModal.transform.SetParent(transform.parent, false);
            var canvas = _genModal.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 700;   // au-dessus du HUD : la modale a toute l'attention
            var scaler = _genModal.AddComponent<CanvasScaler>();
            TransportManager.Core.UiScale.Configure(scaler);
            _genModal.AddComponent<GraphicRaycaster>();
            Stretch(_genModal.GetComponent<RectTransform>());

            // Voile plein écran (toucher hors du panneau = fermer)
            var scrim = MakeGO("Scrim", _genModal.transform);
            var scrimImg = scrim.AddComponent<Image>();
            scrimImg.color = new Color(0.03f, 0.04f, 0.06f, 0.80f);
            Stretch(scrim.GetComponent<RectTransform>());
            var scrimBtn = scrim.AddComponent<Button>();
            scrimBtn.transition = Selectable.Transition.None;
            scrimBtn.onClick.AddListener(CloseGenModal);

            // ── Panneau « carnet de commandes » ─────────────────────────────────────
            var panel = MakeGO("Board", _genModal.transform);
            var pImg = panel.AddComponent<Image>();
            pImg.sprite = _sprR16; pImg.type = Image.Type.Sliced;
            pImg.color = new Color32(0x22, 0x26, 0x2E, 255);
            var pOl = panel.AddComponent<Outline>();
            pOl.effectColor = BorderFaint; pOl.effectDistance = new Vector2(1f, -1f);
            var pRt = panel.GetComponent<RectTransform>();
            pRt.anchorMin = pRt.anchorMax = new Vector2(0.5f, 0.5f);
            pRt.pivot = new Vector2(0.5f, 0.5f);
            pRt.sizeDelta = new Vector2(1120f, 0f);
            var pVlg = panel.AddComponent<VerticalLayoutGroup>();
            pVlg.padding = new RectOffset(32, 32, 28, 28);
            pVlg.spacing = 22f;
            pVlg.childForceExpandWidth = true; pVlg.childForceExpandHeight = false;
            pVlg.childControlWidth = true; pVlg.childControlHeight = true;
            panel.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            panel.AddComponent<Button>().transition = Selectable.Transition.None;   // bloque la fermeture

            // En-tête : titre + sous-titre (adapté à la flotte) | fermer
            var hdr = HRow(panel.transform, 92f);
            hdr.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleLeft;
            var hCol = MakeGO("HCol", hdr);
            var hVlg = hCol.AddComponent<VerticalLayoutGroup>();
            hVlg.spacing = 6f; hVlg.childForceExpandWidth = true; hVlg.childForceExpandHeight = false;
            hVlg.childAlignment = TextAnchor.MiddleLeft;
            hVlg.childControlWidth = true; hVlg.childControlHeight = true;
            Le(hCol, flexW: true);
            var eyebrow = MakeTMP("Eye", hCol.transform, "CARNET DE COMMANDES", 10f, FontStyles.Bold, AccentBlue);
            eyebrow.characterSpacing = 2.5f;
            var title = MakeTMP("Title", hCol.transform, "Choisis ton prochain contrat", 22f, FontStyles.Bold, TextPrime);
            title.textWrappingMode = TextWrappingModes.NoWrap;
            Le(title.gameObject, h: 34f);
            if (hasFleet)
            {
                string cap = _sessionMaxCapacityTons < int.MaxValue ? $"jusqu'à {_sessionMaxCapacityTons} t" : "";
                string rng = _sessionMaxRangeKm < float.MaxValue ? $"{_sessionMaxRangeKm:F0} km max" : "";
                string fit = cap.Length > 0 && rng.Length > 0 ? cap + "  ·  " + rng : cap + rng;
                var sub = MakeTMP("Sub", hCol.transform, $"Offres adaptées à ta flotte{(fit.Length > 0 ? " : " + fit : "")}", 12f, FontStyles.Normal, TextSecond);
                sub.textWrappingMode = TextWrappingModes.NoWrap;
                Le(sub.gameObject, h: 18f);
            }
            // « Retour » à gauche du titre (plus de croix).
            UI.Common.BackButton.Create(hdr, CloseGenModal).transform.SetAsFirstSibling();

            // Pas de camion → rien à générer.
            if (!hasFleet)
            {
                BuildNoFleetMessage(panel.transform);
                TransportManager.UI.Common.ScrollFit.Wrap(pRt, 24f);
                return;
            }

            // Liste des offres (une ligne = un « ticket »)
            var list = MakeGO("Offers", panel.transform);
            var lVlg = list.AddComponent<VerticalLayoutGroup>();
            lVlg.spacing = 14f;
            lVlg.childForceExpandWidth = true; lVlg.childForceExpandHeight = false;
            lVlg.childControlWidth = true; lVlg.childControlHeight = true;

            for (int i = 0; i < slots; i++)
                BuildGenSlot(list.transform, i);

            // Pied : conseil + nouvelles offres
            var foot = HRow(panel.transform, 40f);
            var hint = MakeTMP("Hint", foot, "Les offres non retenues disparaissent à la fermeture.", 12f, FontStyles.Normal, TextDim);
            hint.textWrappingMode = TextWrappingModes.NoWrap;
            Le(hint.gameObject, flexW: true, h: 40f);
            var again = MakeGO("Again", foot);
            var aImg = again.AddComponent<Image>();
            aImg.sprite = _sprR12; aImg.type = Image.Type.Sliced; aImg.color = BgElevated;
            var aBtn = again.AddComponent<Button>();
            aBtn.targetGraphic = aImg;
            aBtn.onClick.AddListener(OpenGenerationModal);
            Le(again, w: 170f, h: 40f);
            var aLbl = MakeTMP("L", again.transform, "Autres offres", 13f, FontStyles.Bold, TextPrime);
            aLbl.alignment = TextAlignmentOptions.Center;
            Stretch(aLbl.GetComponent<RectTransform>());

            // Mobile : le panneau défile s'il dépasse l'écran (nombreuses offres).
            TransportManager.UI.Common.ScrollFit.Wrap(pRt, 24f);

            int sid = _genSessionId;
            for (int i = 0; i < slots; i++)
            {
                StartCoroutine(PulseWhileLoading(i, sid));
                GenerateSingleAsync(i, sid);
            }
        }

        private CanvasGroup[] _genGroups = new CanvasGroup[3];

        // Chargement : la ligne « respire » tant que l'offre n'est pas arrivée.
        private IEnumerator PulseWhileLoading(int slotIdx, int sessionId)
        {
            while (_genModal != null && _genSessionId == sessionId && _genResults[slotIdx] == null
                   && _genTakeBtns[slotIdx] != null && !_genTakeBtns[slotIdx].interactable)
            {
                var cg = _genGroups[slotIdx];
                if (cg != null) cg.alpha = 0.55f + 0.25f * Mathf.Sin(Time.unscaledTime * 4f);
                yield return null;
            }
            if (_genGroups != null && slotIdx < _genGroups.Length && _genGroups[slotIdx] != null) _genGroups[slotIdx].alpha = 1f;
        }

        // Une offre = un ticket horizontal :
        // [bande couleur] [difficulté · trajet · marchandise] [4 chiffres clés] [récompense + Choisir]
        private void BuildGenSlot(Transform parent, int slotIdx)
        {
            var diff   = _sessionDiffs[slotIdx];
            var accent = DiffColors[(int)diff];

            var card    = MakeGO("Offer" + slotIdx, parent);
            var cardImg = card.AddComponent<Image>();
            cardImg.sprite = _sprR16; cardImg.type = Image.Type.Sliced; cardImg.color = BgElevated;
            _genGroups[slotIdx] = card.AddComponent<CanvasGroup>();
            var cardBtn = card.AddComponent<Button>();      // toute la ligne est cliquable
            cardBtn.targetGraphic = cardImg;
            cardBtn.interactable = false;
            var hlg = card.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(0, 22, 0, 0);
            hlg.spacing = 24f;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            Le(card, h: 136f);

            // Bande de difficulté
            var strip = MakeGO("Strip", card.transform);
            var sImg = strip.AddComponent<Image>();
            sImg.sprite = _sprR8; sImg.type = Image.Type.Sliced; sImg.color = accent; sImg.raycastTarget = false;
            Le(strip, w: 6f, h: 136f);

            // Colonne trajet
            var colA = MakeGO("Route", card.transform);
            var aVlg = colA.AddComponent<VerticalLayoutGroup>();
            aVlg.spacing = 8f; aVlg.childAlignment = TextAnchor.MiddleLeft;
            aVlg.childForceExpandWidth = true; aVlg.childForceExpandHeight = false;
            aVlg.childControlWidth = true; aVlg.childControlHeight = true;
            Le(colA, flexW: true);
            var chips = HRow(colA.transform, 22f);
            RoundedPill(chips, DiffNames[(int)diff], accent, 60f).interactable = false;
            var routeLbl = MakeTMP("Dest", colA.transform, "Recherche d'un client…", 19f, FontStyles.Bold, TextSecond);
            routeLbl.textWrappingMode = TextWrappingModes.NoWrap;
            routeLbl.overflowMode     = TextOverflowModes.Ellipsis;
            _genRouteLabels[slotIdx] = routeLbl;
            var cargoLbl = MakeTMP("Cargo", colA.transform, "", 12.5f, FontStyles.Normal, TextSecond);
            cargoLbl.textWrappingMode = TextWrappingModes.NoWrap;
            cargoLbl.overflowMode     = TextOverflowModes.Ellipsis;
            _genCargoLabels[slotIdx] = cargoLbl;

            // Chiffres clés (remplis à la réception)
            var stats = MakeGO("Stats", card.transform);
            var stHlg = stats.AddComponent<HorizontalLayoutGroup>();
            stHlg.spacing = 10f; stHlg.childAlignment = TextAnchor.MiddleCenter;
            stHlg.childForceExpandWidth = true; stHlg.childForceExpandHeight = false;
            stHlg.childControlWidth = true; stHlg.childControlHeight = true;
            Le(stats, w: 330f);
            _genInfoHosts[slotIdx] = stats.transform;

            // Récompense + action
            var colC = MakeGO("Reward", card.transform);
            var cVlg = colC.AddComponent<VerticalLayoutGroup>();
            cVlg.spacing = 4f; cVlg.childAlignment = TextAnchor.MiddleRight;
            cVlg.childForceExpandWidth = true; cVlg.childForceExpandHeight = false;
            cVlg.childControlWidth = true; cVlg.childControlHeight = true;
            Le(colC, w: 180f);
            var rewardLbl = MakeTMP("Rew", colC.transform, "", 24f, FontStyles.Bold, AccentGreen);
            rewardLbl.alignment = TextAlignmentOptions.MidlineRight;
            rewardLbl.textWrappingMode = TextWrappingModes.NoWrap;
            _genRewardLabels[slotIdx] = rewardLbl;
            var xpLbl = MakeTMP("Xp", colC.transform, "", 11f, FontStyles.Bold, AccentBlue);
            xpLbl.alignment = TextAlignmentOptions.MidlineRight;
            xpLbl.textWrappingMode = TextWrappingModes.NoWrap;
            _genXpLabels[slotIdx] = xpLbl;

            var btnGo  = MakeGO("Btn", colC.transform);
            var btnImg = btnGo.AddComponent<Image>();
            btnImg.sprite = _sprR12; btnImg.type = Image.Type.Sliced;
            btnImg.color  = new Color(accent.r, accent.g, accent.b, 0.30f);
            var btn = btnGo.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            btn.interactable  = false;
            Le(btnGo, h: 44f);
            var btnLbl = MakeTMP("L", btnGo.transform, "…", 14f, FontStyles.Bold, Color.white);
            btnLbl.alignment = TextAlignmentOptions.Center;
            Stretch(btnLbl.GetComponent<RectTransform>());

            _genTakeBtns[slotIdx]  = btn;
            _genBtnLabels[slotIdx] = btnLbl;
            _genBtnImgs[slotIdx]   = btnImg;
            _genCardBtns[slotIdx]  = cardBtn;
        }

        private Button[]   _genCardBtns = new Button[3];
        private TMP_Text[] _genXpLabels = new TMP_Text[3];

        // Chiffre clé : valeur en gras + libellé discret dessous.
        private void GenStat(Transform host, string value, string label, Color valueColor)
        {
            var cell = MakeGO("Stat", host);
            var img = cell.AddComponent<Image>();
            img.sprite = _sprR8; img.type = Image.Type.Sliced; img.color = new Color(1f, 1f, 1f, 0.04f);
            img.raycastTarget = false;
            var vlg = cell.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(6, 6, 12, 12); vlg.spacing = 4f;
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            Le(cell, flexW: true);
            var v = MakeTMP("V", cell.transform, value, 16f, FontStyles.Bold, valueColor);
            v.alignment = TextAlignmentOptions.Center; v.textWrappingMode = TextWrappingModes.NoWrap;
            var l = MakeTMP("K", cell.transform, label, 9.5f, FontStyles.Bold, TextDim);
            l.alignment = TextAlignmentOptions.Center; l.textWrappingMode = TextWrappingModes.NoWrap;
            l.characterSpacing = 1f;
        }

        private async void GenerateSingleAsync(int slotIdx, int sessionId)
        {
            var gen = ServiceLocator.Get<ContractGenerator>();
            if (gen == null) { MarkGenError(slotIdx); return; }

            var profile = _sessionProfile;
            var diff    = _sessionDiffs[slotIdx];

            ContractData result = null;
            try { result = await gen.GenerateAsync(profile, diff, _sessionMaxRangeKm, _sessionMaxCapacityTons); }
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

            bool tour = def.isMultiStop && !string.IsNullOrEmpty(def.displayName);
            _genRouteLabels[slotIdx].text      = tour ? def.displayName : $"→  {CityName(def.destinationCityId)}";
            _genRouteLabels[slotIdx].color     = TextPrime;
            _genRouteLabels[slotIdx].fontStyle = FontStyles.Bold;

            if (_genCargoLabels[slotIdx] != null)
            {
                string cargo = string.IsNullOrEmpty(def.cargoLabel) ? "Marchandises diverses" : def.cargoLabel;
                _genCargoLabels[slotIdx].text  = tour ? $"{def.StopCount} arrêts  ·  {cargo}"
                                                      : $"Départ {CityName(def.originCityId)}  ·  {cargo}";
                _genCargoLabels[slotIdx].color = TextSecond;
            }
            if (_genXpLabels != null && slotIdx < _genXpLabels.Length && _genXpLabels[slotIdx] != null)
                _genXpLabels[slotIdx].text = $"+{XpCurve.CompanyXpReward(def.distanceKm, def.difficulty)} XP";

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
                if (_genBtnLabels[slotIdx] != null) _genBtnLabels[slotIdx].text = "Choisir";
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => OnTakeGeneratedContract(idx));
            }
            var row = _genCardBtns != null && slotIdx < _genCardBtns.Length ? _genCardBtns[slotIdx] : null;
            if (row != null)
            {
                row.interactable = true;
                row.onClick.RemoveAllListeners();
                row.onClick.AddListener(() => OnTakeGeneratedContract(idx));
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
            if (_genGroups != null && slotIdx < _genGroups.Length && _genGroups[slotIdx] != null) _genGroups[slotIdx].alpha = 1f;
        }

        private void RetryGenSlot(int slotIdx)
        {
            if (_genRouteLabels[slotIdx] == null) return;
            var accent = DiffColors[(int)_sessionDiffs[slotIdx]];

            _genResults[slotIdx] = null;
            _genRouteLabels[slotIdx].text      = "Recherche d'un client…";
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
                if (_genBtnLabels[slotIdx] != null) _genBtnLabels[slotIdx].text = "…";
                btn.onClick.RemoveAllListeners();
            }

            StartCoroutine(PulseWhileLoading(slotIdx, _genSessionId));
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

            // « Carnet de commandes » : contrats supplémentaires, alternant moyen / difficile.
            int extra = Mathf.Max(0, Systems.Progression.SkillTreeSystem.FlatOf(SkillEffectType.ExtraContractChoices));
            var diffs = new List<ContractDifficulty> { d0, d1, d2 };
            for (int i = 0; i < extra; i++) diffs.Add(i % 2 == 0 ? d1 : d2);
            return diffs.ToArray();
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
                float range = data.TankCapacity * 100f / data.fuelConsumptionLPer100Km;
                if (range > maxRange) maxRange = range;
            }
            return maxRange;
        }

        // Affiché dans la modale uniquement quand le joueur n'a encore aucun camion.
        private void BuildNoFleetMessage(Transform parent)
        {
            var box = MakeGO("NoFleet", parent);
            Le(box, h: 150f);
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
            // Largeur calée sur le texte (+ marges) : plus de débordement quand le texte grandit.
            Le(go, h: 22f);
            var pillLe = go.GetComponent<LayoutElement>();
            pillLe.minWidth = Mathf.Min(width, 48f);
            var pillHlg = go.AddComponent<HorizontalLayoutGroup>();
            pillHlg.padding = new RectOffset(9, 9, 0, 0);
            pillHlg.childAlignment = TextAnchor.MiddleCenter;
            pillHlg.childForceExpandWidth = false; pillHlg.childForceExpandHeight = false;
            pillHlg.childControlWidth = true;      pillHlg.childControlHeight = true;
            var lbl = MakeTMP("L", go.transform, text, 10.5f, FontStyles.Bold, color);
            lbl.characterSpacing  = 0.3f;
            lbl.alignment         = TextAlignmentOptions.Center;
            lbl.textWrappingMode  = TextWrappingModes.NoWrap;
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
            GenStat(host, $"{def.distanceKm:F0} km", "DISTANCE", TextPrime);
            GenStat(host, def.baseDurationSeconds > 0f ? FormatDuration(def.baseDurationSeconds) : "-", "DURÉE", TextPrime);
            GenStat(host, $"{def.cargoTons} t", "CHARGE", TextPrime);
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
        private void RouteEnd(Transform row, string tag, string city, bool right)
        {
            var col = MakeGO(tag, row);
            var vlg = col.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 2f; vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childAlignment = right ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
            Le(col, flexW: true, h: 66f);
            var t = MakeTMP("T", col.transform, tag, 10f, FontStyles.Bold, TextDim);
            t.characterSpacing = 1.5f;
            t.alignment = right ? TextAlignmentOptions.Right : TextAlignmentOptions.Left;
            Le(t.gameObject, h: 14f);
            var c = MakeTMP("C", col.transform, city, 22f, FontStyles.Bold, TextPrime);
            c.alignment = right ? TextAlignmentOptions.Right : TextAlignmentOptions.Left;
            c.textWrappingMode = TextWrappingModes.NoWrap;
            c.enableAutoSizing = true; c.fontSizeMin = 14f; c.fontSizeMax = 22f;
            Le(c.gameObject, h: 30f);
        }

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

            var valLbl = MakeTMP("V", cell.transform, value, 19f, FontStyles.Bold, valueColor ?? TextPrime);
            valLbl.alignment        = TextAlignmentOptions.Center;
            valLbl.textWrappingMode = TextWrappingModes.NoWrap;
            valLbl.enableAutoSizing = true;      // rétrécit si besoin au lieu de couper le texte
            valLbl.fontSizeMin = 12f; valLbl.fontSizeMax = 19f;
            Le(valLbl.gameObject, h: 26f);

            var keyLbl = MakeTMP("K", cell.transform, key, 10f, FontStyles.Bold, TextDim);
            keyLbl.characterSpacing = 1.2f;
            keyLbl.alignment        = TextAlignmentOptions.Center;
            keyLbl.textWrappingMode = TextWrappingModes.NoWrap;
            Le(keyLbl.gameObject, h: 14f);
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
