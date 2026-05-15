using System.Collections.Generic;
using UnityEngine;

namespace TransportManager.Entities.Vehicles
{
    [CreateAssetMenu(fileName = "VehicleCatalog", menuName = "TransportManager/Vehicle Catalog")]
    public class VehicleCatalog : ScriptableObject
    {
        public List<VehicleData> vehicles = new List<VehicleData>();

        public VehicleData GetById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < vehicles.Count; i++)
            {
                if (vehicles[i].id == id) return vehicles[i];
            }
            return null;
        }

        public int GetMaxKilometers(string id)
        {
            var v = GetById(id);
            return v != null ? v.maxKilometers : 0;
        }
    }
}
