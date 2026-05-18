using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using TransportManager.Systems.Map.Visualization;

namespace TransportManager.UI.Tabs
{
    public class MapTabView : MonoBehaviour, IPointerDownHandler, IDragHandler, IScrollHandler
    {
        [SerializeField] private SlippyMapView mapView;
        [SerializeField] private TMP_Text attributionLabel;

        [Header("Interaction")]
        [SerializeField] private float panSensitivity = 0.0003f;
        [SerializeField] private float scrollZoomSensitivity = 1f;

        private int _tileSize;

        private void OnEnable()
        {
            if (attributionLabel && mapView != null && mapView.Config != null)
                attributionLabel.text = mapView.Config.attribution;
        }

        private void Start()
        {
            _tileSize = (mapView != null && mapView.Config != null) ? mapView.Config.tilePixelSize : 256;
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
