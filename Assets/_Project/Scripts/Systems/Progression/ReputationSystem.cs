using UnityEngine;
using TransportManager.Enums;
using TransportManager.Events;
using TransportManager.Save;

namespace TransportManager.Systems.Progression
{
    /// <summary>
    /// Réputation de l'entreprise (E4). Monte avec les livraisons réussies (surtout à l'heure),
    /// baisse avec les accidents et les retards. Donne un bonus de récompense par palier.
    /// </summary>
    public class ReputationSystem
    {
        private readonly GameSaveData _save;
        public ReputationSystem(GameSaveData save) { _save = save; }

        // Paliers : (nom, points minimum, bonus de récompense)
        private static readonly (string name, int min, float bonus)[] Tiers =
        {
            ("Inconnu",     0,    0.00f),
            ("Réputé",      100,  0.03f),
            ("Renommé",     400,  0.06f),
            ("Établi",      1000, 0.10f),
            ("Légendaire",  2500, 0.15f),
        };

        public int Reputation => _save.reputation;

        public int TierIndex
        {
            get
            {
                int idx = 0;
                for (int i = 0; i < Tiers.Length; i++) if (_save.reputation >= Tiers[i].min) idx = i;
                return idx;
            }
        }

        public string TierName  => Tiers[TierIndex].name;
        public float  RewardBonus => Tiers[TierIndex].bonus;        // ex. 0.06 = +6 %
        public int    TierMin    => Tiers[TierIndex].min;
        public int    NextTierMin => TierIndex + 1 < Tiers.Length ? Tiers[TierIndex + 1].min : Tiers[TierIndex].min;
        public bool   IsMaxTier  => TierIndex == Tiers.Length - 1;

        // ── Étoiles (5 max) ──
        // Progression (0..1) vers le palier suivant.
        public float TierProgress => IsMaxTier ? 1f
            : (NextTierMin > TierMin ? Mathf.Clamp01((float)(Reputation - TierMin) / (NextTierMin - TierMin)) : 1f);

        // Valeur d'étoiles continue 0..5 : palier (étoiles pleines) + progression (étoile partielle).
        // Ex. 2.3 → 2 étoiles pleines + 1 étoile remplie à 30 %.
        public float Stars => Mathf.Clamp(TierIndex + TierProgress, 0f, 5f);

        // Repli texte (★/☆) tant qu'aucun PNG d'étoile n'est fourni.
        public int StarCount => Mathf.Clamp(Mathf.RoundToInt(Stars), 0, 5);
        public string StarString() => new string('★', StarCount) + new string('☆', 5 - StarCount);

        /// Gain de réputation pour une livraison (plus si à l'heure, peu en cas de retard).
        public void AddForDelivery(ContractDifficulty difficulty, bool onTime)
        {
            int basePts = difficulty switch
            {
                ContractDifficulty.Easy    => 6,
                ContractDifficulty.Medium  => 10,
                ContractDifficulty.Hard    => 16,
                ContractDifficulty.Premium => 26,
                _                          => 6,
            };
            int pts = onTime ? Mathf.RoundToInt(basePts * 1.5f) : Mathf.RoundToInt(basePts * 0.4f);
            Add(pts);
        }

        /// Pénalité de réputation (accident, contrat raté).
        public void Penalize(int amount) => Add(-Mathf.Abs(amount));

        private void Add(int delta)
        {
            if (delta == 0) return;
            int oldTier = TierIndex;
            _save.reputation = Mathf.Max(0, _save.reputation + delta);
            int newTier = TierIndex;

            GameEvents.RaiseReputationChanged(_save.reputation, newTier);
            if (newTier > oldTier) GameEvents.RaiseReputationTierUp(Tiers[newTier].name);

            SaveSystem.Save(_save);
        }
    }
}
