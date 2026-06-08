using System;
using System.Collections.Generic;

namespace TransportManager.Save
{
    /// <summary>
    /// État persistant des succès (G1). Regroupe les compteurs cumulés « à vie »
    /// (qui ne se réinitialisent jamais, contrairement aux missions journalières) et
    /// la liste des succès débloqués / réclamés.
    /// </summary>
    [Serializable]
    public class AchievementsState
    {
        // ── Compteurs cumulés à vie ──────────────────────────────────────────────
        public int  totalContractsDelivered;   // contrats livrés (succès)
        public long totalKm;                    // distance cumulée (km, arrondie)
        public long totalEarnings;              // $ gagnés via contrats
        public int  totalTours;                 // tournées multi-arrêts livrées
        public int  totalPremiumContracts;      // contrats difficiles+ livrés
        public List<string> visitedCountries = new List<string>(); // pays distincts visités (à vie)

        // ── Meilleurs records (mis à jour, jamais redescendus) ───────────────────
        public int bestCompanyLevel;
        public int bestReputationTier;
        public int maxVehiclesOwned;
        public int bestVehicleCategory = -1;    // plus haute VehicleCategory possédée (index)

        // ── Succès ───────────────────────────────────────────────────────────────
        public List<string> unlocked = new List<string>(); // ids débloqués
        public List<string> claimed  = new List<string>(); // ids dont la récompense est réclamée

        // Vrai une fois que les compteurs ont été amorcés depuis une save existante
        // (pour ne pas repartir de zéro chez un joueur qui a déjà progressé).
        public bool seeded;
    }
}
