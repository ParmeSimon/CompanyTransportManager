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

        public HrSystem(GameSaveData save)
        {
            _save = save;
            // Nouvelle partie : le vivier a été tiré avant que le joueur choisisse l'adresse de
            // son entreprise. Dès qu'elle est créée, on le régénère avec des pilotes de sa région.
            GameEvents.OnCompanyCreated += () =>
            {
                if (_save.hiredDrivers.Count == 0) RefreshRecruitmentPool();
            };
        }

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
            // Anciennes sauvegardes : genre + origine déduits du prénom (portrait cohérent).
            foreach (var d in _save.hiredDrivers) DriverOrigins.EnsureIdentity(d);
            foreach (var d in _save.recruitmentPool) DriverOrigins.EnsureIdentity(d);

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
            // Salaire relevé à un niveau qui le satisfait → la demande en cours est réglée.
            if (HasRaiseRequest(driver) && wagePerContract >= driver.raiseRequestedWage) ClearRaiseRequest(driver, resetRefusals: true);
            GameEvents.RaiseDriverWageChanged(driver);
            return true;
        }

        // ── Demandes d'augmentation ───────────────────────────────────────────────
        // Un conducteur mal payé (< 70 % de sa demande) ne part plus d'un coup : il DEMANDE une
        // augmentation. Le joueur a 24 h pour répondre ; 3 refus (ou pas de réponse) = départ.
        public const float SatisfiedRatio     = 0.7f;
        public const int   RaiseDeadlineHours = 24;
        public const int   MaxRaiseRefusals   = 3;
        public const int   RaiseCooldownHours = 12;   // délai avant une nouvelle demande après un refus

        public static float Satisfaction(DriverInstance d)
            => d.assignedWagePerContract / (float)Mathf.Max(1, d.desiredWagePerContract);
        public static bool HasRaiseRequest(DriverInstance d) => d != null && d.raiseRequestDeadlineTicks > 0;
        public static int  RefusalsLeft(DriverInstance d) => Mathf.Max(0, MaxRaiseRefusals - d.raiseRefusals);

        private static long Now
        {
            get
            {
                long ctx = Notifications.NotificationSystem.ContextTicks;
                return ctx > 0 ? ctx : DateTime.UtcNow.Ticks;
            }
        }

        private static string Pronoun(DriverInstance d) => d.gender == "f" ? "elle" : "il";

        private void OpenRaiseRequest(DriverInstance d)
        {
            long now = Now;
            d.raiseRequestedWage        = Mathf.Max(d.desiredWagePerContract, d.assignedWagePerContract + 1);
            d.raiseRequestDeadlineTicks = now + TimeSpan.FromHours(RaiseDeadlineHours).Ticks;
            var limit = new DateTime(d.raiseRequestDeadlineTicks, DateTimeKind.Utc).ToLocalTime();
            Notifications.NotificationSystem.Push(TransportManager.Save.NotificationKind.Raise,
                $"{d.FullName} demande une augmentation",
                $"{char.ToUpper(Pronoun(d)[0]) + Pronoun(d).Substring(1)} touche {d.assignedWagePerContract:N0} $ par contrat et en demande {d.raiseRequestedWage:N0} $. " +
                $"Réponds avant le {limit:dd/MM à HH:mm} dans l'onglet Pilote, sinon {Pronoun(d)} partira. " +
                $"Refus possibles : {RefusalsLeft(d) - 1} avant départ.", now);
            GameEvents.RaiseDriverWageChanged(d);
        }

        private static void ClearRaiseRequest(DriverInstance d, bool resetRefusals)
        {
            d.raiseRequestDeadlineTicks = 0;
            d.raiseRequestedWage = 0;
            if (resetRefusals) d.raiseRefusals = 0;
        }

        public bool AcceptRaise(string driverInstanceId)
        {
            var d = GetHired(driverInstanceId);
            if (!HasRaiseRequest(d)) return false;
            d.assignedWagePerContract = Mathf.Max(d.assignedWagePerContract, d.raiseRequestedWage);
            ClearRaiseRequest(d, resetRefusals: true);
            GameEvents.RaiseDriverWageChanged(d);
            return true;
        }

        public bool RefuseRaise(string driverInstanceId)
        {
            var d = GetHired(driverInstanceId);
            if (!HasRaiseRequest(d)) return false;
            d.raiseRefusals++;
            ClearRaiseRequest(d, resetRefusals: false);
            if (d.raiseRefusals >= MaxRaiseRefusals)
                LeaveOrSchedule(d, $"Après {MaxRaiseRefusals} refus d'augmentation, {Pronoun(d)} a quitté l'entreprise.");
            else
            {
                d.raiseNextAllowedTicks = Now + TimeSpan.FromHours(RaiseCooldownHours).Ticks;
                GameEvents.RaiseDriverWageChanged(d);
            }
            return true;
        }

        /// Délais de réponse expirés → départ (immédiat, ou à la fin de la livraison en cours).
        public void TickRaiseDeadlines()
        {
            long now = DateTime.UtcNow.Ticks;
            foreach (var d in _save.hiredDrivers.ToArray())
            {
                if (!HasRaiseRequest(d) || now < d.raiseRequestDeadlineTicks) continue;
                long deadline = d.raiseRequestDeadlineTicks;
                ClearRaiseRequest(d, resetRefusals: false);
                Notifications.NotificationSystem.ContextTicks = deadline;
                try { LeaveOrSchedule(d, $"Sans réponse à sa demande d'augmentation sous {RaiseDeadlineHours} h, {Pronoun(d)} a quitté l'entreprise."); }
                finally { Notifications.NotificationSystem.ContextTicks = 0; }
            }
        }

        private bool IsOnTrip(DriverInstance d)
        {
            var v = ServiceLocator.Get<FleetSystem>()?.GetById(d.assignedVehicleInstanceId);
            return v != null && !string.IsNullOrEmpty(v.activeContractInstanceId);
        }

        private readonly System.Collections.Generic.Dictionary<string, string> _pendingQuitReasons = new System.Collections.Generic.Dictionary<string, string>();

        private void LeaveOrSchedule(DriverInstance d, string reason)
        {
            if (IsOnTrip(d))
            {
                d.quitPending = true;   // termine sa livraison, puis part (cf. ProcessPostContract)
                _pendingQuitReasons[d.instanceId] = reason;
                Notifications.NotificationSystem.Push(TransportManager.Save.NotificationKind.Resigned,
                    $"{d.FullName} va démissionner",
                    $"{reason.Replace("a quitté l'entreprise", "partira")} à la fin de sa livraison en cours.");
                GameEvents.RaiseDriverWageChanged(d);
                return;
            }
            Resign(d, reason);
        }

        private void Resign(DriverInstance d, string reason)
        {
            Notifications.NotificationSystem.Push(TransportManager.Save.NotificationKind.Resigned, $"{d.FullName} a démissionné", reason);
            GameEvents.RaiseDriverResigned(d);
            RemoveFromPayroll(d);
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
            return AfterContractMood(driver);
        }

        // Après une livraison : départ programmé, ou demande d'augmentation si mal payé.
        private bool AfterContractMood(DriverInstance driver)
        {
            if (driver.quitPending)
            {
                driver.quitPending = false;
                Resign(driver, _pendingQuitReasons.TryGetValue(driver.instanceId, out var why) ? why : $"{char.ToUpper(Pronoun(driver)[0]) + Pronoun(driver).Substring(1)} a quitté l'entreprise après sa livraison.");
                return true;
            }
            if (Satisfaction(driver) >= SatisfiedRatio || HasRaiseRequest(driver)) return false;
            if (Now < driver.raiseNextAllowedTicks) return false;
            OpenRaiseRequest(driver);
            return false;
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
