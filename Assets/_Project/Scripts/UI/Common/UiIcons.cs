using UnityEngine;
using UnityEngine.UI;

namespace TransportManager.UI.Common
{
    /// <summary>
    /// Chargement d'icônes sûr : si la ressource est absente, l'Image est masquée
    /// (au lieu d'afficher le carré blanc par défaut d'un Image sans sprite).
    /// </summary>
    public static class UiIcons
    {
        public static Sprite Load(string path) =>
            string.IsNullOrEmpty(path) ? null : Resources.Load<Sprite>(path);

        /// Affecte l'icône à l'Image ; masque l'Image si l'icône est introuvable.
        public static void Apply(Image img, string path)
        {
            if (img == null) return;
            var sprite = Load(path);
            img.sprite  = sprite;
            img.enabled = sprite != null;
        }
    }
}
