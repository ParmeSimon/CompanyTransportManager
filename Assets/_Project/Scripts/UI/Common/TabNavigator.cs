using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TransportManager.Enums;
using TransportManager.Events;
using TransportManager.Systems.Tutorial;

namespace TransportManager.UI.Common
{
    public class TabNavigator : MonoBehaviour
    {
        [SerializeField] private Button mapButton;
        [SerializeField] private Button depotButton;
        [SerializeField] private Button vehiclesButton;
        [SerializeField] private Button shopButton;
        [SerializeField] private Button fuelButton;
        [SerializeField] private Button hrButton;

        private TabType _activeTab = TabType.Map;
        private static Sprite _roundedSprite;

        private void Awake() => ApplyStyle();

        private static Sprite GetRoundedSprite()
        {
            if (_roundedSprite != null) return _roundedSprite;
            const int size = 64;
            const int radius = 18;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool inside = true;
                    int cx = x, cy = y;
                    if (x < radius && y < radius) { cx = radius; cy = radius; inside = (x - cx) * (x - cx) + (y - cy) * (y - cy) <= radius * radius; }
                    else if (x >= size - radius && y < radius) { cx = size - radius - 1; cy = radius; inside = (x - cx) * (x - cx) + (y - cy) * (y - cy) <= radius * radius; }
                    else if (x < radius && y >= size - radius) { cx = radius; cy = size - radius - 1; inside = (x - cx) * (x - cx) + (y - cy) * (y - cy) <= radius * radius; }
                    else if (x >= size - radius && y >= size - radius) { cx = size - radius - 1; cy = size - radius - 1; inside = (x - cx) * (x - cx) + (y - cy) * (y - cy) <= radius * radius; }
                    tex.SetPixel(x, y, inside ? Color.white : new Color(0, 0, 0, 0));
                }
            }
            tex.Apply();
            _roundedSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
            return _roundedSprite;
        }

        private void ApplyStyle()
        {
            var img = GetComponent<Image>();
            if (img != null)
            {
                img.color = new Color(0f, 0f, 0f, 0.72f);
                img.sprite = GetRoundedSprite();
                img.type = Image.Type.Sliced;
            }

            StyleTabButton(mapButton, "MAP", "Carte");
            StyleTabButton(depotButton, "DEP", "Dépôt");
            StyleTabButton(vehiclesButton, "VEH", "Véhicules");
            StyleTabButton(shopButton, "SHOP", "Magasin");
            if (fuelButton) StyleTabButton(fuelButton, "FUEL", "Carburant");
            if (hrButton) StyleTabButton(hrButton, "RH", "RH");

            if (mapButton) TutorialTargetRegistry.Register("tab:map", mapButton.GetComponent<RectTransform>());
            if (depotButton) TutorialTargetRegistry.Register("tab:depot", depotButton.GetComponent<RectTransform>());
            if (vehiclesButton) TutorialTargetRegistry.Register("tab:vehicles", vehiclesButton.GetComponent<RectTransform>());
            if (shopButton) TutorialTargetRegistry.Register("tab:shop", shopButton.GetComponent<RectTransform>());
        }

        private static void StyleTabButton(Button btn, string emoji, string label)
        {
            if (btn == null) return;

            // Keep existing Image component as-is for raycast/click compatibility
            // Just change its appearance
            var img = btn.GetComponent<Image>();
            if (img != null)
            {
                img.enabled = true;
                img.raycastTarget = true;
                img.color = new Color(1f, 1f, 1f, 0.001f);
                img.sprite = GetRoundedSprite();
                img.type = Image.Type.Sliced;
            }
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.None;
            btn.interactable = true;

            // Find existing label (TMP) and update its content + style
            var existingLabel = btn.GetComponentInChildren<TMP_Text>(true);
            if (existingLabel != null)
            {
                existingLabel.text = emoji + "\n" + label;
                existingLabel.fontSize = 11;
                existingLabel.fontStyle = FontStyles.Bold;
                existingLabel.alignment = TextAlignmentOptions.Center;
                existingLabel.color = new Color(0.85f, 0.85f, 0.90f);
                existingLabel.enableWordWrapping = false;
                existingLabel.raycastTarget = false;

                // Stretch label to fill the button
                var rt = existingLabel.rectTransform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }
        }

        private void OnEnable()
        {
            if (mapButton) mapButton.onClick.AddListener(GoToMap);
            if (depotButton) depotButton.onClick.AddListener(GoToDepot);
            if (vehiclesButton) vehiclesButton.onClick.AddListener(GoToVehicles);
            if (shopButton) shopButton.onClick.AddListener(GoToShop);
            if (fuelButton) fuelButton.onClick.AddListener(GoToFuel);
            if (hrButton) hrButton.onClick.AddListener(GoToHr);
            GameEvents.OnTabChanged += OnTabChanged;
            UpdateVisuals();
        }

        private void OnDisable()
        {
            if (mapButton) mapButton.onClick.RemoveListener(GoToMap);
            if (depotButton) depotButton.onClick.RemoveListener(GoToDepot);
            if (vehiclesButton) vehiclesButton.onClick.RemoveListener(GoToVehicles);
            if (shopButton) shopButton.onClick.RemoveListener(GoToShop);
            if (fuelButton) fuelButton.onClick.RemoveListener(GoToFuel);
            if (hrButton) hrButton.onClick.RemoveListener(GoToHr);
            GameEvents.OnTabChanged -= OnTabChanged;
        }

        private void GoToMap()      { _activeTab = TabType.Map;      GameEvents.RaiseTabChanged(TabType.Map);      UpdateVisuals(); }
        private void GoToDepot()    { _activeTab = TabType.Depot;    GameEvents.RaiseTabChanged(TabType.Depot);    UpdateVisuals(); }
        private void GoToVehicles() { _activeTab = TabType.Vehicles; GameEvents.RaiseTabChanged(TabType.Vehicles); UpdateVisuals(); }
        private void GoToShop()     { _activeTab = TabType.Shop;     GameEvents.RaiseTabChanged(TabType.Shop);     UpdateVisuals(); }
        private void GoToFuel()     { _activeTab = TabType.Fuel;     GameEvents.RaiseTabChanged(TabType.Fuel);     UpdateVisuals(); }
        private void GoToHr()       { _activeTab = TabType.Hr;       GameEvents.RaiseTabChanged(TabType.Hr);       UpdateVisuals(); }

        private void OnTabChanged(TabType tab)
        {
            _activeTab = tab;
            UpdateVisuals();
        }

        private void UpdateVisuals()
        {
            SetActive(mapButton,      _activeTab == TabType.Map);
            SetActive(depotButton,    _activeTab == TabType.Depot);
            SetActive(vehiclesButton, _activeTab == TabType.Vehicles);
            SetActive(shopButton,     _activeTab == TabType.Shop);
            SetActive(fuelButton,     _activeTab == TabType.Fuel);
            SetActive(hrButton,       _activeTab == TabType.Hr);
        }

        private static void SetActive(Button btn, bool active)
        {
            if (btn == null) return;
            var img = btn.GetComponent<Image>();
            if (img) img.color = active ? new Color(0f, 0f, 0f, 0.55f) : new Color(1f, 1f, 1f, 0.001f);
            var lbl = btn.GetComponentInChildren<TMP_Text>(true);
            if (lbl) lbl.color = active ? Color.white : new Color(0.85f, 0.85f, 0.90f);
        }
    }
}
