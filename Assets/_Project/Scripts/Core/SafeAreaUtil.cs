using UnityEngine;

namespace TransportManager.Core
{
    /// <summary>
    /// Calcule les marges de la « safe area » (encoche / Dynamic Island / home indicator /
    /// coins arrondis) en **unités canvas** (et non en pixels), pour décaler l'UID HUD afin
    /// qu'elle ne soit ni rognée ni masquée. Recalculé dynamiquement → gère la rotation
    /// gauche/droite (l'encoche change de côté).
    /// </summary>
    public static class SafeAreaUtil
    {
        /// <summary>Marges en unités canvas. x = gauche, y = droite, z = haut, w = bas.</summary>
        public static Vector4 Insets(Canvas canvas)
        {
            Rect sa = Screen.safeArea;
            float sf = (canvas != null && canvas.scaleFactor > 0.0001f) ? canvas.scaleFactor : 1f;

            float left   = sa.xMin / sf;
            float right   = (Screen.width  - sa.xMax) / sf;
            float bottom = sa.yMin / sf;
            float top     = (Screen.height - sa.yMax) / sf;

            // Garde-fous (valeurs négatives possibles en éditeur).
            if (left   < 0f) left   = 0f;
            if (right  < 0f) right  = 0f;
            if (top    < 0f) top    = 0f;
            if (bottom < 0f) bottom = 0f;

            return new Vector4(left, right, top, bottom);
        }

        // Géométrie de la sidebar (navbar) — doit rester synchronisée avec NavbarView.
        public const float NavbarLeftMargin = 0f;     // rail collé au bord gauche (NavbarView)
        public const float NavbarWidth      = 104f;   // largeur visible du rail (hors encoche)

        /// <summary>
        /// Espace à réserver en HAUT d'une page plein écran pour ne rien cacher sous le bandeau
        /// (carte entreprise à gauche ≈ 140 unités canvas, boutons à droite). Inclut l'encoche.
        /// </summary>
        public const float HudTopHeight = 74f;    // barre du haut collée au bord (HeaderView.HeaderHeight 64) + 10 de marge
        public static float HudTopReserve(Canvas canvas) => HudTopHeight + Insets(canvas).z;

        /// <summary>
        /// Espace à réserver à GAUCHE d'une page/popup pour ne pas passer sous la
        /// sidebar. Inclut l'encoche (insets.x) car la navbar y est elle aussi décalée.
        /// </summary>
        public static float SidebarReserve(Canvas canvas, float gap = 18f)
            => NavbarLeftMargin + Insets(canvas).x + NavbarWidth + gap;
    }
}
