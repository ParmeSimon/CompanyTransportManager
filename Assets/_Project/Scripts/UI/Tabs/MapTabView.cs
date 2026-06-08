using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.UI;
using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TransportManager.Core;
using TransportManager.Entities.Contracts;
using TransportManager.Enums;
using TransportManager.Events;
using TransportManager.Systems.Map;
using TransportManager.Systems.Map.Visualization;
using TransportManager.UI.Map;

namespace TransportManager.UI.Tabs
{
    public class MapTabView : MonoBehaviour, IPointerDownHandler, IDragHandler, IScrollHandler
    {
        [SerializeField] private SlippyMapView mapView;

        [Header("Interaction")]
        [SerializeField] private float panSensitivity = 0.0003f;
        [SerializeField] private float scrollZoomSensitivity = 1f;

        [Header("Markers container (RectTransform overlay on the map)")]
        [SerializeField] private RectTransform markersContainer;

        private int _tileSize;
        private HomeMarker          _homeMarker;
        private ContractsPanelView  _contractsPanel;
        private RouteOverlayView     _routeOverlay;
        private FleetMapOverlayView _fleetOverlay;

        // Prévisualisation de route (bouton « Rouvrir le contrat »)
        private GameObject   _reopenBar;
        private ContractData _previewedContract;
        private Sprite       _sprR12;

        private float _pinchPrevDist;

        private void OnEnable()
        {
            GameEvents.OnShowContractRoute += HandleShowContractRoute;
            GameEvents.OnContractStarted   += HandleContractStarted;
            EnhancedTouchSupport.Enable();   // active la lecture multi-touch (pinch)
        }

        private void OnDisable()
        {
            GameEvents.OnShowContractRoute -= HandleShowContractRoute;
            GameEvents.OnContractStarted   -= HandleContractStarted;
            EnhancedTouchSupport.Disable();
        }

        // Pinch à deux doigts → zoom carte (le scroll/molette ne marche que sur desktop).
        private void Update()
        {
            if (mapView == null) return;

            var touches = ETouch.activeTouches;
            if (touches.Count < 2) { _pinchPrevDist = 0f; return; }

            float dist = Vector2.Distance(touches[0].screenPosition, touches[1].screenPosition);
            if (_pinchPrevDist <= 0f) { _pinchPrevDist = dist; return; }

            const float step = 80f;   // pixels d'écart avant un cran de zoom
            float delta = dist - _pinchPrevDist;
            if (delta >  step) { mapView.ZoomIn();  _pinchPrevDist = dist; }
            else if (delta < -step) { mapView.ZoomOut(); _pinchPrevDist = dist; }
        }

        private void Start()
        {
            _tileSize = (mapView != null && mapView.Config != null) ? mapView.Config.tilePixelSize : 256;
            EnsureRouteOverlay();   // en premier → rendu derrière le HomeMarker
            EnsureFleetOverlay();   // trajets actifs (trait fin + camions)
            EnsureHomeMarker();
            EnsureContractsPanel();
            FocusOnHomeIfAvailable();
        }

        private void EnsureFleetOverlay()
        {
            if (_fleetOverlay != null) return;
            var container = markersContainer != null ? markersContainer : (mapView != null ? (RectTransform)mapView.transform : null);
            if (container == null) return;

            var go = new GameObject("FleetMapOverlay", typeof(RectTransform));
            go.transform.SetParent(container, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            _fleetOverlay = go.AddComponent<FleetMapOverlayView>();
            _fleetOverlay.Init(mapView);
        }

        private void EnsureRouteOverlay()
        {
            if (_routeOverlay != null) return;
            var container = markersContainer != null ? markersContainer : (mapView != null ? (RectTransform)mapView.transform : null);
            if (container == null) return;

            var go = new GameObject("RouteOverlay", typeof(RectTransform));
            go.transform.SetParent(container, false);
            // NOTE : ne PAS faire SetAsFirstSibling — le conteneur partage souvent son
            // parent avec les tuiles de carte ; placé en premier, le tracé passait
            // DERRIÈRE les tuiles et restait invisible. Ajouté en dernier, il se rend
            // au-dessus des tuiles ; le HomeMarker (créé juste après) reste au-dessus.
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            _routeOverlay = go.AddComponent<RouteOverlayView>();
            _routeOverlay.Init(mapView);
            _routeOverlay.gameObject.SetActive(false);
        }

        private void HandleShowContractRoute(ContractData def)
        {
            _previewedContract = def;
            ShowReopenBar();
            _ = ShowContractRouteAsync(def);
        }

        // Un contrat vient de démarrer : la prévisualisation épaisse n'a plus lieu
        // d'être (le trajet actif apparaît désormais en trait fin via l'overlay flotte).
        private void HandleContractStarted(ContractInstance _)
        {
            ClearPreview();
        }

        // ── Barre flottante « Rouvrir le contrat » ──────────────────────────────────
        private void ShowReopenBar()
        {
            if (_reopenBar == null) BuildReopenBar();
            if (_reopenBar != null) _reopenBar.SetActive(true);
        }

        private void ClearPreview()
        {
            _previewedContract = null;
            if (_reopenBar != null) _reopenBar.SetActive(false);
            if (_routeOverlay != null) _routeOverlay.Hide();
        }

        private void ReopenPreviewedContract()
        {
            if (_contractsPanel != null && _previewedContract != null)
                _contractsPanel.ShowContractByDefinition(_previewedContract);
        }

        private void BuildReopenBar()
        {
            EnsureRoundedSprites();

            _reopenBar = new GameObject("ReopenContractBar", typeof(RectTransform));
            _reopenBar.transform.SetParent(transform, false);
            var rt = _reopenBar.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot     = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 24f);
            rt.sizeDelta        = new Vector2(304f, 52f);

            var hlg = _reopenBar.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing               = 8f;
            hlg.childAlignment        = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth  = false;
            hlg.childForceExpandHeight = true;
            hlg.childControlWidth      = true;
            hlg.childControlHeight     = true;

            // Bouton principal
            var reopenBtn = MakeBarButton("Reopen", "Rouvrir le contrat",
                new Color(0.22f, 0.52f, 1f, 1f), 240f);
            reopenBtn.onClick.AddListener(ReopenPreviewedContract);

            // Bouton fermeture (masque la prévisualisation) — icône x.png
            var closeBtn = MakeBarButton("Close", "",
                new Color(0.17f, 0.18f, 0.22f, 1f), 52f);
            AddCenterIcon(closeBtn.transform, "x", new Color(0.85f, 0.88f, 0.93f), 18f);
            closeBtn.onClick.AddListener(ClearPreview);
        }

        // Icône centrée sur un bouton (Resources/UI/Icons/icons/<iconName>).
        private static void AddCenterIcon(Transform parent, string iconName, Color color, float size)
        {
            var spr = Resources.Load<Sprite>($"UI/Icons/icons/{iconName}");
            if (spr == null) return;
            var go  = new GameObject("Icon", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.sprite         = spr;
            img.color          = color;
            img.preserveAspect = true;
            img.raycastTarget  = false;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.sizeDelta        = new Vector2(size, size);
            rt.anchoredPosition = Vector2.zero;
        }

        private Button MakeBarButton(string name, string label, Color color, float width)
        {
            var go  = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(_reopenBar.transform, false);
            var img = go.AddComponent<Image>();
            img.sprite = _sprR12;
            img.type   = Image.Type.Sliced;
            img.color  = color;
            var sh = go.AddComponent<Shadow>();
            sh.effectColor    = new Color(0f, 0f, 0f, 0.5f);
            sh.effectDistance = new Vector2(0f, -3f);

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition    = Selectable.Transition.None;

            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth  = width;
            le.preferredHeight = 52f;

            var txtGo = new GameObject("L", typeof(RectTransform));
            txtGo.transform.SetParent(go.transform, false);
            var txtRt = txtGo.GetComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = txtRt.offsetMax = Vector2.zero;
            var tmp = txtGo.AddComponent<TextMeshProUGUI>();
            tmp.text          = label;
            tmp.fontSize      = 15f;
            tmp.fontStyle     = FontStyles.Bold;
            tmp.color         = Color.white;
            tmp.alignment     = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;

            return btn;
        }

        private void EnsureRoundedSprites()
        {
            if (_sprR12 != null) return;
            _sprR12 = MakeRoundedSprite(12);
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
            float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
            return Mathf.Clamp01(r - d + 0.5f);
        }

        private async Task ShowContractRouteAsync(ContractData def)
        {
            var mapSys = ServiceLocator.Get<MapSystem>();
            if (mapSys == null || def == null || mapView == null || _routeOverlay == null) return;

            // Liste ordonnée des villes : origine → escales → destination.
            var cities = new System.Collections.Generic.List<Entities.Map.CityEntry>();
            var origin = mapSys.Catalog?.GetById(def.originCityId);
            if (origin == null) return;
            cities.Add(origin);
            if (def.isMultiStop && def.viaCityIds != null)
                foreach (var vc in def.viaCityIds)
                {
                    var c = mapSys.Catalog?.GetById(vc);
                    if (c != null) cities.Add(c);
                }
            var dest = mapSys.Catalog?.GetById(def.destinationCityId);
            if (dest == null) return;
            cities.Add(dest);

            // 1. Cadrage sur l'ensemble des arrêts + marqueurs (avec escales) tout de suite
            double minLat = double.MaxValue, maxLat = -double.MaxValue;
            double minLon = double.MaxValue, maxLon = -double.MaxValue;
            var stops = new System.Collections.Generic.List<(double, double, string)>();
            foreach (var c in cities)
            {
                double la = c.location.latitude, lo = c.location.longitude;
                stops.Add((la, lo, c.displayName));
                if (la < minLat) minLat = la; if (la > maxLat) maxLat = la;
                if (lo < minLon) minLon = lo; if (lo > maxLon) maxLon = lo;
            }
            mapView.FitBounds(minLat, minLon, maxLat, maxLon);
            _routeOverlay.ShowRouteStops(stops);

            // 2. Récupérer chaque segment et concaténer la polyline complète (passe par les escales)
            var combined = new Entities.Map.RouteResult { found = true };
            for (int i = 0; i < cities.Count - 1; i++)
            {
                var leg = await mapSys.GetRouteAsync(cities[i], cities[i + 1], VehicleRoutingProfile.HeavyGoodsVehicle);
                if (_routeOverlay == null || mapView == null) return;   // toujours valide après l'await ?

                if (leg != null && leg.found && leg.polyline != null && leg.polyline.Count >= 2)
                    combined.polyline.AddRange(leg.polyline);
                else
                {
                    combined.polyline.Add(new Entities.Map.GeoPoint(cities[i].location.latitude, cities[i].location.longitude));
                    combined.polyline.Add(new Entities.Map.GeoPoint(cities[i + 1].location.latitude, cities[i + 1].location.longitude));
                }
            }

            Debug.Log($"[Route] {origin.id}->{dest.id} legs={cities.Count - 1} points={combined.polyline.Count}");
            _routeOverlay.SetRoute(combined);
        }

        private void EnsureHomeMarker()
        {
            if (_homeMarker != null) return;
            var container = markersContainer != null ? markersContainer : (mapView != null ? (RectTransform)mapView.transform : null);
            if (container == null) return;

            var go = new GameObject("HomeMarker", typeof(RectTransform));
            go.transform.SetParent(container, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            _homeMarker = go.AddComponent<HomeMarker>();
            _homeMarker.Init(mapView, rt);
        }

        private void EnsureContractsPanel()
        {
            if (_contractsPanel != null) return;
            var go = new GameObject("ContractsPanel", typeof(RectTransform));
            go.transform.SetParent((RectTransform)transform, false);
            _contractsPanel = go.AddComponent<ContractsPanelView>();
            _contractsPanel.Build();
        }

        private void FocusOnHomeIfAvailable()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Save == null || mapView == null) return;
            var c = gm.Save.company;
            if (c != null && c.hasLocationCoordinates)
                mapView.SetView(c.locationLatitude, c.locationLongitude, Mathf.Max(mapView.Zoom, 10));
        }

        public void OnPointerDown(PointerEventData eventData) { }

        public void OnDrag(PointerEventData eventData)
        {
            if (mapView == null) return;
            if (ETouch.activeTouches.Count >= 2) return;   // pinch en cours → pas de pan

            float metersPerPixel = MetersPerPixel(mapView.CenterLatitude, mapView.Zoom, _tileSize);
            double deltaLon = -eventData.delta.x * metersPerPixel / (111320.0 * System.Math.Cos(mapView.CenterLatitude * System.Math.PI / 180.0));
            double deltaLat = -eventData.delta.y * metersPerPixel / 111320.0;
            mapView.Pan(deltaLat, deltaLon);
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (mapView == null) return;
            if (eventData.scrollDelta.y > 0)
                mapView.ZoomIn();
            else if (eventData.scrollDelta.y < 0)
                mapView.ZoomOut();
        }

        private static float MetersPerPixel(double latitude, int zoom, int tileSize)
        {
            double earthCircumference = 2 * System.Math.PI * 6378137.0;
            double tilesAtZoom = System.Math.Pow(2, zoom);
            return (float)(earthCircumference * System.Math.Cos(latitude * System.Math.PI / 180.0) / (tilesAtZoom * tileSize));
        }
    }
}
