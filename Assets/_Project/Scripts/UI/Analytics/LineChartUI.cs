using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TransportManager.UI.Analytics
{
    [RequireComponent(typeof(CanvasRenderer))]
    public class LineChartUI : MaskableGraphic
    {
        private float[] _points;
        private Color   _lineColor = Color.white;
        private Color   _fillColor = new Color(1f, 1f, 1f, 0.12f);
        private float[] _gridValues;

        private const float LineThickness = 4f;
        private const float DotRadius     = 7f;
        private const int   DotSegments   = 12;
        private const int   MaxDotsCount  = 15;
        private const float PadLeft       = 16f;
        private const float PadRight      = 16f;
        private const float PadTop        = 12f;
        private const float PadBot        = 12f;

        public float VizMin { get; private set; }
        public float VizMax { get; private set; }

        public void SetData(float[] points, Color lineColor)
        {
            _points    = points;
            _lineColor = lineColor;
            _fillColor = new Color(lineColor.r, lineColor.g, lineColor.b, 0.12f);

            if (points != null && points.Length >= 2)
            {
                float min = points[0], max = points[0];
                foreach (float p in points)
                {
                    if (p < min) min = p;
                    if (p > max) max = p;
                }
                float range = max - min;
                if (range < 1f) { min -= 0.5f; max += 0.5f; range = 1f; }
                VizMin = min - range * 0.08f;
                VizMax = max + range * 0.08f;
                _gridValues = ComputeNiceGridValues(min, max, 4);
            }

            SetVerticesDirty();
        }

        // Returns normalized Y (0=bottom, 1=top) of a value within the chart rect
        public float GetNormalizedRectY(float value)
        {
            Rect r = rectTransform.rect;
            float chartH = r.height - PadTop - PadBot;
            float range  = VizMax - VizMin;
            if (chartH <= 0 || range <= 0) return 0.5f;
            return (PadBot + chartH * (value - VizMin) / range) / r.height;
        }

        public static float[] ComputeNiceGridValues(float min, float max, int count = 4)
        {
            float range = max - min;
            if (range <= 0 || count <= 0) return new float[0];

            float rawStep = range / count;
            float mag = Mathf.Pow(10f, Mathf.Floor(Mathf.Log10(Mathf.Max(rawStep, 0.0001f))));
            float[] niceM = { 1f, 2f, 2.5f, 5f, 10f };
            float step = mag;
            foreach (float m in niceM)
            {
                if (m * mag >= rawStep) { step = m * mag; break; }
            }

            var result = new List<float>();
            float first = Mathf.Ceil(min / step) * step;
            for (float v = first; v <= max + step * 0.01f; v += step)
            {
                if (v >= min && v <= max) result.Add(v);
                if (result.Count >= count + 1) break;
            }
            return result.ToArray();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_points == null || _points.Length < 2) return;

            Rect r = rectTransform.rect;
            float chartW = r.width  - PadLeft - PadRight;
            float chartH = r.height - PadTop  - PadBot;
            if (chartW <= 0 || chartH <= 0) return;

            float range = VizMax - VizMin;
            if (range <= 0) return;

            int n = _points.Length;
            var pos = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                pos[i] = new Vector2(
                    r.xMin + PadLeft + chartW * i / (n - 1),
                    r.yMin + PadBot  + chartH * (_points[i] - VizMin) / range
                );
            }

            float baseline = r.yMin + PadBot;

            // Grid lines
            Color gridC = new Color(1f, 1f, 1f, 0.06f);
            if (_gridValues != null)
            {
                foreach (float gv in _gridValues)
                {
                    float gy = r.yMin + PadBot + chartH * (gv - VizMin) / range;
                    if (gy < r.yMin + PadBot - 1f || gy > r.yMin + PadBot + chartH + 1f) continue;
                    AddThickLine(vh,
                        new Vector2(r.xMin + PadLeft,  gy),
                        new Vector2(r.xMax - PadRight, gy), 1f, gridC);
                }
            }

            // Fill area
            for (int i = 0; i < n - 1; i++)
                AddQuad(vh,
                    pos[i].x, baseline,
                    pos[i].x, pos[i].y,
                    pos[i + 1].x, pos[i + 1].y,
                    pos[i + 1].x, baseline,
                    _fillColor);

            // Line stroke
            for (int i = 0; i < n - 1; i++)
                AddThickLine(vh, pos[i], pos[i + 1], LineThickness, _lineColor);

            // Dots (only when few points)
            if (n <= MaxDotsCount)
                foreach (var p in pos)
                    AddCircle(vh, p, DotRadius, DotSegments, _lineColor);
        }

        private static void AddQuad(VertexHelper vh,
            float x0, float y0, float x1, float y1,
            float x2, float y2, float x3, float y3, Color c)
        {
            int vi = vh.currentVertCount;
            vh.AddVert(new Vector3(x0, y0), c, Vector2.zero);
            vh.AddVert(new Vector3(x1, y1), c, Vector2.zero);
            vh.AddVert(new Vector3(x2, y2), c, Vector2.zero);
            vh.AddVert(new Vector3(x3, y3), c, Vector2.zero);
            vh.AddTriangle(vi, vi + 1, vi + 2);
            vh.AddTriangle(vi, vi + 2, vi + 3);
        }

        private static void AddThickLine(VertexHelper vh, Vector2 a, Vector2 b, float thick, Color c)
        {
            Vector2 dir  = (b - a).normalized;
            Vector2 perp = new Vector2(-dir.y, dir.x) * (thick * 0.5f);
            AddQuad(vh,
                a.x - perp.x, a.y - perp.y,
                a.x + perp.x, a.y + perp.y,
                b.x + perp.x, b.y + perp.y,
                b.x - perp.x, b.y - perp.y, c);
        }

        private static void AddCircle(VertexHelper vh, Vector2 center, float radius, int segs, Color c)
        {
            int vi = vh.currentVertCount;
            vh.AddVert(new Vector3(center.x, center.y), c, Vector2.zero);
            for (int s = 0; s < segs; s++)
            {
                float angle = s * 2f * Mathf.PI / segs;
                vh.AddVert(new Vector3(
                    center.x + Mathf.Cos(angle) * radius,
                    center.y + Mathf.Sin(angle) * radius), c, Vector2.zero);
            }
            for (int s = 0; s < segs; s++)
                vh.AddTriangle(vi, vi + 1 + s, vi + 1 + (s + 1) % segs);
        }
    }
}
