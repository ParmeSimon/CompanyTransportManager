using TransportManager.Enums;

namespace TransportManager.Entities.Vehicles
{
    /// <summary>
    /// Niveau d'entreprise minimum requis pour débloquer chaque <see cref="VehicleCategory"/>.
    /// Le plancher de classe s'applique en plus du <see cref="VehicleData.minCompanyLevelRequired"/>
    /// défini sur chaque véhicule : on retient toujours le plus exigeant des deux.
    /// </summary>
    public static class VehicleClassUnlock
    {
        public static int ForCategory(VehicleCategory category)
        {
            switch (category)
            {
                case VehicleCategory.Fourgonnette:       return 1;
                case VehicleCategory.Camion:             return 3;
                case VehicleCategory.PoidsLourd:         return 6;
                case VehicleCategory.SemiRemorque:       return 10;
                case VehicleCategory.ConvoiExceptionnel: return 15;
                default:                                 return 1;
            }
        }
    }
}
