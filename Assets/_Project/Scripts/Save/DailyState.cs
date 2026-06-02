using System;
using System.Collections.Generic;

namespace TransportManager.Save
{
    /// État persistant des défis quotidiens et de la récompense de connexion.
    [Serializable]
    public class DailyState
    {
        // ── Missions journalières ──
        public long missionsDayTicks;                 // jour (minuit UTC) de génération du set courant
        public List<MissionState> missions = new List<MissionState>();
        public List<string> visitedCountries = new List<string>(); // pour la mission « visiter N pays »

        // ── Récompense de connexion ──
        public long loginLastClaimDayTicks;           // dernier jour réclamé (minuit UTC)
        public int  loginStreak;                      // jours consécutifs réclamés

        // ── Récompense de ligue (fin de semaine) ──
        public int  leagueProcessedWeek;              // dernière semaine évaluée (0 = pas encore initialisé)
        public bool leaguePending;                    // une récompense de ligue est à réclamer
        public int  leaguePendingDollars;
        public int  leaguePendingGold;
        public int  leaguePendingLeague;              // index de ligue (0..4)
        public int  leaguePendingRank;                // place dans la ligue
    }

    [Serializable]
    public class MissionState
    {
        public string type;          // distance | contracts | premium | earn | tour | countries
        public int    target;
        public int    progress;
        public bool   claimed;
        public string rewardKind;    // dollars | ingots | skill
        public int    rewardAmount;
    }
}
