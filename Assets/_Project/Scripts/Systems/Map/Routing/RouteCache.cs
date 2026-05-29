using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using TransportManager.Entities.Map;
using TransportManager.Enums;

namespace TransportManager.Systems.Map.Routing
{
    public class RouteCache
    {
        private const string FileName = "route_cache.json";
        private static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        private Dictionary<string, RouteResult> _entries = new Dictionary<string, RouteResult>();
        private bool _loaded;

        private static string Key(string fromCityId, string toCityId, VehicleRoutingProfile profile)
        {
            string a = string.CompareOrdinal(fromCityId, toCityId) <= 0 ? fromCityId : toCityId;
            string b = ReferenceEquals(a, fromCityId) ? toCityId : fromCityId;
            return $"{(int)profile}|{a}::{b}";
        }

        public bool TryGet(string fromCityId, string toCityId, VehicleRoutingProfile profile, out RouteResult result)
        {
            EnsureLoaded();
            if (!_entries.TryGetValue(Key(fromCityId, toCityId, profile), out var stored))
            {
                result = null;
                return false;
            }

            // Ignore les entrées « vol d'oiseau » (≤2 points, repli euclidien) : on force
            // un nouveau fetch ORS pour récupérer le vrai tracé routier. Les itinéraires
            // ORS réels comptent de nombreux points et restent donc en cache.
            if (stored.polyline == null || stored.polyline.Count <= 2)
            {
                result = null;
                return false;
            }

            if (stored.fromCityId == fromCityId)
            {
                result = stored;
                return true;
            }

            // Reverse direction stored — mirror it.
            result = new RouteResult
            {
                fromCityId = fromCityId,
                toCityId = toCityId,
                distanceKm = stored.distanceKm,
                durationSeconds = stored.durationSeconds,
                ascentMeters = stored.descentMeters,
                descentMeters = stored.ascentMeters,
                polyline = new List<GeoPoint>(stored.polyline),
                found = stored.found,
                fetchedAtUtcTicks = stored.fetchedAtUtcTicks
            };
            result.polyline.Reverse();
            return true;
        }

        public void Store(RouteResult result, VehicleRoutingProfile profile)
        {
            if (result == null || !result.found) return;
            EnsureLoaded();
            _entries[Key(result.fromCityId, result.toCityId, profile)] = result;
            Persist();
        }

        public void Clear()
        {
            _entries.Clear();
            if (File.Exists(FilePath)) File.Delete(FilePath);
        }

        private void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            if (!File.Exists(FilePath)) return;
            try
            {
                var json = File.ReadAllText(FilePath);
                var data = JsonConvert.DeserializeObject<Dictionary<string, RouteResult>>(json);
                if (data != null) _entries = data;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RouteCache] Load failed: {e.Message}");
            }
        }

        private void Persist()
        {
            try
            {
                var json = JsonConvert.SerializeObject(_entries);
                File.WriteAllText(FilePath, json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RouteCache] Persist failed: {e.Message}");
            }
        }
    }
}
