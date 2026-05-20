using System.Collections.Generic;
using TransportManager.Entities.Vehicles;
using TransportManager.Events;
using TransportManager.Save;

namespace TransportManager.Systems.Fleet
{
    public class FleetSystem
    {
        private readonly GameSaveData _save;

        public FleetSystem(GameSaveData save) { _save = save; }

        public IReadOnlyList<VehicleInstance> Vehicles => _save.vehicles;

        public void Add(VehicleInstance vehicle)
        {
            _save.vehicles.Add(vehicle);
            GameEvents.RaiseVehicleAdded(vehicle);
        }

        public VehicleInstance GetById(string instanceId)
        {
            return _save.vehicles.Find(v => v.instanceId == instanceId);
        }

        public bool Remove(string instanceId)
        {
            var v = GetById(instanceId);
            if (v == null) return false;
            return _save.vehicles.Remove(v);
        }
    }
}
