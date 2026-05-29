using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using TransportManager.Entities.Map;
using TransportManager.Enums;
using TransportManager.Systems.Map.Routing;

namespace TransportManager.Systems.Map
{
    public class MapSystem
    {
        private readonly CityCatalog _catalog;
        private readonly IRoutingProvider _provider;
        private readonly RouteCache _cache;

        public MapSystem(CityCatalog catalog, IRoutingProvider provider)
        {
            _catalog = catalog;
            _provider = provider;
            _cache = new RouteCache();
        }

        public CityCatalog Catalog => _catalog;
        public bool HasCities => _catalog != null && _catalog.cities.Count > 0;

        public CityEntry GetRandomCity()
        {
            if (!HasCities) return null;
            return _catalog.cities[Random.Range(0, _catalog.cities.Count)];
        }

        public (CityEntry from, CityEntry to) GetRandomCityPair()
        {
            if (!HasCities || _catalog.cities.Count < 2) return (null, null);
            var from = GetRandomCity();
            CityEntry to = null;
            for (int i = 0; i < 8; i++)
            {
                to = GetRandomCity();
                if (to != from) break;
            }
            return (from, to);
        }

        // ── Variantes filtrées par pays (déblocage géographique des contrats) ───────
        // allowedCountries == null  → aucune restriction (monde entier).

        public CityEntry GetNearestCity(double latitude, double longitude)
        {
            if (!HasCities) return null;
            var target = new GeoPoint(latitude, longitude);
            CityEntry best = null;
            double bestKm = double.MaxValue;
            foreach (var c in _catalog.cities)
            {
                double d = GeoPoint.HaversineKm(c.location, target);
                if (d < bestKm) { bestKm = d; best = c; }
            }
            return best;
        }

        public (CityEntry from, CityEntry to) GetRandomCityPair(ICollection<string> allowedCountries)
        {
            if (allowedCountries == null) return GetRandomCityPair();

            var pool = new List<CityEntry>();
            foreach (var c in _catalog.cities)
                if (allowedCountries.Contains(c.country)) pool.Add(c);

            // Repli de sécurité : pas assez de villes dans la zone → on ne bloque pas.
            if (pool.Count < 2) return GetRandomCityPair();

            var from = pool[Random.Range(0, pool.Count)];
            CityEntry to = null;
            for (int i = 0; i < 8; i++)
            {
                to = pool[Random.Range(0, pool.Count)];
                if (to != from) break;
            }
            return (from, to);
        }

        // Nombre de villes du catalogue situées dans l'un des pays autorisés.
        public int CountCitiesIn(ICollection<string> allowedCountries)
        {
            if (allowedCountries == null) return HasCities ? _catalog.cities.Count : 0;
            int n = 0;
            foreach (var c in _catalog.cities)
                if (allowedCountries.Contains(c.country)) n++;
            return n;
        }

        public string GetRandomAddressLabel(CityEntry city)
        {
            if (city == null) return string.Empty;
            if (city.deliveryPointLabels == null || city.deliveryPointLabels.Count == 0)
                return city.displayName;
            return city.deliveryPointLabels[Random.Range(0, city.deliveryPointLabels.Count)];
        }

        public async Task<RouteResult> GetRouteAsync(CityEntry from, CityEntry to, VehicleRoutingProfile profile)
        {
            if (from == null || to == null) return null;
            if (_cache.TryGet(from.id, to.id, profile, out var cached)) return cached;
            if (_provider == null) return null;

            var result = await _provider.GetRouteAsync(from, to, profile);
            if (result != null && result.found) _cache.Store(result, profile);
            return result;
        }

        public void ClearCache() => _cache.Clear();
    }
}
