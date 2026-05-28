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

        // ── RH ──
        DriverWageReduction,        // pct  : réduction des salaires versés aux conducteurs
        DriverXpGainBonus,          // pct  : bonus d'XP gagnée par les conducteurs
        RecruitmentLevelBonus,      // flat : niveau minimum des candidats du vivier
        FatigueReduction,           // pct  : réduction de la fatigue accumulée sur les trajets

        // ── Essence ──
        FuelPriceReduction,         // pct  : réduction du prix d'achat du carburant
        FuelConsumptionReduction,   // pct  : réduction de la consommation sur les trajets
        StationCapacityBonus,       // pct  : bonus de capacité de la cuve de la station
        RefillSpeedBonus,           // pct  : accélération du remplissage de la station
        TripSpeedBonus              // pct  : bonus de vitesse sur les trajets (durée réduite)
    }
}
