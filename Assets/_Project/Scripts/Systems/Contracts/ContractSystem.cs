using System;
using System.Collections.Generic;
using UnityEngine;
using TransportManager.Core;
using TransportManager.Entities.Contracts;
using TransportManager.Entities.Drivers;
using TransportManager.Entities.Vehicles;
using TransportManager.Enums;
using TransportManager.Events;
using TransportManager.Save;
using TransportManager.Systems.Accidents;
using TransportManager.Systems.Economy;
using TransportManager.Systems.Fleet;
using TransportManager.Systems.Hr;
using TransportManager.Systems.Maintenance;
using TransportManager.Systems.Progression;

namespace TransportManager.Systems.Contracts
{
    public class ContractSystem
    {
        private readonly GameSaveData _save;

        public ContractSystem(GameSaveData save) { _save = save; }

        public IReadOnlyList<ContractData> Available => _save.availableContracts;
        public IReadOnlyList<ContractInstance> Active => _save.activeContracts;

        public void AddToPool(ContractData def)
        {
            if (def != null && !_save.availableContracts.Contains(def))
                _save.availableContracts.Add(def);
        }

        public bool CanAttempt(ContractData contract, VehicleData data, DriverInstance driver = null)
        {
            if (contract == null || data == null) return false;
            float consumption = data.fuelConsumptionLPer100Km;
            if (driver != null) consumption *= driver.stats.FuelConsumptionMultiplier;
            consumption *= (1f - (ServiceLocator.Get<SkillTreeSystem>()?.Pct(SkillEffectType.FuelConsumptionReduction) ?? 0f));
            if (consumption <= 0f) return false;
            float maxRange = data.fuelTankCapacityLiters * 100f / consumption;
            return maxRange >= contract.distanceKm;
        }

        public ContractInstance StartContract(ContractData definition, VehicleInstance vehicle, VehicleData data)
        {
            if (definition == null || vehicle == null || data == null) return null;
            if (vehicle.status != VehicleStatus.Idle) return null;
            if (string.IsNullOrEmpty(vehicle.assignedDriverInstanceId)) return null;
            if (data.speedKmh <= 0f) return null;

            var hr = ServiceLocator.Get<HrSystem>();
            var driver = hr?.GetHired(vehicle.assignedDriverInstanceId);
            if (driver == null) return null;

            var skills = ServiceLocator.Get<SkillTreeSystem>();
            float effectiveSpeed = data.speedKmh * driver.stats.SpeedMultiplier
                * (1f + (skills?.Pct(SkillEffectType.TripSpeedBonus) ?? 0f));
            float effectiveConsumption = data.fuelConsumptionLPer100Km * driver.stats.FuelConsumptionMultiplier
                * (1f - (ServiceLocator.Get<SkillTreeSystem>()?.Pct(SkillEffectType.FuelConsumptionReduction) ?? 0f));
            if (effectiveSpeed <= 0f || effectiveConsumption <= 0f) return null;
            float effectiveMaxRange = data.fuelTankCapacityLiters * 100f / effectiveConsumption;
            if (effectiveMaxRange < definition.distanceKm) return null;

            float fuelNeeded = definition.distanceKm / 100f * effectiveConsumption;
            var fuelSystem = ServiceLocator.Get<Systems.Fuel.FuelSystem>();
            if (fuelSystem == null) return null;
            if (!fuelSystem.TryEnsureVehicleHasFuel(vehicle, data, fuelNeeded)) return null;

            vehicle.currentFuelLiters = Mathf.Max(0f, vehicle.currentFuelLiters - fuelNeeded);
            GameEvents.RaiseVehicleFuelChanged(vehicle);

            DateTime now = DateTime.UtcNow;
            float durationSeconds = definition.distanceKm / effectiveSpeed * 3600f;
            DateTime completion = now.AddSeconds(durationSeconds);

            var instance = new ContractInstance
            {
                instanceId = Guid.NewGuid().ToString(),
                definition = definition,
                assignedVehicleInstanceId = vehicle.instanceId,
                startTimeUtcTicks = now.Ticks,
                completionTimeUtcTicks = completion.Ticks,
                status = ContractStatus.InProgress
            };

            vehicle.status = VehicleStatus.OnContract;
            vehicle.activeContractInstanceId = instance.instanceId;

            _save.activeContracts.Add(instance);
            _save.availableContracts.Remove(definition);

            PrecomputeAccidentForContract(instance, driver, data);

            GameEvents.RaiseContractStarted(instance);
            GameEvents.RaiseVehicleStatusChanged(vehicle);
            return instance;
        }

        public bool TryCompleteIfReady(string contractInstanceId, VehicleData data)
        {
            int idx = _save.activeContracts.FindIndex(c => c.instanceId == contractInstanceId);
            if (idx < 0) return false;

            var contract = _save.activeContracts[idx];
            if (contract.status != ContractStatus.InProgress) return false;

            // L'accident est vérifié en priorité — il peut interrompre le trajet avant la fin.
            if (contract.IsAccidentDue)
            {
                FinalizeAccident(contract, data);
                _save.activeContracts.RemoveAt(idx);
                return true;
            }

            if (!contract.IsReadyToComplete) return false;

            Finalize(contract, data);
            _save.activeContracts.RemoveAt(idx);
            return true;
        }

        /// <summary>
        /// Vérifie et déclenche un accident en cours de trajet sans attendre la complétion.
        /// À appeler depuis la boucle de jeu pour les notifications temps-réel.
        /// </summary>
        public bool TryTriggerAccidentIfDue(string contractInstanceId, VehicleData data)
        {
            int idx = _save.activeContracts.FindIndex(c => c.instanceId == contractInstanceId);
            if (idx < 0) return false;

            var contract = _save.activeContracts[idx];
            if (contract.status != ContractStatus.InProgress || !contract.IsAccidentDue) return false;

            FinalizeAccident(contract, data);
            _save.activeContracts.RemoveAt(idx);
            return true;
        }

        public bool SkipWithGoldIngots(string contractInstanceId, int ingotCost, VehicleData data)
        {
            var contract = _save.activeContracts.Find(c => c.instanceId == contractInstanceId);
            if (contract == null || contract.status != ContractStatus.InProgress) return false;

            var wallet = ServiceLocator.Get<WalletSystem>();
            if (wallet == null) return false;
            if (!wallet.TrySpend(CurrencyType.GoldIngot, ingotCost)) return false;

            contract.completionTimeUtcTicks = DateTime.UtcNow.Ticks;
            return TryCompleteIfReady(contractInstanceId, data);
        }

        // ── Livraison normale (aucun accident) ───────────────────────────────

        private void Finalize(ContractInstance contract, VehicleData data)
        {
            var wallet = ServiceLocator.Get<WalletSystem>();
            float rewardBonus = ServiceLocator.Get<SkillTreeSystem>()?.Pct(SkillEffectType.ContractRewardBonus) ?? 0f;
            int reward = Mathf.RoundToInt(contract.definition.baseReward * (1f + rewardBonus));
            wallet?.Add(CurrencyType.Dollar, reward);

            var fleet   = ServiceLocator.Get<FleetSystem>();
            var vehicle = fleet?.GetById(contract.assignedVehicleInstanceId);

            if (vehicle != null)
            {
                vehicle.totalKilometers          += Mathf.RoundToInt(contract.definition.distanceKm);
                vehicle.activeContractInstanceId  = null;
                vehicle.status                    = VehicleStatus.Idle;

                if (data != null)
                    ServiceLocator.Get<MaintenanceSystem>()?.EvaluateAfterContract(vehicle, data.maxKilometers);

                if (!string.IsNullOrEmpty(vehicle.assignedDriverInstanceId))
                {
                    var hr     = ServiceLocator.Get<HrSystem>();
                    var driver = hr?.GetHired(vehicle.assignedDriverInstanceId);
                    if (driver != null && hr != null)
                    {
                        AccidentSystem.ApplyFatigueCycle(driver, contract.startTimeUtcTicks, contract.definition.distanceKm);
                        bool resigned = hr.ProcessPostContract(driver, contract.definition.distanceKm);
                        if (resigned) Debug.Log($"[Contract] {driver.FullName} a démissionné après livraison.");
                    }
                }
            }

            ServiceLocator.Get<XpSystem>()?.AddCompanyXpForContract(contract.definition.distanceKm);
            contract.status = ContractStatus.Completed;
            GameEvents.RaiseContractCompleted(contract);
        }

        // ── Accident en cours de trajet (pré-calculé à la signature) ─────────

        private void FinalizeAccident(ContractInstance contract, VehicleData data)
        {
            var accident = new AccidentResult
            {
                severity          = contract.scheduledAccidentSeverity,
                vehicleRepairCost = contract.scheduledAccidentRepairCost,
                description       = contract.scheduledAccidentDescription
            };

            var fleet   = ServiceLocator.Get<FleetSystem>();
            var vehicle = fleet?.GetById(contract.assignedVehicleInstanceId);
            var hr      = ServiceLocator.Get<HrSystem>();

            DriverInstance driver = null;
            if (vehicle != null && !string.IsNullOrEmpty(vehicle.assignedDriverInstanceId))
                driver = hr?.GetHired(vehicle.assignedDriverInstanceId);

            // Kilométrage partiel jusqu'au point d'accident
            float progress    = Mathf.Clamp01(contract.AccidentProgressRatio);
            float kmAtCrash   = contract.definition.distanceKm * progress;

            if (vehicle != null)
            {
                vehicle.totalKilometers         += Mathf.RoundToInt(kmAtCrash);
                vehicle.activeContractInstanceId = null;
                vehicle.status                   = VehicleStatus.Idle;

                if (data != null)
                    ServiceLocator.Get<MaintenanceSystem>()?.EvaluateAfterContract(vehicle, data.maxKilometers);
            }

            if (driver != null)
            {
                // Fatigue accumulée jusqu'au crash (distance partielle)
                AccidentSystem.ApplyFatigueCycle(driver, contract.startTimeUtcTicks, kmAtCrash);

                if (accident.IsFatal)
                {
                    if (vehicle != null) vehicle.assignedDriverInstanceId = null;
                    hr?.KillDriver(driver);
                    Debug.Log($"[Accident FATAL] {driver.FullName} a péri dans un accident ({progress:P0} du trajet).");
                }
                else
                {
                    if (accident.vehicleRepairCost > 0)
                        ServiceLocator.Get<WalletSystem>()?.TrySpend(CurrencyType.Dollar, accident.vehicleRepairCost);

                    GameEvents.RaiseDriverAccident(driver, accident);
                    Debug.Log($"[Accident] {driver.FullName} — {accident.description} à {progress:P0} du trajet, réparation : {accident.vehicleRepairCost:N0} $");

                    // Le conducteur ne perçoit pas de salaire (contrat annulé)
                    driver.contractsCompleted++;
                    GameEvents.RaiseDriverXpChanged(driver);
                }
            }

            // Pas de récompense, pas d'XP entreprise — contrat annulé
            contract.status = ContractStatus.Completed;
            GameEvents.RaiseContractCompleted(contract);
        }

        // ── Pré-calcul de l'accident à la signature du contrat ────────────────

        private void PrecomputeAccidentForContract(ContractInstance instance, DriverInstance driver, VehicleData data)
        {
            if (driver == null || data == null) return;

            float fatigueAtEnd = AccidentSystem.ComputeFatigueAtEnd(
                driver, instance.startTimeUtcTicks, instance.definition.distanceKm);

            var result = AccidentSystem.Roll(
                driver, fatigueAtEnd, instance.definition.distanceKm, data.purchasePrice);

            if (!result.IsAccident) return;

            instance.scheduledAccidentTimeTicks      = AccidentSystem.ScheduleAccidentTime(instance.startTimeUtcTicks, instance.completionTimeUtcTicks);
            instance.scheduledAccidentSeverity       = result.severity;
            instance.scheduledAccidentRepairCost     = result.vehicleRepairCost;
            instance.scheduledAccidentDescription    = result.description;

            Debug.Log($"[Accident prévu] {driver.FullName} — {result.description} à {new DateTime(instance.scheduledAccidentTimeTicks, DateTimeKind.Utc):HH:mm} UTC");
        }
    }
}
