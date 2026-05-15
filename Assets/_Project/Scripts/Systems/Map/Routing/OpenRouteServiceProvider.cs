using System;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;
using TransportManager.Entities.Map;
using TransportManager.Enums;

namespace TransportManager.Systems.Map.Routing
{
    public class OpenRouteServiceProvider : IRoutingProvider
    {
        private readonly OrsConfig _config;

        public OpenRouteServiceProvider(OrsConfig config)
        {
            _config = config;
        }

        public async Task<RouteResult> GetRouteAsync(CityEntry from, CityEntry to, VehicleRoutingProfile profile)
        {
            var result = new RouteResult
            {
                fromCityId = from.id,
                toCityId = to.id,
                fetchedAtUtcTicks = DateTime.UtcNow.Ticks,
                found = false
            };

            if (_config == null || string.IsNullOrEmpty(_config.apiKey))
            {
                Debug.LogWarning("[ORS] No API key configured.");
                return result;
            }

            string profileSlug = profile == VehicleRoutingProfile.HeavyGoodsVehicle ? "driving-hgv" : "driving-car";
            string url = $"{_config.baseUrl}/v2/directions/{profileSlug}/geojson";

            var body = new OrsDirectionsRequest
            {
                Coordinates = new[]
                {
                    new[] { from.location.longitude, from.location.latitude },
                    new[] { to.location.longitude, to.location.latitude }
                },
                Elevation = _config.requestElevation ? (bool?)true : null
            };
            string jsonBody = JsonConvert.SerializeObject(body);

            using var req = new UnityWebRequest(url, "POST");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Authorization", _config.apiKey);
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Accept", "application/geo+json");
            req.timeout = _config.requestTimeoutSeconds;

            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[ORS] {from.id}->{to.id} failed: {req.error} ({req.responseCode})");
                return result;
            }

            try
            {
                var parsed = JsonConvert.DeserializeObject<OrsGeoJsonResponse>(req.downloadHandler.text);
                if (parsed?.Features == null || parsed.Features.Count == 0) return result;

                var feature = parsed.Features[0];
                var summary = feature.Properties?.Summary;
                if (summary == null) return result;

                result.distanceKm = (float)(summary.DistanceMeters / 1000.0);
                result.durationSeconds = (float)summary.DurationSeconds;

                if (feature.Properties.Segments != null)
                {
                    foreach (var seg in feature.Properties.Segments)
                    {
                        result.ascentMeters += (float)seg.AscentMeters;
                        result.descentMeters += (float)seg.DescentMeters;
                    }
                }

                if (feature.Geometry?.Coordinates != null)
                {
                    foreach (var c in feature.Geometry.Coordinates)
                    {
                        if (c.Count < 2) continue;
                        result.polyline.Add(new GeoPoint(c[1], c[0]));
                    }
                }

                result.found = true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ORS] Parse error for {from.id}->{to.id}: {e.Message}");
            }

            return result;
        }
    }
}
