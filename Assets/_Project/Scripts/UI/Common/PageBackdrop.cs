using UnityEngine;
using UnityEngine.UI;

namespace TransportManager.UI.Common
{
    /// <summary>
    /// Fond de page sobre, généré par code (aucune image) : dégradé sombre aux couleurs de
    /// l'interface, trame de plan très discrète et une lueur douce de la couleur de la page
    /// en haut à droite. Remplace les photos en fond d'écran : cohérent avec la palette,
    /// net à toutes les résolutions, jamais déformé.
    /// </summary>
    public class PageBackdrop : MonoBehaviour
    {
        private static readonly Color Top    = new Color32(0x20, 0x24, 0x2C, 255);
        private static readonly Color Bottom = new Color32(0x12, 0x15, 0x1B, 255);
        private const float GridCell = 48f;   // taille d'une case de la trame (unités canvas)

        private static Texture2D _gradientTex, _gridTex, _glowTex;
        private RawImage _grid;

        /// <summary>
        /// Crée le fond comme PREMIER enfant de <paramref name="parent"/>, étiré sur lui.
        /// outsetMin / outsetMax : débord (gauche/bas, droite/haut) pour couvrir aussi la
        /// zone sous le header et la navbar quand la page est insérée.
        /// </summary>
        public static PageBackdrop Create(Transform parent, Color accent, Vector2 outsetMin = default, Vector2 outsetMax = default)
        {
            var go = new GameObject("PageBackdrop", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.transform.SetAsFirstSibling();
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = -outsetMin;
            rt.offsetMax = outsetMax;

            var gradient = Layer("Gradient", go.transform, GradientTex(), Color.white);
            Stretch(gradient.rectTransform);

            // Lueur d'accent : grande tache très diffuse, en haut à droite.
            var glow = Layer("Glow", go.transform, GlowTex(), new Color(accent.r, accent.g, accent.b, 0.10f));
            var grt = glow.rectTransform;
            grt.anchorMin = grt.anchorMax = new Vector2(1f, 1f);
            grt.pivot = new Vector2(0.5f, 0.5f);
            grt.sizeDelta = new Vector2(1400f, 1000f);
            grt.anchoredPosition = new Vector2(-220f, -120f);

            return go.AddComponent<PageBackdrop>();
        }

        private void OnRectTransformDimensionsChange() => UpdateGrid();

        // La trame se répète (wrap Repeat) : une case = GridCell unités, quelle que soit la taille.
        private void UpdateGrid()
        {
            if (_grid == null) return;
            var size = ((RectTransform)transform).rect.size;
            if (size.x <= 0f || size.y <= 0f) return;
            _grid.uvRect = new Rect(0f, 0f, size.x / GridCell, size.y / GridCell);
        }

        private static RawImage Layer(string name, Transform parent, Texture tex, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var raw = go.AddComponent<RawImage>();
            raw.texture = tex;
            raw.color = color;
            raw.raycastTarget = false;
            return raw;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        // ── Textures générées (créées une seule fois, partagées entre les pages) ──────

        private static Texture2D GradientTex()
        {
            if (_gradientTex != null) return _gradientTex;
            const int h = 128;
            _gradientTex = new Texture2D(1, h, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
            };
            for (int y = 0; y < h; y++)
                _gradientTex.SetPixel(0, y, Color.Lerp(Bottom, Top, y / (h - 1f)));
            _gradientTex.Apply();
            return _gradientTex;
        }

        // Une case de trame : un trait d'1 px à gauche et en bas, presque invisible.
        private static Texture2D GridTex()
        {
            if (_gridTex != null) return _gridTex;
            const int s = 48;
            _gridTex = new Texture2D(s, s, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear
            };
            var clear = new Color(1f, 1f, 1f, 0f);
            var line  = new Color(1f, 1f, 1f, 0.035f);
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                    _gridTex.SetPixel(x, y, x == 0 || y == 0 ? line : clear);
            _gridTex.Apply();
            return _gridTex;
        }

        private static Texture2D GlowTex()
        {
            if (_glowTex != null) return _glowTex;
            const int s = 128;
            _glowTex = new Texture2D(s, s, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
            };
            float c = (s - 1) * 0.5f;
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float d = Mathf.Clamp01(Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c);
                    float a = 1f - d;
                    _glowTex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a * a));
                }
            _glowTex.Apply();
            return _glowTex;
        }
    }
}
