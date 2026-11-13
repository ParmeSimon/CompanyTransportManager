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

        public void ApplyOfflineProgress() => Tick();

        /// <summary>
        /// Fait avancer tout ce qui dépend de l'horloge réelle : livraison de carburant,
        /// contrats arrivés à terme, accidents échus. Appelé au lancement ET régulièrement
        /// pendant le jeu (GameManager.Update) — sans ça, rien ne se terminait avant un
        /// redémarrage. Renvoie true si quelque chose a changé (→ sauvegarde).
        /// </summary>
        public bool Tick()
        {
            bool changed = false;

            // Fuel station refill timer must tick before contracts so that any
            // vehicle waiting on a delivery sees the new tank level.
            var fuel = ServiceLocator.Get<Systems.Fuel.FuelSystem>();
            if (fuel != null)
            {
                bool wasRefilling = fuel.IsRefilling;
                fuel.TickOfflineProgress();
                if (wasRefilling && !fuel.IsRefilling) changed = true;
            }

            var contractSystem = ServiceLocator.Get<ContractSystem>();
            var fleet = ServiceLocator.Get<FleetSystem>();
            var catalog = ServiceLocator.Get<VehicleCatalog>();
            if (contractSystem == null || fleet == null) return changed;

            List<(string contractId, VehicleData data)> ready = null;
            foreach (var c in _save.activeContracts)
            {
                if (!c.IsReadyToComplete && !c.IsAccidentDue) continue;
                var v = fleet.GetById(c.assignedVehicleInstanceId);
                var data = catalog != null ? catalog.GetById(v?.vehicleDataId) : null;
                if (ready == null) ready = new List<(string contractId, VehicleData data)>();
                ready.Add((c.instanceId, data));
            }

            if (ready != null)
                foreach (var entry in ready)
                    if (contractSystem.TryCompleteIfReady(entry.contractId, entry.data)) changed = true;

            // Demandes d'augmentation restées sans réponse → départ.
            ServiceLocator.Get<Hr.HrSystem>()?.TickRaiseDeadlines();

            return changed;
        }
    }
}
