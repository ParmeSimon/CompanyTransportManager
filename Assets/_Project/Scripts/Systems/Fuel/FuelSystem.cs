using System;
using UnityEngine;
using TransportManager.Core;
using TransportManager.Entities.Fuel;
using TransportManager.Entities.Vehicles;
using TransportManager.Enums;
using TransportManager.Events;
using TransportManager.Save;
using TransportManager.Systems.Economy;

namespace TransportManager.Systems.Fuel
{
    public class FuelSystem
    {
        private readonly GameSaveData _save;
        private readonly FuelStationConfig _config;

        public FuelSystem(GameSaveData save, FuelStationConfig config)
        {
            _save = save;
            _config = config;
            if (_save.fuelStation.pumpLevel <= 0) _save.fuelStation.pumpLevel = 1;
        }

        public FuelStationState State => _save.fuelStation;
        public FuelStationConfig Config => _config;
        public FuelPumpTier CurrentTier => _config?.GetTier(_save.fuelStation.pumpLevel);

        public float MaxCapacityLiters => CurrentTier != null ? CurrentTier.storageCapacityLiters : 0f;
        public float CurrentLiters => _save.fuelStation.currentLiters;
        public float RemainingCapacity => Mathf.Max(0f, MaxCapacityLiters - CurrentLiters);
        public bool IsRefilling => _save.fuelStation.refillInProgress;

        public TimeSpan RefillRemaining
        {
            get
            {
                if (!IsRefilling) return TimeSpan.Zero;
                long diff = _save.fuelStation.refillCompleteUtcTicks - DateTime.UtcNow.Ticks;
                return diff <= 0 ? TimeSpan.Zero : new TimeSpan(diff);
            }
        }

        // ---- Pump upgrade ----

        public bool CanUpgradePump()
        {
            if (_config == null) return false;
            var next = _config.GetNextTier(_save.fuelStation.pumpLevel);
            if (next == null) return false;
            var wallet = ServiceLocator.Get<WalletSystem>();
            return wallet != null && wallet.CanAfford(CurrencyType.Dollar, next.upgradeCostDollars);
        }

        public bool TryUpgradePump()
        {
            if (_config == null) return false;
            var next = _config.GetNextTier(_save.fuelStation.pumpLevel);
            if (next == null) return false;
            var wallet = ServiceLocator.Get<WalletSystem>();
            if (wallet == null || !wallet.TrySpend(CurrencyType.Dollar, next.upgradeCostDollars)) return false;
            _save.fuelStation.pumpLevel = next.level;
            GameEvents.RaisePumpUpgraded(_save.fuelStation.pumpLevel);
            return true;
        }

        // ---- Station refill ----

        public bool TryStartTruckRefill(float liters)
        {
            if (_config == null || liters <= 0f) return false;
            if (IsRefilling) return false;
            var tier = CurrentTier;
            if (tier == null || tier.storageCapacityLiters <= 0f) return false;

            liters = Mathf.Min(liters, RemainingCapacity);
            if (liters <= 0f) return false;

            int cost = Mathf.CeilToInt(liters * _config.dollarsPerLiter);
            var wallet = ServiceLocator.Get<WalletSystem>();
            if (wallet == null || !wallet.TrySpend(CurrencyType.Dollar, cost)) return false;

            float seconds = liters / tier.storageCapacityLiters * tier.fullRefillDurationSeconds;
            _save.fuelStation.refillInProgress = true;
            _save.fuelStation.pendingRefillLiters = liters;
            _save.fuelStation.refillCompleteUtcTicks = DateTime.UtcNow.AddSeconds(seconds).Ticks;
            GameEvents.RaiseFuelRefillStarted(_save.fuelStation);
            return true;
        }

        public bool TryInstantRefillWithIngots(float liters)
        {
            if (_config == null || liters <= 0f) return false;
            var tier = CurrentTier;
            if (tier == null || tier.storageCapacityLiters <= 0f) return false;

            liters = Mathf.Min(liters, RemainingCapacity);
            if (liters <= 0f) return false;

            int cost = Mathf.Max(0, Mathf.CeilToInt(liters / tier.storageCapacityLiters * tier.instantRefillIngotCost));
            if (cost > 0)
            {
                var wallet = ServiceLocator.Get<WalletSystem>();
                if (wallet == null || !wallet.TrySpend(CurrencyType.GoldIngot, cost)) return false;
            }

            DeliverLiters(liters);
            return true;
        }

        public void TickOfflineProgress()
        {
            if (!IsRefilling) return;
            if (DateTime.UtcNow.Ticks < _save.fuelStation.refillCompleteUtcTicks) return;
            CompleteTruckRefill();
        }

        private void CompleteTruckRefill()
        {
            DeliverLiters(_save.fuelStation.pendingRefillLiters);
            _save.fuelStation.refillInProgress = false;
            _save.fuelStation.pendingRefillLiters = 0f;
            _save.fuelStation.refillCompleteUtcTicks = 0;
            GameEvents.RaiseFuelRefillCompleted(_save.fuelStation);
        }

        private void DeliverLiters(float liters)
        {
            _save.fuelStation.currentLiters = Mathf.Min(
                MaxCapacityLiters,
                _save.fuelStation.currentLiters + liters);
            GameEvents.RaiseStationFuelChanged(_save.fuelStation.currentLiters);
        }

        // ---- Vehicle refueling ----

        public bool TryEnsureVehicleHasFuel(VehicleInstance vehicle, VehicleData data, float requiredLiters)
        {
            if (vehicle == null || data == null) return false;
            if (vehicle.currentFuelLiters >= requiredLiters) return true;

            float deficit = requiredLiters - vehicle.currentFuelLiters;
            float headroom = data.fuelTankCapacityLiters - vehicle.currentFuelLiters;
            float topUp = Mathf.Min(deficit, headroom);
            if (topUp <= 0f) return false;
            if (_save.fuelStation.currentLiters < topUp) return false;

            _save.fuelStation.currentLiters -= topUp;
            vehicle.currentFuelLiters += topUp;
            GameEvents.RaiseStationFuelChanged(_save.fuelStation.currentLiters);
            GameEvents.RaiseVehicleFuelChanged(vehicle);
            return true;
        }

        public bool TryFillVehicleTank(VehicleInstance vehicle, VehicleData data)
        {
            if (vehicle == null || data == null) return false;
            float headroom = data.fuelTankCapacityLiters - vehicle.currentFuelLiters;
            if (headroom <= 0f) return true;

            float available = Mathf.Min(headroom, _save.fuelStation.currentLiters);
            if (available <= 0f) return false;

            _save.fuelStation.currentLiters -= available;
            vehicle.currentFuelLiters += available;
            GameEvents.RaiseStationFuelChanged(_save.fuelStation.currentLiters);
            GameEvents.RaiseVehicleFuelChanged(vehicle);
            return Mathf.Approximately(vehicle.currentFuelLiters, data.fuelTankCapacityLiters);
        }
    }
}
