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
        private const int MaxAttempts = 3;        // 1 essai + 2 reprises
        private const int BaseBackoffMs = 800;    // backoff exponentiel : 0.8s, 1.6s

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
            byte[] payload = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(body));

            // L'API publique gratuite renvoie régulièrement des 502/504 ou expire :
            // on réessaie avec un backoff exponentiel avant de retomber sur le vol d'oiseau.
            for (int attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                var outcome = await TryFetch(url, payload, from, to, result);
                if (outcome == FetchOutcome.Success) return result;
                if (outcome == FetchOutcome.Permanent) return result; // inutile d'insister

                if (attempt < MaxAttempts)
                {
                    int delay = BaseBackoffMs * (1 << (attempt - 1));
                    Debug.LogWarning($"[ORS] {from.id}->{to.id} tentative {attempt}/{MaxAttempts} échouée, reprise dans {delay}ms…");
                    await Task.Delay(delay);
                }
            }

            Debug.LogWarning($"[ORS] {from.id}->{to.id} : routage routier indisponible après {MaxAttempts} tentatives (repli ligne droite).");
            return result;
        }

        private enum FetchOutcome { Success, Transient, Permanent }

        /// Effectue une seule requête. Remplit `result` et indique s'il faut réessayer.
        private async Task<FetchOutcome> TryFetch(string url, byte[] payload, CityEntry from, CityEntry to, RouteResult result)
        {
            using var req = new UnityWebRequest(url, "POST");
            req.uploadHandler = new UploadHandlerRaw(payload);
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
                long code = req.responseCode;
                // 4xx (clé invalide, requête malformée…) = inutile de réessayer, sauf 429 (quota).
                bool permanent = code >= 400 && code < 500 && code != 429;
                return permanent ? FetchOutcome.Permanent : FetchOutcome.Transient;
            }

            try
            {
                var parsed = JsonConvert.DeserializeObject<OrsGeoJsonResponse>(req.downloadHandler.text);
                if (parsed?.Features == null || parsed.Features.Count == 0) return FetchOutcome.Permanent;

                var feature = parsed.Features[0];
                var summary = feature.Properties?.Summary;
                if (summary == null) return FetchOutcome.Permanent;

                result.distanceKm = (float)(summary.DistanceMeters / 1000.0);
                result.durationSeconds = (float)summary.DurationSeconds;
                result.ascentMeters = 0f;
                result.descentMeters = 0f;
                result.polyline.Clear();

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
                return FetchOutcome.Success;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ORS] Parse error for {from.id}->{to.id}: {e.Message}");
                return FetchOutcome.Permanent;
            }
        }
    }
}
