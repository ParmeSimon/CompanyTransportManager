using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TransportManager.Entities.Map;

namespace TransportManager.Systems.Map.Visualization
{
    [RequireComponent(typeof(RectTransform))]
    public class SlippyMapView : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private TileMapConfig config;
        [SerializeField] private RectTransform tilesContainer;
        [SerializeField] private RawImage tilePrefab;

        [Header("View")]
        [SerializeField] private double centerLatitude = 48.8566;   // Paris
        [SerializeField] private double centerLongitude = 2.3522;
        [SerializeField] private int zoom = 5;

        private MapTileService _service;
        private readonly Dictionary<TileKey, RawImage> _active = new Dictionary<TileKey, RawImage>();
        private readonly Queue<RawImage> _pool = new Queue<RawImage>();

        public TileMapConfig Config => config;
        public double CenterLatitude => centerLatitude;
        public double CenterLongitude => centerLongitude;
        public int Zoom => zoom;

        public event Action OnViewChanged;

        private void Awake()
        {
            if (config != null) _service = new MapTileService(config);
        }

        private void Start() => Refresh();

        public void SetView(double latitude, double longitude, int newZoom)
        {
            centerLatitude = latitude;
            centerLongitude = longitude;
            zoom = Mathf.Clamp(newZoom, config != null ? config.minZoom : 1, config != null ? config.maxZoom : 18);
            Refresh();
            OnViewChanged?.Invoke();
        }

        public void Pan(double deltaLatitude, double deltaLongitude)
        {
            SetView(centerLatitude + deltaLatitude, centerLongitude + deltaLongitude, zoom);
        }

        public void ZoomIn() => SetView(centerLatitude, centerLongitude, zoom + 1);
        public void ZoomOut() => SetView(centerLatitude, centerLongitude, zoom - 1);

        public Vector2 LatLonToLocal(double latitude, double longitude)
        {
            int tileSize = config != null ? config.tilePixelSize : 256;
            var (cx, cy) = TileCoordinate.LatLonToPixel(centerLatitude, centerLongitude, zoom, tileSize);
            var (px, py) = TileCoordinate.LatLonToPixel(latitude, longitude, zoom, tileSize);
            return new Vector2((float)(px - cx), -(float)(py - cy));
        }

        public void Refresh()
        {
            if (_service == null || config == null || tilesContainer == null || tilePrefab == null) return;

            int tileSize = config.tilePixelSize;
            var rect = tilesContainer.rect;
            var (cx, cy) = TileCoordinate.LatLonToPixel(centerLatitude, centerLongitude, zoom, tileSize);

            int xMin = (int)Math.Floor((cx - rect.width / 2.0) / tileSize);
            int xMax = (int)Math.Floor((cx + rect.width / 2.0) / tileSize);
            int yMin = (int)Math.Floor((cy - rect.height / 2.0) / tileSize);
            int yMax = (int)Math.Floor((cy + rect.height / 2.0) / tileSize);

            int worldMax = (1 << zoom) - 1;
            var keep = new HashSet<TileKey>();

            for (int tx = xMin; tx <= xMax; tx++)
            {
                for (int ty = yMin; ty <= yMax; ty++)
                {
                    if (tx < 0 || tx > worldMax || ty < 0 || ty > worldMax) continue;
                    var key = new TileKey(zoom, tx, ty);
                    keep.Add(key);
                    if (!_active.TryGetValue(key, out var img))
                    {
                        img = AcquireImage();
                        _active[key] = img;
                        _ = LoadIntoAsync(img, key);
                    }
                    double localX = (tx + 0.5) * tileSize - cx;
                    double localY = -((ty + 0.5) * tileSize - cy);
                    img.rectTransform.anchoredPosition = new Vector2((float)localX, (float)localY);
                    img.rectTransform.sizeDelta = new Vector2(tileSize, tileSize);
                }
            }

            var toRemove = new List<TileKey>();
            foreach (var kv in _active)
                if (!keep.Contains(kv.Key)) toRemove.Add(kv.Key);
            foreach (var k in toRemove)
            {
                ReleaseImage(_active[k]);
                _active.Remove(k);
            }
        }

        private async Task LoadIntoAsync(RawImage img, TileKey key)
        {
            var tex = await _service.GetTileAsync(key);
            if (tex != null && img != null) img.texture = tex;
        }

        private RawImage AcquireImage()
        {
            RawImage img;
            if (_pool.Count > 0)
            {
                img = _pool.Dequeue();
                img.gameObject.SetActive(true);
            }
            else
            {
                img = Instantiate(tilePrefab, tilesContainer);
                img.gameObject.SetActive(true);
            }
            img.texture = null;
            return img;
        }

        private void ReleaseImage(RawImage img)
        {
            if (img == null) return;
            img.texture = null;
            img.gameObject.SetActive(false);
            _pool.Enqueue(img);
        }
    }
}
