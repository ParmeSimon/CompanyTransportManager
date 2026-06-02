namespace TransportManager.Enums
{
    public enum VehicleCategory
    {
        // ⚠️ Les valeurs sont sérialisées par INDEX dans VehicleCatalog.asset :
        // ne JAMAIS réordonner/insérer — uniquement ajouter à la fin.
        Fourgonnette        = 0,
        Camion              = 1,
        PoidsLourd          = 2,
        SemiRemorque        = 3,
        ConvoiExceptionnel  = 4,
        Frigorifique        = 5,  // réfrigéré
        Benne               = 6,  // BTP / chantier
        Citerne             = 7,  // liquides / carburant
        PorteConteneur      = 8,  // conteneurs maritimes
        TrainRoutier        = 9,  // road train (late game)
        MegaConvoi          = 10  // transport hors-norme géant (end game)
    }
}
