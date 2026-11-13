using UnityEngine;
using UnityEngine.UI;

namespace TransportManager.UI.Common
{
    /// <summary>
    /// Rend défilable une carte/popup dont la hauteur suit son contenu (ContentSizeFitter) :
    /// tant qu'elle tient à l'écran rien ne change ; si elle dépasse (mobile, longues listes),
    /// sa hauteur visible est plafonnée à l'écran et on la fait défiler au doigt.
    ///   ScrollFit.Wrap(cardRt, margin: 24f);
    /// La carte doit être centrée dans son parent (anchors 0.5/0.5) et de largeur fixe.
    /// </summary>
    public class ScrollFit : MonoBehaviour
    {
        private RectTransform _viewport, _content;
        private ScrollRect    _scroll;
        private float         _margin;

        public static ScrollRect Wrap(RectTransform card, float margin = 24f)
        {
            var parent = card.parent;
            var vpGo = new GameObject(card.name + "_Scroll", typeof(RectTransform));
            vpGo.transform.SetParent(parent, false);
            vpGo.transform.SetSiblingIndex(card.GetSiblingIndex());
            var vp = (RectTransform)vpGo.transform;
            vp.anchorMin = vp.anchorMax = new Vector2(0.5f, 0.5f);
            vp.pivot = new Vector2(0.5f, 0.5f);
            vp.anchoredPosition = card.anchoredPosition;
            vp.sizeDelta = new Vector2(card.sizeDelta.x, 100f);
            vpGo.AddComponent<RectMask2D>();
            var hit = vpGo.AddComponent<Image>();          // capte le glisser pour défiler
            hit.color = new Color(0f, 0f, 0f, 0f);

            card.SetParent(vp, false);
            card.anchorMin = card.anchorMax = new Vector2(0.5f, 1f);
            card.pivot = new Vector2(0.5f, 1f);
            card.anchoredPosition = Vector2.zero;

            var sr = vpGo.AddComponent<ScrollRect>();
            sr.horizontal = false;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 36f;
            sr.viewport = vp;
            sr.content = card;

            var fit = vpGo.AddComponent<ScrollFit>();
            fit._viewport = vp; fit._content = card; fit._scroll = sr; fit._margin = margin;
            fit.LateUpdate();
            return sr;
        }

        private float _minHeight = -1f;   // ≥ 0 : mode « étiré » (WrapStretched)

        /// <summary>
        /// Pour un contenu étiré dans son panneau (anchors 0→1, layout à hauteurs flexibles) :
        /// il remplit toujours le panneau, mais ne descend jamais sous minHeight ; si l'écran
        /// est plus petit, il garde minHeight et on le fait défiler.
        /// </summary>
        public static ScrollRect WrapStretched(RectTransform content, float minHeight)
        {
            var vpGo = new GameObject(content.name + "_Scroll", typeof(RectTransform));
            vpGo.transform.SetParent(content.parent, false);
            vpGo.transform.SetSiblingIndex(content.GetSiblingIndex());
            var vp = (RectTransform)vpGo.transform;
            vp.anchorMin = content.anchorMin; vp.anchorMax = content.anchorMax;
            vp.pivot = content.pivot;
            vp.offsetMin = content.offsetMin; vp.offsetMax = content.offsetMax;
            vpGo.AddComponent<RectMask2D>();
            var hit = vpGo.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);

            content.SetParent(vp, false);
            content.anchorMin = new Vector2(0f, 1f); content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero; content.offsetMax = Vector2.zero;

            var sr = vpGo.AddComponent<ScrollRect>();
            sr.horizontal = false;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 36f;
            sr.viewport = vp;
            sr.content = content;

            var fit = vpGo.AddComponent<ScrollFit>();
            fit._viewport = vp; fit._content = content; fit._scroll = sr; fit._minHeight = minHeight;
            fit.LateUpdate();
            return sr;
        }

        private void LateUpdate()
        {
            if (_viewport == null || _content == null) return;
            if (_minHeight >= 0f)
            {
                float viewH = _viewport.rect.height;
                float ch = Mathf.Max(viewH, _minHeight);
                if (Mathf.Abs(_content.sizeDelta.y - ch) > 0.5f) _content.sizeDelta = new Vector2(0f, ch);
                _scroll.vertical = ch > viewH + 0.5f;
                return;
            }
            var parentRt = _viewport.parent as RectTransform;
            float avail = parentRt != null ? parentRt.rect.height - 2f * _margin : float.MaxValue;
            float want  = _content.rect.height;
            float h     = Mathf.Max(80f, Mathf.Min(want, avail));
            if (Mathf.Abs(_viewport.sizeDelta.y - h) > 0.5f || Mathf.Abs(_viewport.sizeDelta.x - _content.rect.width) > 0.5f)
                _viewport.sizeDelta = new Vector2(_content.rect.width, h);
            _scroll.vertical = want > h + 0.5f;
        }
    }
}
