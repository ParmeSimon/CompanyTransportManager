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

        // ── Tournées multi-arrêts (skill « Tournées multi-arrêts ») ──
        private const int   MultiStopMaxStops         = 4;     // jusqu'à 4 livraisons par tournée
        private const float MultiStopChance           = 0.28f; // proba d'une tournée quand le skill est débloqué
        private const float MultiStopRewardMultiplier = 1.5f;  // bonus de base d'une tournée (+0,2 par escale)

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
            var allowed = AllowedCountriesForContracts(map);

            // Tout part (ou revient) du dépôt : la ville du siège est toujours une extrémité.
            var depot = HomeCity(map) ?? map.GetRandomCityIn(allowed);
            if (depot == null) return null;

            // Tournée à escales si le skill est débloqué (jamais sur les contrats faciles).
            bool tryTour = difficulty != ContractDifficulty.Easy
                           && MultiStopUnlocked()
                           && UnityEngine.Random.value < MultiStopChance;
            if (tryTour)
            {
                var tour = await GenerateTour(map, allowed, depot, profile, difficulty, maxDistanceKm);
                if (tour != null) return tour;   // sinon, repli sur un contrat direct
            }

            return await GenerateSingle(map, allowed, depot, profile, difficulty, maxDistanceKm);
        }

        // Contrat direct : dépôt → ville (livraison) ou ville → dépôt (collecte).
        private async Task<ContractData> GenerateSingle(
            Systems.Map.MapSystem map, System.Collections.Generic.HashSet<string> allowed,
            CityEntry depot, VehicleRoutingProfile profile, ContractDifficulty difficulty, float maxDistanceKm)
        {
            int attempts = maxDistanceKm >= float.MaxValue ? 1 : MaxPairAttempts;
            for (int i = 0; i < attempts; i++)
            {
                var other = map.GetRandomCityIn(allowed, depot);
                if (other == null || other == depot) continue;

                bool collection = UnityEngine.Random.value < 0.5f;   // sens du contrat
                var from = collection ? other : depot;
                var to   = collection ? depot : other;

                if (maxDistanceKm < float.MaxValue)
                {
                    float estimate = (float)(Entities.Map.GeoPoint.HaversineKm(from.location, to.location)
                                              * FallbackDetourFactor * (1f + AddressVariancePercent));
                    if (estimate > maxDistanceKm) continue;
                }

                var (distance, duration) = await LegAsync(map, from, to, profile);
                float jit = 1f + UnityEngine.Random.Range(-AddressVariancePercent, AddressVariancePercent);
                distance *= jit; duration *= jit;
                if (distance > maxDistanceKm) continue;

                return BuildContract(map, difficulty, from, to, distance, duration, null);
            }
            return null;
        }

        // Tournée : dépôt → escale 1 → escale 2 → … (2 à 4 livraisons enchaînées).
        private async Task<ContractData> GenerateTour(
            Systems.Map.MapSystem map, System.Collections.Generic.HashSet<string> allowed,
            CityEntry depot, VehicleRoutingProfile profile, ContractDifficulty difficulty, float maxDistanceKm)
        {
            int target = UnityEngine.Random.Range(2, MultiStopMaxStops + 1);   // 2..MaxStops
            var stops = new System.Collections.Generic.List<CityEntry>();
            var chosen = new System.Collections.Generic.HashSet<CityEntry> { depot };
            var prev = depot;
            float totalDist = 0f, totalDur = 0f;
            int guard = target * 5;

            while (stops.Count < target && guard-- > 0)
            {
                var next = map.GetRandomCityIn(allowed, depot);
                if (next == null || chosen.Contains(next)) continue;

                var (d, du) = await LegAsync(map, prev, next, profile);
                if (maxDistanceKm < float.MaxValue && totalDist + d > maxDistanceKm)
                    break;   // on garde la tournée construite jusqu'ici si elle a ≥2 arrêts

                totalDist += d; totalDur += du;
                stops.Add(next); chosen.Add(next); prev = next;
            }

            if (stops.Count < 2) return null;   // pas une vraie tournée → repli sur contrat direct

            float jit = 1f + UnityEngine.Random.Range(-AddressVariancePercent, AddressVariancePercent);
            totalDist *= jit; totalDur *= jit;

            // Le dépôt est toujours une extrémité. Sens aléatoire :
            //  - sortante : dépôt → escale → … → ville
            //  - rentrante : ville → … → escale → dépôt
            // Inverser l'ordre ne change pas la distance totale (mêmes segments).
            var ordered = new System.Collections.Generic.List<CityEntry> { depot };
            ordered.AddRange(stops);
            if (UnityEngine.Random.value < 0.5f) ordered.Reverse();

            var from = ordered[0];
            var to   = ordered[ordered.Count - 1];
            var via  = ordered.GetRange(1, ordered.Count - 2);   // escales intermédiaires
            return BuildContract(map, difficulty, from, to, totalDist, totalDur, via);
        }

        // Calcule une étape (route réelle ou repli grand-cercle).
        private async System.Threading.Tasks.Task<(float dist, float dur)> LegAsync(
            Systems.Map.MapSystem map, CityEntry a, CityEntry b, VehicleRoutingProfile profile)
        {
            var route = await map.GetRouteAsync(a, b, profile);
            if (route != null && route.found)
                return (route.distanceKm, route.durationSeconds);

            float dist = (float)(Entities.Map.GeoPoint.HaversineKm(a.location, b.location) * FallbackDetourFactor);
            float speed = profile == VehicleRoutingProfile.HeavyGoodsVehicle ? FallbackSpeedKmhHgv : FallbackSpeedKmhCar;
            return (dist, dist / UnityEngine.Mathf.Max(1f, speed) * 3600f);
        }

        // Assemble le ContractData (direct si via == null, sinon tournée à escales).
        private ContractData BuildContract(
            Systems.Map.MapSystem map, ContractDifficulty difficulty,
            CityEntry from, CityEntry to, float distance, float duration,
            System.Collections.Generic.List<CityEntry> via)
        {
            int cargoTons = CargoTonsFor(difficulty);
            string cargoLabel = CargoGoods[UnityEngine.Random.Range(0, CargoGoods.Length)];

            bool multi = via != null && via.Count > 0;
            float cargoBonus = 1f + cargoTons * CargoBonusPerTon;
            float multiMult  = multi ? (MultiStopRewardMultiplier + 0.2f * via.Count) : 1f;

            // Priorité : distance (base) > difficulté (multiplicateur) > tournée > cargaison (bonus mineur).
            int reward = Mathf.RoundToInt(distance * BaseDollarsPerKm
                         * DifficultyRewardMultiplier(difficulty) * multiMult * cargoBonus);

            var data = new ContractData
            {
                id = Guid.NewGuid().ToString(),
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
                baseReward = reward,
                isMultiStop = multi
            };

            var names = new System.Collections.Generic.List<string> { from.displayName };
            if (multi)
                foreach (var c in via)
                {
                    data.viaCityIds.Add(c.id);
                    data.viaAddressLabels.Add(map.GetRandomAddressLabel(c));
                    names.Add(c.displayName);
                }
            names.Add(to.displayName);
            data.displayName = string.Join(" → ", names);
            return data;
        }

        private static CityEntry HomeCity(Systems.Map.MapSystem map)
        {
            // Dépôt = vraie position de l'entreprise (entrée « maison »), pas la ville la plus proche.
            if (map.Catalog?.Home != null) return map.Catalog.Home;

            var company = GameManager.Instance?.Save?.company;
            if (company == null || !company.hasLocationCoordinates) return null;
            return map.GetNearestCity(company.locationLatitude, company.locationLongitude);
        }

        private static bool MultiStopUnlocked() =>
            (ServiceLocator.Get<SkillTreeSystem>()?.Flat(SkillEffectType.MultiStopContractsUnlocked) ?? 0) > 0;

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
