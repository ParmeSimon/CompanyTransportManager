using TMPro;
using UnityEngine;
using TransportManager.Core;
using TransportManager.Events;

namespace TransportManager.UI.Common
{
    public class HeaderView : MonoBehaviour
    {
        [SerializeField] private TMP_Text companyNameLabel;
        [SerializeField] private TMP_Text dollarsLabel;
        [SerializeField] private TMP_Text goldIngotsLabel;

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
            if (companyNameLabel) companyNameLabel.text = gm.Save.company.companyName;
            UpdateDollars(gm.Save.dollars);
            UpdateGoldIngots(gm.Save.goldIngots);
        }

        private void UpdateDollars(int value)
        {
            if (dollarsLabel) dollarsLabel.text = $"${value:N0}";
        }

        private void UpdateGoldIngots(int value)
        {
            if (goldIngotsLabel) goldIngotsLabel.text = value.ToString("N0");
        }
    }
}
