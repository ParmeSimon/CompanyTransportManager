using System;
using UnityEngine;

namespace TransportManager.Systems.Events
{
    /// Un événement de jeu : un thème + un bonus de récompense sur les contrats.
    public class LiveEvent
    {
        public string  id;
        public string  title;
        public string  description;
        public float   rewardMultiplier;   // 1.0 = aucun bonus
        public Color32  color;

        public int BonusPercent => Mathf.RoundToInt((rewardMultiplier - 1f) * 100f);
    }

    /// <summary>
    /// Événements limités dans le temps. Pour l'instant : un événement **rotatif quotidien**
    /// (toujours un actif, change chaque jour UTC). Facile à étendre vers des plages de dates.
    /// </summary>
    public static class LiveEvents
    {
        private static readonly LiveEvent[] Pool =
        {
            new LiveEvent { id = "cold",    title = "Semaine du froid",     description = "Demande de transport frigorifique en hausse.", rewardMultiplier = 1.30f, color = new Color32(0x4D, 0xC4, 0xE6, 255) },
            new LiveEvent { id = "rush",    title = "Rush logistique",      description = "Les commandes explosent : primes augmentées.",  rewardMultiplier = 1.40f, color = new Color32(0xF2, 0x9B, 0x38, 255) },
            new LiveEvent { id = "harvest", title = "Saison des récoltes",  description = "L'agroalimentaire tourne à plein régime.",      rewardMultiplier = 1.25f, color = new Color32(0x6E, 0xC9, 0x4E, 255) },
            new LiveEvent { id = "festive", title = "Fièvre des fêtes",     description = "Pic de livraisons : tout le monde commande.",    rewardMultiplier = 1.50f, color = new Color32(0xE0, 0x55, 0x6B, 255) },
            new LiveEvent { id = "fuel",    title = "Carburant subventionné", description = "Trajets plus rentables grâce aux aides.",      rewardMultiplier = 1.20f, color = new Color32(0x9B, 0x7B, 0xFF, 255) },
        };

        /// L'événement actif aujourd'hui (jamais null).
        public static LiveEvent Current
        {
            get
            {
                int day = (int)Math.Floor((DateTime.UtcNow.Date - new DateTime(2024, 1, 1)).TotalDays);
                int idx = ((day % Pool.Length) + Pool.Length) % Pool.Length;
                return Pool[idx];
            }
        }

        /// Multiplicateur de récompense actif (1.0 si aucun événement).
        public static float RewardMultiplier => Current?.rewardMultiplier ?? 1f;
    }
}
