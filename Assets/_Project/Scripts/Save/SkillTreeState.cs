using System;
using System.Collections.Generic;

namespace TransportManager.Save
{
    /// <summary>État persistant de l'arbre de compétences.</summary>
    [Serializable]
    public class SkillTreeState
    {
        // Points disponibles à dépenser dans l'arbre.
        public int availablePoints;

        // Total cumulé de points jamais accordés (sert à créditer rétroactivement
        // les niveaux gagnés avant l'ajout de la fonctionnalité, sans double-compter).
        public int totalEarnedPoints;

        // Identifiants des nœuds débloqués.
        public List<string> unlockedNodeIds = new List<string>();
    }
}
