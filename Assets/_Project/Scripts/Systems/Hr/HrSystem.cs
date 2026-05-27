using System;
using System.Collections.Generic;
using UnityEngine;
using TransportManager.Core;
using TransportManager.Entities.Drivers;
using TransportManager.Entities.Progression;
using TransportManager.Enums;
using TransportManager.Events;
using TransportManager.Save;
using TransportManager.Systems.Economy;
using TransportManager.Systems.Fleet;

namespace TransportManager.Systems.Hr
{
    public class HrSystem
    {
        public const int RecruitmentPoolSize = 5;

        private readonly GameSaveData _save;

        public HrSystem(GameSaveData save) { _save = save; }

        public IReadOnlyList<DriverInstance> HiredDrivers => _save.hiredDrivers;
        public IReadOnlyList<DriverInstance> RecruitmentPool => _save.recruitmentPool;

        public DriverInstance GetHired(string id) => _save.hiredDrivers.Find(d => d.instanceId == id);
        public DriverInstance GetCandidate(string id) => _save.recruitmentPool.Find(d => d.instanceId == id);

        public void EnsureRecruitmentPool()
        {
            while (_save.recruitmentPool.Count < RecruitmentPoolSize)
            {
                _save.recruitmentPool.Add(DriverGenerator.Generate());
            }
            if (_save.lastHrRefreshUtcTicks == 0) _save.lastHrRefreshUtcTicks = DateTime.UtcNow.Ticks;
        }

        public void RefreshRecruitmentPool()
        {
            _save.recruitmentPool.Clear();
            EnsureRecruitmentPool();
            _save.lastHrRefreshUtcTicks = DateTime.UtcNow.Ticks;
        }

        public bool Hire(string candidateId, int wagePerContract)
        {
            int idx = _save.recruitmentPool.FindIndex(d => d.instanceId == candidateId);
            if (idx < 0) return false;
            var driver = _save.recruitmentPool[idx];

            driver.hired = true;
            driver.assignedWagePerContract = Mathf.Max(0, wagePerContract);
            driver.hiredAtUtcTicks = DateTime.UtcNow.Ticks;

            _save.recruitmentPool.RemoveAt(idx);
            _save.hiredDrivers.Add(driver);
            GameEvents.RaiseDriverHired(driver);
            return true;
        }

        public bool Fire(string driverInstanceId)
        {
            var driver = GetHired(driverInstanceId);
            if (driver == null) return false;
            RemoveFromPayroll(driver);
            GameEvents.RaiseDriverFired(driver);
            return true;
        }

        public bool AssignToVehicle(string driverInstanceId, string vehicleInstanceId)
        {
            var driver = GetHired(driverInstanceId);
            if (driver == null) return false;

            var fleet = ServiceLocator.Get<FleetSystem>();
            var vehicle = fleet?.GetById(vehicleInstanceId);
            if (vehicle == null) return false;

            // Free the vehicle from any previous driver
            var previousDriver = _save.hiredDrivers.Find(d => d.assignedVehicleInstanceId == vehicleInstanceId);
            if (previousDriver != null && previousDriver != driver)
            {
                previousDriver.assignedVehicleInstanceId = null;
                GameEvents.RaiseDriverAssigned(previousDriver);
            }

            // Free the driver from any previous vehicle
            if (!string.IsNullOrEmpty(driver.assignedVehicleInstanceId))
            {
                var prev = fleet.GetById(driver.assignedVehicleInstanceId);
                if (prev != null) prev.assignedDriverInstanceId = null;
            }

            driver.assignedVehicleInstanceId = vehicleInstanceId;
            vehicle.assignedDriverInstanceId = driverInstanceId;
            GameEvents.RaiseDriverAssigned(driver);
            return true;
        }

        public bool UnassignFromVehicle(string driverInstanceId)
        {
            var driver = GetHired(driverInstanceId);
            if (driver == null || string.IsNullOrEmpty(driver.assignedVehicleInstanceId)) return false;

            var fleet = ServiceLocator.Get<FleetSystem>();
            var vehicle = fleet?.GetById(driver.assignedVehicleInstanceId);
            if (vehicle != null) vehicle.assignedDriverInstanceId = null;

            driver.assignedVehicleInstanceId = null;
            GameEvents.RaiseDriverAssigned(driver);
            return true;
        }

        public bool SetWage(string driverInstanceId, int wagePerContract)
        {
            var driver = GetHired(driverInstanceId);
            if (driver == null || wagePerContract < 0) return false;
            driver.assignedWagePerContract = wagePerContract;
            GameEvents.RaiseDriverWageChanged(driver);
            return true;
        }

        // Called by ContractSystem when a contract is finalised.
        // Returns true if the driver resigned.
        public bool ProcessPostContract(DriverInstance driver, float distanceKm)
        {
            if (driver == null) return false;

            var wallet = ServiceLocator.Get<WalletSystem>();
            wallet?.TrySpend(CurrencyType.Dollar, driver.assignedWagePerContract);

            int xpGain = XpCurve.ContractXpReward(distanceKm);
            driver.xp += xpGain;
            driver.contractsCompleted++;

            int newLevel = XpCurve.DriverLevelFromXp(driver.xp);
            driver.desiredWagePerContract = DriverGenerator.ComputeDesiredWage(newLevel, driver.stats);

            GameEvents.RaiseDriverXpChanged(driver);
            return RollResignation(driver);
        }

        private bool RollResignation(DriverInstance driver)
        {
            float satisfaction = driver.assignedWagePerContract / (float)Mathf.Max(1, driver.desiredWagePerContract);
            if (satisfaction >= 0.7f) return false;

            float chance = (1f - satisfaction) * 0.5f;
            if (UnityEngine.Random.value > chance) return false;

            GameEvents.RaiseDriverResigned(driver);
            RemoveFromPayroll(driver);
            return true;
        }

        /// <summary>Supprime définitivement un conducteur suite à un accident mortel.</summary>
        public void KillDriver(DriverInstance driver)
        {
            RemoveFromPayroll(driver);
            GameEvents.RaiseDriverDied(driver);
        }

        private void RemoveFromPayroll(DriverInstance driver)
        {
            if (!string.IsNullOrEmpty(driver.assignedVehicleInstanceId))
            {
                var fleet = ServiceLocator.Get<FleetSystem>();
                var vehicle = fleet?.GetById(driver.assignedVehicleInstanceId);
                if (vehicle != null) vehicle.assignedDriverInstanceId = null;
            }
            _save.hiredDrivers.Remove(driver);
        }
    }
}
