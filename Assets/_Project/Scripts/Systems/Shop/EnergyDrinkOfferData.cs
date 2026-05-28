using UnityEngine;

namespace TransportManager.Systems.Shop
{
    [System.Serializable]
    public class EnergyDrinkOfferData
    {
        [Tooltip("Titre affiché sur la carte (ex: SOLO, DUO, PACK X3)")]
        public string title = "PACK";

        [Tooltip("Nombre de boissons incluses")]
        public int quantity = 1;

        [Tooltip("Prix affiché (ex: 0,99 €)")]
        public string priceText = "0,99 €";

        [Tooltip("Texte bonus en vert — laisser vide si aucun (ex: +1 offert)")]
        public string bonus = "";

        [Tooltip("Chemin Resources/ vers le sprite (ex: UI/Shop/drink_solo) — laisser vide pour l'icône par défaut")]
        public string resourceImage = "";
    }
}
