using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using TransportManager.Systems.Buildings;

namespace TransportManager.UI.Depot
{
    /// <summary>
    /// Le dépôt en 3D « low poly », construit entièrement en code (aucune image) :
    /// pelouse, cour en enrobé avec parkings, route en bas à droite, et 3 emplacements
    /// (garage, station essence, bâtiment RH). Une caméra orthographique en vue isométrique
    /// le rend dans une RenderTexture affichée par l'onglet Dépôt.
    ///
    /// Repère (mètres) : sol = plan XZ, y = hauteur. Caméra tournée de 45° :
    /// +x+z = fond de l'écran, -z = bas-droite (côté route), -x = bas-gauche.
    /// </summary>
    public sealed class DepotScene3D
    {
        public const int Layer = 30;                                   // calque réservé au dépôt
        private static readonly Vector3 Origin = new Vector3(0f, -2000f, 0f);   // loin de tout le reste
        private static readonly Quaternion CamRotation = Quaternion.Euler(30f, 45f, 0f);

        // ── Plan du terrain ─────────────────────────────────────────────────────────────
        // Allées de 8 m en croix (centrées sur 0) ; chaque emplacement commence à 1 m de l'allée.
        private const float LaneCenter = 0f, LaneHalf = 4f;
        private const float LotMin = -22f, LotMax = 26f;   // cour en enrobé (carré)
        // Emplacements (dalle béton) : le garage a le plus de niveaux → la plus grande dalle.
        private const float HangarSize = 18f, FuelSize = 14f, OfficeSize = 10f;
        private static readonly Vector2 HangarPlot = new Vector2(14f, 14f);    // au fond   : x,z ∈ [5, 23]
        private static readonly Vector2 FuelPlot   = new Vector2(12f, -12f);   // à droite  : x ∈ [5, 19], z ∈ [-19, -5]
        private static readonly Vector2 OfficePlot = new Vector2(-10f, 10f);   // à gauche  : x ∈ [-15, -5], z ∈ [5, 15]
        private const float RoadZ = -27.5f, RoadHalf = 4f;   // route (bas-droite de l'écran)

        private static Vector2 PlotOf(string b) => b == BuildingVisuals.Hangar ? HangarPlot : b == BuildingVisuals.FuelTank ? FuelPlot : OfficePlot;
        private static float SizeOf(string b) => b == BuildingVisuals.Hangar ? HangarSize : b == BuildingVisuals.FuelTank ? FuelSize : OfficeSize;

        // Les designs sont dessinés pour une dalle de 14 m : le garage grandit jusqu'à remplir
        // sa dalle de 18 m au dernier niveau, le RH est réduit pour tenir dans 10 m.
        private static float DesignScale(string b, int design)
        {
            if (design <= 0) return b == BuildingVisuals.Office ? 0.8f : 1f;
            if (b == BuildingVisuals.Hangar) return Mathf.Lerp(1f, 1.25f, (design - 1) / 5f);
            if (b == BuildingVisuals.Office) return 0.82f;
            return 1f;
        }

        // Nombre de designs dessinés par bâtiment (niveau 1 = petit, dernier = remplit sa dalle).
        private static int MaxDesign(string b) => b == BuildingVisuals.FuelTank ? 10 : b == BuildingVisuals.Office ? 3 : 6;

        // ── Palette ─────────────────────────────────────────────────────────────────────
        private static readonly Color Grass     = new Color(0.45f, 0.62f, 0.33f);
        private static readonly Color GrassDark = new Color(0.36f, 0.53f, 0.28f);
        private static readonly Color Asphalt   = new Color(0.30f, 0.32f, 0.35f);
        private static readonly Color RoadCol   = new Color(0.24f, 0.25f, 0.28f);
        private static readonly Color Concrete  = new Color(0.72f, 0.72f, 0.70f);
        private static readonly Color Curb      = new Color(0.84f, 0.84f, 0.82f);
        private static readonly Color Paint     = new Color(0.93f, 0.93f, 0.90f);
        private static readonly Color PaintYel  = new Color(0.96f, 0.78f, 0.22f);
        private static readonly Color Wall      = new Color(0.92f, 0.91f, 0.88f);
        private static readonly Color Cladding  = new Color(0.78f, 0.80f, 0.84f);
        private static readonly Color RoofCol   = new Color(0.50f, 0.52f, 0.56f);
        private static readonly Color Metal     = new Color(0.62f, 0.64f, 0.67f);
        private static readonly Color DarkMetal = new Color(0.38f, 0.41f, 0.46f);
        private static readonly Color Glass     = new Color(0.46f, 0.63f, 0.78f);
        private static readonly Color GlassDark = new Color(0.20f, 0.28f, 0.36f);
        private static readonly Color White     = new Color(0.95f, 0.95f, 0.94f);
        private static readonly Color Trunk     = new Color(0.45f, 0.33f, 0.23f);
        private static readonly Color Orange    = new Color(0.95f, 0.52f, 0.16f);
        // Une couleur d'accent par bâtiment, la même que ses boutons dans l'interface.
        private static readonly Color AccentHangar = new Color(0.29f, 0.56f, 0.89f);
        private static readonly Color AccentFuel   = new Color(0.20f, 0.65f, 0.45f);
        private static readonly Color AccentOffice = new Color(0.75f, 0.40f, 0.80f);

        public Camera Camera { get; }
        private readonly GameObject _root;
        private readonly Light _light;
        private Material _template;
        private readonly Dictionary<(Color, float), Material> _mats = new Dictionary<(Color, float), Material>();

        private class Slot
        {
            public Transform root;      // posé sur la dalle de l'emplacement
            public Transform model;     // le bâtiment (reconstruit si ruine ↔ bâti)
            public int level = -1;
            public bool ruin;
            public float scale = 1f;
            public float pop = -1f;     // < 0 : pas d'animation en cours
        }
        private readonly Dictionary<string, Slot> _slots = new Dictionary<string, Slot>();

        public DepotScene3D()
        {
            _root = new GameObject("Depot3D");
            _root.transform.position = Origin;
            _root.layer = Layer;

            // Lumière venant du haut-gauche de l'écran → ombres vers le bas-droite.
            var lightGo = new GameObject("DepotSun");
            lightGo.transform.SetParent(_root.transform, false);
            lightGo.transform.rotation = Quaternion.Euler(50f, 157f, 0f);
            _light = lightGo.AddComponent<Light>();
            _light.type = LightType.Directional;
            _light.color = new Color(1f, 0.96f, 0.90f);
            _light.intensity = 1.15f;
            _light.shadows = LightShadows.Soft;
            _light.shadowStrength = 0.55f;
            _light.cullingMask = 1 << Layer;

            // Lumière ambiante douce (sinon les faces à l'ombre sont noires).
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.52f, 0.55f, 0.60f);
            var sh = new SphericalHarmonicsL2();
            sh.AddAmbientLight(RenderSettings.ambientLight);
            RenderSettings.ambientProbe = sh;

            var camGo = new GameObject("DepotCamera");
            camGo.transform.SetParent(_root.transform, false);
            Camera = camGo.AddComponent<Camera>();
            Camera.orthographic = true;
            Camera.cullingMask = 1 << Layer;
            Camera.clearFlags = CameraClearFlags.SolidColor;
            Camera.backgroundColor = Grass;
            Camera.nearClipPlane = 1f;
            Camera.farClipPlane = 300f;
            Camera.depth = -10;

            BuildGround();
            foreach (var b in new[] { BuildingVisuals.Hangar, BuildingVisuals.FuelTank, BuildingVisuals.Office })
                _slots[b] = MakeSlot(PlotOf(b), SizeOf(b));
            Frame(16f / 9f, new Rect(0f, 0f, 1f, 1f));
        }

        public void SetActive(bool on)
        {
            if (_root != null) _root.SetActive(on);
        }

        public void Dispose()
        {
            if (_root != null) Object.Destroy(_root);
            foreach (var m in _mats.Values) if (m != null) Object.Destroy(m);
            _mats.Clear();
        }

        // ── Niveaux ─────────────────────────────────────────────────────────────────────

        /// <summary>Affiche le bâtiment au niveau visuel donné (0 = chantier / ruine).</summary>
        public void SetLevel(string building, int level, bool animate)
        {
            if (!_slots.TryGetValue(building, out var s)) return;
            // Un design par niveau ; au-delà du dernier dessiné, on garde le plus abouti.
            int design = level <= 0 ? 0 : Mathf.Min(level, MaxDesign(building));
            if (s.model != null && s.level == design) return;

            if (s.model != null) Object.Destroy(s.model.gameObject);
            s.model = new GameObject("Model").transform;
            s.model.SetParent(s.root, false);
            SetLayer(s.model.gameObject);
            if (design == 0) BuildRuin(s.model);
            else if (building == BuildingVisuals.Hangar) BuildHangar(s.model, design);
            else if (building == BuildingVisuals.FuelTank) BuildFuelStation(s.model, design);
            else BuildOffice(s.model, design);

            s.ruin = design == 0;
            s.level = design;
            s.scale = DesignScale(building, design);
            s.pop = animate ? 0f : -1f;
            ApplyScale(s);
        }

        public void Tick(float dt)
        {
            foreach (var s in _slots.Values)
            {
                if (s.pop < 0f) continue;
                s.pop += dt / 0.4f;
                if (s.pop >= 1f) s.pop = -1f;
                ApplyScale(s);
            }
        }

        private static void ApplyScale(Slot s)
        {
            if (s.model == null) return;
            float k = 1f;
            if (s.pop >= 0f)   // 0.85 → 1.08 → 1.00
                k = s.pop < 0.6f ? Mathf.Lerp(0.85f, 1.08f, s.pop / 0.6f) : Mathf.Lerp(1.08f, 1f, (s.pop - 0.6f) / 0.4f);
            float v = s.scale * k;
            s.model.localScale = new Vector3(v, v, v);
        }

        /// <summary>Point du monde où accrocher le panneau d'info : coin avant de l'emplacement.</summary>
        public Vector3 PanelAnchor(string building)
        {
            Vector2 p = PlotOf(building);
            float h = SizeOf(building) * 0.5f;
            return Origin + new Vector3(p.x - h, 0.2f, p.y - h);
        }

        // ── Caméra ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Cadre la cour dans la partie libre de l'écran (<paramref name="free"/>, en fraction
        /// du rendu : hors barre du haut et rail de gauche). Le reste de l'image montre la pelouse.
        /// </summary>
        public void Frame(float aspect, Rect free)
        {
            if (aspect <= 0f || free.width <= 0f || free.height <= 0f) return;
            var inv = Quaternion.Inverse(CamRotation);
            Vector3[] pts =
            {
                new Vector3(LotMin, 0f, LotMin), new Vector3(LotMax, 11f, LotMax),
                new Vector3(LotMin, 7f, LotMax), new Vector3(LotMax, 7f, LotMin),
                new Vector3(LaneCenter, 0f, RoadZ - RoadHalf),
            };
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            foreach (var p in pts)
            {
                var q = inv * p;
                minX = Mathf.Min(minX, q.x); maxX = Mathf.Max(maxX, q.x);
                minY = Mathf.Min(minY, q.y); maxY = Mathf.Max(maxY, q.y);
            }
            float ex = (maxX - minX) * 0.5f, ey = (maxY - minY) * 0.5f;
            float size = Mathf.Max(ey / free.height, ex / (free.width * aspect)) * 1.03f;
            float cx = (minX + maxX) * 0.5f, cy = (minY + maxY) * 0.5f;
            Vector2 target = free.center;   // centre de la zone libre, en viewport
            float camX = cx - (target.x - 0.5f) * 2f * size * aspect;
            float camY = cy - (target.y - 0.5f) * 2f * size;

            Camera.orthographicSize = size;
            Camera.transform.rotation = CamRotation;
            Camera.transform.position = Origin + CamRotation * new Vector3(camX, camY, -60f);
        }

        // ── Terrain ─────────────────────────────────────────────────────────────────────

        private void BuildGround()
        {
            var g = new GameObject("Ground").transform;
            g.SetParent(_root.transform, false);
            SetLayer(g.gameObject);

            // Pelouse (dessus à y = 0).
            Box(g, new Vector3(0f, -0.1f, 0f), new Vector3(400f, 0.2f, 400f), Grass, shadows: false);

            // Cour en enrobé (dessus à y = 0.06) + bordure.
            float lotC = (LotMin + LotMax) * 0.5f, lotS = LotMax - LotMin;
            Box(g, new Vector3(lotC, 0f, lotC), new Vector3(lotS, 0.12f, lotS), Asphalt, shadows: false);
            float cw = 0.35f;
            Box(g, new Vector3(lotC, 0.1f, LotMax + cw * 0.5f), new Vector3(lotS + cw * 2f, 0.2f, cw), Curb, shadows: false);
            Box(g, new Vector3(LotMin - cw * 0.5f, 0.1f, lotC), new Vector3(cw, 0.2f, lotS + cw * 2f), Curb, shadows: false);
            Box(g, new Vector3(LotMax + cw * 0.5f, 0.1f, lotC), new Vector3(cw, 0.2f, lotS + cw * 2f), Curb, shadows: false);
            // Bord côté route : ouvert au niveau de l'entrée (allée centrale).
            float gapA = LaneCenter - LaneHalf, gapB = LaneCenter + LaneHalf;
            Box(g, new Vector3((LotMin - cw + gapA) * 0.5f, 0.1f, LotMin - cw * 0.5f), new Vector3(gapA - LotMin + cw, 0.2f, cw), Curb, shadows: false);
            Box(g, new Vector3((gapB + LotMax + cw) * 0.5f, 0.1f, LotMin - cw * 0.5f), new Vector3(LotMax + cw - gapB, 0.2f, cw), Curb, shadows: false);

            // Route + trottoir + entrée.
            Box(g, new Vector3(0f, 0f, RoadZ), new Vector3(400f, 0.12f, RoadHalf * 2f), RoadCol, shadows: false);
            for (float x = -120f; x <= 120f; x += 4f)
                Box(g, new Vector3(x, 0.07f, RoadZ), new Vector3(2f, 0.02f, 0.18f), Paint, shadows: false);
            float swZ = RoadZ + RoadHalf + 0.7f;
            Box(g, new Vector3((-200f + gapA) * 0.5f, 0.06f, swZ), new Vector3(gapA + 200f, 0.12f, 1.4f), Curb, shadows: false);
            Box(g, new Vector3((gapB + 200f) * 0.5f, 0.06f, swZ), new Vector3(200f - gapB, 0.12f, 1.4f), Curb, shadows: false);
            Box(g, new Vector3(LaneCenter, 0.005f, (LotMin + RoadZ + RoadHalf) * 0.5f), new Vector3(LaneHalf * 2f, 0.12f, LotMin - RoadZ - RoadHalf + 0.2f), RoadCol, shadows: false);

            // Les 2 allées en enrobé plus sombre : elles se lisent comme de vraies routes internes.
            Box(g, new Vector3(LaneCenter, 0.065f, lotC), new Vector3(LaneHalf * 2f, 0.01f, lotS), RoadCol, shadows: false);
            Box(g, new Vector3(lotC, 0.065f, LaneCenter), new Vector3(lotS, 0.01f, LaneHalf * 2f), RoadCol, shadows: false);

            // Allées qui relient les 3 emplacements et l'entrée : lignes pointillées jaunes.
            for (float t = LotMin + 1f; t < LotMax - 1f; t += 2.6f)
            {
                Box(g, new Vector3(LaneCenter, 0.07f, t), new Vector3(0.16f, 0.02f, 1.3f), PaintYel, shadows: false);
                Box(g, new Vector3(t, 0.07f, LaneCenter), new Vector3(1.3f, 0.02f, 0.16f), PaintYel, shadows: false);
            }
            for (float z = LotMin - 0.5f; z > RoadZ + RoadHalf; z -= 2.6f)
                Box(g, new Vector3(LaneCenter, 0.07f, z), new Vector3(0.16f, 0.02f, 1.3f), PaintYel, shadows: false);

            // Parking camions (devant) : grandes places perpendiculaires à l'allée.
            float pkMin = LotMin + 3f, pkMax = LaneCenter - LaneHalf - 1f;   // x et z ∈ [-19, -5]
            for (float x = pkMin; x <= pkMax + 0.01f; x += (pkMax - pkMin) / 4f)
                Box(g, new Vector3(x, 0.07f, (pkMin + pkMax) * 0.5f), new Vector3(0.16f, 0.02f, pkMax - pkMin - 1f), Paint, shadows: false);
            Box(g, new Vector3((pkMin + pkMax) * 0.5f, 0.07f, pkMin + 0.5f), new Vector3(pkMax - pkMin, 0.02f, 0.16f), Paint, shadows: false);

            // Places voitures le long des emplacements (bande libre entre dalle et bordure).
            float ho = OfficeSize * 0.5f, hh = HangarSize * 0.5f, hf = FuelSize * 0.5f;
            for (float z = OfficePlot.y - ho; z <= OfficePlot.y + ho + 0.01f; z += 2.5f)   // à gauche du bâtiment RH
                Box(g, new Vector3(LotMin + 1.5f, 0.07f, z), new Vector3(2.6f, 0.02f, 0.14f), Paint, shadows: false);
            for (float x = HangarPlot.x - hh; x <= HangarPlot.x + hh + 0.01f; x += 2.8f)   // derrière le garage
                Box(g, new Vector3(x, 0.07f, LotMax - 1.5f), new Vector3(0.14f, 0.02f, 2.6f), Paint, shadows: false);
            for (float z = FuelPlot.y - hf; z <= HangarPlot.y + hh + 0.01f; z += 2.8f)     // à droite du garage et de la station
                Box(g, new Vector3(LotMax - 1.5f, 0.07f, z), new Vector3(2.6f, 0.02f, 0.14f), Paint, shadows: false);

            // Arbres et buissons sur la pelouse (tirage fixe : toujours le même décor).
            var rng = new System.Random(4242);
            for (int i = 0; i < 140; i++)
            {
                float x = (float)(rng.NextDouble() * 140.0 - 75.0);
                float z = (float)(rng.NextDouble() * 140.0 - 75.0);
                bool inLot = x > LotMin - 2.5f && x < LotMax + 2.5f && z > LotMin - 2.5f && z < LotMax + 2.5f;
                bool onRoad = z > RoadZ - RoadHalf - 2f && z < LotMin + 1f;
                if (inLot || onRoad) continue;
                float s = 0.7f + (float)rng.NextDouble() * 0.7f;
                if (rng.NextDouble() < 0.3) Bush(g, new Vector3(x, 0f, z), s);
                else Tree(g, new Vector3(x, 0f, z), s, rng.NextDouble() < 0.5);
            }
            // Haie basse le long de la bordure du fond (cache le bord net de la cour).
            for (float t = LotMin; t <= LotMax; t += 1.6f)
            {
                Bush(g, new Vector3(t, 0f, LotMax + 1.3f), 0.8f);
                Bush(g, new Vector3(LotMax + 1.3f, 0f, t), 0.8f);
            }
        }

        private Slot MakeSlot(Vector2 plot, float size)
        {
            var root = new GameObject("Plot").transform;
            root.SetParent(_root.transform, false);
            SetLayer(root.gameObject);

            // Dalle béton de l'emplacement + liseré : le terrain réservé au bâtiment et à ses agrandissements.
            Box(root, new Vector3(plot.x, 0.11f, plot.y), new Vector3(size, 0.1f, size), Concrete, shadows: false);
            float h = size * 0.5f, w = 0.3f;
            Box(root, new Vector3(plot.x, 0.17f, plot.y + h - w * 0.5f), new Vector3(size, 0.04f, w), Curb, shadows: false);
            Box(root, new Vector3(plot.x, 0.17f, plot.y - h + w * 0.5f), new Vector3(size, 0.04f, w), Curb, shadows: false);
            Box(root, new Vector3(plot.x + h - w * 0.5f, 0.17f, plot.y), new Vector3(w, 0.04f, size), Curb, shadows: false);
            Box(root, new Vector3(plot.x - h + w * 0.5f, 0.17f, plot.y), new Vector3(w, 0.04f, size), Curb, shadows: false);

            var anchor = new GameObject("Building").transform;
            anchor.SetParent(root, false);
            anchor.localPosition = new Vector3(plot.x, 0.16f, plot.y);
            SetLayer(anchor.gameObject);
            return new Slot { root = anchor };
        }

        // ── Bâtiments : un design par niveau, tous tenus dans la dalle (±7 m) ─────────────
        // Façades visibles par la caméra : -z (bas-droite de l'écran) et -x (bas-gauche).

        private static readonly Color Brick    = new Color(0.66f, 0.38f, 0.29f);
        private static readonly Color RoofOld  = new Color(0.46f, 0.41f, 0.37f);
        private static readonly Color Beige    = new Color(0.86f, 0.82f, 0.74f);
        private static readonly Color Modern   = new Color(0.70f, 0.73f, 0.78f);
        private static readonly Color Solar    = new Color(0.16f, 0.24f, 0.40f);
        private static readonly Color Wood     = new Color(0.62f, 0.48f, 0.34f);
        private static readonly Color Barrel   = new Color(0.30f, 0.40f, 0.55f);
        private static readonly Color OldRed   = new Color(0.72f, 0.32f, 0.26f);

        // ---------- Garage (hangar) : 6 niveaux ----------
        //  1 vieux garage en brique, 1 porte · 2 garage rénové, 2 portes + bureau
        //  3 entrepôt bardé, 3 quais · 4 + bureau vitré, lanterneaux · 5 4 quais, panneaux solaires
        //  6 plateforme logistique avec tour de bureaux vitrée
        private void BuildHangar(Transform t, int level)
        {
            switch (level)
            {
                case 1:
                    HangarBody(t, 5f, 5f, 3.2f, Brick, RoofOld, 1, 2.6f, false, 0, 0);
                    Box(t, new Vector3(-2.54f, 1.9f, 0.8f), new Vector3(0.08f, 0.9f, 1.3f), Glass, smooth: 0.7f);
                    Cyl(t, new Vector3(-3.3f, 0.45f, -1.6f), 0.35f, 0.9f, Barrel);
                    Cyl(t, new Vector3(-3.3f, 0.45f, -0.8f), 0.35f, 0.9f, OldRed);
                    Box(t, new Vector3(3.3f, 0.1f, -2f), new Vector3(1.2f, 0.2f, 1.0f), Wood);
                    break;
                case 2:
                    HangarBody(t, 7f, 6f, 3.6f, Beige, RoofCol, 2, 2.5f, false, 1, 0);
                    Cyl(t, new Vector3(4.4f, 0.45f, -2.4f), 0.35f, 0.9f, Barrel);
                    break;
                case 3:
                    HangarBody(t, 8.5f, 7.5f, 4.5f, Cladding, RoofCol, 3, 2.1f, true, 1, 0);
                    break;
                case 4:
                    HangarBody(t, 9.2f, 8.5f, 5f, Cladding, RoofCol, 3, 2.4f, true, 2, 1);
                    break;
                case 5:
                    HangarBody(t, 10f, 9.5f, 5.5f, Cladding, RoofCol, 4, 1.9f, true, 2, 2);
                    break;
                default:
                    HangarBody(t, 10.6f, 10f, 6f, Modern, RoofCol, 4, 2.1f, true, 3, 3);
                    break;
            }
        }

        /// <param name="annex">0 aucun · 1 petit bureau · 2 bureau vitré · 3 tour vitrée</param>
        /// <param name="roofKit">0 rien · 1 lanterneaux · 2 panneaux solaires · 3 les deux</param>
        private void HangarBody(Transform t, float w, float d, float h, Color wall, Color roof,
                                int doors, float doorW, bool modern, int annex, int roofKit)
        {
            float aw = annex == 0 ? 0f : annex == 3 ? 3f : 2.4f;   // largeur de l'annexe côté -x
            float bx = aw * 0.5f;                                   // le tout reste centré sur la dalle
            float front = -d * 0.5f, left = bx - w * 0.5f;
            bool brick = wall == Brick;

            Box(t, new Vector3(bx, h * 0.5f, 0f), new Vector3(w, h, d), wall);
            Color trim = brick ? Concrete : wall * 0.88f;
            foreach (float cx in new[] { left, bx + w * 0.5f })
                Box(t, new Vector3(cx, h * 0.5f, front), new Vector3(0.3f, h, 0.3f), trim);
            Box(t, new Vector3(bx, h + 0.12f, 0f), new Vector3(w + 0.3f, 0.24f, d + 0.3f), roof);
            if (modern) Box(t, new Vector3(bx, h - 0.55f, 0f), new Vector3(w + 0.06f, 0.4f, d + 0.06f), AccentHangar);
            else if (brick)   // assises de brique plus claires
                for (float y = 0.6f; y < h - 0.2f; y += 0.6f)
                    Box(t, new Vector3(bx, y, 0f), new Vector3(w + 0.04f, 0.05f, d + 0.04f), Brick * 1.12f, shadows: false);
            if (!brick)       // nervures du bardage sur la façade gauche
                for (float z = front + 0.6f; z < d * 0.5f - 0.3f; z += 0.9f)
                    Box(t, new Vector3(left - 0.03f, h * 0.5f, z), new Vector3(0.06f, h - 0.3f, 0.08f), wall * 0.9f, shadows: false);

            // Portes sectionnelles réparties sur la façade avant.
            float doorH = Mathf.Min(3.4f, h - 0.9f);
            float step = w / doors;
            for (int i = 0; i < doors; i++)
            {
                float x = left + step * (i + 0.5f);
                Color door = brick ? new Color(0.48f, 0.50f, 0.46f) : DarkMetal;
                Box(t, new Vector3(x, doorH * 0.5f, front - 0.04f), new Vector3(doorW, doorH, 0.12f), door);
                for (float y = 0.5f; y < doorH - 0.2f; y += 0.75f)
                    Box(t, new Vector3(x, y, front - 0.12f), new Vector3(doorW, 0.06f, 0.04f), door * 0.82f, shadows: false);
                Box(t, new Vector3(x, doorH + 0.1f, front - 0.08f), new Vector3(doorW + 0.3f, 0.2f, 0.14f), door * 0.75f);
                if (modern)
                {
                    Box(t, new Vector3(x - doorW * 0.5f - 0.12f, 0.35f, front - 0.15f), new Vector3(0.2f, 0.5f, 0.2f), new Color(0.12f, 0.12f, 0.13f));
                    Box(t, new Vector3(x + doorW * 0.5f + 0.12f, 0.35f, front - 0.15f), new Vector3(0.2f, 0.5f, 0.2f), new Color(0.12f, 0.12f, 0.13f));
                    Box(t, new Vector3(x - step * 0.5f, 0.01f, front - 1.1f), new Vector3(0.14f, 0.02f, 2.2f), PaintYel, shadows: false);
                }
            }
            if (modern) Box(t, new Vector3(left + w, 0.01f, front - 1.1f), new Vector3(0.14f, 0.02f, 2.2f), PaintYel, shadows: false);

            // Annexe côté -x, alignée sur la façade avant.
            if (annex > 0)
            {
                float ax = left - aw * 0.5f;
                if (annex == 3)
                {
                    float ad = 3.2f, th = 9f, az = front + ad * 0.5f;
                    Box(t, new Vector3(ax, th * 0.5f, az), new Vector3(aw, th, ad), Glass, smooth: 0.75f);
                    for (float y = 0f; y <= th; y += 2.25f)
                        Box(t, new Vector3(ax, y, az), new Vector3(aw + 0.08f, 0.22f, ad + 0.08f), White);
                    for (float z = az - ad * 0.5f + 0.8f; z < az + ad * 0.5f; z += 0.8f)
                        Box(t, new Vector3(ax - aw * 0.5f - 0.04f, th * 0.5f, z), new Vector3(0.06f, th, 0.08f), White, shadows: false);
                    Box(t, new Vector3(ax, th + 0.25f, az), new Vector3(aw + 0.2f, 0.3f, ad + 0.2f), AccentHangar);
                    Box(t, new Vector3(ax, 1.1f, front - 0.05f), new Vector3(1.4f, 2.2f, 0.08f), GlassDark, smooth: 0.7f);
                }
                else
                {
                    float ad = Mathf.Min(d * 0.6f, 5.5f), ah = annex == 2 ? 3.6f : 2.8f, az = front + ad * 0.5f;
                    Box(t, new Vector3(ax, ah * 0.5f, az), new Vector3(aw, ah, ad), Wall);
                    float gh = annex == 2 ? ah - 0.8f : 1.0f;
                    Box(t, new Vector3(ax - aw * 0.5f - 0.03f, ah * 0.55f, az), new Vector3(0.08f, gh, ad - 0.8f), Glass, smooth: 0.7f);
                    Box(t, new Vector3(ax, 1.1f, front - 0.04f), new Vector3(1.0f, 2.2f, 0.08f), GlassDark, smooth: 0.7f);
                    Box(t, new Vector3(ax, ah + 0.1f, az), new Vector3(aw + 0.2f, 0.2f, ad + 0.2f), annex == 2 ? AccentHangar : RoofCol);
                }
            }
            else if (!brick)
                Box(t, new Vector3(left - 0.05f, h - 1.3f, 0f), new Vector3(0.08f, 0.9f, Mathf.Min(3f, d * 0.5f)), AccentHangar);

            // Toit.
            float ry = h + 0.24f;
            if (roofKit == 1 || roofKit == 3)
            {
                float xEnd = roofKit == 3 ? bx : bx + w * 0.5f - 0.8f;
                for (float x = left + 1.2f; x < xEnd; x += 2.3f)
                    Box(t, new Vector3(x, ry + 0.1f, 0f), new Vector3(1.0f, 0.22f, d * 0.7f), Glass, smooth: 0.7f);
            }
            if (roofKit >= 2)
            {
                float x0 = roofKit == 3 ? bx + 0.3f : left + 0.6f, x1 = bx + w * 0.5f - 0.6f;
                for (float z = front + 1.2f; z < d * 0.5f - 0.8f; z += 1.5f)
                    Box(t, new Vector3((x0 + x1) * 0.5f, ry + 0.4f, z), new Vector3(x1 - x0, 0.08f, 1.1f), Solar, smooth: 0.6f, euler: new Vector3(-15f, 0f, 0f));
            }
            if (!brick)
            {
                Box(t, new Vector3(bx + w * 0.5f - 1f, ry + 0.35f, d * 0.5f - 1.2f), new Vector3(1.1f, 0.7f, 1.1f), Metal);
                if (w > 8f) Box(t, new Vector3(bx + w * 0.5f - 2.4f, ry + 0.3f, d * 0.5f - 1.2f), new Vector3(0.9f, 0.6f, 0.9f), Metal);
            }
        }

        // ---------- Station essence : 10 niveaux ----------
        //  1 vieille pompe + fûts · 2 cuve-cube en cage · 3 1 pompe sous petit auvent + petite cuve
        //  4 îlot de 2 pompes · 5 2 îlots + boutique · 6 grand auvent, 4 pompes, 2 cuves
        //  7 + 1 grande cuve verticale · 8 2 cuves verticales, passerelle, rack de tuyaux
        //  9 terminal : 3 cuves, portique de chargement, bâtiment de contrôle
        // 10 4 grandes cuves et tour de contrôle vitrée
        private void BuildFuelStation(Transform t, int level)
        {
            switch (level)
            {
                case 1:
                    Box(t, new Vector3(-0.8f, 0.06f, -1f), new Vector3(4f, 0.12f, 4f), Concrete * 0.93f, shadows: false);
                    Box(t, new Vector3(-1.6f, 0.82f, -1.8f), new Vector3(0.6f, 1.4f, 0.6f), OldRed);
                    Box(t, new Vector3(-1.6f, 1.62f, -1.8f), new Vector3(0.7f, 0.25f, 0.7f), White * 0.9f);
                    Box(t, new Vector3(-1.93f, 1.1f, -1.8f), new Vector3(0.04f, 0.3f, 0.35f), GlassDark);
                    Cyl(t, new Vector3(0f, 0.57f, -0.6f), 0.35f, 0.9f, Barrel);
                    Cyl(t, new Vector3(0.7f, 0.57f, 0.2f), 0.35f, 0.9f, Barrel);
                    Cyl(t, new Vector3(-0.1f, 0.57f, 0.4f), 0.35f, 0.9f, OldRed);
                    break;
                case 2:
                    Box(t, new Vector3(-0.8f, 0.06f, -0.8f), new Vector3(5f, 0.12f, 4.4f), Concrete * 0.95f, shadows: false);
                    Ibc(t, new Vector3(0.6f, 0.12f, 0f));
                    Pump(t, -1.6f, -1.6f, 0.12f);
                    Cyl(t, new Vector3(-2.6f, 0.57f, 0.4f), 0.35f, 0.9f, Barrel);
                    break;
                case 3:
                    Canopy(t, -2f, -2.5f, 3.6f, 3.6f, 3.6f);
                    Island(t, -2f, -2.5f, 2.4f, 1);
                    HTank(t, 0.8f, 3.4f, 0.9f, 3.6f);
                    break;
                case 4:
                    Canopy(t, -2f, -2.5f, 4.6f, 4.6f, 4f);
                    Island(t, -2f, -2.5f, 3.6f, 2);
                    HTank(t, 0.8f, 3.8f, 1.0f, 4.5f);
                    Ibc(t, new Vector3(4.4f, 0f, -2f));
                    break;
                case 5:
                    Canopy(t, -1.8f, -2.5f, 6.2f, 5f, 4.2f);
                    Island(t, -3.3f, -2.5f, 2.4f, 1);
                    Island(t, -0.3f, -2.5f, 2.4f, 1);
                    HTank(t, 1f, 4.4f, 1.2f, 5f);
                    Kiosk(t, -5f, 4.6f);
                    break;
                case 6:
                    Forecourt(t);
                    HTank(t, 1f, 3.2f, 1.0f, 5f);
                    HTank(t, 1f, 5.6f, 1.0f, 5f);
                    Kiosk(t, -5f, 4.6f);
                    break;
                case 7:
                    Forecourt(t);
                    HTank(t, 1f, 3.2f, 1.0f, 5f);
                    HTank(t, 1f, 5.6f, 1.0f, 5f);
                    Kiosk(t, -5f, 4.6f);
                    VTank(t, 5f, -3.4f, 1.5f, 5.5f, true);
                    break;
                case 8:
                    Forecourt(t);
                    HTank(t, 0.5f, 4.6f, 1.2f, 4.5f);
                    Kiosk(t, -5f, 4.6f);
                    VTank(t, 5f, -3.6f, 1.5f, 5.5f, true);
                    VTank(t, 5f, 0.6f, 1.5f, 5.5f, false);
                    Box(t, new Vector3(5f, 5.5f, -1.5f), new Vector3(0.6f, 0.12f, 1.2f), DarkMetal);   // passerelle
                    PipeRack(t, 2.8f, -5f, 4f, 2.4f);
                    break;
                case 9:
                    Terminal(t, false);
                    break;
                default:
                    Terminal(t, true);
                    break;
            }
        }

        private void Forecourt(Transform t)
        {
            Canopy(t, -1.8f, -2.5f, 7.2f, 6.2f, 4.4f);
            Island(t, -3.3f, -2.5f, 4.2f, 2);
            Island(t, -0.3f, -2.5f, 4.2f, 2);
        }

        private void Terminal(Transform t, bool big)
        {
            Canopy(t, -0.8f, -2.8f, 5f, 6f, 4.4f);
            Island(t, -1.9f, -2.8f, 4f, 2);
            Island(t, 0.3f, -2.8f, 4f, 2);
            Gantry(t, -5.6f, -3f);
            float g = big ? 0.15f : 0f, hh = big ? 1f : 0f;
            VTank(t, 5f, -3.8f, 1.45f + g, 6f + hh, true);
            VTank(t, 5f, 0.4f, 1.45f + g, 6f + hh, false);
            VTank(t, 4.7f, 4.7f, 1.65f + g, 7f + hh, false);
            if (big) VTank(t, 0.4f, 4.9f, 1.8f, 8f, true);
            else Box(t, new Vector3(0.4f, 1f, 4.8f), new Vector3(2f, 2f, 2f), Wall);   // local pompes
            Box(t, new Vector3(5f, 6f + hh, -1.7f), new Vector3(0.6f, 0.12f, 1.4f), DarkMetal);
            Box(t, new Vector3(4.85f, 6.5f + hh, 2.5f), new Vector3(0.6f, 0.12f, 1.4f), DarkMetal);
            PipeRack(t, 2.55f, -5.5f, 5.5f, 2.6f);
            ControlBuilding(t, -4.7f, 4.6f, big);
        }

        private void Canopy(Transform t, float cx, float cz, float w, float d, float h)
        {
            Box(t, new Vector3(cx, h, cz), new Vector3(w, 0.38f, d), AccentFuel);
            Box(t, new Vector3(cx, h + 0.24f, cz), new Vector3(w - 0.2f, 0.12f, d - 0.2f), White);
            float px = w * 0.5f - 0.7f, pz = d * 0.5f - 0.7f;
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                    Cyl(t, new Vector3(cx + sx * px, (h - 0.19f) * 0.5f, cz + sz * pz), 0.16f, h - 0.19f, Metal);
        }

        private void Island(Transform t, float x, float cz, float len, int pumps)
        {
            Box(t, new Vector3(x, 0.12f, cz), new Vector3(1.0f, 0.24f, len), Concrete);
            for (int i = 0; i < pumps; i++)
                Pump(t, x, cz - len * 0.5f + len * (i + 0.5f) / pumps, 0.24f);
            Cyl(t, new Vector3(x, 0.5f, cz - len * 0.5f - 0.3f), 0.14f, 1.0f, PaintYel);
            Cyl(t, new Vector3(x, 0.5f, cz + len * 0.5f + 0.3f), 0.14f, 1.0f, PaintYel);
        }

        private void Pump(Transform t, float x, float z, float y0)
        {
            Box(t, new Vector3(x, y0 + 0.8f, z), new Vector3(0.6f, 1.6f, 0.8f), White);
            Box(t, new Vector3(x, y0 + 1.5f, z), new Vector3(0.64f, 0.3f, 0.84f), AccentFuel);
            Box(t, new Vector3(x - 0.31f, y0 + 1.05f, z), new Vector3(0.04f, 0.35f, 0.5f), GlassDark, smooth: 0.7f);
        }

        // Cuve-cube (GRV) en cage métallique sur palette.
        private void Ibc(Transform t, Vector3 p)
        {
            Box(t, p + new Vector3(0f, 0.1f, 0f), new Vector3(1.4f, 0.2f, 1.4f), Wood);
            Box(t, p + new Vector3(0f, 0.85f, 0f), new Vector3(1.2f, 1.2f, 1.2f), White * 0.97f, smooth: 0.3f);
            foreach (float sx in new[] { -0.62f, 0.62f })
                foreach (float sz in new[] { -0.62f, 0.62f })
                    Box(t, p + new Vector3(sx, 0.85f, sz), new Vector3(0.06f, 1.3f, 0.06f), Metal);
            Box(t, p + new Vector3(0f, 1.5f, 0f), new Vector3(1.3f, 0.06f, 1.3f), Metal);
            Box(t, p + new Vector3(0f, 0.85f, -0.63f), new Vector3(1.24f, 0.05f, 0.04f), Metal, shadows: false);
        }

        // Cuve horizontale sur berceaux (axe x).
        private void HTank(Transform t, float cx, float cz, float r, float len)
        {
            float y = r + 0.55f;
            Cyl(t, new Vector3(cx, y, cz), r, len, White, new Vector3(0f, 0f, 90f));
            Cyl(t, new Vector3(cx - len * 0.45f, y, cz), r + 0.03f, 0.25f, AccentFuel, new Vector3(0f, 0f, 90f));
            Cyl(t, new Vector3(cx + len * 0.45f, y, cz), r + 0.03f, 0.25f, AccentFuel, new Vector3(0f, 0f, 90f));
            foreach (float sx in new[] { -0.3f, 0.3f })
                Box(t, new Vector3(cx + sx * len, 0.35f, cz), new Vector3(0.4f, 0.7f, r * 1.7f), DarkMetal);
        }

        // Cuve verticale (dôme, bandes vertes, échelle sur la face visible).
        private void VTank(Transform t, float x, float z, float r, float h, bool ladder)
        {
            Cyl(t, new Vector3(x, 0.1f, z), r + 0.3f, 0.2f, Concrete);
            Cyl(t, new Vector3(x, 0.2f + h * 0.5f, z), r, h, White);
            Ball(t, new Vector3(x, 0.2f + h, z), new Vector3(r * 2f, 0.9f, r * 2f), White);
            Cyl(t, new Vector3(x, 0.2f + h * 0.3f, z), r + 0.03f, 0.3f, AccentFuel);
            Cyl(t, new Vector3(x, 0.2f + h * 0.75f, z), r + 0.03f, 0.3f, AccentFuel);
            if (ladder)
            {
                Box(t, new Vector3(x - r - 0.05f, 0.2f + h * 0.5f, z), new Vector3(0.08f, h, 0.5f), DarkMetal);
                Box(t, new Vector3(x, 0.25f + h, z), new Vector3(r * 1.4f, 0.08f, 0.08f), PaintYel, shadows: false);
            }
        }

        // Portique de chargement des camions-citernes.
        private void Gantry(Transform t, float x, float z)
        {
            foreach (float sx in new[] { -0.9f, 0.9f })
                foreach (float sz in new[] { -2.2f, 2.2f })
                    Box(t, new Vector3(x + sx, 2.4f, z + sz), new Vector3(0.22f, 4.8f, 0.22f), Metal);
            Box(t, new Vector3(x, 4.2f, z), new Vector3(2.3f, 0.15f, 4.9f), DarkMetal);
            Box(t, new Vector3(x - 1.12f, 4.6f, z), new Vector3(0.05f, 0.7f, 4.9f), PaintYel, shadows: false);
            Box(t, new Vector3(x, 5.0f, z), new Vector3(2.4f, 0.12f, 5.1f), AccentFuel);
            foreach (float sz in new[] { -1.1f, 1.1f })
                Box(t, new Vector3(x + 0.3f, 3.2f, z + sz), new Vector3(0.12f, 1.8f, 0.12f), DarkMetal);
        }

        // Rack de tuyaux le long de l'axe z.
        private void PipeRack(Transform t, float x, float z0, float z1, float y)
        {
            for (float z = z0; z <= z1 + 0.01f; z += 2.2f)
                Box(t, new Vector3(x, y * 0.5f, z), new Vector3(0.18f, y, 0.18f), DarkMetal);
            float mid = (z0 + z1) * 0.5f, len = z1 - z0;
            Box(t, new Vector3(x, y, mid), new Vector3(0.5f, 0.1f, len), DarkMetal);
            Cyl(t, new Vector3(x - 0.12f, y + 0.17f, mid), 0.11f, len, Metal, new Vector3(90f, 0f, 0f));
            Cyl(t, new Vector3(x + 0.12f, y + 0.17f, mid), 0.11f, len, AccentFuel, new Vector3(90f, 0f, 0f));
        }

        private void Kiosk(Transform t, float x, float z)
        {
            Box(t, new Vector3(x, 1.3f, z), new Vector3(3f, 2.6f, 2.4f), Wall);
            Box(t, new Vector3(x, 1.4f, z - 1.22f), new Vector3(2.4f, 1.4f, 0.08f), Glass, smooth: 0.7f);
            Box(t, new Vector3(x - 1.52f, 1.4f, z), new Vector3(0.08f, 1.4f, 1.6f), Glass, smooth: 0.7f);
            Box(t, new Vector3(x, 2.7f, z), new Vector3(3.2f, 0.2f, 2.6f), AccentFuel);
        }

        private void ControlBuilding(Transform t, float x, float z, bool tower)
        {
            Box(t, new Vector3(x, 1.4f, z), new Vector3(3.6f, 2.8f, 3f), Wall);
            Box(t, new Vector3(x, 1.6f, z - 1.52f), new Vector3(3f, 1.2f, 0.08f), Glass, smooth: 0.7f);
            Box(t, new Vector3(x - 1.82f, 1.6f, z), new Vector3(0.08f, 1.2f, 2.4f), Glass, smooth: 0.7f);
            Box(t, new Vector3(x, 2.9f, z), new Vector3(3.8f, 0.2f, 3.2f), AccentFuel);
            if (!tower) return;
            Box(t, new Vector3(x + 0.4f, 4.6f, z + 0.3f), new Vector3(1.4f, 3.4f, 1.4f), Wall);
            Box(t, new Vector3(x + 0.4f, 7f, z + 0.3f), new Vector3(2.8f, 1.5f, 2.8f), Glass, smooth: 0.8f);
            Box(t, new Vector3(x + 0.4f, 6.2f, z + 0.3f), new Vector3(3f, 0.15f, 3f), White);
            Box(t, new Vector3(x + 0.4f, 7.85f, z + 0.3f), new Vector3(3.1f, 0.25f, 3.1f), AccentFuel);
        }

        // ---------- Bâtiment RH : 3 niveaux ----------
        //  1 cabine préfabriquée · 2 immeuble de 2 étages · 3 siège vitré de 3 étages, toit-terrasse et solaire
        private void BuildOffice(Transform t, int level)
        {
            switch (level)
            {
                case 1:
                {
                    foreach (float sx in new[] { -1.9f, 1.9f })
                        foreach (float sz in new[] { -1f, 1f })
                            Box(t, new Vector3(sx, 0.1f, sz), new Vector3(0.4f, 0.2f, 0.4f), Concrete);
                    Box(t, new Vector3(0f, 1.45f, 0f), new Vector3(4.6f, 2.5f, 2.6f), new Color(0.90f, 0.91f, 0.92f));
                    for (float x = -1.15f; x <= 1.16f; x += 1.15f)
                        Box(t, new Vector3(x, 1.45f, -1.32f), new Vector3(0.05f, 2.5f, 0.04f), Metal, shadows: false);
                    Box(t, new Vector3(0f, 2.6f, 0f), new Vector3(4.64f, 0.18f, 2.64f), AccentOffice);
                    Box(t, new Vector3(0f, 2.78f, 0f), new Vector3(4.8f, 0.12f, 2.8f), Metal);
                    Box(t, new Vector3(1.4f, 1.25f, -1.32f), new Vector3(0.9f, 2.0f, 0.06f), DarkMetal);
                    Box(t, new Vector3(1.4f, 0.12f, -1.75f), new Vector3(1.2f, 0.24f, 0.8f), Concrete);
                    Box(t, new Vector3(-0.8f, 1.7f, -1.32f), new Vector3(1.3f, 0.8f, 0.06f), Glass, smooth: 0.7f);
                    Box(t, new Vector3(-2.32f, 1.7f, 0f), new Vector3(0.06f, 0.8f, 1.2f), Glass, smooth: 0.7f);
                    Box(t, new Vector3(2.5f, 1.4f, 0.5f), new Vector3(0.35f, 0.6f, 0.7f), Metal);   // clim
                    Box(t, new Vector3(-1.2f, 0.3f, -2.3f), new Vector3(1.4f, 0.1f, 0.45f), Wood);  // banc
                    Ball(t, new Vector3(-2.8f, 0.45f, -1.8f), new Vector3(0.9f, 0.8f, 0.9f), GrassDark);
                    break;
                }
                case 2:
                    OfficeBody(t, 8f, 6f, 2, 3f, false);
                    Box(t, new Vector3(2f, 6.55f, 1.8f), new Vector3(1.3f, 0.7f, 1.3f), Metal);
                    Planters(t, 0.8f, 3.6f, -3.4f);
                    break;
                default:
                {
                    float h = OfficeBody(t, 10f, 8f, 3, 2.7f, true);
                    // Toit-terrasse (gauche) et panneaux solaires (droite).
                    Box(t, new Vector3(-2.4f, h + 0.45f, 0.4f), new Vector3(4.6f, 0.1f, 6.6f), Wood);
                    Box(t, new Vector3(-4.65f, h + 0.85f, 0.4f), new Vector3(0.05f, 0.7f, 6.6f), Glass, smooth: 0.8f);
                    Box(t, new Vector3(-2.4f, h + 0.85f, -2.95f), new Vector3(4.6f, 0.7f, 0.05f), Glass, smooth: 0.8f);
                    foreach (float z in new[] { -1.8f, 2.6f })
                    {
                        Box(t, new Vector3(-3.8f, h + 0.75f, z), new Vector3(0.8f, 0.5f, 0.8f), Wood * 0.8f);
                        Ball(t, new Vector3(-3.8f, h + 1.2f, z), new Vector3(0.9f, 0.8f, 0.9f), GrassDark);
                    }
                    for (float z = -2.4f; z < 3.6f; z += 1.5f)
                        Box(t, new Vector3(2.5f, h + 0.75f, z), new Vector3(4.2f, 0.08f, 1.1f), Solar, smooth: 0.6f, euler: new Vector3(-15f, 0f, 0f));
                    Planters(t, 1.4f, 4.6f, -4.6f);
                    break;
                }
            }
        }

        /// <summary>Immeuble de bureaux : renvoie la hauteur du toit.</summary>
        private float OfficeBody(Transform t, float w, float d, int floors, float fh, bool curtain)
        {
            const float cz = 0.4f;
            float h = floors * fh, front = cz - d * 0.5f, left = -w * 0.5f;
            Box(t, new Vector3(0f, h * 0.5f, cz), new Vector3(w, h, d), Wall);
            float doorX = w * 0.5f - 1.5f;
            for (int f = 0; f < floors; f++)
            {
                float y0 = f * fh;
                if (f > 0) Box(t, new Vector3(0f, y0, cz), new Vector3(w + 0.12f, 0.22f, d + 0.12f), Metal);
                float gh = curtain ? fh - 0.4f : fh * 0.5f, gy = y0 + (curtain ? fh * 0.5f : fh * 0.55f);
                float x0 = left + 0.4f, x1 = f == 0 ? doorX - 0.9f : w * 0.5f - 0.4f;   // rez : place pour l'entrée
                Box(t, new Vector3((x0 + x1) * 0.5f, gy, front - 0.04f), new Vector3(x1 - x0, gh, 0.08f), Glass, smooth: 0.7f);
                Box(t, new Vector3(left - 0.04f, gy, cz), new Vector3(0.08f, gh, d - 0.8f), Glass, smooth: 0.7f);
                for (float x = x0 + 1.1f; x < x1 - 0.2f; x += 1.1f)
                    Box(t, new Vector3(x, gy, front - 0.09f), new Vector3(0.08f, gh, 0.06f), Wall, shadows: false);
                for (float z = front + 1.5f; z < front + d - 0.5f; z += 1.1f)
                    Box(t, new Vector3(left - 0.09f, gy, z), new Vector3(0.06f, gh, 0.08f), Wall, shadows: false);
            }
            Box(t, new Vector3(0f, h + 0.05f, cz), new Vector3(w + 0.08f, 0.3f, d + 0.08f), AccentOffice);
            Box(t, new Vector3(0f, h + 0.3f, cz), new Vector3(w + 0.25f, 0.2f, d + 0.25f), RoofCol);
            Box(t, new Vector3(doorX, 1.1f, front - 0.04f), new Vector3(1.6f, 2.2f, 0.08f), GlassDark, smooth: 0.7f);
            Box(t, new Vector3(doorX, 2.45f, front - 0.65f), new Vector3(2.4f, 0.16f, 1.3f), AccentOffice);
            return h + 0.3f;
        }

        private void Planters(Transform t, float xa, float xb, float z)
        {
            foreach (float x in new[] { xa, xb })
            {
                Box(t, new Vector3(x, 0.3f, z), new Vector3(0.9f, 0.6f, 0.9f), new Color(0.55f, 0.50f, 0.45f));
                Ball(t, new Vector3(x, 0.95f, z), new Vector3(1.1f, 0.9f, 1.1f), GrassDark);
            }
        }

        /// <summary>Bâtiment pas encore réparé : chantier (barrières, gravats, palettes).</summary>
        private void BuildRuin(Transform t)
        {
            float h = 4.2f;
            for (int side = 0; side < 4; side++)
            {
                for (float u = -h + 1.05f; u < h; u += 2.1f)
                {
                    Vector3 p = side == 0 ? new Vector3(u, 0.45f, -h) : side == 1 ? new Vector3(u, 0.45f, h)
                              : side == 2 ? new Vector3(-h, 0.45f, u) : new Vector3(h, 0.45f, u);
                    Vector3 size = side < 2 ? new Vector3(1.9f, 0.5f, 0.15f) : new Vector3(0.15f, 0.5f, 1.9f);
                    Box(t, p, size, Orange);
                    Box(t, p + new Vector3(0f, 0.28f, 0f), size + new Vector3(0.02f, -0.36f, 0.02f), White);
                }
            }
            // Gravats et murets cassés.
            var rng = new System.Random(7);
            for (int i = 0; i < 9; i++)
            {
                float x = (float)(rng.NextDouble() * 5.0 - 2.5), z = (float)(rng.NextDouble() * 5.0 - 2.5);
                float s = 0.4f + (float)rng.NextDouble() * 0.7f;
                float grey = 0.50f + (float)rng.NextDouble() * 0.2f;
                Box(t, new Vector3(x, s * 0.35f, z), new Vector3(s, s * 0.7f, s * 0.9f), new Color(grey, grey * 0.97f, grey * 0.94f),
                    euler: new Vector3((float)rng.NextDouble() * 20f, (float)rng.NextDouble() * 90f, (float)rng.NextDouble() * 20f));
            }
            Box(t, new Vector3(-1.5f, 0.8f, 2f), new Vector3(3.2f, 1.6f, 0.35f), new Color(0.66f, 0.42f, 0.34f), euler: new Vector3(0f, 10f, 6f));
            // Palette de matériaux neufs.
            Box(t, new Vector3(2.2f, 0.12f, 2.2f), new Vector3(1.4f, 0.24f, 1.2f), new Color(0.70f, 0.55f, 0.36f));
            Box(t, new Vector3(2.2f, 0.6f, 2.2f), new Vector3(1.2f, 0.7f, 1.0f), new Color(0.80f, 0.80f, 0.78f));
        }

        // ── Décor ───────────────────────────────────────────────────────────────────────

        private void Tree(Transform parent, Vector3 basePos, float s, bool round)
        {
            Cyl(parent, basePos + new Vector3(0f, 0.8f * s, 0f), 0.18f * s, 1.6f * s, Trunk);
            Color leaf = round ? new Color(0.33f, 0.52f, 0.27f) : new Color(0.27f, 0.45f, 0.26f);
            if (round)
            {
                Ball(parent, basePos + new Vector3(0f, 2.3f * s, 0f), new Vector3(2.2f, 2.0f, 2.2f) * s, leaf);
                Ball(parent, basePos + new Vector3(0.4f * s, 3.0f * s, 0.2f * s), new Vector3(1.4f, 1.3f, 1.4f) * s, leaf * 1.08f);
            }
            else   // conifère : boules empilées de plus en plus petites
            {
                Ball(parent, basePos + new Vector3(0f, 2.0f * s, 0f), new Vector3(2.0f, 1.4f, 2.0f) * s, leaf);
                Ball(parent, basePos + new Vector3(0f, 2.9f * s, 0f), new Vector3(1.5f, 1.2f, 1.5f) * s, leaf);
                Ball(parent, basePos + new Vector3(0f, 3.7f * s, 0f), new Vector3(0.9f, 1.0f, 0.9f) * s, leaf);
            }
        }

        private void Bush(Transform parent, Vector3 basePos, float s)
            => Ball(parent, basePos + new Vector3(0f, 0.45f * s, 0f), new Vector3(1.4f, 1.0f, 1.3f) * s, GrassDark);

        // ── Primitives ──────────────────────────────────────────────────────────────────

        private GameObject Box(Transform p, Vector3 center, Vector3 size, Color c, float smooth = 0.15f, bool shadows = true, Vector3 euler = default)
            => Prim(PrimitiveType.Cube, p, center, size, c, euler, smooth, shadows);

        /// <summary>Cylindre de rayon et hauteur donnés (axe vertical, sauf rotation).</summary>
        private GameObject Cyl(Transform p, Vector3 center, float radius, float height, Color c, Vector3 euler = default)
            => Prim(PrimitiveType.Cylinder, p, center, new Vector3(radius * 2f, height * 0.5f, radius * 2f), c, euler, 0.25f, true);

        private GameObject Ball(Transform p, Vector3 center, Vector3 diameter, Color c)
            => Prim(PrimitiveType.Sphere, p, center, diameter, c, default, 0.05f, true);

        private GameObject Prim(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Color c, Vector3 euler, float smooth, bool shadows)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.Destroy(go.GetComponent<Collider>());
            go.layer = Layer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            var r = go.GetComponent<MeshRenderer>();
            if (_template == null) _template = r.sharedMaterial;   // matériau par défaut du pipeline (URP Lit)
            r.sharedMaterial = Mat(c, smooth);
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            r.receiveShadows = true;
            return go;
        }

        private Material Mat(Color c, float smooth)
        {
            if (_mats.TryGetValue((c, smooth), out var m) && m != null) return m;
            m = new Material(_template) { color = c };
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
            _mats[(c, smooth)] = m;
            return m;
        }

        private static void SetLayer(GameObject go) => go.layer = Layer;
    }
}
