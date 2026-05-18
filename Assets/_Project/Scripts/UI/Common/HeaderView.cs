using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TransportManager.Core;
using TransportManager.Events;

namespace TransportManager.UI.Common
{
        public class HeaderView : MonoBehaviour
    {
        private TMP_Text _companyNameLabel;
        private TMP_Text _dollarsLabel;
        private TMP_Text _goldIngotsLabel;

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

            // --- Header background ---
            var img = GetComponent<Image>();
            if (img == null) img = gameObject.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.72f);
            img.sprite = null;
            img.type = Image.Type.Simple;

            var rt = GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(10, -68);
            rt.offsetMax = new Vector2(-10, -8);

            // --- CompanyName (gauche) ---
            var companyGo = new GameObject("CompanyName", typeof(RectTransform));
            companyGo.transform.SetParent(transform, false);
            _companyNameLabel = companyGo.AddComponent<TextMeshProUGUI>();
            _companyNameLabel.text = "Mon Entreprise";
            _companyNameLabel.fontSize = 17;
            _companyNameLabel.fontStyle = FontStyles.Bold;
            _companyNameLabel.color = new Color(0.85f, 0.85f, 0.90f);
            _companyNameLabel.alignment = TextAlignmentOptions.MidlineLeft;
            var companyRt = companyGo.GetComponent<RectTransform>();
            companyRt.anchorMin = new Vector2(0, 0);
            companyRt.anchorMax = new Vector2(0.5f, 1);
            companyRt.offsetMin = new Vector2(16, 0);
            companyRt.offsetMax = new Vector2(0, 0);

            // --- CurrencyGroup (droite) ---
            var currencyGo = new GameObject("CurrencyGroup", typeof(RectTransform));
            currencyGo.transform.SetParent(transform, false);
            var currencyRt = currencyGo.GetComponent<RectTransform>();
            currencyRt.anchorMin = new Vector2(0.5f, 0);
            currencyRt.anchorMax = new Vector2(1, 1);
            currencyRt.offsetMin = new Vector2(0, 0);
            currencyRt.offsetMax = new Vector2(-16, 0);
            var currencyLayout = currencyGo.AddComponent<HorizontalLayoutGroup>();
            currencyLayout.childAlignment = TextAnchor.MiddleRight;
            currencyLayout.spacing = 10;
            currencyLayout.childForceExpandWidth = false;
            currencyLayout.childForceExpandHeight = false;
            currencyLayout.padding = new RectOffset(0, 0, 0, 0);

            // --- Pill Dollars ---
            _dollarsLabel = CreatePill(currencyGo.transform, "DollarsGroup", "$15 000", new Color(0.15f, 0.35f, 0.65f, 0.75f), new Color(0.65f, 0.85f, 1f));

            // --- Pill Gold ---
            _goldIngotsLabel = CreatePill(currencyGo.transform, "GoldGroup", "◆ 10", new Color(0.45f, 0.30f, 0.05f, 0.75f), new Color(1f, 0.82f, 0.35f));

        }

        private TMP_Text CreatePill(Transform parent, string name, string defaultText, Color bgColor, Color textColor)
        {
            var pillGo = new GameObject(name, typeof(RectTransform));
            pillGo.transform.SetParent(parent, false);

            var pillImg = pillGo.AddComponent<Image>();
            pillImg.color = bgColor;
            pillImg.sprite = null;
            pillImg.type = Image.Type.Simple;

            var layout = pillGo.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.padding = new RectOffset(12, 12, 6, 6);
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var le = pillGo.AddComponent<LayoutElement>();
            le.preferredHeight = 36;

            var textGo = new GameObject("Label", typeof(RectTransform));
            textGo.transform.SetParent(pillGo.transform, false);
            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = defaultText;
            tmp.fontSize = 14;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = textColor;
            tmp.alignment = TextAlignmentOptions.Center;

            var textLe = textGo.AddComponent<LayoutElement>();
            textLe.preferredHeight = 24;

            return tmp;
        }

        private void OnEnable()
        {
            GameEvents.OnDollarsChanged += UpdateDollars;
            GameEvents.OnGoldIngotsChanged += UpdateGoldIngots;
        }

        private void OnDisable()
        {
            GameEvents.OnDollarsChanged -= UpdateDollars;
            GameEvents.OnGoldIngotsChanged -= UpdateGoldIngots;
        }

        private void Start()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Save == null) return;
            if (_companyNameLabel) _companyNameLabel.text = gm.Save.company.companyName;
            UpdateDollars(gm.Save.dollars);
            UpdateGoldIngots(gm.Save.goldIngots);
        }

        private void UpdateDollars(int value)
        {
            if (_dollarsLabel) _dollarsLabel.text = $"$ {value:N0}";
        }

        private void UpdateGoldIngots(int value)
        {
            if (_goldIngotsLabel) _goldIngotsLabel.text = $"◆ {value:N0}";
        }
    }
}
