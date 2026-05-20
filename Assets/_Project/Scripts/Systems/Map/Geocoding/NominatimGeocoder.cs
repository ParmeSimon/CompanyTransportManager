using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace TransportManager.Systems.Map.Geocoding
{
    public static class NominatimGeocoder
    {
        public struct Result
        {
            public bool success;
            public double latitude;
            public double longitude;
            public string displayName;
        }

        public delegate void GeocodeCallback(Result result);

        public static IEnumerator Geocode(string query, GeocodeCallback callback)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                callback?.Invoke(new Result { success = false });
                yield break;
            }

            string url = $"https://nominatim.openstreetmap.org/search?q={UnityWebRequest.EscapeURL(query)}&format=json&limit=1";
            using (var req = UnityWebRequest.Get(url))
            {
                // Nominatim usage policy requires a unique User-Agent
                req.SetRequestHeader("User-Agent", "CompanyTransportManager/1.0 (Unity)");
                req.SetRequestHeader("Accept-Language", "fr,en");
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[Geocoder] Request failed: {req.error}");
                    callback?.Invoke(new Result { success = false });
                    yield break;
                }

                var json = req.downloadHandler.text;
                if (string.IsNullOrEmpty(json) || !json.Contains("\"lat\""))
                {
                    callback?.Invoke(new Result { success = false });
                    yield break;
                }

                // Light parsing: extract first lat/lon/display_name values
                double lat = ParseDouble(ExtractField(json, "\"lat\":\""));
                double lon = ParseDouble(ExtractField(json, "\"lon\":\""));
                string display = ExtractField(json, "\"display_name\":\"");

                callback?.Invoke(new Result
                {
                    success = !double.IsNaN(lat) && !double.IsNaN(lon),
                    latitude = lat,
                    longitude = lon,
                    displayName = display,
                });
            }
        }

        private static string ExtractField(string json, string startToken)
        {
            int i = json.IndexOf(startToken, StringComparison.Ordinal);
            if (i < 0) return null;
            int start = i + startToken.Length;
            int end = json.IndexOf('"', start);
            if (end < 0) return null;
            return json.Substring(start, end - start);
        }

        private static double ParseDouble(string s)
        {
            if (string.IsNullOrEmpty(s)) return double.NaN;
            return double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : double.NaN;
        }
    }
}
