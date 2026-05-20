using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TransportManager.Core;
using TransportManager.Events;
using TransportManager.Systems.Map.Geocoding;
using TransportManager.Systems.Tutorial;

namespace TransportManager.UI.Tutorial
{
    public class IntroDialogueView : MonoBehaviour, IPointerClickHandler
    {
        private enum LineKind { Text, AskName, AskLocation }

        private struct DialogueLine
        {
            public string text;
            public LineKind kind;
            public int secretaryIndex; // 0 or 1
            public DialogueLine(string t, LineKind k, int s) { text = t; kind = k; secretaryIndex = s; }
        }

        private static readonly Color ScrimColor = new Color(0f, 0f, 0f, 0.78f);
        private static readonly Color BubbleColor = new Color(0x1B / 255f, 0x22 / 255f, 0x28 / 255f, 0.96f);
        private static readonly Color AccentColor = new Color(1f, 0.85f, 0.30f);

        private readonly List<DialogueLine> _lines = new List<DialogueLine>();
        private int _index;

        private Image _scrim;
        private Image _secretaryImg;
        private RectTransform _bubbleRt;
        private TMP_Text _titleLabel;
        private TMP_Text _messageLabel;
        private TMP_InputField _inputField;
        private TMP_Text _hintLabel;
        private Button _nextBtn;
        private TMP_Text _nextBtnLabel;

        private Sprite _secretary0;
        private Sprite _secretary1;

        private void Awake()
        {
            BuildScript();
            BuildUI();
            Show(_lines[0]);
        }

        private void BuildScript()
        {
            _lines.Clear();
            _lines.Add(new DialogueLine("Bonjour ! Je suis Élise, votre assistante personnelle.", LineKind.Text, 0));
            _lines.Add(new DialogueLine("Vous venez d'hériter d'une entreprise de transport… disons, qui a connu des jours meilleurs.", LineKind.Text, 1));
            _lines.Add(new DialogueLine("Mais ne vous inquiétez pas, ensemble nous allons la remettre sur pieds !", LineKind.Text, 0));
            _lines.Add(new DialogueLine("Avant toute chose, comment souhaitez-vous appeler cette nouvelle entreprise ?", LineKind.AskName, 0));
            _lines.Add(new DialogueLine("« {name} » — quel beau nom ! Ça sonne bien.", LineKind.Text, 1));
            _lines.Add(new DialogueLine("Où installez-vous votre dépôt ? Indiquez-moi une ville ou une adresse.", LineKind.AskLocation, 0));
            _lines.Add(new DialogueLine("Parfait, {location}. C'est de là que nous lancerons nos premiers contrats.", LineKind.Text, 1));
            _lines.Add(new DialogueLine("Le bâtiment est en ruine, alors retroussons nos manches. Suivez-moi !", LineKind.Text, 0));
        }

        private void BuildUI()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 8000;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            // Scrim covers full screen and receives clicks to advance
            var scrimGo = new GameObject("Scrim", typeof(RectTransform));
            scrimGo.transform.SetParent(transform, false);
            _scrim = scrimGo.AddComponent<Image>();
            _scrim.color = ScrimColor;
            _scrim.raycastTarget = true;
            StretchFull(scrimGo.GetComponent<RectTransform>());

            // Secretary on the right side — fixed size container so swapping
            // between secretary01 / secretary02 doesn't change the displayed size
            // (the two PNG can have different native dimensions).
            _secretary0 = LoadSpriteFlexible("UI/secretary01");
            _secretary1 = LoadSpriteFlexible("UI/secretary02");

            var secGo = new GameObject("Secretary", typeof(RectTransform));
            secGo.transform.SetParent(transform, false);
            var secRt = secGo.GetComponent<RectTransform>();
            secRt.anchorMin = new Vector2(1, 0);
            secRt.anchorMax = new Vector2(1, 0);
            secRt.pivot = new Vector2(1, 0);
            secRt.sizeDelta = new Vector2(640, 1040);
            secRt.anchoredPosition = new Vector2(-40, 0);

            _secretaryImg = secGo.AddComponent<Image>();
            _secretaryImg.preserveAspect = true;
            _secretaryImg.raycastTarget = false;
            _secretaryImg.sprite = _secretary0;

            // Bubble — bottom left/center
            var bubbleGo = new GameObject("Bubble", typeof(RectTransform));
            bubbleGo.transform.SetParent(transform, false);
            var bubbleImg = bubbleGo.AddComponent<Image>();
            bubbleImg.color = BubbleColor;
            bubbleImg.sprite = MakeRounded();
            bubbleImg.type = Image.Type.Sliced;
            bubbleImg.raycastTarget = true;
            _bubbleRt = bubbleGo.GetComponent<RectTransform>();
            _bubbleRt.anchorMin = new Vector2(0, 0);
            _bubbleRt.anchorMax = new Vector2(1, 0);
            _bubbleRt.pivot = new Vector2(0.5f, 0);
            _bubbleRt.offsetMin = new Vector2(60, 60);
            _bubbleRt.offsetMax = new Vector2(-760, 380);

            // Title (Élise)
            var titleGo = new GameObject("Title", typeof(RectTransform));
            titleGo.transform.SetParent(_bubbleRt, false);
            _titleLabel = titleGo.AddComponent<TextMeshProUGUI>();
            _titleLabel.text = "Élise";
            _titleLabel.fontSize = 20;
            _titleLabel.fontStyle = FontStyles.Bold;
            _titleLabel.color = AccentColor;
            _titleLabel.alignment = TextAlignmentOptions.TopLeft;
            _titleLabel.raycastTarget = false;
            var titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0, 1);
            titleRt.anchorMax = new Vector2(1, 1);
            titleRt.pivot = new Vector2(0.5f, 1);
            titleRt.offsetMin = new Vector2(28, -52);
            titleRt.offsetMax = new Vector2(-28, -16);

            // Message
            var msgGo = new GameObject("Message", typeof(RectTransform));
            msgGo.transform.SetParent(_bubbleRt, false);
            _messageLabel = msgGo.AddComponent<TextMeshProUGUI>();
            _messageLabel.fontSize = 18;
            _messageLabel.color = new Color(0.92f, 0.93f, 0.95f);
            _messageLabel.alignment = TextAlignmentOptions.TopLeft;
            _messageLabel.enableWordWrapping = true;
            _messageLabel.raycastTarget = false;
            var msgRt = msgGo.GetComponent<RectTransform>();
            msgRt.anchorMin = new Vector2(0, 0);
            msgRt.anchorMax = new Vector2(1, 1);
            msgRt.offsetMin = new Vector2(28, 100);
            msgRt.offsetMax = new Vector2(-28, -58);

            // Input field (hidden by default)
            var inputGo = new GameObject("Input", typeof(RectTransform));
            inputGo.transform.SetParent(_bubbleRt, false);
            var inputImg = inputGo.AddComponent<Image>();
            inputImg.color = new Color(1f, 1f, 1f, 0.08f);
            inputImg.sprite = MakeRounded();
            inputImg.type = Image.Type.Sliced;
            var inputRt = inputGo.GetComponent<RectTransform>();
            inputRt.anchorMin = new Vector2(0, 0);
            inputRt.anchorMax = new Vector2(1, 0);
            inputRt.pivot = new Vector2(0.5f, 0);
            inputRt.offsetMin = new Vector2(28, 14);
            inputRt.offsetMax = new Vector2(-180, 64);

            _inputField = inputGo.AddComponent<TMP_InputField>();
            _inputField.targetGraphic = inputImg;
            _inputField.textViewport = inputRt;

            var inputTextGo = new GameObject("Text", typeof(RectTransform));
            inputTextGo.transform.SetParent(inputGo.transform, false);
            var inputTmp = inputTextGo.AddComponent<TextMeshProUGUI>();
            inputTmp.fontSize = 18;
            inputTmp.color = Color.white;
            inputTmp.alignment = TextAlignmentOptions.MidlineLeft;
            var inputTextRt = inputTextGo.GetComponent<RectTransform>();
            inputTextRt.anchorMin = Vector2.zero;
            inputTextRt.anchorMax = Vector2.one;
            inputTextRt.offsetMin = new Vector2(16, 6);
            inputTextRt.offsetMax = new Vector2(-16, -6);
            _inputField.textComponent = inputTmp;

            var phGo = new GameObject("Placeholder", typeof(RectTransform));
            phGo.transform.SetParent(inputGo.transform, false);
            var phTmp = phGo.AddComponent<TextMeshProUGUI>();
            phTmp.fontSize = 18;
            phTmp.color = new Color(1f, 1f, 1f, 0.35f);
            phTmp.alignment = TextAlignmentOptions.MidlineLeft;
            var phRt = phGo.GetComponent<RectTransform>();
            phRt.anchorMin = Vector2.zero;
            phRt.anchorMax = Vector2.one;
            phRt.offsetMin = new Vector2(16, 6);
            phRt.offsetMax = new Vector2(-16, -6);
            _inputField.placeholder = phTmp;

            // Hint label below input (e.g. "Appuyez sur Entrée pour valider")
            var hintGo = new GameObject("Hint", typeof(RectTransform));
            hintGo.transform.SetParent(_bubbleRt, false);
            _hintLabel = hintGo.AddComponent<TextMeshProUGUI>();
            _hintLabel.fontSize = 12;
            _hintLabel.color = new Color(0.55f, 0.60f, 0.68f);
            _hintLabel.alignment = TextAlignmentOptions.MidlineLeft;
            _hintLabel.raycastTarget = false;
            var hintRt = hintGo.GetComponent<RectTransform>();
            hintRt.anchorMin = new Vector2(0, 0);
            hintRt.anchorMax = new Vector2(1, 0);
            hintRt.pivot = new Vector2(0, 0);
            hintRt.offsetMin = new Vector2(28, 64);
            hintRt.offsetMax = new Vector2(-28, 84);

            // Next button (bottom right of bubble)
            var btnGo = new GameObject("Next", typeof(RectTransform));
            btnGo.transform.SetParent(_bubbleRt, false);
            var btnImg = btnGo.AddComponent<Image>();
            btnImg.color = AccentColor;
            btnImg.sprite = MakeRounded();
            btnImg.type = Image.Type.Sliced;
            _nextBtn = btnGo.AddComponent<Button>();
            _nextBtn.targetGraphic = btnImg;
            _nextBtn.transition = Selectable.Transition.None;
            _nextBtn.onClick.AddListener(OnNextClicked);
            var btnRt = btnGo.GetComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(1, 0);
            btnRt.anchorMax = new Vector2(1, 0);
            btnRt.pivot = new Vector2(1, 0);
            btnRt.sizeDelta = new Vector2(150, 50);
            btnRt.anchoredPosition = new Vector2(-28, 14);

            var btnLblGo = new GameObject("Lbl", typeof(RectTransform));
            btnLblGo.transform.SetParent(btnRt, false);
            _nextBtnLabel = btnLblGo.AddComponent<TextMeshProUGUI>();
            _nextBtnLabel.text = "Continuer";
            _nextBtnLabel.fontSize = 16;
            _nextBtnLabel.fontStyle = FontStyles.Bold;
            _nextBtnLabel.color = new Color(0.10f, 0.12f, 0.14f);
            _nextBtnLabel.alignment = TextAlignmentOptions.Center;
            _nextBtnLabel.raycastTarget = false;
            var btnLblRt = btnLblGo.GetComponent<RectTransform>();
            btnLblRt.anchorMin = Vector2.zero;
            btnLblRt.anchorMax = Vector2.one;
            btnLblRt.offsetMin = Vector2.zero;
            btnLblRt.offsetMax = Vector2.zero;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // Only advance via scrim click; bubble clicks are absorbed by its own raycast
            // Avoid advancing while an input is focused
            if (eventData.pointerCurrentRaycast.gameObject == _scrim.gameObject)
                TryAdvance();
        }

        private void OnNextClicked() => TryAdvance();

        private void TryAdvance()
        {
            var current = _lines[_index];
            if (current.kind == LineKind.AskName)
            {
                var name = _inputField.text?.Trim();
                if (string.IsNullOrWhiteSpace(name)) { _inputField.Select(); return; }
                var gm = GameManager.Instance;
                if (gm != null && gm.Save != null)
                {
                    gm.Save.company.companyName = name;
                    gm.Save.company.createdAtUtcTicks = DateTime.UtcNow.Ticks;
                }
            }
            else if (current.kind == LineKind.AskLocation)
            {
                var loc = _inputField.text?.Trim();
                if (string.IsNullOrWhiteSpace(loc)) { _inputField.Select(); return; }
                var gm = GameManager.Instance;
                if (gm != null && gm.Save != null)
                {
                    gm.Save.company.location = loc;
                    gm.Save.company.hasLocationCoordinates = false;
                    StartCoroutine(GeocodeLocation(loc));
                }
            }

            _index++;
            if (_index >= _lines.Count) { Finish(); return; }
            Show(_lines[_index]);
        }

        private IEnumerator GeocodeLocation(string address)
        {
            yield return NominatimGeocoder.Geocode(address, result =>
            {
                var gm = GameManager.Instance;
                if (gm == null || gm.Save == null) return;
                if (!result.success) return;
                gm.Save.company.locationLatitude = result.latitude;
                gm.Save.company.locationLongitude = result.longitude;
                gm.Save.company.hasLocationCoordinates = true;
                if (!string.IsNullOrEmpty(result.displayName))
                    gm.Save.company.location = result.displayName;
                gm.SaveNow();
                GameEvents.RaiseCompanyProfileChanged();
            });
        }

        private void Show(DialogueLine line)
        {
            var gm = GameManager.Instance;
            var name = gm?.Save?.company?.companyName ?? "votre entreprise";
            var loc = gm?.Save?.company?.location ?? "votre ville";
            var text = line.text.Replace("{name}", name).Replace("{location}", loc);

            _messageLabel.text = text;
            _secretaryImg.sprite = line.secretaryIndex == 1 ? _secretary1 : _secretary0;

            bool needsInput = line.kind != LineKind.Text;
            _inputField.gameObject.SetActive(needsInput);
            _hintLabel.gameObject.SetActive(needsInput);

            if (needsInput)
            {
                _inputField.text = "";
                _hintLabel.text = "Saisissez votre réponse puis cliquez sur Valider.";
                _nextBtnLabel.text = "Valider";
                if (line.kind == LineKind.AskName)
                    ((TMP_Text)_inputField.placeholder).text = "Ex. Logistik Express";
                else
                    ((TMP_Text)_inputField.placeholder).text = "Ex. Paris, France";
                _inputField.Select();
                _inputField.ActivateInputField();
            }
            else
            {
                _hintLabel.text = "Cliquez n'importe où ou sur Continuer.";
                _nextBtnLabel.text = _index == _lines.Count - 1 ? "Commencer" : "Continuer";
            }
        }

        private void Finish()
        {
            var gm = GameManager.Instance;
            if (gm != null) gm.SaveNow();
            GameEvents.RaiseCompanyProfileChanged();
            GameEvents.RaiseCompanyCreated();
            Destroy(gameObject);
        }

        private static Sprite LoadSpriteFlexible(string path)
        {
            var s = Resources.Load<Sprite>(path);
            if (s != null) return s;
            var t = Resources.Load<Texture2D>(path);
            return t != null ? Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f)) : null;
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static Sprite _roundedSprite;
        private static Sprite MakeRounded()
        {
            if (_roundedSprite != null) return _roundedSprite;
            const int size = 48;
            const int radius = 14;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool inside = true;
                    int cx = x, cy = y;
                    if (x < radius && y < radius) { cx = radius; cy = radius; inside = (x - cx) * (x - cx) + (y - cy) * (y - cy) <= radius * radius; }
                    else if (x >= size - radius && y < radius) { cx = size - radius - 1; cy = radius; inside = (x - cx) * (x - cx) + (y - cy) * (y - cy) <= radius * radius; }
                    else if (x < radius && y >= size - radius) { cx = radius; cy = size - radius - 1; inside = (x - cx) * (x - cx) + (y - cy) * (y - cy) <= radius * radius; }
                    else if (x >= size - radius && y >= size - radius) { cx = size - radius - 1; cy = size - radius - 1; inside = (x - cx) * (x - cx) + (y - cy) * (y - cy) <= radius * radius; }
                    tex.SetPixel(x, y, inside ? Color.white : new Color(0, 0, 0, 0));
                }
            }
            tex.Apply();
            _roundedSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
            return _roundedSprite;
        }
    }
}
