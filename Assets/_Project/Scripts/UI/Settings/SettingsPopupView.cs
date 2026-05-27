using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TransportManager.Core;
using TransportManager.Events;
using TransportManager.Save;
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

        private static readonly Color32 BgOverlay   = new Color32(0x00, 0x00, 0x00, 180);
        private static readonly Color32 BgPanel     = new Color32(0x16, 0x19, 0x1F, 255);
        private static readonly Color32 BgCard      = new Color32(0x1F, 0x23, 0x2B, 255);
        private static readonly Color32 BgInput     = new Color32(0x12, 0x15, 0x1A, 255);
        private static readonly Color32 BgTrack     = new Color32(0x2C, 0x32, 0x3C, 255);
        private static readonly Color32 Accent      = new Color32(0x3D, 0xC9, 0x6E, 255);
        private static readonly Color32 AccentBlue  = new Color32(0x35, 0x8E, 0xF5, 255);
        private static readonly Color32 TextPri     = new Color32(0xEC, 0xEF, 0xF5, 255);
        private static readonly Color32 TextSec     = new Color32(0x9A, 0xA5, 0xB8, 255);
        private static readonly Color32 TextMuted   = new Color32(0x55, 0x63, 0x78, 255);
        private static readonly Color32 Divider     = new Color32(0x28, 0x2D, 0x38, 255);

        private TMP_InputField _nameInput;
        private Slider         _musicSlider;
        private Slider         _fxSlider;
        private Toggle         _audioToggle;
        private Toggle         _notifContract;
        private Toggle         _notifMaint;
        private Toggle         _notifFuel;

        private Sprite _sprRound12;
        private Sprite _sprRound8;
        private Sprite _sprPill;

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
            _sprPill    = MakeRoundedSprite(32);
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
            contentVlg.padding               = new RectOffset(14, 14, 12, 16);
            contentVlg.spacing               = 6;
            contentVlg.childForceExpandWidth  = true;
            contentVlg.childForceExpandHeight = false;
            contentVlg.childControlWidth      = true;
            contentVlg.childControlHeight     = true;

            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scrollRect.content = contentRt;

            // Sections
            BuildCardSection(content.transform, "AUDIO",             BuildAudioRows);
            BuildCardSection(content.transform, "NOTIFICATIONS",     BuildNotifRows);
            BuildCardSection(content.transform, "PROFIL ENTREPRISE", BuildProfilRows);
            BuildCardSection(content.transform, "SAUVEGARDE",        BuildSaveRows);
            BuildCardSection(content.transform, "INFORMATIONS",      BuildInfoRows);

            LoadValues();
        }

        // ── Section card ──────────────────────────────────────────────────────

        private void BuildCardSection(Transform parent, string header, Action<Transform> fill)
        {
            // Label section
            var lbl = AddTMP("Lbl_" + header, parent, header, 10, FontStyles.Bold, TextMuted);
            lbl.characterSpacing = 120;
            lbl.gameObject.AddComponent<LayoutElement>().preferredHeight = 16;

            // Carte
            var card = MakeGO("Card_" + header, parent);
            var cardImg = card.AddComponent<Image>();
            cardImg.sprite        = _sprRound12;
            cardImg.type          = Image.Type.Sliced;
            cardImg.color         = BgCard;
            cardImg.raycastTarget = false;
            card.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var vlg = card.AddComponent<VerticalLayoutGroup>();
            vlg.padding               = new RectOffset(14, 14, 2, 2);
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
            _audioToggle = AddToggleRow(p, "Son activé",  true);
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
            _notifContract = AddToggleRow(p, "Contrat terminé",    true);
            MakeRowDivider(p);
            _notifMaint    = AddToggleRow(p, "Maintenance requise", true);
            MakeRowDivider(p);
            _notifFuel     = AddToggleRow(p, "Réservoir bas",       true);
        }

        private void BuildProfilRows(Transform p)
        {
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

            MakeGO("SpP", p).AddComponent<LayoutElement>().preferredHeight = 8;
            AddPrimaryBtn(p, "Enregistrer", Accent, SaveCompanyName);
            MakeGO("SpB", p).AddComponent<LayoutElement>().preferredHeight = 4;
        }

        private void BuildSaveRows(Transform p)
        {
            MakeGO("SpT", p).AddComponent<LayoutElement>().preferredHeight = 4;
            AddPrimaryBtn(p, "Sauvegarder maintenant", AccentBlue, SaveGame);
            MakeGO("SpB", p).AddComponent<LayoutElement>().preferredHeight = 4;
        }

        private void BuildInfoRows(Transform p)
        {
            MakeGO("SpT", p).AddComponent<LayoutElement>().preferredHeight = 4;
            var ver = AddTMP("Ver", p, $"Version {Application.version}", 12, FontStyles.Normal, TextMuted);
            ver.alignment = TextAlignmentOptions.Center;
            ver.gameObject.AddComponent<LayoutElement>().preferredHeight = 20;

            MakeGO("Sp1", p).AddComponent<LayoutElement>().preferredHeight = 6;
            AddSecondaryBtn(p, "Crédits",                    OnCredits);
            MakeGO("Sp2", p).AddComponent<LayoutElement>().preferredHeight = 4;
            AddSecondaryBtn(p, "Politique de confidentialité", OnPrivacy);
            MakeGO("SpB", p).AddComponent<LayoutElement>().preferredHeight = 4;
        }

        // ── Controls ──────────────────────────────────────────────────────────

        private Toggle AddToggleRow(Transform parent, string label, bool isOn)
        {
            var row = MakeGO("Row_" + label, parent);
            row.AddComponent<LayoutElement>().preferredHeight = 52;
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment        = TextAnchor.MiddleLeft;
            hlg.childForceExpandWidth  = false;
            hlg.childForceExpandHeight = true;
            hlg.childControlWidth      = true;
            hlg.childControlHeight     = true;

            var lbl = AddTMP("Lbl", row.transform, label, 16, FontStyles.Normal, TextPri);
            lbl.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

            // Track (pill)
            var trackGo  = MakeGO("Track", row.transform);
            var trackImg = trackGo.AddComponent<Image>();
            trackImg.sprite = _sprPill;
            trackImg.type   = Image.Type.Sliced;
            trackImg.color  = isOn ? Accent : BgTrack;
            var trackLe = trackGo.AddComponent<LayoutElement>();
            trackLe.preferredWidth  = 52;
            trackLe.preferredHeight = 30;

            // Thumb
            var thumbGo  = MakeGO("Thumb", trackGo.transform);
            var thumbImg = thumbGo.AddComponent<Image>();
            thumbImg.sprite = _sprPill;
            thumbImg.type   = Image.Type.Sliced;
            thumbImg.color  = Color.white;
            var thumbRt = thumbGo.GetComponent<RectTransform>();
            thumbRt.anchorMin        = new Vector2(0.5f, 0.5f);
            thumbRt.anchorMax        = new Vector2(0.5f, 0.5f);
            thumbRt.pivot            = new Vector2(0.5f, 0.5f);
            thumbRt.sizeDelta        = new Vector2(24, 24);
            thumbRt.anchoredPosition = isOn ? new Vector2(11, 0) : new Vector2(-11, 0);

            var toggle = trackGo.AddComponent<Toggle>();
            toggle.targetGraphic = trackImg;
            toggle.graphic       = thumbImg;
            toggle.isOn          = isOn;
            toggle.onValueChanged.AddListener(on =>
            {
                trackImg.color           = on ? Accent : BgTrack;
                thumbRt.anchoredPosition = on ? new Vector2(11, 0) : new Vector2(-11, 0);
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
            var lblLe = lbl.gameObject.AddComponent<LayoutElement>();
            lblLe.preferredWidth = 90;

            var sliderGo = MakeGO("Slider", row.transform);
            sliderGo.AddComponent<LayoutElement>().flexibleWidth = 1;

            var slider      = sliderGo.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value    = value;

            var bgGo  = MakeGO("Bg", sliderGo.transform);
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.sprite = _sprPill;
            bgImg.type   = Image.Type.Sliced;
            bgImg.color  = BgTrack;
            var bgRt = bgGo.GetComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0f, 0.42f);
            bgRt.anchorMax = new Vector2(1f, 0.58f);
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            slider.targetGraphic = bgImg;

            var fillAreaGo = MakeGO("FillArea", sliderGo.transform);
            var fillAreaRt = fillAreaGo.GetComponent<RectTransform>();
            fillAreaRt.anchorMin = new Vector2(0f, 0.42f);
            fillAreaRt.anchorMax = new Vector2(1f, 0.58f);
            fillAreaRt.offsetMin = new Vector2(4, 0);
            fillAreaRt.offsetMax = new Vector2(-4, 0);

            var fillGo  = MakeGO("Fill", fillAreaGo.transform);
            var fillImg = fillGo.AddComponent<Image>();
            fillImg.sprite = _sprPill;
            fillImg.type   = Image.Type.Sliced;
            fillImg.color  = Accent;
            var fillRt = fillGo.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = new Vector2(value, 1f);
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;
            slider.fillRect  = fillRt;

            var handleAreaGo = MakeGO("HandleArea", sliderGo.transform);
            var handleAreaRt = handleAreaGo.GetComponent<RectTransform>();
            handleAreaRt.anchorMin = Vector2.zero;
            handleAreaRt.anchorMax = Vector2.one;
            handleAreaRt.offsetMin = new Vector2(10, 0);
            handleAreaRt.offsetMax = new Vector2(-10, 0);

            var handleGo  = MakeGO("Handle", handleAreaGo.transform);
            var handleImg = handleGo.AddComponent<Image>();
            handleImg.sprite = _sprPill;
            handleImg.type   = Image.Type.Sliced;
            handleImg.color  = Color.white;
            var handleRt = handleGo.GetComponent<RectTransform>();
            handleRt.anchorMin        = new Vector2(0.5f, 0.5f);
            handleRt.anchorMax        = new Vector2(0.5f, 0.5f);
            handleRt.pivot            = new Vector2(0.5f, 0.5f);
            handleRt.anchoredPosition = Vector2.zero;
            handleRt.sizeDelta        = new Vector2(20, 20);
            slider.handleRect         = handleRt;

            return slider;
        }

        private void AddPrimaryBtn(Transform parent, string label, Color32 color, Action onClick)
        {
            var go  = MakeGO("BtnP_" + label, parent);
            var img = go.AddComponent<Image>();
            img.sprite = _sprRound8;
            img.type   = Image.Type.Sliced;
            img.color  = color;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            go.AddComponent<LayoutElement>().preferredHeight = 52;

            var lbl = AddTMP("Lbl", go.transform, label, 16, FontStyles.Bold, TextPri);
            lbl.alignment = TextAlignmentOptions.Center;
            FillParent(lbl.GetComponent<RectTransform>());

            btn.onClick.AddListener(() => onClick?.Invoke());
        }

        private void AddSecondaryBtn(Transform parent, string label, Action onClick)
        {
            var go  = MakeGO("BtnS_" + label, parent);
            var img = go.AddComponent<Image>();
            img.sprite = _sprRound8;
            img.type   = Image.Type.Sliced;
            img.color  = BgTrack;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            go.AddComponent<LayoutElement>().preferredHeight = 48;

            var lbl = AddTMP("Lbl", go.transform, label, 15, FontStyles.Normal, TextSec);
            lbl.alignment = TextAlignmentOptions.Center;
            FillParent(lbl.GetComponent<RectTransform>());

            btn.onClick.AddListener(() => onClick?.Invoke());
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
