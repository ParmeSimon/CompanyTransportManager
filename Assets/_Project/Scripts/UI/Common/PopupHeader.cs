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
            hlg.padding                = new RectOffset(20, 14, 0, 0);
            hlg.spacing                = 12;

            // Filet d'accent bleu avant le titre (cohérent avec le reste de l'UI)
            var accent    = Make("Accent", bar.transform);
            var accentImg = accent.AddComponent<Image>();
            accentImg.sprite        = roundedSprite;
            accentImg.type          = Image.Type.Sliced;
            accentImg.color         = Accent;
            accentImg.raycastTarget = false;
            var accentLe = accent.AddComponent<LayoutElement>();
            accentLe.preferredWidth  = 3;
            accentLe.preferredHeight = 24;

            var tmp = MakeTMP("Title", bar.transform, title, 18, FontStyles.Bold, TextPri);
            tmp.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

            // ── Bouton ✕ ──
            var btnGo  = Make("CloseBtn", bar.transform);
            var btnImg = btnGo.AddComponent<Image>();
            btnImg.sprite = roundedSprite;
            btnImg.type   = Image.Type.Sliced;
            btnImg.color  = BgPill;
            var btn = btnGo.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            btn.onClick.AddListener(onClose);
            var btnLe = btnGo.AddComponent<LayoutElement>();
            btnLe.preferredWidth  = 40;
            btnLe.preferredHeight = 40;

            var iconGo  = Make("Icon", btnGo.transform);
            var iconImg = iconGo.AddComponent<Image>();
            UiIcons.Apply(iconImg, "UI/Icons/icons/x");
            iconImg.color          = TextSec;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget  = false;
            var iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin        = new Vector2(0.5f, 0.5f);
            iconRt.anchorMax        = new Vector2(0.5f, 0.5f);
            iconRt.pivot            = new Vector2(0.5f, 0.5f);
            iconRt.anchoredPosition = Vector2.zero;
            iconRt.sizeDelta        = new Vector2(20, 20);

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
