using System.Collections.Generic;
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
        public readonly NodeShape shape;      // forme visuelle (cercle / carré / losange)

        // Prérequis ADDITIONNELS (au-delà de prerequisiteId). Utilisé par les nœuds de
        // « convergence » : pour les débloquer, TOUS les prérequis doivent l'être.
        public readonly string[] extraPrerequisiteIds;

        public SkillNodeDefinition(string id, SkillBranch branch, string title, string description,
            int cost, string prerequisiteId, SkillEffectType effect, float magnitude, int tier,
            NodeShape shape = NodeShape.Circle, string[] extraPrerequisiteIds = null)
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
            this.shape = shape;
            this.extraPrerequisiteIds = extraPrerequisiteIds;
        }

        public bool IsBranchRoot => string.IsNullOrEmpty(prerequisiteId);

        /// Nœud de convergence : possède plusieurs prérequis (tous requis pour le débloquer).
        public bool IsConvergence => extraPrerequisiteIds != null && extraPrerequisiteIds.Length > 0;

        /// Tous les prérequis du nœud (principal + additionnels), sans doublon ni vide.
        public IEnumerable<string> AllPrerequisiteIds
        {
            get
            {
                if (!string.IsNullOrEmpty(prerequisiteId)) yield return prerequisiteId;
                if (extraPrerequisiteIds != null)
                    foreach (var p in extraPrerequisiteIds)
                        if (!string.IsNullOrEmpty(p) && p != prerequisiteId) yield return p;
            }
        }
    }
}
