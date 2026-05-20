using System;
using System.Collections.Generic;

namespace TransportManager.Entities.Map
{
    [Serializable]
    public class RouteResult
    {
        public string fromCityId;
        public string toCityId;
        public float distanceKm;
        public float durationSeconds;
        public float ascentMeters;
        public float descentMeters;
        public List<GeoPoint> polyline = new List<GeoPoint>();
        public bool found;
        public long fetchedAtUtcTicks;
    }
}
