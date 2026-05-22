using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TransportManager.Systems.Map.Geocoding;
using TransportManager.Systems.Map.Visualization;

namespace TransportManager.UI.Map
{
    public class AddressSearchView : MonoBehaviour
    {
        private SlippyMapView _mapView;
        private AddressMarker _marker;
        private TMP_InputField _inputField;
        private TMP_Text _statusText;
        private bool _searching;

        private static readonly Color BgColor  = new Color(0.11f, 0.14f, 0.19f, 0.93f);
        private static readonly Color RowBg    = new Color(0.18f, 0.22f, 0.28f, 1f);
        private static readonly Color BtnColor = new Color(0.18f, 0.53f, 0.95f, 1f);

        public void Init(SlippyMapView mapView, AddressMarker marker)
        {
            _mapView = mapView;
            _marker  = marker;
            BuildUI();
        }

        private void BuildUI()
        {
            var rt = GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(8f,  -74f);
            rt.offsetMax = new Vector2(-8f, -8f);

            var bg = gameObject.AddComponent<Image>();
            bg.color  = BgColor;
            bg.sprite = MakeRoundedSprite();
            bg.type   = Image.Type.Sliced;

            // — Search button (right side of input row) —
            const float rowH = 40f;
            const float btnW = 78f;
            const float rowY = -6f;

            var btnGo  = new GameObject("SearchBtn", typeof(RectTransform));
            btnGo.transform.SetParent(transform, false);
            var btnImg = btnGo.AddComponent<Image>();
            btnImg.color  = BtnColor;
            btnImg.sprite = MakeRoundedSprite();
            btnImg.type   = Image.Type.Sliced;
            var btn = btnGo.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            btn.transition    = Selectable.Transition.ColorTint;
            btn.onClick.AddListener(OnSearchClicked);
            var btnRt = btnGo.GetComponent<RectTransform>();
            btnRt.anchorMin        = new Vector2(1f, 1f);
            btnRt.anchorMax        = new Vector2(1f, 1f);
            btnRt.pivot            = new Vector2(1f, 1f);
            btnRt.sizeDelta        = new Vector2(btnW, rowH);
            btnRt.anchoredPosition = new Vector2(-6f, rowY);
            var btnLblGo = new GameObject("Lbl", typeof(RectTransform));
            btnLblGo.transform.SetParent(btnRt, false);
            var btnLbl = btnLblGo.AddComponent<TextMeshProUGUI>();
            btnLbl.text           = "Chercher";
            btnLbl.fontSize       = 11f;
            btnLbl.fontStyle      = FontStyles.Bold;
            btnLbl.color          = Color.white;
            btnLbl.alignment      = TextAlignmentOptions.Center;
            btnLbl.raycastTarget  = false;
            var btnLblRt = btnLblGo.GetComponent<RectTransform>();
            btnLblRt.anchorMin = Vector2.zero;
            btnLblRt.anchorMax = Vector2.one;
            btnLblRt.offsetMin = Vector2.zero;
            btnLblRt.offsetMax = Vector2.zero;

            // — Input field —
            var inputGo = new GameObject("InputField", typeof(RectTransform));
            inputGo.transform.SetParent(transform, false);
            var inputBg = inputGo.AddComponent<Image>();
            inputBg.color  = RowBg;
            inputBg.sprite = MakeRoundedSprite();
            inputBg.type   = Image.Type.Sliced;
            _inputField = inputGo.AddComponent<TMP_InputField>();
            var inputRt = inputGo.GetComponent<RectTransform>();
            inputRt.anchorMin        = new Vector2(0f, 1f);
            inputRt.anchorMax        = new Vector2(1f, 1f);
            inputRt.pivot            = new Vector2(0f, 1f);
            inputRt.offsetMin        = new Vector2(6f,  rowY - rowH);
            inputRt.offsetMax        = new Vector2(-(btnW + 10f), rowY);

            // TextArea (viewport with clipping)
            var vpGo = new GameObject("TextViewport", typeof(RectTransform));
            vpGo.transform.SetParent(inputGo.transform, false);
            vpGo.AddComponent<RectMask2D>();
            var vpRt = vpGo.GetComponent<RectTransform>();
            vpRt.anchorMin = Vector2.zero;
            vpRt.anchorMax = Vector2.one;
            vpRt.offsetMin = new Vector2(10f, 2f);
            vpRt.offsetMax = new Vector2(-10f, -2f);

            // Placeholder
            var phGo = new GameObject("Placeholder", typeof(RectTransform));
            phGo.transform.SetParent(vpGo.transform, false);
            var phText = phGo.AddComponent<TextMeshProUGUI>();
            phText.text          = "Rechercher une adresse…";
            phText.color         = new Color(0.52f, 0.57f, 0.65f, 0.9f);
            phText.fontSize      = 12f;
            phText.alignment     = TextAlignmentOptions.MidlineLeft;
            phText.raycastTarget = false;
            var phRt = phGo.GetComponent<RectTransform>();
            phRt.anchorMin = Vector2.zero;
            phRt.anchorMax = Vector2.one;
            phRt.offsetMin = Vector2.zero;
            phRt.offsetMax = Vector2.zero;

            // Input text
            var txtGo = new GameObject("Text", typeof(RectTransform));
            txtGo.transform.SetParent(vpGo.transform, false);
            var txtComp = txtGo.AddComponent<TextMeshProUGUI>();
            txtComp.color         = Color.white;
            txtComp.fontSize      = 12f;
            txtComp.alignment     = TextAlignmentOptions.MidlineLeft;
            var txtRt = txtGo.GetComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = Vector2.zero;
            txtRt.offsetMax = Vector2.zero;

            _inputField.textViewport = vpRt;
            _inputField.textComponent = txtComp;
            _inputField.placeholder   = phText;
            _inputField.onSubmit.AddListener(_ => OnSearchClicked());

            // — Status line —
            var statusGo = new GameObject("Status", typeof(RectTransform));
            statusGo.transform.SetParent(transform, false);
            _statusText = statusGo.AddComponent<TextMeshProUGUI>();
            _statusText.fontSize      = 10f;
            _statusText.color         = new Color(0.55f, 0.60f, 0.68f, 0.9f);
            _statusText.alignment     = TextAlignmentOptions.MidlineLeft;
            _statusText.textWrappingMode = TextWrappingModes.NoWrap;
            _statusText.overflowMode  = TextOverflowModes.Ellipsis;
            _statusText.raycastTarget = false;
            var statusRt = statusGo.GetComponent<RectTransform>();
            statusRt.anchorMin        = new Vector2(0f, 0f);
            statusRt.anchorMax        = new Vector2(1f, 0f);
            statusRt.pivot            = new Vector2(0f, 0f);
            statusRt.offsetMin        = new Vector2(10f, 4f);
            statusRt.offsetMax        = new Vector2(-10f, 20f);
        }

        private void OnSearchClicked()
        {
            if (_searching || string.IsNullOrWhiteSpace(_inputField?.text)) return;
            StartCoroutine(DoSearch(_inputField.text.Trim()));
        }

        private IEnumerator DoSearch(string query)
        {
            _searching = true;
            SetStatus("Recherche en cours…", new Color(0.75f, 0.78f, 0.82f));

            NominatimGeocoder.Result res = default;
            yield return NominatimGeocoder.Geocode(query, r => res = r);

            if (res.success)
            {
                _marker?.PlaceAt(res.latitude, res.longitude);
                _mapView?.SetView(res.latitude, res.longitude, Mathf.Max(_mapView.Zoom, 12));
                SetStatus(res.displayName ?? query, new Color(0.38f, 0.82f, 0.44f));
            }
            else
            {
                _marker?.Clear();
                SetStatus("Adresse introuvable.", new Color(0.90f, 0.35f, 0.30f));
            }

            _searching = false;
        }

        private void SetStatus(string msg, Color color)
        {
            if (_statusText == null) return;
            _statusText.text  = msg;
            _statusText.color = color;
        }

        private static Sprite _roundedSprite;
        private static Sprite MakeRoundedSprite()
        {
            if (_roundedSprite != null) return _roundedSprite;
            const int size   = 48;
            const int radius = 12;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode   = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int cx = x, cy = y;
                    bool inside = true;
                    if      (x < radius          && y < radius)          { cx = radius;          cy = radius;          inside = (x-cx)*(x-cx)+(y-cy)*(y-cy) <= radius*radius; }
                    else if (x >= size - radius   && y < radius)          { cx = size-radius-1;   cy = radius;          inside = (x-cx)*(x-cx)+(y-cy)*(y-cy) <= radius*radius; }
                    else if (x < radius           && y >= size - radius)  { cx = radius;          cy = size-radius-1;   inside = (x-cx)*(x-cx)+(y-cy)*(y-cy) <= radius*radius; }
                    else if (x >= size - radius   && y >= size - radius)  { cx = size-radius-1;   cy = size-radius-1;   inside = (x-cx)*(x-cx)+(y-cy)*(y-cy) <= radius*radius; }
                    tex.SetPixel(x, y, inside ? Color.white : new Color(0,0,0,0));
                }
            }
            tex.Apply();
            _roundedSprite = Sprite.Create(tex, new Rect(0,0,size,size), new Vector2(0.5f,0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
            return _roundedSprite;
        }
    }
}
