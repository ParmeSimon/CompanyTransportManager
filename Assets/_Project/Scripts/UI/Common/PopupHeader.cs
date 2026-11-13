using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TransportManager.UI.Common
{
    /// <summary>
    /// Helper statique — construit une barre de titre standard pour toutes les popups.
    /// Usage : PopupHeader.Build(panelTransform, "Mon titre", onClose, titleBarHeight);
    /// </summary>
    public static class PopupHeader
    {
        // Palette partagée (Header / Navbar / ContractsPanel / VehiclesTab)
        private static readonly Color32 BgPill  = new Color32(0x1A, 0x1D, 0x24, 230);
        private static readonly Color32 TextPri = new Color32(0xEC, 0xEE, 0xF5, 255);
        private static readonly Color32 TextSec = new Color32(0x7A, 0x8F, 0xA6, 255);
        private static readonly Color32 Divider = new Color32(0x3A, 0x3F, 0x4A, 150);
        private static readonly Color32 Accent  = new Color32(0x38, 0x85, 0xFF, 255);

        public static void Build(Transform panel, string title, UnityEngine.Events.UnityAction onClose,
                                 int barHeight, Sprite roundedSprite)
        {
            // ── Titre ──
            var bar   = Make("TitleBar", panel);
            var barRt = bar.GetComponent<RectTransform>();
            barRt.anchorMin = new Vector2(0, 1);
            barRt.anchorMax = new Vector2(1, 1);
            barRt.pivot     = new Vector2(0.5f, 1);
            barRt.offsetMin = new Vector2(0, -barHeight);
            barRt.offsetMax = Vector2.zero;

            var hlg = bar.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment         = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth  = false;
            hlg.childForceExpandHeight = true;
            hlg.childControlWidth      = true;
            hlg.childControlHeight     = true;
            hlg.padding                = new RectOffset(12, 20, 6, 6);
            hlg.spacing                = 14;
            hlg.childForceExpandHeight = false;
            hlg.childAlignment         = TextAnchor.MiddleLeft;

            // « Retour » à gauche (plus de croix) puis le titre.
            BackButton.Create(bar.transform, onClose);

            var tmp = MakeTMP("Title", bar.transform, title, 18, FontStyles.Bold, TextPri);
            tmp.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

            // ── Séparateur ──
            var div   = Make("Divider", panel);
            var divImg = div.AddComponent<Image>();
            divImg.color         = Divider;
            divImg.raycastTarget = false;
            var divRt = div.GetComponent<RectTransform>();
            divRt.anchorMin = new Vector2(0, 1);
            divRt.anchorMax = new Vector2(1, 1);
            divRt.pivot     = new Vector2(0.5f, 1);
            divRt.offsetMin = new Vector2(0, -(barHeight + 1));
            divRt.offsetMax = new Vector2(0, -barHeight);
        }

        private static GameObject Make(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static TMP_Text MakeTMP(string name, Transform parent, string text,
                                        float size, FontStyles style, Color32 color)
        {
            var go  = Make(name, parent);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text          = text;
            tmp.fontSize      = size;
            tmp.fontStyle     = style;
            tmp.color         = color;
            tmp.alignment     = TextAlignmentOptions.MidlineLeft;
            tmp.raycastTarget = false;
            return tmp;
        }
    }
}
