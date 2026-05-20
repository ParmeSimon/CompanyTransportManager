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

        private struct TabEntry
        {
            public Button button;
            public TabType type;
            public GameObject activeBar;
            public Image bgImage;
            public TMP_Text labelText;
            public TMP_Text iconText;
        }

        private readonly System.Collections.Generic.List<TabEntry> _entries = new();

        private void Awake()
        {
            BuildEntries();
        }

        private void BuildEntries()
        {
            _entries.Clear();

            Add(mapButton,      TabType.Map);
            Add(depotButton,    TabType.Depot);
            Add(vehiclesButton, TabType.Vehicles);
            Add(hrButton,       TabType.Hr);
            Add(fuelButton,     TabType.Fuel);
            Add(shopButton,     TabType.Shop);

            if (mapButton)      TutorialTargetRegistry.Register("tab:map",      mapButton.GetComponent<RectTransform>());
            if (depotButton)    TutorialTargetRegistry.Register("tab:depot",    depotButton.GetComponent<RectTransform>());
            if (vehiclesButton) TutorialTargetRegistry.Register("tab:vehicles", vehiclesButton.GetComponent<RectTransform>());
            if (shopButton)     TutorialTargetRegistry.Register("tab:shop",     shopButton.GetComponent<RectTransform>());
        }

        // Looks for child GameObjects named "ActiveBar", "Label", "Icon" inside each button.
        // Name your children accordingly in the Unity hierarchy.
        private void Add(Button btn, TabType type)
        {
            if (btn == null) return;
            _entries.Add(new TabEntry
            {
                button    = btn,
                type      = type,
                bgImage   = btn.GetComponent<Image>(),
                activeBar = btn.transform.Find("ActiveBar")?.gameObject,
                labelText = btn.transform.Find("Label")?.GetComponent<TMP_Text>(),
                iconText  = btn.transform.Find("Icon")?.GetComponent<TMP_Text>(),
            });
        }

        private void OnEnable()
        {
            foreach (var e in _entries)
                if (e.button != null) e.button.onClick.AddListener(() => SelectTab(e.type));
            GameEvents.OnTabChanged += OnTabChanged;
            UpdateVisuals();
        }

        private void OnDisable()
        {
            foreach (var e in _entries)
                if (e.button != null) e.button.onClick.RemoveAllListeners();
            GameEvents.OnTabChanged -= OnTabChanged;
        }

        private void SelectTab(TabType t)
        {
            _activeTab = t;
            GameEvents.RaiseTabChanged(t);
            UpdateVisuals();
        }

        private void OnTabChanged(TabType tab)
        {
            _activeTab = tab;
            UpdateVisuals();
        }

        private void UpdateVisuals()
        {
            foreach (var e in _entries)
            {
                bool active = e.type == _activeTab;
                if (e.bgImage  != null) e.bgImage.color = active ? new Color(0.05f, 0.07f, 0.10f, 0.96f) : new Color(0.07f, 0.09f, 0.12f, 0.88f);
                if (e.activeBar != null) e.activeBar.SetActive(active);
                var color = active ? Color.white : new Color(0.78f, 0.82f, 0.88f);
                if (e.labelText != null) e.labelText.color = color;
                if (e.iconText  != null) e.iconText.color  = active ? new Color(1f, 0.85f, 0.30f) : color;
            }
        }
    }
}
