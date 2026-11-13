using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TransportManager.UI.Skills
{
    /// <summary>
    /// Dessine toutes les branches de l'arbre de compétences en un seul maillage :
    /// des courbes de Bézier cubiques dont l'épaisseur s'effile du parent vers l'enfant
    /// (rendu « organique », comme de vraies branches). Coordonnées locales : origine au
    /// pivot du RectTransform (mettre le pivot au centre et étirer sur le graphe).
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]   // sans lui : MissingComponentException à chaque frame
    public class SkillLinksGraphic : MaskableGraphic
    {
        public struct Curve
        {
            public Vector2 a, ca, cb, b;   // départ, poignée de départ, poignée d'arrivée, arrivée
            public float widthA, widthB;   // épaisseur au départ / à l'arrivée
            public Color32 color;
        }

        private const int Segments = 20;
        private readonly List<Curve> _curves = new List<Curve>();

        public void Clear()
        {
            _curves.Clear();
            SetVerticesDirty();
        }

        public void Add(Curve c)
        {
            _curves.Add(c);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            foreach (var c in _curves)
            {
                int start = vh.currentVertCount;
                for (int i = 0; i <= Segments; i++)
                {
                    float t = i / (float)Segments;
                    Vector2 p   = Point(c, t);
                    Vector2 tan = Tangent(c, t);
                    if (tan.sqrMagnitude < 1e-6f) tan = c.b - c.a;
                    Vector2 n = new Vector2(-tan.y, tan.x).normalized;
                    float half = Mathf.Lerp(c.widthA, c.widthB, t) * 0.5f;

                    vh.AddVert(p + n * half, c.color, Vector2.zero);
                    vh.AddVert(p - n * half, c.color, Vector2.zero);
                    if (i == 0) continue;
                    int k = start + i * 2;
                    vh.AddTriangle(k - 2, k - 1, k);
                    vh.AddTriangle(k - 1, k + 1, k);
                }
            }
        }

        private static Vector2 Point(Curve c, float t)
        {
            float u = 1f - t;
            return u * u * u * c.a + 3f * u * u * t * c.ca + 3f * u * t * t * c.cb + t * t * t * c.b;
        }

        private static Vector2 Tangent(Curve c, float t)
        {
            float u = 1f - t;
            return 3f * u * u * (c.ca - c.a) + 6f * u * t * (c.cb - c.ca) + 3f * t * t * (c.b - c.cb);
        }
    }
}
