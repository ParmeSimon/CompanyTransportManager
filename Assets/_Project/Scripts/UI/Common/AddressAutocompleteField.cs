using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TransportManager.Systems.Map.Geocoding;

namespace TransportManager.UI.Common
{
    /// <summary>
    /// Reusable address autocomplete field.
    /// Build() wires the input + dropdown into the supplied parent RectTransform.
    /// The dropdown opens ABOVE the input (suitable for bottom-anchored bubbles).
    /// </summary>
    public class AddressAutocompleteField : MonoBehaviour
    {
        // ── Public API ────────────────────────────────────────────────────────────
        public Action<string, double, double> OnSelected; // (displayName, lat, lon)

        public bool   HasSelection  { get; private set; }
        public string SelectedName  { get; private set; }
        public double SelectedLat   { get; private set; }
        public double SelectedLon   { get; private set; }
        public string InputText     => _input != null ? _input.text : "";

        public void ClearSelection()
        {
            HasSelection = false;
            if (_input != null) _input.text = "";
            HideDropdown();
        }

        // ── Config ────────────────────────────────────────────────────────────────
        private const float RowHeight    = 36f;
        private const int   MaxSuggests  = 5;
        private const float DebounceTime = 0.45f;
        private const int   MinQueryLen  = 3;

        private static readonly Color InputBg   = new Color(1f, 1f, 1f, 0.08f);
        private static readonly Color DropBg    = new Color(0.12f, 0.16f, 0.22f, 0.97f);
        private static readonly Color RowHover  = new Color(0.22f, 0.45f, 0.80f, 0.85f);
        private static readonly Color RowNormal = new Color(0f, 0f, 0f, 0f);

        // ── State ─────────────────────────────────────────────────────────────────
        private TMP_InputField _input;
        private RectTransform  _dropRt;
        private readonly List<(Image bg, TMP_Text lbl, Button btn)> _rows =
            new List<(Image, TMP_Text, Button)>();
        private string    _lastTyped;
        private Coroutine _debounce;
        private bool      _suppressCallback;

        // ── Build ─────────────────────────────────────────────────────────────────
        /// <param name="parent">Container inside which the input + dropdown are placed.</param>
        /// <param name="inputOffsetMin">offsetMin of the input relative to parent.</param>
        /// <param name="inputOffsetMax">offsetMax of the input relative to parent.</param>
        /// <param name="dropdownHeight">Total pixel height of the dropdown panel.</param>
        /// <param name="placeholder">Placeholder text.</param>
        /// <param name="roundedSprite">Shared sliced sprite for backgrounds.</param>
        public void Build(
            RectTransform parent,
            Vector2 inputOffsetMin,
            Vector2 inputOffsetMax,
            float   dropdownHeight,
            string  placeholder,
            Sprite  roundedSprite)
        {
            BuildInput(parent, inputOffsetMin, inputOffsetMax, placeholder, roundedSprite);
            BuildDropdown(parent, inputOffsetMax.y, dropdownHeight, roundedSprite);
        }

        // ── Internal builders ─────────────────────────────────────────────────────
        private void BuildInput(RectTransform parent, Vector2 offMin, Vector2 offMax,
                                string placeholder, Sprite rounded)
        {
            var go = new GameObject("AutoInput", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var bg = go.AddComponent<Image>();
            bg.color  = InputBg;
            bg.sprite = rounded;
            bg.type   = Image.Type.Sliced;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot     = new Vector2(0.5f, 0f);
            rt.offsetMin = offMin;
            rt.offsetMax = offMax;

            _input = go.AddComponent<TMP_InputField>();
            _input.targetGraphic = bg;

            var vpGo = new GameObject("Viewport", typeof(RectTransform));
            vpGo.transform.SetParent(go.transform, false);
            vpGo.AddComponent<RectMask2D>();
            var vpRt = vpGo.GetComponent<RectTransform>();
            vpRt.anchorMin = Vector2.zero;
            vpRt.anchorMax = Vector2.one;
            vpRt.offsetMin = new Vector2(14f, 4f);
            vpRt.offsetMax = new Vector2(-14f, -4f);

            var phGo = new GameObject("Placeholder", typeof(RectTransform));
            phGo.transform.SetParent(vpGo.transform, false);
            var phTmp = phGo.AddComponent<TextMeshProUGUI>();
            phTmp.text          = placeholder;
            phTmp.color         = new Color(1f, 1f, 1f, 0.35f);
            phTmp.fontSize      = 16f;
            phTmp.alignment     = TextAlignmentOptions.MidlineLeft;
            phTmp.raycastTarget = false;
            Stretch(phGo.GetComponent<RectTransform>());

            var txtGo = new GameObject("Text", typeof(RectTransform));
            txtGo.transform.SetParent(vpGo.transform, false);
            var txtTmp = txtGo.AddComponent<TextMeshProUGUI>();
            txtTmp.color     = Color.white;
            txtTmp.fontSize  = 16f;
            txtTmp.alignment = TextAlignmentOptions.MidlineLeft;
            Stretch(txtGo.GetComponent<RectTransform>());

            _input.textViewport = vpRt;
            _input.textComponent = txtTmp;
            _input.placeholder   = phTmp;
            _input.onValueChanged.AddListener(OnInputChanged);
        }

        private void BuildDropdown(RectTransform parent, float inputTop, float height, Sprite rounded)
        {
            var go = new GameObject("AutoDrop", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var bg = go.AddComponent<Image>();
            bg.color  = DropBg;
            bg.sprite = rounded;
            bg.type   = Image.Type.Sliced;
            _dropRt = go.GetComponent<RectTransform>();
            _dropRt.anchorMin = new Vector2(0f, 0f);
            _dropRt.anchorMax = new Vector2(1f, 0f);
            _dropRt.pivot     = new Vector2(0.5f, 0f);
            _dropRt.offsetMin = new Vector2(28f, inputTop + 4f);
            _dropRt.offsetMax = new Vector2(-28f, inputTop + 4f + height);
            go.SetActive(false);

            // Pre-build rows
            for (int i = 0; i < MaxSuggests; i++)
            {
                int idx = i;
                float y = height - (i + 1) * RowHeight;

                var rowGo = new GameObject($"Row{i}", typeof(RectTransform));
                rowGo.transform.SetParent(go.transform, false);
                var rowBg = rowGo.AddComponent<Image>();
                rowBg.color = RowNormal;
                var rowRt = rowGo.GetComponent<RectTransform>();
                rowRt.anchorMin        = new Vector2(0f, 0f);
                rowRt.anchorMax        = new Vector2(1f, 0f);
                rowRt.pivot            = new Vector2(0f, 0f);
                rowRt.offsetMin        = new Vector2(0f, y);
                rowRt.offsetMax        = new Vector2(0f, y + RowHeight);
                rowRt.anchoredPosition = new Vector2(0f, y);

                var rowBtn = rowGo.AddComponent<Button>();
                rowBtn.targetGraphic = rowBg;
                rowBtn.transition    = Selectable.Transition.None;
                rowBtn.onClick.AddListener(() => OnRowClicked(idx));

                var lblGo = new GameObject("Lbl", typeof(RectTransform));
                lblGo.transform.SetParent(rowGo.transform, false);
                var lbl = lblGo.AddComponent<TextMeshProUGUI>();
                lbl.fontSize      = 13f;
                lbl.color         = new Color(0.88f, 0.90f, 0.94f);
                lbl.alignment     = TextAlignmentOptions.MidlineLeft;
                lbl.textWrappingMode = TextWrappingModes.NoWrap;
                lbl.overflowMode  = TextOverflowModes.Ellipsis;
                lbl.raycastTarget = false;
                var lblRt = lblGo.GetComponent<RectTransform>();
                lblRt.anchorMin = Vector2.zero;
                lblRt.anchorMax = Vector2.one;
                lblRt.offsetMin = new Vector2(12f, 0f);
                lblRt.offsetMax = new Vector2(-12f, 0f);

                // Separator line
                if (i < MaxSuggests - 1)
                {
                    var sep = new GameObject("Sep", typeof(RectTransform));
                    sep.transform.SetParent(rowGo.transform, false);
                    var sepImg = sep.AddComponent<Image>();
                    sepImg.color        = new Color(1f, 1f, 1f, 0.06f);
                    sepImg.raycastTarget = false;
                    var sepRt = sep.GetComponent<RectTransform>();
                    sepRt.anchorMin = new Vector2(0f, 0f);
                    sepRt.anchorMax = new Vector2(1f, 0f);
                    sepRt.pivot     = new Vector2(0.5f, 0f);
                    sepRt.sizeDelta = new Vector2(0f, 1f);
                    sepRt.anchoredPosition = Vector2.zero;
                }

                _rows.Add((rowBg, lbl, rowBtn));
                rowGo.SetActive(false);
            }
        }

        // ── Input handling ────────────────────────────────────────────────────────
        private void OnInputChanged(string text)
        {
            if (_suppressCallback) return;
            HasSelection = false;
            _lastTyped = text;
            if (_debounce != null) StopCoroutine(_debounce);
            if (text.Length >= MinQueryLen)
                _debounce = StartCoroutine(DebouncedSearch(text));
            else
                HideDropdown();
        }

        private IEnumerator DebouncedSearch(string query)
        {
            yield return new WaitForSecondsRealtime(DebounceTime);
            if (_lastTyped != query) yield break;
            yield return NominatimGeocoder.Suggest(query, MaxSuggests, OnSuggestionsReceived);
        }

        private void OnSuggestionsReceived(List<NominatimGeocoder.SuggestResult> results)
        {
            if (results == null || results.Count == 0 || _lastTyped != _input?.text)
            { HideDropdown(); return; }
            ShowDropdown(results);
        }

        // ── Dropdown display ──────────────────────────────────────────────────────
        private List<NominatimGeocoder.SuggestResult> _pendingResults;

        private void ShowDropdown(List<NominatimGeocoder.SuggestResult> results)
        {
            _pendingResults = results;
            int count = Mathf.Min(results.Count, MaxSuggests);
            float totalH = count * RowHeight;

            // Resize dropdown height
            var offMin = _dropRt.offsetMin;
            _dropRt.offsetMax = new Vector2(_dropRt.offsetMax.x, offMin.y + totalH);
            _dropRt.gameObject.SetActive(true);

            for (int i = 0; i < MaxSuggests; i++)
            {
                bool active = i < count;
                _rows[i].bg.gameObject.SetActive(active);
                if (active)
                {
                    _rows[i].lbl.text = results[i].displayName;
                    _rows[i].bg.color = RowNormal;
                }
            }
        }

        private void HideDropdown()
        {
            if (_dropRt != null) _dropRt.gameObject.SetActive(false);
            _pendingResults = null;
        }

        private void OnRowClicked(int idx)
        {
            if (_pendingResults == null || idx >= _pendingResults.Count) return;
            var r = _pendingResults[idx];

            _suppressCallback = true;
            _input.text = r.displayName;
            _suppressCallback = false;

            HasSelection    = true;
            SelectedName    = r.displayName;
            SelectedLat     = r.latitude;
            SelectedLon     = r.longitude;
            HideDropdown();
            OnSelected?.Invoke(r.displayName, r.latitude, r.longitude);
        }

        // ── Focus management ──────────────────────────────────────────────────────
        public void Select() { _input?.Select(); _input?.ActivateInputField(); }

        public void SetPlaceholder(string text)
        {
            if (_input?.placeholder is TMP_Text t) t.text = text;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
