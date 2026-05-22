#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Net;
using System.Globalization;
using UnityEditor;
using UnityEngine;
using TransportManager.Entities.Map;

/// <summary>
/// Transport Manager > Éditeur de Villes
/// Lets you search any address via Nominatim and add it to a CityCatalog asset.
/// Contracts will automatically include the new city as a possible origin/destination.
/// Travel times are calculated via ORS (real roads, vehicle speed).
/// </summary>
public class CityCatalogEditorWindow : EditorWindow
{
    private CityCatalog _catalog;
    private string      _query   = "";
    private string      _status  = "";
    private Vector2     _scroll;
    private readonly List<NominatimResult> _suggestions = new List<NominatimResult>();

    private struct NominatimResult
    {
        public string displayName;
        public double lat, lon;
    }

    [MenuItem("Transport Manager/Éditeur de Villes %#v")]
    static void Open() => GetWindow<CityCatalogEditorWindow>("Éditeur de Villes").Show();

    private void OnGUI()
    {
        EditorGUILayout.Space(6);

        // ── CityCatalog picker ────────────────────────────────────────────────
        _catalog = (CityCatalog)EditorGUILayout.ObjectField(
            "CityCatalog", _catalog, typeof(CityCatalog), false);

        if (_catalog == null)
        {
            EditorGUILayout.HelpBox(
                "Sélectionnez le fichier CityCatalog dans Assets/ScriptableObjects/Map/.",
                MessageType.Info);
            if (GUILayout.Button("Trouver automatiquement"))
                AutoFindCatalog();
            return;
        }

        EditorGUILayout.Space(8);

        // ── Search ────────────────────────────────────────────────────────────
        EditorGUILayout.LabelField("Ajouter une destination", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        _query = EditorGUILayout.TextField(_query);
        GUI.enabled = !string.IsNullOrWhiteSpace(_query);
        if (GUILayout.Button("Rechercher", GUILayout.Width(100)))
            RunSearch();
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();

        if (!string.IsNullOrEmpty(_status))
            EditorGUILayout.LabelField(_status, EditorStyles.miniLabel);

        // ── Suggestions ───────────────────────────────────────────────────────
        foreach (var s in _suggestions)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            EditorGUILayout.LabelField(s.displayName, GUILayout.ExpandWidth(true));
            EditorGUILayout.LabelField(
                $"{s.lat:F4}, {s.lon:F4}",
                EditorStyles.miniLabel, GUILayout.Width(130));
            if (GUILayout.Button("+ Ajouter", GUILayout.Width(80)))
                AddCity(s);
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(10);

        // ── Existing cities ───────────────────────────────────────────────────
        EditorGUILayout.LabelField(
            $"Destinations existantes ({_catalog.cities.Count})",
            EditorStyles.boldLabel);

        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        foreach (var city in _catalog.cities)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(
                $"{city.displayName}",
                GUILayout.Width(160));
            EditorGUILayout.LabelField(
                city.country,
                EditorStyles.miniLabel, GUILayout.Width(100));
            EditorGUILayout.LabelField(
                $"{city.location.latitude:F4}, {city.location.longitude:F4}",
                EditorStyles.miniLabel, GUILayout.Width(140));
            if (GUILayout.Button("✕", GUILayout.Width(24)))
            {
                if (EditorUtility.DisplayDialog(
                    "Supprimer",
                    $"Supprimer '{city.displayName}' du catalogue ?",
                    "Oui", "Non"))
                {
                    Undo.RecordObject(_catalog, "Supprimer ville");
                    _catalog.cities.Remove(city);
                    EditorUtility.SetDirty(_catalog);
                    AssetDatabase.SaveAssets();
                    GUIUtility.ExitGUI();
                }
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();
    }

    // ── Search via Nominatim (synchronous — editor only) ─────────────────────
    private void RunSearch()
    {
        _suggestions.Clear();
        _status = "Recherche en cours…";
        Repaint();

        try
        {
            var client = new WebClient();
            client.Headers[HttpRequestHeader.UserAgent]       = "CompanyTransportManager/1.0";
            client.Headers[HttpRequestHeader.AcceptLanguage]  = "fr,en";
            string url  = $"https://nominatim.openstreetmap.org/search" +
                          $"?q={WebUtility.UrlEncode(_query)}&format=json&limit=8";
            string json = client.DownloadString(url);
            _suggestions.AddRange(ParseNominatim(json));
            _status = _suggestions.Count > 0
                ? $"{_suggestions.Count} résultat(s) — cliquez + Ajouter pour l'intégrer au jeu."
                : "Aucun résultat. Essayez une formulation différente.";
        }
        catch (Exception e)
        {
            _status = "Erreur réseau : " + e.Message;
        }

        Repaint();
    }

    private void AddCity(NominatimResult r)
    {
        // Parse city name and country from Nominatim display_name
        var parts   = r.displayName.Split(',');
        string city = parts.Length > 0 ? parts[0].Trim() : r.displayName;
        string country = parts.Length > 1 ? parts[parts.Length - 1].Trim() : "";
        string id   = city.ToLowerInvariant()
                          .Replace(" ", "_")
                          .Replace("-", "_")
                          .Replace("'", "")
                          .Replace("é", "e").Replace("è", "e").Replace("ê", "e")
                          .Replace("à", "a").Replace("â", "a")
                          .Replace("ô", "o").Replace("û", "u").Replace("ù", "u")
                          .Replace("î", "i").Replace("ï", "i").Replace("ç", "c");

        if (_catalog.cities.Exists(c => c.id == id))
        {
            _status = $"⚠ '{id}' existe déjà dans le catalogue.";
            Repaint();
            return;
        }

        var entry = new CityEntry
        {
            id           = id,
            displayName  = city,
            country      = country,
            location     = new GeoPoint { latitude = r.lat, longitude = r.lon },
            deliveryPointLabels = new List<string> { city },
        };

        Undo.RecordObject(_catalog, "Ajouter ville");
        _catalog.cities.Add(entry);
        EditorUtility.SetDirty(_catalog);
        AssetDatabase.SaveAssets();

        _status = $"✔ '{city}' ajoutée ! Les contrats l'utiliseront automatiquement.";
        _suggestions.Clear();
        _query = "";
        Repaint();
    }

    private void AutoFindCatalog()
    {
        var guids = AssetDatabase.FindAssets("t:CityCatalog");
        if (guids.Length > 0)
            _catalog = AssetDatabase.LoadAssetAtPath<CityCatalog>(
                AssetDatabase.GUIDToAssetPath(guids[0]));
    }

    // ── Light JSON parser for Nominatim array ────────────────────────────────
    private static List<NominatimResult> ParseNominatim(string json)
    {
        var list = new List<NominatimResult>();
        if (string.IsNullOrEmpty(json)) return list;

        int pos = 0;
        while (pos < json.Length)
        {
            int li = json.IndexOf("\"lat\":\"", pos, StringComparison.Ordinal);
            if (li < 0) break;
            int ls = li + 7, le = json.IndexOf('"', ls); if (le < 0) break;

            int oi = json.IndexOf("\"lon\":\"", le, StringComparison.Ordinal);
            if (oi < 0) break;
            int os = oi + 7, oe = json.IndexOf('"', os); if (oe < 0) break;

            int di = json.IndexOf("\"display_name\":\"", oe, StringComparison.Ordinal);
            if (di < 0) break;
            int ds = di + 16, de = json.IndexOf('"', ds);
            while (de > 0 && json[de - 1] == '\\') de = json.IndexOf('"', de + 1);
            if (de < 0) break;

            if (double.TryParse(json.Substring(ls, le - ls), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out double lat) &&
                double.TryParse(json.Substring(os, oe - os), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out double lon))
            {
                list.Add(new NominatimResult
                {
                    lat = lat, lon = lon,
                    displayName = json.Substring(ds, de - ds)
                                      .Replace("\\\"", "\"").Replace("\\/", "/"),
                });
            }

            pos = de + 1;
        }
        return list;
    }
}
#endif
