using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TransportManager.Core;
using TransportManager.Entities.Drivers;
using TransportManager.Entities.Progression;
using TransportManager.Events;
using TransportManager.Systems.Hr;

namespace TransportManager.UI.Tabs
{
    public class HrTabView : MonoBehaviour
    {
        private Transform _hiredContainer;
        private Transform _poolContainer;
        private TMP_Text _hiredCountLabel;
        private TMP_Text _poolCountLabel;

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

            // --- Section : Conducteurs embauchÃ©s ---
            var hiredHeader = BuildSectionHeader(contentGo.transform, "Conducteurs embauchÃ©s", out _hiredCountLabel);
            _hiredContainer = BuildListContainer(contentGo.transform, "HiredList");

            // --- Section : Pool de recrutement ---
            BuildSectionHeader(contentGo.transform, "Candidats disponibles", out _poolCountLabel);
            _poolContainer = BuildListContainer(contentGo.transform, "PoolList");

            // Bouton rafraÃ®chir pool
            var refreshCard = CreateCard(contentGo.transform, "RefreshCard");
            CreateCardDescription(refreshCard, "GÃ©nÃ©rer de nouveaux candidats (rÃ©initialise la liste).");
            var refreshBtn = CreateButton(refreshCard, "RefreshBtn", "RafraÃ®chir les candidats", new Color(0.50f, 0.50f, 0.58f));
            refreshBtn.onClick.AddListener(OnRefreshPool);
        }

        // ---- Section header ----

        private GameObject BuildSectionHeader(Transform parent, string title, out TMP_Text countLabel)
        {
            var go = new GameObject("SectionHeader_" + title, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = false;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            go.AddComponent<LayoutElement>().preferredHeight = 28;

            var titleGo = new GameObject("Title", typeof(RectTransform));
            titleGo.transform.SetParent(go.transform, false);
            var titleTmp = titleGo.AddComponent<TextMeshProUGUI>();
            titleTmp.text = title;
            titleTmp.fontSize = 14;
            titleTmp.fontStyle = FontStyles.Bold;
            titleTmp.color = new Color(0.35f, 0.35f, 0.40f);
            titleTmp.alignment = TextAlignmentOptions.MidlineLeft;

            var countGo = new GameObject("Count", typeof(RectTransform));
            countGo.transform.SetParent(go.transform, false);
            countLabel = countGo.AddComponent<TextMeshProUGUI>();
            countLabel.text = "(0)";
            countLabel.fontSize = 12;
            countLabel.color = new Color(0.60f, 0.60f, 0.65f);
            countLabel.alignment = TextAlignmentOptions.MidlineRight;

            return go;
        }

        private Transform BuildListContainer(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 8;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            var csf = go.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return go.transform;
        }

        // ---- Driver cards ----

        private void BuildHiredCard(DriverInstance driver)
        {
            var card = CreateCard(_hiredContainer, "Driver_" + driver.instanceId);

            // Name + level row
            var nameRow = new GameObject("NameRow", typeof(RectTransform));
            nameRow.transform.SetParent(card.transform, false);
            var nameHlg = nameRow.AddComponent<HorizontalLayoutGroup>();
            nameHlg.childForceExpandWidth = true;
            nameHlg.childForceExpandHeight = false;
            nameHlg.childAlignment = TextAnchor.MiddleLeft;
            nameRow.AddComponent<LayoutElement>().preferredHeight = 26;

            var nameLbl = CreateLabel(nameRow.transform, "Name", driver.FullName, 14, FontStyles.Bold, new Color(0.13f, 0.13f, 0.18f), TextAlignmentOptions.MidlineLeft);
            int level = XpCurve.DriverLevelFromXp(driver.xp);
            CreateBadge(nameRow.transform, $"Niv. {level}", new Color(0.29f, 0.56f, 0.89f));

            // Stats row
            var statsRow = new GameObject("StatsRow", typeof(RectTransform));
            statsRow.transform.SetParent(card.transform, false);
            var statsHlg = statsRow.AddComponent<HorizontalLayoutGroup>();
            statsHlg.spacing = 6;
            statsHlg.childForceExpandWidth = false;
            statsHlg.childForceExpandHeight = false;
            statsHlg.childAlignment = TextAnchor.MiddleLeft;
            statsRow.AddComponent<LayoutElement>().preferredHeight = 22;

            string vehicle = string.IsNullOrEmpty(driver.assignedVehicleInstanceId) ? "Sans vÃ©hicule" : "AssignÃ©";
            Color vehicleColor = string.IsNullOrEmpty(driver.assignedVehicleInstanceId)
                ? new Color(0.85f, 0.35f, 0.25f)
                : new Color(0.20f, 0.65f, 0.45f);
            CreateBadge(statsRow.transform, vehicle, vehicleColor);
            CreateBadge(statsRow.transform, $"Salaire : ${driver.assignedWagePerContract}/contrat", new Color(0.50f, 0.50f, 0.55f));

            // XP bar
            int xpForCurrent = XpCurve.XpForDriverLevel(level);
            int xpForNext = XpCurve.XpForDriverLevel(level + 1);
            float xpProgress = xpForNext > xpForCurrent
                ? (float)(driver.xp - xpForCurrent) / (xpForNext - xpForCurrent)
                : 1f;
            BuildMiniBar(card, xpProgress, new Color(0.29f, 0.56f, 0.89f), $"XP : {driver.xp}");

            // Fire button
            var fireBtn = CreateButton(card, "FireBtn", "Licencier", new Color(0.80f, 0.25f, 0.20f));
            string driverId = driver.instanceId;
            fireBtn.onClick.AddListener(() => OnFireClicked(driverId));
        }

        private void BuildCandidateCard(DriverInstance candidate)
        {
            var card = CreateCard(_poolContainer, "Candidate_" + candidate.instanceId);

            // Name + level
            var nameRow = new GameObject("NameRow", typeof(RectTransform));
            nameRow.transform.SetParent(card.transform, false);
            var nameHlg = nameRow.AddComponent<HorizontalLayoutGroup>();
            nameHlg.childForceExpandWidth = true;
            nameHlg.childForceExpandHeight = false;
            nameHlg.childAlignment = TextAnchor.MiddleLeft;
            nameRow.AddComponent<LayoutElement>().preferredHeight = 26;

            CreateLabel(nameRow.transform, "Name", candidate.FullName, 14, FontStyles.Bold, new Color(0.13f, 0.13f, 0.18f), TextAlignmentOptions.MidlineLeft);
            int level = XpCurve.DriverLevelFromXp(candidate.xp);
            CreateBadge(nameRow.transform, $"Niv. {level}", new Color(0.29f, 0.56f, 0.89f));

            // Stats
            var statsRow = new GameObject("StatsRow", typeof(RectTransform));
            statsRow.transform.SetParent(card.transform, false);
            var statsHlg = statsRow.AddComponent<HorizontalLayoutGroup>();
            statsHlg.spacing = 6;
            statsHlg.childForceExpandWidth = false;
            statsHlg.childForceExpandHeight = false;
            statsHlg.childAlignment = TextAnchor.MiddleLeft;
            statsRow.AddComponent<LayoutElement>().preferredHeight = 22;

            CreateBadge(statsRow.transform, $"PrÃ©tention : ${candidate.desiredWagePerContract}/contrat", new Color(0.50f, 0.50f, 0.55f));

            // Skills badges
            var skillsRow = new GameObject("SkillsRow", typeof(RectTransform));
            skillsRow.transform.SetParent(card.transform, false);
            var skillsHlg = skillsRow.AddComponent<HorizontalLayoutGroup>();
            skillsHlg.spacing = 6;
            skillsHlg.childForceExpandWidth = false;
            skillsHlg.childForceExpandHeight = false;
            skillsHlg.childAlignment = TextAnchor.MiddleLeft;
            skillsRow.AddComponent<LayoutElement>().preferredHeight = 22;

            if (candidate.stats.speedBonus > 0)
                CreateBadge(skillsRow.transform, $"+{candidate.stats.speedBonus * 100:F0}% vitesse", new Color(0.90f, 0.55f, 0.10f));
            if (candidate.stats.fuelEfficiencyBonus > 0)
                CreateBadge(skillsRow.transform, $"+{candidate.stats.fuelEfficiencyBonus * 100:F0}% carburant", new Color(0.20f, 0.65f, 0.45f));
            if (candidate.stats.safetyScore > 0.7f)
                CreateBadge(skillsRow.transform, "Prudent", new Color(0.29f, 0.56f, 0.89f));

            // Hire button
            var hireBtn = CreateButton(card, "HireBtn", $"Embaucher (${candidate.desiredWagePerContract}/contrat)", new Color(0.20f, 0.65f, 0.45f));
            string candidateId = candidate.instanceId;
            int wage = candidate.desiredWagePerContract;
            hireBtn.onClick.AddListener(() => OnHireClicked(candidateId, wage));
        }

        private void BuildMiniBar(GameObject card, float value, Color fillColor, string labelText)
        {
            var rowGo = new GameObject("XpRow", typeof(RectTransform));
            rowGo.transform.SetParent(card.transform, false);
            var hlg = rowGo.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = false;
            rowGo.AddComponent<LayoutElement>().preferredHeight = 20;

            var lbl = new GameObject("XpLabel", typeof(RectTransform));
            lbl.transform.SetParent(rowGo.transform, false);
            var lblTmp = lbl.AddComponent<TextMeshProUGUI>();
            lblTmp.text = labelText;
            lblTmp.fontSize = 11;
            lblTmp.color = new Color(0.55f, 0.55f, 0.60f);
            lblTmp.alignment = TextAlignmentOptions.MidlineLeft;
            lbl.AddComponent<LayoutElement>().minWidth = 80;

            var barGo = new GameObject("Bar", typeof(RectTransform));
            barGo.transform.SetParent(rowGo.transform, false);
            barGo.AddComponent<LayoutElement>().preferredHeight = 8;

            var barBg = barGo.AddComponent<Image>();
            barBg.color = new Color(0.88f, 0.88f, 0.90f);
            barBg.sprite = null;
            barBg.type = Image.Type.Simple;

            var fillGo = new GameObject("Fill", typeof(RectTransform));
            fillGo.transform.SetParent(barGo.transform, false);
            var fillImg = fillGo.AddComponent<Image>();
            fillImg.color = fillColor;
            fillImg.sprite = null;
            fillImg.type = Image.Type.Simple;
            var fillRt = fillGo.GetComponent<RectTransform>();
            fillRt.anchorMin = new Vector2(0, 0);
            fillRt.anchorMax = new Vector2(Mathf.Clamp01(value), 1);
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;
        }

        // ---- Generic helpers ----

        private GameObject CreateCard(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = Color.white;
            img.sprite = null;
            img.type = Image.Type.Simple;
            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(16, 16, 14, 14);
            vlg.spacing = 8;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            var csf = go.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            go.AddComponent<LayoutElement>().minHeight = 50;
            return go;
        }

        private TMP_Text CreateLabel(Transform parent, string name, string text, float size, FontStyles style, Color color, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.alignment = align;
            return tmp;
        }

        private void CreateBadge(Transform parent, string text, Color color)
        {
            var go = new GameObject("Badge_" + text, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(color.r, color.g, color.b, 0.12f);
            img.sprite = null;
            img.type = Image.Type.Simple;
            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(8, 8, 3, 3);
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            go.AddComponent<LayoutElement>().preferredHeight = 22;

            var lbl = new GameObject("Lbl", typeof(RectTransform));
            lbl.transform.SetParent(go.transform, false);
            var tmp = lbl.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 11;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
        }

        private void CreateCardDescription(GameObject card, string text)
        {
            var go = new GameObject("Desc", typeof(RectTransform));
            go.transform.SetParent(card.transform, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 12;
            tmp.color = new Color(0.5f, 0.5f, 0.55f);
            go.AddComponent<LayoutElement>().preferredHeight = 18;
        }

        private Button CreateButton(GameObject card, string name, string label, Color color)
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
            btnGo.AddComponent<LayoutElement>().preferredHeight = 40;
            var hlg = btnGo.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            var lbl = new GameObject("Label", typeof(RectTransform));
            lbl.transform.SetParent(btnGo.transform, false);
            var tmp = lbl.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 13;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;

            return btn;
        }

        // ---- Lifecycle ----

        private void OnEnable()
        {
            GameEvents.OnDriverHired += OnDriverChanged;
            GameEvents.OnDriverFired += OnDriverChanged;
            GameEvents.OnDriverResigned += OnDriverChanged;
            GameEvents.OnDriverAssigned += OnDriverChanged;
            GameEvents.OnDriverXpChanged += OnDriverChanged;
            Refresh();
        }

        private void OnDisable()
        {
            GameEvents.OnDriverHired -= OnDriverChanged;
            GameEvents.OnDriverFired -= OnDriverChanged;
            GameEvents.OnDriverResigned -= OnDriverChanged;
            GameEvents.OnDriverAssigned -= OnDriverChanged;
            GameEvents.OnDriverXpChanged -= OnDriverChanged;
        }

        private void OnDriverChanged(DriverInstance _) => Refresh();

        private void OnHireClicked(string candidateId, int wage)
        {
            var hr = ServiceLocator.Get<HrSystem>();
            hr?.Hire(candidateId, wage);
        }

        private void OnFireClicked(string driverId)
        {
            var hr = ServiceLocator.Get<HrSystem>();
            hr?.Fire(driverId);
        }

        private void OnRefreshPool()
        {
            var hr = ServiceLocator.Get<HrSystem>();
            hr?.RefreshRecruitmentPool();
            Refresh();
        }

        private void Refresh()
        {
            var hr = ServiceLocator.Get<HrSystem>();
            if (hr == null) return;

            hr.EnsureRecruitmentPool();

            // Clear and rebuild hired list
            foreach (Transform child in _hiredContainer)
            {
#if UNITY_EDITOR
                DestroyImmediate(child.gameObject);
#else
                Destroy(child.gameObject);
#endif
            }

            var hired = hr.HiredDrivers;
            if (_hiredCountLabel) _hiredCountLabel.text = $"({hired.Count})";
            if (hired.Count == 0)
            {
                var emptyCard = CreateCard(_hiredContainer, "EmptyHired");
                var emptyGo = new GameObject("Msg", typeof(RectTransform));
                emptyGo.transform.SetParent(emptyCard.transform, false);
                var tmp = emptyGo.AddComponent<TextMeshProUGUI>();
                tmp.text = "Aucun conducteur embauchÃ©.";
                tmp.fontSize = 13;
                tmp.color = new Color(0.55f, 0.55f, 0.60f);
                tmp.alignment = TextAlignmentOptions.Center;
                emptyGo.AddComponent<LayoutElement>().preferredHeight = 28;
            }
            else
            {
                foreach (var d in hired)
                    BuildHiredCard(d);
            }

            // Clear and rebuild pool list
            foreach (Transform child in _poolContainer)
            {
#if UNITY_EDITOR
                DestroyImmediate(child.gameObject);
#else
                Destroy(child.gameObject);
#endif
            }

            var pool = hr.RecruitmentPool;
            if (_poolCountLabel) _poolCountLabel.text = $"({pool.Count})";
            foreach (var c in pool)
                BuildCandidateCard(c);
        }
    }
}
