using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using TransportManager.Core;
using TransportManager.Entities.Contracts;
using TransportManager.Enums;

namespace TransportManager.Systems.Contracts
{
    public class ContractGenerator
    {
        private const float AddressVariancePercent = 0.05f;   // ±5% jitter
        private const float BaseDollarsPerKm = 2.5f;

        public async Task<ContractData> GenerateAsync(VehicleRoutingProfile profile, ContractDifficulty difficulty)
        {
            var map = ServiceLocator.Get<Systems.Map.MapSystem>();
            if (map == null || !map.HasCities) return null;

            var (from, to) = map.GetRandomCityPair();
            if (from == null || to == null) return null;

            var route = await map.GetRouteAsync(from, to, profile);
            if (route == null || !route.found) return null;

            float jitter = 1f + UnityEngine.Random.Range(-AddressVariancePercent, AddressVariancePercent);
            float distance = route.distanceKm * jitter;
            float duration = route.durationSeconds * jitter;

            int reward = Mathf.RoundToInt(distance * BaseDollarsPerKm * DifficultyRewardMultiplier(difficulty));

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
                requiredCapacity = DifficultyMinCapacity(difficulty),
                baseReward = reward
            };
        }

        public async Task RefreshAvailablePool(List<ContractData> pool, int targetCount, VehicleRoutingProfile profile)
        {
            if (pool == null) return;
            int safety = targetCount * 4;
            while (pool.Count < targetCount && safety-- > 0)
            {
                var c = await GenerateAsync(profile, RandomDifficulty());
                if (c == null) break;
                pool.Add(c);
            }
        }

        private static ContractDifficulty RandomDifficulty()
        {
            float r = UnityEngine.Random.value;
            if (r < 0.55f) return ContractDifficulty.Easy;
            if (r < 0.85f) return ContractDifficulty.Medium;
            if (r < 0.97f) return ContractDifficulty.Hard;
            return ContractDifficulty.Premium;
        }

        private static float DifficultyRewardMultiplier(ContractDifficulty d) => d switch
        {
            ContractDifficulty.Easy => 1f,
            ContractDifficulty.Medium => 1.5f,
            ContractDifficulty.Hard => 2.2f,
            ContractDifficulty.Premium => 3.5f,
            _ => 1f
        };

        private static int DifficultyMinCapacity(ContractDifficulty d) => d switch
        {
            ContractDifficulty.Easy => 0,
            ContractDifficulty.Medium => 5,
            ContractDifficulty.Hard => 15,
            ContractDifficulty.Premium => 30,
            _ => 0
        };
    }
}
