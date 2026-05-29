using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using TransportManager.Core;
using TransportManager.Entities.Contracts;
using TransportManager.Entities.Map;
using TransportManager.Enums;
using TransportManager.Systems.Progression;

namespace TransportManager.Systems.Contracts
{
    public class ContractGenerator
    {
        private const float AddressVariancePercent = 0.05f;   // ±5% jitter
        private const float BaseDollarsPerKm  = 2.5f;          // facteur PRIORITAIRE : la distance
        private const float CargoBonusPerTon  = 0.01f;         // facteur MINEUR : +1% du prix par tonne

        // Fallback constants when the routing provider can't deliver a route.
        private const float FallbackDetourFactor   = 1.3f;    // roads are ~30% longer than great-circle
        private const float FallbackSpeedKmhCar    = 80f;
        private const float FallbackSpeedKmhHgv    = 65f;

        // Number of city pairs we'll try before giving up when a distance cap is set.
        private const int MaxPairAttempts = 12;

        // maxDistanceKm bounds the route length so a contract never exceeds the range
        // of the available fleet. Pass float.MaxValue (default) for no cap.
        public async Task<ContractData> GenerateAsync(
            VehicleRoutingProfile profile,
            ContractDifficulty difficulty,
            float maxDistanceKm = float.MaxValue)
        {
            var map = ServiceLocator.Get<Systems.Map.MapSystem>();
            if (map == null || !map.HasCities) return null;

            // Portée géographique débloquée (arbre Dépôt) : restreint les pays éligibles.
            var allowedCountries = AllowedCountriesForContracts(map);

            CityEntry from = null, to = null;
            float distance = 0f, duration = 0f;
            bool found = false;

            int attempts = maxDistanceKm >= float.MaxValue ? 1 : MaxPairAttempts;
            for (int i = 0; i < attempts; i++)
            {
                (from, to) = map.GetRandomCityPair(allowedCountries);
                if (from == null || to == null) continue;

                // Cheap pre-filter: a straight-line estimate already over the cap can
                // never route under it, so skip without spending a routing call.
                if (maxDistanceKm < float.MaxValue)
                {
                    float estimate = (float)(Entities.Map.GeoPoint.HaversineKm(from.location, to.location)
                                              * FallbackDetourFactor * (1f + AddressVariancePercent));
                    if (estimate > maxDistanceKm) continue;
                }

                var route = await map.GetRouteAsync(from, to, profile);

                // Routing can fail (no API key, offline, rate limit). Fall back to a
                // great-circle estimate so a contract is always produced when cities exist.
                if (route != null && route.found)
                {
                    distance = route.distanceKm;
                    duration = route.durationSeconds;
                }
                else
                {
                    distance = (float)(Entities.Map.GeoPoint.HaversineKm(from.location, to.location) * FallbackDetourFactor);
                    float speed = profile == VehicleRoutingProfile.HeavyGoodsVehicle ? FallbackSpeedKmhHgv : FallbackSpeedKmhCar;
                    duration = distance / UnityEngine.Mathf.Max(1f, speed) * 3600f;
                }

                float jit = 1f + UnityEngine.Random.Range(-AddressVariancePercent, AddressVariancePercent);
                distance *= jit;
                duration *= jit;

                if (distance <= maxDistanceKm) { found = true; break; }
            }

            if (!found) return null;

            int cargoTons = CargoTonsFor(difficulty);
            string cargoLabel = CargoGoods[UnityEngine.Random.Range(0, CargoGoods.Length)];

            // Priorité : distance (base) > difficulté (multiplicateur) > cargaison (bonus mineur).
            float cargoBonus = 1f + cargoTons * CargoBonusPerTon;
            int reward = Mathf.RoundToInt(distance * BaseDollarsPerKm * DifficultyRewardMultiplier(difficulty) * cargoBonus);

            return new ContractData
            {
                id = Guid.NewGuid().ToString(),
                displayName = $"{from.displayName} → {to.displayName}",
                difficulty = difficulty,
                originCityId = from.id,
                destinationCityId = to.id,
                originAddressLabel = map.GetRandomAddressLabel(from),
                destinationAddressLabel = map.GetRandomAddressLabel(to),
                distanceKm = distance,
                baseDurationSeconds = duration,
                cargoTons = cargoTons,
                cargoLabel = cargoLabel,
                requiredCapacity = cargoTons,
                baseReward = reward
            };
        }

        public async Task RefreshAvailablePool(
            List<ContractData> pool, int targetCount, VehicleRoutingProfile profile,
            float maxDistanceKm = float.MaxValue)
        {
            if (pool == null) return;
            int safety = targetCount * 4;
            while (pool.Count < targetCount && safety-- > 0)
            {
                var c = await GenerateAsync(profile, RandomDifficulty(), maxDistanceKm);
                if (c == null) break;
                pool.Add(c);
            }
        }

        // Pays éligibles aux contrats selon le pays d'attache (ville la plus proche du
        // siège) et la portée débloquée dans l'arbre Dépôt. Renvoie null = monde entier.
        private static HashSet<string> AllowedCountriesForContracts(Systems.Map.MapSystem map)
        {
            const int MinCities = 4;   // garantit un vivier jouable, même pour un petit pays

            var company = GameManager.Instance?.Save?.company;
            if (company == null || !company.hasLocationCoordinates) return null;

            var home = map.GetNearestCity(company.locationLatitude, company.locationLongitude);
            if (home == null) return null;

            int reach = ServiceLocator.Get<SkillTreeSystem>()?.Flat(SkillEffectType.ContractCountryReach) ?? 0;

            // Élargit d'un cran tant que la zone compte trop peu de villes : évite tout
            // blocage si le joueur s'installe dans un pays minuscule (Luxembourg, Malte…).
            var set = Entities.Map.GeoRegions.AllowedCountries(home.country, reach);
            while (set != null && map.CountCitiesIn(set) < MinCities && reach < 3)
            {
                reach++;
                set = Entities.Map.GeoRegions.AllowedCountries(home.country, reach);
            }
            return set;
        }

        private static ContractDifficulty RandomDifficulty()
        {
            float r = UnityEngine.Random.value;
            if (r < 0.55f) return ContractDifficulty.Easy;
            if (r < 0.85f) return ContractDifficulty.Medium;

            // Les contrats premium n'apparaissent qu'avec le capstone RH
            // « Carnet d'adresses premium ». Sinon, repli sur Hard.
            bool premiumUnlocked = (ServiceLocator.Get<SkillTreeSystem>()?
                                        .Flat(SkillEffectType.PremiumContractsUnlocked) ?? 0) > 0;
            if (r < 0.97f || !premiumUnlocked) return ContractDifficulty.Hard;
            return ContractDifficulty.Premium;
        }

        // Facteur secondaire : la difficulté. Spread modéré pour rester sous l'influence
        // de la distance, mais nettement au-dessus du bonus de cargaison.
        private static float DifficultyRewardMultiplier(ContractDifficulty d) => d switch
        {
            ContractDifficulty.Easy => 1f,
            ContractDifficulty.Medium => 1.4f,
            ContractDifficulty.Hard => 1.9f,
            ContractDifficulty.Premium => 2.5f,
            _ => 1f
        };

        private static readonly string[] CargoGoods =
        {
            "Palettes de boissons", "Matériel électronique", "Pièces automobiles",
            "Mobilier", "Produits alimentaires", "Matériaux de construction",
            "Textiles & vêtements", "Produits chimiques", "Machines industrielles",
            "Électroménager", "Produits pharmaceutiques", "Acier & métaux",
            "Bois & papier", "Denrées réfrigérées", "Équipement agricole"
        };

        private static int CargoTonsFor(ContractDifficulty d) => d switch
        {
            ContractDifficulty.Easy    => UnityEngine.Random.Range(2, 7),    // 2–6 t
            ContractDifficulty.Medium  => UnityEngine.Random.Range(7, 15),   // 7–14 t
            ContractDifficulty.Hard    => UnityEngine.Random.Range(15, 28),  // 15–27 t
            ContractDifficulty.Premium => UnityEngine.Random.Range(28, 41),  // 28–40 t
            _ => UnityEngine.Random.Range(2, 7)
        };
    }
}
