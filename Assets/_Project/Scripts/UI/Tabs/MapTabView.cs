using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TransportManager.Systems.Map.Visualization;

namespace TransportManager.UI.Tabs
{
    public class MapTabView : MonoBehaviour
    {
        [SerializeField] private SlippyMapView mapView;
        [SerializeField] private Button zoomInButton;
        [SerializeField] private Button zoomOutButton;
        [SerializeField] private TMP_Text attributionLabel;

        private void OnEnable()
        {
            if (zoomInButton) zoomInButton.onClick.AddListener(OnZoomIn);
            if (zoomOutButton) zoomOutButton.onClick.AddListener(OnZoomOut);
            if (attributionLabel && mapView != null && mapView.Config != null)
                attributionLabel.text = mapView.Config.attribution;
        }

        private void OnDisable()
        {
            if (zoomInButton) zoomInButton.onClick.RemoveListener(OnZoomIn);
            if (zoomOutButton) zoomOutButton.onClick.RemoveListener(OnZoomOut);
        }

        private void OnZoomIn() => mapView?.ZoomIn();
        private void OnZoomOut() => mapView?.ZoomOut();

        // TODO next pass: PolylineOverlay for active contracts, VehicleMarker for in-progress trips.
    }
}
