using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace TransportManager.Systems.Map.Geocoding
{
    public static class NominatimGeocoder
    {
        private const string UserAgent = "CompanyTransportManager/1.0 (Unity)";
        private const string BaseUrl   = "https://nominatim.openstreetmap.org/search";

        public struct Result
        {
            public bool success;
            public double latitude;
            public double longitude;
            public string displayName;
        }

        public struct SuggestResult
        {
            public double latitude;
            public double longitude;
            public string displayName;
        }

        public delegate void GeocodeCallback(Result result);
        public delegate void SuggestCallback(List<SuggestResult> results);

        // Single best result (used for saving company location, etc.)
        public static IEnumerator Geocode(string query, GeocodeCallback callback)
        {
            if (string.IsNullOrWhiteSpace(query)) { callback?.Invoke(new Result { success = false }); yield break; }

            string url = $"{BaseUrl}?q={UnityWebRequest.EscapeURL(query)}&format=json&limit=1";
            using var req = UnityWebRequest.Get(url);
            req.SetRequestHeader("User-Agent", UserAgent);
            req.SetRequestHeader("Accept-Language", "fr,en");
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[Geocoder] {req.error}");
                callback?.Invoke(new Result { success = false });
                yield break;
            }

            var list = ParseArray(req.downloadHandler.text);
            if (list.Count == 0) { callback?.Invoke(new Result { success = false }); yield break; }

            callback?.Invoke(new Result
            {
                success     = true,
                latitude    = list[0].latitude,
                longitude   = list[0].longitude,
                displayName = list[0].displayName,
            });
        }

        // Up to `limit` suggestions for autocomplete dropdowns
        public static IEnumerator Suggest(string query, int limit, SuggestCallback callback)
        {
            if (string.IsNullOrWhiteSpace(query) || query.Length < 3)
            {
                callback?.Invoke(new List<SuggestResult>());
                yield break;
            }

            string url = $"{BaseUrl}?q={UnityWebRequest.EscapeURL(query)}&format=json&limit={limit}";
            using var req = UnityWebRequest.Get(url);
            req.SetRequestHeader("User-Agent", UserAgent);
            req.SetRequestHeader("Accept-Language", "fr,en");
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[Geocoder] {req.error}");
                callback?.Invoke(new List<SuggestResult>());
                yield break;
            }

            callback?.Invoke(ParseArray(req.downloadHandler.text));
        }

        // ── JSON parsing ─────────────────────────────────────────────────────────
        // Nominatim returns a JSON array: [{..., "lat":"xx", "lon":"yy", "display_name":"...", ...}, ...]
        // display_name appears AFTER lat and lon in each object.
        private static List<SuggestResult> ParseArray(string json)
        {
            var results = new List<SuggestResult>();
            if (string.IsNullOrEmpty(json)) return results;

            int pos = 0;
            while (pos < json.Length)
            {
                int latIdx = json.IndexOf("\"lat\":\"", pos, StringComparison.Ordinal);
                if (latIdx < 0) break;

                int latStart = latIdx + 7;
                int latEnd   = json.IndexOf('"', latStart);
                if (latEnd < 0) break;

                int lonIdx = json.IndexOf("\"lon\":\"", latEnd, StringComparison.Ordinal);
                if (lonIdx < 0) break;
                int lonStart = lonIdx + 7;
                int lonEnd   = json.IndexOf('"', lonStart);
                if (lonEnd < 0) break;

                int dnIdx = json.IndexOf("\"display_name\":\"", lonEnd, StringComparison.Ordinal);
                if (dnIdx < 0) break;
                int dnStart = dnIdx + 16;
                int dnEnd   = json.IndexOf('"', dnStart);
                // skip escaped quotes
                while (dnEnd > 0 && json[dnEnd - 1] == '\\')
                    dnEnd = json.IndexOf('"', dnEnd + 1);
                if (dnEnd < 0) break;

                double lat = ParseDouble(json.Substring(latStart, latEnd - latStart));
                double lon = ParseDouble(json.Substring(lonStart, lonEnd - lonStart));
                string dn  = json.Substring(dnStart, dnEnd - dnStart)
                                 .Replace("\\\"", "\"").Replace("\\/", "/");

                if (!double.IsNaN(lat) && !double.IsNaN(lon))
                    results.Add(new SuggestResult { latitude = lat, longitude = lon, displayName = dn });

                pos = dnEnd + 1;
            }
            return results;
        }

        private static double ParseDouble(string s)
        {
            if (string.IsNullOrEmpty(s)) return double.NaN;
            return double.TryParse(s, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : double.NaN;
        }
    }
}

