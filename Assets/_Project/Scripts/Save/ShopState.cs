using System;
using System.Collections.Generic;

namespace TransportManager.Save
{
    [Serializable]
    public class ShopState
    {
        public long lastAdWatchUtcTicks;
        public long adDayStartUtcTicks; // début de la journée UTC pour le compteur quotidien
        public int  adsWatchedToday;
        public List<string> claimedSpecialOfferIds = new List<string>();
    }
}
