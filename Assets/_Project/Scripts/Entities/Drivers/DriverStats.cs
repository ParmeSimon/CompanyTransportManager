using System;
using UnityEngine;

namespace TransportManager.Entities.Drivers
{
    [Serializable]
    public class DriverStats
    {
        // Toutes les stats de performance sont sur une échelle 0-100.
        // Plus la valeur est haute, plus l'effet associé est fort (bénéfique au joueur).

        [Tooltip("Vitesse 0-100. Plus haut = trajets plus rapides.")]
        public float speed;

        [Tooltip("Carburant 0-100. Plus haut = consommation réduite.")]
        public float fuelEfficiency;

        [Tooltip("Sécurité 0-100. Plus haut = réparations moins coûteuses après accident.")]
        public float safety;

        [Tooltip("Concentration 0-100. Plus haut = récupération de fatigue plus rapide et moins d'accidents mortels.")]
        public float concentration;

        [Tooltip("Esquive 0-100. Plus haut = réduit directement la probabilité d'accident.")]
        public float dodge;

        [Tooltip("Endurance 0-100. Plus haut = la fatigue monte très peu pendant les contrats.")]
        public float endurance;

        /// <summary>Note générale 0-100 : moyenne des 6 stats de performance.</summary>
        public float General =>
            (speed + fuelEfficiency + safety + concentration + dodge + endurance) / 6f;

        /// <summary>Multiplicateur de vitesse : -10% (stat 0) → +25% (stat 100).</summary>
        public float SpeedMultiplier => 1f + Mathf.Lerp(-0.10f, 0.25f, speed / 100f);

        /// <summary>Multiplicateur de consommation : +5% (stat 0) → -20% (stat 100). Plus bas = mieux.</summary>
        public float FuelConsumptionMultiplier => 1f - Mathf.Lerp(-0.05f, 0.20f, fuelEfficiency / 100f);

        /// <summary>Facteur de coût de réparation : 1.0 (stat 0) → 0.5 (stat 100). Plus bas = mieux.</summary>
        public float RepairCostFactor => Mathf.Lerp(1f, 0.5f, safety / 100f);
    }
}
