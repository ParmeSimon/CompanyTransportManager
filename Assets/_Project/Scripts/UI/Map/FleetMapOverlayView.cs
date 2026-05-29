using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TransportManager.Core;
using TransportManager.Entities.Contracts;
using TransportManager.Entities.Map;
using TransportManager.Enums;
using TransportManager.Events;
using TransportManager.Systems.Contracts;
using TransportManager.Systems.Map;
using TransportManager.Systems.Map.Visualization;

namespace TransportManager.UI.Map
{
    /// Trace l'itinéraire (trait fin) et place une icône camion pour CHAQUE
    /// contrat en cours dont l'affichage n'a pas été masqué via l'œil de la liste
    /// de flotte. Distinct de RouteOverlayView (prévisualisation d'une seule route,
    /// trait épais + marqueurs A/B).
    [RequireComponent(typeof(RectTransform))]
    public class FleetMapOverlayView : MonoBehaviour
    {
        // Trait plus fin et un peu plus pâle que la prévisualisation.
        private static readonly Color CoreColor   = new Color(0.26f, 0.56f, 0.98f, 0.92f);
        private static readonly Color CasingColor = new Color(0.10f, 0.24f, 0.52f, 0.92f);
        private static readonly Color TruckBg     = new Color(0.22f, 0.52f, 1.00f, 1f);

        private const float CoreThickness   = 3.5f;
        private const float CasingThickness = 6f;
        private const int   MaxSegments     = 160;
        private const float TruckSize       = 30f;
        private const float TickSeconds     = 1f;

        private SlippyMapView _map;
        private MapSystem     _mapSys;
        private RectTransform _lineLayer;
        private Coroutine     _tick;

        private class RouteVisual
        {
            public ContractInstance inst;
            public double           latA, lonA, latB, lonB;
            public List<GeoPoint>   polyline;   // null tant que la route n'est pas chargée
            public double[]         cumKm;      // cumul de distance le long de la polyline
            public RectTransform    truck;
            public readonly List<RectTransform> segments = new List<RectTransform>();
            public bool routeRequested;
        }

        private readonly Dictionary<string, RouteVisual> _visuals = new Dictionary<string, RouteVisual>();
        private static Sprite _capSprite, _circleSprite, _truckSprite;

        // ── Init ──────────────────────────────────────────────────────────────────
        public void Init(SlippyMapView map)
        {
            _map    = map;
            _mapSys = ServiceLocator.Get<MapSystem>();

            var lg = new GameObject("FleetLines", typeof(RectTransform));
            lg.transform.SetParent(transform, false);
            _lineLayer = lg.GetComponent<RectTransform>();
            StretchFill(_lineLayer);

            if (_map != null) _map.OnViewChanged += RedrawAll;
            FleetMapDisplayState.OnChanged += Reconcile;
            GameEvents.OnContractStarted   += OnContractsChanged;
            GameEvents.OnContractCompleted += OnContractsChanged;

            Reconcile();
            RestartTick();
        }

        private void OnEnable()
        {
            // Init() (re)lance déjà le tick ; ce garde-fou couvre les cycles
            // désactivation/réactivation de l'onglet carte une fois initialisé.
            if (_lineLayer != null) RestartTick();
        }

        private void OnDisable()
        {
            if (_tick != null) { StopCoroutine(_tick); _tick = null; }
        }

        private void OnDestroy()
        {
            if (_map != null) _map.OnViewChanged -= RedrawAll;
            FleetMapDisplayState.OnChanged -= Reconcile;
            GameEvents.OnContractStarted   -= OnContractsChanged;
            GameEvents.OnContractCompleted -= OnContractsChanged;
            if (_tick != null) StopCoroutine(_tick);
        }

        private void RestartTick()
        {
            if (_tick != null) StopCoroutine(_tick);
            _tick = StartCoroutine(TickRoutine());
        }

        private void OnContractsChanged(ContractInstance _) => Reconcile();

        private IEnumerator TickRoutine()
        {
            var wait = new WaitForSecondsRealtime(TickSeconds);
            while (true)
            {
                yield return wait;
                UpdateTrucks();
            }
        }

        // ── Réconciliation visuels ↔ contrats en cours visibles ─────────────────────
        private void Reconcile()
        {
            var contracts = ServiceLocator.Get<ContractSystem>();
            var keep = new HashSet<string>();

            if (contracts != null)
            {
                foreach (var inst in contracts.Active)
                {
                    if (inst.status != ContractStatus.InProgress) continue;
                    if (inst.definition == null) continue;
                    if (!FleetMapDisplayState.IsVisible(inst.instanceId)) continue;

                    keep.Add(inst.instanceId);
                    if (_visuals.TryGetValue(inst.instanceId, out var existing))
                        existing.inst = inst;
                    else
                        AddVisual(inst);
                }
            }

            var toRemove = new List<string>();
            foreach (var kv in _visuals)
                if (!keep.Contains(kv.Key)) toRemove.Add(kv.Key);
            foreach (var id in toRemove) RemoveVisual(id);

            RedrawAll();
        }

        private void AddVisual(ContractInstance inst)
        {
            var v = new RouteVisual { inst = inst };

            if (_mapSys?.Catalog != null)
            {
                var from = _mapSys.Catalog.GetById(inst.definition.originCityId);
                var to   = _mapSys.Catalog.GetById(inst.definition.destinationCityId);
                if (from != null && to != null)
                {
                    v.latA = from.location.latitude;  v.lonA = from.location.longitude;
                    v.latB = to.location.latitude;    v.lonB = to.location.longitude;
                }
            }

            v.truck = BuildTruckMarker();
            _visuals[inst.instanceId] = v;
            _ = LoadRouteAsync(v);
        }

        private void RemoveVisual(string id)
        {
            if (!_visuals.TryGetValue(id, out var v)) return;
            ClearSegments(v);
            if (v.truck != null) Destroy(v.truck.gameObject);
            _visuals.Remove(id);
        }

        // ── Chargement de la route (cache ORS ou réseau) ────────────────────────────
        private async Task LoadRouteAsync(RouteVisual v)
        {
            if (v.routeRequested || _mapSys?.Catalog == null) return;
            v.routeRequested = true;

            var from = _mapSys.Catalog.GetById(v.inst.definition.originCityId);
            var to   = _mapSys.Catalog.GetById(v.inst.definition.destinationCityId);
            if (from == null || to == null) return;

            RouteResult route = null;
            try { route = await _mapSys.GetRouteAsync(from, to, VehicleRoutingProfile.HeavyGoodsVehicle); }
            catch (Exception e) { Debug.LogWarning($"[FleetOverlay] route fetch failed: {e.Message}"); }

            // L'objet (ou le visuel) a pu disparaître pendant l'await.
            if (this == null) return;
            if (!_visuals.TryGetValue(v.inst.instanceId, out var current) || current != v) return;

            if (route != null && route.polyline != null && route.polyline.Count >= 2)
            {
                v.polyline = route.polyline;
                BuildCumulative(v);
            }
            RedrawVisual(v);
        }

        // ── Redessin ────────────────────────────────────────────────────────────────
        private void RedrawAll()
        {
            foreach (var v in _visuals.Values) RedrawVisual(v);
        }

        private void RedrawVisual(RouteVisual v)
        {
            ClearSegments(v);
            if (_map == null) return;

            var screen = new List<Vector2>();
            if (v.polyline != null && v.polyline.Count >= 2)
            {
                int step = Mathf.Max(1, v.polyline.Count / MaxSegments);
                for (int i = 0; i < v.polyline.Count; i += step)
                    screen.Add(_map.LatLonToLocal(v.polyline[i].latitude, v.polyline[i].longitude));
                var last = v.polyline[v.polyline.Count - 1];
                screen.Add(_map.LatLonToLocal(last.latitude, last.longitude));
            }
            else
            {
                screen.Add(_map.LatLonToLocal(v.latA, v.lonA));
                screen.Add(_map.LatLonToLocal(v.latB, v.lonB));
            }

            DrawPolyPass(v, screen, CasingColor, CasingThickness);
            DrawPolyPass(v, screen, CoreColor,   CoreThickness);

            PositionTruck(v);
        }

        private void UpdateTrucks()
        {
            foreach (var v in _visuals.Values) PositionTruck(v);
        }

        private void PositionTruck(RouteVisual v)
        {
            if (v.truck == null || _map == null) return;
            var g = GeoAtProgress(v, ProgressOf(v.inst));
            v.truck.anchoredPosition = _map.LatLonToLocal(g.latitude, g.longitude);
        }

        // ── Géométrie ─────────────────────────────────────────────────────────────
        private static float ProgressOf(ContractInstance inst)
        {
            long total = inst.completionTimeUtcTicks - inst.startTimeUtcTicks;
            if (total <= 0) return 1f;
            long elapsed = DateTime.UtcNow.Ticks - inst.startTimeUtcTicks;
            return Mathf.Clamp01((float)elapsed / total);
        }

        private static void BuildCumulative(RouteVisual v)
        {
            var p = v.polyline;
            v.cumKm = new double[p.Count];
            v.cumKm[0] = 0;
            for (int i = 1; i < p.Count; i++)
                v.cumKm[i] = v.cumKm[i - 1] + GeoPoint.HaversineKm(p[i - 1], p[i]);
        }

        // Position géographique du camion à une fraction d'avancement (0–1).
        private static GeoPoint GeoAtProgress(RouteVisual v, float progress)
        {
            if (v.polyline == null || v.polyline.Count < 2 || v.cumKm == null)
                return new GeoPoint(
                    v.latA + (v.latB - v.latA) * progress,
                    v.lonA + (v.lonB - v.lonA) * progress);

            var p = v.polyline;
            double total  = v.cumKm[p.Count - 1];
            double target = total * Mathf.Clamp01(progress);

            int i = 1;
            while (i < p.Count && v.cumKm[i] < target) i++;
            if (i >= p.Count) return p[p.Count - 1];

            double segLen = v.cumKm[i] - v.cumKm[i - 1];
            double t = segLen > 0 ? (target - v.cumKm[i - 1]) / segLen : 0;
            return new GeoPoint(
                p[i - 1].latitude  + (p[i].latitude  - p[i - 1].latitude)  * t,
                p[i - 1].longitude + (p[i].longitude - p[i - 1].longitude) * t);
        }

        // ── Tracé des segments ──────────────────────────────────────────────────────
        private void DrawPolyPass(RouteVisual v, List<Vector2> pts, Color color, float thickness)
        {
            for (int i = 0; i < pts.Count - 1; i++)
            {
                DrawSegment(v, pts[i], pts[i + 1], color, thickness);
                if (i > 0) DrawDot(v, pts[i], color, thickness);
            }
        }

        private void DrawSegment(RouteVisual v, Vector2 a, Vector2 b, Color color, float thickness)
        {
            Vector2 dir = b - a;
            float   len = dir.magnitude;
            if (len < 0.5f) return;

            var go  = new GameObject("S", typeof(RectTransform));
            go.transform.SetParent(_lineLayer, false);
            var img = go.AddComponent<Image>();
            img.color         = color;
            img.raycastTarget = false;

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = (a + b) * 0.5f;
            rt.sizeDelta        = new Vector2(len, thickness);
            rt.localRotation    = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);

            v.segments.Add(rt);
        }

        private void DrawDot(RouteVisual v, Vector2 p, Color color, float diameter)
        {
            var go  = new GameObject("J", typeof(RectTransform));
            go.transform.SetParent(_lineLayer, false);
            var img = go.AddComponent<Image>();
            img.sprite        = GetCapSprite();
            img.color         = color;
            img.raycastTarget = false;

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = p;
            rt.sizeDelta        = new Vector2(diameter, diameter);

            v.segments.Add(rt);
        }

        private void ClearSegments(RouteVisual v)
        {
            foreach (var s in v.segments)
                if (s != null) Destroy(s.gameObject);
            v.segments.Clear();
        }

        // ── Marqueur camion ─────────────────────────────────────────────────────────
        private RectTransform BuildTruckMarker()
        {
            var go = new GameObject("Truck", typeof(RectTransform));
            go.transform.SetParent(transform, false);   // après _lineLayer ⇒ au-dessus du tracé
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(TruckSize, TruckSize);

            var bg = go.AddComponent<Image>();
            bg.sprite        = GetCircleSprite();
            bg.color         = TruckBg;
            bg.raycastTarget = false;

            var iconGo = new GameObject("Icon", typeof(RectTransform));
            iconGo.transform.SetParent(go.transform, false);
            var iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin = iconRt.anchorMax = iconRt.pivot = new Vector2(0.5f, 0.5f);
            iconRt.anchoredPosition = Vector2.zero;
            iconRt.sizeDelta = new Vector2(TruckSize * 0.6f, TruckSize * 0.6f);

            var icon = iconGo.AddComponent<Image>();
            icon.sprite         = GetTruckSprite();
            icon.color          = Color.white;
            icon.preserveAspect = true;
            icon.raycastTarget  = false;

            return rt;
        }

        // ── Sprites partagés ──────────────────────────────────────────────────────
        private static Sprite GetTruckSprite()
        {
            if (_truckSprite == null) _truckSprite = Resources.Load<Sprite>("UI/Icons/icons/truck");
            return _truckSprite;
        }

        private static Sprite GetCapSprite()
        {
            if (_capSprite != null) return _capSprite;
            const int sz = 32;
            var tex = new Texture2D(sz, sz, TextureFormat.RGBA32, false)
            {
                wrapMode   = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            float c = (sz - 1) / 2f, r = sz / 2f - 0.5f;
            for (int y = 0; y < sz; y++)
                for (int x = 0; x < sz; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(r - d + 0.5f)));
                }
            tex.Apply();
            _capSprite = Sprite.Create(tex, new Rect(0, 0, sz, sz), new Vector2(0.5f, 0.5f));
            return _capSprite;
        }

        private static Sprite GetCircleSprite()
        {
            if (_circleSprite != null) return _circleSprite;
            const int sz = 64;
            var tex = new Texture2D(sz, sz, TextureFormat.RGBA32, false)
            {
                wrapMode   = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            float cx = sz / 2f, cy = sz / 2f, r = sz / 2f - 2f;
            Color outline = new Color(1f, 1f, 1f, 1f);
            for (int y = 0; y < sz; y++)
                for (int x = 0; x < sz; x++)
                {
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    tex.SetPixel(x, y,
                        d > r       ? new Color(0, 0, 0, 0) :
                        d > r - 3f  ? outline : Color.white);
                }
            tex.Apply();
            _circleSprite = Sprite.Create(tex, new Rect(0, 0, sz, sz), new Vector2(0.5f, 0.5f));
            return _circleSprite;
        }

        private static void StretchFill(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
