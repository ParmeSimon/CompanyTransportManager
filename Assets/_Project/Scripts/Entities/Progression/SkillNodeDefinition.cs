using TransportManager.Enums;

namespace TransportManager.Entities.Progression
{
    /// <summary>
    /// Définition immuable d'un nœud (augment) de l'arbre de compétences.
    /// Voir <see cref="SkillTreeCatalog"/> pour la liste complète et les réglages.
    /// </summary>
    public class SkillNodeDefinition
    {
        public readonly string id;
        public readonly SkillBranch branch;
        public readonly string title;
        public readonly string description;
        public readonly int cost;             // points de compétence requis
        public readonly string prerequisiteId; // null/"" = rattaché directement au tronc
        public readonly SkillEffectType effect;
        public readonly float magnitude;      // fraction (pct) ou nombre (flat)
        public readonly int tier;             // profondeur dans la branche (1 = proche du tronc)

        public SkillNodeDefinition(string id, SkillBranch branch, string title, string description,
            int cost, string prerequisiteId, SkillEffectType effect, float magnitude, int tier)
        {
            this.id = id;
            this.branch = branch;
            this.title = title;
            this.description = description;
            this.cost = cost;
            this.prerequisiteId = prerequisiteId;
            this.effect = effect;
            this.magnitude = magnitude;
            this.tier = tier;
        }

        public bool IsBranchRoot => string.IsNullOrEmpty(prerequisiteId);
    }
}
