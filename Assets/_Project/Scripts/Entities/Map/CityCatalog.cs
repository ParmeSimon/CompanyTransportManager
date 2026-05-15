using System.Collections.Generic;
using UnityEngine;

namespace TransportManager.Entities.Map
{
    [CreateAssetMenu(fileName = "CityCatalog", menuName = "TransportManager/City Catalog")]
    public class CityCatalog : ScriptableObject
    {
        public List<CityEntry> cities = new List<CityEntry>();

        public CityEntry GetById(string id) => cities.Find(c => c.id == id);
    }
}
