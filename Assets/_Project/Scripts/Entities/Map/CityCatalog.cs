using System.Collections.Generic;
using UnityEngine;

namespace TransportManager.Entities.Map
{
    [CreateAssetMenu(fileName = "CityCatalog", menuName = "TransportManager/City Catalog")]
    public class CityCatalog : ScriptableObject
    {
        public List<CityEntry> cities = new List<CityEntry>();

        // Dépôt du joueur : entrée synthétique bâtie au runtime depuis les coordonnées
        // de l'entreprise. NON sérialisée → jamais écrite dans l'asset.
        [System.NonSerialized] private CityEntry _home;
        public CityEntry Home => _home;
        public void SetHome(CityEntry home) => _home = home;

        public CityEntry GetById(string id)
        {
            if (_home != null && _home.id == id) return _home;
            return cities.Find(c => c.id == id);
        }
    }
}
