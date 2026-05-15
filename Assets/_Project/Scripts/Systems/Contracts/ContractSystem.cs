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

        public bool CanAttempt(ContractData contract, VehicleData data, DriverInstance driver = null)
        {
            if (contract == null || data == null) return false;
            float consumption = data.fuelConsumptionLPer100Km;
            if (driver != null) consumption *= (1f - driver.stats.fuelEfficiencyBonus);
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

            float effectiveSpeed = data.speedKmh * (1f + driver.stats.speedBonus);
            float effectiveConsumption = data.fuelConsumptionLPer100Km * (1f - driver.stats.fuelEfficiencyBonus);
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
            if (!contract.IsReadyToComplete) return false;

            Finalize(contract, data);
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

        private void Finalize(ContractInstance contract, VehicleData data)
        {
            var wallet = ServiceLocator.Get<WalletSystem>();
            wallet?.Add(CurrencyType.Dollar, contract.definition.baseReward);

            var fleet = ServiceLocator.Get<FleetSystem>();
            var vehicle = fleet?.GetById(contract.assignedVehicleInstanceId);
            if (vehicle != null)
            {
                vehicle.totalKilometers += Mathf.RoundToInt(contract.definition.distanceKm);
                vehicle.activeContractInstanceId = null;
                vehicle.status = VehicleStatus.Idle;

                if (data != null)
                {
                    var maintenance = ServiceLocator.Get<MaintenanceSystem>();
                    maintenance?.EvaluateAfterContract(vehicle, data.maxKilometers);
                }

                if (!string.IsNullOrEmpty(vehicle.assignedDriverInstanceId))
                {
                    var hr = ServiceLocator.Get<HrSystem>();
                    var driver = hr?.GetHired(vehicle.assignedDriverInstanceId);
                    if (driver != null && hr != null)
                    {
                        bool resigned = hr.ProcessPostContract(driver, contract.definition.distanceKm);
                        if (resigned) Debug.Log($"[Contract] Driver {driver.FullName} a démissionné après livraison.");
                        ServiceLocator.Get<XpSystem>()?.NotifyChanged();
                    }
                }
            }

            contract.status = ContractStatus.Completed;
            GameEvents.RaiseContractCompleted(contract);
        }
    }
}
