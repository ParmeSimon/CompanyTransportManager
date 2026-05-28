using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TransportManager.Core;
using TransportManager.Entities.Vehicles;
using TransportManager.Enums;
using TransportManager.Events;
using TransportManager.Systems.Depot;
using TransportManager.Systems.Economy;
using TransportManager.Systems.Fleet;
using TransportManager.Systems.Progression;

namespace TransportManager.UI.Tabs
{
    public class VehiclesTabView : MonoBehaviour
    {
        private readonly VehiclePurchaseService _purchase = new VehiclePurchaseService();

        // main panels
        private GameObject _listPanel;
        private GameObject _detailPanel;

        // list panel refs
        private RectTransform _catalogContent;
        private TMP_Text _fleetCountLabel;

        // detail panel refs
        private TMP_Text _detailName;
        private TMP_Text _detailCategory;
        private TMP_Text _detailStats;
        private TMP_Text _detailPrice;
        private TMP_Text _detailIngotPrice;
        private Button _buyDollarBtn;
        private Button _buyIngotBtn;
        private TMP_Text _buyDollarLabel;
        private TMP_Text _buyIngotLabel;
        private TMP_Text _detailStatus;
        private Image _detailStatusIcon;
        private Image _detailImage;
        private TMP_Text _detailImagePlaceholder;
        private TMP_Text _detailSpecs;       // top-left: technical description
        private TMP_Text _detailCareerStats; // bottom: career statistics

        private VehicleData _selectedVehicle;

        private RawImage _bgImage;
        private RectTransform _bgRect;
        private Texture2D _bgTex;
        private Vector2 _lastBgSize;

        // ── Palette partagée (Header / Navbar / ContractsPanel) ───────────────────
        private static readonly Color BgPanel     = new Color(0x2C / 255f, 0x30 / 255f, 0x38 / 255f, 0.94f); // fond panneau (#2C3038)
        private static readonly Color BgElevated  = new Color(0x34 / 255f, 0x38 / 255f, 0x42 / 255f, 1f);    // cartes
        private static readonly Color BgInset     = new Color(1f, 1f, 1f, 0.05f);                            // sous-cartes sur BgElevated
        private static readonly Color BgPill      = new Color(0x1A / 255f, 0x1D / 255f, 0x24 / 255f, 230 / 255f); // pills/badges
        private static readonly Color BorderFaint = new Color(1f, 1f, 1f, 0.08f);
        private static readonly Color DividerCol  = new Color(0x3A / 255f, 0x3F / 255f, 0x4A / 255f, 150 / 255f);
        private static readonly Color TextPrime   = new Color(0xEC / 255f, 0xEE / 255f, 0xF5 / 255f, 1f);
        private static readonly Color TextSecond  = new Color(0x7A / 255f, 0x8F / 255f, 0xA6 / 255f, 1f);
        private static readonly Color TextDim     = new Color(0.40f, 0.44f, 0.52f, 1f);
        private static readonly Color AccentBlue  = new Color(0.22f, 0.52f, 1.00f, 1f);
        private static readonly Color AccentGreen = new Color(0x3D / 255f, 0xC9 / 255f, 0x6E / 255f, 1f);
        private static readonly Color AccentGold  = new Color(0xF2 / 255f, 0xD9 / 255f, 0x66 / 255f, 1f);

        private Sprite _sprR8, _sprR12, _sprR16;

        private void Awake() => Build();

        private void Build()
        {
            foreach (Transform child in transform)
            {
#if UNITY_EDITOR
                DestroyImmediate(child.gameObject);
#else
                Destroy(child.gameObject);
#endif
            }

            EnsureRoundedSprites();

            var rt = GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            // Background
            var bgGo = new GameObject("Background", typeof(RectTransform));
            bgGo.transform.SetParent(transform, false);
            var bgImg = bgGo.AddComponent<RawImage>();
            var bgTex = Resources.Load<Texture2D>("UI/Tutorial/VehicleBackground");
            bgImg.texture = bgTex;
            bgImg.color = Color.white;
            bgImg.raycastTarget = false;
            _bgImage = bgImg;
            _bgTex = bgTex;
            var bgRt = bgGo.GetComponent<RectTransform>();
            StretchFull(bgRt);
            _bgRect = bgRt;
            UpdateBackgroundUVs();

            // Dark scrim
            var scrimGo = new GameObject("Scrim", typeof(RectTransform));
            scrimGo.transform.SetParent(transform, false);
            scrimGo.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
            StretchFull(scrimGo.GetComponent<RectTransform>());

            // Split view: list on the left, detail on the right (always visible side by side)
            var splitGo = new GameObject("Split", typeof(RectTransform));
            splitGo.transform.SetParent(transform, false);
            var splitRt = splitGo.GetComponent<RectTransform>();
            StretchFull(splitRt);
            _splitRoot = splitGo;

            _listPanel = BuildListPanel(splitGo.transform);
            _detailPanel = BuildDetailPanel(splitGo.transform);
            ShowDetailPlaceholder();
        }

        private GameObject _splitRoot;
        private GameObject _detailPlaceholder;
        private GameObject _detailContent;

        // ---- List panel ----

        // Layout constants — single source of truth for the modern, airy look
        private const float SIDEBAR_RESERVE = 130f;  // left margin to clear HUD sidebar (Carte/Dépôt/Véhicules/Magasin)
        private const float TOP_RESERVE     = 160f;  // top margin to clear HUD header (Saiaze block + tab strip)
        private const float RIGHT_PADDING   = 32f;
        private const float BOTTOM_PADDING  = 32f;
        private const float HEADER_HEIGHT   = 56f;
        private const float CARD_HEIGHT     = 108f;
        private const float CARD_SPACING    = 12f;
        private const float SPLIT_GAP       = 20f;   // gap between list and detail panels

        private GameObject BuildListPanel(Transform parent)
        {
            var panel = new GameObject("ListPanel", typeof(RectTransform));
            panel.transform.SetParent(parent, false);
            // Take exactly the left half of the available area
            var panelRt = panel.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0, 0);
            panelRt.anchorMax = new Vector2(0.5f, 1);
            panelRt.offsetMin = new Vector2(SIDEBAR_RESERVE, BOTTOM_PADDING);
            panelRt.offsetMax = new Vector2(-SPLIT_GAP * 0.5f, -TOP_RESERVE);

            // Rounded panel backdrop (même fond que le header #2C3038)
            var panelBg = panel.AddComponent<Image>();
            panelBg.sprite        = _sprR16;
            panelBg.type          = Image.Type.Sliced;
            panelBg.color         = BgPanel;
            panelBg.raycastTarget = true;

            // Header bar — sits at the top of the list panel
            var header = new GameObject("Header", typeof(RectTransform));
            header.transform.SetParent(panel.transform, false);
            var headerRt = header.GetComponent<RectTransform>();
            headerRt.anchorMin = new Vector2(0, 1);
            headerRt.anchorMax = new Vector2(1, 1);
            headerRt.pivot = new Vector2(0.5f, 1f);
            headerRt.offsetMin = new Vector2(0, -HEADER_HEIGHT);
            headerRt.offsetMax = Vector2.zero;

            var headerHlg = header.AddComponent<HorizontalLayoutGroup>();
            headerHlg.padding = new RectOffset(24, 24, 0, 0);
            headerHlg.spacing = 14;
            headerHlg.childAlignment = TextAnchor.MiddleLeft;
            headerHlg.childForceExpandWidth = false;
            headerHlg.childForceExpandHeight = true;

            // Accent bar on the left of the header
            var accentGo = new GameObject("HeaderAccent", typeof(RectTransform));
            accentGo.transform.SetParent(header.transform, false);
            var accentImg = accentGo.AddComponent<Image>();
            accentImg.sprite        = _sprR8;
            accentImg.type          = Image.Type.Sliced;
            accentImg.color         = AccentBlue;
            accentImg.raycastTarget = false;
            var accentLe = accentGo.AddComponent<LayoutElement>();
            accentLe.preferredWidth = 3;
            accentLe.preferredHeight = 26;

            var titleGo = new GameObject("Title", typeof(RectTransform));
            titleGo.transform.SetParent(header.transform, false);
            var titleTmp = titleGo.AddComponent<TextMeshProUGUI>();
            titleTmp.text = "<b>GARAGE</b><color=#5A6D8A>   /   </color><color=#7A8FA6>Catalogue véhicules</color>";
            titleTmp.fontSize = 18;
            titleTmp.color = TextPrime;
            titleTmp.alignment = TextAlignmentOptions.MidlineLeft;
            titleGo.AddComponent<LayoutElement>().flexibleWidth = 1;

            var countGo = new GameObject("FleetCount", typeof(RectTransform));
            countGo.transform.SetParent(header.transform, false);
            _fleetCountLabel = countGo.AddComponent<TextMeshProUGUI>();
            _fleetCountLabel.fontSize = 13;
            _fleetCountLabel.color = TextSecond;
            _fleetCountLabel.alignment = TextAlignmentOptions.MidlineRight;
            countGo.AddComponent<LayoutElement>().preferredWidth = 260;

            // Fine separator under the header
            var hSep = new GameObject("HeaderSep", typeof(RectTransform));
            hSep.transform.SetParent(panel.transform, false);
            hSep.AddComponent<Image>().color = BorderFaint;
            var hSepRt = hSep.GetComponent<RectTransform>();
            hSepRt.anchorMin = new Vector2(0, 1);
            hSepRt.anchorMax = new Vector2(1, 1);
            hSepRt.pivot     = new Vector2(0.5f, 1f);
            hSepRt.offsetMin = new Vector2(16, -HEADER_HEIGHT - 1);
            hSepRt.offsetMax = new Vector2(-16, -HEADER_HEIGHT);

            // Scroll view — fills the panel below the header
            var scrollGo = new GameObject("Scroll", typeof(RectTransform));
            scrollGo.transform.SetParent(panel.transform, false);
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.anchorMin = new Vector2(0, 0);
            scrollRt.anchorMax = new Vector2(1, 1);
            scrollRt.offsetMin = new Vector2(12, 10);
            scrollRt.offsetMax = new Vector2(-12, -(HEADER_HEIGHT + 12));

            var scrollRect = scrollGo.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.scrollSensitivity = 36;

            var viewportGo = new GameObject("Viewport", typeof(RectTransform));
            viewportGo.transform.SetParent(scrollGo.transform, false);
            StretchFull(viewportGo.GetComponent<RectTransform>());
            viewportGo.AddComponent<RectMask2D>();
            scrollRect.viewport = viewportGo.GetComponent<RectTransform>();

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(viewportGo.transform, false);
            _catalogContent = contentGo.GetComponent<RectTransform>();
            _catalogContent.anchorMin = new Vector2(0, 1);
            _catalogContent.anchorMax = new Vector2(1, 1);
            _catalogContent.pivot = new Vector2(0.5f, 1f);
            _catalogContent.offsetMin = Vector2.zero;
            _catalogContent.offsetMax = Vector2.zero;

            var vlg = contentGo.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = CARD_SPACING;
            vlg.padding = new RectOffset(4, 4, 6, 16);
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var csf = contentGo.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.content = _catalogContent;

            return panel;
        }

        // ---- Detail panel ----

        private GameObject BuildDetailPanel(Transform parent)
        {
            // Right-half panel (always visible)
            var panel = new GameObject("DetailPanel", typeof(RectTransform));
            panel.transform.SetParent(parent, false);
            var panelRt = panel.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.5f, 0);
            panelRt.anchorMax = new Vector2(1, 1);
            panelRt.offsetMin = new Vector2(SPLIT_GAP * 0.5f, BOTTOM_PADDING);
            panelRt.offsetMax = new Vector2(-RIGHT_PADDING, -TOP_RESERVE);

            var panelBg = panel.AddComponent<Image>();
            panelBg.sprite        = _sprR16;
            panelBg.type          = Image.Type.Sliced;
            panelBg.color         = BgPanel;
            panelBg.raycastTarget = true;

            // Placeholder when nothing is selected
            _detailPlaceholder = new GameObject("Placeholder", typeof(RectTransform));
            _detailPlaceholder.transform.SetParent(panel.transform, false);
            StretchFull(_detailPlaceholder.GetComponent<RectTransform>());
            var phTmp = _detailPlaceholder.AddComponent<TextMeshProUGUI>();
            phTmp.text = "<color=#7A8FA6>Sélectionnez un véhicule pour voir ses caractéristiques</color>";
            phTmp.fontSize = 14;
            phTmp.alignment = TextAlignmentOptions.Center;
            phTmp.raycastTarget = false;

            // Content container (shown when a vehicle is selected)
            _detailContent = new GameObject("Content", typeof(RectTransform));
            _detailContent.transform.SetParent(panel.transform, false);
            StretchFull(_detailContent.GetComponent<RectTransform>());

            var vlg = _detailContent.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(24, 24, 22, 22);
            vlg.spacing = 14;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childAlignment = TextAnchor.UpperLeft;

            // ---- Title row: name + accent stripe ----
            var titleRow = new GameObject("TitleRow", typeof(RectTransform));
            titleRow.transform.SetParent(_detailContent.transform, false);
            var titleHlg = titleRow.AddComponent<HorizontalLayoutGroup>();
            titleHlg.spacing = 14;
            titleHlg.childForceExpandWidth = false;
            titleHlg.childForceExpandHeight = false;
            titleHlg.childAlignment = TextAnchor.MiddleLeft;
            titleRow.AddComponent<LayoutElement>().preferredHeight = 40;

            var titleAccent = new GameObject("Accent", typeof(RectTransform));
            titleAccent.transform.SetParent(titleRow.transform, false);
            var titleAccentImg = titleAccent.AddComponent<Image>();
            titleAccentImg.sprite        = _sprR8;
            titleAccentImg.type          = Image.Type.Sliced;
            titleAccentImg.color         = AccentBlue;
            titleAccentImg.raycastTarget = false;
            var titleAccentLe = titleAccent.AddComponent<LayoutElement>();
            titleAccentLe.preferredWidth = 4;
            titleAccentLe.preferredHeight = 34;

            var nameGo = new GameObject("Name", typeof(RectTransform));
            nameGo.transform.SetParent(titleRow.transform, false);
            _detailName = nameGo.AddComponent<TextMeshProUGUI>();
            _detailName.fontSize = 24;
            _detailName.fontStyle = FontStyles.Bold;
            _detailName.color = Color.white;
            _detailName.alignment = TextAlignmentOptions.MidlineLeft;
            nameGo.AddComponent<LayoutElement>().flexibleWidth = 1;

            // Category subtitle
            var catGo = new GameObject("Category", typeof(RectTransform));
            catGo.transform.SetParent(_detailContent.transform, false);
            _detailCategory = catGo.AddComponent<TextMeshProUGUI>();
            _detailCategory.fontSize = 13;
            _detailCategory.color = TextSecond;
            catGo.AddComponent<LayoutElement>().preferredHeight = 18;

            AddDivider(_detailContent.transform);

            // ============================================================
            //  GRID:  top row = [ specs (left) | image (right) ]
            //         bottom  = [ career stats (full width) ]
            // ============================================================

            // ---- Top row: 2-column horizontal layout ----
            var topRow = new GameObject("TopRow", typeof(RectTransform));
            topRow.transform.SetParent(_detailContent.transform, false);
            var topRowLe = topRow.AddComponent<LayoutElement>();
            topRowLe.flexibleHeight = 1f;
            topRowLe.minHeight = 340;   // enough to fit 3 sections × 4 rows comfortably

            var topHlg = topRow.AddComponent<HorizontalLayoutGroup>();
            topHlg.spacing = 14;
            topHlg.childForceExpandWidth = false;
            topHlg.childForceExpandHeight = true;
            topHlg.childAlignment = TextAnchor.UpperLeft;

            // -- Left card: technical specs --
            var specsCard = new GameObject("SpecsCard", typeof(RectTransform));
            specsCard.transform.SetParent(topRow.transform, false);
            var specsLe = specsCard.AddComponent<LayoutElement>();
            specsLe.flexibleWidth = 1f;
            StyleInsetCard(specsCard);

            var specsInner = new GameObject("Inner", typeof(RectTransform));
            specsInner.transform.SetParent(specsCard.transform, false);
            var specsInnerRt = specsInner.GetComponent<RectTransform>();
            specsInnerRt.anchorMin = Vector2.zero;
            specsInnerRt.anchorMax = Vector2.one;
            specsInnerRt.offsetMin = new Vector2(20, 18);
            specsInnerRt.offsetMax = new Vector2(-20, -20);
            specsCard.AddComponent<RectMask2D>();   // clip any internal overflow
            _detailSpecs = specsInner.AddComponent<TextMeshProUGUI>();
            _detailSpecs.fontSize = 14;
            _detailSpecs.color = new Color(0.86f, 0.88f, 0.93f);
            _detailSpecs.lineSpacing = 10;
            _detailSpecs.paragraphSpacing = 6;
            _detailSpecs.alignment = TextAlignmentOptions.TopLeft;
            _detailSpecs.enableWordWrapping = false;
            _detailSpecs.overflowMode = TextOverflowModes.Truncate;
            _detailSpecs.raycastTarget = false;

            // -- Right card: hero image --
            var heroGo = new GameObject("Hero", typeof(RectTransform));
            heroGo.transform.SetParent(topRow.transform, false);
            var heroLe = heroGo.AddComponent<LayoutElement>();
            heroLe.flexibleWidth = 1f;
            StyleInsetCard(heroGo);

            var imgHolder = new GameObject("Image", typeof(RectTransform));
            imgHolder.transform.SetParent(heroGo.transform, false);
            var imgRt = imgHolder.GetComponent<RectTransform>();
            imgRt.anchorMin = Vector2.zero;
            imgRt.anchorMax = Vector2.one;
            imgRt.offsetMin = new Vector2(16, 16);
            imgRt.offsetMax = new Vector2(-16, -16);
            _detailImage = imgHolder.AddComponent<Image>();
            _detailImage.preserveAspect = true;
            _detailImage.raycastTarget = false;

            var imgPhGo = new GameObject("ImagePlaceholder", typeof(RectTransform));
            imgPhGo.transform.SetParent(heroGo.transform, false);
            StretchFull(imgPhGo.GetComponent<RectTransform>());
            _detailImagePlaceholder = imgPhGo.AddComponent<TextMeshProUGUI>();
            _detailImagePlaceholder.fontSize = 64;
            _detailImagePlaceholder.fontStyle = FontStyles.Bold;
            _detailImagePlaceholder.alignment = TextAlignmentOptions.Center;
            _detailImagePlaceholder.color = new Color(0.30f, 0.45f, 0.70f, 0.45f);
            _detailImagePlaceholder.raycastTarget = false;

            // ---- Bottom row: career statistics (full width) ----
            var careerCard = new GameObject("CareerCard", typeof(RectTransform));
            careerCard.transform.SetParent(_detailContent.transform, false);
            var careerLe = careerCard.AddComponent<LayoutElement>();
            careerLe.flexibleHeight = 1.1f;
            careerLe.minHeight = 200;
            StyleInsetCard(careerCard);

            var careerInner = new GameObject("Inner", typeof(RectTransform));
            careerInner.transform.SetParent(careerCard.transform, false);
            var careerInnerRt = careerInner.GetComponent<RectTransform>();
            careerInnerRt.anchorMin = Vector2.zero;
            careerInnerRt.anchorMax = Vector2.one;
            careerInnerRt.offsetMin = new Vector2(22, 18);
            careerInnerRt.offsetMax = new Vector2(-22, -18);
            careerCard.AddComponent<RectMask2D>();
            _detailCareerStats = careerInner.AddComponent<TextMeshProUGUI>();
            _detailCareerStats.fontSize = 13;
            _detailCareerStats.color = new Color(0.86f, 0.88f, 0.93f);
            _detailCareerStats.lineSpacing = 12;
            _detailCareerStats.paragraphSpacing = 8;
            _detailCareerStats.alignment = TextAlignmentOptions.TopLeft;
            _detailCareerStats.enableWordWrapping = false;
            _detailCareerStats.overflowMode = TextOverflowModes.Truncate;
            _detailCareerStats.raycastTarget = false;

            // Legacy ref kept for compatibility (unused now)
            _detailStats = _detailSpecs;

            // Status line — horizontal row: [warehouse icon] [text]
            var statusRow = new GameObject("StatusRow", typeof(RectTransform));
            statusRow.transform.SetParent(_detailContent.transform, false);
            var statusHlg = statusRow.AddComponent<HorizontalLayoutGroup>();
            statusHlg.spacing = 6;
            statusHlg.childAlignment = TextAnchor.MiddleCenter;
            statusHlg.childForceExpandWidth = false;
            statusHlg.childForceExpandHeight = false;
            statusHlg.childControlWidth = true;
            statusHlg.childControlHeight = true;
            statusRow.AddComponent<LayoutElement>().preferredHeight = 22;

            var statusIconGo = new GameObject("StatusIcon", typeof(RectTransform));
            statusIconGo.transform.SetParent(statusRow.transform, false);
            _detailStatusIcon = statusIconGo.AddComponent<Image>();
            _detailStatusIcon.sprite = Resources.Load<Sprite>("UI/Icons/icons/warehouse");
            _detailStatusIcon.color = new Color(1f, 0.75f, 0.3f);
            _detailStatusIcon.preserveAspect = true;
            _detailStatusIcon.raycastTarget = false;
            var statusIconLe = statusIconGo.AddComponent<LayoutElement>();
            statusIconLe.preferredWidth = 20;
            statusIconLe.preferredHeight = 20;
            statusIconGo.SetActive(false);

            var statusGo = new GameObject("Status", typeof(RectTransform));
            statusGo.transform.SetParent(statusRow.transform, false);
            _detailStatus = statusGo.AddComponent<TextMeshProUGUI>();
            _detailStatus.fontSize = 13;
            _detailStatus.color = new Color(1f, 0.75f, 0.3f);
            _detailStatus.alignment = TextAlignmentOptions.MidlineLeft;
            _detailStatus.raycastTarget = false;
            var statusLe = statusGo.AddComponent<LayoutElement>();
            statusLe.preferredHeight = 22;
            statusLe.flexibleWidth = 1;

            // Buy row
            var buyRow = new GameObject("BuyRow", typeof(RectTransform));
            buyRow.transform.SetParent(_detailContent.transform, false);
            var buyHlg = buyRow.AddComponent<HorizontalLayoutGroup>();
            buyHlg.spacing = 14;
            buyHlg.childForceExpandWidth = true;
            buyHlg.childForceExpandHeight = false;
            buyHlg.childAlignment = TextAnchor.MiddleCenter;
            buyRow.AddComponent<LayoutElement>().preferredHeight = 58;

            _buyDollarBtn = MakeButton(buyRow.transform, "", new Color(0.18f, 0.50f, 0.22f), -1, 58);
            _buyDollarLabel = _buyDollarBtn.GetComponentInChildren<TMP_Text>();
            _buyDollarBtn.onClick.AddListener(OnBuyWithDollars);
            AddButtonLeftIcon(_buyDollarBtn.transform, "UI/Icons/Infos/dollars", 32);

            _buyIngotBtn = MakeButton(buyRow.transform, "", new Color(0.65f, 0.48f, 0.10f), -1, 58);
            _buyIngotLabel = _buyIngotBtn.GetComponentInChildren<TMP_Text>();
            _buyIngotBtn.onClick.AddListener(OnBuyWithIngots);
            AddButtonLeftIcon(_buyIngotBtn.transform, "UI/Icons/Infos/gold", 32);

            _detailContent.SetActive(false);
            return panel;
        }

        private void ShowDetailPlaceholder()
        {
            if (_detailContent != null) _detailContent.SetActive(false);
            if (_detailPlaceholder != null) _detailPlaceholder.SetActive(true);
        }

        private void ShowDetailContent()
        {
            if (_detailPlaceholder != null) _detailPlaceholder.SetActive(false);
            if (_detailContent != null) _detailContent.SetActive(true);
        }

        // ---- Catalog card ----

        private void SpawnCatalogCard(VehicleData data, bool owned, int ownedCount, bool unlocked, bool canAfford, bool depotFull)
        {
            // ---- Root card: solid, opaque, no overlap with siblings ----
            var card = new GameObject($"Card_{data.id}", typeof(RectTransform));
            card.transform.SetParent(_catalogContent, false);
            var cardBg = card.AddComponent<Image>();
            cardBg.sprite = _sprR16;
            cardBg.type   = Image.Type.Sliced;
            cardBg.color  = unlocked ? BgElevated : new Color(0x26 / 255f, 0x29 / 255f, 0x30 / 255f, 1f);
            var cardShadow = card.AddComponent<Shadow>();
            cardShadow.effectColor    = new Color(0f, 0f, 0f, 0.5f);
            cardShadow.effectDistance = new Vector2(0f, -4f);
            var cardLe = card.AddComponent<LayoutElement>();
            cardLe.preferredHeight = CARD_HEIGHT;
            cardLe.minHeight = CARD_HEIGHT;

            // Whole-card click
            var cardBtn = card.AddComponent<Button>();
            var cardColors = ColorBlock.defaultColorBlock;
            cardColors.normalColor      = Color.white;
            cardColors.highlightedColor = new Color(1.08f, 1.10f, 1.18f, 1f);
            cardColors.pressedColor     = new Color(0.85f, 0.88f, 0.95f, 1f);
            cardColors.colorMultiplier  = 1f;
            cardBtn.colors = cardColors;
            cardBtn.targetGraphic = cardBg;
            cardBtn.onClick.AddListener(() => OpenDetail(data));

            // ---- Left accent stripe (category color) ----
            var stripeGo = new GameObject("Stripe", typeof(RectTransform));
            stripeGo.transform.SetParent(card.transform, false);
            var stripeRt = stripeGo.GetComponent<RectTransform>();
            stripeRt.anchorMin = new Vector2(0, 0);
            stripeRt.anchorMax = new Vector2(0, 1);
            stripeRt.pivot     = new Vector2(0, 0.5f);
            stripeRt.offsetMin = new Vector2(4, 12);
            stripeRt.offsetMax = new Vector2(8, -12);
            var stripeImg = stripeGo.AddComponent<Image>();
            stripeImg.sprite        = _sprR8;
            stripeImg.type          = Image.Type.Sliced;
            stripeImg.color         = unlocked ? CategoryAccent(data.category) : new Color(0.35f, 0.35f, 0.40f, 0.5f);
            stripeImg.raycastTarget = false;

            // ---- Category badge (large rounded letter, sober) ----
            var badgeZone = new GameObject("Badge", typeof(RectTransform));
            badgeZone.transform.SetParent(card.transform, false);
            var badgeRt = badgeZone.GetComponent<RectTransform>();
            badgeRt.anchorMin = new Vector2(0, 0.5f);
            badgeRt.anchorMax = new Vector2(0, 0.5f);
            badgeRt.pivot     = new Vector2(0, 0.5f);
            badgeRt.anchoredPosition = new Vector2(24, 0);
            badgeRt.sizeDelta = new Vector2(64, 64);

            var badgeBg = badgeZone.AddComponent<Image>();
            badgeBg.sprite        = _sprR12;
            badgeBg.type          = Image.Type.Sliced;
            badgeBg.color         = BgPill;
            badgeBg.raycastTarget = false;

            // Icon on top of badge if available, else category letter
            if (data.icon != null)
            {
                var iconHolder = new GameObject("Icon", typeof(RectTransform));
                iconHolder.transform.SetParent(badgeZone.transform, false);
                var iconRt = iconHolder.GetComponent<RectTransform>();
                iconRt.anchorMin = Vector2.zero;
                iconRt.anchorMax = Vector2.one;
                iconRt.offsetMin = new Vector2(6, 6);
                iconRt.offsetMax = new Vector2(-6, -6);
                var iconImg = iconHolder.AddComponent<Image>();
                iconImg.sprite = data.icon;
                iconImg.preserveAspect = true;
                iconImg.color = unlocked ? Color.white : new Color(0.5f, 0.5f, 0.5f, 0.55f);
                iconImg.raycastTarget = false;
            }
            else
            {
                var letterGo = new GameObject("Letter", typeof(RectTransform));
                letterGo.transform.SetParent(badgeZone.transform, false);
                var letterRt = letterGo.GetComponent<RectTransform>();
                StretchFull(letterRt);
                var letterTmp = letterGo.AddComponent<TextMeshProUGUI>();
                letterTmp.text = data.category.ToString().Substring(0, 1).ToUpper();
                letterTmp.fontSize = 30;
                letterTmp.fontStyle = FontStyles.Bold;
                letterTmp.color = unlocked
                    ? CategoryAccent(data.category)
                    : new Color(0.45f, 0.45f, 0.50f, 0.7f);
                letterTmp.alignment = TextAlignmentOptions.Center;
                letterTmp.raycastTarget = false;
            }

            // ---- Text content (no fade overlay, clean offset) ----
            var textZone = new GameObject("TextZone", typeof(RectTransform));
            textZone.transform.SetParent(card.transform, false);
            var textZoneRt = textZone.GetComponent<RectTransform>();
            textZoneRt.anchorMin = new Vector2(0, 0);
            textZoneRt.anchorMax = new Vector2(1, 1);
            textZoneRt.offsetMin = new Vector2(108, 0);
            textZoneRt.offsetMax = new Vector2(-180, 0);

            var textVlg = textZone.AddComponent<VerticalLayoutGroup>();
            textVlg.childForceExpandWidth = true;
            textVlg.childForceExpandHeight = false;
            textVlg.childAlignment = TextAnchor.MiddleLeft;
            textVlg.padding = new RectOffset(0, 0, 20, 20);
            textVlg.spacing = 6;

            var nameGo = new GameObject("Name", typeof(RectTransform));
            nameGo.transform.SetParent(textZone.transform, false);
            var nameTmp = nameGo.AddComponent<TextMeshProUGUI>();
            nameTmp.text = data.displayName;
            nameTmp.fontSize = 17;
            nameTmp.fontStyle = FontStyles.Bold;
            nameTmp.color = unlocked ? Color.white : new Color(0.55f, 0.55f, 0.60f);
            nameTmp.raycastTarget = false;
            nameTmp.overflowMode = TextOverflowModes.Ellipsis;
            nameGo.AddComponent<LayoutElement>().preferredHeight = 24;

            var subGo = new GameObject("Sub", typeof(RectTransform));
            subGo.transform.SetParent(textZone.transform, false);
            var subTmp = subGo.AddComponent<TextMeshProUGUI>();
            string ownedBadge = ownedCount > 0
                ? $"     <color=#66ccff>● {ownedCount} possédé{(ownedCount > 1 ? "s" : "")}</color>"
                : "";
            string dot = "<color=#3a4458>  •  </color>";
            subTmp.text = unlocked
                ? $"<color=#8899bb>{data.category}</color>{dot}<color=#c8d2e0>{data.capacity}t</color>{dot}<color=#c8d2e0>{data.speedKmh} km/h</color>{dot}<color=#f0c060>${data.purchasePrice:N0}</color>{ownedBadge}"
                : $"<color=#cc7733>◆ Niveau entreprise {data.minCompanyLevelRequired} requis</color>";
            subTmp.fontSize = 12;
            subTmp.color = new Color(0.70f, 0.72f, 0.80f);
            subTmp.raycastTarget = false;
            subTmp.overflowMode = TextOverflowModes.Ellipsis;
            subGo.AddComponent<LayoutElement>().preferredHeight = 18;

            // ---- "Voir plus" chip (right side, modern pill) ----
            var chipGo = new GameObject("Chip", typeof(RectTransform));
            chipGo.transform.SetParent(card.transform, false);
            var chipRt = chipGo.GetComponent<RectTransform>();
            chipRt.anchorMin = new Vector2(1, 0.5f);
            chipRt.anchorMax = new Vector2(1, 0.5f);
            chipRt.pivot     = new Vector2(1f, 0.5f);
            chipRt.anchoredPosition = new Vector2(-24, 0);
            chipRt.sizeDelta = new Vector2(132, 40);

            var chipImg = chipGo.AddComponent<Image>();
            chipImg.sprite        = _sprR8;
            chipImg.type          = Image.Type.Sliced;
            chipImg.color         = unlocked
                ? new Color(AccentBlue.r, AccentBlue.g, AccentBlue.b, 0.16f)
                : BgPill;
            chipImg.raycastTarget = false;

            var chipLblGo = new GameObject("Label", typeof(RectTransform));
            chipLblGo.transform.SetParent(chipGo.transform, false);
            var chipLblRt = chipLblGo.GetComponent<RectTransform>();
            StretchFull(chipLblRt);
            var chipLbl = chipLblGo.AddComponent<TextMeshProUGUI>();
            chipLbl.text = unlocked ? "Voir plus  ›" : "Verrouillé";
            chipLbl.fontSize = 13;
            chipLbl.fontStyle = FontStyles.Bold;
            chipLbl.color = unlocked ? new Color(0.62f, 0.78f, 1f) : TextDim;
            chipLbl.alignment = TextAlignmentOptions.Center;
            chipLbl.raycastTarget = false;
        }

        private static Color CategoryAccent(VehicleCategory cat)
        {
            // Subtle, on-brand accent per category — keeps the dark/blue palette
            switch (cat)
            {
                case VehicleCategory.Fourgonnette:       return new Color(0.40f, 0.72f, 1.00f, 1f); // blue
                case VehicleCategory.Camion:             return new Color(0.95f, 0.72f, 0.32f, 1f); // amber
                case VehicleCategory.PoidsLourd:         return new Color(0.95f, 0.55f, 0.35f, 1f); // orange
                case VehicleCategory.SemiRemorque:       return new Color(0.85f, 0.45f, 0.55f, 1f); // rose
                case VehicleCategory.ConvoiExceptionnel: return new Color(0.75f, 0.55f, 1.00f, 1f); // purple
                default:                                 return new Color(0.55f, 0.78f, 0.95f, 1f);
            }
        }


        // ---- Detail open/close ----

        private void OpenDetail(VehicleData data)
        {
            _selectedVehicle = data;

            var wallet = ServiceLocator.Get<WalletSystem>();
            var depot = ServiceLocator.Get<DepotSystem>();
            var xp = ServiceLocator.Get<XpSystem>();
            var fleet = ServiceLocator.Get<FleetSystem>();

            bool unlocked = xp == null || xp.IsVehicleUnlocked(data.minCompanyLevelRequired);
            bool depotFull = depot != null && !depot.HasRoomForOneMore();
            bool canAffordDollar = wallet != null && wallet.CanAfford(CurrencyType.Dollar, data.purchasePrice);
            int ingotCost = Mathf.Max(1, data.purchasePrice / 1000);
            bool canAffordIngot = wallet != null && wallet.CanAfford(CurrencyType.GoldIngot, ingotCost);

            int ownedCount = 0;
            if (fleet != null)
                foreach (var v in fleet.Vehicles)
                    if (v.vehicleDataId == data.id) ownedCount++;

            _detailName.text = data.displayName;
            _detailCategory.text = $"<b>{data.category}</b>   <color=#3a4458>•</color>   Niveau min: <b>{data.minCompanyLevelRequired}</b>   <color=#3a4458>•</color>   Profil: <b>{data.routingProfile}</b>";

            // Hero image: use sprite if available, else show category letter as placeholder
            if (data.icon != null)
            {
                _detailImage.sprite = data.icon;
                _detailImage.color = Color.white;
                _detailImage.enabled = true;
                _detailImagePlaceholder.text = "";
            }
            else
            {
                _detailImage.sprite = null;
                _detailImage.enabled = false;
                _detailImagePlaceholder.text = data.category.ToString().Substring(0, 1).ToUpper();
            }

            // Format helpers
            string lbl(string s) => $"<color=#7d8aa3>{s}</color>";
            string val(string s) => $"<color=#e8ecf3><b>{s}</b></color>";
            string secHdr(string s) => $"<color=#5fa8ff><size=12><b>{s.ToUpper()}</b></size></color>";

            float range = data.MaxRangeKm();
            int payloadKg = data.capacity * 1000;
            float fuelPerKm = data.fuelConsumptionLPer100Km / 100f;

            // ---- TOP-LEFT: technical specs (mise en page aérée avec lignes alignées) ----
            // Utilise des tabulations TMP <pos=> pour aligner les colonnes label/valeur
            string row(string label, string value) =>
                $"<pos=8>{lbl(label)}<pos=160>{value}";

            _detailSpecs.text =
                $"{secHdr("Performance")}\n\n" +
                row("Capacité",        val(data.capacity + " t")) + "\n" +
                row("Charge utile",    val(payloadKg.ToString("N0") + " kg")) + "\n" +
                row("Vitesse max",     val(data.speedKmh + " km/h")) + "\n" +
                row("Durabilité",      val(data.maxKilometers.ToString("N0") + " km")) + "\n\n" +
                $"{secHdr("Carburant")}\n\n" +
                row("Réservoir",       val(data.fuelTankCapacityLiters.ToString("0") + " L")) + "\n" +
                row("Consommation",    val(data.fuelConsumptionLPer100Km.ToString("0.0") + " L/100km")) + "\n" +
                row("Autonomie",       val(range.ToString("0") + " km")) + "\n" +
                row("Coût / km",       val(fuelPerKm.ToString("0.00") + " L")) + "\n\n" +
                $"{secHdr("Économie")}\n\n" +
                row("Prix d'achat",    $"<color=#f0c060><b>${data.purchasePrice:N0}</b></color>") + "\n" +
                row("Lingots",         $"<color=#e8b850><b>{ingotCost} ◆</b></color>") + "\n" +
                row("Entretien",       val("$" + data.maintenanceCost.ToString("N0") + " / contrat"));

            // ---- BOTTOM: career statistics for this vehicle model ----
            // TODO: branch real tracking system. For now, zeros until the model has been used.
            int kmDriven          = 0;
            int dollarsEarned     = 0;
            int contractsDone     = 0;
            int driversHosted     = 0;
            int fuelBurnt         = 0;
            int citiesVisited     = 0;
            int countriesVisited  = 0;
            int packagesDelivered = 0;
            int hoursOnTheRoad    = 0;
            int breakdowns        = 0;

            bool hasHistory = ownedCount > 0 || kmDriven > 0;
            string fmt(int n, string suffix = "") => hasHistory
                ? $"{n:N0}{suffix}"
                : "<color=#3a4458>—</color>";
            string fmtMoney(int n) => hasHistory
                ? $"${n:N0}"
                : "<color=#3a4458>—</color>";

            // Two-column career stats. Use colored bullets (• ★ ▲) — these glyphs exist in
            // every standard TMP font, so no "missing character" boxes.
            string bullet(string color) => $"<color={color}>●</color>";
            string statL(string color, string label, string value) =>
                $"<pos=4>{bullet(color)}  {lbl(label)}<pos=260>{val(value)}";
            string statR(string color, string label, string value) =>
                $"<pos=440>{bullet(color)}  {lbl(label)}<pos=720>{val(value)}";

            string careerSubtitle = hasHistory
                ? "<color=#5a6d8a><size=11>(total cumulé sur ce modèle)</size></color>"
                : "<color=#cc7733><size=11>(aucune donnée — modèle non encore utilisé)</size></color>";

            _detailCareerStats.text =
                $"{secHdr("Statistiques de carrière")}    {careerSubtitle}\n\n" +
                statL("#f0c060", "Argent gagné",          fmtMoney(dollarsEarned)) +
                statR("#5fa8ff", "Kilomètres parcourus",  fmt(kmDriven, " km")) + "\n" +
                statL("#7ed957", "Contrats finalisés",    fmt(contractsDone)) +
                statR("#d385ff", "Conducteurs accueillis", fmt(driversHosted)) + "\n" +
                statL("#ff9a3c", "Carburant consommé",   fmt(fuelBurnt, " L")) +
                statR("#66d4cf", "Villes desservies",    fmt(citiesVisited)) + "\n" +
                statL("#a8d8ff", "Pays visités",          fmt(countriesVisited)) +
                statR("#e8b850", "Colis livrés",          fmt(packagesDelivered)) + "\n" +
                statL("#9bb1d4", "Heures sur la route",   fmt(hoursOnTheRoad, " h")) +
                statR("#ff6b6b", "Pannes",                 fmt(breakdowns));

            // Status message
            if (!unlocked)
            {
                _detailStatus.text = $"🔒 Requiert niveau entreprise {data.minCompanyLevelRequired}";
                if (_detailStatusIcon != null) _detailStatusIcon.gameObject.SetActive(false);
            }
            else if (depotFull)
            {
                _detailStatus.text = "Dépôt plein — améliorez le hangar";
                if (_detailStatusIcon != null) _detailStatusIcon.gameObject.SetActive(true);
            }
            else
            {
                _detailStatus.text = "";
                if (_detailStatusIcon != null) _detailStatusIcon.gameObject.SetActive(false);
            }

            bool canBuy = unlocked && !depotFull;

            _buyDollarLabel.text = $"Acheter\n${data.purchasePrice:N0}";
            _buyDollarBtn.interactable = canBuy && canAffordDollar;
            var dollarImg = _buyDollarBtn.GetComponent<Image>();
            dollarImg.color = (canBuy && canAffordDollar)
                ? new Color(0.18f, 0.50f, 0.22f)
                : new Color(0.28f, 0.28f, 0.32f);

            _buyIngotLabel.text = $"Acheter\n{ingotCost} lingots";
            _buyIngotBtn.interactable = canBuy && canAffordIngot;
            var ingotImg = _buyIngotBtn.GetComponent<Image>();
            ingotImg.color = (canBuy && canAffordIngot)
                ? new Color(0.65f, 0.48f, 0.10f)
                : new Color(0.28f, 0.28f, 0.32f);

            // Both panels stay visible — just swap the right pane from placeholder to content
            ShowDetailContent();
        }

        private void CloseDetail()
        {
            _selectedVehicle = null;
            ShowDetailPlaceholder();
        }

        // ---- Buy actions ----

        private void OnBuyWithDollars()
        {
            if (_selectedVehicle == null) return;
            if (_purchase.TryPurchase(_selectedVehicle, out _, out var err))
            {
                CloseDetail();
                Refresh();
            }
            else
            {
                _detailStatus.text = $"Achat impossible : {err}";
            }
        }

        private void OnBuyWithIngots()
        {
            if (_selectedVehicle == null) return;
            var wallet = ServiceLocator.Get<WalletSystem>();
            var depot = ServiceLocator.Get<DepotSystem>();
            var fleet = ServiceLocator.Get<FleetSystem>();
            if (wallet == null || depot == null || fleet == null) return;

            int ingotCost = Mathf.Max(1, _selectedVehicle.purchasePrice / 1000);
            if (!depot.HasRoomForOneMore())
            {
                _detailStatus.text = "Dépôt plein";
                if (_detailStatusIcon != null) _detailStatusIcon.gameObject.SetActive(true);
                return;
            }
            if (!wallet.TrySpend(CurrencyType.GoldIngot, ingotCost)) { _detailStatus.text = "Lingots insuffisants"; return; }

            var instance = new Entities.Vehicles.VehicleInstance
            {
                instanceId = System.Guid.NewGuid().ToString(),
                vehicleDataId = _selectedVehicle.id,
                totalKilometers = 0,
                currentFuelLiters = _selectedVehicle.fuelTankCapacityLiters,
                status = VehicleStatus.Idle
            };
            fleet.Add(instance);
            CloseDetail();
            Refresh();
        }

        // ---- Lifecycle ----

        private void OnEnable()
        {
            GameEvents.OnVehicleAdded += OnVehicleEvent;
            GameEvents.OnVehicleStatusChanged += OnVehicleEvent;
            GameEvents.OnDollarsChanged += OnIntEvent;
            GameEvents.OnGoldIngotsChanged += OnIntEvent;
            GameEvents.OnDockUnlocked += OnIntEvent;
            GameEvents.OnCompanyXpChanged += OnCompanyXp;
            Refresh();
        }

        private void OnDisable()
        {
            GameEvents.OnVehicleAdded -= OnVehicleEvent;
            GameEvents.OnVehicleStatusChanged -= OnVehicleEvent;
            GameEvents.OnDollarsChanged -= OnIntEvent;
            GameEvents.OnGoldIngotsChanged -= OnIntEvent;
            GameEvents.OnDockUnlocked -= OnIntEvent;
            GameEvents.OnCompanyXpChanged -= OnCompanyXp;
        }

        private void OnVehicleEvent(Entities.Vehicles.VehicleInstance _) => Refresh();
        private void OnIntEvent(int _) => Refresh();
        private void OnCompanyXp(int xp, int level) => Refresh();

        private void Refresh()
        {
            if (_catalogContent == null) return;

            // Clear catalog cards
            foreach (Transform child in _catalogContent)
            {
#if UNITY_EDITOR
                DestroyImmediate(child.gameObject);
#else
                Destroy(child.gameObject);
#endif
            }

            var catalog = ServiceLocator.Get<VehicleCatalog>();
            var fleet = ServiceLocator.Get<FleetSystem>();
            var depot = ServiceLocator.Get<DepotSystem>();
            var wallet = ServiceLocator.Get<WalletSystem>();
            var xp = ServiceLocator.Get<XpSystem>();
            if (catalog == null) return;

            int used = depot != null ? depot.GetUsedSlots() : (fleet?.Vehicles.Count ?? 0);
            int max = depot != null ? depot.MaxVehicleSlots : 0;
            int companyLevel = xp != null ? xp.CompanyLevel : 1;

            if (_fleetCountLabel)
                _fleetCountLabel.text = $"Flotte: {used}/{max}  |  Niv. {companyLevel}";

            bool depotFull = depot != null && !depot.HasRoomForOneMore();

            foreach (var data in catalog.vehicles)
            {
                bool unlocked = xp == null || xp.IsVehicleUnlocked(data.minCompanyLevelRequired);
                bool canAfford = wallet != null && wallet.CanAfford(CurrencyType.Dollar, data.purchasePrice);
                int ownedCount = 0;
                if (fleet != null)
                    foreach (var v in fleet.Vehicles)
                        if (v.vehicleDataId == data.id) ownedCount++;

                SpawnCatalogCard(data, ownedCount > 0, ownedCount, unlocked, canAfford, depotFull);
            }

            // Keep the right pane in sync if a vehicle is currently selected
            if (_selectedVehicle != null && _detailContent != null && _detailContent.activeSelf)
                OpenDetail(_selectedVehicle);
        }

        // ---- Helpers ----

        private static void AddButtonLeftIcon(Transform btn, string spritePath, int size)
        {
            var sprite = Resources.Load<Sprite>(spritePath);
            if (sprite == null) return;
            var go = new GameObject("Icon", typeof(RectTransform));
            go.transform.SetParent(btn, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0.5f);
            rt.anchorMax = new Vector2(0, 0.5f);
            rt.pivot = new Vector2(0, 0.5f);
            rt.anchoredPosition = new Vector2(8, 0);
            rt.sizeDelta = new Vector2(size, size);
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
        }

        private Button MakeButton(Transform parent, string label, Color color, float width, float height)
        {
            var go = new GameObject("Btn_" + label, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var img = go.AddComponent<Image>();
            img.sprite = _sprR12;
            img.type   = Image.Type.Sliced;
            img.color  = color;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            var le = go.AddComponent<LayoutElement>();
            if (width > 0) le.preferredWidth = width;
            le.preferredHeight = height;

            var lblGo = new GameObject("Label", typeof(RectTransform));
            lblGo.transform.SetParent(go.transform, false);
            var lblRt = lblGo.GetComponent<RectTransform>();
            lblRt.anchorMin = Vector2.zero;
            lblRt.anchorMax = Vector2.one;
            lblRt.offsetMin = new Vector2(4, 2);
            lblRt.offsetMax = new Vector2(-4, -2);

            var tmp = lblGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 13;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;

            return btn;
        }

        private static void AddDivider(Transform parent)
        {
            var go = new GameObject("Divider", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.AddComponent<Image>().color = DividerCol;
            go.AddComponent<LayoutElement>().preferredHeight = 1;
        }

        // Sous-carte encadrée : coins arrondis + légère ombre, comme les cartes du panneau de contrats.
        private void StyleInsetCard(GameObject go)
        {
            var img = go.GetComponent<Image>() ?? go.AddComponent<Image>();
            img.sprite        = _sprR12;
            img.type          = Image.Type.Sliced;
            img.color         = BgInset;
            img.raycastTarget = false;
            var sh = go.AddComponent<Shadow>();
            sh.effectColor    = new Color(0f, 0f, 0f, 0.45f);
            sh.effectDistance = new Vector2(0f, -3f);
        }

        // ── Fabrique de sprites arrondis (9-slice, générés une fois) ────────────────
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

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private void Update()
        {
            if (_bgRect == null) return;
            var size = _bgRect.rect.size;
            if (size != _lastBgSize)
            {
                _lastBgSize = size;
                UpdateBackgroundUVs();
            }
        }

        private void UpdateBackgroundUVs()
        {
            if (_bgImage == null || _bgTex == null || _bgRect == null) return;

            float containerAspect = _bgRect.rect.width / Mathf.Max(1f, _bgRect.rect.height);
            float texAspect = (float)_bgTex.width / Mathf.Max(1, _bgTex.height);

            float u = 0f, v = 0f, uw = 1f, vh = 1f;
            if (containerAspect > texAspect)
            {
                vh = texAspect / containerAspect;
                v = (1f - vh) * 0.5f;
            }
            else
            {
                uw = containerAspect / texAspect;
                u = (1f - uw) * 0.5f;
            }
            _bgImage.uvRect = new Rect(u, v, uw, vh);
        }
    }
}
