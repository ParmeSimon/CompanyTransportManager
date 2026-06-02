using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TransportManager.Core;
using TransportManager.Events;
using TransportManager.Save;
using TransportManager.Social;
using TransportManager.UI.Common;

namespace TransportManager.UI.Settings
{
    public class SettingsPopupView : MonoBehaviour
    {
        private const string KeyMusicVol      = "s_music_vol";
        private const string KeyFxVol         = "s_fx_vol";
        private const string KeyAudioOn       = "s_audio_on";
        private const string KeyNotifContract = "s_notif_contract";
        private const string KeyNotifMaint    = "s_notif_maint";
        private const string KeyNotifFuel     = "s_notif_fuel";

        // Palette partagée (Header / Navbar / ContractsPanel / VehiclesTab)
        private static readonly Color32 BgOverlay   = new Color32(0x00, 0x00, 0x00, 200);
        private static readonly Color32 BgPanel     = new Color32(0x2C, 0x30, 0x38, 255);
        private static readonly Color32 BgCard      = new Color32(0x34, 0x38, 0x42, 255);
        private static readonly Color32 BgInput     = new Color32(0x1A, 0x1D, 0x24, 255);
        private static readonly Color32 BgTrack     = new Color32(0x1A, 0x1D, 0x24, 255);
        private static readonly Color32 BorderFaint = new Color32(0xFF, 0xFF, 0xFF, 18);
        private static readonly Color32 Accent      = new Color32(0x3D, 0xC9, 0x6E, 255);
        private static readonly Color32 AccentBlue  = new Color32(0x35, 0x8E, 0xF5, 255);
        private static readonly Color32 AccentGold  = new Color32(0xF2, 0xD9, 0x66, 255);
        private static readonly Color32 AccentViolet= new Color32(0x9B, 0x7B, 0xFF, 255);
        private static readonly Color32 TextPri     = new Color32(0xEC, 0xEE, 0xF5, 255);
        private static readonly Color32 TextSec     = new Color32(0x7A, 0x8F, 0xA6, 255);
        private static readonly Color32 TextMuted   = new Color32(0x5A, 0x65, 0x77, 255);
        private static readonly Color32 Divider     = new Color32(0x3A, 0x3F, 0x4A, 150);

        private TMP_InputField _nameInput;
        private Slider         _musicSlider;
        private Slider         _fxSlider;
        private Toggle         _audioToggle;
        private Toggle         _notifContract;
        private Toggle         _notifMaint;
        private Toggle         _notifFuel;

        private Sprite _sprRound12;
        private Sprite _sprRound8;
        private Sprite _sprCircle;  // cercle plein — rendu en Image.Type.Simple (poignées, pouces, pastilles)
        private Sprite _sprPill;    // pill pour la piste du toggle (rayon adapté à ~28 px de haut)
        private Sprite _sprBar;     // piste fine du slider (~8 px de haut)

        // Éditeur de logo
        private Image _logoPreviewBg;
        private Image _logoPreviewIcon;
        private bool  _logoIsCustom;

        // ── Entry point ───────────────────────────────────────────────────────

        public static void Show()
        {
            if (FindObjectOfType<SettingsPopupView>() != null) return;
            new GameObject("SettingsPopup", typeof(RectTransform)).AddComponent<SettingsPopupView>();
        }

        private void Awake()
        {
            _sprRound12 = MakeRoundedSprite(12);
            _sprRound8  = MakeRoundedSprite(8);
            _sprCircle  = MakeRoundedSprite(32);  // disque complet (rendu en Simple sur des rects carrés)
            _sprPill    = MakeRoundedSprite(13);  // pill : rayon = demi-hauteur de la piste (26 px)
            _sprBar     = MakeRoundedSprite(3);   // piste fine (~6 px) : rayon = demi-hauteur
            BuildUI();
        }

        // ── Build ─────────────────────────────────────────────────────────────

        private const int TitleBarH = 56;

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

            // Fond dim — clic = fermer
            var overlay = MakeImg("Overlay", transform, BgOverlay);
            overlay.raycastTarget = true;
            overlay.gameObject.AddComponent<Button>().onClick.AddListener(Close);
            FillParent(overlay.GetComponent<RectTransform>());

            // Panel — ancré sur 90% de l'écran
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
            panelRt.anchorMin = new Vector2(0.08f, 0.06f);
            panelRt.anchorMax = new Vector2(0.92f, 0.94f);
            panelRt.offsetMin = Vector2.zero;
            panelRt.offsetMax = Vector2.zero;

            // Titre + séparateur via helper partagé
            PopupHeader.Build(panelGo.transform, "Paramètres", Close, TitleBarH, _sprRound8);

            // Scroll — remplit le reste sous le séparateur
            BuildScrollArea(panelGo.transform);
        }

        private void BuildScrollArea(Transform parent)
        {
            var scrollGo = MakeGO("Scroll", parent);
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.anchorMin = new Vector2(0, 0);
            scrollRt.anchorMax = new Vector2(1, 1);
            scrollRt.offsetMin = new Vector2(0, 0);
            scrollRt.offsetMax = new Vector2(0, -(TitleBarH + 1));

            var scrollRect = scrollGo.AddComponent<ScrollRect>();
            scrollRect.horizontal        = false;
            scrollRect.vertical          = true;
            scrollRect.scrollSensitivity = 50;
            scrollRect.movementType      = ScrollRect.MovementType.Elastic;

            var viewport = MakeGO("Viewport", scrollGo.transform);
            FillParent(viewport.GetComponent<RectTransform>());
            viewport.AddComponent<RectMask2D>();
            scrollRect.viewport = viewport.GetComponent<RectTransform>();

            var content = MakeGO("Content", viewport.transform);
            var contentRt = content.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot     = new Vector2(0.5f, 1f);
            contentRt.sizeDelta = Vector2.zero;

            var contentVlg = content.AddComponent<VerticalLayoutGroup>();
            contentVlg.padding               = new RectOffset(18, 18, 18, 22);
            contentVlg.spacing               = 22;
            contentVlg.childForceExpandWidth  = true;
            contentVlg.childForceExpandHeight = false;
            contentVlg.childControlWidth      = true;
            contentVlg.childControlHeight     = true;

            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scrollRect.content = contentRt;

            // Sections — chacune avec sa couleur d'accent et une légende
            BuildCardSection(content.transform, "Audio",             "Sons et volume",            Accent,       BuildAudioRows);
            BuildCardSection(content.transform, "Notifications",      "Alertes de jeu",            AccentGold,   BuildNotifRows);
            BuildCardSection(content.transform, "Profil entreprise",  "Identité de ta société",    AccentBlue,   BuildProfilRows);
            BuildCardSection(content.transform, "Sauvegarde",         "Conserve ta progression",   AccentViolet, BuildSaveRows);
            BuildCardSection(content.transform, "Informations",       "À propos du jeu",           TextSec,      BuildInfoRows);

            LoadValues();
        }

        // ── Section card ──────────────────────────────────────────────────────

        private void BuildCardSection(Transform parent, string title, string caption, Color32 accent, Action<Transform> fill)
        {
            // Conteneur section (header + carte)
            var section = MakeGO("Section_" + title, parent);
            var sectionVlg = section.AddComponent<VerticalLayoutGroup>();
            sectionVlg.spacing               = 10;
            sectionVlg.childForceExpandWidth  = true;
            sectionVlg.childForceExpandHeight = false;
            sectionVlg.childControlWidth      = true;
            sectionVlg.childControlHeight     = true;

            // ── Header de section : barre d'accent + titre + légende ──
            var header = MakeGO("Header", section.transform);
            header.AddComponent<LayoutElement>().preferredHeight = 38;
            var hHlg = header.AddComponent<HorizontalLayoutGroup>();
            hHlg.childAlignment        = TextAnchor.MiddleLeft;
            hHlg.spacing               = 12;
            hHlg.padding               = new RectOffset(2, 2, 0, 0);
            hHlg.childForceExpandWidth  = false;
            hHlg.childForceExpandHeight = true;
            hHlg.childControlWidth      = true;
            hHlg.childControlHeight     = true;

            var bar    = MakeGO("Bar", header.transform);
            var barImg = bar.AddComponent<Image>();
            barImg.sprite        = _sprRound8;
            barImg.type          = Image.Type.Sliced;
            barImg.color         = accent;
            barImg.raycastTarget = false;
            var barLe = bar.AddComponent<LayoutElement>();
            barLe.preferredWidth  = 4;
            barLe.preferredHeight = 26;

            var texts = MakeGO("Texts", header.transform);
            texts.AddComponent<LayoutElement>().flexibleWidth = 1;
            var tVlg = texts.AddComponent<VerticalLayoutGroup>();
            tVlg.childAlignment        = TextAnchor.MiddleLeft;
            tVlg.spacing               = 0;
            tVlg.childForceExpandWidth  = true;
            tVlg.childForceExpandHeight = false;
            tVlg.childControlWidth      = true;
            tVlg.childControlHeight     = true;

            var titleTmp = AddTMP("Title", texts.transform, title.ToUpper(), 13, FontStyles.Bold, accent);
            titleTmp.characterSpacing = 60;
            titleTmp.gameObject.AddComponent<LayoutElement>().preferredHeight = 18;

            if (!string.IsNullOrEmpty(caption))
            {
                var capTmp = AddTMP("Caption", texts.transform, caption, 11.5f, FontStyles.Normal, TextMuted);
                capTmp.gameObject.AddComponent<LayoutElement>().preferredHeight = 15;
            }

            // ── Carte ──
            var card = MakeGO("Card_" + title, section.transform);
            var cardImg = card.AddComponent<Image>();
            cardImg.sprite        = _sprRound12;
            cardImg.type          = Image.Type.Sliced;
            cardImg.color         = BgCard;
            cardImg.raycastTarget = false;
            card.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Liseré fin pour détacher la carte du fond
            var outline = card.AddComponent<Outline>();
            outline.effectColor    = BorderFaint;
            outline.effectDistance = new Vector2(1f, -1f);

            var vlg = card.AddComponent<VerticalLayoutGroup>();
            vlg.padding               = new RectOffset(16, 16, 4, 4);
            vlg.spacing               = 0;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth      = true;
            vlg.childControlHeight     = true;

            fill(card.transform);
        }

        // ── Row builders ──────────────────────────────────────────────────────

        private void BuildAudioRows(Transform p)
        {
            _audioToggle = AddToggleRow(p, "Son activé", "Coupe ou rétablit tout l'audio", true);
            MakeRowDivider(p);
            _musicSlider = AddSliderRow(p, "Musique", 0.8f);
            MakeRowDivider(p);
            _fxSlider    = AddSliderRow(p, "Effets",  1f);

            _audioToggle.onValueChanged.AddListener(on =>
            {
                _musicSlider.interactable = on;
                _fxSlider.interactable    = on;
                ApplyAudio();
            });
            _musicSlider.onValueChanged.AddListener(_ => ApplyAudio());
            _fxSlider.onValueChanged.AddListener(_    => ApplyAudio());
        }

        private void BuildNotifRows(Transform p)
        {
            _notifContract = AddToggleRow(p, "Contrat terminé",     "Quand une livraison est bouclée",   true);
            MakeRowDivider(p);
            _notifMaint    = AddToggleRow(p, "Maintenance requise", "Un véhicule a besoin d'entretien",   true);
            MakeRowDivider(p);
            _notifFuel     = AddToggleRow(p, "Réservoir bas",       "Carburant insuffisant pour rouler",  true);
        }

        private void BuildProfilRows(Transform p)
        {
            BuildLogoEditor(p);

            MakeRowDivider(p);
            MakeGO("SpT", p).AddComponent<LayoutElement>().preferredHeight = 12;

            var fieldLbl = AddTMP("FieldLbl", p, "Nom de l'entreprise", 12, FontStyles.Normal, TextSec);
            fieldLbl.gameObject.AddComponent<LayoutElement>().preferredHeight = 18;
            MakeGO("SpL", p).AddComponent<LayoutElement>().preferredHeight = 6;

            // Input
            var inputGo  = MakeGO("NameInput", p);
            var inputImg = inputGo.AddComponent<Image>();
            inputImg.sprite = _sprRound8;
            inputImg.type   = Image.Type.Sliced;
            inputImg.color  = BgInput;
            var inputLe = inputGo.AddComponent<LayoutElement>();
            inputLe.preferredHeight = 52;
            inputLe.minHeight       = 52;

            _nameInput               = inputGo.AddComponent<TMP_InputField>();
            _nameInput.targetGraphic = inputImg;
            _nameInput.characterLimit = 40;

            var ta = MakeGO("TextArea", inputGo.transform);
            ta.AddComponent<RectMask2D>();
            var taRt = ta.GetComponent<RectTransform>();
            FillParent(taRt);
            taRt.offsetMin = new Vector2(16, 6);
            taRt.offsetMax = new Vector2(-16, -6);

            var ph = AddTMP("Placeholder", ta.transform, "Nom de l'entreprise…", 16, FontStyles.Normal,
                            new Color32(0x40, 0x4E, 0x60, 255));
            ph.alignment = TextAlignmentOptions.MidlineLeft;
            FillParent(ph.GetComponent<RectTransform>());

            var txt = AddTMP("Text", ta.transform, "", 16, FontStyles.Normal, TextPri);
            txt.alignment = TextAlignmentOptions.MidlineLeft;
            FillParent(txt.GetComponent<RectTransform>());

            _nameInput.textViewport  = ta.GetComponent<RectTransform>();
            _nameInput.textComponent = txt;
            _nameInput.placeholder   = ph;

            MakeGO("SpP", p).AddComponent<LayoutElement>().preferredHeight = 10;
            AddButton(p, "Enregistrer", ButtonStyle.Primary, AccentBlue, 46, SaveCompanyName);
            MakeGO("SpB", p).AddComponent<LayoutElement>().preferredHeight = 8;
        }

        private Button _removeLogoBtn;

        private void BuildLogoEditor(Transform p)
        {
            var company = GameManager.Instance?.Save?.company;
            _logoIsCustom = company != null && company.logoIsCustom;

            MakeGO("SpLg0", p).AddComponent<LayoutElement>().preferredHeight = 8;
            var lbl = AddTMP("LogoLbl", p, "Logo", 12, FontStyles.Normal, TextSec);
            lbl.gameObject.AddComponent<LayoutElement>().preferredHeight = 18;
            MakeGO("SpLg1", p).AddComponent<LayoutElement>().preferredHeight = 10;

            // Ligne : aperçu (gauche) + actions (droite)
            var previewRow = MakeGO("LogoPreviewRow", p);
            previewRow.AddComponent<LayoutElement>().preferredHeight = 72;
            var prHlg = previewRow.AddComponent<HorizontalLayoutGroup>();
            prHlg.childAlignment        = TextAnchor.MiddleLeft;
            prHlg.spacing               = 16;
            prHlg.childForceExpandWidth  = false;
            prHlg.childForceExpandHeight = false;
            prHlg.childControlWidth      = false;
            prHlg.childControlHeight     = false;

            var previewGo = MakeGO("Preview", previewRow.transform);
            _logoPreviewBg = previewGo.AddComponent<Image>();
            _logoPreviewBg.sprite        = _sprRound12;
            _logoPreviewBg.type          = Image.Type.Sliced;
            _logoPreviewBg.raycastTarget = false;
            var previewMask = previewGo.AddComponent<Mask>();
            previewMask.showMaskGraphic = true;
            previewGo.AddComponent<LayoutElement>();
            previewGo.GetComponent<RectTransform>().sizeDelta = new Vector2(72, 72);

            var previewIconGo = MakeGO("Icon", previewGo.transform);
            _logoPreviewIcon = previewIconGo.AddComponent<Image>();
            _logoPreviewIcon.color          = Color.white;
            _logoPreviewIcon.preserveAspect = true;
            _logoPreviewIcon.raycastTarget  = false;

            // Colonne de droite : bouton import + bouton retirer + indice
            var actionsCol = MakeGO("ActionsCol", previewRow.transform);
            actionsCol.AddComponent<LayoutElement>().flexibleWidth = 1;
            var acVlg = actionsCol.AddComponent<VerticalLayoutGroup>();
            acVlg.childAlignment        = TextAnchor.MiddleLeft;
            acVlg.spacing               = 6;
            acVlg.childForceExpandWidth  = true;
            acVlg.childForceExpandHeight = false;
            acVlg.childControlWidth      = true;
            acVlg.childControlHeight     = true;

            var btnRow = MakeGO("BtnRow", actionsCol.transform);
            btnRow.AddComponent<LayoutElement>().preferredHeight = 42;
            var brHlg = btnRow.AddComponent<HorizontalLayoutGroup>();
            brHlg.spacing               = 8;
            brHlg.childForceExpandWidth  = true;
            brHlg.childForceExpandHeight = true;
            brHlg.childControlWidth      = true;
            brHlg.childControlHeight     = true;

            AddButton(btnRow.transform, "Importer une image", ButtonStyle.Primary, AccentBlue, 42, ImportLogo);
            _removeLogoBtn = AddButton(btnRow.transform, "Retirer", ButtonStyle.Ghost, default, 42, RemoveLogo);

            var hint = AddTMP("ImportHint", actionsCol.transform, "PNG ou JPG depuis ta galerie", 11, FontStyles.Normal, TextMuted);
            hint.gameObject.AddComponent<LayoutElement>().preferredHeight = 15;

            RefreshLogoPreview();
        }

        private void RefreshLogoPreview()
        {
            var company = GameManager.Instance?.Save?.company;
            CompanyLogo.ApplyTo(_logoPreviewBg, _logoPreviewIcon, company);
            if (_removeLogoBtn) _removeLogoBtn.gameObject.SetActive(_logoIsCustom);
        }

        private void ImportLogo()
        {
            NativeImagePicker.Pick(tex =>
            {
                if (tex == null) return; // annulé / échec
                if (!CompanyLogoStore.Save(tex)) return;
                _logoIsCustom = true;
                ApplyLogo();
                RefreshLogoPreview();
            });
        }

        private void RemoveLogo()
        {
            CompanyLogoStore.Clear();
            _logoIsCustom = false;
            ApplyLogo();
            RefreshLogoPreview();
        }

        private void ApplyLogo()
        {
            var gm = GameManager.Instance;
            if (gm?.Save?.company == null) return;
            gm.Save.company.logoIsCustom = _logoIsCustom;
            GameEvents.RaiseCompanyProfileChanged();
            SaveSystem.Save(gm.Save);
        }

        private void BuildSaveRows(Transform p)
        {
            MakeGO("SpT", p).AddComponent<LayoutElement>().preferredHeight = 10;
            AddButton(p, "Sauvegarder maintenant", ButtonStyle.Primary, AccentViolet, 46, SaveGame);
            MakeGO("SpB", p).AddComponent<LayoutElement>().preferredHeight = 8;
        }

        private void BuildInfoRows(Transform p)
        {
            MakeGO("SpT", p).AddComponent<LayoutElement>().preferredHeight = 8;
            var ver = AddTMP("Ver", p, $"Version {Application.version}", 12, FontStyles.Normal, TextMuted);
            ver.alignment = TextAlignmentOptions.Center;
            ver.gameObject.AddComponent<LayoutElement>().preferredHeight = 20;

            MakeGO("Sp1", p).AddComponent<LayoutElement>().preferredHeight = 8;
            AddButton(p, "Crédits", ButtonStyle.Ghost, default, 44, OnCredits);
            MakeGO("Sp2", p).AddComponent<LayoutElement>().preferredHeight = 6;
            AddButton(p, "Politique de confidentialité", ButtonStyle.Ghost, default, 44, OnPrivacy);
            MakeGO("SpB", p).AddComponent<LayoutElement>().preferredHeight = 8;
        }

        // ── Controls ──────────────────────────────────────────────────────────

        private Toggle AddToggleRow(Transform parent, string label, string desc, bool isOn)
        {
            bool hasDesc = !string.IsNullOrEmpty(desc);

            var row = MakeGO("Row_" + label, parent);
            row.AddComponent<LayoutElement>().preferredHeight = hasDesc ? 62 : 52;
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment        = TextAnchor.MiddleLeft;
            hlg.spacing               = 12;
            hlg.childForceExpandWidth  = false;
            hlg.childForceExpandHeight = false; // ne pas étirer le track : il garde sa hauteur de pill (26 px)
            hlg.childControlWidth      = true;
            hlg.childControlHeight     = true;

            // Bloc texte (label + description)
            var texts = MakeGO("Texts", row.transform);
            texts.AddComponent<LayoutElement>().flexibleWidth = 1;
            var tVlg = texts.AddComponent<VerticalLayoutGroup>();
            tVlg.childAlignment        = TextAnchor.MiddleLeft;
            tVlg.spacing               = 2;
            tVlg.childForceExpandWidth  = true;
            tVlg.childForceExpandHeight = false;
            tVlg.childControlWidth      = true;
            tVlg.childControlHeight     = true;

            var lbl = AddTMP("Lbl", texts.transform, label, 16, FontStyles.Normal, TextPri);
            lbl.gameObject.AddComponent<LayoutElement>().preferredHeight = 20;
            if (hasDesc)
            {
                var d = AddTMP("Desc", texts.transform, desc, 11.5f, FontStyles.Normal, TextMuted);
                d.gameObject.AddComponent<LayoutElement>().preferredHeight = 15;
            }

            // Track (pill) — large et plat (64×26), bordure visible à l'état OFF.
            // Le pouce est ancré aux extrémités du track, donc sa position suit la largeur.
            const float thumbPad = 3f;
            var trackGo  = MakeGO("Track", row.transform);
            var trackImg = trackGo.AddComponent<Image>();
            trackImg.sprite = _sprPill;
            trackImg.type   = Image.Type.Sliced;
            trackImg.color  = isOn ? Accent : BgTrack;
            var trackOutline = trackGo.AddComponent<Outline>();
            trackOutline.effectColor    = isOn ? new Color32(0, 0, 0, 0) : new Color32(0x4A, 0x52, 0x60, 255);
            trackOutline.effectDistance = new Vector2(1.2f, -1.2f);
            var trackLe = trackGo.AddComponent<LayoutElement>();
            trackLe.preferredWidth  = 64;
            trackLe.preferredHeight = 26;

            // Thumb — cercle plein (Type.Simple), ancré à gauche/droite selon l'état
            var thumbGo  = MakeGO("Thumb", trackGo.transform);
            var thumbImg = thumbGo.AddComponent<Image>();
            thumbImg.sprite = _sprCircle;
            thumbImg.type   = Image.Type.Simple;
            thumbImg.color  = Color.white;
            var thumbShadow = thumbGo.AddComponent<Shadow>();
            thumbShadow.effectColor    = new Color(0f, 0f, 0f, 0.4f);
            thumbShadow.effectDistance = new Vector2(0f, -1.5f);
            var thumbRt = thumbGo.GetComponent<RectTransform>();
            thumbRt.pivot     = new Vector2(0.5f, 0.5f);
            thumbRt.sizeDelta = new Vector2(20, 20);
            SetThumbSide(thumbRt, isOn, thumbPad);

            var toggle = trackGo.AddComponent<Toggle>();
            toggle.targetGraphic = trackImg;
            toggle.graphic       = thumbImg;
            toggle.isOn          = isOn;
            toggle.onValueChanged.AddListener(on =>
            {
                trackImg.color            = on ? Accent : BgTrack;
                trackOutline.effectColor  = on ? new Color32(0, 0, 0, 0) : new Color32(0x4A, 0x52, 0x60, 255);
                SetThumbSide(thumbRt, on, thumbPad);
            });

            return toggle;
        }

        private Slider AddSliderRow(Transform parent, string label, float value)
        {
            var row = MakeGO("Row_" + label, parent);
            row.AddComponent<LayoutElement>().preferredHeight = 52;
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment        = TextAnchor.MiddleLeft;
            hlg.spacing               = 14;
            hlg.childForceExpandWidth  = false;
            hlg.childForceExpandHeight = true;
            hlg.childControlWidth      = true;
            hlg.childControlHeight     = true;

            var lbl = AddTMP("Lbl", row.transform, label, 16, FontStyles.Normal, TextPri);
            lbl.gameObject.AddComponent<LayoutElement>().preferredWidth = 84;

            var sliderGo = MakeGO("Slider", row.transform);
            sliderGo.AddComponent<LayoutElement>().flexibleWidth = 1;

            // Zone de capture transparente sur toute la surface du slider → glissable partout.
            var hitImg = sliderGo.AddComponent<Image>();
            hitImg.color         = new Color(0f, 0f, 0f, 0f);
            hitImg.raycastTarget = true;

            var slider      = sliderGo.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value    = value;

            // Piste de fond — barre fine de 6 px centrée verticalement
            var bgGo  = MakeGO("Bg", sliderGo.transform);
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.sprite = _sprBar;
            bgImg.type   = Image.Type.Sliced;
            bgImg.color  = BgTrack;
            var bgRt = bgGo.GetComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0f, 0.5f);
            bgRt.anchorMax = new Vector2(1f, 0.5f);
            bgRt.pivot     = new Vector2(0.5f, 0.5f);
            bgRt.sizeDelta = new Vector2(0f, 6f);
            slider.targetGraphic = bgImg;

            var fillAreaGo = MakeGO("FillArea", sliderGo.transform);
            var fillAreaRt = fillAreaGo.GetComponent<RectTransform>();
            fillAreaRt.anchorMin = new Vector2(0f, 0.5f);
            fillAreaRt.anchorMax = new Vector2(1f, 0.5f);
            fillAreaRt.pivot     = new Vector2(0.5f, 0.5f);
            fillAreaRt.sizeDelta = new Vector2(-14f, 6f);

            var fillGo  = MakeGO("Fill", fillAreaGo.transform);
            var fillImg = fillGo.AddComponent<Image>();
            fillImg.sprite = _sprBar;
            fillImg.type   = Image.Type.Sliced;
            fillImg.color  = Accent;
            var fillRt = fillGo.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = new Vector2(value, 1f);
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;
            slider.fillRect  = fillRt;

            const float handleSize = 18f;
            var handleAreaGo = MakeGO("HandleArea", sliderGo.transform);
            var handleAreaRt = handleAreaGo.GetComponent<RectTransform>();
            handleAreaRt.anchorMin = Vector2.zero;
            handleAreaRt.anchorMax = Vector2.one;
            handleAreaRt.offsetMin = new Vector2(handleSize * 0.5f, 0f);
            handleAreaRt.offsetMax = new Vector2(-handleSize * 0.5f, 0f);

            // Poignée = zone de capture pleine hauteur (transparente) → facile à saisir/glisser.
            // Le Slider l'étire en hauteur ; le disque visuel est un enfant centré qui, lui, ne s'étire pas.
            var handleGo  = MakeGO("Handle", handleAreaGo.transform);
            var handleImg = handleGo.AddComponent<Image>();
            handleImg.color         = new Color(0f, 0f, 0f, 0f); // invisible mais raycastable
            handleImg.raycastTarget = true;
            var handleRt = handleGo.GetComponent<RectTransform>();
            handleRt.pivot     = new Vector2(0.5f, 0.5f);
            handleRt.sizeDelta = new Vector2(handleSize, 0f);
            slider.handleRect  = handleRt;

            // Disque visuel centré (accent) — taille fixe, donc toujours rond
            var visGo  = MakeGO("Visual", handleGo.transform);
            var visImg = visGo.AddComponent<Image>();
            visImg.sprite        = _sprCircle;
            visImg.type          = Image.Type.Simple;
            visImg.color         = Accent;
            visImg.raycastTarget = false;
            var visShadow = visGo.AddComponent<Shadow>();
            visShadow.effectColor    = new Color(0f, 0f, 0f, 0.4f);
            visShadow.effectDistance = new Vector2(0f, -1.5f);
            var visRt = visGo.GetComponent<RectTransform>();
            visRt.anchorMin = visRt.anchorMax = visRt.pivot = new Vector2(0.5f, 0.5f);
            visRt.sizeDelta = new Vector2(handleSize, handleSize);

            var handleCoreGo  = MakeGO("Core", visGo.transform);
            var handleCoreImg = handleCoreGo.AddComponent<Image>();
            handleCoreImg.sprite        = _sprCircle;
            handleCoreImg.type          = Image.Type.Simple;
            handleCoreImg.color         = Color.white;
            handleCoreImg.raycastTarget = false;
            var handleCoreRt = handleCoreGo.GetComponent<RectTransform>();
            handleCoreRt.anchorMin = Vector2.zero;
            handleCoreRt.anchorMax = Vector2.one;
            handleCoreRt.offsetMin = new Vector2(4, 4);
            handleCoreRt.offsetMax = new Vector2(-4, -4);

            // Badge de valeur en direct (%)
            var valTmp = AddTMP("Val", row.transform, Pct(value), 14, FontStyles.Bold, TextSec);
            valTmp.alignment = TextAlignmentOptions.MidlineRight;
            valTmp.gameObject.AddComponent<LayoutElement>().preferredWidth = 46;
            slider.onValueChanged.AddListener(v => valTmp.text = Pct(v));

            return slider;
        }

        private static string Pct(float v) => Mathf.RoundToInt(Mathf.Clamp01(v) * 100f) + "%";

        private enum ButtonStyle { Primary, Ghost }

        // Bouton moderne : Primary (rempli + ombre + retour visuel) ou Ghost (contour discret).
        private Button AddButton(Transform parent, string label, ButtonStyle style, Color32 accent, float height, Action onClick)
        {
            var go  = MakeGO("Btn_" + label, parent);
            var img = go.AddComponent<Image>();
            img.sprite = _sprRound8;
            img.type   = Image.Type.Sliced;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition    = Selectable.Transition.ColorTint;

            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            le.minHeight       = height;

            var cb = ColorBlock.defaultColorBlock;
            cb.colorMultiplier = 1f;
            cb.fadeDuration    = 0.08f;

            Color32 textCol;
            float   fontSize;
            FontStyles fontStyle;

            if (style == ButtonStyle.Primary)
            {
                img.color = accent;
                var sh = go.AddComponent<Shadow>();
                sh.effectColor    = new Color(0f, 0f, 0f, 0.28f);
                sh.effectDistance = new Vector2(0f, -2f);

                cb.normalColor      = Color.white;
                cb.highlightedColor = new Color(1.06f, 1.06f, 1.06f, 1f);
                cb.pressedColor     = new Color(0.90f, 0.90f, 0.90f, 1f);
                cb.selectedColor    = Color.white;
                cb.disabledColor    = new Color(0.55f, 0.55f, 0.55f, 1f);

                textCol   = TextPri;
                fontSize  = 14.5f;
                fontStyle = FontStyles.Bold;
            }
            else // Ghost — fond translucide + contour fin
            {
                img.color = new Color32(0xFF, 0xFF, 0xFF, 0x0E);
                var ol = go.AddComponent<Outline>();
                ol.effectColor    = new Color32(0xFF, 0xFF, 0xFF, 0x26);
                ol.effectDistance = new Vector2(1f, -1f);

                cb.normalColor      = Color.white;
                cb.highlightedColor = new Color(2.2f, 2.2f, 2.2f, 1f); // éclaire le fond translucide
                cb.pressedColor     = new Color(0.7f, 0.7f, 0.7f, 1f);
                cb.selectedColor    = Color.white;
                cb.disabledColor    = Color.white;

                textCol   = TextSec;
                fontSize  = 14f;
                fontStyle = FontStyles.Normal;
            }
            btn.colors = cb;

            var lbl = AddTMP("Lbl", go.transform, label, fontSize, fontStyle, textCol);
            lbl.alignment = TextAlignmentOptions.Center;
            FillParent(lbl.GetComponent<RectTransform>());

            btn.onClick.AddListener(() => onClick?.Invoke());
            return btn;
        }

        // Place le pouce contre le bord gauche (off) ou droit (on) de la piste.
        private static void SetThumbSide(RectTransform thumb, bool on, float pad)
        {
            float ax = on ? 1f : 0f;
            thumb.anchorMin = new Vector2(ax, 0.5f);
            thumb.anchorMax = new Vector2(ax, 0.5f);
            float dx = thumb.sizeDelta.x * 0.5f + pad;
            thumb.anchoredPosition = new Vector2(on ? -dx : dx, 0);
        }

        private static void MakeRowDivider(Transform parent)
        {
            var go  = MakeGO("Div", parent);
            var img = go.AddComponent<Image>();
            img.color         = Divider;
            img.raycastTarget = false;
            go.AddComponent<LayoutElement>().preferredHeight = 1;
        }

        // ── Logic ─────────────────────────────────────────────────────────────

        private void LoadValues()
        {
            _musicSlider.value    = PlayerPrefs.GetFloat(KeyMusicVol, 0.8f);
            _fxSlider.value       = PlayerPrefs.GetFloat(KeyFxVol, 1f);
            bool audioOn          = PlayerPrefs.GetInt(KeyAudioOn, 1) == 1;
            _audioToggle.isOn     = audioOn;
            _musicSlider.interactable = audioOn;
            _fxSlider.interactable    = audioOn;
            _notifContract.isOn   = PlayerPrefs.GetInt(KeyNotifContract, 1) == 1;
            _notifMaint.isOn      = PlayerPrefs.GetInt(KeyNotifMaint, 1) == 1;
            _notifFuel.isOn       = PlayerPrefs.GetInt(KeyNotifFuel, 1) == 1;

            var gm = GameManager.Instance;
            if (gm?.Save?.company != null)
                _nameInput.text = gm.Save.company.companyName;
        }

        private void ApplyAudio()
        {
            bool on = _audioToggle.isOn;
            AudioListener.volume = on ? 1f : 0f;
            PlayerPrefs.SetFloat(KeyMusicVol, _musicSlider.value);
            PlayerPrefs.SetFloat(KeyFxVol,    _fxSlider.value);
            PlayerPrefs.SetInt(KeyAudioOn, on ? 1 : 0);
        }

        private void SaveCompanyName()
        {
            var name = _nameInput.text.Trim();
            if (string.IsNullOrEmpty(name)) return;
            var gm = GameManager.Instance;
            if (gm?.Save?.company == null) return;
            gm.Save.company.companyName = name;
            GameEvents.RaiseCompanyProfileChanged();
            SaveSystem.Save(gm.Save);
        }

        private void SaveGame()
        {
            PersistPrefs();
            var gm = GameManager.Instance;
            if (gm?.Save != null) SaveSystem.Save(gm.Save);
        }

        private void PersistPrefs()
        {
            PlayerPrefs.SetInt(KeyNotifContract, _notifContract.isOn ? 1 : 0);
            PlayerPrefs.SetInt(KeyNotifMaint,    _notifMaint.isOn    ? 1 : 0);
            PlayerPrefs.SetInt(KeyNotifFuel,     _notifFuel.isOn     ? 1 : 0);
            PlayerPrefs.Save();
        }

        private void Close()      { PersistPrefs(); Destroy(gameObject); }
        private void OnCredits()  => Debug.Log("[Settings] Crédits — à implémenter");
        private void OnPrivacy()  => Application.OpenURL("https://votresite.com/privacy");

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

        private static TMP_Text AddTMP(string name, Transform parent, string text, float size, FontStyles style, Color32 color)
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

        // Génère un sprite avec coins arrondis (9-slice) sans asset externe
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
            {
                for (int x = 0; x < size; x++)
                {
                    pixels[y * size + x] = new Color(1f, 1f, 1f, RoundedAlpha(x, y, size, r));
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            float b = r;
            return Sprite.Create(tex, new Rect(0, 0, size, size),
                                 new Vector2(0.5f, 0.5f), 100f, 0,
                                 SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

        private static float RoundedAlpha(int x, int y, int size, int r)
        {
            int cx = -1, cy = -1;
            if      (x < r          && y < r)           { cx = r;        cy = r;        }
            else if (x >= size - r  && y < r)           { cx = size - r; cy = r;        }
            else if (x < r          && y >= size - r)   { cx = r;        cy = size - r; }
            else if (x >= size - r  && y >= size - r)   { cx = size - r; cy = size - r; }
            if (cx < 0) return 1f;
            float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
            return Mathf.Clamp01(r - d + 0.5f);
        }
    }
}
