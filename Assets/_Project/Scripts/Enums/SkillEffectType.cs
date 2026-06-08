namespace TransportManager.Enums
{
    /// <summary>
    /// Type d'effet d'un nœud de compétence. Les effets en pourcentage (Pct) stockent
    /// une fraction (0.15 = 15 %) et se cumulent ; les effets « flat » stockent un entier.
    /// </summary>
    public enum SkillEffectType
    {
        // ── Dépôt ──
        ExtraVehicleSlots,          // flat : emplacements de véhicule supplémentaires
        DepotUpgradeCostReduction,  // pct  : réduction du coût d'agrandissement du dépôt
        RepairCostReduction,        // pct  : réduction du coût de réparation/maintenance
        ContractRewardBonus,        // pct  : bonus sur la récompense des contrats
        ContractCountryReach,       // flat : portée géographique des contrats (0 pays d'attache · 1 limitrophes · 2 continent · 3 monde)
        MultiStopContractsUnlocked, // flag : débloque les contrats à escales (tournées multi-arrêts), bien mieux payés
        AutoRepair,                 // flag : atelier robotisé — réparation auto gratuite au retour (capstone Dépôt)

        // ── RH ──
        DriverWageReduction,        // pct  : réduction des salaires versés aux conducteurs
        DriverXpGainBonus,          // pct  : bonus d'XP gagnée par les conducteurs
        RecruitmentLevelBonus,      // flat : niveau minimum des candidats du vivier
        FatigueReduction,           // pct  : réduction de la fatigue accumulée sur les trajets
        PremiumContractsUnlocked,   // flag : débloque l'apparition des contrats premium (capstone RH)
        RecruitmentPoolSizeBonus,   // flat : candidats supplémentaires dans le vivier de recrutement
        HrRefreshHoursReduction,    // flat : heures retranchées au cooldown de régénération du vivier (cumulatif)
        HrRefreshPayWithDollars,    // flag : débloque le refresh payé en Dollars (coût croissant, reset quotidien)
        HrRefreshInstant,           // flag : aucun temps d'attente entre deux refresh (capstone RH)
        HrRefreshFree,              // flag : le refresh manuel devient gratuit (capstone RH)

        // ── Essence ──
        FuelPriceReduction,         // pct  : réduction du prix d'achat du carburant
        FuelConsumptionReduction,   // pct  : réduction de la consommation sur les trajets
        StationCapacityBonus,       // pct  : bonus de capacité de la cuve de la station
        RefillSpeedBonus,           // pct  : accélération du remplissage de la station
        TripSpeedBonus,             // pct  : bonus de vitesse sur les trajets (durée réduite)
        FuelMarketHistory,          // flag : débloque l'historique du prix du carburant
        FuelMarketForecast,         // flat : (obsolète — plus de prévision possible avec les cours réels)
        AutoStationRefill,          // flag : remplissage automatique et gratuit de la station (capstone Essence)
        FuelMarketHistoryDays       // flat : nb de jours d'historique réel visibles dans le graphe (cumulatif)
    }
}
