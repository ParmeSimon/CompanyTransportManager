using UnityEngine;
using UnityEngine.UI;
using TransportManager.Core;
using TransportManager.Systems.Map.Visualization;

namespace TransportManager.UI.Map
{
    [RequireComponent(typeof(RectTransform))]
    public class HomeMarker : MonoBehaviour
    {
        [SerializeField] private SlippyMapView mapView;
        [SerializeField] private RectTransform markerContainer;
        [SerializeField] private float markerSize = 56f;

        private RectTransform _markerRt;
        private Image _markerImg;
        private bool _hasCoords;
        private double _lat, _lng;

        public void Init(SlippyMapView mv, RectTransform container)
        {
            mapView         = mv;
            markerContainer = container;
            BuildMarker();
            ReadCoordsFromSave();
            if (mapView != null) mapView.OnViewChanged += Reposition;
            Reposition();
        }

        private void OnEnable()
        {
            // Subscription is handled by Init(); re-subscribe only on subsequent enables.
            if (mapView != null) mapView.OnViewChanged += Reposition;
            Reposition();
        }

        private void OnDisable()
        {
            if (mapView != null) mapView.OnViewChanged -= Reposition;
        }

        public void SetCoordinates(double latitude, double longitude)
        {
            _lat = latitude;
            _lng = longitude;
            _hasCoords = true;
            Reposition();
        }

        private void ReadCoordsFromSave()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Save == null) return;
            var c = gm.Save.company;
            if (c != null && c.hasLocationCoordinates)
            {
                _lat = c.locationLatitude;
                _lng = c.locationLongitude;
                _hasCoords = true;
            }
            else
            {
                _hasCoords = false;
            }
        }

        private void BuildMarker()
        {
            var go = new GameObject("HomeMarker_Pin", typeof(RectTransform));
            go.transform.SetParent(markerContainer != null ? markerContainer : (RectTransform)transform, false);
            _markerImg = go.AddComponent<Image>();
            _markerImg.sprite = MakePinSprite();
            _markerImg.raycastTarget = false;
            _markerRt = go.GetComponent<RectTransform>();
            _markerRt.sizeDelta = new Vector2(markerSize, markerSize);
            _markerRt.pivot = new Vector2(0.5f, 0f); // bottom-center of pin
            _markerRt.anchorMin = new Vector2(0.5f, 0.5f);
            _markerRt.anchorMax = new Vector2(0.5f, 0.5f);
            _markerRt.gameObject.SetActive(false);
        }

        private void Reposition()
        {
            if (_markerRt == null) return;
            if (!_hasCoords || mapView == null)
            {
                _markerRt.gameObject.SetActive(false);
                return;
            }
            _markerRt.gameObject.SetActive(true);
            var local = mapView.LatLonToLocal(_lat, _lng);
            _markerRt.anchoredPosition = local;
        }

        private static Sprite _pinSprite;
        private static Sprite MakePinSprite()
        {
            if (_pinSprite != null) return _pinSprite;
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            // Pin shape: teardrop with small hole at top, anchored at bottom
            // - Outline color: dark red
            // - Fill color: bright red
            // - Inner circle: white (home symbol placeholder)
            Color fill = new Color(0.85f, 0.20f, 0.20f, 1f);
            Color outline = new Color(0.35f, 0.05f, 0.05f, 1f);
            Color inner = new Color(1f, 1f, 1f, 1f);
            Color clear = new Color(0, 0, 0, 0);

            float cx = size * 0.5f;
            float circleCy = size * 0.62f;
            float circleRadius = size * 0.32f;
            float tipY = size * 0.06f;
            float outlineWidth = 3f;
            float innerRadius = circleRadius * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - cx;
                    float distFromCircle = Mathf.Sqrt(dx * dx + (y - circleCy) * (y - circleCy));

                    bool inCircle = distFromCircle <= circleRadius;

                    // Triangular tail going from circle bottom to tipY
                    bool inTail = false;
                    if (y >= tipY && y <= circleCy)
                    {
                        float t = (y - tipY) / (circleCy - tipY);
                        float halfWidth = Mathf.Lerp(0.5f, circleRadius, t);
                        if (Mathf.Abs(dx) <= halfWidth) inTail = true;
                    }

                    bool inShape = inCircle || inTail;
                    bool inOutline = false;
                    if (inShape)
                    {
                        // Outline = within outlineWidth of the shape boundary
                        bool circleOutline = (distFromCircle >= circleRadius - outlineWidth) && inCircle;
                        bool tailOutline = false;
                        if (inTail && !inCircle)
                        {
                            float t = (y - tipY) / (circleCy - tipY);
                            float halfWidth = Mathf.Lerp(0.5f, circleRadius, t);
                            if (Mathf.Abs(Mathf.Abs(dx) - halfWidth) < outlineWidth) tailOutline = true;
                        }
                        inOutline = circleOutline || tailOutline;
                    }

                    Color c;
                    if (!inShape) c = clear;
                    else if (inOutline) c = outline;
                    else
                    {
                        // Inner home symbol = white disc inside the head
                        float dToInner = Mathf.Sqrt(dx * dx + (y - circleCy) * (y - circleCy));
                        c = dToInner <= innerRadius ? inner : fill;
                    }
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            _pinSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0f));
            return _pinSprite;
        }
    }
}
