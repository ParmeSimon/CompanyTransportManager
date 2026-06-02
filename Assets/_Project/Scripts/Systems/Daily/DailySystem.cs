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
                return;
            }
            S.missionsDayTicks = TodayTicks();
            S.visitedCountries.Clear();
            S.missions = GenerateMissions();
            Save();
        }

        private static readonly string[] AllTypes = { "distance", "contracts", "premium", "earn", "tour", "countries" };

        private List<MissionState> GenerateMissions()
        {
            var list = new List<MissionState>();

            // Quête fixe, toujours présente : se connecter aujourd'hui (1000 $), complétée d'office.
            list.Add(MakeConnectMission());

            // + 3 missions aléatoires de types distincts (changent chaque jour).
            var pool = new List<string>(AllTypes);
            for (int n = 0; n < 3 && pool.Count > 0; n++)
            {
                int p = UnityEngine.Random.Range(0, pool.Count);
                list.Add(MakeMission(pool[p]));
                pool.RemoveAt(p);
            }
            return list;
        }

        // La connexion du jour : complétée dès la génération (le joueur est connecté).
        private static MissionState MakeConnectMission()
        {
            var m = M("connect", 1, "dollars", 1000);
            m.progress = 1;
            return m;
        }

        private static MissionState MakeMission(string type)
        {
            int R(int a, int b, int step) => UnityEngine.Random.Range(a, b + 1) / step * step;
            switch (type)
            {
                case "distance":  { int t = R(1500, 3000, 100); return M(type, t, "dollars", Mathf.RoundToInt(t * 2f)); }
                case "contracts": { int t = UnityEngine.Random.Range(3, 7);  return M(type, t, "ingots", 2); }
                case "premium":   { int t = UnityEngine.Random.Range(1, 3);  return M(type, t, "skill", 1); }
                case "earn":      { int t = R(12000, 30000, 1000); return M(type, t, "dollars", Mathf.RoundToInt(t * 0.25f)); }
                case "tour":      { int t = UnityEngine.Random.Range(1, 3);  return M(type, t, "ingots", 2); }
                case "countries": { int t = UnityEngine.Random.Range(3, 6);  return M(type, t, "skill", 1); }
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
            Grant(m.rewardKind, m.rewardAmount);
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
            "ingots"  => $"+{amount} ◆",
            "skill"   => $"+{amount} pt",
            _         => $"+{amount}",
        };
    }
}
