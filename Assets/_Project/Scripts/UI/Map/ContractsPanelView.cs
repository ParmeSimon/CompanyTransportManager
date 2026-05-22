using System;
using System.Collections;
using System.Collections.Generic;
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
        // ── Colors calquées sur la sidebar (#2C3038) ──────────────────────────────
        private static readonly Color32 PanelBg     = new Color32(0x2C, 0x30, 0x38, 255);
        private static readonly Color32 SectionHdr  = new Color32(0x22, 0x26, 0x2E, 255);
        private static readonly Color   DividerCol  = new Color32(0x3A, 0x3F, 0x4A, 160);
        private static readonly Color   TextActive  = Color.white;
        private static readonly Color   TextMuted   = new Color(0.55f, 0.55f, 0.60f);
        private static readonly Color   ActiveTabBg = new Color(1f, 1f, 1f, 0.10f);
        private static readonly Color   AccentBlue  = new Color(0.22f, 0.50f, 0.90f, 1f);
        private static readonly Color   AccentGreen = new Color(0.20f, 0.72f, 0.38f, 1f);
        private static readonly Color   AccentAmber = new Color(0.95f, 0.60f, 0.10f, 1f);
        private static readonly Color   ScrimColor  = new Color(0f, 0f, 0f, 0.78f);

        private const float PanelWidth  = 280f;
        private const float Margin      = 12f;   // same spirit as sidebar's 10px from edge

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
        }

        // ── Build ─────────────────────────────────────────────────────────────────
        public void Build()
        {
            // Floating panel: top-right, spaced from edges like the sidebar
            var rt = GetComponent<RectTransform>();
            rt.anchorMin        = new Vector2(1f, 1f);
            rt.anchorMax        = new Vector2(1f, 1f);
            rt.pivot            = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-Margin, -Margin);
            rt.sizeDelta        = new Vector2(PanelWidth, 0f); // height = ContentSizeFitter

            var bg = gameObject.AddComponent<Image>();
            bg.color         = PanelBg;
            bg.raycastTarget = true;

            var csf = gameObject.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var vlg = gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding               = new RectOffset(0, 0, 0, 0);
            vlg.spacing               = 0f;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth      = true;
            vlg.childControlHeight     = false;
            vlg.childAlignment        = TextAnchor.UpperCenter;

            // Header — même style que l'en-tête du tab actif dans la sidebar
            var hdrGo = MakeGO("Header", transform);
            hdrGo.AddComponent<Image>().color = (Color)PanelBg;
            Le(hdrGo, h: 40f);
            var hdrHlg = hdrGo.AddComponent<HorizontalLayoutGroup>();
            hdrHlg.padding               = new RectOffset(14, 14, 0, 0);
            hdrHlg.childAlignment        = TextAnchor.MiddleLeft;
            hdrHlg.childForceExpandWidth  = false;
            hdrHlg.childForceExpandHeight = false;
            hdrHlg.childControlWidth      = true;
            hdrHlg.childControlHeight     = true;
            var hdrLbl = MakeTMP("Lbl", hdrGo.transform, "CONTRATS", 11f, FontStyles.Bold, TextMuted);
            hdrLbl.alignment = TextAlignmentOptions.MidlineLeft;
            Le(hdrLbl.gameObject, flexW: true, h: 40f);

            Divider(transform);

            // "En cours" section
            _activeRows    = BuildSection(transform, "En cours",   AccentBlue);
            Divider(transform);
            // "En attente" section
            _availableRows = BuildSection(transform, "En attente", AccentAmber);

            Refresh();
        }

        private Transform BuildSection(Transform parent, string title, Color accent)
        {
            // Section label row (like a disabled tab entry in sidebar)
            var hdrGo = MakeGO("SHdr_" + title, parent);
            hdrGo.AddComponent<Image>().color = (Color)SectionHdr;
            Le(hdrGo, h: 28f);
            var hhlg = hdrGo.AddComponent<HorizontalLayoutGroup>();
            hhlg.padding               = new RectOffset(14, 8, 0, 0);
            hhlg.spacing               = 6f;
            hhlg.childAlignment        = TextAnchor.MiddleLeft;
            hhlg.childForceExpandWidth  = false;
            hhlg.childForceExpandHeight = false;
            hhlg.childControlWidth      = true;
            hhlg.childControlHeight     = true;

            // Accent pip
            var pip = MakeGO("Pip", hdrGo.transform);
            pip.AddComponent<Image>().color = accent;
            Le(pip, w: 3f, h: 14f);

            var sLbl = MakeTMP("Lbl", hdrGo.transform, title, 10f, FontStyles.Bold, TextMuted);
            sLbl.alignment = TextAlignmentOptions.MidlineLeft;
            Le(sLbl.gameObject, flexW: true, h: 28f);

            // Rows container
            var cGo = MakeGO("Rows_" + title, parent);
            var cvlg = cGo.AddComponent<VerticalLayoutGroup>();
            cvlg.spacing               = 0f;
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
                EmptyRow(_activeRows, "Aucun contrat en cours");

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

            var row = MakeRow(_activeRows, AccentBlue);
            var top = HRow(row.transform, 20f);

            var routeLbl = MakeTMP("R", top,
                $"{CityName(def.originCityId)} → {CityName(def.destinationCityId)}",
                12f, FontStyles.Bold, TextActive);
            routeLbl.textWrappingMode = TextWrappingModes.NoWrap;
            routeLbl.overflowMode     = TextOverflowModes.Ellipsis;
            Le(routeLbl.gameObject, flexW: true, h: 20f);

            var rew = MakeTMP("$", top, $"+{def.baseReward:N0}$", 10f, FontStyles.Bold, AccentGreen);
            rew.textWrappingMode = TextWrappingModes.NoWrap;
            Le(rew.gameObject, w: 58f, h: 20f);

            var bot = HRow(row.transform, 18f);
            var timeLbl = MakeTMP("T", bot, FormatRemaining(inst), 10f, FontStyles.Normal, TextMuted);
            timeLbl.textWrappingMode = TextWrappingModes.NoWrap;
            Le(timeLbl.gameObject, flexW: true, h: 18f);

            var btn = SmallBtn(bot, "Voir +", AccentBlue);
            btn.onClick.AddListener(() => OpenPopup(null, inst));
        }

        private void BuildAvailableRow(ContractData def)
        {
            var row = MakeRow(_availableRows, AccentAmber);
            var top = HRow(row.transform, 20f);

            var routeLbl = MakeTMP("R", top,
                $"{CityName(def.originCityId)} → {CityName(def.destinationCityId)}",
                12f, FontStyles.Bold, TextActive);
            routeLbl.textWrappingMode = TextWrappingModes.NoWrap;
            routeLbl.overflowMode     = TextOverflowModes.Ellipsis;
            Le(routeLbl.gameObject, flexW: true, h: 20f);

            var rew = MakeTMP("$", top, $"+{def.baseReward:N0}$", 10f, FontStyles.Bold, AccentGreen);
            rew.textWrappingMode = TextWrappingModes.NoWrap;
            Le(rew.gameObject, w: 58f, h: 20f);

            var bot = HRow(row.transform, 18f);
            var info = MakeTMP("I", bot,
                $"{def.distanceKm:F0} km  ·  cap. {def.requiredCapacity} t",
                10f, FontStyles.Normal, TextMuted);
            info.textWrappingMode = TextWrappingModes.NoWrap;
            Le(info.gameObject, flexW: true, h: 18f);

            var btn = SmallBtn(bot, "Voir +", AccentAmber);
            btn.onClick.AddListener(() => OpenPopup(def, null));
        }

        private void EmptyRow(Transform parent, string msg)
        {
            var go = MakeGO("Empty", parent);
            go.AddComponent<Image>().color = new Color(0, 0, 0, 0);
            Le(go, h: 32f);
            var lbl = MakeTMP("Lbl", go.transform, msg, 10f, FontStyles.Normal, TextMuted);
            lbl.alignment = TextAlignmentOptions.MidlineLeft;
            Stretch(lbl.GetComponent<RectTransform>(), new Vector2(14f, 0f), Vector2.zero);
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

            // Canvas propre pour overlay au-dessus de tout
            _popup = new GameObject("ContractPopup", typeof(RectTransform));
            _popup.transform.SetParent(transform.parent, false);
            var canvas = _popup.AddComponent<Canvas>();
            canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            var scaler = _popup.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight  = 0.5f;
            _popup.AddComponent<GraphicRaycaster>();
            Stretch(_popup.GetComponent<RectTransform>());

            // Scrim
            var scrimGo  = MakeGO("Scrim", _popup.transform);
            var scrimImg = scrimGo.AddComponent<Image>();
            scrimImg.color        = ScrimColor;
            scrimImg.raycastTarget = true;
            Stretch(scrimGo.GetComponent<RectTransform>());
            var scrimBtn = scrimGo.AddComponent<Button>();
            scrimBtn.transition = Selectable.Transition.None;
            scrimBtn.onClick.AddListener(ClosePopup);

            // Card — couleurs sidebar
            var cardGo  = MakeGO("Card", _popup.transform);
            var cardImg = cardGo.AddComponent<Image>();
            cardImg.color = (Color)PanelBg;
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
            var statusColor = isActive ? AccentBlue : AccentAmber;

            // Card header
            var cHdrGo = MakeGO("CHdr", cardGo.transform);
            cHdrGo.AddComponent<Image>().color = (Color)SectionHdr;
            Le(cHdrGo, h: 44f);
            var cHhlg = cHdrGo.AddComponent<HorizontalLayoutGroup>();
            cHhlg.padding               = new RectOffset(16, 16, 0, 0);
            cHhlg.spacing               = 8f;
            cHhlg.childAlignment        = TextAnchor.MiddleLeft;
            cHhlg.childForceExpandWidth  = false;
            cHhlg.childForceExpandHeight = false;
            cHhlg.childControlWidth      = true;
            cHhlg.childControlHeight     = true;

            var pip2 = MakeGO("Pip", cHdrGo.transform);
            pip2.AddComponent<Image>().color = statusColor;
            Le(pip2, w: 3f, h: 20f);

            var cHdrLbl = MakeTMP("Lbl", cHdrGo.transform,
                isActive ? "CONTRAT EN COURS" : "CONTRAT DISPONIBLE",
                11f, FontStyles.Bold, statusColor);
            Le(cHdrLbl.gameObject, flexW: true, h: 44f);

            Divider(cardGo.transform);

            // Route
            var routeGo = MakeGO("Route", cardGo.transform);
            routeGo.AddComponent<Image>().color = new Color(0, 0, 0, 0);
            Le(routeGo, h: 44f);
            var routeLbl = MakeTMP("Lbl", routeGo.transform,
                $"{FullCity(d.originCityId)}  →  {FullCity(d.destinationCityId)}",
                13f, FontStyles.Bold, TextActive);
            routeLbl.alignment       = TextAlignmentOptions.Center;
            routeLbl.textWrappingMode = TextWrappingModes.NoWrap;
            routeLbl.overflowMode    = TextOverflowModes.Ellipsis;
            Stretch(routeLbl.GetComponent<RectTransform>(), new Vector2(16f, 0f), new Vector2(-16f, 0f));

            Divider(cardGo.transform);

            // Stats
            var statsGo = MakeGO("Stats", cardGo.transform);
            statsGo.AddComponent<Image>().color = new Color(0, 0, 0, 0);
            var sVlg = statsGo.AddComponent<VerticalLayoutGroup>();
            sVlg.padding               = new RectOffset(16, 16, 8, 8);
            sVlg.spacing               = 2f;
            sVlg.childForceExpandWidth  = true;
            sVlg.childForceExpandHeight = false;
            sVlg.childControlWidth      = true;
            sVlg.childControlHeight     = false;
            statsGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            StatRow(statsGo.transform, "Distance",     $"{d.distanceKm:F0} km");
            StatRow(statsGo.transform, "Récompense",   $"{d.baseReward:N0} $");
            StatRow(statsGo.transform, "Capacité min", $"{d.requiredCapacity} t");
            StatRow(statsGo.transform, "Difficulté",   d.difficulty.ToString());
            if (isActive)
                StatRow(statsGo.transform, "Temps restant", FormatRemaining(_popupInst));

            // Vehicle picker (contrats disponibles uniquement)
            if (!isActive)
            {
                Divider(cardGo.transform);

                var pickGo = MakeGO("Picker", cardGo.transform);
                pickGo.AddComponent<Image>().color = new Color(0, 0, 0, 0);
                var pVlg = pickGo.AddComponent<VerticalLayoutGroup>();
                pVlg.padding               = new RectOffset(16, 16, 8, 8);
                pVlg.spacing               = 2f;
                pVlg.childForceExpandWidth  = true;
                pVlg.childForceExpandHeight = false;
                pVlg.childControlWidth      = true;
                pVlg.childControlHeight     = false;
                pickGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                var pickLbl = MakeTMP("Lbl", pickGo.transform, "Affecter un camion :", 10f, FontStyles.Bold, TextMuted);
                pickLbl.alignment = TextAlignmentOptions.Left;
                Le(pickLbl.gameObject, h: 18f);

                BuildVehiclePicker(pickGo.transform, d);
            }

            Divider(cardGo.transform);

            // Buttons
            var btnGo = MakeGO("Btns", cardGo.transform);
            btnGo.AddComponent<Image>().color = new Color(0, 0, 0, 0);
            Le(btnGo, h: 50f);
            var bHlg = btnGo.AddComponent<HorizontalLayoutGroup>();
            bHlg.padding               = new RectOffset(12, 12, 8, 8);
            bHlg.spacing               = 8f;
            bHlg.childAlignment        = TextAnchor.MiddleCenter;
            bHlg.childForceExpandWidth  = false;
            bHlg.childForceExpandHeight = false;
            bHlg.childControlWidth      = true;
            bHlg.childControlHeight     = true;

            var closeBtn = PopupBtn(btnGo.transform, "Fermer", DividerCol);
            Le(closeBtn.gameObject, flexW: true, h: 34f);
            closeBtn.onClick.AddListener(ClosePopup);

            if (!isActive)
            {
                _startBtn = PopupBtn(btnGo.transform, "Démarrer", AccentBlue);
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
                    10f, FontStyles.Normal, new Color(0.85f, 0.38f, 0.28f));
                noLbl.enableWordWrapping = true;
                Le(noLbl.gameObject, h: 32f);
                return;
            }

            for (int i = 0; i < _selectableVehicles.Count; i++)
            {
                int idx  = i;
                var v    = _selectableVehicles[i];
                var data = catalog?.GetById(v.vehicleDataId);

                var rowGo  = MakeGO("VRow", parent);
                var rowImg = rowGo.AddComponent<Image>();
                rowImg.color = ActiveTabBg;
                _vehicleRowBgs.Add(rowImg);
                Le(rowGo, h: 36f);

                var rowBtn = rowGo.AddComponent<Button>();
                rowBtn.targetGraphic = rowImg;
                rowBtn.transition    = Selectable.Transition.None;
                rowBtn.onClick.AddListener(() => SelectVehicle(idx));

                var rowHlg = rowGo.AddComponent<HorizontalLayoutGroup>();
                rowHlg.padding               = new RectOffset(12, 12, 0, 0);
                rowHlg.spacing               = 6f;
                rowHlg.childAlignment        = TextAnchor.MiddleLeft;
                rowHlg.childForceExpandWidth  = false;
                rowHlg.childForceExpandHeight = false;
                rowHlg.childControlWidth      = true;
                rowHlg.childControlHeight     = true;

                var nameLbl = MakeTMP("N", rowGo.transform,
                    data?.displayName ?? v.vehicleDataId, 11f, FontStyles.Bold, TextActive);
                nameLbl.textWrappingMode = TextWrappingModes.NoWrap;
                Le(nameLbl.gameObject, flexW: true, h: 36f);

                var capLbl = MakeTMP("C", rowGo.transform,
                    $"{data?.capacity ?? 0} t", 10f, FontStyles.Normal, TextMuted);
                capLbl.alignment = TextAlignmentOptions.MidlineRight;
                Le(capLbl.gameObject, w: 36f, h: 36f);
            }
        }

        private void SelectVehicle(int idx)
        {
            _selectedVehicleIdx = idx;
            for (int i = 0; i < _vehicleRowBgs.Count; i++)
                _vehicleRowBgs[i].color = i == idx
                    ? new Color(0.22f, 0.50f, 0.90f, 0.30f)
                    : ActiveTabBg;
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
            if (rem.TotalSeconds <= 0) return "Prêt à livrer !";
            if (rem.TotalHours >= 1)   return $"{(int)rem.TotalHours}h {rem.Minutes:D2}m";
            return $"{rem.Minutes}m {rem.Seconds:D2}s";
        }

        // ── UI primitives ─────────────────────────────────────────────────────────
        private static GameObject MakeRow(Transform parent, Color accent)
        {
            var go = MakeGO("Row", parent);
            go.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.04f);
            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.padding               = new RectOffset(14, 10, 8, 8);
            vlg.spacing               = 4f;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth      = true;
            vlg.childControlHeight     = false;
            Le(go, h: 66f);

            // Left accent bar (comme la sidebar active indicator)
            var bar = MakeGO("Bar", go.transform);
            bar.AddComponent<Image>().color = accent;
            var bRt = bar.GetComponent<RectTransform>();
            bRt.anchorMin        = new Vector2(0f, 0.1f);
            bRt.anchorMax        = new Vector2(0f, 0.9f);
            bRt.pivot            = new Vector2(0f, 0.5f);
            bRt.sizeDelta        = new Vector2(2f, 0f);
            bRt.anchoredPosition = new Vector2(2f, 0f);

            return go;
        }

        private static Transform HRow(Transform parent, float h)
        {
            var go = MakeGO("HR", parent);
            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment        = TextAnchor.MiddleLeft;
            hlg.spacing               = 4f;
            hlg.childForceExpandWidth  = false;
            hlg.childForceExpandHeight = false;
            hlg.childControlWidth      = true;
            hlg.childControlHeight     = true;
            Le(go, h: h);
            return go.transform;
        }

        private static Button SmallBtn(Transform parent, string label, Color color)
        {
            var go  = MakeGO("B", parent);
            var img = go.AddComponent<Image>();
            img.color = color;
            Le(go, w: 50f, h: 18f);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition    = Selectable.Transition.None;
            var lbl = MakeTMP("L", go.transform, label, 8f, FontStyles.Bold, Color.white);
            lbl.alignment = TextAlignmentOptions.Center;
            Stretch(lbl.GetComponent<RectTransform>());
            return btn;
        }

        private static Button PopupBtn(Transform parent, string label, Color color)
        {
            var go  = MakeGO("PB_" + label, parent);
            var img = go.AddComponent<Image>();
            img.color = color;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition    = Selectable.Transition.None;
            var lbl = MakeTMP("L", go.transform, label, 12f, FontStyles.Bold, Color.white);
            lbl.alignment = TextAlignmentOptions.Center;
            Stretch(lbl.GetComponent<RectTransform>());
            return btn;
        }

        private static void StatRow(Transform parent, string key, string val)
        {
            var go = MakeGO("S", parent);
            go.AddComponent<Image>().color = new Color(0, 0, 0, 0);
            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment        = TextAnchor.MiddleLeft;
            hlg.childForceExpandWidth  = false;
            hlg.childForceExpandHeight = false;
            hlg.childControlWidth      = true;
            hlg.childControlHeight     = true;
            Le(go, h: 22f);

            var k = MakeTMP("K", go.transform, key, 11f, FontStyles.Normal, TextMuted);
            k.alignment = TextAlignmentOptions.MidlineLeft;
            Le(k.gameObject, flexW: true, h: 22f);

            var v = MakeTMP("V", go.transform, val, 11f, FontStyles.Bold, TextActive);
            v.alignment = TextAlignmentOptions.MidlineRight;
            Le(v.gameObject, w: 120f, h: 22f);
        }

        private static void Divider(Transform parent)
        {
            var go = MakeGO("Div", parent);
            go.AddComponent<Image>().color = DividerCol;
            Le(go, h: 1f);
        }

        private static GameObject MakeGO(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static TMP_Text MakeTMP(string name, Transform parent, string text, float size, FontStyles style, Color color)
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

