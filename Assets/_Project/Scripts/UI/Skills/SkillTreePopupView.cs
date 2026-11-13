using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.UI;
using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TransportManager.Core;
using TransportManager.Entities.Progression;
using TransportManager.Enums;
using TransportManager.Systems.Progression;
using TransportManager.UI.Common;

namespace TransportManager.UI.Skills
{
    /// <summary>
    /// Arbre de compétences « organique » : un tronc doré pousse depuis le bas, se termine
    /// par la couronne (les points disponibles) d'où partent trois grosses branches —
    /// Dépôt (bleu, à gauche), RH (violet, au centre), Essence (vert, à droite). Les liens
    /// sont des courbes qui s'affinent en s'éloignant du tronc ; les positions sont
    /// légèrement décalées (de façon stable) pour casser l'effet « grille ».
    ///
    /// Chaque nœud affiche son effet en clair (-8 %, +1, ★…). Forme : cercle = bonus en %,
    /// carré = amélioration structurelle, losange = capstone. Les nœuds débloquables
    /// pulsent. Pincer (ou les boutons +/−) pour zoomer ; l'arbre s'ouvre en entier.
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
        private static readonly Color32 Bark      = new Color32(0xB0, 0x8A, 0x3E, 255);   // tronc / racines
        private static readonly Color32 InkDark   = new Color32(0x10, 0x14, 0x1A, 255);

        private static readonly Color32 AccDepot = new Color32(0x35, 0x8E, 0xF5, 255); // bleu  — Dépôt
        private static readonly Color32 AccHr    = new Color32(0xA9, 0x70, 0xF0, 255); // violet — RH
        private static readonly Color32 AccFuel  = new Color32(0x3D, 0xC9, 0x6E, 255); // vert  — Essence
        private static readonly Color32 AccCom   = new Color32(0xF2, 0x9B, 0x38, 255); // orange — Commercial

        private const int   TitleBarH  = 56;
        private const float HubSize    = 96f;
        private const float NodeSize   = 52f;
        private const float FirstY       = 110f;   // hauteur des racines de branche au-dessus de la couronne
        private const float Step         = 86f;    // hauteur entre deux étages (compact : l'arbre doit tenir en largeur d'écran)
        private const float Slot         = 70f;    // largeur d'une colonne (une par extrémité)
        private const float BranchGapX   = 60f;    // respiration entre deux branches
        private const float MinGap       = 68f;    // écart minimal entre voisins d'un même étage
        private const float TrunkSqueeze = 0.62f;  // resserrage de la ramure près du tronc (1 = aucun)
        private const float Dome         = 60f;    // bombé de la couronne (le centre monte plus haut)
        private const float StartDist    = 90f;    // longueur du tronc sous la couronne (décor)
        private const float GraphPad     = 60f;    // marge autour de l'arbre dans la zone défilante
        private const float MinZoom    = 0.35f;
        private const float MaxZoom    = 1.6f;

        // ── Disposition ─────────────────────────────────────────────────────────────
        // true  = CERCLE : la couronne au centre, une branche par quart (Dépôt à gauche,
        //         RH en haut, Essence à droite, Commercial en bas).
        // false = ARBRE  : tronc en bas, ramure vers le haut (version précédente).
        private static readonly bool RadialLayout = true;
        private const float RingFirst    = 150f;   // rayon du premier anneau (racines de branche)
        private const float RingStep     = 78f;    // écart minimal entre deux anneaux
        private const float RingMinStep  = 62f;    // écart minimal garanti même si un anneau grandit
        private const float Stretch      = 1.55f;  // ellipse horizontale : le cercle remplit l'écran paysage

        // Ordre gauche → droite (arbre) / sens horaire depuis la gauche (cercle).
        private struct BranchDef { public SkillBranch branch; public string title; public Color32 accent; }
        private static readonly BranchDef[] Branches =
        {
            new BranchDef { branch = SkillBranch.Depot,      title = "DÉPÔT",      accent = AccDepot },
            new BranchDef { branch = SkillBranch.Hr,         title = "RH",         accent = AccHr    },
            new BranchDef { branch = SkillBranch.Fuel,       title = "ESSENCE",    accent = AccFuel  },
            new BranchDef { branch = SkillBranch.Commercial, title = "COMMERCIAL", accent = AccCom   },
        };

        private class NodeVis
        {
            public Image bg;
            public Outline outline;
            public Color32 baseOutline;
        }

        private SkillTreeSystem _skills;
        private Transform _panel;
        private ScrollRect _scroll;
        private RectTransform _graph;
        private SkillLinksGraphic _links;
        private Transform _detailHost;
        private TMP_Text _ptsTop;
        private string _selectedId;
        private Vector2 _origin;            // position de la couronne dans le repère du graphe
        private float _zoom = 1f;
        private float _pinchPrevDist;

        // Positions calculées dans le repère « couronne » (0,0 = centre de la couronne).
        private readonly Dictionary<string, NodeVis> _nodes = new Dictionary<string, NodeVis>();
        private readonly Dictionary<string, Vector2> _pos   = new Dictionary<string, Vector2>();
        private readonly Dictionary<string, int>     _depth = new Dictionary<string, int>();
        private readonly Dictionary<SkillBranch, Vector2> _labelPos = new Dictionary<SkillBranch, Vector2>();
        private readonly List<Image> _glows = new List<Image>();

        private Sprite _sprR12, _sprR8, _sprCircle, _sprSquare, _sprGlow;

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
            _sprSquare = MakeRoundedSprite(10); // carré aux coins légèrement arrondis
            _sprGlow   = MakeGlowSprite();
            BuildShell();
        }

        private void OnEnable()  => EnhancedTouchSupport.Enable();
        private void OnDisable() => EnhancedTouchSupport.Disable();

        private void Update()
        {
            // Pulsation douce des nœuds débloquables.
            float a = 0.20f + 0.16f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3f));
            foreach (var g in _glows)
                if (g != null) { var c = g.color; c.a = a; g.color = c; }

            // Bouton « retour » Android / touche Échap = Retour.
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) { Close(); return; }

            // Pincer à deux doigts → zoom.
            var touches = ETouch.activeTouches;
            if (touches.Count < 2) { _pinchPrevDist = 0f; return; }
            float dist = Vector2.Distance(touches[0].screenPosition, touches[1].screenPosition);
            if (_pinchPrevDist > 0f && dist > 0f) SetZoom(_zoom * dist / _pinchPrevDist);
            _pinchPrevDist = dist;
        }

        // ── Build shell ─────────────────────────────────────────────────────────────

        private void BuildShell()
        {
            // Écran plein, par-dessus tout (HUD compris) : l arbre a besoin de toute la place.
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 800;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            TransportManager.Core.UiScale.Configure(scaler);
            gameObject.AddComponent<GraphicRaycaster>();

            // Fond opaque bord à bord (sous l encoche aussi).
            var bg = MakeImg("Background", transform, BgPanel);
            bg.raycastTarget = true;
            FillParent(bg.GetComponent<RectTransform>());

            // Contenu dans la safe area.
            var panelGo = MakeGO("Panel", transform);
            var panelRt = panelGo.GetComponent<RectTransform>();
            Vector4 ins = SafeAreaUtil.Insets(canvas);
            panelRt.anchorMin = Vector2.zero; panelRt.anchorMax = Vector2.one;
            panelRt.offsetMin = new Vector2(ins.x, ins.w);
            panelRt.offsetMax = new Vector2(-ins.y, -ins.z);
            _panel = panelGo.transform;

            BuildTopBar();

            BuildGraphArea();
            BuildDetailCard();
            UpdateDetail(null);
        }

        // Barre du haut d un écran plein : « Retour » à gauche (pas de croix), titre à côté.
        private void BuildTopBar()
        {
            var bar = MakeGO("TopBar", _panel);
            var rt = bar.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0.5f, 1);
            rt.offsetMin = new Vector2(0, -TitleBarH); rt.offsetMax = Vector2.zero;
            var hlg = bar.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(12, 16, 6, 6);
            hlg.spacing = 16;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
            hlg.childControlWidth = true; hlg.childControlHeight = true;

            BackButton.Create(bar.transform, Close);

            var title = AddTMP("Title", bar.transform, "Compétences", 22, FontStyles.Bold, TextPri);
            title.textWrappingMode = TextWrappingModes.NoWrap;
            title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

            // Compteur de points (anciennement un bandeau dédié)
            var pts = MakeGO("Points", bar.transform);
            var pImg = pts.AddComponent<Image>();
            pImg.sprite = _sprR8; pImg.type = Image.Type.Sliced; pImg.color = BgPill;
            var pHlg = pts.AddComponent<HorizontalLayoutGroup>();
            pHlg.padding = new RectOffset(14, 14, 4, 4); pHlg.spacing = 8;
            pHlg.childAlignment = TextAnchor.MiddleCenter;
            pHlg.childForceExpandWidth = false; pHlg.childForceExpandHeight = false;
            pHlg.childControlWidth = true; pHlg.childControlHeight = true;
            pts.AddComponent<LayoutElement>().preferredHeight = 44;
            _ptsTop = AddTMP("N", pts.transform, (_skills?.AvailablePoints ?? 0).ToString(), 22, FontStyles.Bold, Gold);
            var pl = AddTMP("L", pts.transform, "points  ·  +1 / niveau", 12, FontStyles.Normal, TextSec);
            pl.textWrappingMode = TextWrappingModes.NoWrap;

            var sep = MakeImg("Sep", _panel, new Color32(0xFF, 0xFF, 0xFF, 18));
            var sRt = sep.GetComponent<RectTransform>();
            sRt.anchorMin = new Vector2(0, 1); sRt.anchorMax = new Vector2(1, 1); sRt.pivot = new Vector2(0.5f, 1);
            sRt.offsetMin = new Vector2(0, -TitleBarH - 1); sRt.offsetMax = new Vector2(0, -TitleBarH);
        }

        private void BuildGraphArea()
        {
            var scrollGo = MakeGO("Scroll", _panel);
            var scrollImg = scrollGo.AddComponent<Image>();
            scrollImg.sprite = _sprR8; scrollImg.type = Image.Type.Sliced; scrollImg.color = BgGraph;
            var srt = scrollGo.GetComponent<RectTransform>();
            srt.anchorMin = new Vector2(0, 0); srt.anchorMax = new Vector2(1, 1);
            srt.offsetMin = new Vector2(12, 12 + DetailH + 8);
            srt.offsetMax = new Vector2(-12, -(TitleBarH + 8));

            var sr = scrollGo.AddComponent<ScrollRect>();
            sr.horizontal = true; sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Elastic;
            sr.scrollSensitivity = 30f;
            _scroll = sr;

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
            grt.anchoredPosition = Vector2.zero;
            sr.content = grt;
            _graph = grt;

            BuildZoomControls(scrollGo.transform);
            RebuildGraph();
            StartCoroutine(FitAfterLayout());
        }

        // À l'ouverture : on attend que la zone ait sa taille réelle, puis on cadre l'arbre
        // entier… sauf s'il serait trop petit pour être lisible : on s'ouvre alors centré sur
        // la couronne à un zoom confortable (« Tout voir » reste dispo pour la vue d'ensemble).
        private const float OpenZoom = 0.8f;

        private IEnumerator FitAfterLayout()
        {
            yield return null;
            FitToView();
            if (_zoom < OpenZoom)
            {
                SetZoom(OpenZoom);
                _graph.anchoredPosition = -_origin * _zoom;
            }
        }

        // ── Zoom ────────────────────────────────────────────────────────────────────

        private void BuildZoomControls(Transform parent)
        {
            var bar = MakeGO("Zoom", parent);
            var rt = bar.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-10f, -10f);
            rt.sizeDelta = new Vector2(150f, 40f);
            var hlg = bar.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 6; hlg.childAlignment = TextAnchor.MiddleRight;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = true;
            hlg.childControlWidth = true; hlg.childControlHeight = true;

            ZoomButton(bar.transform, "-",  40f, () => SetZoom(_zoom / 1.25f));
            ZoomButton(bar.transform, "+",  40f, () => SetZoom(_zoom * 1.25f));
            ZoomButton(bar.transform, "Tout voir", 58f, FitToView);
        }

        private void ZoomButton(Transform parent, string label, float width, UnityEngine.Events.UnityAction onClick)
        {
            var go = MakeGO("Z_" + label, parent);
            var img = go.AddComponent<Image>();
            img.sprite = _sprR8; img.type = Image.Type.Sliced; img.color = BgPill;
            go.AddComponent<LayoutElement>().preferredWidth = width;
            var btn = go.AddComponent<Button>(); btn.targetGraphic = img; btn.onClick.AddListener(onClick);
            var t = AddTMP("L", go.transform, label, label.Length > 1 ? 11 : 20, FontStyles.Bold, TextPri);
            t.alignment = TextAlignmentOptions.Center;
            FillParent(t.rectTransform);
        }

        private void SetZoom(float z)
        {
            _zoom = Mathf.Clamp(z, MinZoom, MaxZoom);
            if (_graph != null) _graph.localScale = new Vector3(_zoom, _zoom, 1f);
        }

        private void FitToView()
        {
            if (_scroll == null || _graph == null) return;
            Vector2 vp = _scroll.viewport.rect.size;
            Vector2 g  = _graph.sizeDelta;
            if (vp.x <= 0f || vp.y <= 0f || g.x <= 0f || g.y <= 0f) return;
            SetZoom(Mathf.Min(1f, Mathf.Min(vp.x / g.x, vp.y / g.y)));
            _graph.anchoredPosition = Vector2.zero;
            _scroll.velocity = Vector2.zero;
        }

        // ── Construction du graphe ───────────────────────────────────────────────

        private void RebuildGraph()
        {
            _nodes.Clear();
            _pos.Clear();
            _depth.Clear();
            _labelPos.Clear();
            _glows.Clear();
            for (int i = _graph.childCount - 1; i >= 0; i--)
                Destroy(_graph.GetChild(i).gameObject);

            if (RadialLayout) ComputeRadialLayout(); else ComputeLayout();
            FitGraphToTree();

            // 1) Toutes les branches (courbes) en un seul maillage, derrière le reste.
            var linksGo = MakeGO("Links", _graph);
            FillParent(linksGo.GetComponent<RectTransform>());
            linksGo.AddComponent<CanvasRenderer>();   // indispensable à tout Graphic personnalisé
            _links = linksGo.AddComponent<SkillLinksGraphic>();
            _links.raycastTarget = false;

            if (!RadialLayout) BuildTrunk();
            foreach (var b in Branches)
                foreach (var node in SkillTreeCatalog.InBranch(b.branch))
                    AddLinksFor(node, b.accent);

            // 2) Couronne (points) + noms des branches.
            BuildHub();
            foreach (var b in Branches) BuildBranchLabel(b);

            // 3) Nœuds (devant).
            foreach (var b in Branches)
                foreach (var node in SkillTreeCatalog.InBranch(b.branch))
                    if (_pos.ContainsKey(node.id))
                        BuildNode(node, G(_pos[node.id]), b.accent);

            if (!string.IsNullOrEmpty(_selectedId) && _nodes.ContainsKey(_selectedId))
                Highlight(_selectedId, true);
        }

        // Repère « couronne » → repère du graphe.
        private Vector2 G(Vector2 p) => p + _origin;

        // Tronc + racines (décor) : sous la couronne, en bas de la zone.
        private Vector2 TrunkBase => new Vector2(0f, -StartDist * 1.4f);

        // Dispose la ramure en arbre qui pousse vers le haut :
        //  1) chaque extrémité reçoit sa propre colonne (Slot) → aucun chevauchement possible,
        //     un parent est centré au-dessus de ses enfants ; les 3 branches côte à côte ;
        //  2) la ramure est resserrée près du tronc et s'évase vers le haut, sans jamais
        //     rapprocher deux nœuds d'un même étage à moins de MinGap ;
        //  3) le centre est bombé (couronne) et chaque nœud est légèrement décalé, de façon
        //     stable, pour casser l'effet « grille ».
        private void ComputeLayout()
        {
            var rawX = new Dictionary<string, float>();
            var branchNodes = new Dictionary<SkillBranch, List<string>>();
            float cursor = 0f;
            int maxDepth = 1;

            foreach (var b in Branches)
            {
                var children = new Dictionary<string, List<SkillNodeDefinition>>();
                var roots = new List<SkillNodeDefinition>();
                // Les nœuds de convergence (plusieurs parents) sont placés à part.
                foreach (var n in SkillTreeCatalog.InBranch(b.branch).Where(x => !x.IsConvergence))
                {
                    if (n.IsBranchRoot) { roots.Add(n); continue; }
                    if (!children.TryGetValue(n.prerequisiteId, out var l))
                        children[n.prerequisiteId] = l = new List<SkillNodeDefinition>();
                    l.Add(n);
                }
                var ids = new List<string>();
                foreach (var r in roots) PlaceTidy(r.id, 1, children, rawX, ids, ref cursor, ref maxDepth);
                branchNodes[b.branch] = ids;
                cursor += BranchGapX;
            }
            if (rawX.Count == 0) return;

            // Recentre sur 0 (la couronne est au milieu).
            float minRaw = rawX.Values.Min(), maxRaw = rawX.Values.Max();
            float shift = (minRaw + maxRaw) * 0.5f, halfW = Mathf.Max(1f, (maxRaw - minRaw) * 0.5f);
            foreach (var k in rawX.Keys.ToList()) rawX[k] -= shift;

            // Écart minimal entre deux nœuds voisins d'un même étage (avant resserrage).
            var minGap = new Dictionary<int, float>();
            foreach (var g in rawX.GroupBy(kv => _depth[kv.Key]))
            {
                var xs = g.Select(kv => kv.Value).OrderBy(v => v).ToList();
                float gap = float.MaxValue;
                for (int i = 1; i < xs.Count; i++) gap = Mathf.Min(gap, xs[i] - xs[i - 1]);
                minGap[g.Key] = gap;
            }

            foreach (var kv in rawX)
            {
                int d = _depth[kv.Key];
                float t = maxDepth > 1 ? (d - 1f) / (maxDepth - 1f) : 1f;
                float squeeze = Mathf.Lerp(TrunkSqueeze, 1f, t);
                if (minGap[d] < float.MaxValue) squeeze = Mathf.Max(squeeze, MinGap / minGap[d]);
                float u = kv.Value / halfW;   // -1 (gauche) … 1 (droite)
                float x = kv.Value * squeeze + (Hash01(kv.Key, 1) - 0.5f) * 10f;
                float y = FirstY + (d - 1) * Step + Dome * t * (1f - u * u) + (Hash01(kv.Key, 2) - 0.5f) * 20f;
                _pos[kv.Key] = new Vector2(x, y);
            }

            // Convergence = le « fruit » au sommet de la branche, relié à toutes ses extrémités.
            foreach (var b in Branches)
            {
                var ids = branchNodes[b.branch];
                if (ids.Count == 0) continue;
                float top = ids.Max(id => _pos[id].y);
                float cx  = ids.Average(id => _pos[id].x);
                foreach (var cn in SkillTreeCatalog.InBranch(b.branch).Where(x => x.IsConvergence))
                {
                    var linked = cn.AllPrerequisiteIds.Where(_pos.ContainsKey).ToList();
                    float fx = linked.Count > 0 ? linked.Average(id => _pos[id].x) : cx;
                    top += Step * 0.9f;
                    _pos[cn.id]   = new Vector2(fx, top);
                    _depth[cn.id] = maxDepth + 1;
                }
                _labelPos[b.branch] = new Vector2(cx, top + 64f);
            }
        }

        // Disposition en CERCLE : même placement « une colonne par extrémité », mais les
        // colonnes deviennent des angles autour de la couronne (sens horaire, RH centrée en
        // haut). Le rayon de chaque anneau grandit autant que nécessaire pour que deux
        // voisins ne se touchent jamais ; l'ensemble est étiré en ellipse pour l'écran paysage.
        private void ComputeRadialLayout()
        {
            var slot = new Dictionary<string, float>();
            var branchNodes = new Dictionary<SkillBranch, List<string>>();
            float cursor = 0f;
            int maxDepth = 1;

            foreach (var b in Branches)
            {
                var children = new Dictionary<string, List<SkillNodeDefinition>>();
                var roots = new List<SkillNodeDefinition>();
                foreach (var n in SkillTreeCatalog.InBranch(b.branch).Where(x => !x.IsConvergence))
                {
                    if (n.IsBranchRoot) { roots.Add(n); continue; }
                    if (!children.TryGetValue(n.prerequisiteId, out var l))
                        children[n.prerequisiteId] = l = new List<SkillNodeDefinition>();
                    l.Add(n);
                }
                var ids = new List<string>();
                foreach (var r in roots) PlaceTidy(r.id, 1, children, slot, ids, ref cursor, ref maxDepth);
                branchNodes[b.branch] = ids;
                cursor += Slot * 1.2f;   // respiration entre deux branches
            }
            if (slot.Count == 0) return;

            // Colonne → angle (sens horaire), puis rotation pour centrer RH en haut (90°).
            float total = cursor;
            var ang = new Dictionary<string, float>();
            foreach (var kv in slot) ang[kv.Key] = -kv.Value / total * 360f;
            if (branchNodes.TryGetValue(SkillBranch.Hr, out var hrIds) && hrIds.Count > 0)
            {
                float hrMid = (hrIds.Max(i => ang[i]) + hrIds.Min(i => ang[i])) * 0.5f;
                foreach (var k in ang.Keys.ToList()) ang[k] += 90f - hrMid;
            }

            // Rayon de chaque anneau : assez grand pour respecter MinGap entre deux voisins.
            var radius = new Dictionary<int, float>();
            for (int d = 1; d <= maxDepth; d++)
            {
                int depth = d;
                var angles = ang.Where(kv => _depth[kv.Key] == depth).Select(kv => kv.Value).OrderBy(v => v).ToList();
                float gap = float.MaxValue;
                for (int i = 1; i < angles.Count; i++) gap = Mathf.Min(gap, angles[i] - angles[i - 1]);
                if (angles.Count > 1) gap = Mathf.Min(gap, 360f - (angles[angles.Count - 1] - angles[0]));
                float r = RingFirst + (d - 1) * RingStep;
                if (gap < float.MaxValue && gap > 0f) r = Mathf.Max(r, MinGap / (gap * Mathf.Deg2Rad));
                if (d > 1) r = Mathf.Max(r, radius[d - 1] + RingMinStep);
                radius[d] = r;
            }

            foreach (var kv in ang)
            {
                float a = kv.Value + (Hash01(kv.Key, 1) - 0.5f) * 1.5f;
                float r = radius[_depth[kv.Key]] + (Hash01(kv.Key, 2) - 0.5f) * 14f;
                _pos[kv.Key] = Ellipse(a, r);
            }

            // Récompense ultime + nom de branche, à l'extérieur, dans l'axe de la branche.
            foreach (var b in Branches)
            {
                if (!branchNodes.TryGetValue(b.branch, out var ids) || ids.Count == 0) continue;
                float mid  = (ids.Max(i => ang[i]) + ids.Min(i => ang[i])) * 0.5f;
                float outR = ids.Max(i => radius[_depth[i]]);
                foreach (var cn in SkillTreeCatalog.InBranch(b.branch).Where(x => x.IsConvergence))
                {
                    outR += 80f;
                    _pos[cn.id]   = Ellipse(mid, outR);
                    _depth[cn.id] = maxDepth + 1;
                }
                _labelPos[b.branch] = Ellipse(mid, outR + 70f);
            }
        }

        private static Vector2 Ellipse(float deg, float r)
        {
            float rad = deg * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(rad) * r * Stretch, Mathf.Sin(rad) * r);
        }

        private static Vector2 Dir(Vector2 v) => v.sqrMagnitude > 1e-4f ? v.normalized : Vector2.up;

        // Placement « en colonnes » : une colonne par extrémité, parent centré sur ses enfants.
        private void PlaceTidy(string id, int depth, Dictionary<string, List<SkillNodeDefinition>> children,
            Dictionary<string, float> rawX, List<string> ids, ref float cursor, ref int maxDepth)
        {
            _depth[id] = depth;
            ids.Add(id);
            if (depth > maxDepth) maxDepth = depth;

            if (!children.TryGetValue(id, out var kids) || kids.Count == 0)
            {
                rawX[id] = cursor;
                cursor += Slot;
                return;
            }
            foreach (var c in kids) PlaceTidy(c.id, depth + 1, children, rawX, ids, ref cursor, ref maxDepth);
            rawX[id] = (rawX[kids[0].id] + rawX[kids[kids.Count - 1].id]) * 0.5f;
        }

        // Dimensionne la zone défilante sur l'arbre réel (+ marge) et place la couronne.
        private void FitGraphToTree()
        {
            float bottom = RadialLayout ? -HubSize : TrunkBase.y - 70f;
            Vector2 min = new Vector2(-HubSize - 110f, bottom), max = new Vector2(HubSize + 110f, HubSize);
            foreach (var p in _pos.Values) { min = Vector2.Min(min, p); max = Vector2.Max(max, p); }
            foreach (var p in _labelPos.Values)
            {
                min = Vector2.Min(min, p - new Vector2(110f, 20f));
                max = Vector2.Max(max, p + new Vector2(110f, 20f));
            }
            min -= Vector2.one * GraphPad;
            max += Vector2.one * GraphPad;
            _graph.sizeDelta = max - min;
            _origin = -(min + max) * 0.5f;   // centre de l'arbre = centre de la zone
        }

        // Pseudo-aléatoire STABLE (même rendu à chaque ouverture, sur toutes les plateformes).
        private static float Hash01(string s, int salt)
        {
            unchecked
            {
                uint h = 2166136261u ^ (uint)(salt * 16777619);
                foreach (char ch in s) { h ^= ch; h *= 16777619u; }
                return (h & 0xFFFF) / 65535f;
            }
        }

        // ── Branches (courbes) ──────────────────────────────────────────────────────

        // Épaisseur d'une branche selon sa profondeur : grosse près du tronc, fine au bout.
        private static float Thickness(int depth) => Mathf.Max(3.5f, 15f - depth * 2f);

        private void AddLinksFor(SkillNodeDefinition node, Color32 accent)
        {
            if (!_pos.TryGetValue(node.id, out var to)) return;
            int depth = _depth[node.id];

            // Convergence : un fin filament vers CHAQUE extrémité, allumé dès qu'elle est obtenue.
            if (node.IsConvergence)
            {
                foreach (var pid in node.AllPrerequisiteIds)
                {
                    if (!_pos.TryGetValue(pid, out var from)) continue;
                    bool done = _skills != null && _skills.IsUnlocked(pid);
                    Color32 fc = Fade(accent, (byte)(done ? 200 : 45));
                    if (RadialLayout)
                    {
                        float dd = Vector2.Distance(from, to);
                        AddCurve(from, from + Dir(from) * dd * 0.40f, to - Dir(to) * dd * 0.35f, to, 2.5f, 2f, fc, done);
                        continue;
                    }
                    float dyc = Mathf.Max(40f, to.y - from.y);
                    AddCurve(from, from + new Vector2(0f, dyc * 0.6f), to - new Vector2(0f, dyc * 0.45f), to,
                             2.5f, 2f, fc, done);
                }
                return;
            }

            bool isRoot = node.IsBranchRoot || !_pos.ContainsKey(node.prerequisiteId);
            int parentDepth = isRoot ? 0 : _depth[node.prerequisiteId];
            Vector2 a, ca, cb;
            if (RadialLayout)
            {
                // Lien rayonnant : part dans l'axe du parent (ou de la couronne), arrive dans celui de l'enfant.
                a = isRoot ? Dir(to) * HubSize * 0.45f : _pos[node.prerequisiteId];
                float dist = Vector2.Distance(a, to);
                ca = a + Dir(isRoot ? to : a) * dist * 0.42f;
                cb = to - Dir(to) * dist * 0.40f;
            }
            else
            {
                // La branche monte depuis le parent puis s'incline vers l'enfant (courbe en S douce).
                a = isRoot ? new Vector2(0f, HubSize * 0.35f) : _pos[node.prerequisiteId];
                float dy = Mathf.Max(40f, to.y - a.y);
                ca = a + new Vector2(0f, dy * (isRoot ? 0.65f : 0.55f));
                cb = to - new Vector2(0f, dy * 0.50f);
            }

            bool reached = _skills != null && _skills.IsUnlocked(node.id);
            float wa = isRoot ? 18f : Thickness(parentDepth);
            float wb = Thickness(depth);
            AddCurve(a, ca, cb, to, wa, wb, Fade(accent, (byte)(reached ? 255 : 70)), reached);
        }

        // halo = lueur discrète autour des branches débloquées.
        private void AddCurve(Vector2 a, Vector2 ca, Vector2 cb, Vector2 b, float wa, float wb, Color32 color, bool halo)
        {
            if (halo)
                _links.Add(new SkillLinksGraphic.Curve
                {
                    a = G(a), ca = G(ca), cb = G(cb), b = G(b),
                    widthA = wa + 9f, widthB = wb + 7f, color = Fade(color, 55)
                });
            _links.Add(new SkillLinksGraphic.Curve
            {
                a = G(a), ca = G(ca), cb = G(cb), b = G(b), widthA = wa, widthB = wb, color = color
            });
        }

        private void BuildTrunk()
        {
            Vector2 b0 = TrunkBase, top = new Vector2(0f, -HubSize * 0.3f);
            float d = top.y - b0.y;
            // Tronc légèrement en S.
            AddCurve(b0, b0 + new Vector2(16f, d * 0.38f), top + new Vector2(-12f, -d * 0.38f), top,
                     36f, 24f, Bark, true);
            // Racines.
            Color32 root = Fade(Bark, 150);
            AddCurve(b0, b0 + new Vector2(-30f, -8f), b0 + new Vector2(-70f, -22f), b0 + new Vector2(-104f, -48f), 13f, 2.5f, root, false);
            AddCurve(b0, b0 + new Vector2(28f, -10f), b0 + new Vector2(64f, -26f), b0 + new Vector2(96f, -58f), 12f, 2.5f, root, false);
            AddCurve(b0, b0 + new Vector2(-4f, -22f), b0 + new Vector2(-14f, -44f), b0 + new Vector2(-18f, -66f), 10f, 2f, root, false);
        }

        private static Color32 Fade(Color32 c, byte alpha) => new Color32(c.r, c.g, c.b, alpha);

        // ── Couronne, libellés, nœuds ───────────────────────────────────────────────

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
            rt.anchoredPosition = G(Vector2.zero);
            rt.sizeDelta = new Vector2(HubSize, HubSize);

            int pts = _skills?.AvailablePoints ?? 0;
            var label = AddTMP("Pts", go.transform, pts.ToString(), 34, FontStyles.Bold, Gold);
            label.alignment = TextAlignmentOptions.Center;
            var lr = label.GetComponent<RectTransform>();
            lr.anchorMin = new Vector2(0, 0.32f); lr.anchorMax = new Vector2(1, 1);
            lr.offsetMin = Vector2.zero; lr.offsetMax = Vector2.zero;

            var sub = AddTMP("Sub", go.transform, "POINTS", 10, FontStyles.Bold, TextSec);
            sub.alignment = TextAlignmentOptions.Center;
            sub.characterSpacing = 3;
            var sr = sub.GetComponent<RectTransform>();
            sr.anchorMin = new Vector2(0, 0.05f); sr.anchorMax = new Vector2(1, 0.34f);
            sr.offsetMin = Vector2.zero; sr.offsetMax = Vector2.zero;
        }

        private void BuildBranchLabel(BranchDef b)
        {
            if (!_labelPos.TryGetValue(b.branch, out var p)) return;
            int total = 0, done = 0;
            foreach (var n in SkillTreeCatalog.InBranch(b.branch))
            {
                total++;
                if (_skills != null && _skills.IsUnlocked(n.id)) done++;
            }
            var t = AddTMP("Branch_" + b.title, _graph, $"{b.title}\n<size=60%><color=#7A8FA6>{done} / {total}</color></size>",
                           24, FontStyles.Bold, Fade(b.accent, 230));
            t.alignment = TextAlignmentOptions.Center;
            t.characterSpacing = 6;
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(220f, 60f);
            rt.anchoredPosition = G(p);
        }

        private void BuildNode(SkillNodeDefinition node, Vector2 pos, Color32 accent)
        {
            bool unlocked  = _skills != null && _skills.IsUnlocked(node.id);
            bool canUnlock = _skills != null && _skills.CanUnlock(node.id, out _);

            bool diamond = node.shape == NodeShape.Diamond;
            float size   = diamond ? NodeSize * 1.12f : NodeSize;

            // Halo pulsant derrière les nœuds qu'on peut débloquer maintenant.
            if (canUnlock && !unlocked)
            {
                var glow = MakeImg("Glow_" + node.id, _graph, Fade(accent, 60));
                glow.sprite = _sprGlow;
                glow.raycastTarget = false;
                var grt = glow.rectTransform;
                grt.anchorMin = grt.anchorMax = grt.pivot = new Vector2(0.5f, 0.5f);
                grt.anchoredPosition = pos;
                grt.sizeDelta = new Vector2(size * 2.1f, size * 2.1f);
                _glows.Add(glow);
            }

            var go = MakeGO("N_" + node.id, _graph);
            var img = go.AddComponent<Image>();
            img.sprite = node.shape == NodeShape.Circle ? _sprCircle : _sprSquare;
            img.type = Image.Type.Simple;
            img.color = unlocked ? (Color)accent : (canUnlock ? (Color32)BgCard : BgPill);

            var ol = go.AddComponent<Outline>();
            Color32 baseOutline = unlocked ? accent : (canUnlock ? accent : DivColor);
            ol.effectColor = baseOutline;
            ol.effectDistance = new Vector2(canUnlock && !unlocked ? 2f : 1.2f, canUnlock && !unlocked ? -2f : -1.2f);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(size, size);
            if (diamond) rt.localRotation = Quaternion.Euler(0, 0, 45f); // carré pivoté → losange

            // L'effet du nœud, lisible d'un coup d'œil (au lieu d'un simple numéro de profondeur).
            Color32 txtColor = unlocked ? InkDark : (canUnlock ? accent : TextMuted);
            var badge = AddTMP("T", go.transform, Badge(node), 15, FontStyles.Bold, txtColor);
            badge.alignment = TextAlignmentOptions.Center;
            badge.enableAutoSizing = true;
            badge.fontSizeMin = 9; badge.fontSizeMax = 15;
            var brt = badge.rectTransform;
            FillParent(brt);
            brt.offsetMin = new Vector2(3f, 0f); brt.offsetMax = new Vector2(-3f, 0f);
            if (diamond) brt.localRotation = Quaternion.Euler(0, 0, -45f); // garde le texte droit

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            string id = node.id;
            btn.onClick.AddListener(() => OnNodeTap(id));

            _nodes[node.id] = new NodeVis { bg = img, outline = ol, baseOutline = baseOutline };
        }

        // Résumé court de l'effet : « -8% », « +20% », « +1 », « -12h », « +7j », « ★ ».
        private static string Badge(SkillNodeDefinition n)
        {
            switch (n.effect)
            {
                // Réductions en %.
                case SkillEffectType.DepotUpgradeCostReduction:
                case SkillEffectType.RepairCostReduction:
                case SkillEffectType.DriverWageReduction:
                case SkillEffectType.FatigueReduction:
                case SkillEffectType.FuelPriceReduction:
                case SkillEffectType.FuelConsumptionReduction:
                case SkillEffectType.ReputationLossReduction:
                case SkillEffectType.AccidentChanceReduction:
                case SkillEffectType.FatalAccidentReduction:
                case SkillEffectType.VehiclePriceReduction:
                case SkillEffectType.WearReduction:
                    return $"-{Mathf.RoundToInt(n.magnitude * 100f)}%";
                // Bonus en %.
                case SkillEffectType.ContractRewardBonus:
                case SkillEffectType.DriverXpGainBonus:
                case SkillEffectType.StationCapacityBonus:
                case SkillEffectType.RefillSpeedBonus:
                case SkillEffectType.TripSpeedBonus:
                case SkillEffectType.DeadlineTimeBonus:
                case SkillEffectType.OnTimeBonusIncrease:
                case SkillEffectType.ReputationGainBonus:
                case SkillEffectType.LiveEventBonus:
                case SkillEffectType.DailyMissionRewardBonus:
                case SkillEffectType.EnergyDrinkBoost:
                case SkillEffectType.VehicleTankBonus:
                case SkillEffectType.MaintenanceThresholdBonus:
                    return $"+{Mathf.RoundToInt(n.magnitude * 100f)}%";
                // Valeurs fixes.
                case SkillEffectType.ExtraVehicleSlots:
                case SkillEffectType.RecruitmentLevelBonus:
                case SkillEffectType.RecruitmentPoolSizeBonus:
                case SkillEffectType.ExtraTourStops:
                case SkillEffectType.ExtraContractChoices:
                case SkillEffectType.ContractCountryReach:
                    return n.shape == NodeShape.Diamond && n.effect == SkillEffectType.ContractCountryReach
                        ? "★" : $"+{Mathf.RoundToInt(n.magnitude)}";
                case SkillEffectType.HrRefreshHoursReduction:
                    return $"-{Mathf.RoundToInt(n.magnitude)}h";
                case SkillEffectType.FuelMarketHistoryDays:
                {
                    // Total d'historique atteint avec ce nœud (7j, 30j, 60j, 90j), comme dans sa description,
                    // plutôt que l'incrément brut (+23j) qui n'a pas de sens pour le joueur.
                    float days = 0f;
                    for (var c = n; c != null && c.effect == n.effect; c = SkillTreeCatalog.GetById(c.prerequisiteId))
                        days += c.magnitude;
                    return $"{Mathf.RoundToInt(days)}j";
                }
                // Déblocages (capstones).
                default:
                    return "★";
            }
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
            rt.offsetMax = new Vector2(-12, 12 + DetailH);
            _detailHost = host.transform;
        }

        private const float DetailH = 76f;   // fiche compacte : l'arbre garde la place

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
            bool prereqMet = true;
            foreach (var pid in node.AllPrerequisiteIds)
                if (!_skills.IsUnlocked(pid)) { prereqMet = false; break; }
            bool canUnlock = _skills.CanUnlock(id, out _);
            Color32 accent = AccentFor(node.branch);

            var hlg = _detailHost.gameObject.GetComponent<HorizontalLayoutGroup>() ?? _detailHost.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(16, 12, 8, 8);
            hlg.spacing = 16; hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            var row = _detailHost;

            // Titre + description (une ligne chacun)
            var col = MakeGO("Info", row);
            var cv = col.AddComponent<VerticalLayoutGroup>();
            cv.spacing = 2; cv.childAlignment = TextAnchor.MiddleLeft;
            cv.childForceExpandWidth = true; cv.childForceExpandHeight = false;
            cv.childControlWidth = true; cv.childControlHeight = true;
            col.AddComponent<LayoutElement>().flexibleWidth = 1;
            var title = AddTMP("Title", col.transform, node.title, 16, FontStyles.Bold, unlocked ? accent : TextPri);
            title.textWrappingMode = TextWrappingModes.NoWrap; title.overflowMode = TextOverflowModes.Ellipsis;
            var desc = AddTMP("Desc", col.transform, node.description, 12, FontStyles.Normal, TextSec);
            desc.textWrappingMode = TextWrappingModes.NoWrap; desc.overflowMode = TextOverflowModes.Ellipsis;

            string status;
            Color32 statusColor;
            if (unlocked)        { status = "Débloqué";                 statusColor = accent; }
            else if (canUnlock)  { status = $"Coût : {node.cost} pt";     statusColor = TextPri; }
            else if (!prereqMet)
            {
                if (node.IsConvergence)
                {
                    int total = 0, done = 0;
                    foreach (var pid in node.AllPrerequisiteIds) { total++; if (_skills.IsUnlocked(pid)) done++; }
                    status = $"Extrémités requises : {done}/{total}";
                }
                else status = "Débloque le nœud précédent";
                statusColor = TextMuted;
            }
            else                 { status = $"{node.cost} pt · points insuffisants"; statusColor = TextMuted; }

            var st = AddTMP("Status", row, status, 13, FontStyles.Bold, statusColor);
            st.alignment = TextAlignmentOptions.MidlineRight;
            st.textWrappingMode = TextWrappingModes.NoWrap;

            if (!unlocked)
            {
                var btnGo = MakeGO("Unlock", row);
                var bImg = btnGo.AddComponent<Image>();
                bImg.sprite = _sprR8; bImg.type = Image.Type.Sliced;
                bImg.color = canUnlock ? (Color32)accent : new Color32(0x3A, 0x3F, 0x4A, 255);
                var le = btnGo.AddComponent<LayoutElement>();
                le.preferredWidth = 168; le.preferredHeight = 44; le.minWidth = 168;
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
            // On garde le cadrage et le zoom courants : seul le contenu change.
            Vector2 keepPos = _graph.anchoredPosition;
            RebuildGraph();
            _graph.anchoredPosition = keepPos;
            if (_ptsTop != null) _ptsTop.text = _skills.AvailablePoints.ToString();
            UpdateDetail(id);
            GameManager.Instance?.SaveNow();
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static Color32 AccentFor(SkillBranch b)
        {
            switch (b)
            {
                case SkillBranch.Depot: return AccDepot;
                case SkillBranch.Hr:    return AccHr;
                case SkillBranch.Commercial: return AccCom;
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

        // Disque au bord très doux (halo des nœuds débloquables).
        private static Sprite MakeGlowSprite()
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode   = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color[size * size];
            float c = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;   // 0 centre → 1 bord
                    float a = Mathf.Clamp01(1f - d);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a * a);
                }
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
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
