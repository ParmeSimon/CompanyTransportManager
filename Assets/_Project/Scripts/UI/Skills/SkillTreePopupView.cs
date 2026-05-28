using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TransportManager.Core;
using TransportManager.Entities.Progression;
using TransportManager.Enums;
using TransportManager.Systems.Progression;
using TransportManager.UI.Common;

namespace TransportManager.UI.Skills
{
    /// <summary>
    /// Arbre de compétences radial : un cercle central (le tronc + les points) d'où
    /// rayonnent 3 branches (Dépôt / RH / Essence) de 10 augments chacune, reliées par
    /// des liens. On sélectionne un nœud pour voir ses détails et le débloquer.
    /// Palette alignée sur le header / la sidebar.
    /// </summary>
    public class SkillTreePopupView : MonoBehaviour
    {
        // ── Palette partagée ─────────────────────────────────────────────────────
        private static readonly Color32 BgOverlay = new Color32(0x00, 0x00, 0x00, 200);
        private static readonly Color32 BgPanel   = new Color32(0x2C, 0x30, 0x38, 255);
        private static readonly Color32 BgCard    = new Color32(0x34, 0x38, 0x42, 255);
        private static readonly Color32 BgPill    = new Color32(0x1A, 0x1D, 0x24, 230);
        private static readonly Color32 BgGraph   = new Color32(0x12, 0x15, 0x1B, 255);
        private static readonly Color32 TextPri   = new Color32(0xEC, 0xEE, 0xF5, 255);
        private static readonly Color32 TextSec   = new Color32(0x7A, 0x8F, 0xA6, 255);
        private static readonly Color32 TextMuted = new Color32(0x5A, 0x65, 0x77, 255);
        private static readonly Color32 DivColor  = new Color32(0x3A, 0x3F, 0x4A, 200);
        private static readonly Color32 Gold      = new Color32(0xFA, 0xC0, 0x24, 255);
        private static readonly Color32 InkDark   = new Color32(0x10, 0x14, 0x1A, 255);

        private static readonly Color32 AccDepot = new Color32(0x35, 0x8E, 0xF5, 255);
        private static readonly Color32 AccHr    = new Color32(0x3D, 0xC9, 0x6E, 255);
        private static readonly Color32 AccFuel  = new Color32(0xFA, 0xC0, 0x24, 255);

        private const int   TitleBarH  = 56;
        private const float HubSize    = 92f;
        private const float NodeSize   = 50f;
        private const float StartDist  = 145f;   // hub → racine de branche
        private const float Step       = 95f;    // distance entre profondeurs
        private const float SectorHalf = 56f;    // demi-ouverture angulaire d'une branche (°)
        private static readonly Vector2 GraphSize = new Vector2(1680f, 1680f);

        // Branche → (titre, accent, angle en degrés)
        private struct BranchDef { public SkillBranch branch; public string title; public Color32 accent; public float angle; }
        private static readonly BranchDef[] Branches =
        {
            new BranchDef { branch = SkillBranch.Hr,    title = "RH",      accent = AccHr,    angle =  90f },
            new BranchDef { branch = SkillBranch.Depot, title = "DÉPÔT",   accent = AccDepot, angle = 210f },
            new BranchDef { branch = SkillBranch.Fuel,  title = "ESSENCE", accent = AccFuel,  angle = 330f },
        };

        private class NodeVis
        {
            public Image bg;
            public Outline outline;
            public Color32 baseOutline;
            public Vector2 pos;
        }

        private SkillTreeSystem _skills;
        private Transform _panel;
        private RectTransform _graph;
        private Transform _detailHost;
        private TMP_Text _ptsTop, _hubLabel;
        private string _selectedId;

        private readonly Dictionary<string, NodeVis> _nodes = new Dictionary<string, NodeVis>();
        private readonly Dictionary<string, Vector2> _pos   = new Dictionary<string, Vector2>();
        private readonly Dictionary<string, int>     _depth = new Dictionary<string, int>();

        private Sprite _sprR12, _sprR8, _sprCircle;

        // ── Entry point ───────────────────────────────────────────────────────────

        public static void Show()
        {
            if (FindObjectOfType<SkillTreePopupView>() != null) return;
            new GameObject("SkillTreePopup", typeof(RectTransform)).AddComponent<SkillTreePopupView>();
        }

        private void Awake()
        {
            _skills    = ServiceLocator.Get<SkillTreeSystem>();
            _sprR12    = MakeRoundedSprite(12);
            _sprR8     = MakeRoundedSprite(8);
            _sprCircle = MakeRoundedSprite(32); // rayon = moitié → cercle plein
            BuildShell();
        }

        // ── Build shell ─────────────────────────────────────────────────────────────

        private void BuildShell()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 510;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight  = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            var overlay = MakeImg("Overlay", transform, BgOverlay);
            overlay.raycastTarget = true;
            overlay.gameObject.AddComponent<Button>().onClick.AddListener(Close);
            FillParent(overlay.GetComponent<RectTransform>());

            var panelGo  = MakeGO("Panel", transform);
            var panelImg = panelGo.AddComponent<Image>();
            panelImg.sprite        = _sprR12;
            panelImg.type          = Image.Type.Sliced;
            panelImg.color         = BgPanel;
            panelImg.raycastTarget = true;
            var panelShadow = panelGo.AddComponent<Shadow>();
            panelShadow.effectColor    = new Color(0f, 0f, 0f, 0.5f);
            panelShadow.effectDistance = new Vector2(0f, -4f);
            var panelRt = panelGo.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.03f, 0.05f);
            panelRt.anchorMax = new Vector2(0.97f, 0.95f);
            panelRt.offsetMin = Vector2.zero;
            panelRt.offsetMax = Vector2.zero;
            _panel = panelGo.transform;

            PopupHeader.Build(_panel, "Compétences", Close, TitleBarH, _sprR8);

            BuildPointsBar();
            BuildGraphArea();
            BuildDetailCard();
            UpdateDetail(null);
        }

        private void BuildPointsBar()
        {
            var box = MakeGO("PointsBar", _panel);
            var img = box.AddComponent<Image>();
            img.sprite = _sprR8; img.type = Image.Type.Sliced; img.color = BgPill;
            var rt = box.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0.5f, 1);
            rt.offsetMin = new Vector2(12, -(TitleBarH + 8 + 50));
            rt.offsetMax = new Vector2(-12, -(TitleBarH + 8));

            var hlg = box.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(16, 16, 0, 0);
            hlg.spacing = 10;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = true;
            hlg.childControlWidth = true; hlg.childControlHeight = true;

            int pts = _skills?.AvailablePoints ?? 0;
            _ptsTop = AddTMP("Pts", box.transform, pts.ToString(), 26, FontStyles.Bold, Gold);
            _ptsTop.gameObject.AddComponent<LayoutElement>().preferredWidth = 40;

            AddTMP("Lbl", box.transform,
                "points de compétence — touchez un nœud pour le débloquer  (+1 / niveau d'entreprise)",
                12, FontStyles.Normal, TextSec)
                .gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
        }

        private void BuildGraphArea()
        {
            var scrollGo = MakeGO("Scroll", _panel);
            var scrollImg = scrollGo.AddComponent<Image>();
            scrollImg.sprite = _sprR8; scrollImg.type = Image.Type.Sliced; scrollImg.color = BgGraph;
            var srt = scrollGo.GetComponent<RectTransform>();
            srt.anchorMin = new Vector2(0, 0); srt.anchorMax = new Vector2(1, 1);
            srt.offsetMin = new Vector2(12, 12 + 158);
            srt.offsetMax = new Vector2(-12, -(TitleBarH + 8 + 50 + 8));

            var sr = scrollGo.AddComponent<ScrollRect>();
            sr.horizontal = true; sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Elastic;
            sr.scrollSensitivity = 30f;

            var viewport = MakeGO("Viewport", scrollGo.transform);
            FillParent(viewport.GetComponent<RectTransform>());
            viewport.AddComponent<RectMask2D>();
            var vpImg = viewport.AddComponent<Image>(); // cible de drag pour le pan
            vpImg.color = new Color(0, 0, 0, 0f);
            vpImg.raycastTarget = true;
            sr.viewport = viewport.GetComponent<RectTransform>();

            var graph = MakeGO("Graph", viewport.transform);
            var grt = graph.GetComponent<RectTransform>();
            grt.anchorMin = new Vector2(0.5f, 0.5f);
            grt.anchorMax = new Vector2(0.5f, 0.5f);
            grt.pivot     = new Vector2(0.5f, 0.5f);
            grt.sizeDelta = GraphSize;
            grt.anchoredPosition = Vector2.zero;
            sr.content = grt;
            _graph = grt;

            RebuildGraph();
        }

        // ── Construction du graphe ───────────────────────────────────────────────

        private void RebuildGraph()
        {
            _nodes.Clear();
            _pos.Clear();
            _depth.Clear();
            for (int i = _graph.childCount - 1; i >= 0; i--)
                Destroy(_graph.GetChild(i).gameObject);

            // Calcule la disposition en éventail de chaque branche.
            foreach (var b in Branches)
                LayoutBranch(b);

            // 1) Liens (derrière) : chaque nœud relié à son parent (ou au hub si racine).
            foreach (var b in Branches)
            {
                foreach (var node in SkillTreeCatalog.InBranch(b.branch))
                {
                    if (!_pos.ContainsKey(node.id)) continue;
                    Vector2 parent = node.IsBranchRoot || !_pos.ContainsKey(node.prerequisiteId)
                        ? Vector2.zero : _pos[node.prerequisiteId];
                    bool reached = _skills != null && _skills.IsUnlocked(node.id);
                    BuildLink(parent, _pos[node.id], reached ? (Color32)b.accent : DivColor);
                }
            }

            // 2) Hub central.
            BuildHub();

            // 3) Nœuds (devant).
            foreach (var b in Branches)
                foreach (var node in SkillTreeCatalog.InBranch(b.branch))
                    if (_pos.ContainsKey(node.id))
                        BuildNode(node, _pos[node.id], b.accent, _depth[node.id]);

            // Réapplique la sélection si elle existe encore.
            if (!string.IsNullOrEmpty(_selectedId) && _nodes.ContainsKey(_selectedId))
                Highlight(_selectedId, true);
        }

        // Dispose les nœuds d'une branche en éventail : profondeur = rayon, sous-arbres
        // répartis angulairement selon leur nombre de feuilles → remplit tout le secteur.
        private void LayoutBranch(BranchDef b)
        {
            var nodes = SkillTreeCatalog.InBranch(b.branch).ToList();
            var children = new Dictionary<string, List<SkillNodeDefinition>>();
            var roots = new List<SkillNodeDefinition>();
            foreach (var n in nodes)
            {
                if (n.IsBranchRoot) { roots.Add(n); continue; }
                if (!children.TryGetValue(n.prerequisiteId, out var l))
                    children[n.prerequisiteId] = l = new List<SkillNodeDefinition>();
                l.Add(n);
            }

            var leaves = new Dictionary<string, int>();
            int totalLeaves = 0;
            foreach (var r in roots) totalLeaves += CountLeaves(r.id, children, leaves);
            if (totalLeaves <= 0) return;

            float full = SectorHalf * 2f;
            float cursor = b.angle - SectorHalf;
            foreach (var r in roots)
            {
                float span = full * leaves[r.id] / totalLeaves;
                AssignNode(r.id, cursor, cursor + span, 1, children, leaves);
                cursor += span;
            }
        }

        private int CountLeaves(string id, Dictionary<string, List<SkillNodeDefinition>> children, Dictionary<string, int> leaves)
        {
            if (!children.TryGetValue(id, out var kids) || kids.Count == 0) { leaves[id] = 1; return 1; }
            int sum = 0;
            foreach (var c in kids) sum += CountLeaves(c.id, children, leaves);
            leaves[id] = sum;
            return sum;
        }

        private void AssignNode(string id, float angMin, float angMax, int depth,
            Dictionary<string, List<SkillNodeDefinition>> children, Dictionary<string, int> leaves)
        {
            float mid  = (angMin + angMax) * 0.5f;
            float rad  = mid * Mathf.Deg2Rad;
            float dist = StartDist + (depth - 1) * Step;
            _pos[id]   = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * dist;
            _depth[id] = depth;

            if (!children.TryGetValue(id, out var kids) || kids.Count == 0) return;
            int tl = 0;
            foreach (var c in kids) tl += leaves[c.id];
            float cursor = angMin;
            foreach (var c in kids)
            {
                float span = (angMax - angMin) * leaves[c.id] / tl;
                AssignNode(c.id, cursor, cursor + span, depth + 1, children, leaves);
                cursor += span;
            }
        }

        private void BuildLink(Vector2 a, Vector2 b, Color32 color)
        {
            var go = MakeImg("Link", _graph, color);
            go.raycastTarget = false;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = (a + b) * 0.5f;
            float len = Vector2.Distance(a, b);
            rt.sizeDelta = new Vector2(len, 5f);
            float ang = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
            rt.localRotation = Quaternion.Euler(0, 0, ang);
        }

        private void BuildHub()
        {
            var go = MakeGO("Hub", _graph);
            var img = go.AddComponent<Image>();
            img.sprite = _sprCircle; img.type = Image.Type.Simple; img.color = BgCard;
            var ol = go.AddComponent<Outline>();
            ol.effectColor = Gold; ol.effectDistance = new Vector2(2.5f, -2.5f);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(HubSize, HubSize);

            int pts = _skills?.AvailablePoints ?? 0;
            _hubLabel = AddTMP("Pts", go.transform, pts.ToString(), 34, FontStyles.Bold, Gold);
            _hubLabel.alignment = TextAlignmentOptions.Center;
            var lr = _hubLabel.GetComponent<RectTransform>();
            lr.anchorMin = new Vector2(0, 0.32f); lr.anchorMax = new Vector2(1, 1);
            lr.offsetMin = Vector2.zero; lr.offsetMax = Vector2.zero;

            var sub = AddTMP("Sub", go.transform, "POINTS", 10, FontStyles.Bold, TextSec);
            sub.alignment = TextAlignmentOptions.Center;
            sub.characterSpacing = 3;
            var sr = sub.GetComponent<RectTransform>();
            sr.anchorMin = new Vector2(0, 0.05f); sr.anchorMax = new Vector2(1, 0.34f);
            sr.offsetMin = Vector2.zero; sr.offsetMax = Vector2.zero;
        }

        private void BuildNode(SkillNodeDefinition node, Vector2 pos, Color32 accent, int depth)
        {
            bool unlocked  = _skills != null && _skills.IsUnlocked(node.id);
            bool canUnlock = _skills != null && _skills.CanUnlock(node.id, out _);

            var go = MakeGO("N_" + node.id, _graph);
            var img = go.AddComponent<Image>();
            img.sprite = _sprCircle; img.type = Image.Type.Simple;
            img.color = unlocked ? (Color)accent : (canUnlock ? (Color32)BgCard : BgPill);

            var ol = go.AddComponent<Outline>();
            Color32 baseOutline = unlocked ? accent : (canUnlock ? accent : DivColor);
            ol.effectColor = baseOutline;
            ol.effectDistance = new Vector2(canUnlock && !unlocked ? 2f : 1.2f, canUnlock && !unlocked ? -2f : -1.2f);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(NodeSize, NodeSize);

            Color32 numColor = unlocked ? InkDark : (canUnlock ? accent : TextMuted);
            var num = AddTMP("T", go.transform, depth.ToString(), 16, FontStyles.Bold, numColor);
            num.alignment = TextAlignmentOptions.Center;
            FillParent(num.GetComponent<RectTransform>());

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            string id = node.id;
            btn.onClick.AddListener(() => OnNodeTap(id));

            _nodes[node.id] = new NodeVis { bg = img, outline = ol, baseOutline = baseOutline, pos = pos };
        }

        // ── Sélection / surbrillance ───────────────────────────────────────────────

        private void OnNodeTap(string id)
        {
            if (!string.IsNullOrEmpty(_selectedId) && _selectedId != id)
                Highlight(_selectedId, false);
            _selectedId = id;
            Highlight(id, true);
            UpdateDetail(id);
        }

        private void Highlight(string id, bool on)
        {
            if (!_nodes.TryGetValue(id, out var v) || v.outline == null) return;
            if (on)
            {
                v.outline.effectColor    = Color.white;
                v.outline.effectDistance = new Vector2(3f, -3f);
            }
            else
            {
                v.outline.effectColor    = v.baseOutline;
                v.outline.effectDistance = new Vector2(1.2f, -1.2f);
            }
        }

        // ── Carte de détail (bas) ────────────────────────────────────────────────

        private void BuildDetailCard()
        {
            var host = MakeGO("Detail", _panel);
            var img = host.AddComponent<Image>();
            img.sprite = _sprR8; img.type = Image.Type.Sliced; img.color = BgPill;
            var rt = host.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, 0); rt.pivot = new Vector2(0.5f, 0);
            rt.offsetMin = new Vector2(12, 12);
            rt.offsetMax = new Vector2(-12, 12 + 138);
            _detailHost = host.transform;
        }

        private void UpdateDetail(string id)
        {
            for (int i = _detailHost.childCount - 1; i >= 0; i--)
                Destroy(_detailHost.GetChild(i).gameObject);

            if (string.IsNullOrEmpty(id) || _skills == null)
            {
                var hint = AddTMP("Hint", _detailHost, "Touchez un nœud pour voir ses détails.",
                                  14, FontStyles.Italic, TextSec);
                hint.alignment = TextAlignmentOptions.Center;
                FillParent(hint.GetComponent<RectTransform>());
                return;
            }

            var node = SkillTreeCatalog.GetById(id);
            if (node == null) return;

            bool unlocked  = _skills.IsUnlocked(id);
            bool prereqMet = node.IsBranchRoot || _skills.IsUnlocked(node.prerequisiteId);
            bool canUnlock = _skills.CanUnlock(id, out _);
            Color32 accent = AccentFor(node.branch);

            var vlg = _detailHost.gameObject.GetComponent<VerticalLayoutGroup>() ?? _detailHost.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(16, 16, 12, 12);
            vlg.spacing = 4;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true; vlg.childControlHeight = true;

            var title = AddTMP("Title", _detailHost, node.title, 17, FontStyles.Bold,
                               unlocked ? accent : TextPri);
            title.gameObject.AddComponent<LayoutElement>().preferredHeight = 24;

            var desc = AddTMP("Desc", _detailHost, node.description, 13, FontStyles.Normal, TextSec);
            desc.textWrappingMode = TextWrappingModes.Normal;
            desc.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1;

            // Ligne du bas : statut + bouton.
            var row = MakeGO("Row", _detailHost);
            row.AddComponent<LayoutElement>().preferredHeight = 42;
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 10; hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = true;
            hlg.childControlWidth = true; hlg.childControlHeight = true;

            string status;
            Color32 statusColor;
            if (unlocked)        { status = "✓ Débloqué";                 statusColor = accent; }
            else if (canUnlock)  { status = $"Coût : {node.cost} pt";     statusColor = TextPri; }
            else if (!prereqMet) { status = "🔒 Débloque le nœud précédent"; statusColor = TextMuted; }
            else                 { status = $"{node.cost} pt — points insuffisants"; statusColor = TextMuted; }

            var st = AddTMP("Status", row.transform, status, 13, FontStyles.Bold, statusColor);
            st.alignment = TextAlignmentOptions.MidlineLeft;
            st.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

            if (!unlocked)
            {
                var btnGo = MakeGO("Unlock", row.transform);
                var bImg = btnGo.AddComponent<Image>();
                bImg.sprite = _sprR8; bImg.type = Image.Type.Sliced;
                bImg.color = canUnlock ? (Color32)accent : new Color32(0x3A, 0x3F, 0x4A, 255);
                var le = btnGo.AddComponent<LayoutElement>();
                le.preferredWidth = 168; le.preferredHeight = 38;
                var lbl = AddTMP("L", btnGo.transform, canUnlock ? $"Débloquer · {node.cost} pt" : "Indisponible",
                                 13, FontStyles.Bold, canUnlock ? InkDark : TextMuted);
                lbl.alignment = TextAlignmentOptions.Center;
                FillParent(lbl.GetComponent<RectTransform>());
                if (canUnlock)
                {
                    var btn = btnGo.AddComponent<Button>();
                    btn.targetGraphic = bImg;
                    string nid = id;
                    btn.onClick.AddListener(() => OnUnlock(nid));
                }
            }
        }

        private void OnUnlock(string id)
        {
            if (_skills == null || !_skills.TryUnlock(id)) return;
            RebuildGraph();
            if (_ptsTop != null) _ptsTop.text = _skills.AvailablePoints.ToString();
            UpdateDetail(id);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static Color32 AccentFor(SkillBranch b)
        {
            switch (b)
            {
                case SkillBranch.Depot: return AccDepot;
                case SkillBranch.Hr:    return AccHr;
                default:                return AccFuel;
            }
        }

        private void Close() => Destroy(gameObject);

        private static GameObject MakeGO(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static Image MakeImg(string name, Transform parent, Color32 color)
        {
            var go  = MakeGO(name, parent);
            var img = go.AddComponent<Image>();
            img.color = color;
            return img;
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
            float dd = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
            return Mathf.Clamp01(r - dd + 0.5f);
        }
    }
}
