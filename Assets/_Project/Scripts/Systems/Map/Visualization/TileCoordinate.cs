using System;

namespace TransportManager.Systems.Map.Visualization
{
    // Web-Mercator projection helpers (the projection OSM tiles use).
    // World pixel space at zoom Z = (2^Z * tileSize) on each axis,
    // origin top-left, x increases east, y increases south.
    public static class TileCoordinate
    {
        public static (int x, int y) LatLonToTile(double latitude, double longitude, int zoom)
        {
            int n = 1 << zoom;
            double latRad = latitude * Math.PI / 180.0;
            int x = (int)Math.Floor((longitude + 180.0) / 360.0 * n);
            int y = (int)Math.Floor((1.0 - Math.Log(Math.Tan(latRad) + 1.0 / Math.Cos(latRad)) / Math.PI) / 2.0 * n);
            x = Clamp(x, 0, n - 1);
            y = Clamp(y, 0, n - 1);
            return (x, y);
        }

        public static (double latitude, double longitude) TileToLatLon(int x, int y, int zoom)
        {
            int n = 1 << zoom;
            double lon = x / (double)n * 360.0 - 180.0;
            double latRad = Math.Atan(Math.Sinh(Math.PI * (1.0 - 2.0 * y / (double)n)));
            double lat = latRad * 180.0 / Math.PI;
            return (lat, lon);
        }

        public static (double px, double py) LatLonToPixel(double latitude, double longitude, int zoom, int tileSize = 256)
        {
            int n = 1 << zoom;
            double latRad = latitude * Math.PI / 180.0;
            double px = (longitude + 180.0) / 360.0 * n * tileSize;
            double py = (1.0 - Math.Log(Math.Tan(latRad) + 1.0 / Math.Cos(latRad)) / Math.PI) / 2.0 * n * tileSize;
            return (px, py);
        }

        public static (double latitude, double longitude) PixelToLatLon(double px, double py, int zoom, int tileSize = 256)
        {
            int n = 1 << zoom;
            double lon = px / (n * (double)tileSize) * 360.0 - 180.0;
            double latRad = Math.Atan(Math.Sinh(Math.PI * (1.0 - 2.0 * py / (n * (double)tileSize))));
            return (latRad * 180.0 / Math.PI, lon);
        }

        private static int Clamp(int v, int min, int max) => v < min ? min : (v > max ? max : v);
    }
}
