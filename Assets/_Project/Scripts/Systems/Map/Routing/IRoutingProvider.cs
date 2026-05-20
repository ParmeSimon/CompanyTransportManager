using System.Threading.Tasks;
using TransportManager.Entities.Map;
using TransportManager.Enums;

namespace TransportManager.Systems.Map.Routing
{
    public interface IRoutingProvider
    {
        Task<RouteResult> GetRouteAsync(
            CityEntry from,
            CityEntry to,
            VehicleRoutingProfile profile);
    }
}
