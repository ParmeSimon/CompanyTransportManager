m)^^^^=$o74
8596using System;
usin
gàoçk

^=)^$ù)m TransportManager.Core;
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

            var contractSystem = ServiceLocator.Get<ContractSystem>();
            var fleet = ServiceLocator.Get<FleetSystem>();
            if (contractSystem == null || fleet == null) return;

            // Finalize any contracts whose ETA has passed while offline.
            // We snapshot the ids because TryCompleteIfReady mutates the list.
            var ready = new System.Collections.Generic.List<(string contractId, int vehicleMaxKm)>();
            foreach (var c in _save.activeContracts)
            {
                if (!c.IsReadyToComplete) continue;
                var v = fleet.GetById(c.assignedVehicleInstanceId);
                int maxKm = ResolveVehicleMaxKm(v?.vehicleDataId);
                ready.Add((c.instanceId, maxKm));
            }

            foreach (var entry in ready)
            {
                contractSystem.TryCompleteIfReady(entry.contractId, entry.vehicleMaxKm);
            }
        }

        private int ResolveVehicleMaxKm(string vehicleDataId)
        {
            var catalog = ServiceLocator.Get<VehicleCatalog>();
            return catalog != null ? catalog.GetMaxKilometers(vehicleDataId) : 0;
        }
    }
}
