using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using TransportManager.Core;
using TransportManager.Systems.Map.Visualization;
using TransportManager.Systems.Map.Geocoding;
using TransportManager.UI.Map;

namespace TransportManager.UI.Tabs
{
    public class MapTabView : MonoBehaviour, IPointerDownHandler, IDragHandler, IScrollHandler
    {
        [SerializeField] private SlippyMapView mapView;
        [SerializeField] private TMP_Text attributionLabel;

        [Header("Interaction")]
        [SerializeField] private float panSensitivity = 0.0003f;
        [SerializeField] private float scrollZoomSensitivity = 1f;

        [Header("Markers container (RectTransform overlay on the map)")]
        [SerializeField] private RectTransform markersContainer;

        private int _tileSize;
        private HomeMarker _homeMarker;

        private void OnEnable()
        {
            if (attributionLabel && mapView != null && mapView.Config != null)
                attributionLabel.text = mapView.Config.attribution;
        }

        private void Start()
        {
            _tileSize = (mapView != null && mapView.Config != null) ? mapView.Config.tilePixelSize : 256;
            EnsureHomeMarker();
            FocusOnHomeIfAvailable();
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
            var t = typeof(HomeMarker);
            t.GetField("mapView", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(_homeMarker, mapView);
            t.GetField("markerContainer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(_homeMarker, rt);
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
