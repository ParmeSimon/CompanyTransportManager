using System;
using UnityEngine;

namespace TransportManager.Systems.Economy
{
    /// <summary>
    /// Marché : prix du carburant **fluctuant** dans le temps (E3). Déterministe (basé sur
    /// l'horloge UTC) → tous les joueurs voient la même tendance ; on achète au bon moment.
    /// </summary>
    public static class Market
    {
        private static readonly DateTime Epoch = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private static double NowDays => (DateTime.UtcNow - Epoch).TotalDays;

        // Indice de prix du carburant à un instant donné (en jours depuis l'epoch).
        private static float MultiplierAt(double days)
        {
            float slow = Mathf.Sin((float)(days / 3.0) * 2f * Mathf.PI);     // cycle lent (~3 jours)
            float fast = Mathf.Sin((float)(days * 1.3) * 2f * Mathf.PI);     // variation intra-journalière
            float idx  = 1f + 0.18f * slow + 0.08f * fast;                   // ~0.74 .. 1.26
            return Mathf.Clamp(idx, 0.7f, 1.35f);
        }

        /// Multiplicateur courant du prix du carburant (1.0 = prix de base).
        public static float FuelPriceMultiplier => MultiplierAt(NowDays);

        /// Multiplicateur à un décalage donné (heures, négatif = passé, positif = futur).
        public static float MultiplierAtOffsetHours(float hoursOffset) => MultiplierAt(NowDays + hoursOffset / 24.0);

        /// Tendance : +1 = en hausse, -1 = en baisse, 0 = stable (comparé à ~1 h avant).
        public static int FuelTrend
        {
            get
            {
                float now  = MultiplierAt(NowDays);
                float prev = MultiplierAt(NowDays - 1.0 / 24.0);
                float d = now - prev;
                if (d > 0.002f) return 1;
                if (d < -0.002f) return -1;
                return 0;
            }
        }
    }
}
