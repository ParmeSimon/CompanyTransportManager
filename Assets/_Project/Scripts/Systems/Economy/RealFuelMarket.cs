using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace TransportManager.Systems.Economy
{
    /// <summary>
    /// Cours du carburant **réel et identique pour tous les joueurs**.
    ///
    /// Le client NE contacte PAS Alpha Vantage directement (ça grillerait le quota dès quelques
    /// joueurs, et exposerait la clé). À la place, un job GitHub Actions appelle Alpha Vantage
    /// **1×/jour** et publie un petit fichier <c>fuel.json</c> public. Tous les téléphones lisent
    /// ce même fichier → prix identique pour tous, clé jamais embarquée, mise à l'échelle illimitée.
    ///
    /// Format attendu de <c>fuel.json</c> (récent → ancien, en $/baril) :
    /// <code>{ "date": "2026-05-26", "brent": [102.75, 106.9, 105.84, ...] }</code>
    ///
    /// Mise en place : voir tools/fuel-price-cache/README.md, puis coller l'URL dans <see cref="CacheUrl"/>.
    /// </summary>
    public static class RealFuelMarket
    {
        // URL publique du cache quotidien (publié par GitHub Actions sur la branche fuel-data).
        // Vide → repli local. Le repo doit rester PUBLIC pour que l'app puisse lire ce fichier.
        private const string CacheUrl =
            "https://raw.githubusercontent.com/ParmeSimon/CompanyTransportManager/fuel-data/fuel.json";

        private const float ReferenceBrent = 80f;          // $/baril ≈ prix de base du jeu (multiplicateur 1.0)
        private const float MinMult = 0.7f, MaxMult = 1.4f; // bornes du multiplicateur de prix
        private const int   MaxHistory = 90;               // ~3 mois d'historique conservés

        // Multiplicateurs de prix, du plus récent (index 0 = aujourd'hui) au plus ancien.
        private static readonly List<float> _history = new List<float>();
        private static string _fetchedDay;   // jour UTC du dernier fetch (pour ne fetcher qu'1×/jour)
        private static bool   _fetching;

        /// Vrai si un cours réel a été chargé.
        public static bool Available => _history.Count > 0;

        /// Nombre de jours d'historique réel disponibles.
        public static int HistoryDays => _history.Count;

        /// Multiplicateur de prix courant (1.0 = prix de base).
        public static float Multiplier => Available ? _history[0] : 1f;

        /// Multiplicateur il y a `daysAgo` jours (0 = aujourd'hui), borné à l'historique connu.
        public static float MultiplierDaysAgo(int daysAgo)
        {
            if (_history.Count == 0) return 1f;
            return _history[Mathf.Clamp(daysAgo, 0, _history.Count - 1)];
        }

        /// Tendance vs la veille : +1 hausse, -1 baisse, 0 stable.
        public static int Trend
        {
            get
            {
                if (_history.Count < 2) return 0;
                float d = _history[0] - _history[1];
                if (d >  0.002f) return 1;
                if (d < -0.002f) return -1;
                return 0;
            }
        }

        /// À appeler au démarrage : charge le cache si pas déjà fait aujourd'hui.
        public static void EnsureFresh()
        {
            if (string.IsNullOrEmpty(CacheUrl)) return;                       // pas d'URL → repli local
            if (_fetching) return;
            if (_fetchedDay == DateTime.UtcNow.ToString("yyyy-MM-dd")) return; // déjà chargé aujourd'hui
            _ = FetchAsync();
        }

        private static async Task FetchAsync()
        {
            _fetching = true;
            try
            {
                using var req = UnityWebRequest.Get(CacheUrl);
                req.timeout = 15;
                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[RealFuelMarket] cache injoignable ({req.error}) → repli local.");
                    return;
                }

                var resp = JsonConvert.DeserializeObject<FuelCache>(req.downloadHandler.text);
                if (resp?.brent == null || resp.brent.Count == 0)
                {
                    Debug.LogWarning("[RealFuelMarket] cache vide/illisible → repli local.");
                    return;
                }

                var hist = new List<float>(MaxHistory);
                foreach (float v in resp.brent)
                {
                    if (v > 0f) hist.Add(Mathf.Clamp(v / ReferenceBrent, MinMult, MaxMult));
                    if (hist.Count >= MaxHistory) break;
                }

                if (hist.Count > 0)
                {
                    _history.Clear();
                    _history.AddRange(hist);
                    _fetchedDay = DateTime.UtcNow.ToString("yyyy-MM-dd");
                    Debug.Log($"[RealFuelMarket] ✅ Cours réel OK ({resp.date}) — Brent ×{_history[0]:0.000} " +
                              $"({_history.Count} jours d'historique).");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RealFuelMarket] erreur de lecture du cache, repli local : {e.Message}");
            }
            finally
            {
                _fetching = false;
            }
        }

        [Serializable] private class FuelCache { public string date; public List<float> brent; }
    }
}
