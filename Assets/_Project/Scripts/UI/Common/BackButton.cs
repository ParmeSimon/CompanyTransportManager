using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TransportManager.UI.Common
{
    /// <summary>
    /// Bouton « Retour » unique de l'interface (remplace toutes les croix) :
    /// pastille arrondie, chevron dessiné en code, liseré clair, léger enfoncement au toucher.
    ///   BackButton.Create(parent, Close);                 // dans une mise en page
    ///   BackButton.Create(parent, Close, anchored: true); // posé en haut à gauche
    /// </summary>
    public static class BackButton
    {
        private static readonly Color32 Bg      = new Color32(0x2E, 0x33, 0x3D, 255);
        private static readonly Color32 BgPress = new Color32(0x38, 0x3E, 0x4A, 255);
        private static readonly Color32 Ink     = new Color32(0xEC, 0xEE, 0xF5, 255);
        private static readonly Color32 Accent  = new Color32(0x38, 0x85, 0xFF, 255);

        public const float Width = 128f, Height = 44f;

        private static Sprite _pill, _chevron;

        public static Button Create(Transform parent, UnityEngine.Events.UnityAction onClick, string label = "Retour")
        {
            var go = new GameObject("BackButton", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.sprite = Pill(); img.type = Image.Type.Sliced; img.color = Bg;
            var ol = go.AddComponent<Outline>();
            ol.effectColor = new Color(1f, 1f, 1f, 0.10f); ol.effectDistance = new Vector2(1f, -1f);

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.normalColor = Color.white; colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(BgPress.r / (float)Bg.r, BgPress.g / (float)Bg.g, BgPress.b / (float)Bg.b, 1f);
            colors.fadeDuration = 0.06f;
            btn.colors = colors;
            btn.onClick.AddListener(onClick);
            go.AddComponent<PressScale>();

            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = Width; le.minWidth = Width; le.preferredHeight = Height; le.minHeight = Height;
            ((RectTransform)go.transform).sizeDelta = new Vector2(Width, Height);

            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(12, 16, 0, 0); hlg.spacing = 8;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
            hlg.childControlWidth = true; hlg.childControlHeight = true;

            // Pastille ronde bleue avec le chevron
            var dot = new GameObject("Chevron", typeof(RectTransform));
            dot.transform.SetParent(go.transform, false);
            var dImg = dot.AddComponent<Image>();
            dImg.sprite = Pill(); dImg.type = Image.Type.Sliced; dImg.color = new Color32(Accent.r, Accent.g, Accent.b, 60);
            dImg.raycastTarget = false;
            var dLe = dot.AddComponent<LayoutElement>(); dLe.preferredWidth = 26; dLe.preferredHeight = 26; dLe.minWidth = 26;
            var chev = new GameObject("Icon", typeof(RectTransform));
            chev.transform.SetParent(dot.transform, false);
            var cImg = chev.AddComponent<Image>();
            cImg.sprite = Chevron(); cImg.color = Ink; cImg.raycastTarget = false; cImg.preserveAspect = true;
            var cRt = (RectTransform)chev.transform;
            cRt.anchorMin = cRt.anchorMax = new Vector2(0.5f, 0.5f);
            cRt.sizeDelta = new Vector2(12, 12); cRt.anchoredPosition = new Vector2(-1f, 0f);

            var lblGo = new GameObject("Label", typeof(RectTransform));
            lblGo.transform.SetParent(go.transform, false);
            var lbl = lblGo.AddComponent<TextMeshProUGUI>();
            lbl.text = label; lbl.fontSize = 15; lbl.fontStyle = FontStyles.Bold; lbl.color = Ink;
            lbl.alignment = TextAlignmentOptions.MidlineLeft; lbl.raycastTarget = false;
            lbl.textWrappingMode = TextWrappingModes.NoWrap;
            return btn;
        }

        /// Variante posée librement (hors mise en page) en haut à gauche du parent.
        public static Button CreateTopLeft(Transform parent, UnityEngine.Events.UnityAction onClick, Vector2 offset)
        {
            var b = Create(parent, onClick);
            b.GetComponent<LayoutElement>().ignoreLayout = true;
            var rt = (RectTransform)b.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = offset;
            return b;
        }

        // Petit « enfoncement » au toucher (retour tactile visuel).
        private class PressScale : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
        {
            public void OnPointerDown(PointerEventData e) => transform.localScale = new Vector3(0.96f, 0.96f, 1f);
            public void OnPointerUp(PointerEventData e)   => transform.localScale = Vector3.one;
            public void OnPointerExit(PointerEventData e) => transform.localScale = Vector3.one;
        }

        private static Sprite Pill()
        {
            if (_pill != null) return _pill;
            const int size = 64, r = 22;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int cx = -1, cy = -1;
                    if (x < r && y < r) { cx = r; cy = r; }
                    else if (x >= size - r && y < r) { cx = size - r; cy = r; }
                    else if (x < r && y >= size - r) { cx = r; cy = size - r; }
                    else if (x >= size - r && y >= size - r) { cx = size - r; cy = size - r; }
                    float a = cx < 0 ? 1f : Mathf.Clamp01(r - Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) + 0.5f);
                    px[y * size + x] = new Color(1, 1, 1, a);
                }
            tex.SetPixels(px); tex.Apply();
            _pill = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                                  SpriteMeshType.FullRect, new Vector4(r, r, r, r));
            return _pill;
        }

        // Chevron « < » anti-aliasé : deux segments épais qui se rejoignent à gauche.
        private static Sprite Chevron()
        {
            if (_chevron != null) return _chevron;
            const int size = 48;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[size * size];
            Vector2 tip = new Vector2(14, 24), top = new Vector2(32, 42), bot = new Vector2(32, 6);
            const float half = 3.6f;   // demi-épaisseur du trait
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float d = Mathf.Min(SegDist(p, tip, top), SegDist(p, tip, bot));
                    px[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(half - d + 0.5f));
                }
            tex.SetPixels(px); tex.Apply();
            _chevron = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return _chevron;
        }

        private static float SegDist(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }
    }
}
