namespace TransportManager.Enums
{
    /// <summary>
    /// Forme visuelle d'un nœud de l'arbre de compétences.
    /// Cercle : augment en pourcentage. Carré : amélioration structurelle « flat »
    /// (+1 emplacement, +1 niveau de recrutement…). Losange : capstone (fin de branche).
    /// </summary>
    public enum NodeShape
    {
        Circle,
        Square,
        Diamond
    }
}
