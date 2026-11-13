namespace TransportManager.Systems.Buildings
{
    /// <summary>
    /// Identifiants des 3 bâtiments du dépôt. Leur look n'est plus une image : il est
    /// modélisé en 3D dans TransportManager.UI.Depot.DepotScene3D (un design par bâtiment,
    /// qui grandit avec le niveau).
    /// </summary>
    public static class BuildingVisuals
    {
        public const string Hangar = "Hangar";
        public const string Office = "Office";
        public const string FuelTank = "FuelTank";
    }
}
