using System;
using System.Collections.Generic;
using TransportManager.Core;
using TransportManager.Entities.Vehicles;
using TransportManager.Save;
using TransportManager.Systems.Contracts;
using TransportManager.Systems.Fleet;

namespace TransportManager.Systems.Time
{
    public class OfflineTimeService
    {
        private readonly GameSaveData _save;

        public OfflineTimeService(GameSaveData save) { _save = save; }

        public TimeSpan TimeSinceLastSave()
        {
            if (_save.lastSaveUtcTicks == 0) return TimeSpan.Zero;
            long diff = DateTime.UtcNow.Ticks - _save.lastSaveUtcTicks;
            return diff <= 0 ? TimeSpan.Zero : new TimeSpan(diff);
        }

        public void ApplyOfflineProgress()
        {
            var elapsed = TimeSinceLastSave();
            if (elapsed == TimeSpan.Zero) return;

            // Fuel station refill timer must tick before contracts so that any
            // vehicle waiting on a delivery sees the new tank level.
            var fuel = ServiceLocator.Get<Systems.Fuel.FuelSystem>();
            fuel?.TickOfflineProgress();

            var contractSystem = ServiceLocator.Get<ContractSystem>();
            var fleet = ServiceLocator.Get<FleetSystem>();
            var catalog = ServiceLocator.Get<VehicleCatalog>();
            if (contractSystem == null || fleet == null) return;

            var ready = new List<(string contractId, VehicleData data)>();
            foreach (var c in _save.activeContracts)
            {
                if (!c.IsReadyToComplete) continue;
                var v = fleet.GetById(c.assignedVehicleInstanceId);
                var data = catalog != null ? catalog.GetById(v?.vehicleDataId) : null;
                ready.Add((c.instanceId, data));
            }

            foreach (var entry in ready)
            {
                contractSystem.TryCompleteIfReady(entry.contractId, entry.data);
            }
        }
    }
}
