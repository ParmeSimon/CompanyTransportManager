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
using TransportManager.Systems.Progression;

namespace TransportManager.Systems.Hr
{
    public class HrSystem
    {
        // ── Constantes d'équilibrage (réglage facile) ──────────────────────────────
        public const int BasePoolSize             = 3;    // candidats au départ
        public const int BaseRefreshCooldownHours = 24;   // délai d'auto-régénération de base
        public const int GoldRefreshCost          = 2;    // skip payé en lingots (forfait)
        public const int BaseDollarRefreshCost    = 500;  // skip payé en dollars (× (n+1) dans la journée)

        private readonly GameSaveData _save;

        public HrSystem(GameSaveData save) { _save = save; }

        public IReadOnlyList<DriverInstance> HiredDrivers => _save.hiredDrivers;
        public IReadOnlyList<DriverInstance> RecruitmentPool => _save.recruitmentPool;

        public DriverInstance GetHired(string id) => _save.hiredDrivers.Find(d => d.instanceId == id);
        public DriverInstance GetCandidate(string id) => _save.recruitmentPool.Find(d => d.instanceId == id);

        // ── Modificateurs issus de l'arbre de compétences ──────────────────────────
        private SkillTreeSystem Skills => ServiceLocator.Get<SkillTreeSystem>();

        public int CurrentPoolSize => BasePoolSize + (Skills?.Flat(SkillEffectType.RecruitmentPoolSizeBonus) ?? 0);

        public bool RefreshIsFree         => (Skills?.Flat(SkillEffectType.HrRefreshFree) ?? 0) > 0;
        public bool DollarRefreshUnlocked => (Skills?.Flat(SkillEffectType.HrRefreshPayWithDollars) ?? 0) > 0;

        public TimeSpan RefreshCooldown
        {
            get
            {
                if ((Skills?.Flat(SkillEffectType.HrRefreshInstant) ?? 0) > 0) return TimeSpan.Zero;
                int reduction = Skills?.Flat(SkillEffectType.HrRefreshHoursReduction) ?? 0;
                return TimeSpan.FromHours(Mathf.Max(0, BaseRefreshCooldownHours - reduction));
            }
        }

        // ── Vivier ─────────────────────────────────────────────────────────────────
        public void EnsureRecruitmentPool()
        {
            // Réduit le vivier s'il dépasse la taille courante (ancienne sauvegarde à 8,
            // ou plafond abaissé). On retire les candidats en trop par la fin.
            if (_save.recruitmentPool.Count > CurrentPoolSize)
                _save.recruitmentPool.RemoveRange(CurrentPoolSize, _save.recruitmentPool.Count - CurrentPoolSize);

            // Amorçage initial UNIQUEMENT : ensuite le vivier ne se recomplète que via un
            // refresh. Les embauches laissent donc des emplacements vides jusqu'au prochain refresh.
            if (_save.lastHrRefreshUtcTicks == 0)
            {
                while (_save.recruitmentPool.Count < CurrentPoolSize)
                    _save.recruitmentPool.Add(DriverGenerator.Generate());
                _save.lastHrRefreshUtcTicks = DateTime.UtcNow.Ticks;
            }
        }

        /// Régénère intégralement le vivier jusqu'à la taille courante (refresh gratuit ou payant).
        public void RefreshRecruitmentPool()
        {
            _save.recruitmentPool.Clear();
            while (_save.recruitmentPool.Count < CurrentPoolSize)
                _save.recruitmentPool.Add(DriverGenerator.Generate());
            _save.lastHrRefreshUtcTicks = DateTime.UtcNow.Ticks;
        }

        // ── Refresh GRATUIT (verrouillé par le cooldown) ────────────────────────────

        /// Secondes restantes avant que le refresh gratuit soit dispo (0 = dispo / instantané).
        public double SecondsUntilFreeRefresh()
        {
            var cd = RefreshCooldown;
            if (cd <= TimeSpan.Zero) return 0;
            double elapsedSec = (DateTime.UtcNow.Ticks - _save.lastHrRefreshUtcTicks) / (double)TimeSpan.TicksPerSecond;
            double remaining = cd.TotalSeconds - elapsedSec;
            return remaining > 0 ? remaining : 0;
        }

        public bool FreeRefreshReady => SecondsUntilFreeRefresh() <= 0.0;

        /// Refresh gratuit : autorisé seulement quand le cooldown est écoulé.
        public bool TryFreeRefresh()
        {
            if (!FreeRefreshReady) return false;
            RefreshRecruitmentPool();
            return true;
        }

        /// Tous les paliers « +1 candidat » non encore débloqués (un emplacement verrouillé chacun),
        /// triés par profondeur dans l'arbre.
        public List<SkillNodeDefinition> LockedPoolNodes()
        {
            var skills = Skills;
            var list = new List<SkillNodeDefinition>();
            foreach (var n in SkillTreeCatalog.All)
            {
                if (n.effect != SkillEffectType.RecruitmentPoolSizeBonus) continue;
                if (skills != null && skills.IsUnlocked(n.id)) continue;
                list.Add(n);
            }
            list.Sort((a, b) => a.tier.CompareTo(b.tier));
            return list;
        }

        // ── Refresh PAYANT (skip immédiat du cooldown) ──────────────────────────────
        public struct RefreshCost
        {
            public bool free;
            public CurrencyType currency;
            public int amount;
        }

        /// Coût du refresh payant (gratuit si capstone > dollars croissants si débloqué > lingots).
        public RefreshCost CurrentPaidRefreshCost()
        {
            if (RefreshIsFree)
                return new RefreshCost { free = true, currency = CurrencyType.Dollar, amount = 0 };
            if (DollarRefreshUnlocked)
                return new RefreshCost { free = false, currency = CurrencyType.Dollar,
                                         amount = BaseDollarRefreshCost * (PaidRefreshesTodaySynced() + 1) };
            return new RefreshCost { free = false, currency = CurrencyType.GoldIngot, amount = GoldRefreshCost };
        }

        /// Refresh payant immédiat. Renvoie false (avec raison) si fonds insuffisants.
        public bool TryPaidRefresh(out string reason)
        {
            reason = null;
            var cost = CurrentPaidRefreshCost();
            if (!cost.free)
            {
                var wallet = ServiceLocator.Get<WalletSystem>();
                if (wallet == null) { reason = "Portefeuille indisponible."; return false; }
                if (!wallet.TrySpend(cost.currency, cost.amount))
                {
                    reason = cost.currency == CurrencyType.Dollar ? "Dollars insuffisants." : "Lingots insuffisants.";
                    return false;
                }
                if (cost.currency == CurrencyType.Dollar)
                {
                    PaidRefreshesTodaySynced();   // garantit que le compteur correspond au jour courant
                    _save.hrPaidRefreshesToday++; // escalade le coût du prochain refresh du jour
                }
            }
            RefreshRecruitmentPool();
            return true;
        }

        // Compteur de refresh payants du jour, remis à zéro au changement de jour (UTC).
        private int PaidRefreshesTodaySynced()
        {
            long today = DateTime.UtcNow.Date.Ticks;
            if (_save.hrRefreshCounterDayUtcTicks != today)
            {
                _save.hrRefreshCounterDayUtcTicks = today;
                _save.hrPaidRefreshesToday = 0;
            }
            return _save.hrPaidRefreshesToday;
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

            var skills = ServiceLocator.Get<SkillTreeSystem>();

            var wallet = ServiceLocator.Get<WalletSystem>();
            float wageReduction = skills?.Pct(SkillEffectType.DriverWageReduction) ?? 0f;
            int wage = Mathf.Max(0, Mathf.RoundToInt(driver.assignedWagePerContract * (1f - wageReduction)));
            wallet?.TrySpend(CurrencyType.Dollar, wage);

            int prevLevel = XpCurve.DriverLevelFromXp(driver.xp);
            float xpBonus = skills?.Pct(SkillEffectType.DriverXpGainBonus) ?? 0f;
            int xpGain = Mathf.RoundToInt(XpCurve.ContractXpReward(distanceKm) * (1f + xpBonus));
            driver.xp += xpGain;
            driver.contractsCompleted++;

            int newLevel = XpCurve.DriverLevelFromXp(driver.xp);

            // Montée de niveau → les stats grimpent (graine de talent conservée).
            if (newLevel != prevLevel && driver.statSeed != 0)
                DriverGenerator.ApplyStatsForLevel(driver.stats, newLevel, driver.statSeed);

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
