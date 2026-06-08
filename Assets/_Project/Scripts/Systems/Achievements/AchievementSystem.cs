using System.Collections.Generic;
using UnityEngine;
using TransportManager.Core;
using TransportManager.Entities.Contracts;
using TransportManager.Entities.Vehicles;
using TransportManager.Enums;
using TransportManager.Events;
using TransportManager.Save;
using TransportManager.Systems.Economy;
using TransportManager.Systems.Map;
using TransportManager.Systems.Progression;

namespace TransportManager.Systems.Achievements
{
    /// <summary>
    /// Succès / achievements globaux (G1). Met à jour des compteurs cumulés « à vie »
    /// au fil du jeu (via les events), débloque les succès atteints (toast + son par la
    /// couche game feel) et expose la progression + la réclamation des récompenses à l'UI.
    /// </summary>
    public class AchievementSystem
    {
        private readonly GameSaveData _save;
        private AchievementsState S => _save.achievements;

        public AchievementSystem(GameSaveData save)
        {
            _save = save;
            if (_save.achievements == null) _save.achievements = new AchievementsState();

            SeedFromExistingSaveIfNeeded();
            RecomputeRecords();          // aligne les records (niveau, flotte, réputation…) sur l'état courant

            GameEvents.OnContractDelivered += OnContractDelivered;
            GameEvents.OnVehicleAdded      += _ => { RecomputeRecords(); Evaluate(); };
            GameEvents.OnCompanyLevelUp    += (_, lvl) => { Bump(ref S.bestCompanyLevel, lvl); Evaluate(); };
            GameEvents.OnReputationTierUp  += _ => { RecomputeRecords(); Evaluate(); };

            Evaluate();                  // débloque silencieusement ce qui est déjà atteint
        }

        // ── API UI ───────────────────────────────────────────────────────────────
        public IReadOnlyList<AchievementDef> All => AchievementCatalog.All;

        public bool IsUnlocked(string id) => S.unlocked.Contains(id);
        public bool IsClaimed(string id)  => S.claimed.Contains(id);

        /// Nombre de succès dont la récompense est prête à être réclamée (pour le badge header).
        public int ClaimableCount()
        {
            int n = 0;
            foreach (var id in S.unlocked) if (!S.claimed.Contains(id)) n++;
            return n;
        }

        public int UnlockedCount => S.unlocked.Count;
        public int TotalCount    => AchievementCatalog.All.Count;

        /// Valeur courante de la grandeur suivie par ce succès.
        public long CurrentValue(AchievementDef def)
        {
            switch (def.metric)
            {
                case AchievementMetric.ContractsDelivered: return S.totalContractsDelivered;
                case AchievementMetric.KmDriven:           return S.totalKm;
                case AchievementMetric.CountriesVisited:   return S.visitedCountries.Count;
                case AchievementMetric.EarningsTotal:      return S.totalEarnings;
                case AchievementMetric.ToursCompleted:     return S.totalTours;
                case AchievementMetric.PremiumContracts:   return S.totalPremiumContracts;
                case AchievementMetric.VehiclesOwned:      return S.maxVehiclesOwned;
                case AchievementMetric.CompanyLevel:       return S.bestCompanyLevel;
                case AchievementMetric.ReputationTier:     return S.bestReputationTier;
                case AchievementMetric.VehicleCategory:    return S.bestVehicleCategory; // index de catégorie
                default: return 0;
            }
        }

        public float Progress01(AchievementDef def) =>
            def.target <= 0 ? 1f : Mathf.Clamp01((float)CurrentValue(def) / def.target);

        /// Réclame la récompense d'un succès débloqué (une seule fois).
        public bool Claim(string id)
        {
            if (!S.unlocked.Contains(id) || S.claimed.Contains(id)) return false;
            var def = AchievementCatalog.GetById(id);
            if (def == null) return false;
            Grant(def.rewardKind, def.rewardAmount);
            S.claimed.Add(id);
            Save();
            return true;
        }

        // ── Tracking ───────────────────────────────────────────────────────────────
        private void OnContractDelivered(ContractInstance inst, int reward)
        {
            var def = inst?.definition;
            if (def == null) return;

            S.totalContractsDelivered += 1;
            S.totalKm                 += Mathf.RoundToInt(def.distanceKm);
            S.totalEarnings           += Mathf.Max(0, reward);
            if (def.isMultiStop) S.totalTours += 1;
            if (def.difficulty >= ContractDifficulty.Hard) S.totalPremiumContracts += 1;

            string country = CountryVisited(def);
            if (!string.IsNullOrEmpty(country) && !S.visitedCountries.Contains(country))
                S.visitedCountries.Add(country);

            Evaluate();
        }

        private static string CountryVisited(ContractData def)
        {
            var cat = ServiceLocator.Get<MapSystem>()?.Catalog;
            if (cat == null) return null;
            var dest = cat.GetById(def.destinationCityId);
            var orig = cat.GetById(def.originCityId);
            if (dest != null && dest.id != "home_depot") return dest.country;
            if (orig != null && orig.id != "home_depot") return orig.country;
            return dest?.country;
        }

        // Met à jour les records dérivés de l'état courant (flotte, niveau, réputation, catégories).
        private void RecomputeRecords()
        {
            int level = ServiceLocator.Get<XpSystem>()?.CompanyLevel ?? 0;
            Bump(ref S.bestCompanyLevel, level);

            int repTier = ServiceLocator.Get<Progression.ReputationSystem>()?.TierIndex ?? 0;
            Bump(ref S.bestReputationTier, repTier);

            int count = _save.vehicles?.Count ?? 0;
            Bump(ref S.maxVehiclesOwned, count);

            var catalog = ServiceLocator.Get<VehicleCatalog>();
            if (catalog != null && _save.vehicles != null)
                foreach (var v in _save.vehicles)
                {
                    var data = catalog.GetById(v.vehicleDataId);
                    if (data != null) Bump(ref S.bestVehicleCategory, (int)data.category);
                }
        }

        private static void Bump(ref int field, int value) { if (value > field) field = value; }

        // ── Évaluation / déblocage ───────────────────────────────────────────────
        private void Evaluate()
        {
            bool changed = false;
            foreach (var def in AchievementCatalog.All)
            {
                if (S.unlocked.Contains(def.id)) continue;
                if (CurrentValue(def) >= def.target)
                {
                    S.unlocked.Add(def.id);
                    changed = true;
                    GameEvents.RaiseAchievementUnlocked(def);
                }
            }
            if (changed) Save();
        }

        // ── Amorçage depuis une save existante ───────────────────────────────────
        private void SeedFromExistingSaveIfNeeded()
        {
            if (S.seeded) return;
            S.seeded = true;

            // Contrats déjà livrés : reconstitués depuis le total porté par les conducteurs.
            if (_save.hiredDrivers != null)
            {
                int total = 0;
                foreach (var d in _save.hiredDrivers) total += d.contractsCompleted;
                if (total > S.totalContractsDelivered) S.totalContractsDelivered = total;
            }

            // Pays déjà visités aujourd'hui (la seule trace disponible) → fond de départ.
            if (_save.daily?.visitedCountries != null)
                foreach (var c in _save.daily.visitedCountries)
                    if (!string.IsNullOrEmpty(c) && !S.visitedCountries.Contains(c))
                        S.visitedCountries.Add(c);

            // km / gains cumulés ne sont pas reconstituables → repartent de 0.
            Save();
        }

        // ── Récompenses (mêmes types que DailySystem) ────────────────────────────
        private void Grant(string kind, int amount)
        {
            switch (kind)
            {
                case "dollars": ServiceLocator.Get<WalletSystem>()?.Add(CurrencyType.Dollar, amount); break;
                case "ingots":  ServiceLocator.Get<WalletSystem>()?.Add(CurrencyType.GoldIngot, amount); break;
                case "skill":   ServiceLocator.Get<SkillTreeSystem>()?.GrantPoints(amount); break;
            }
        }

        public static string RewardLabel(string kind, int amount) => kind switch
        {
            "dollars" => $"+{amount:N0} $",
            "ingots"  => $"+{amount} ◆",
            "skill"   => $"+{amount} pt",
            _         => $"+{amount}",
        };

        private void Save() => SaveSystem.Save(_save);
    }
}
