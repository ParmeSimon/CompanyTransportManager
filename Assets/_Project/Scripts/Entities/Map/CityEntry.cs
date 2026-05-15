using System;
using System.Collections.Generic;

namespace TransportManager.Entities.Map
{
    [Serializable]
    public class CityEntry
    {
        public string id;
        public string displayName;
        public string country;
        public GeoPoint location;

        // Cosmetic delivery point labels used to flavor contracts.
        // (Not geocoded — they ride on top of the city-level route.)
        public List<string> deliveryPointLabels = new List<string>();
    }
}
