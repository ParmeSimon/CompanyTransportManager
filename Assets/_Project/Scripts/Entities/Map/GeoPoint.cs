using System;

namespace TransportManager.Entities.Map
{
    [Serializable]
    public struct GeoPoint
    {
        public double latitude;
        public double longitude;

        public GeoPoint(double latitude, double longitude)
        {
            this.latitude = latitude;
            this.longitude = longitude;
        }

        public static double HaversineKm(GeoPoint a, GeoPoint b)
        {
            const double earthRadiusKm = 6371.0;
            double dLat = (b.latitude - a.latitude) * Math.PI / 180.0;
            double dLon = (b.longitude - a.longitude) * Math.PI / 180.0;
            double lat1 = a.latitude * Math.PI / 180.0;
            double lat2 = b.latitude * Math.PI / 180.0;

            double h = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Sin(dLon / 2) * Math.Sin(dLon / 2) * Math.Cos(lat1) * Math.Cos(lat2);
            return 2 * earthRadiusKm * Math.Asin(Math.Sqrt(h));
        }

        public override string ToString() => $"({latitude:F4}, {longitude:F4})";
    }
}
