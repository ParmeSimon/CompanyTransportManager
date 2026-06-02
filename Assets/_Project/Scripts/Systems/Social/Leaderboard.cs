using System;
using System.Collections.Generic;
using UnityEngine;
using TransportManager.Core;
using TransportManager.Social;
using TransportManager.Systems.Fleet;
using TransportManager.Systems.Progression;

namespace TransportManager.Systems.Social
{
    public class LeaderboardEntry
    {
        public string name;
        public bool   isPlayer;
        public bool   isFriend;
        public int    level;
        public long   km;
        public int    worldRank;
    }

    /// <summary>
    /// Classement hebdomadaire **local**, par kilomètres parcourus :
    /// joueur + amis (FriendsData) + entreprises rivales générées (renouvelées chaque semaine).
    /// 5 ligues selon les km. À rebrancher sur un vrai backend plus tard.
    /// </summary>
    public static class Leaderboard
    {
        private static readonly DateTime Epoch = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // ── Ligues (5) ───────────────────────────────────────────────────────────────
        public const int LeagueCount = 5;
        private static readonly long[]    LeagueMin    = { 0, 5_000, 25_000, 100_000, 500_000 };
        private static readonly string[]  LeagueNames  = { "Bronze", "Argent", "Or", "Platine", "Diamant" };
        private static readonly Color32[] LeagueColors =
        {
            new Color32(0xCD, 0x7F, 0x4B, 255), // Bronze
            new Color32(0xA8, 0xB4, 0xC2, 255), // Argent
            new Color32(0xF2, 0xD9, 0x66, 255), // Or
            new Color32(0xC9, 0xD4, 0xE0, 255), // Platine
            new Color32(0x6F, 0xD0, 0xF5, 255), // Diamant
        };

        public static int LeagueIndex(long km)
        {
            int idx = 0;
            for (int i = 0; i < LeagueCount; i++) if (km >= LeagueMin[i]) idx = i;
            return idx;
        }
        public static string LeagueName(int idx)  => LeagueNames[Mathf.Clamp(idx, 0, LeagueCount - 1)];
        public static Color32 LeagueColor(int idx) => LeagueColors[Mathf.Clamp(idx, 0, LeagueCount - 1)];

        // ── Semaine ──────────────────────────────────────────────────────────────────
        public static int WeekIndex => (int)Math.Floor((DateTime.UtcNow.Date - Epoch).TotalDays / 7.0);
        public static int DaysUntilReset
        {
            get
            {
                var next = Epoch.AddDays((WeekIndex + 1) * 7);
                return Mathf.Max(0, (int)Math.Ceiling((next - DateTime.UtcNow).TotalDays));
            }
        }

        private static readonly string[] BotNames =
        {
            "TransAlpha", "RouteMaster", "CargoPlus", "ViaExpress", "FretLogic",
            "NordTrans", "SwiftCargo", "MegaHaul", "EuroLignes", "PrimeFret",
            "OmniRoute", "VelociTrans", "ApexLogistique", "TitanCargo", "RapidVoie",
            "BlueHaul", "GalaxieFret", "PoleTransport", "AxisCargo", "HeliosLog",
        };

        // ── Construction ─────────────────────────────────────────────────────────────
        public static LeaderboardEntry BuildPlayer()
        {
            var gm    = GameManager.Instance;
            var xp    = ServiceLocator.Get<XpSystem>();
            var fleet = ServiceLocator.Get<FleetSystem>();

            long km = 0;
            if (fleet != null) foreach (var v in fleet.Vehicles) km += Mathf.Max(0, v.totalKilometers);

            string name = gm?.Save?.company?.companyName;
            return new LeaderboardEntry
            {
                name     = string.IsNullOrWhiteSpace(name) ? "Mon entreprise" : name,
                isPlayer = true,
                level    = xp?.CompanyLevel ?? 1,
                km       = km,
            };
        }

        /// Le classement mondial complet (joueur + amis + rivales), trié par km décroissant.
        public static List<LeaderboardEntry> BuildWorld() => BuildWorld(WeekIndex);

        public static List<LeaderboardEntry> BuildWorld(int week)
        {
            var list = new List<LeaderboardEntry>();
            list.Add(BuildPlayer());

            foreach (var f in FriendsData.LoadAll())
            {
                int h = StableHash(f.uid);
                list.Add(new LeaderboardEntry
                {
                    name     = string.IsNullOrEmpty(f.companyName) ? "Ami" : f.companyName,
                    isFriend = true,
                    level    = Mathf.Max(1, f.level),
                    km       = (long)f.level * 1400 + h % 6000,
                });
            }

            // ~50 rivales réparties sur toutes les ligues (distribution log, seed = semaine).
            var rng = new System.Random(week * 7919 + 101);
            for (int i = 0; i < 50; i++)
            {
                double exp = rng.NextDouble() * 3.3;                  // 0 → 3.3
                long km = (long)Math.Round(1000.0 * Math.Pow(10.0, exp)); // ~1 000 → ~2 000 000
                int level = Mathf.Clamp((int)(km / 1500) + rng.Next(1, 6), 1, 80);
                list.Add(new LeaderboardEntry { name = BotName(i), level = level, km = km });
            }

            list.Sort((a, b) => b.km.CompareTo(a.km));
            for (int i = 0; i < list.Count; i++) list[i].worldRank = i + 1;
            return list;
        }

        public static List<LeaderboardEntry> WorldTop(List<LeaderboardEntry> world, int n)
        {
            var top = new List<LeaderboardEntry>();
            for (int i = 0; i < world.Count && i < n; i++) top.Add(world[i]);
            return top;
        }

        public static LeaderboardEntry PlayerEntry(List<LeaderboardEntry> world)
        {
            foreach (var e in world) if (e.isPlayer) return e;
            return null;
        }

        /// Le 1er d'une ligue donnée (plus haut km de la ligue).
        public static LeaderboardEntry LeagueLeader(List<LeaderboardEntry> world, int leagueIdx)
        {
            foreach (var e in world) if (LeagueIndex(e.km) == leagueIdx) return e; // world est trié décroissant
            return null;
        }

        /// Rang du joueur à l'intérieur de sa ligue (1 = 1er de la ligue).
        public static int LeagueRank(List<LeaderboardEntry> world, LeaderboardEntry entry)
        {
            int league = LeagueIndex(entry.km);
            int rank = 0;
            foreach (var e in world)
            {
                if (LeagueIndex(e.km) != league) continue;
                rank++;
                if (e == entry) return rank;
            }
            return rank;
        }

        /// Classement réduit au joueur + ses amis, trié par km.
        public static List<LeaderboardEntry> FriendsBoard()
        {
            var list = new List<LeaderboardEntry> { BuildPlayer() };
            foreach (var f in FriendsData.LoadAll())
            {
                int h = StableHash(f.uid);
                list.Add(new LeaderboardEntry
                {
                    name = string.IsNullOrEmpty(f.companyName) ? "Ami" : f.companyName,
                    isFriend = true, level = Mathf.Max(1, f.level),
                    km = (long)f.level * 1400 + h % 6000,
                });
            }
            list.Sort((a, b) => b.km.CompareTo(a.km));
            for (int i = 0; i < list.Count; i++) list[i].worldRank = i + 1;
            return list;
        }

        // ── Récompenses de fin de semaine (selon ligue × placement) ──────────────────
        private static readonly int[] LeagueBaseDollars = { 5_000, 12_000, 30_000, 75_000, 180_000 };
        private static readonly int[] LeagueBaseGold    = {     1,      2,      4,      8,      15 };

        public static (int dollars, int gold) LeagueReward(int leagueIdx, int rankInLeague)
        {
            int li = Mathf.Clamp(leagueIdx, 0, LeagueCount - 1);
            float mult = rankInLeague <= 1 ? 1.00f
                       : rankInLeague <= 3 ? 0.70f
                       : rankInLeague <= 10 ? 0.45f
                       : 0.25f;
            int dollars = Mathf.RoundToInt(LeagueBaseDollars[li] * mult);
            int gold    = Mathf.Max(0, Mathf.RoundToInt(LeagueBaseGold[li] * mult));
            return (dollars, gold);
        }

        private static string BotName(int i)
        {
            int n = i % BotNames.Length;
            int suffix = i / BotNames.Length;
            return suffix == 0 ? BotNames[n] : $"{BotNames[n]} {suffix + 1}";
        }

        private static int StableHash(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            int h = 17;
            foreach (char c in s) h = unchecked(h * 31 + c);
            return h & 0x7FFFFFFF;
        }
    }
}
