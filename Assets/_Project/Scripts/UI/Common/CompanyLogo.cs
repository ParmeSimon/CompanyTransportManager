using UnityEngine;
using UnityEngine.UI;
using TransportManager.Entities.Company;

namespace TransportManager.UI.Common
{
    /// <summary>
    /// Rendu du logo d'entreprise : photo importée si présente, sinon le logo par défaut du jeu.
    /// </summary>
    public static class CompanyLogo
    {
        private static readonly Color32 NeutralBg = new Color32(0x1A, 0x1D, 0x24, 255);

        public static Sprite DefaultLogo =>
            Resources.Load<Sprite>("UI/Logo/LogoFull") ?? Resources.Load<Sprite>("UI/Logo/Logo");

        /// <summary>
        /// Applique le logo sur une paire (fond, premier plan). Le cadre `bg` devrait porter
        /// un Mask pour des coins arrondis.
        /// </summary>
        public static void ApplyTo(Image bg, Image fg, CompanyProfile c)
        {
            if (bg == null || fg == null) return;

            Sprite custom = (c != null && c.logoIsCustom) ? CompanyLogoStore.LoadSprite() : null;

            bg.color = NeutralBg;
            fg.color = Color.white;
            fg.preserveAspect = true;

            if (custom != null)
            {
                fg.sprite = custom;
                SetInset(fg.rectTransform, 0f);     // la photo remplit le cadre
            }
            else
            {
                fg.sprite = DefaultLogo;
                SetInset(fg.rectTransform, 0.12f);  // léger retrait pour le logo par défaut
            }
        }

        // Ancre l'image au cadre avec une marge fractionnelle (0 = plein cadre).
        private static void SetInset(RectTransform rt, float frac)
        {
            rt.anchorMin = new Vector2(frac, frac);
            rt.anchorMax = new Vector2(1f - frac, 1f - frac);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
