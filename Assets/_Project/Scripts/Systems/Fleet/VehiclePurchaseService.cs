using System;
using TransportManager.Core;
using TransportManager.Entities.Vehicles;
using TransportManager.Enums;
using TransportManager.Systems.Depot;
using TransportManager.Systems.Economy;
using TransportManager.Systems.Progression;

namespace TransportManager.Systems.Fleet
{
    public class VehiclePurchaseService
    {
        public enum PurchaseError { None, InvalidData, MissingService, DepotFull, NotEnoughMoney, Locked }

        public bool TryPurchase(VehicleData data, out VehicleInstance instance, out PurchaseError error)
        {
            instance = null;
            error = PurchaseError.None;

            if (data == null) { error = PurchaseError.InvalidData; return false; }

            var wallet = ServiceLocator.Get<WalletSystem>();
            var fleet = ServiceLocator.Get<FleetSystem>();
            var depot = ServiceLocator.Get<DepotSystem>();
            if (wallet == null || fleet == null || depot == null) { error = PurchaseError.MissingService; return false; }

            var xp = ServiceLocator.Get<XpSystem>();
            if (xp != null && !xp.IsVehicleUnlocked(data.RequiredCompanyLevel))
            {
                error = PurchaseError.Locked;
                return false;
            }

            if (!depot.HasRoomForOneMore()) { error = PurchaseError.DepotFull; return false; }
            if (!wallet.CanAfford(CurrencyType.Dollar, data.purchasePrice)) { error = PurchaseError.NotEnoughMoney; return false; }
            if (!wallet.TrySpend(CurrencyType.Dollar, data.purchasePrice)) { error = PurchaseError.NotEnoughMoney; return false; }

            instance = new VehicleInstance
            {
                instanceId = Guid.NewGuid().ToString(),
                vehicleDataId = data.id,
                totalKilometers = 0,
                currentFuelLiters = data.fuelTankCapacityLiters,
                status = VehicleStatus.Idle
            };
            fleet.Add(instance);
            return true;
        }

        public bool TryPurchase(VehicleData data, out VehicleInstance instance) =>
            TryPurchase(data, out instance, out _);
    }
}
