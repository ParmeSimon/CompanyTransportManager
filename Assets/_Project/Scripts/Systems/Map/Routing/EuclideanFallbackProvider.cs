using System;
using System.Threading.Tasks;
using TransportManager.Entities.Map;
using TransportManager.Enums;

namespace TransportManager.Systems.Map.Routing
{
    // Offline fallback : great-circle distance with a detour factor.
    // Used when no API key is configured, no network, or for unit tests.
    public class EuclideanFallbackProvider : IRoutingProvider
    {
        private const float DetourFactor = 1.3f;           // roads are ~30% longer than great-circle
        private const float AverageSpeedKmhCar = 80f;
        private const float AverageSpeedKmhHgv = 65f;

        public Task<RouteResult> GetRouteAsync(CityEntry from, CityEntry to, VehicleRoutingProfile profile)
        {
            double straightKm = GeoPoint.HaversineKm(from.location, to.location);
            float distanceKm = (float)(straightKm * DetourFactor);
            float speed = profile == VehicleRoutingProfile.HeavyGoodsVehicle ? AverageSpeedKmhHgv : AverageSpeedKmhCar;

            var result = new RouteResult
            {
                fromCityId = from.id,
                toCityId = to.id,
                distanceKm = distanceKm,
                durationSeconds = distanceKm / Math.Max(1f, speed) * 3600f,
                ascentMeters = 0f,
                descentMeters = 0f,
                found = true,
                fetchedAtUtcTicks = DateTime.UtcNow.Ticks
            };
            result.polyline.Add(from.location);
            result.polyline.Add(to.location);
            return Task.FromResult(result);
        }
    }
}
