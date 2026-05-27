using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
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
        private HomeMarker       _homeMarker;
        private ContractsPanelView _contractsPanel;
        private RouteOverlayView  _routeOverlay;

        private void OnEnable()
        {
            GameEvents.OnShowContractRoute += HandleShowContractRoute;
        }

        private void OnDisable()
        {
            GameEvents.OnShowContractRoute -= HandleShowContractRoute;
        }

        private void Start()
        {
            _tileSize = (mapView != null && mapView.Config != null) ? mapView.Config.tilePixelSize : 256;
            EnsureRouteOverlay();   // en premier → rendu derrière le HomeMarker
            EnsureHomeMarker();
            EnsureContractsPanel();
            FocusOnHomeIfAvailable();
        }

        private void EnsureRouteOverlay()
        {
            if (_routeOverlay != null) return;
            var container = markersContainer != null ? markersContainer : (mapView != null ? (RectTransform)mapView.transform : null);
            if (container == null) return;

            var go = new GameObject("RouteOverlay", typeof(RectTransform));
            go.transform.SetParent(container, false);
            go.transform.SetAsFirstSibling(); // sous le HomeMarker
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
            _ = ShowContractRouteAsync(def);
        }

        private async Task ShowContractRouteAsync(ContractData def)
        {
            var mapSys = ServiceLocator.Get<MapSystem>();
            if (mapSys == null || def == null || mapView == null || _routeOverlay == null) return;

            var from = mapSys.Catalog?.GetById(def.originCityId);
            var to   = mapSys.Catalog?.GetById(def.destinationCityId);
            if (from == null || to == null) return;

            // 1. Zoom immédiat sur les deux villes
            mapView.FitBounds(from.location.latitude, from.location.longitude,
                              to.location.latitude,   to.location.longitude);

            // 2. Marqueurs A/B visibles tout de suite
            _routeOverlay.ShowMarkers(
                from.location.latitude, from.location.longitude,
                to.location.latitude,   to.location.longitude,
                from.displayName,       to.displayName);

            // 3. Récupérer la route (cache ORS ou appel réseau)
            var route = await mapSys.GetRouteAsync(from, to, VehicleRoutingProfile.HeavyGoodsVehicle);

            // Vérifier que l'objet est toujours valide après l'await
            if (_routeOverlay == null || mapView == null) return;
            _routeOverlay.SetRoute(route);
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
