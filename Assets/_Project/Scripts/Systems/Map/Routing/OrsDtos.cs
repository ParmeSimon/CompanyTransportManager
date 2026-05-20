using System.Collections.Generic;
using Newtonsoft.Json;

namespace TransportManager.Systems.Map.Routing
{
    // ---- Request ----

    internal class OrsDirectionsRequest
    {
        [JsonProperty("coordinates")]
        public double[][] Coordinates { get; set; }   // [[lon, lat], [lon, lat]]

        [JsonProperty("elevation", NullValueHandling = NullValueHandling.Ignore)]
        public bool? Elevation { get; set; }

        [JsonProperty("instructions")]
        public bool Instructions { get; set; } = false;
    }

    // ---- Response (GeoJSON shape) ----

    internal class OrsGeoJsonResponse
    {
        [JsonProperty("features")]
        public List<OrsFeature> Features { get; set; }
    }

    internal class OrsFeature
    {
        [JsonProperty("geometry")]
        public OrsGeometry Geometry { get; set; }

        [JsonProperty("properties")]
        public OrsProperties Properties { get; set; }
    }

    internal class OrsGeometry
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        // Each point is [lon, lat] or [lon, lat, elevation]
        [JsonProperty("coordinates")]
        public List<List<double>> Coordinates { get; set; }
    }

    internal class OrsProperties
    {
        [JsonProperty("summary")]
        public OrsSummary Summary { get; set; }

        [JsonProperty("segments")]
        public List<OrsSegment> Segments { get; set; }
    }

    internal class OrsSummary
    {
        [JsonProperty("distance")]
        public double DistanceMeters { get; set; }

        [JsonProperty("duration")]
        public double DurationSeconds { get; set; }
    }

    internal class OrsSegment
    {
        [JsonProperty("distance")]
        public double DistanceMeters { get; set; }

        [JsonProperty("duration")]
        public double DurationSeconds { get; set; }

        [JsonProperty("ascent")]
        public double AscentMeters { get; set; }

        [JsonProperty("descent")]
        public double DescentMeters { get; set; }
    }
}
