using System.Collections.Generic;
using System.Linq;
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
    /// <summary>
    /// "Terminal Équipage" : interface type ordinateur listant les profils de
    /// conducteurs (embauchés + candidats) classés par note générale décroissante.
    /// Chaque profil affiche ses 6 stats 0-100 et sa moyenne générale.
    /// </summary>
    public class HrTabView : MonoBehaviour
    {
        // ── Palette (alignée header / sidebar) ───────────────────────────────────
        private static readonly Color BgRoot    = new Color(0x1A / 255f, 0x1D / 255f, 0x24 / 255f, 1f);    // fond profond (= pill header)
        private static readonly Color BgPanel   = new Color(0x2C / 255f, 0x30 / 255f, 0x38 / 255f, 1f);    // panneau (= header/navbar)
        private static readonly Color BgCard    = new Color(0x34 / 255f, 0x3A / 255f, 0x44 / 255f, 1f);    // carte (légère élévation)
        private static readonly Color BgInset   = new Color(0x1A / 255f, 0x1D / 255f, 0x24 / 255f, 1f);    // inset (= pill header)
        private static readonly Color Accent    = new Color(0x3D / 255f, 0xC9 / 255f, 0x6E / 255f, 1f);    // vert header
        private static readonly Color Border    = new Color(0x3A / 255f, 0x3F / 255f, 0x4A / 255f, 0.6f);  // séparateur header
        private static readonly Color TextPri   = new Color(0xEC / 255f, 0xEE / 255f, 0xF5 / 255f, 1f);
        private static readonly Color TextSec   = new Color(0x7A / 255f, 0x8F / 255f, 0xA6 / 255f, 1f);
        private static readonly Color TextDim   = new Color(0x56 / 255f, 0x60 / 255f, 0x7A / 255f, 1f);
        private static readonly Color TierHigh  = new Color(0x3D / 255f, 0xC9 / 255f, 0x6E / 255f, 1f);    // vert header
        private static readonly Color TierMid   = new Color(0xF2 / 255f, 0xD9 / 255f, 0x66 / 255f, 1f);    // or header
        private static readonly Color TierLow   = new Color(0xE0 / 255f, 0x65 / 255f, 0x5B / 255f, 1f);    // rouge

        // ── Layout ─────────────────────────────────────────────────────────────
        private const float SIDEBAR_RESERVE = 130f;
        private const float TOP_RESERVE     = 160f;
        private const float RIGHT_PADDING   = 32f;
        private const float BOTTOM_PADDING  = 32f;
        private const float HEADER_HEIGHT   = 56f;
        private const float MIN_CARD_W      = 360f;
        private const float CARD_H          = 340f;
        private const float GRID_SPACING    = 14f;
        private const float CONTENT_PAD     = 14f;

        private static readonly (string label, System.Func<DriverStats, float> get)[] StatDefs =
        {
            ("Vitesse",       s => s.speed),
            ("Carburant",     s => s.fuelEfficiency),
            ("Sécurité",      s => s.safety),
            ("Concentration", s => s.concentration),
            ("Esquive",       s => s.dodge),
            ("Endurance",     s => s.endurance),
        };

        private RectTransform _viewportRt;
        private GridLayoutGroup _crewGrid;
        private GridLayoutGroup _poolGrid;
        private Transform _crewGridTf;
        private Transform _poolGridTf;
        private TMP_Text _crewCount;
        private TMP_Text _poolCount;
        private float _lastWidth = -1f;

        private Sprite _sprR8, _sprR12, _sprR16;

        private void Awake() => Build();

        // ── Build ────────────────────────────────────────────────────────────────

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
            EnsureSprites();

            var rt = GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var bg = GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            bg.color  = BgRoot;
            bg.sprite = null;

            // Panneau principal (sous le HUD)
            var panel   = MakeGO("Panel", transform);
            var panelRt = panel.GetComponent<RectTransform>();
            panelRt.anchorMin = Vector2.zero;
            panelRt.anchorMax = Vector2.one;
            panelRt.offsetMin = new Vector2(SIDEBAR_RESERVE, BOTTOM_PADDING);
            panelRt.offsetMax = new Vector2(-RIGHT_PADDING, -TOP_RESERVE);
            var panelImg = panel.AddComponent<Image>();
            panelImg.sprite = _sprR16;
            panelImg.type   = Image.Type.Sliced;
            panelImg.color  = BgPanel;
            var panelShadow = panel.AddComponent<Shadow>();
            panelShadow.effectColor    = new Color(0f, 0f, 0f, 0.5f);
            panelShadow.effectDistance = new Vector2(3f, -4f);

            BuildHeader(panel.transform);
            BuildScroll(panel.transform);
        }

        private void BuildHeader(Transform panel)
        {
            var header   = MakeGO("Header", panel);
            var headerRt = header.GetComponent<RectTransform>();
            headerRt.anchorMin = new Vector2(0, 1);
            headerRt.anchorMax = new Vector2(1, 1);
            headerRt.pivot     = new Vector2(0.5f, 1f);
            headerRt.offsetMin = new Vector2(0, -HEADER_HEIGHT);
            headerRt.offsetMax = Vector2.zero;

            var hlg = header.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(22, 22, 0, 0);
            hlg.spacing = 12;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;

            var accent = MakeImg("Accent", header.transform, Accent);
            accent.sprite = _sprR8; accent.type = Image.Type.Sliced;
            var accentLe = accent.gameObject.AddComponent<LayoutElement>();
            accentLe.preferredWidth = 3; accentLe.preferredHeight = 26;

            var title = MakeTMP("Title", header.transform,
                "<b>TERMINAL ÉQUIPAGE</b>  <color=#56607A>//  profils classés par note générale</color>",
                18, FontStyles.Normal, TextPri);
            title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

            // Séparateur sous le header
            var sep = MakeImg("HeaderSep", panel, Border);
            var sepRt = sep.GetComponent<RectTransform>();
            sepRt.anchorMin = new Vector2(0, 1);
            sepRt.anchorMax = new Vector2(1, 1);
            sepRt.pivot     = new Vector2(0.5f, 1f);
            sepRt.offsetMin = new Vector2(14, -HEADER_HEIGHT - 1);
            sepRt.offsetMax = new Vector2(-14, -HEADER_HEIGHT);
        }

        private void BuildScroll(Transform panel)
        {
            var scrollGo = MakeGO("Scroll", panel);
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.anchorMin = Vector2.zero;
            scrollRt.anchorMax = Vector2.one;
            scrollRt.offsetMin = new Vector2(10, 10);
            scrollRt.offsetMax = new Vector2(-10, -(HEADER_HEIGHT + 10));

            var scrollRect = scrollGo.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.scrollSensitivity = 40;
            scrollRect.movementType = ScrollRect.MovementType.Elastic;

            var viewport = MakeGO("Viewport", scrollGo.transform);
            FillParent(viewport.GetComponent<RectTransform>());
            viewport.AddComponent<RectMask2D>();
            _viewportRt = viewport.GetComponent<RectTransform>();
            scrollRect.viewport = _viewportRt;

            var content   = MakeGO("Content", viewport.transform);
            var contentRt = content.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0, 1);
            contentRt.anchorMax = new Vector2(1, 1);
            contentRt.pivot     = new Vector2(0.5f, 1f);
            contentRt.sizeDelta = Vector2.zero;
            scrollRect.content = contentRt;

            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset((int)CONTENT_PAD, (int)CONTENT_PAD, 8, 18);
            vlg.spacing = 12;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Section ÉQUIPAGE
            BuildSectionHeader(content.transform, "ÉQUIPAGE", out _crewCount);
            _crewGridTf = BuildGrid(content.transform, "CrewGrid", out _crewGrid);

            // Section CANDIDATS
            BuildSectionHeader(content.transform, "CANDIDATS", out _poolCount);
            _poolGridTf = BuildGrid(content.transform, "PoolGrid", out _poolGrid);

            // Bouton rafraîchir
            var refreshBtn = MakeButton(content.transform, "Régénérer les candidats", new Color(0x3A / 255f, 0x41 / 255f, 0x50 / 255f, 1f), 46);
            refreshBtn.onClick.AddListener(OnRefreshPool);
        }

        private void BuildSectionHeader(Transform parent, string title, out TMP_Text countLabel)
        {
            var row = MakeGO("Sec_" + title, parent);
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 10;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            row.AddComponent<LayoutElement>().preferredHeight = 30;

            var dot = MakeImg("Dot", row.transform, Accent);
            dot.sprite = _sprR8; dot.type = Image.Type.Sliced;
            var dotLe = dot.gameObject.AddComponent<LayoutElement>();
            dotLe.preferredWidth = 8; dotLe.preferredHeight = 8;

            var lbl = MakeTMP("Title", row.transform, title, 14, FontStyles.Bold, Accent);
            lbl.characterSpacing = 6;
            lbl.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

            countLabel = MakeTMP("Count", row.transform, "0", 13, FontStyles.Bold, TextSec);
            countLabel.alignment = TextAlignmentOptions.MidlineRight;
            countLabel.gameObject.AddComponent<LayoutElement>().preferredWidth = 60;
        }

        private Transform BuildGrid(Transform parent, string name, out GridLayoutGroup grid)
        {
            var go = MakeGO(name, parent);
            grid = go.AddComponent<GridLayoutGroup>();
            grid.padding = new RectOffset(0, 0, 0, 0);
            grid.spacing = new Vector2(GRID_SPACING, GRID_SPACING);
            grid.cellSize = new Vector2(MIN_CARD_W, CARD_H);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            grid.childAlignment = TextAnchor.UpperLeft;
            go.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return go.transform;
        }

        // ── Profil card ────────────────────────────────────────────────────────

        private void BuildProfileCard(Transform parent, DriverInstance d, bool hired)
        {
            int level = XpCurve.DriverLevelFromXp(d.xp);
            float general = d.stats.General;

            var card = MakeGO("P_" + d.instanceId, parent);
            var cardImg = card.AddComponent<Image>();
            cardImg.sprite = _sprR12; cardImg.type = Image.Type.Sliced; cardImg.color = BgCard;
            // Liseré coloré (effet contour terminal)
            var outline = card.AddComponent<Outline>();
            outline.effectColor = Border;
            outline.effectDistance = new Vector2(1.2f, -1.2f);

            var vlg = card.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(16, 16, 14, 14);
            vlg.spacing = 8;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;

            // ── Ligne identité : monogramme + nom/nationalité + niveau ──
            var idRow = MakeGO("IdRow", card.transform);
            var idHlg = idRow.AddComponent<HorizontalLayoutGroup>();
            idHlg.spacing = 12;
            idHlg.childAlignment = TextAnchor.MiddleLeft;
            idHlg.childForceExpandWidth = false;
            idHlg.childForceExpandHeight = false;
            idHlg.childControlWidth = true;
            idHlg.childControlHeight = true;
            idRow.AddComponent<LayoutElement>().preferredHeight = 48;

            BuildMonogram(idRow.transform, d, general);

            var nameCol = MakeGO("NameCol", idRow.transform);
            var nameVlg = nameCol.AddComponent<VerticalLayoutGroup>();
            nameVlg.spacing = 1; nameVlg.childForceExpandHeight = false; nameVlg.childControlHeight = true;
            nameVlg.childForceExpandWidth = true; nameVlg.childControlWidth = true;
            nameCol.AddComponent<LayoutElement>().flexibleWidth = 1;
            var nameLbl = MakeTMP("Name", nameCol.transform, d.FullName, 16, FontStyles.Bold, TextPri);
            nameLbl.overflowMode = TextOverflowModes.Ellipsis; nameLbl.textWrappingMode = TextWrappingModes.NoWrap;
            string nat = string.IsNullOrEmpty(d.nationality) ? "—" : d.nationality;
            MakeTMP("Sub", nameCol.transform, $"<color=#56607A>Niv.</color> {level}   <color=#3A3F4A>•</color>   {nat}", 12, FontStyles.Normal, TextSec);

            // ── Bloc note générale ──
            BuildGeneralBlock(card.transform, general);

            // ── Barres de stats ──
            foreach (var def in StatDefs)
                BuildStatBar(card.transform, def.label, def.get(d.stats));

            // ── Action ──
            if (hired)
            {
                var fireBtn = MakeButton(card.transform, "Licencier", new Color(0x6B / 255f, 0x2A / 255f, 0x2A / 255f, 1f), 36);
                string id = d.instanceId;
                fireBtn.onClick.AddListener(() => OnFireClicked(id));
            }
            else
            {
                var hireBtn = MakeButton(card.transform, $"Embaucher  ·  ${d.desiredWagePerContract}/contrat",
                                         new Color(0x2E / 255f, 0x7D / 255f, 0x52 / 255f, 1f), 36);
                string id = d.instanceId; int wage = d.desiredWagePerContract;
                hireBtn.onClick.AddListener(() => OnHireClicked(id, wage));
            }
        }

        private void BuildMonogram(Transform parent, DriverInstance d, float general)
        {
            var go = MakeGO("Mono", parent);
            var img = go.AddComponent<Image>();
            img.sprite = _sprR12; img.type = Image.Type.Sliced; img.color = BgInset;
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = 44; le.minWidth = 44; le.preferredHeight = 44;

            string initials = $"{Initial(d.firstName)}{Initial(d.lastName)}";
            var lbl = MakeTMP("Init", go.transform, initials, 18, FontStyles.Bold, TierColor(general));
            lbl.alignment = TextAlignmentOptions.Center;
            FillParent(lbl.GetComponent<RectTransform>());
        }

        private void BuildGeneralBlock(Transform parent, float general)
        {
            var row = MakeGO("General", parent);
            var img = row.AddComponent<Image>();
            img.sprite = _sprR8; img.type = Image.Type.Sliced; img.color = BgInset;
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(12, 12, 0, 0);
            hlg.spacing = 8;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            row.AddComponent<LayoutElement>().preferredHeight = 36;

            var lbl = MakeTMP("Lbl", row.transform, "GÉNÉRAL", 12, FontStyles.Bold, TextSec);
            lbl.characterSpacing = 4;
            lbl.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

            var val = MakeTMP("Val", row.transform, Mathf.RoundToInt(general).ToString(), 26, FontStyles.Bold, TierColor(general));
            val.alignment = TextAlignmentOptions.MidlineRight;
            val.gameObject.AddComponent<LayoutElement>().preferredWidth = 60;
        }

        private void BuildStatBar(Transform parent, string label, float value)
        {
            var row = MakeGO("Stat_" + label, parent);
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            row.AddComponent<LayoutElement>().preferredHeight = 18;

            var lbl = MakeTMP("Lbl", row.transform, label, 11.5f, FontStyles.Normal, TextSec);
            var lblLe = lbl.gameObject.AddComponent<LayoutElement>();
            lblLe.preferredWidth = 104; lblLe.minWidth = 104;

            // Piste
            var track = MakeGO("Track", row.transform);
            var trackImg = track.AddComponent<Image>();
            trackImg.sprite = _sprR8; trackImg.type = Image.Type.Sliced; trackImg.color = BgInset;
            track.AddComponent<LayoutElement>().flexibleWidth = 1;

            // Remplissage
            var fill = MakeGO("Fill", track.transform);
            var fillImg = fill.AddComponent<Image>();
            fillImg.sprite = _sprR8; fillImg.type = Image.Type.Sliced; fillImg.color = TierColor(value);
            var fillRt = fill.GetComponent<RectTransform>();
            fillRt.anchorMin = new Vector2(0, 0);
            fillRt.anchorMax = new Vector2(Mathf.Clamp01(value / 100f), 1);
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;

            var val = MakeTMP("Val", row.transform, Mathf.RoundToInt(value).ToString(), 11.5f, FontStyles.Bold, TextPri);
            val.alignment = TextAlignmentOptions.MidlineRight;
            var valLe = val.gameObject.AddComponent<LayoutElement>();
            valLe.preferredWidth = 28; valLe.minWidth = 28;
        }

        // ── Responsive grid sizing ───────────────────────────────────────────────

        private void Update()
        {
            if (_viewportRt == null) return;
            float w = _viewportRt.rect.width;
            if (w <= 1f || Mathf.Approximately(w, _lastWidth)) return;
            _lastWidth = w;
            ResizeGrids(w - CONTENT_PAD * 2f);
        }

        private void ResizeGrids(float innerWidth)
        {
            if (innerWidth <= 1f) return;
            int cols = Mathf.Max(1, Mathf.FloorToInt((innerWidth + GRID_SPACING) / (MIN_CARD_W + GRID_SPACING)));
            float cell = (innerWidth - GRID_SPACING * (cols - 1)) / cols;
            ApplyGrid(_crewGrid, cols, cell);
            ApplyGrid(_poolGrid, cols, cell);
        }

        private static void ApplyGrid(GridLayoutGroup grid, int cols, float cellW)
        {
            if (grid == null) return;
            grid.constraintCount = cols;
            grid.cellSize = new Vector2(cellW, CARD_H);
        }

        // ── Lifecycle / refresh ──────────────────────────────────────────────────

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

        private void OnHireClicked(string candidateId, int wage) => ServiceLocator.Get<HrSystem>()?.Hire(candidateId, wage);
        private void OnFireClicked(string driverId) => ServiceLocator.Get<HrSystem>()?.Fire(driverId);

        private void OnRefreshPool()
        {
            ServiceLocator.Get<HrSystem>()?.RefreshRecruitmentPool();
            Refresh();
        }

        private void Refresh()
        {
            var hr = ServiceLocator.Get<HrSystem>();
            if (hr == null || _crewGridTf == null) return;
            hr.EnsureRecruitmentPool();

            ClearChildren(_crewGridTf);
            ClearChildren(_poolGridTf);

            var crew = hr.HiredDrivers.OrderByDescending(d => d.stats.General).ToList();
            var pool = hr.RecruitmentPool.OrderByDescending(d => d.stats.General).ToList();

            if (_crewCount) _crewCount.text = crew.Count.ToString();
            if (_poolCount) _poolCount.text = pool.Count.ToString();

            if (crew.Count == 0)
                AddEmptyNote(_crewGridTf, "Aucun conducteur embauché.");
            else
                foreach (var d in crew) BuildProfileCard(_crewGridTf, d, hired: true);

            foreach (var d in pool) BuildProfileCard(_poolGridTf, d, hired: false);

            _lastWidth = -1f; // force recompute des colonnes au prochain Update
        }

        private void AddEmptyNote(Transform gridParent, string message)
        {
            // Note pleine largeur : on la pose hors-grille via un enfant simple stylé.
            var note = MakeGO("Empty", gridParent);
            var img = note.AddComponent<Image>();
            img.sprite = _sprR8; img.type = Image.Type.Sliced; img.color = BgInset;
            var lbl = MakeTMP("Lbl", note.transform, message, 13, FontStyles.Italic, TextDim);
            lbl.alignment = TextAlignmentOptions.Center;
            FillParent(lbl.GetComponent<RectTransform>());
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static Color TierColor(float v)
        {
            if (v >= 70f) return TierHigh;
            if (v >= 40f) return TierMid;
            return TierLow;
        }

        private static string Initial(string s) =>
            string.IsNullOrEmpty(s) ? "?" : s.Substring(0, 1).ToUpper();

        private Button MakeButton(Transform parent, string label, Color color, float height)
        {
            var go = MakeGO("Btn", parent);
            var img = go.AddComponent<Image>();
            img.sprite = _sprR8; img.type = Image.Type.Sliced; img.color = color;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            go.AddComponent<LayoutElement>().preferredHeight = height;

            var lbl = MakeTMP("Lbl", go.transform, label, 13, FontStyles.Bold, TextPri);
            lbl.alignment = TextAlignmentOptions.Center;
            FillParent(lbl.GetComponent<RectTransform>());
            return btn;
        }

        private static void ClearChildren(Transform t)
        {
            foreach (Transform c in t)
            {
#if UNITY_EDITOR
                DestroyImmediate(c.gameObject);
#else
                Destroy(c.gameObject);
#endif
            }
        }

        private static GameObject MakeGO(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static Image MakeImg(string name, Transform parent, Color color)
        {
            var go = MakeGO(name, parent);
            var img = go.AddComponent<Image>();
            img.color = color;
            return img;
        }

        private static TMP_Text MakeTMP(string name, Transform parent, string text, float size, FontStyles style, Color color)
        {
            var go = MakeGO(name, parent);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
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

        private void EnsureSprites()
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
                wrapMode = TextureWrapMode.Clamp,
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
    }
}
