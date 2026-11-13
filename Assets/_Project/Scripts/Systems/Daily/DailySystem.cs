using System;
using System.Collections.Generic;
using UnityEngine;
using TransportManager.Core;
using TransportManager.Entities.Contracts;
using TransportManager.Enums;
using TransportManager.Events;
using TransportManager.Save;
using TransportManager.Systems.Economy;
using TransportManager.Systems.Map;
using TransportManager.Systems.Progression;
using TransportManager.Systems.Social;

namespace TransportManager.Systems.Daily
{
    /// <summary>
    /// Défis quotidiens (missions) + récompense de connexion (streak).
    /// Suit la progression via les contrats livrés, réinitialise chaque jour UTC.
    /// </summary>
    public class DailySystem
    {
        private readonly GameSaveData _save;
        private DailyState S => _save.daily;

        public bool LoginPending { get; private set; }

        public DailySystem(GameSaveData save)
        {
            _save = save;
            if (_save.daily == null) _save.daily = new DailyState();
            EnsureToday();
            CheckLogin();
            CheckLeagueReward();
            GameEvents.OnContractDelivered += OnDelivered;
        }

        private static long TodayTicks()     => DateTime.UtcNow.Date.Ticks;
        private static long YesterdayTicks()  => DateTime.UtcNow.Date.AddDays(-1).Ticks;
        private void Save() { SaveSystem.Save(_save); }

        // ── Missions ────────────────────────────────────────────────────────────────
        public IReadOnlyList<MissionState> Missions => S.missions;

        private void EnsureToday()
        {
            if (S.missionsDayTicks == TodayTicks() && S.missions.Count > 0)
            {
                // Migration : garantir la quête de connexion même sur un set déjà généré aujourd'hui.
                if (!S.missions.Exists(m => m.type == "connect"))
                {
                    S.missions.Insert(0, MakeConnectMission());
                    Save();
                }
                ReplaceIneligibleMissions();
                return;
            }
            S.missionsDayTicks = TodayTicks();
            S.visitedCountries.Clear();
            S.missions = GenerateMissions();
            Save();
        }

        // ── Quêtes selon l'avancement ──────────────────────────────────────────────
        // Une quête n'est proposée que si le joueur PEUT la réussir aujourd'hui : tournées et pays
        // demandent les compétences correspondantes, les contrats difficiles un camion assez gros,
        // et les objectifs chiffrés suivent le niveau et la taille de la flotte.
        private struct Progress
        {
            public int  level;          // niveau entreprise
            public int  crews;          // camions avec conducteur
            public int  maxCap;         // plus gros camion (t)
            public bool tours;          // « Tournées multi-arrêts »
            public int  countryReach;   // 0 = pays d'attache, 1 = limitrophes, 2 = continent, 3 = monde
        }

        private Progress ReadProgress()
        {
            var p = new Progress { level = Entities.Progression.XpCurve.CompanyLevelFromXp(_save.companyXp) };
            var catalog = ServiceLocator.Get<Entities.Vehicles.VehicleCatalog>();
            foreach (var v in _save.vehicles)
            {
                if (string.IsNullOrEmpty(v.assignedDriverInstanceId)) continue;
                p.crews++;
                var data = catalog != null ? catalog.GetById(v.vehicleDataId) : null;
                if (data != null && data.capacity > p.maxCap) p.maxCap = data.capacity;
            }
            p.tours        = SkillTreeSystem.FlatOf(SkillEffectType.MultiStopContractsUnlocked) > 0;
            p.countryReach = SkillTreeSystem.FlatOf(SkillEffectType.ContractCountryReach);
            return p;
        }

        private static bool IsEligible(string type, Progress p)
        {
            switch (type)
            {
                case "tour":      return p.tours;
                case "countries": return p.countryReach >= 1;   // il faut pouvoir livrer hors de son pays
                case "premium":   return p.maxCap >= 15;        // contrats « difficiles » : camion ≥ 15 t
                default:          return true;                  // distance, contrats, gains : toujours
            }
        }

        private static readonly string[] AllTypes = { "distance", "contracts", "premium", "earn", "tour", "countries" };

        private List<MissionState> GenerateMissions()
        {
            var list = new List<MissionState>();

            // Quête fixe, toujours présente : se connecter aujourd'hui (1000 $), complétée d'office.
            list.Add(MakeConnectMission());

            // + 3 missions de types distincts, uniquement parmi celles réalisables à ce stade.
            var prog = ReadProgress();
            var pool = new List<string>();
            foreach (var t in AllTypes) if (IsEligible(t, prog)) pool.Add(t);
            for (int n = 0; n < 3 && pool.Count > 0; n++)
            {
                int i = UnityEngine.Random.Range(0, pool.Count);
                list.Add(MakeMission(pool[i], prog));
                pool.RemoveAt(i);
            }
            return list;
        }

        // Quêtes du jour générées avant cette règle : on remplace celles qui sont impossibles
        // (et pas encore entamées) par une quête réalisable.
        private void ReplaceIneligibleMissions()
        {
            var prog = ReadProgress();
            bool changed = false;
            for (int i = 0; i < S.missions.Count; i++)
            {
                var m = S.missions[i];
                if (m.type == "connect" || m.claimed || m.progress > 0 || IsEligible(m.type, prog)) continue;
                var pool = new List<string>();
                foreach (var t in AllTypes)
                    if (IsEligible(t, prog) && !S.missions.Exists(x => x.type == t)) pool.Add(t);
                if (pool.Count == 0) { S.missions.RemoveAt(i); i--; changed = true; continue; }
                S.missions[i] = MakeMission(pool[UnityEngine.Random.Range(0, pool.Count)], prog);
                changed = true;
            }
            if (changed) Save();
        }

        // La connexion du jour : complétée dès la génération (le joueur est connecté).
        private static MissionState MakeConnectMission()
        {
            var m = M("connect", 1, "dollars", 1000);
            m.progress = 1;
            return m;
        }

        // Durée moyenne d'un contrat selon le niveau (cf. ContractGenerator.TargetDurationHours).
        private static float AvgContractHours(int level) =>
            level <= 2 ? 0.5f : level <= 4 ? 1f : level <= 7 ? 2f : level <= 11 ? 3.5f : level <= 16 ? 5.5f : 8f;

        private static MissionState MakeMission(string type, Progress p)
        {
            int Round(float v, int step) => Mathf.Max(step, Mathf.RoundToInt(v / step) * step);
            int crews = Mathf.Max(1, p.crews);

            // Contrats faisables dans une journée « normale » (quelques sessions de jeu) :
            // ~3 par camion, plafonné ; un peu d'aléa pour varier.
            int contracts = Mathf.Clamp(Mathf.RoundToInt(crews * 2.5f) + UnityEngine.Random.Range(-1, 2), 2, 10);
            float kmPerContract = AvgContractHours(p.level) * 70f;      // ~70 km/h de moyenne
            float km = contracts * kmPerContract;

            switch (type)
            {
                case "distance":  { int t = Round(km, 50);       return M(type, t, "dollars", Round(t * 2f, 50)); }
                case "contracts": {                              return M(type, contracts, "ingots", contracts >= 6 ? 3 : 2); }
                case "premium":   { int t = crews >= 3 ? 2 : 1;  return M(type, t, "skill", 1); }
                case "earn":      { int t = Round(km * 3f, 500); return M(type, t, "dollars", Round(t * 0.25f, 50)); }
                case "tour":      { int t = crews >= 3 ? 2 : 1;  return M(type, t, "ingots", 2); }
                case "countries":
                {
                    // limitrophes → 2 pays ; continent → 3 ; monde → 3 à 4.
                    int t = p.countryReach >= 3 ? UnityEngine.Random.Range(3, 5) : p.countryReach == 2 ? 3 : 2;
                    return M(type, t, "skill", 1);
                }
                default:          return M(type, 1, "dollars", 1000);
            }
        }

        private static MissionState M(string type, int target, string kind, int amount) =>
            new MissionState { type = type, target = target, progress = 0, claimed = false, rewardKind = kind, rewardAmount = amount };

        public static bool IsComplete(MissionState m) => m != null && m.progress >= m.target;

        public bool ClaimMission(int index)
        {
            EnsureToday();
            if (index < 0 || index >= S.missions.Count) return false;
            var m = S.missions[index];
            if (m.claimed || !IsComplete(m)) return false;
            m.claimed = true;
            Grant(m.rewardKind, EffectiveMissionReward(m.rewardKind, m.rewardAmount));
            Save();
            return true;
        }

        private void OnDelivered(ContractInstance inst, int reward)
        {
            var def = inst?.definition;
            if (def == null) return;
            EnsureToday();

            string visited = CountryVisited(def);
            if (!string.IsNullOrEmpty(visited) && !S.visitedCountries.Contains(visited))
                S.visitedCountries.Add(visited);

            foreach (var m in S.missions)
            {
                if (m.claimed) continue;
                switch (m.type)
                {
                    case "distance":  m.progress += Mathf.RoundToInt(def.distanceKm); break;
                    case "contracts": m.progress += 1; break;
                    case "premium":   if (def.difficulty >= ContractDifficulty.Hard) m.progress += 1; break;
                    case "earn":      m.progress += reward; break;
                    case "tour":      if (def.isMultiStop) m.progress += 1; break;
                    case "countries": m.progress = S.visitedCountries.Count; break;
                }
            }
            Save();
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

        // ── Récompense de connexion ──────────────────────────────────────────────────
        public int LoginStreak => S.loginStreak;

        private void CheckLogin() => LoginPending = S.loginLastClaimDayTicks != TodayTicks();

        // Jour du cycle (1..7) qui sera réclamé maintenant.
        public int PendingCycleDay()
        {
            int streak = LoginPending
                ? (S.loginLastClaimDayTicks == YesterdayTicks() ? S.loginStreak + 1 : 1)
                : S.loginStreak;
            if (streak < 1) streak = 1;
            return ((streak - 1) % 7) + 1;
        }

        public bool ClaimLogin()
        {
            if (!LoginPending) return false;
            S.loginStreak = (S.loginLastClaimDayTicks == YesterdayTicks()) ? S.loginStreak + 1 : 1;
            S.loginLastClaimDayTicks = TodayTicks();
            LoginPending = false;
            var (kind, amount) = LoginRewardForDay(((S.loginStreak - 1) % 7) + 1);
            Grant(kind, amount);
            Save();
            return true;
        }

        /// Récompense d'un jour du cycle (1..7).
        public static (string kind, int amount) LoginRewardForDay(int day) => day switch
        {
            1 => ("dollars", 1000),
            2 => ("dollars", 1500),
            3 => ("ingots", 1),
            4 => ("dollars", 2500),
            5 => ("ingots", 2),
            6 => ("dollars", 4000),
            7 => ("skill", 1),
            _ => ("dollars", 1000),
        };

        // ── Récompense de ligue (fin de semaine) ─────────────────────────────────────
        public bool LeaguePending        => _save.daily.leaguePending;
        public int  LeaguePendingDollars => _save.daily.leaguePendingDollars;
        public int  LeaguePendingGold    => _save.daily.leaguePendingGold;
        public int  LeaguePendingLeague  => _save.daily.leaguePendingLeague;
        public int  LeaguePendingRank    => _save.daily.leaguePendingRank;

        private void CheckLeagueReward()
        {
            int cur = Leaderboard.WeekIndex;

            // Premier lancement : on s'aligne sur la semaine courante, pas de récompense rétroactive.
            if (_save.daily.leagueProcessedWeek == 0)
            {
                _save.daily.leagueProcessedWeek = cur;
                Save();
                return;
            }
            if (_save.daily.leaguePending) return;            // une récompense est déjà en attente
            if (_save.daily.leagueProcessedWeek >= cur) return; // pas de nouvelle semaine terminée

            // Récompense basée sur le classement de la semaine qui vient de se terminer.
            int prevWeek = cur - 1;
            var world  = Leaderboard.BuildWorld(prevWeek);
            var player = Leaderboard.PlayerEntry(world);
            int league = Leaderboard.LeagueIndex(player?.km ?? 0);
            int rank   = Leaderboard.LeagueRank(world, player);
            var (dollars, gold) = Leaderboard.LeagueReward(league, rank);

            _save.daily.leaguePending        = true;
            _save.daily.leaguePendingDollars = dollars;
            _save.daily.leaguePendingGold    = gold;
            _save.daily.leaguePendingLeague  = league;
            _save.daily.leaguePendingRank    = rank;
            _save.daily.leagueProcessedWeek  = cur;
            Save();
        }

        public bool ClaimLeague()
        {
            if (!_save.daily.leaguePending) return false;
            Grant("dollars", _save.daily.leaguePendingDollars);
            if (_save.daily.leaguePendingGold > 0) Grant("ingots", _save.daily.leaguePendingGold);
            _save.daily.leaguePending = false;
            Save();
            return true;
        }

        // ── Récompenses ──────────────────────────────────────────────────────────────
        /// Récompense réellement versée pour une mission : « Objectifs ambitieux » augmente
        /// les $ et les lingots (pas les points de compétence). Utilisé aussi par l'affichage.
        public static int EffectiveMissionReward(string kind, int amount)
        {
            if (kind != "dollars" && kind != "ingots") return amount;
            return Mathf.RoundToInt(amount * (1f + SkillTreeSystem.PctOf(SkillEffectType.DailyMissionRewardBonus)));
        }

        private void Grant(string kind, int amount)
        {
            switch (kind)
            {
                case "dollars": ServiceLocator.Get<WalletSystem>()?.Add(CurrencyType.Dollar, amount); break;
                case "ingots":  ServiceLocator.Get<WalletSystem>()?.Add(CurrencyType.GoldIngot, amount); break;
                case "skill":   ServiceLocator.Get<SkillTreeSystem>()?.GrantPoints(amount); break;
            }
        }

        // ── Libellés (partagés avec l'UI) ────────────────────────────────────────────
        public static string MissionLabel(MissionState m) => m.type switch
        {
            "connect"   => "Se connecter aujourd'hui",
            "distance"  => $"Parcourir {m.target:N0} km",
            "contracts" => $"Terminer {m.target} contrats",
            "premium"   => $"Réussir {m.target} contrat(s) difficile(s)",
            "earn"      => $"Gagner {m.target:N0} $",
            "tour"      => $"Réussir {m.target} tournée(s) à escales",
            "countries" => $"Livrer dans {m.target} pays différents",
            _           => "Objectif",
        };

        public static string RewardLabel(string kind, int amount) => kind switch
        {
            "dollars" => $"+{amount:N0} $",
            "ingots"  => $"+{amount} lingots",
            "skill"   => $"+{amount} pt",
            _         => $"+{amount}",
        };
    }
}
