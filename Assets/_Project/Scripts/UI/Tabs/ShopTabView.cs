using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using TransportManager.Core;
using TransportManager.Events;
using TransportManager.Systems.Economy;
using TransportManager.Systems.Shop;
using UnityEngine;
using UnityEngine.UI;

namespace TransportManager.UI.Tabs
{
    /// <summary>
    /// Boutique (style Awwwards 2026) : layout asymétrique, typographie expressive,
    /// cartes avec emplacements d'image (drop Sprite via Resources/UI/Shop/...).
    /// </summary>
    public class ShopTabView : MonoBehaviour
    {
        // ╔════════════════════════════════════════════════════════════════════╗
        // ║ Palette                                                            ║
        // ╚════════════════════════════════════════════════════════════════════╝

        // Palette partagée (Header / Navbar / ContractsPanel / VehiclesTab)
        private static readonly Color32 BgPanel    = new Color32(0x2C, 0x30, 0x38, 240);
        private static readonly Color32 BgCard     = new Color32(0x34, 0x38, 0x42, 255);
        private static readonly Color32 BgCardSoft = new Color32(0x1A, 0x1D, 0x24, 230);
        private static readonly Color32 BgHero     = new Color32(0x3A, 0x33, 0x22, 255);   // carte mise en avant, teinte dorée
        private static readonly Color32 BgImgSlot  = new Color32(0x1A, 0x1D, 0x24, 255);
        private static readonly Color32 BgLockedOv = new Color32(0x1A, 0x1D, 0x24, 235);
        private static readonly Color32 Divider    = new Color32(0x3A, 0x3F, 0x4A, 150);

        private static readonly Color32 TextPri    = new Color32(0xEC, 0xEE, 0xF5, 255);
        private static readonly Color32 TextSec    = new Color32(0x7A, 0x8F, 0xA6, 255);
        private static readonly Color32 TextMuted  = new Color32(0x5A, 0x65, 0x77, 255);
        private static readonly Color32 TextDark   = new Color32(0x12, 0x14, 0x1A, 255);

        private static readonly Color32 ColGreen   = new Color32(0x3D, 0xC9, 0x6E, 255);
        private static readonly Color32 ColGold    = new Color32(0xF2, 0xD9, 0x66, 255);
        private static readonly Color32 ColBlue    = new Color32(0x4D, 0x9C, 0xFF, 255);
        private static readonly Color32 ColPurple  = new Color32(0xA8, 0x7E, 0xFF, 255);
        private static readonly Color32 ColOrange  = new Color32(0xFA, 0xA0, 0x24, 255);
        private static readonly Color32 ColRed     = new Color32(0xFF, 0x5E, 0x5E, 255);
        private static readonly Color32 ColDisabled = new Color32(0x3A, 0x3F, 0x4A, 255);

        // ╔════════════════════════════════════════════════════════════════════╗
        // ║ Configuration Inspector                                            ║
        // ╚════════════════════════════════════════════════════════════════════╝

        [Header("Safe area (offsets pour éviter header + navbar)")]
        [SerializeField] private int safeTop    = 140;
        [SerializeField] private int safeLeft   = 130;
        [SerializeField] private int safeRight  = 20;
        [SerializeField] private int safeBottom = 20;

        // ── Pour ajouter une offre : cliquer sur + ci-dessous, remplir title / quantity / priceText / bonus / resourceImage ──
        [Header("Boissons Énergisantes — ajoutez vos offres ici sans toucher au code")]
        [SerializeField] private EnergyDrinkOfferData[] _energyDrinkOffers = new EnergyDrinkOfferData[]
        {
            new EnergyDrinkOfferData { title = "SOLO",    quantity = 1, priceText = "0,99 €", bonus = ""           },
            new EnergyDrinkOfferData { title = "DUO",     quantity = 2, priceText = "1,79 €", bonus = ""           },
            new EnergyDrinkOfferData { title = "PACK X3", quantity = 3, priceText = "2,49 €", bonus = "+1 offert"  },
            new EnergyDrinkOfferData { title = "STOCK",   quantity = 5, priceText = "3,99 €", bonus = "+2 offerts" },
        };

        // ╔════════════════════════════════════════════════════════════════════╗
        // ║ État interne                                                       ║
        // ╚════════════════════════════════════════════════════════════════════╝

        private Sprite _sprRound8;
        private Sprite _sprRound16;
        private Sprite _sprRound24;

        private TMP_Text _adQuotaLabel;
        private TMP_Text _adButtonLabel;
        private Button   _adButton;
        private Image    _adButtonBg;

        private TMP_Text _starterPackCountdownLabel;
        private TMP_Text _beginnerPackCountdownLabel;

        private readonly List<Action> _refreshActions = new List<Action>();
        private Coroutine _refreshCoroutine;

        // ╔════════════════════════════════════════════════════════════════════╗
        // ║ Lifecycle                                                          ║
        // ╚════════════════════════════════════════════════════════════════════╝

        private void Awake()
        {
            _sprRound8  = MakeRoundedSprite(8);
            _sprRound16 = MakeRoundedSprite(16);
            _sprRound24 = MakeRoundedSprite(24);
            Build();
        }

        private void OnEnable()
        {
            GameEvents.OnDollarsChanged    += OnCurrencyChanged;
            GameEvents.OnGoldIngotsChanged += OnCurrencyChanged;
            _refreshCoroutine = StartCoroutine(TickLoop());
            RefreshAll();
        }

        private void OnDisable()
        {
            GameEvents.OnDollarsChanged    -= OnCurrencyChanged;
            GameEvents.OnGoldIngotsChanged -= OnCurrencyChanged;
            if (_refreshCoroutine != null) StopCoroutine(_refreshCoroutine);
        }

        private void OnCurrencyChanged(int _) => RefreshAll();

        private IEnumerator TickLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(1f);
                RefreshAdButton();
                RefreshPackCountdowns();
            }
        }

        private void RefreshAll()
        {
            RefreshAdButton();
            RefreshPackCountdowns();
            foreach (var a in _refreshActions) a?.Invoke();
        }

        // ╔════════════════════════════════════════════════════════════════════╗
        // ║ Construction racine                                                ║
        // ╚════════════════════════════════════════════════════════════════════╝

        private void Build()
        {
            // Wipe
            var children = new List<GameObject>();
            foreach (Transform c in transform) children.Add(c.gameObject);
            foreach (var c in children)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying) DestroyImmediate(c); else Destroy(c);
#else
                Destroy(c);
#endif
            }

            // Inset (évite header + sidebar)
            var rt = GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(safeLeft,   safeBottom);
            rt.offsetMax = new Vector2(-safeRight, -safeTop);

            // Racine transparente : le visuel est porté par le fond d'écran + le panneau d'offres.
            var bg = GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            bg.sprite        = null;
            bg.color         = new Color(0f, 0f, 0f, 0f);
            bg.raycastTarget = true;

            // 1. Image en fond d'écran (plein écran, derrière le HUD flottant)
            BuildPageBackground("UI/background/shop");

            // 2. Panneau des offres (inséré, arrondi) — la « popup » par-dessus le fond
            var panelGo  = MakeGO("OffersPanel", transform);
            FillParent(panelGo.GetComponent<RectTransform>());
            var panelImg = panelGo.AddComponent<Image>();
            panelImg.sprite        = _sprRound24;
            panelImg.type          = Image.Type.Sliced;
            panelImg.color         = BgPanel;
            panelImg.raycastTarget = true;

            // 3. Contenu défilant (par-dessus le panneau)
            BuildScroll();
        }

        // Image en fond d'écran (RawImage : marche quel que soit le type d'import de la texture).
        // On annule les marges d'insertion (safe*) pour couvrir tout le conteneur d'onglets,
        // lui-même rendu DERRIÈRE la navbar et le header.
        private void BuildPageBackground(string texturePath)
        {
            var tex = Resources.Load<Texture2D>(texturePath);
            if (tex == null) return;
            var go = MakeGO("PageBackground", transform);
            go.transform.SetAsFirstSibling();
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(-safeLeft,  -safeBottom);
            rt.offsetMax = new Vector2( safeRight,  safeTop);
            var raw = go.AddComponent<RawImage>();
            raw.texture       = tex;
            raw.raycastTarget = false;
        }

        private void BuildScroll()
        {
            var scrollGo = MakeGO("Scroll", transform);
            FillParent(scrollGo.GetComponent<RectTransform>());

            var scrollRect = scrollGo.AddComponent<ScrollRect>();
            scrollRect.horizontal        = false;
            scrollRect.vertical          = true;
            scrollRect.scrollSensitivity = 60;
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
            vlg.padding                = new RectOffset(22, 22, 22, 28);
            vlg.spacing                = 22;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth      = true;
            vlg.childControlHeight     = true;
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Sections
            BuildHeroAdSection(content.transform);
            BuildTemporaryOffersSection(content.transform);
            BuildEnergyDrinksSection(content.transform);
            BuildGoldPacksSection(content.transform);
            BuildConvertSection(content.transform);
        }

        // ╔════════════════════════════════════════════════════════════════════╗
        // ║ Section 1 — HERO : bonus quotidien                                 ║
        // ╚════════════════════════════════════════════════════════════════════╝

        private void BuildHeroAdSection(Transform parent)
        {
            var card = MakeRoundedCard(parent, BgHero, 100);
            var vlg = card.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(28, 28, 22, 24);
            vlg.spacing = 8;

            // Ligne 1 : tiny label + quota à droite
            var topRow = MakeRow(card.transform, height: 18, spacing: 8);
            var label  = AddTMP("Lbl", topRow.transform, "BONUS QUOTIDIEN", 11, FontStyles.Bold, ColGold);
            label.characterSpacing = 6f;
            label.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

            _adQuotaLabel = AddTMP("Quota", topRow.transform, "0/4", 12, FontStyles.Bold, TextSec);
            _adQuotaLabel.alignment = TextAlignmentOptions.MidlineRight;
            _adQuotaLabel.gameObject.AddComponent<LayoutElement>().preferredWidth = 80;

            // Valeur héro : "+2 ◆"
            var heroRow = MakeRow(card.transform, height: 56, spacing: 12);
            heroRow.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleLeft;

            var plus = AddTMP("Plus", heroRow.transform, "+", 48, FontStyles.Bold, ColGold);
            plus.gameObject.AddComponent<LayoutElement>().preferredWidth = 30;

            var amount = AddTMP("Amount", heroRow.transform, ShopSystem.AdGoldReward.ToString(), 56, FontStyles.Bold, TextPri);
            amount.gameObject.AddComponent<LayoutElement>().preferredWidth = 60;

            BuildIconImage(heroRow.transform, "UI/Icons/Infos/gold", ColGold, 44);

            var subtext = AddTMP("Sub", heroRow.transform, "lingots offerts", 14, FontStyles.Normal, TextSec);
            subtext.alignment = TextAlignmentOptions.MidlineLeft;
            subtext.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

            // Subtitle
            var hint = AddTMP("Hint", card.transform,
                $"Cooldown {ShopSystem.AdCooldownMinutes / 60}h  ·  max {ShopSystem.MaxAdsPerDay} pubs/jour",
                12, FontStyles.Normal, TextMuted);
            hint.gameObject.AddComponent<LayoutElement>().preferredHeight = 16;

            // Bouton CTA (large, doré)
            var spacer = MakeGO("Sp", card.transform);
            spacer.AddComponent<LayoutElement>().preferredHeight = 6;

            var btn = BuildCtaButton(card.transform, "", ColGold, TextDark, 52, OnWatchAdClicked);
            _adButton  = btn.button;
            _adButtonBg = btn.bg;
            btn.label.gameObject.SetActive(false); // remplacé par contentRow

            // Contenu centré : icône + texte côte à côte
            var contentRow = MakeGO("ContentRow", btn.button.transform);
            FillParent(contentRow.GetComponent<RectTransform>());
            var cHlg = contentRow.AddComponent<HorizontalLayoutGroup>();
            cHlg.childAlignment        = TextAnchor.MiddleCenter;
            cHlg.spacing               = 8;
            cHlg.childForceExpandWidth  = false;
            cHlg.childForceExpandHeight = false;
            cHlg.childControlWidth      = true;
            cHlg.childControlHeight     = true;

            BuildIconImage(contentRow.transform, "UI/Icons/icons/add", TextDark, 22);

            var adLbl = AddTMP("AdLbl", contentRow.transform, "REGARDER LA PUB", 14, FontStyles.Bold, TextDark);
            adLbl.textWrappingMode = TextWrappingModes.NoWrap;
            adLbl.characterSpacing = 3f;
            _adButtonLabel = adLbl;
        }

        private void RefreshAdButton()
        {
            if (_adButton == null) return;
            var shop = ServiceLocator.Get<ShopSystem>();
            if (shop == null) return;

            if (_adQuotaLabel != null)
                _adQuotaLabel.text = $"{shop.AdsWatchedToday}/{ShopSystem.MaxAdsPerDay}";

            if (shop.CanWatchAd)
            {
                _adButton.interactable = true;
                _adButtonBg.color      = ColGold;
                _adButtonLabel.text    = "REGARDER LA PUB";
                _adButtonLabel.color   = TextDark;
            }
            else
            {
                _adButton.interactable = false;
                _adButtonBg.color      = ColDisabled;
                _adButtonLabel.color   = TextSec;
                _adButtonLabel.text    = shop.AdsRemainingToday <= 0
                    ? "QUOTA QUOTIDIEN ATTEINT"
                    : $"DISPO DANS  {FormatCooldown(shop.AdCooldownRemaining)}";
            }
        }

        private void OnWatchAdClicked()
        {
            var shop = ServiceLocator.Get<ShopSystem>();
            if (shop != null && shop.TryWatchAdForGold())
            {
                Debug.Log($"[Shop] Ad reward (+{ShopSystem.AdGoldReward} gold).");
                RefreshAdButton();
            }
        }

        // ╔════════════════════════════════════════════════════════════════════╗
        // ║ Section 2 — Offres temporaires (limitées dans le temps)            ║
        // ╚════════════════════════════════════════════════════════════════════╝

        private void BuildTemporaryOffersSection(Transform parent)
        {
            // Header
            var head = MakeRow(parent, height: 24, spacing: 10);
            head.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(2, 2, 0, 0);

            BuildAccentDot(head.transform, ColRed, 8);
            var lbl = AddTMP("Lbl", head.transform, "OFFRES TEMPORAIRES", 12, FontStyles.Bold, TextPri);
            lbl.characterSpacing = 5f;
            lbl.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

            // Rangée de 2 offres IAP
            var row = BuildPairRow(parent);

            _starterPackCountdownLabel = BuildOfferCard(row.transform,
                offerId:       "starter_pack",
                accent:        ColPurple,
                resourceImage: "UI/offers/starter_pack",
                title:         "PACK DE DÉMARRAGE",
                bigValue:      "20K $",
                items:         new[] { "20 000 $", "100 lingots", "Camion exclusif", "-25% maintenance 7j" },
                badge:         "2 SEM.",
                badgeColor:    ColRed,
                realPriceText: "4,99 €",
                dollarsReward: 20000,
                goldReward:    100,
                isAvailable:   s => s.IsStarterPackAvailable,
                getTimeLeft:   s => s.StarterPackTimeLeft);

            _beginnerPackCountdownLabel = BuildOfferCard(row.transform,
                offerId:       "beginner_pack",
                accent:        ColPurple,
                resourceImage: "UI/offers/beginner_pack",
                title:         "PACK DE DÉBUTANT",
                bigValue:      "50K $",
                items:         new[] { "50 000 $", "200 lingots", "Camion exclusif", "Conducteur exclusif", "+1 emplacement", "-50% maintenance" },
                badge:         "3 SEM.",
                badgeColor:    ColOrange,
                realPriceText: "9,99 €",
                dollarsReward: 50000,
                goldReward:    200,
                isAvailable:   s => s.IsBeginnerPackAvailable,
                getTimeLeft:   s => s.BeginnerPackTimeLeft);
        }

        private void RefreshPackCountdowns()
        {
            RefreshPackCountdown(_starterPackCountdownLabel,
                s => s.IsStarterPackAvailable, s => s.StarterPackTimeLeft);
            RefreshPackCountdown(_beginnerPackCountdownLabel,
                s => s.IsBeginnerPackAvailable, s => s.BeginnerPackTimeLeft);
        }

        private void RefreshPackCountdown(TMP_Text label,
            Func<ShopSystem, bool> isAvailable, Func<ShopSystem, TimeSpan> getTimeLeft)
        {
            if (label == null) return;
            var shop = ServiceLocator.Get<ShopSystem>();
            if (shop == null) return;

            if (isAvailable(shop))
            {
                var left = getTimeLeft(shop);
                label.color = left.TotalDays > 3 ? ColOrange : ColRed;
                label.text  = $"EXPIRE {FormatStarterCountdown(left)}";
            }
            else
            {
                label.color = TextMuted;
                label.text  = "EXPIRÉE";
            }
        }

        private TMP_Text BuildOfferCard(Transform parent, string offerId, Color32 accent,
                                       string resourceImage, string title, string bigValue,
                                       string[] items, string badge, Color32 badgeColor,
                                       string realPriceText, int dollarsReward, int goldReward,
                                       Func<ShopSystem, bool> isAvailable,
                                       Func<ShopSystem, TimeSpan> getTimeLeft)
        {
            // fitContentHeight:false → la rangée impose une hauteur égale aux deux cartes.
            var card = MakeRoundedCard(parent, BgCard, 0, fitContentHeight: false);
            var vlg = card.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(0, 0, 0, 0);
            vlg.spacing = 0;

            // ── Bloc visuel (image + badge superposé) ──
            var visualGo = MakeGO("Visual", card.transform);
            visualGo.AddComponent<LayoutElement>().preferredHeight = 110;

            var visualImg = visualGo.AddComponent<Image>();
            visualImg.sprite = _sprRound16;
            visualImg.type   = Image.Type.Sliced;
            visualImg.color  = BgImgSlot;
            var mask = visualGo.AddComponent<Mask>();
            mask.showMaskGraphic = true;

            // Image (sprite drop later via Resources)
            var imgGo = MakeGO("PackImage", visualGo.transform);
            var imgRt = imgGo.GetComponent<RectTransform>();
            FillParent(imgRt);
            var packImg = imgGo.AddComponent<Image>();
            var loadedSprite = LoadSprite(resourceImage);
            packImg.sprite         = loadedSprite;
            packImg.preserveAspect = false;
            packImg.color          = loadedSprite != null
                ? Color.white
                : new Color(accent.r / 255f, accent.g / 255f, accent.b / 255f, 0.18f);
            packImg.raycastTarget  = false;

            // Badge en haut-droite
            BuildCornerBadge(visualGo.transform, badge, badgeColor);

            // Accent gauche
            var stripe = MakeGO("Stripe", visualGo.transform);
            var stripeImg = stripe.AddComponent<Image>();
            stripeImg.color = accent;
            var stripeRt = stripe.GetComponent<RectTransform>();
            stripeRt.anchorMin = new Vector2(0, 0);
            stripeRt.anchorMax = new Vector2(0, 1);
            stripeRt.pivot     = new Vector2(0, 0.5f);
            stripeRt.sizeDelta = new Vector2(4, 0);
            stripeRt.anchoredPosition = Vector2.zero;

            // ── Contenu textuel ── (flexibleHeight pour remplir la carte égalisée)
            var infoGo = MakeGO("Info", card.transform);
            infoGo.AddComponent<LayoutElement>().flexibleHeight = 1;
            var infoVlg = infoGo.AddComponent<VerticalLayoutGroup>();
            infoVlg.padding = new RectOffset(18, 18, 14, 16);
            infoVlg.spacing = 4;
            infoVlg.childForceExpandWidth = true;
            infoVlg.childControlWidth     = true;
            infoVlg.childControlHeight    = true;
            infoVlg.childForceExpandHeight = false;

            var titleLbl = AddTMP("Title", infoGo.transform, title, 11, FontStyles.Bold, TextSec);
            titleLbl.characterSpacing = 4f;
            titleLbl.gameObject.AddComponent<LayoutElement>().preferredHeight = 14;

            var valueLbl = AddTMP("Big", infoGo.transform, bigValue, 26, FontStyles.Bold, accent);
            valueLbl.gameObject.AddComponent<LayoutElement>().preferredHeight = 32;

            foreach (var item in items)
            {
                var itemLbl = AddTMP("Item", infoGo.transform, "· " + item, 11, FontStyles.Normal, TextSec);
                itemLbl.gameObject.AddComponent<LayoutElement>().preferredHeight = 14;
            }

            // Compte à rebours par pack
            var countdownLbl = AddTMP("Countdown", infoGo.transform, "", 10, FontStyles.Normal, ColOrange);
            countdownLbl.gameObject.AddComponent<LayoutElement>().preferredHeight = 14;

            // Spacer flexible : pousse le CTA en bas pour aligner les boutons des 2 cartes.
            var spacerGo = MakeGO("Sp", infoGo.transform);
            var spacerLe = spacerGo.AddComponent<LayoutElement>();
            spacerLe.preferredHeight = 6;
            spacerLe.flexibleHeight  = 1;

            // CTA — achat avec argent réel (IAP)
            var cta = BuildCtaButton(infoGo.transform, "ACHETER  ·  " + realPriceText, accent, Color.white, 44, null);
            cta.button.onClick.AddListener(() =>
            {
                var shop = ServiceLocator.Get<ShopSystem>();
                // TODO: déclencher la validation IAP store (Google Play / App Store) avant d'appeler cette méthode.
                // En attendant l'intégration IAP complète, on crédite directement à des fins de test.
                if (shop != null && shop.TryClaimSpecialOfferIAP(offerId, dollarsReward, goldReward))
                    RefreshAll();
            });

            // ── Overlay verrou (s'affiche si expiré OU déjà claimé) ──
            var overlay = MakeGO("LockOverlay", card.transform);
            var overlayRt = overlay.GetComponent<RectTransform>();
            FillParent(overlayRt);
            overlay.transform.SetAsLastSibling();

            var overlayBg = overlay.AddComponent<Image>();
            overlayBg.sprite = _sprRound16;
            overlayBg.type   = Image.Type.Sliced;
            overlayBg.color  = BgLockedOv;
            overlayBg.raycastTarget = true;

            var overlayVlg = overlay.AddComponent<VerticalLayoutGroup>();
            overlayVlg.padding = new RectOffset(20, 20, 20, 20);
            overlayVlg.spacing = 6;
            overlayVlg.childAlignment = TextAnchor.MiddleCenter;
            overlayVlg.childForceExpandHeight = false;

            var lockTitle = AddTMP("LockTitle", overlay.transform, "—", 14, FontStyles.Bold, TextPri);
            lockTitle.alignment = TextAlignmentOptions.Center;
            lockTitle.gameObject.AddComponent<LayoutElement>().preferredHeight = 20;

            var lockSub = AddTMP("LockSub", overlay.transform, "", 11, FontStyles.Normal, TextSec);
            lockSub.alignment = TextAlignmentOptions.Center;
            lockSub.gameObject.AddComponent<LayoutElement>().preferredHeight = 16;

            // Refresh callback
            Action refresh = () =>
            {
                var shop = ServiceLocator.Get<ShopSystem>();
                if (shop == null) return;

                bool claimed = shop.IsSpecialOfferClaimed(offerId);
                bool expired = !isAvailable(shop);

                if (claimed)
                {
                    overlay.SetActive(true);
                    lockTitle.text  = "✓  OBTENUE";
                    lockTitle.color = ColGreen;
                    lockSub.text    = "Cette offre a été utilisée";
                }
                else if (expired)
                {
                    overlay.SetActive(true);
                    lockTitle.text  = "OFFRE EXPIRÉE";
                    lockTitle.color = TextSec;
                    lockSub.text    = "Offre à durée limitée";
                }
                else
                {
                    overlay.SetActive(false);
                    cta.button.interactable = true;
                    cta.bg.color    = accent;
                    cta.label.color = Color.white;
                    cta.label.text  = "ACHETER  ·  " + realPriceText;
                }

                // Countdown inline dans la carte
                if (isAvailable(shop))
                {
                    var left = getTimeLeft(shop);
                    countdownLbl.color = left.TotalDays > 3 ? ColOrange : ColRed;
                    countdownLbl.text  = $"EXPIRE {FormatStarterCountdown(left)}";
                }
                else
                {
                    countdownLbl.color = TextMuted;
                    countdownLbl.text  = "EXPIRÉE";
                }
            };
            _refreshActions.Add(refresh);
            refresh();

            return countdownLbl;
        }

        // ╔════════════════════════════════════════════════════════════════════╗
        // ║ Section 3 — Boissons Énergisantes                                  ║
        // ╚════════════════════════════════════════════════════════════════════╝

        private void BuildEnergyDrinksSection(Transform parent)
        {
            if (_energyDrinkOffers == null || _energyDrinkOffers.Length == 0) return;

            var head = MakeRow(parent, height: 24, spacing: 10);
            head.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(2, 2, 0, 0);
            BuildAccentDot(head.transform, ColBlue, 8);
            var lbl = AddTMP("Lbl", head.transform, "BOISSONS ÉNERGISANTES", 12, FontStyles.Bold, TextPri);
            lbl.characterSpacing = 5f;
            lbl.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

            var sub = AddTMP("Sub", parent, "Protège vos conducteurs contre les accidents", 11, FontStyles.Normal, TextSec);
            sub.gameObject.AddComponent<LayoutElement>().preferredHeight = 16;

            var row = BuildPairRow(parent);
            foreach (var offer in _energyDrinkOffers)
                BuildEnergyDrinkCard(row.transform, offer);
        }

        private void BuildEnergyDrinkCard(Transform parent, EnergyDrinkOfferData offer)
        {
            var card = MakeRoundedCard(parent, BgCard, 0);
            var vlg = card.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(0, 0, 0, 0);
            vlg.spacing = 0;

            // Image
            var imgWrap = MakeGO("ImageWrap", card.transform);
            imgWrap.AddComponent<LayoutElement>().preferredHeight = 68;
            var imgBg = imgWrap.AddComponent<Image>();
            imgBg.sprite = _sprRound16;
            imgBg.type   = Image.Type.Sliced;
            imgBg.color  = BgImgSlot;

            var imgGo  = MakeGO("DrinkImg", imgWrap.transform);
            var imgRt  = imgGo.GetComponent<RectTransform>();
            FillParent(imgRt);
            imgRt.offsetMin = new Vector2(8, 8);
            imgRt.offsetMax = new Vector2(-8, -8);
            var drinkImg = imgGo.AddComponent<Image>();
            drinkImg.sprite = !string.IsNullOrEmpty(offer.resourceImage)
                ? Resources.Load<Sprite>(offer.resourceImage)
                : null;
            drinkImg.preserveAspect = true;
            drinkImg.raycastTarget  = false;
            if (drinkImg.sprite == null)
            {
                // L'icône "energy" n'existe pas ; on retombe sur l'image de canette.
                drinkImg.sprite = Resources.Load<Sprite>("UI/energized_drink/1_can");
                drinkImg.color  = Color.white;
            }
            drinkImg.enabled = drinkImg.sprite != null;

            // Contenu
            var infoGo  = MakeGO("Info", card.transform);
            var infoVlg = infoGo.AddComponent<VerticalLayoutGroup>();
            infoVlg.padding               = new RectOffset(8, 8, 8, 10);
            infoVlg.spacing               = 1;
            infoVlg.childAlignment        = TextAnchor.UpperCenter;
            infoVlg.childForceExpandWidth = true;
            infoVlg.childControlWidth     = true;

            var titleLbl = AddTMP("Title", infoGo.transform, offer.title, 10, FontStyles.Bold, TextSec);
            titleLbl.alignment        = TextAlignmentOptions.Center;
            titleLbl.characterSpacing = 3f;
            titleLbl.gameObject.AddComponent<LayoutElement>().preferredHeight = 12;

            var qtyRow = MakeRow(infoGo.transform, height: 36, spacing: 3);
            qtyRow.GetComponent<HorizontalLayoutGroup>().childAlignment        = TextAnchor.MiddleCenter;
            qtyRow.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;

            var qtyLbl = AddTMP("Qty", qtyRow.transform, offer.quantity.ToString(), 28, FontStyles.Bold, ColBlue);
            qtyLbl.alignment = TextAlignmentOptions.MidlineRight;
            qtyLbl.gameObject.AddComponent<LayoutElement>().preferredWidth = 28;

            var unitLbl = AddTMP("Unit", qtyRow.transform,
                offer.quantity <= 1 ? "bois." : "bois.", 8, FontStyles.Normal, TextMuted);
            unitLbl.alignment = TextAlignmentOptions.MidlineLeft;

            var descLbl = AddTMP("Desc", infoGo.transform, "Évite accidents", 9, FontStyles.Normal, TextMuted);
            descLbl.alignment = TextAlignmentOptions.Center;
            descLbl.gameObject.AddComponent<LayoutElement>().preferredHeight = 11;

            if (!string.IsNullOrEmpty(offer.bonus))
            {
                var bonusLbl = AddTMP("Bonus", infoGo.transform, offer.bonus, 10, FontStyles.Bold, ColGreen);
                bonusLbl.alignment = TextAlignmentOptions.Center;
                bonusLbl.gameObject.AddComponent<LayoutElement>().preferredHeight = 13;
            }
            else
            {
                MakeGO("Sp", infoGo.transform).AddComponent<LayoutElement>().preferredHeight = 13;
            }

            MakeGO("Sp2", infoGo.transform).AddComponent<LayoutElement>().preferredHeight = 4;

            var cta = BuildCtaButton(infoGo.transform, offer.priceText, ColBlue, Color.white, 36, null);
            cta.button.onClick.AddListener(() =>
            {
                var shop = ServiceLocator.Get<ShopSystem>();
                if (shop != null && shop.GrantEnergyDrinks(offer.quantity))
                    Debug.Log($"[Shop] Boisson achetée : +{offer.quantity} ({offer.priceText})");
            });
        }

        // ╔════════════════════════════════════════════════════════════════════╗
        // ║ Section 4 — Packs de lingots                                       ║
        // ╚════════════════════════════════════════════════════════════════════╝

        private void BuildGoldPacksSection(Transform parent)
        {
            var head = MakeRow(parent, height: 24, spacing: 10);
            head.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(2, 2, 0, 0);
            BuildAccentDot(head.transform, ColGold, 8);
            var lbl = AddTMP("Lbl", head.transform, "PACKS DE LINGOTS", 12, FontStyles.Bold, TextPri);
            lbl.characterSpacing = 5f;
            lbl.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

            var r1 = BuildPairRow(parent);
            BuildPackCard(r1.transform, "STARTER",      10,   "0,99 €",  null,    "UI/Shop/pack_starter");
            BuildPackCard(r1.transform, "PETIT SAC",    30,   "1,99 €",  null,    "UI/Shop/pack_small");
            BuildPackCard(r1.transform, "COFFRE",       75,   "4,99 €",  "+10%",  "UI/Shop/pack_medium");

            var r2 = BuildPairRow(parent);
            BuildPackCard(r2.transform, "GRAND COFFRE", 150,  "9,99 €",  "+20%",  "UI/Shop/pack_large");
            BuildPackCard(r2.transform, "TRÉSOR",       400,  "24,99 €", "+40%",  "UI/Shop/pack_mega");
            BuildPackCard(r2.transform, "COFFRE-FORT",  1000, "49,99 €", "+60%",  "UI/Shop/pack_vault");
        }

        private void BuildPackCard(Transform parent, string title, int goldAmount, string priceText,
                                   string bonus, string resourceImage)
        {
            var card = MakeRoundedCard(parent, BgCard, 0);
            var vlg = card.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(0, 0, 0, 0);
            vlg.spacing = 0;

            // ── Image (slot pour sprite custom plus tard) ──
            var imgWrap = MakeGO("ImageWrap", card.transform);
            imgWrap.AddComponent<LayoutElement>().preferredHeight = 110;

            var imgBg = imgWrap.AddComponent<Image>();
            imgBg.sprite = _sprRound16;
            imgBg.type   = Image.Type.Sliced;
            imgBg.color  = BgImgSlot;

            var imgGo = MakeGO("PackImage", imgWrap.transform);
            var imgRt = imgGo.GetComponent<RectTransform>();
            FillParent(imgRt);
            imgRt.offsetMin = new Vector2(10, 10);
            imgRt.offsetMax = new Vector2(-10, -10);

            var packImg = imgGo.AddComponent<Image>();
            packImg.sprite         = LoadSprite(resourceImage);
            packImg.preserveAspect = true;
            packImg.raycastTarget  = false;
            if (packImg.sprite == null)
            {
                packImg.sprite = LoadSprite("UI/Icons/Infos/gold");
                packImg.color  = new Color(1f, 0.82f, 0.4f, 0.18f);
            }

            // ── Texte ──
            var infoGo = MakeGO("Info", card.transform);
            var infoVlg = infoGo.AddComponent<VerticalLayoutGroup>();
            infoVlg.padding         = new RectOffset(16, 16, 14, 16);
            infoVlg.spacing         = 2;
            infoVlg.childAlignment  = TextAnchor.UpperCenter;
            infoVlg.childForceExpandWidth = true;

            var titleLbl = AddTMP("Title", infoGo.transform, title, 11, FontStyles.Bold, TextSec);
            titleLbl.alignment        = TextAlignmentOptions.Center;
            titleLbl.characterSpacing = 4f;
            titleLbl.gameObject.AddComponent<LayoutElement>().preferredHeight = 14;

            // Donnée principale : nombre HUGE + ◆ à côté
            var qtyRow = MakeRow(infoGo.transform, height: 50, spacing: 4);
            qtyRow.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            qtyRow.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;

            var qtyLbl = AddTMP("Qty", qtyRow.transform, goldAmount.ToString("N0"),
                                42, FontStyles.Bold, TextPri);
            qtyLbl.enableAutoSizing = true;
            qtyLbl.fontSizeMin      = 22;
            qtyLbl.fontSizeMax      = 42;
            qtyLbl.gameObject.AddComponent<LayoutElement>().preferredWidth = 100;
            qtyLbl.alignment = TextAlignmentOptions.MidlineRight;

            BuildIconImage(qtyRow.transform, "UI/Icons/Infos/gold", ColGold, 28);

            // Bonus (ou spacer)
            if (bonus != null)
            {
                var bonusLbl = AddTMP("Bonus", infoGo.transform, bonus + " bonus", 12, FontStyles.Bold, ColGreen);
                bonusLbl.alignment = TextAlignmentOptions.Center;
                bonusLbl.gameObject.AddComponent<LayoutElement>().preferredHeight = 18;
            }
            else
            {
                MakeGO("Sp", infoGo.transform).AddComponent<LayoutElement>().preferredHeight = 18;
            }

            var spacer = MakeGO("Sp2", infoGo.transform);
            spacer.AddComponent<LayoutElement>().preferredHeight = 8;

            // CTA prix vert pleine largeur
            var cta = BuildCtaButton(infoGo.transform, priceText, ColGreen, Color.white, 44, null);
            cta.button.onClick.AddListener(() => OnGoldPackClicked(title, goldAmount));
        }

        private void OnGoldPackClicked(string packName, int amount)
        {
            Debug.Log($"[Shop] (mock IAP) {packName} → +{amount} gold");
            ServiceLocator.Get<ShopSystem>()?.GrantGoldIngots(amount);
        }

        // ╔════════════════════════════════════════════════════════════════════╗
        // ║ Section 5 — Bureau de change (gold → dollars uniquement)           ║
        // ╚════════════════════════════════════════════════════════════════════╝

        private void BuildConvertSection(Transform parent)
        {
            var head = MakeRow(parent, height: 24, spacing: 10);
            head.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(2, 2, 0, 0);
            BuildAccentDot(head.transform, ColBlue, 8);
            var lbl = AddTMP("Lbl", head.transform, "VENDRE DES LINGOTS", 12, FontStyles.Bold, TextPri);
            lbl.characterSpacing = 5f;
            lbl.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

            var rate = AddTMP("Rate", head.transform,
                $"1 ◆ = {ShopSystem.GoldToDollarsRate:N0} $", 12, FontStyles.Bold, ColBlue);
            rate.alignment = TextAlignmentOptions.MidlineRight;
            rate.gameObject.AddComponent<LayoutElement>().preferredWidth = 160;

            // Carte unique pleine largeur, 3 boutons horizontaux
            var card = MakeRoundedCard(parent, BgCard, 0);
            var vlg = card.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(18, 18, 18, 18);
            vlg.spacing = 12;

            var btnRow = MakeRow(card.transform, height: 80, spacing: 12);
            btnRow.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
            btnRow.GetComponent<HorizontalLayoutGroup>().childControlWidth     = true;

            foreach (int amt in new[] { 1, 5, 25 })
                BuildConvertQuickCard(btnRow.transform, amt);
        }

        private void BuildConvertQuickCard(Transform parent, int amount)
        {
            int dollars = amount * ShopSystem.GoldToDollarsRate;

            var btnGo = MakeGO($"Quick{amount}", parent);
            var img   = btnGo.AddComponent<Image>();
            img.sprite        = _sprRound16;
            img.type          = Image.Type.Sliced;
            img.color         = BgCardSoft;
            img.raycastTarget = true;

            var btn = btnGo.AddComponent<Button>();
            btn.targetGraphic = img;
            var btnColors = btn.colors;
            btnColors.disabledColor = new Color(1, 1, 1, 1);
            btn.colors = btnColors;

            var le = btnGo.AddComponent<LayoutElement>();
            le.flexibleWidth = 1;
            le.minWidth      = 0;

            var vlg = btnGo.AddComponent<VerticalLayoutGroup>();
            vlg.padding         = new RectOffset(10, 10, 12, 12);
            vlg.spacing         = 2;
            vlg.childAlignment  = TextAnchor.MiddleCenter;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;

            // Ligne 1 : nombre + ◆
            var qtyRow = MakeRow(btnGo.transform, height: 32, spacing: 4);
            qtyRow.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            qtyRow.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;

            var qtyLbl = AddTMP("Qty", qtyRow.transform, amount.ToString(), 28, FontStyles.Bold, ColGold);
            qtyLbl.gameObject.AddComponent<LayoutElement>().preferredWidth = 40;
            qtyLbl.alignment = TextAlignmentOptions.MidlineRight;

            BuildIconImage(qtyRow.transform, "UI/Icons/Infos/gold", ColGold, 22);

            // Ligne 2 : valeur en $
            var valueLbl = AddTMP("Val", btnGo.transform, $"+{dollars:N0} $", 13, FontStyles.Bold, ColGreen);
            valueLbl.alignment = TextAlignmentOptions.Center;
            valueLbl.gameObject.AddComponent<LayoutElement>().preferredHeight = 16;

            btn.onClick.AddListener(() => {
                ServiceLocator.Get<ShopSystem>()?.TryConvertGoldToDollars(amount);
            });

            Action refresh = () =>
            {
                var w = ServiceLocator.Get<WalletSystem>();
                bool ok = w != null && w.CanAfford(Enums.CurrencyType.GoldIngot, amount);
                btn.interactable = ok;
                img.color        = ok ? BgCardSoft : new Color32(0x14, 0x16, 0x1C, 255);
                qtyLbl.color     = ok ? ColGold : TextMuted;
                valueLbl.color   = ok ? ColGreen : TextMuted;
            };
            _refreshActions.Add(refresh);
            refresh();
        }

        // ╔════════════════════════════════════════════════════════════════════╗
        // ║ Helpers UI                                                         ║
        // ╚════════════════════════════════════════════════════════════════════╝

        private GameObject MakeRoundedCard(Transform parent, Color32 color, int minHeight, bool fitContentHeight = true)
        {
            var card = MakeGO("Card", parent);
            var img  = card.AddComponent<Image>();
            img.sprite        = _sprRound16;
            img.type          = Image.Type.Sliced;
            img.color         = color;
            img.raycastTarget = false;

            var shadow = card.AddComponent<Shadow>();
            shadow.effectColor    = new Color(0f, 0f, 0f, 0.5f);
            shadow.effectDistance = new Vector2(0f, -4f);

            var vlg = card.AddComponent<VerticalLayoutGroup>();
            vlg.padding                = new RectOffset(0, 0, 0, 0);
            vlg.spacing                = 0;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth      = true;
            vlg.childControlHeight     = true;
            // Sans fitter, la hauteur est imposée par le parent (rangée) → cartes de hauteur égale.
            if (fitContentHeight)
                card.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var le = card.AddComponent<LayoutElement>();
            le.flexibleWidth = 1;
            le.minWidth      = 0;
            if (minHeight > 0) le.minHeight = minHeight;

            return card;
        }

        private GameObject BuildPairRow(Transform parent)
        {
            var row = MakeGO("PairRow", parent);
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing                = 16;
            hlg.childAlignment         = TextAnchor.UpperLeft;
            hlg.childForceExpandWidth  = true;
            hlg.childForceExpandHeight = true;
            hlg.childControlWidth      = true;
            hlg.childControlHeight     = true;
            row.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return row;
        }

        private GameObject MakeRow(Transform parent, int height, int spacing)
        {
            var row = MakeGO("Row", parent);
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing                = spacing;
            hlg.childAlignment         = TextAnchor.MiddleLeft;
            hlg.childForceExpandWidth  = false;
            hlg.childForceExpandHeight = false;
            hlg.childControlWidth      = true;
            hlg.childControlHeight     = true;
            row.AddComponent<LayoutElement>().preferredHeight = height;
            return row;
        }

        private void BuildAccentDot(Transform parent, Color32 color, int size)
        {
            var go  = MakeGO("Dot", parent);
            var img = go.AddComponent<Image>();
            img.sprite = _sprRound8;
            img.type   = Image.Type.Sliced;
            img.color  = color;
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth  = size;
            le.preferredHeight = size;
        }

        private static void AddCtaLeftIcon(Transform btn, string spritePath, Color32 color, int size)
        {
            var sprite = Resources.Load<Sprite>(spritePath);
            if (sprite == null) return;
            var go = new GameObject("Icon", typeof(RectTransform));
            go.transform.SetParent(btn, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0.5f);
            rt.anchorMax = new Vector2(0, 0.5f);
            rt.pivot = new Vector2(0, 0.5f);
            rt.anchoredPosition = new Vector2(14, 0);
            rt.sizeDelta = new Vector2(size, size);
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.preserveAspect = true;
            img.raycastTarget = false;
        }

        private void BuildIconImage(Transform parent, string spritePath, Color32 color, int size)
        {
            var go  = MakeGO("Icon", parent);
            var img = go.AddComponent<Image>();
            img.sprite         = Resources.Load<Sprite>(spritePath);
            img.enabled        = img.sprite != null;  // pas de carré blanc si l'icône manque
            img.color          = color;
            img.preserveAspect = true;
            img.raycastTarget  = false;
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth  = size;
            le.preferredHeight = size;
            le.minWidth        = size;
        }

        private void BuildCornerBadge(Transform parent, string text, Color32 color)
        {
            var go = MakeGO("Badge", parent);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot     = new Vector2(1, 1);
            rt.sizeDelta = new Vector2(54, 26);
            rt.anchoredPosition = new Vector2(-10, -10);

            var img = go.AddComponent<Image>();
            img.sprite = _sprRound8;
            img.type   = Image.Type.Sliced;
            img.color  = color;
            img.raycastTarget = false;

            var lbl = AddTMP("Lbl", go.transform, text, 12, FontStyles.Bold, Color.white);
            lbl.alignment = TextAlignmentOptions.Center;
            FillParent(lbl.GetComponent<RectTransform>());
        }

        private struct ButtonRefs
        {
            public Button   button;
            public Image    bg;
            public TMP_Text label;
        }

        private ButtonRefs BuildCtaButton(Transform parent, string label, Color32 bgColor, Color32 textColor,
                                          int height, Action onClick)
        {
            var go  = MakeGO("Cta", parent);
            var img = go.AddComponent<Image>();
            img.sprite = _sprRound16;
            img.type   = Image.Type.Sliced;
            img.color  = bgColor;
            img.raycastTarget = true;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            var btnColors = btn.colors;
            btnColors.disabledColor = new Color(1, 1, 1, 1);
            btn.colors = btnColors;

            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            le.flexibleWidth   = 1;

            var lbl = AddTMP("Lbl", go.transform, label, 14, FontStyles.Bold, textColor);
            lbl.alignment        = TextAlignmentOptions.Center;
            lbl.textWrappingMode = TextWrappingModes.NoWrap;
            lbl.characterSpacing = 3f;
            FillParent(lbl.GetComponent<RectTransform>());

            if (onClick != null) btn.onClick.AddListener(() => onClick());

            return new ButtonRefs { button = btn, bg = img, label = lbl };
        }

        // ╔════════════════════════════════════════════════════════════════════╗
        // ║ Formatters                                                         ║
        // ╚════════════════════════════════════════════════════════════════════╝

        private static string FormatCooldown(TimeSpan t)
        {
            if (t.TotalHours   >= 1) return $"{(int)t.TotalHours}h{t.Minutes:D2}";
            if (t.TotalMinutes >= 1) return $"{(int)t.TotalMinutes}m{t.Seconds:D2}";
            return $"{(int)t.TotalSeconds}s";
        }

        private static string FormatStarterCountdown(TimeSpan t)
        {
            if (t.TotalDays  >= 1) return $"{(int)t.TotalDays} JOURS";
            if (t.TotalHours >= 1) return $"{(int)t.TotalHours} HEURES";
            return $"{(int)t.TotalMinutes} MIN";
        }

        // ╔════════════════════════════════════════════════════════════════════╗
        // ║ Bas-niveau                                                         ║
        // ╚════════════════════════════════════════════════════════════════════╝

        private static GameObject MakeGO(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
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

        private static Sprite LoadSprite(string resourcePath)
        {
            if (string.IsNullOrEmpty(resourcePath)) return null;
            var spr = Resources.Load<Sprite>(resourcePath);
            if (spr != null) return spr;
            var tex = Resources.Load<Texture2D>(resourcePath);
            if (tex == null) return null;
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
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
