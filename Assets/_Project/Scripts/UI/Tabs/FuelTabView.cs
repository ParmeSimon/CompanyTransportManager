using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TransportManager.Core;
using TransportManager.Events;
using TransportManager.Systems.Fuel;

namespace TransportManager.UI.Tabs
{
    public class FuelTabView : MonoBehaviour
    {
        private TMP_Text _tierLabel;
        private TMP_Text _litersLabel;
        private TMP_Text _capacityLabel;
        private Slider _fuelSlider;
        private TMP_Text _refillStatusLabel;
        private TMP_Text _refillCostLabel;
        private Button _refillButton;
        private TMP_Text _refillButtonLabel;
        private TMP_Text _instantCostLabel;
        private Button _instantButton;
        private TMP_Text _upgradeCostLabel;
        private Button _upgradeButton;
        private TMP_Text _upgradeButtonLabel;

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

            var bg = GetComponent<Image>();
            if (bg == null) bg = gameObject.AddComponent<Image>();
            bg.color = new Color(0.96f, 0.96f, 0.97f);

            // Scroll
            var scrollGo = new GameObject("ScrollView", typeof(RectTransform));
            scrollGo.transform.SetParent(transform, false);
            var scrollRect = scrollGo.AddComponent<ScrollRect>();
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.anchorMin = Vector2.zero;
            scrollRt.anchorMax = Vector2.one;
            scrollRt.offsetMin = Vector2.zero;
            scrollRt.offsetMax = Vector2.zero;
            scrollRect.horizontal = false;
            scrollRect.scrollSensitivity = 30f;

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(scrollGo.transform, false);
            var contentRt = contentGo.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0, 1);
            contentRt.anchorMax = new Vector2(1, 1);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.offsetMin = Vector2.zero;
            contentRt.offsetMax = Vector2.zero;
            var vlg = contentGo.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(16, 16, 20, 20);
            vlg.spacing = 14;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            var csf = contentGo.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scrollRect.content = contentRt;

            // --- Card : Etat de la cuve ---
            var stateCard = CreateCard(contentGo.transform, "StateCard");
            CreateCardTitle(stateCard, "Cuve Ã  carburant");
            _tierLabel = CreateCardRowSimple(stateCard, "TierRow", "Pompe", "Niveau 1");
            BuildFuelBar(stateCard);
            _litersLabel = CreateCardRowSimple(stateCard, "LitersRow", "Stock actuel", "0 L");
            _capacityLabel = CreateCardRowSimple(stateCard, "CapacityRow", "CapacitÃ©", "500 L");

            // --- Card : Recharge camion ---
            var refillCard = CreateCard(contentGo.transform, "RefillCard");
            CreateCardTitle(refillCard, "Commander un camion-citerne");
            _refillStatusLabel = CreateCardDescription(refillCard, "Recharge disponible");
            _refillCostLabel = CreateCardRowSimple(refillCard, "CostRow", "CoÃ»t (plein)", "$ 0");
            _refillButton = CreateActionButton(refillCard, "RefillButton", new Color(0.20f, 0.65f, 0.45f));
            _refillButtonLabel = _refillButton.GetComponentInChildren<TMP_Text>();

            // --- Card : Recharge instantanÃ©e ---
            var instantCard = CreateCard(contentGo.transform, "InstantCard");
            CreateCardTitle(instantCard, "Recharge instantanÃ©e");
            CreateCardDescription(instantCard, "Utilise des lingots d'or pour remplir la cuve immÃ©diatement.");
            _instantCostLabel = CreateCardRowSimple(instantCard, "InstantCostRow", "CoÃ»t", "â—† 0");
            _instantButton = CreateActionButton(instantCard, "InstantButton", new Color(0.86f, 0.60f, 0.10f));
            _instantButton.GetComponentInChildren<TMP_Text>().text = "Recharge instantanÃ©e";

            // --- Card : AmÃ©lioration pompe ---
            var upgradeCard = CreateCard(contentGo.transform, "UpgradeCard");
            CreateCardTitle(upgradeCard, "AmÃ©liorer la pompe");
            CreateCardDescription(upgradeCard, "Augmente la capacitÃ© de stockage et rÃ©duit le temps de livraison.");
            _upgradeCostLabel = CreateCardRowSimple(upgradeCard, "UpgradeCostRow", "CoÃ»t", "$ 0");
            _upgradeButton = CreateActionButton(upgradeCard, "UpgradeButton", new Color(0.29f, 0.56f, 0.89f));
            _upgradeButtonLabel = _upgradeButton.GetComponentInChildren<TMP_Text>();
        }

        private void BuildFuelBar(GameObject card)
        {
            var rowGo = new GameObject("FuelBarRow", typeof(RectTransform));
            rowGo.transform.SetParent(card.transform, false);
            var le = rowGo.AddComponent<LayoutElement>();
            le.preferredHeight = 28;

            _fuelSlider = rowGo.AddComponent<Slider>();
            _fuelSlider.minValue = 0f;
            _fuelSlider.maxValue = 1f;
            _fuelSlider.value = 0f;
            _fuelSlider.interactable = false;

            // Background
            var bgGo = new GameObject("Background", typeof(RectTransform));
            bgGo.transform.SetParent(rowGo.transform, false);
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.color = new Color(0.88f, 0.88f, 0.90f);
            var bgRt = bgGo.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;

            // Fill area
            var fillAreaGo = new GameObject("Fill Area", typeof(RectTransform));
            fillAreaGo.transform.SetParent(rowGo.transform, false);
            var fillAreaRt = fillAreaGo.GetComponent<RectTransform>();
            fillAreaRt.anchorMin = Vector2.zero;
            fillAreaRt.anchorMax = Vector2.one;
            fillAreaRt.offsetMin = Vector2.zero;
            fillAreaRt.offsetMax = Vector2.zero;

            var fillGo = new GameObject("Fill", typeof(RectTransform));
            fillGo.transform.SetParent(fillAreaGo.transform, false);
            var fillImg = fillGo.AddComponent<Image>();
            fillImg.color = new Color(0.20f, 0.65f, 0.45f);
            fillImg.sprite = null;
            fillImg.type = Image.Type.Simple;
            var fillRt = fillGo.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;

            _fuelSlider.fillRect = fillRt;
            _fuelSlider.targetGraphic = bgImg;
        }

        // ---- Card builders (shared helpers) ----

        private GameObject CreateCard(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = Color.white;
            img.sprite = null;
            img.type = Image.Type.Simple;
            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(18, 18, 16, 16);
            vlg.spacing = 10;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            var csf = go.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            go.AddComponent<LayoutElement>().minHeight = 60;
            return go;
        }

        private void CreateCardTitle(GameObject card, string title)
        {
            var go = new GameObject("Title", typeof(RectTransform));
            go.transform.SetParent(card.transform, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = title;
            tmp.fontSize = 15;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = new Color(0.13f, 0.13f, 0.18f);
            go.AddComponent<LayoutElement>().preferredHeight = 22;
        }

        private TMP_Text CreateCardDescription(GameObject card, string text)
        {
            var go = new GameObject("Desc", typeof(RectTransform));
            go.transform.SetParent(card.transform, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 12;
            tmp.color = new Color(0.5f, 0.5f, 0.55f);
            go.AddComponent<LayoutElement>().preferredHeight = 18;
            return tmp;
        }

        private TMP_Text CreateCardRowSimple(GameObject card, string name, string leftText, string rightText)
        {
            var row = new GameObject(name, typeof(RectTransform));
            row.transform.SetParent(card.transform, false);
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = false;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            row.AddComponent<LayoutElement>().preferredHeight = 28;

            var leftGo = new GameObject("Left", typeof(RectTransform));
            leftGo.transform.SetParent(row.transform, false);
            var leftTmp = leftGo.AddComponent<TextMeshProUGUI>();
            leftTmp.text = leftText;
            leftTmp.fontSize = 13;
            leftTmp.color = new Color(0.45f, 0.45f, 0.50f);
            leftTmp.alignment = TextAlignmentOptions.MidlineLeft;

            var rightGo = new GameObject("Right", typeof(RectTransform));
            rightGo.transform.SetParent(row.transform, false);
            var rightTmp = rightGo.AddComponent<TextMeshProUGUI>();
            rightTmp.text = rightText;
            rightTmp.fontSize = 13;
            rightTmp.fontStyle = FontStyles.Bold;
            rightTmp.color = new Color(0.13f, 0.13f, 0.18f);
            rightTmp.alignment = TextAlignmentOptions.MidlineRight;

            return rightTmp;
        }

        private Button CreateActionButton(GameObject card, string name, Color color)
        {
            var btnGo = new GameObject(name, typeof(RectTransform));
            btnGo.transform.SetParent(card.transform, false);
            var btnImg = btnGo.AddComponent<Image>();
            btnImg.color = color;
            btnImg.sprite = null;
            btnImg.type = Image.Type.Simple;
            var btn = btnGo.AddComponent<Button>();
            var colors = btn.colors;
            colors.disabledColor = new Color(0.75f, 0.75f, 0.80f);
            btn.colors = colors;
            btnGo.AddComponent<LayoutElement>().preferredHeight = 44;
            var hlg = btnGo.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(btnGo.transform, false);
            var tmp = labelGo.AddComponent<TextMeshProUGUI>();
            tmp.text = "Action";
            tmp.fontSize = 14;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;

            return btn;
        }

        // ---- Lifecycle ----

        private void OnEnable()
        {
            if (_refillButton) _refillButton.onClick.AddListener(OnRefillClicked);
            if (_instantButton) _instantButton.onClick.AddListener(OnInstantRefillClicked);
            if (_upgradeButton) _upgradeButton.onClick.AddListener(OnUpgradeClicked);
            GameEvents.OnStationFuelChanged += OnFuelChanged;
            GameEvents.OnFuelRefillStarted += OnRefillStateChanged;
            GameEvents.OnFuelRefillCompleted += OnRefillStateChanged;
            GameEvents.OnPumpUpgraded += OnPumpUpgraded;
            GameEvents.OnDollarsChanged += OnDollarsChanged;
            GameEvents.OnGoldIngotsChanged += OnGoldChanged;
            Refresh();
        }

        private void OnDisable()
        {
            if (_refillButton) _refillButton.onClick.RemoveListener(OnRefillClicked);
            if (_instantButton) _instantButton.onClick.RemoveListener(OnInstantRefillClicked);
            if (_upgradeButton) _upgradeButton.onClick.RemoveListener(OnUpgradeClicked);
            GameEvents.OnStationFuelChanged -= OnFuelChanged;
            GameEvents.OnFuelRefillStarted -= OnRefillStateChanged;
            GameEvents.OnFuelRefillCompleted -= OnRefillStateChanged;
            GameEvents.OnPumpUpgraded -= OnPumpUpgraded;
            GameEvents.OnDollarsChanged -= OnDollarsChanged;
            GameEvents.OnGoldIngotsChanged -= OnGoldChanged;
        }

        private void Update()
        {
            var fuel = ServiceLocator.Get<FuelSystem>();
            if (fuel == null || !fuel.IsRefilling) return;
            var remaining = fuel.RefillRemaining;
            if (_refillStatusLabel)
                _refillStatusLabel.text = $"Livraison en cours... {(int)remaining.TotalMinutes:D2}:{remaining.Seconds:D2}";
        }

        private void OnFuelChanged(float _) => Refresh();
        private void OnRefillStateChanged(Entities.Fuel.FuelStationState _) => Refresh();
        private void OnPumpUpgraded(int _) => Refresh();
        private void OnDollarsChanged(int _) => Refresh();
        private void OnGoldChanged(int _) => Refresh();

        private void OnRefillClicked()
        {
            var fuel = ServiceLocator.Get<FuelSystem>();
            if (fuel == null) return;
            fuel.TryStartTruckRefill(fuel.RemainingCapacity);
        }

        private void OnInstantRefillClicked()
        {
            var fuel = ServiceLocator.Get<FuelSystem>();
            if (fuel == null) return;
            fuel.TryInstantRefillWithIngots(fuel.RemainingCapacity);
        }

        private void OnUpgradeClicked()
        {
            var fuel = ServiceLocator.Get<FuelSystem>();
            fuel?.TryUpgradePump();
        }

        private void Refresh()
        {
            var fuel = ServiceLocator.Get<FuelSystem>();
            if (fuel == null) return;

            var tier = fuel.CurrentTier;
            float liters = fuel.CurrentLiters;
            float capacity = fuel.MaxCapacityLiters;
            float fillRatio = capacity > 0 ? liters / capacity : 0f;

            if (_tierLabel) _tierLabel.text = tier != null ? tier.displayName : $"Niveau {fuel.State.pumpLevel}";
            if (_litersLabel) _litersLabel.text = $"{liters:F0} L";
            if (_capacityLabel) _capacityLabel.text = $"{capacity:F0} L";
            if (_fuelSlider) _fuelSlider.value = fillRatio;

            // Refill button
            bool isRefilling = fuel.IsRefilling;
            bool canRefill = !isRefilling && fuel.RemainingCapacity > 0;
            if (_refillButton) _refillButton.interactable = canRefill;
            if (_refillButtonLabel) _refillButtonLabel.text = isRefilling ? "En cours..." : "Commander";
            if (_refillStatusLabel && !isRefilling) _refillStatusLabel.text = isRefilling ? "" : "Cuve disponible";

            if (fuel.Config != null && tier != null)
            {
                float remaining = fuel.RemainingCapacity;
                int refillCost = Mathf.CeilToInt(remaining * fuel.Config.dollarsPerLiter);
                if (_refillCostLabel) _refillCostLabel.text = $"$ {refillCost:N0}";

                float pct = capacity > 0 ? remaining / capacity : 0f;
                int ingotCost = Mathf.CeilToInt(pct * tier.instantRefillIngotCost);
                if (_instantCostLabel) _instantCostLabel.text = $"â—† {ingotCost}";
            }

            bool instantAvailable = !isRefilling && fuel.RemainingCapacity > 0;
            if (_instantButton) _instantButton.interactable = instantAvailable;

            // Upgrade button
            var nextTier = fuel.Config != null ? fuel.Config.GetNextTier(fuel.State.pumpLevel) : null;
            bool hasNextTier = nextTier != null;
            bool canUpgrade = hasNextTier && fuel.CanUpgradePump();
            if (_upgradeButton) _upgradeButton.interactable = hasNextTier;
            if (_upgradeButtonLabel)
            {
                if (!hasNextTier) _upgradeButtonLabel.text = "Niveau maximum";
                else if (!canUpgrade) _upgradeButtonLabel.text = "Fonds insuffisants";
                else _upgradeButtonLabel.text = "AmÃ©liorer";
            }
            if (_upgradeCostLabel)
                _upgradeCostLabel.text = nextTier != null ? $"$ {nextTier.upgradeCostDollars:N0}" : "â€”";
        }
    }
}
