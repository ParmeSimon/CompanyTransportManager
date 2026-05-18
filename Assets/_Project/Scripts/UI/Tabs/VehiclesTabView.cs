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

        private VehicleData _selectedVehicle;

        private RawImage _bgImage;
        private RectTransform _bgRect;
        private Texture2D _bgTex;
        private Vector2 _lastBgSize;

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

            var rt = GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            // Background
            var bgGo = new GameObject("Background", typeof(RectTransform));
            bgGo.transform.SetParent(transform, false);
            var bgImg = bgGo.AddComponent<RawImage>();
            var bgTex = Resources.Load<Texture2D>("UI/VehicleBackground");
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

            _listPanel = BuildListPanel();
            _detailPanel = BuildDetailPanel();
            _detailPanel.SetActive(false);
        }

        // ---- List panel ----

        private GameObject BuildListPanel()
        {
            var panel = new GameObject("ListPanel", typeof(RectTransform));
            panel.transform.SetParent(transform, false);
            StretchFull(panel.GetComponent<RectTransform>());

            // Header bar
            var header = new GameObject("Header", typeof(RectTransform));
            header.transform.SetParent(panel.transform, false);
            var headerRt = header.GetComponent<RectTransform>();
            headerRt.anchorMin = new Vector2(0, 1);
            headerRt.anchorMax = new Vector2(1, 1);
            headerRt.pivot = new Vector2(0.5f, 1f);
            headerRt.offsetMin = new Vector2(0, -56);
            headerRt.offsetMax = Vector2.zero;

            var headerBg = header.AddComponent<Image>();
            headerBg.color = new Color(0.06f, 0.06f, 0.10f, 0.92f);

            var headerHlg = header.AddComponent<HorizontalLayoutGroup>();
            headerHlg.padding = new RectOffset(20, 20, 0, 0);
            headerHlg.childAlignment = TextAnchor.MiddleLeft;
            headerHlg.childForceExpandWidth = false;
            headerHlg.childForceExpandHeight = true;

            var titleGo = new GameObject("Title", typeof(RectTransform));
            titleGo.transform.SetParent(header.transform, false);
            var titleTmp = titleGo.AddComponent<TextMeshProUGUI>();
            titleTmp.text = "GARAGE — Catalogue";
            titleTmp.fontSize = 18;
            titleTmp.fontStyle = FontStyles.Bold;
            titleTmp.color = Color.white;
            titleGo.AddComponent<LayoutElement>().flexibleWidth = 1;

            var countGo = new GameObject("FleetCount", typeof(RectTransform));
            countGo.transform.SetParent(header.transform, false);
            _fleetCountLabel = countGo.AddComponent<TextMeshProUGUI>();
            _fleetCountLabel.fontSize = 14;
            _fleetCountLabel.color = new Color(0.7f, 0.85f, 1f);
            _fleetCountLabel.alignment = TextAlignmentOptions.Right;
            countGo.AddComponent<LayoutElement>().preferredWidth = 200;

            // Scroll view
            var scrollGo = new GameObject("Scroll", typeof(RectTransform));
            scrollGo.transform.SetParent(panel.transform, false);
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.anchorMin = new Vector2(0, 0);
            scrollRt.anchorMax = new Vector2(1, 1);
            scrollRt.offsetMin = new Vector2(12, 12);
            scrollRt.offsetMax = new Vector2(-12, -60);

            var scrollRect = scrollGo.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.scrollSensitivity = 30;

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
            vlg.spacing = 10;
            vlg.padding = new RectOffset(0, 0, 10, 10);
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var csf = contentGo.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.content = _catalogContent;

            return panel;
        }

        // ---- Detail panel ----

        private GameObject BuildDetailPanel()
        {
            var overlay = new GameObject("DetailOverlay", typeof(RectTransform));
            overlay.transform.SetParent(transform, false);
            StretchFull(overlay.GetComponent<RectTransform>());
            overlay.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);

            var card = new GameObject("DetailCard", typeof(RectTransform));
            card.transform.SetParent(overlay.transform, false);
            var cardRt = card.GetComponent<RectTransform>();
            cardRt.anchorMin = new Vector2(0.5f, 0.5f);
            cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardRt.pivot = new Vector2(0.5f, 0.5f);
            cardRt.sizeDelta = new Vector2(520, 480);
            cardRt.anchoredPosition = Vector2.zero;

            var cardBg = card.AddComponent<Image>();
            cardBg.color = new Color(0.08f, 0.09f, 0.14f, 0.98f);

            var vlg = card.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(24, 24, 20, 20);
            vlg.spacing = 12;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childAlignment = TextAnchor.UpperCenter;

            // Title row (name + close button)
            var titleRow = new GameObject("TitleRow", typeof(RectTransform));
            titleRow.transform.SetParent(card.transform, false);
            var titleHlg = titleRow.AddComponent<HorizontalLayoutGroup>();
            titleHlg.childForceExpandWidth = false;
            titleHlg.childForceExpandHeight = false;
            titleHlg.childAlignment = TextAnchor.MiddleLeft;
            titleRow.AddComponent<LayoutElement>().preferredHeight = 32;

            var nameGo = new GameObject("Name", typeof(RectTransform));
            nameGo.transform.SetParent(titleRow.transform, false);
            _detailName = nameGo.AddComponent<TextMeshProUGUI>();
            _detailName.fontSize = 20;
            _detailName.fontStyle = FontStyles.Bold;
            _detailName.color = Color.white;
            nameGo.AddComponent<LayoutElement>().flexibleWidth = 1;

            var closeBtn = MakeButton(titleRow.transform, "✕", new Color(0.6f, 0.15f, 0.15f), 36, 32);
            closeBtn.onClick.AddListener(CloseDetail);

            // Category badge
            var catGo = new GameObject("Category", typeof(RectTransform));
            catGo.transform.SetParent(card.transform, false);
            _detailCategory = catGo.AddComponent<TextMeshProUGUI>();
            _detailCategory.fontSize = 12;
            _detailCategory.color = new Color(0.6f, 0.75f, 1f);
            catGo.AddComponent<LayoutElement>().preferredHeight = 16;

            // Divider
            AddDivider(card.transform);

            // Stats block
            var statsGo = new GameObject("Stats", typeof(RectTransform));
            statsGo.transform.SetParent(card.transform, false);
            _detailStats = statsGo.AddComponent<TextMeshProUGUI>();
            _detailStats.fontSize = 13;
            _detailStats.color = new Color(0.85f, 0.85f, 0.90f);
            _detailStats.lineSpacing = 6;
            statsGo.AddComponent<LayoutElement>().preferredHeight = 130;

            AddDivider(card.transform);

            // Status line (locked / depot full / etc.)
            var statusGo = new GameObject("Status", typeof(RectTransform));
            statusGo.transform.SetParent(card.transform, false);
            _detailStatus = statusGo.AddComponent<TextMeshProUGUI>();
            _detailStatus.fontSize = 12;
            _detailStatus.color = new Color(1f, 0.75f, 0.3f);
            _detailStatus.alignment = TextAlignmentOptions.Center;
            statusGo.AddComponent<LayoutElement>().preferredHeight = 20;

            // Buy row
            var buyRow = new GameObject("BuyRow", typeof(RectTransform));
            buyRow.transform.SetParent(card.transform, false);
            var buyHlg = buyRow.AddComponent<HorizontalLayoutGroup>();
            buyHlg.spacing = 12;
            buyHlg.childForceExpandWidth = true;
            buyHlg.childForceExpandHeight = false;
            buyHlg.childAlignment = TextAnchor.MiddleCenter;
            buyRow.AddComponent<LayoutElement>().preferredHeight = 52;

            _buyDollarBtn = MakeButton(buyRow.transform, "", new Color(0.18f, 0.50f, 0.22f), -1, 52);
            _buyDollarLabel = _buyDollarBtn.GetComponentInChildren<TMP_Text>();
            _buyDollarBtn.onClick.AddListener(OnBuyWithDollars);

            _buyIngotBtn = MakeButton(buyRow.transform, "", new Color(0.65f, 0.48f, 0.10f), -1, 52);
            _buyIngotLabel = _buyIngotBtn.GetComponentInChildren<TMP_Text>();
            _buyIngotBtn.onClick.AddListener(OnBuyWithIngots);

            return overlay;
        }

        // ---- Catalog card ----

        private void SpawnCatalogCard(VehicleData data, bool owned, int ownedCount, bool unlocked, bool canAfford, bool depotFull)
        {
            // ---- Root card (no LayoutGroup — children positioned manually) ----
            var card = new GameObject($"Card_{data.id}", typeof(RectTransform));
            card.transform.SetParent(_catalogContent, false);
            card.AddComponent<Image>().color = new Color(0.08f, 0.09f, 0.13f, 0.92f);
            var cardLe = card.AddComponent<LayoutElement>();
            cardLe.preferredHeight = 96;

            // Make card a Button (entire surface clickable)
            var cardBtn = card.AddComponent<Button>();
            var cardColors = ColorBlock.defaultColorBlock;
            cardColors.normalColor = Color.white;
            cardColors.highlightedColor = new Color(1f, 1f, 1f, 0.92f);
            cardColors.pressedColor = new Color(0.85f, 0.85f, 0.85f);
            cardBtn.colors = cardColors;
            cardBtn.targetGraphic = card.GetComponent<Image>();
            cardBtn.onClick.AddListener(() => OpenDetail(data));

            // ---- Icon zone (left, fixed width 120) ----
            var iconZone = new GameObject("IconZone", typeof(RectTransform));
            iconZone.transform.SetParent(card.transform, false);
            var iconZoneRt = iconZone.GetComponent<RectTransform>();
            iconZoneRt.anchorMin = new Vector2(0, 0);
            iconZoneRt.anchorMax = new Vector2(0, 1);
            iconZoneRt.offsetMin = Vector2.zero;
            iconZoneRt.offsetMax = new Vector2(130, 0);

            // Icon image
            if (data.icon != null)
            {
                var iconImg = iconZone.AddComponent<Image>();
                iconImg.sprite = data.icon;
                iconImg.preserveAspect = true;
                iconImg.color = unlocked ? Color.white : new Color(0.5f, 0.5f, 0.5f, 0.6f);
                iconImg.raycastTarget = false;
            }
            else
            {
                // Fallback: colored box with category letter
                var fallback = iconZone.AddComponent<Image>();
                fallback.color = unlocked ? new Color(0.18f, 0.22f, 0.32f) : new Color(0.15f, 0.15f, 0.18f);
                fallback.raycastTarget = false;
                var letterGo = new GameObject("Letter", typeof(RectTransform));
                letterGo.transform.SetParent(iconZone.transform, false);
                var letterRt = letterGo.GetComponent<RectTransform>();
                letterRt.anchorMin = Vector2.zero;
                letterRt.anchorMax = Vector2.one;
                letterRt.offsetMin = Vector2.zero;
                letterRt.offsetMax = Vector2.zero;
                var letterTmp = letterGo.AddComponent<TextMeshProUGUI>();
                letterTmp.text = data.category.ToString().Substring(0, 1).ToUpper();
                letterTmp.fontSize = 32;
                letterTmp.fontStyle = FontStyles.Bold;
                letterTmp.color = new Color(0.4f, 0.5f, 0.7f, 0.8f);
                letterTmp.alignment = TextAlignmentOptions.Center;
                letterTmp.raycastTarget = false;
            }

            // ---- Fade gradient overlay (left→right, transparent→card color) ----
            var fadeGo = new GameObject("Fade", typeof(RectTransform));
            fadeGo.transform.SetParent(card.transform, false);
            var fadeRt = fadeGo.GetComponent<RectTransform>();
            fadeRt.anchorMin = new Vector2(0, 0);
            fadeRt.anchorMax = new Vector2(0, 1);
            fadeRt.offsetMin = new Vector2(80, 0);
            fadeRt.offsetMax = new Vector2(210, 0);
            var fadeRaw = fadeGo.AddComponent<RawImage>();
            fadeRaw.raycastTarget = false;
            var fadeTex = new Texture2D(2, 1, TextureFormat.RGBA32, false);
            fadeTex.wrapMode = TextureWrapMode.Clamp;
            fadeTex.SetPixel(0, 0, new Color(0.08f, 0.09f, 0.13f, 0f));
            fadeTex.SetPixel(1, 0, new Color(0.08f, 0.09f, 0.13f, 0.92f));
            fadeTex.Apply();
            fadeRaw.texture = fadeTex;

            // ---- Text content (starts after fade zone) ----
            var textZone = new GameObject("TextZone", typeof(RectTransform));
            textZone.transform.SetParent(card.transform, false);
            var textZoneRt = textZone.GetComponent<RectTransform>();
            textZoneRt.anchorMin = new Vector2(0, 0);
            textZoneRt.anchorMax = new Vector2(1, 1);
            textZoneRt.offsetMin = new Vector2(170, 0);
            textZoneRt.offsetMax = new Vector2(-150, 0);

            var textVlg = textZone.AddComponent<VerticalLayoutGroup>();
            textVlg.childForceExpandWidth = true;
            textVlg.childForceExpandHeight = false;
            textVlg.childAlignment = TextAnchor.MiddleLeft;
            textVlg.padding = new RectOffset(0, 0, 18, 18);
            textVlg.spacing = 4;

            var nameGo = new GameObject("Name", typeof(RectTransform));
            nameGo.transform.SetParent(textZone.transform, false);
            var nameTmp = nameGo.AddComponent<TextMeshProUGUI>();
            nameTmp.text = data.displayName;
            nameTmp.fontSize = 16;
            nameTmp.fontStyle = FontStyles.Bold;
            nameTmp.color = unlocked ? Color.white : new Color(0.55f, 0.55f, 0.60f);
            nameTmp.raycastTarget = false;
            nameGo.AddComponent<LayoutElement>().preferredHeight = 24;

            var subGo = new GameObject("Sub", typeof(RectTransform));
            subGo.transform.SetParent(textZone.transform, false);
            var subTmp = subGo.AddComponent<TextMeshProUGUI>();
            string ownedBadge = ownedCount > 0 ? $"  <color=#66ccff>{ownedCount} possédé{(ownedCount > 1 ? "s" : "")}</color>" : "";
            subTmp.text = unlocked
                ? $"<color=#8899bb>{data.category}</color>  ·  {data.capacity}t  ·  {data.speedKmh} km/h  ·  <color=#f0c060>${data.purchasePrice:N0}</color>{ownedBadge}"
                : $"<color=#cc7733>Niveau entreprise {data.minCompanyLevelRequired} requis</color>";
            subTmp.fontSize = 11;
            subTmp.color = new Color(0.65f, 0.65f, 0.72f);
            subTmp.raycastTarget = false;
            subGo.AddComponent<LayoutElement>().preferredHeight = 16;

            // ---- "Voir plus" button (right side) ----
            var btnZone = new GameObject("BtnZone", typeof(RectTransform));
            btnZone.transform.SetParent(card.transform, false);
            var btnZoneRt = btnZone.GetComponent<RectTransform>();
            btnZoneRt.anchorMin = new Vector2(1, 0.5f);
            btnZoneRt.anchorMax = new Vector2(1, 0.5f);
            btnZoneRt.pivot = new Vector2(1f, 0.5f);
            btnZoneRt.anchoredPosition = new Vector2(-16, 0);
            btnZoneRt.sizeDelta = new Vector2(110, 38);

            var btnBg = btnZone.AddComponent<Image>();
            btnBg.color = Color.clear;
            btnBg.raycastTarget = false;

            var btnVlg = btnZone.AddComponent<VerticalLayoutGroup>();
            btnVlg.childAlignment = TextAnchor.MiddleCenter;
            btnVlg.childForceExpandWidth = true;
            btnVlg.childForceExpandHeight = true;

            // Arrow + label
            var btnLblGo = new GameObject("BtnLabel", typeof(RectTransform));
            btnLblGo.transform.SetParent(btnZone.transform, false);
            var btnLbl = btnLblGo.AddComponent<TextMeshProUGUI>();
            btnLbl.text = "Voir plus  ›";
            btnLbl.fontSize = 13;
            btnLbl.fontStyle = FontStyles.Bold;
            btnLbl.color = unlocked ? new Color(0.45f, 0.70f, 1f) : new Color(0.45f, 0.45f, 0.50f);
            btnLbl.alignment = TextAlignmentOptions.Center;
            btnLbl.raycastTarget = false;

            // Bottom accent line
            var accentGo = new GameObject("Accent", typeof(RectTransform));
            accentGo.transform.SetParent(btnZone.transform, false);
            var accentImg = accentGo.AddComponent<Image>();
            accentImg.color = unlocked ? new Color(0.30f, 0.55f, 1f, 0.7f) : new Color(0.35f, 0.35f, 0.40f, 0.5f);
            accentImg.raycastTarget = false;
            accentGo.AddComponent<LayoutElement>().preferredHeight = 2;
        }


        // ---- Detail open/close ----

        private void OpenDetail(VehicleData data)
        {
            _selectedVehicle = data;

            var wallet = ServiceLocator.Get<WalletSystem>();
            var depot = ServiceLocator.Get<DepotSystem>();
            var xp = ServiceLocator.Get<XpSystem>();

            bool unlocked = xp == null || xp.IsVehicleUnlocked(data.minCompanyLevelRequired);
            bool depotFull = depot != null && !depot.HasRoomForOneMore();
            bool canAffordDollar = wallet != null && wallet.CanAfford(CurrencyType.Dollar, data.purchasePrice);
            int ingotCost = Mathf.Max(1, data.purchasePrice / 1000);
            bool canAffordIngot = wallet != null && wallet.CanAfford(CurrencyType.GoldIngot, ingotCost);

            _detailName.text = data.displayName;
            _detailCategory.text = $"{data.category}  —  Niveau min: {data.minCompanyLevelRequired}";

            _detailStats.text =
                $"<b>Capacité</b>         {data.capacity} tonnes\n" +
                $"<b>Vitesse max</b>      {data.speedKmh} km/h\n" +
                $"<b>Kilométrage max</b>  {data.maxKilometers:N0} km\n" +
                $"<b>Réservoir</b>        {data.fuelTankCapacityLiters:0} L\n" +
                $"<b>Consommation</b>     {data.fuelConsumptionLPer100Km:0.0} L/100 km\n" +
                $"<b>Autonomie</b>        {data.MaxRangeKm():0} km\n" +
                $"<b>Entretien</b>        ${data.maintenanceCost:N0} / contrat";

            // Status message
            if (!unlocked)
                _detailStatus.text = $"🔒 Requiert niveau entreprise {data.minCompanyLevelRequired}";
            else if (depotFull)
                _detailStatus.text = "⚠ Dépôt plein — améliorez le hangar";
            else
                _detailStatus.text = "";

            bool canBuy = unlocked && !depotFull;

            _buyDollarLabel.text = $"Acheter\n${data.purchasePrice:N0}";
            _buyDollarBtn.interactable = canBuy && canAffordDollar;
            var dollarImg = _buyDollarBtn.GetComponent<Image>();
            dollarImg.color = (canBuy && canAffordDollar)
                ? new Color(0.18f, 0.50f, 0.22f)
                : new Color(0.28f, 0.28f, 0.32f);

            _buyIngotLabel.text = $"Acheter\n🔶 {ingotCost} lingots";
            _buyIngotBtn.interactable = canBuy && canAffordIngot;
            var ingotImg = _buyIngotBtn.GetComponent<Image>();
            ingotImg.color = (canBuy && canAffordIngot)
                ? new Color(0.65f, 0.48f, 0.10f)
                : new Color(0.28f, 0.28f, 0.32f);

            _listPanel.SetActive(false);
            _detailPanel.SetActive(true);
        }

        private void CloseDetail()
        {
            _selectedVehicle = null;
            _detailPanel.SetActive(false);
            _listPanel.SetActive(true);
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
            if (!depot.HasRoomForOneMore()) { _detailStatus.text = "⚠ Dépôt plein"; return; }
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
        }

        // ---- Helpers ----

        private static Button MakeButton(Transform parent, string label, Color color, float width, float height)
        {
            var go = new GameObject("Btn_" + label, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var img = go.AddComponent<Image>();
            img.color = color;

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
            go.AddComponent<Image>().color = new Color(0.3f, 0.3f, 0.35f, 0.6f);
            go.AddComponent<LayoutElement>().preferredHeight = 1;
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
