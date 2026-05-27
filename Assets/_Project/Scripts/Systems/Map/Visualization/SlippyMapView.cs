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
            int minZoom = config != null ? ComputeMinZoom() : 1;
            zoom = Mathf.Clamp(newZoom, minZoom, config != null ? config.maxZoom : 18);

            // clamp latitude so the map never scrolls past its top/bottom edge
            double maxLat = ClampLatitudeForZoom(zoom);
            centerLatitude = Math.Max(-maxLat, Math.Min(maxLat, latitude));
            centerLongitude = longitude;

            Refresh();
            OnViewChanged?.Invoke();
        }

        // Returns the max latitude reachable so the map fills the container vertically
        private double ClampLatitudeForZoom(int z)
        {
            if (config == null || tilesContainer == null) return 85.0511;
            int tileSize = config.tilePixelSize;
            float halfHeight = tilesContainer.rect.height / 2f;
            double worldPixels = (1 << z) * tileSize;
            // how many degrees of latitude correspond to halfHeight pixels at this zoom?
            // world goes from -85.0511 to +85.0511 over worldPixels
            double degreesPerPixel = 170.1022 / worldPixels;
            double margin = halfHeight * degreesPerPixel;
            return 85.0511 - margin;
        }

        public void Pan(double deltaLatitude, double deltaLongitude)
        {
            SetView(centerLatitude + deltaLatitude, centerLongitude + deltaLongitude, zoom);
        }

        public void ZoomIn() => SetView(centerLatitude, centerLongitude, zoom + 1);
        public void ZoomOut() => SetView(centerLatitude, centerLongitude, zoom - 1);

        /// Zoom et centre la vue de sorte que les deux points soient visibles avec une marge.
        public void FitBounds(double latA, double lonA, double latB, double lonB)
        {
            if (config == null || tilesContainer == null) return;

            double cLat = (latA + latB) / 2.0;
            double cLon = (lonA + lonB) / 2.0;

            var rect = tilesContainer.rect;
            int ts = config.tilePixelSize;
            // On vise 65 % de la surface (marge de 35 %)
            float maxW = rect.width  * 0.65f;
            float maxH = rect.height * 0.65f;

            int targetZoom = config.minZoom;
            for (int z = config.maxZoom; z >= config.minZoom; z--)
            {
                var (axPx, ayPx) = TileCoordinate.LatLonToPixel(latA, lonA, z, ts);
                var (bxPx, byPx) = TileCoordinate.LatLonToPixel(latB, lonB, z, ts);
                double spanX = Math.Abs(axPx - bxPx);
                double spanY = Math.Abs(ayPx - byPx);
                if (spanX <= maxW && spanY <= maxH) { targetZoom = z; break; }
            }

            SetView(cLat, cLon, targetZoom);
        }

        private int ComputeMinZoom()
        {
            if (config == null || tilesContainer == null) return 1;
            var rect = tilesContainer.rect;
            int tileSize = config.tilePixelSize;
            // need world pixel size to cover both width and height
            for (int z = config.minZoom; z <= config.maxZoom; z++)
            {
                float worldPixels = (1 << z) * tileSize;
                if (worldPixels >= rect.width && worldPixels >= rect.height) return z;
            }
            return config.maxZoom;
        }

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

            int worldCount = 1 << zoom; // total tiles per axis at this zoom
            int worldMax = worldCount - 1;
            var keep = new HashSet<TileKey>();

            for (int tx = xMin; tx <= xMax; tx++)
            {
                for (int ty = yMin; ty <= yMax; ty++)
                {
                    if (ty < 0 || ty > worldMax) continue; // latitude has hard limits
                    int wrappedTx = ((tx % worldCount) + worldCount) % worldCount; // wrap longitude
                    var key = new TileKey(zoom, wrappedTx, ty);
                    // use a visual key so the same tile can appear multiple times (world wrap)
                    var visualKey = new TileKey(zoom, tx, ty);
                    keep.Add(visualKey);
                    if (!_active.TryGetValue(visualKey, out var img))
                    {
                        img = AcquireImage();
                        _active[visualKey] = img;
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
