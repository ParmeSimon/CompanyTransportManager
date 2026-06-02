using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TransportManager.Entities.Map;
using TransportManager.Systems.Map.Visualization;

namespace TransportManager.UI.Map
{
    /// Dessine la polyline d'un contrat et place les marqueurs A / B sur la carte.
    [RequireComponent(typeof(RectTransform))]
    public class RouteOverlayView : MonoBehaviour
    {
        // ── Couleurs (style Google / Waze : liseré sombre + cœur bleu vif) ──────────
        private static readonly Color CoreColor   = new Color(0.26f, 0.56f, 0.98f, 1f);   // bleu vif
        private static readonly Color CasingColor = new Color(0.10f, 0.24f, 0.52f, 1f);   // liseré sombre
        private static readonly Color ColorA      = new Color(0.20f, 0.72f, 0.38f, 1f);   // vert
        private static readonly Color ColorB      = new Color(0.95f, 0.35f, 0.25f, 1f);   // rouge-orange
        private static readonly Color LabelBg     = new Color(0.08f, 0.10f, 0.15f, 0.88f);

        private const float CoreThickness   = 7f;   // épaisseur du tracé bleu
        private const float CasingThickness = 11f;  // liseré sombre dessous
        private const int   MaxSegments     = 250;  // décimation si +de points
        private const float CircleSize    = 38f;
        private const float LabelW        = 96f;
        private const float LabelH        = 18f;
        private const float LabelOffsetY  = -(CircleSize * 0.5f + LabelH * 0.5f + 3f);

        // ── Références ────────────────────────────────────────────────────────────
        private SlippyMapView  _map;
        private RectTransform  _lineLayer;  // enfant pour les segments (rendu en dessous)

        // Marqueurs A / B
        private RectTransform  _circleA, _circleB;
        private RectTransform  _labelA,  _labelB;

        // ── Données courantes ─────────────────────────────────────────────────────
        private RouteResult    _route;
        private double         _latA, _lonA, _latB, _lonB;
        private readonly List<RectTransform> _segments = new List<RectTransform>();
        private static Sprite  _capSprite;   // disque doux pour caps/joints arrondis

        // Escales intermédiaires (tournées multi-arrêts) + liste ordonnée de tous les arrêts.
        private readonly List<(RectTransform circle, RectTransform label, double lat, double lon)> _viaMarkers
            = new List<(RectTransform, RectTransform, double, double)>();
        private readonly List<(double lat, double lon)> _stops = new List<(double, double)>();
        private static readonly Color ColorVia = new Color(0.26f, 0.56f, 0.98f, 1f); // bleu (= tracé)

        // ── Init ──────────────────────────────────────────────────────────────────
        public void Init(SlippyMapView map)
        {
            _map = map;

            // Couche segments — premier enfant = rendu derrière les marqueurs
            var lg = new GameObject("Lines", typeof(RectTransform));
            lg.transform.SetParent(transform, false);
            _lineLayer = lg.GetComponent<RectTransform>();
            StretchFill(_lineLayer);

            // Pré-créer les marqueurs A et B (toujours présents, juste repositionnés)
            (_circleA, _labelA) = BuildMarker("A", ColorA);
            (_circleB, _labelB) = BuildMarker("B", ColorB);
            SetMarkersVisible(false);

            if (_map != null) _map.OnViewChanged += Redraw;
        }

        private void OnDestroy()
        {
            if (_map != null) _map.OnViewChanged -= Redraw;
        }

        // ── API publique ──────────────────────────────────────────────────────────

        /// Affiche les marqueurs A/B immédiatement, sans attendre la polyline.
        public void ShowMarkers(double latA, double lonA, double latB, double lonB,
                                string nameA, string nameB)
        {
            ShowRouteStops(new List<(double, double, string)>
            {
                (latA, lonA, nameA),
                (latB, lonB, nameB)
            });
        }

        /// Affiche un trajet à arrêts multiples : stops[0] = origine, dernier = destination,
        /// les autres = escales intermédiaires (marqueurs numérotés bleus).
        public void ShowRouteStops(List<(double lat, double lon, string name)> stops)
        {
            if (stops == null || stops.Count < 2) return;

            _stops.Clear();
            foreach (var s in stops) _stops.Add((s.lat, s.lon));

            // Extrémités → marqueurs A (vert) / B (rouge)
            _latA = stops[0].lat;                 _lonA = stops[0].lon;
            _latB = stops[stops.Count - 1].lat;   _lonB = stops[stops.Count - 1].lon;
            SetMarkerLabel(_labelA, stops[0].name);
            SetMarkerLabel(_labelB, stops[stops.Count - 1].name);

            // Escales intermédiaires → marqueurs bleus numérotés
            ClearViaMarkers();
            for (int i = 1; i < stops.Count - 1; i++)
            {
                var (circle, label) = BuildMarker(i.ToString(), ColorVia);
                SetMarkerLabel(label, stops[i].name);
                _viaMarkers.Add((circle, label, stops[i].lat, stops[i].lon));
            }

            gameObject.SetActive(true);
            SetMarkersVisible(true);
            RepositionMarkers();
        }

        /// Ajoute la polyline (peut être appelé après ShowMarkers une fois la route chargée).
        public void SetRoute(RouteResult route)
        {
            _route = route;
            RedrawPolyline();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            ClearSegments();
            _route = null;
        }

        // ── Redessin ──────────────────────────────────────────────────────────────
        private void Redraw()
        {
            if (!gameObject.activeSelf) return;
            RepositionMarkers();
            RedrawPolyline();
        }

        private void RepositionMarkers()
        {
            if (_map == null) return;
            MoveMarker(_circleA, _labelA, _latA, _lonA);
            MoveMarker(_circleB, _labelB, _latB, _lonB);
            foreach (var v in _viaMarkers) MoveMarker(v.circle, v.label, v.lat, v.lon);
        }

        private void ClearViaMarkers()
        {
            foreach (var v in _viaMarkers)
            {
                if (v.circle != null) Destroy(v.circle.gameObject);
                if (v.label  != null) Destroy(v.label.gameObject);
            }
            _viaMarkers.Clear();
        }

        private void MoveMarker(RectTransform circle, RectTransform label, double lat, double lon)
        {
            if (circle == null || _map == null) return;
            var pos = _map.LatLonToLocal(lat, lon);
            circle.anchoredPosition = pos;
            label.anchoredPosition  = pos + new Vector2(0f, LabelOffsetY);
        }

        private void RedrawPolyline()
        {
            ClearSegments();
            if (_map == null) return;

            // Liste de points écran : polyline réelle, sinon ligne droite A→B en repli.
            var screen = new List<Vector2>();
            if (_route != null && _route.polyline != null && _route.polyline.Count >= 2)
            {
                var pts = _route.polyline;
                int step = Mathf.Max(1, pts.Count / MaxSegments);
                for (int i = 0; i < pts.Count; i += step)
                    screen.Add(_map.LatLonToLocal(pts[i].latitude, pts[i].longitude));
                var last = pts[pts.Count - 1];
                screen.Add(_map.LatLonToLocal(last.latitude, last.longitude));
            }
            else if (_stops.Count >= 2)
            {
                // Repli : relie en ligne droite tous les arrêts dans l'ordre (origine → escales → destination).
                foreach (var s in _stops)
                    screen.Add(_map.LatLonToLocal(s.lat, s.lon));
            }
            else
            {
                screen.Add(_map.LatLonToLocal(_latA, _lonA));
                screen.Add(_map.LatLonToLocal(_latB, _lonB));
            }

            // Deux passes : d'abord le liseré sombre (plus épais), puis le cœur bleu
            // par-dessus. Des disques aux sommets lissent les angles (style Waze).
            DrawPolyPass(screen, CasingColor, CasingThickness);
            DrawPolyPass(screen, CoreColor,   CoreThickness);
        }

        private void DrawPolyPass(List<Vector2> pts, Color color, float thickness)
        {
            for (int i = 0; i < pts.Count - 1; i++)
            {
                DrawSegment(pts[i], pts[i + 1], color, thickness);
                if (i > 0) DrawDot(pts[i], color, thickness);   // joint arrondi
            }
        }

        private void DrawSegment(Vector2 a, Vector2 b, Color color, float thickness)
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
            rt.pivot      = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = (a + b) * 0.5f;
            rt.sizeDelta        = new Vector2(len, thickness);
            rt.localRotation    = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);

            _segments.Add(rt);
        }

        private void DrawDot(Vector2 p, Color color, float diameter)
        {
            var go  = new GameObject("J", typeof(RectTransform));
            go.transform.SetParent(_lineLayer, false);
            var img = go.AddComponent<Image>();
            img.sprite        = GetCapSprite();
            img.color         = color;
            img.raycastTarget = false;

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot      = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = p;
            rt.sizeDelta        = new Vector2(diameter, diameter);

            _segments.Add(rt);
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

        private void ClearSegments()
        {
            foreach (var s in _segments)
                if (s != null) Destroy(s.gameObject);
            _segments.Clear();
        }

        // ── Construction des marqueurs ────────────────────────────────────────────
        private (RectTransform circle, RectTransform label) BuildMarker(string letter, Color color)
        {
            // Cercle coloré avec lettre
            var cGo = new GameObject("Mk_" + letter, typeof(RectTransform));
            cGo.transform.SetParent(transform, false);
            var cRt = cGo.GetComponent<RectTransform>();
            cRt.anchorMin = cRt.anchorMax = new Vector2(0.5f, 0.5f);
            cRt.pivot     = new Vector2(0.5f, 0.5f);
            cRt.sizeDelta = new Vector2(CircleSize, CircleSize);

            var img = cGo.AddComponent<Image>();
            img.sprite        = MakeCircleSprite(color);
            img.raycastTarget = false;

            var tGo = new GameObject("T", typeof(RectTransform));
            tGo.transform.SetParent(cGo.transform, false);
            var tRt = tGo.GetComponent<RectTransform>();
            tRt.anchorMin = Vector2.zero;
            tRt.anchorMax = Vector2.one;
            tRt.offsetMin = tRt.offsetMax = Vector2.zero;
            var tmp = tGo.AddComponent<TextMeshProUGUI>();
            tmp.text          = letter;
            tmp.fontSize      = 16f;
            tmp.fontStyle     = FontStyles.Bold;
            tmp.color         = Color.white;
            tmp.alignment     = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;

            // Badge nom de ville (sous le cercle)
            var lGo = new GameObject("Lbl_" + letter, typeof(RectTransform));
            lGo.transform.SetParent(transform, false);
            var lRt = lGo.GetComponent<RectTransform>();
            lRt.anchorMin = lRt.anchorMax = new Vector2(0.5f, 0.5f);
            lRt.pivot     = new Vector2(0.5f, 0.5f);
            lRt.sizeDelta = new Vector2(LabelW, LabelH);

            var bgImg = lGo.AddComponent<Image>();
            bgImg.color        = LabelBg;
            bgImg.raycastTarget = false;

            var ntGo = new GameObject("T", typeof(RectTransform));
            ntGo.transform.SetParent(lGo.transform, false);
            var ntRt = ntGo.GetComponent<RectTransform>();
            ntRt.anchorMin = Vector2.zero;
            ntRt.anchorMax = Vector2.one;
            ntRt.offsetMin = new Vector2(4f, 0f);
            ntRt.offsetMax = new Vector2(-4f, 0f);
            var ntTmp = ntGo.AddComponent<TextMeshProUGUI>();
            ntTmp.fontSize         = 9f;
            ntTmp.fontStyle        = FontStyles.Bold;
            ntTmp.color            = Color.white;
            ntTmp.alignment        = TextAlignmentOptions.Center;
            ntTmp.textWrappingMode = TextWrappingModes.NoWrap;
            ntTmp.overflowMode     = TextOverflowModes.Ellipsis;
            ntTmp.raycastTarget    = false;

            return (cRt, lRt);
        }

        private static void SetMarkerLabel(RectTransform labelRt, string text)
        {
            if (labelRt == null) return;
            var tmp = labelRt.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null) tmp.text = text;
        }

        private void SetMarkersVisible(bool visible)
        {
            if (_circleA != null) _circleA.gameObject.SetActive(visible);
            if (_circleB != null) _circleB.gameObject.SetActive(visible);
            if (_labelA  != null) _labelA.gameObject.SetActive(visible);
            if (_labelB  != null) _labelB.gameObject.SetActive(visible);
            foreach (var v in _viaMarkers)
            {
                if (v.circle != null) v.circle.gameObject.SetActive(visible);
                if (v.label  != null) v.label.gameObject.SetActive(visible);
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────────
        private static void StretchFill(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static Sprite MakeCircleSprite(Color fill)
        {
            const int sz = 64;
            var tex = new Texture2D(sz, sz, TextureFormat.RGBA32, false)
            {
                wrapMode   = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            float cx = sz / 2f, cy = sz / 2f, r = sz / 2f - 2f;
            Color outline = new Color(fill.r * 0.35f, fill.g * 0.35f, fill.b * 0.35f, 1f);
            for (int y = 0; y < sz; y++)
                for (int x = 0; x < sz; x++)
                {
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    tex.SetPixel(x, y,
                        d > r       ? new Color(0, 0, 0, 0) :
                        d > r - 3f  ? outline : fill);
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, sz, sz), new Vector2(0.5f, 0.5f));
        }
    }
}
