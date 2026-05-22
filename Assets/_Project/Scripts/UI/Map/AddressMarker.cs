using UnityEngine;
using UnityEngine.UI;
using TransportManager.Systems.Map.Visualization;

namespace TransportManager.UI.Map
{
    public class AddressMarker : MonoBehaviour
    {
        private SlippyMapView _mapView;
        private RectTransform _pinRt;
        private bool _hasCoords;
        private double _lat, _lng;

        public void Init(SlippyMapView mapView, RectTransform container)
        {
            _mapView = mapView;
            BuildPin(container);
        }

        private void OnEnable()
        {
            if (_mapView != null) _mapView.OnViewChanged += Reposition;
            Reposition();
        }

        private void OnDisable()
        {
            if (_mapView != null) _mapView.OnViewChanged -= Reposition;
        }

        public void PlaceAt(double lat, double lng)
        {
            _lat = lat;
            _lng = lng;
            _hasCoords = true;
            Reposition();
        }

        public void Clear()
        {
            _hasCoords = false;
            if (_pinRt != null) _pinRt.gameObject.SetActive(false);
        }

        private void BuildPin(RectTransform container)
        {
            var go = new GameObject("AddressPin", typeof(RectTransform));
            go.transform.SetParent(container, false);
            var img = go.AddComponent<Image>();
            img.sprite = MakeBluePinSprite();
            img.raycastTarget = false;
            _pinRt = go.GetComponent<RectTransform>();
            _pinRt.sizeDelta = new Vector2(40f, 48f);
            _pinRt.pivot = new Vector2(0.5f, 0f);
            _pinRt.anchorMin = new Vector2(0.5f, 0.5f);
            _pinRt.anchorMax = new Vector2(0.5f, 0.5f);
            _pinRt.gameObject.SetActive(false);
        }

        private void Reposition()
        {
            if (_pinRt == null || !_hasCoords || _mapView == null)
            {
                if (_pinRt != null) _pinRt.gameObject.SetActive(false);
                return;
            }
            _pinRt.gameObject.SetActive(true);
            _pinRt.anchoredPosition = _mapView.LatLonToLocal(_lat, _lng);
        }

        private static Sprite _sprite;
        private static Sprite MakeBluePinSprite()
        {
            if (_sprite != null) return _sprite;
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            var fill    = new Color(0.18f, 0.53f, 0.95f, 1f);
            var outline = new Color(0.05f, 0.15f, 0.50f, 1f);
            var clear   = new Color(0f, 0f, 0f, 0f);

            float cx       = size * 0.5f;
            float circleCy = size * 0.62f;
            float circleR  = size * 0.32f;
            float tipY     = size * 0.06f;
            const float outlineW = 3f;
            float innerR   = circleR * 0.38f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx   = x - cx;
                    float dist = Mathf.Sqrt(dx * dx + (y - circleCy) * (y - circleCy));
                    bool inCircle = dist <= circleR;
                    bool inTail = false;
                    if (y >= tipY && y <= circleCy)
                    {
                        float t  = (y - tipY) / (circleCy - tipY);
                        float hw = Mathf.Lerp(0.5f, circleR, t);
                        if (Mathf.Abs(dx) <= hw) inTail = true;
                    }
                    if (!inCircle && !inTail) { tex.SetPixel(x, y, clear); continue; }

                    bool isOutline = inCircle && dist >= circleR - outlineW;
                    if (!isOutline && inTail && !inCircle)
                    {
                        float t  = (y - tipY) / (circleCy - tipY);
                        float hw = Mathf.Lerp(0.5f, circleR, t);
                        isOutline = Mathf.Abs(Mathf.Abs(dx) - hw) < outlineW;
                    }

                    tex.SetPixel(x, y, isOutline ? outline : (dist <= innerR ? Color.white : fill));
                }
            }
            tex.Apply();
            _sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0f));
            return _sprite;
        }
    }
}
