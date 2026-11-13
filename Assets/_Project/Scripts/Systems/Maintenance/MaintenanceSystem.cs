using UnityEngine;
using TransportManager.Core;
using TransportManager.Entities.Vehicles;
using TransportManager.Enums;
using TransportManager.Events;
using TransportManager.Save;
using TransportManager.Systems.Economy;
using TransportManager.Systems.Progression;

namespace TransportManager.Systems.Maintenance
{
    public class MaintenanceSystem
    {
        public const float MandatoryMaintenanceThreshold = 0.8f;   // seuil de base (sans compétence)

        private readonly GameSaveData _save;

        public MaintenanceSystem(GameSaveData save) { _save = save; }

        /// Seuil d'usure réel : relevé par « Révisions espacées » (plafonné à 95 %).
        public static float CurrentThreshold =>
            Mathf.Min(0.95f, MandatoryMaintenanceThreshold + SkillTreeSystem.PctOf(SkillEffectType.MaintenanceThresholdBonus));

        public bool IsMandatory(VehicleInstance vehicle, int vehicleMaxKm)
        {
            // « Pneus renforcés » : l'usure progresse moins vite (le compteur km, lui, reste exact).
            float wear = vehicle.WearRatio(vehicleMaxKm) * Mathf.Clamp01(1f - SkillTreeSystem.PctOf(SkillEffectType.WearReduction));
            return wear >= CurrentThreshold;
        }

        public void EvaluateAfterContract(VehicleInstance vehicle, int vehicleMaxKm)
        {
            if (!IsMandatory(vehicle, vehicleMaxKm)) return;

            // Capstone Dépôt « Atelier robotisé » : révision automatique et gratuite,
            // sans immobiliser le véhicule.
            bool autoRepair = (ServiceLocator.Get<SkillTreeSystem>()?.Flat(SkillEffectType.AutoRepair) ?? 0) > 0;
            if (autoRepair)
            {
                ApplyRepair(vehicle);
                return;
            }

            vehicle.maintenanceDueAfterContract = true;
            vehicle.status = VehicleStatus.Immobilized;
            GameEvents.RaiseMaintenanceDue(vehicle);
            GameEvents.RaiseVehicleStatusChanged(vehicle);
        }

        public bool TryRepairWithDollars(VehicleInstance vehicle, int maintenanceCost)
        {
            var wallet = ServiceLocator.Get<WalletSystem>();
            if (wallet == null) return false;
            float reduction = ServiceLocator.Get<SkillTreeSystem>()?.Pct(SkillEffectType.RepairCostReduction) ?? 0f;
            int cost = Mathf.Max(0, Mathf.RoundToInt(maintenanceCost * (1f - reduction)));
            if (!wallet.TrySpend(CurrencyType.Dollar, cost)) return false;
            ApplyRepair(vehicle);
            return true;
        }

        public bool TrySkipWithIngots(VehicleInstance vehicle, int ingotCost)
        {
            var wallet = ServiceLocator.Get<WalletSystem>();
            if (wallet == null) return false;
            if (!wallet.TrySpend(CurrencyType.GoldIngot, ingotCost)) return false;
            ApplyRepair(vehicle);
            return true;
        }

        private void ApplyRepair(VehicleInstance vehicle)
        {
            vehicle.totalKilometers = 0;
            vehicle.maintenanceDueAfterContract = false;
            vehicle.status = VehicleStatus.Idle;
            GameEvents.RaiseVehicleStatusChanged(vehicle);
        }
    }
}
