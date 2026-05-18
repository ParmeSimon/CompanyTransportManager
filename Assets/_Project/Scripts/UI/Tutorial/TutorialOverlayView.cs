using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TransportManager.Core;
using TransportManager.Systems.Tutorial;

namespace TransportManager.UI.Tutorial
{
    public class TutorialOverlayView : MonoBehaviour
    {
        private static readonly Color HaloColor = new Color(1f, 0.85f, 0.30f, 1f);
        private static readonly Color BubbleBg = new Color(0x1B / 255f, 0x22 / 255f, 0x28 / 255f, 0.95f);

        private Canvas _canvas;
        private CanvasGroup _canvasGroup;
        private RectTransform _haloRt;
        private Image _haloImg;
        private RectTransform _bubbleRt;
        private TMP_Text _titleLabel;
        private TMP_Text _messageLabel;
        private Button _nextButton;
        private TutorialSystem _tutorial;
        private TutorialSystem.StepData _currentStep;

        private void Awake() => BuildUI();

        private void Start()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Tutorial == null) { gameObject.SetActive(false); return; }
            _tutorial = gm.Tutorial;
            _tutorial.OnStepChanged += OnStepChanged;
            _tutorial.OnTutorialCompleted += OnCompleted;
            if (!_tutorial.IsActive) { gameObject.SetActive(false); return; }
            _tutorial.Start();
        }

        private void OnDestroy()
        {
            if (_tutorial != null)
            {
                _tutorial.OnStepChanged -= OnStepChanged;
                _tutorial.OnTutorialCompleted -= OnCompleted;
            }
        }

        private void BuildUI()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 5000;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(390, 844);
            scaler.matchWidthOrHeight = 0.5f;

            gameObject.AddComponent<GraphicRaycaster>();
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();

            // Halo (above target)
            var haloGo = new GameObject("Halo", typeof(RectTransform));
            haloGo.transform.SetParent(transform, false);
            _haloImg = haloGo.AddComponent<Image>();
            _haloImg.color = HaloColor;
            _haloImg.sprite = MakeRingSprite();
            _haloImg.raycastTarget = false;
            _haloRt = haloGo.GetComponent<RectTransform>();
            _haloRt.sizeDelta = new Vector2(120, 120);
            _haloRt.anchoredPosition = Vector2.zero;

            // Bubble container (bottom)
            var bubbleGo = new GameObject("Bubble", typeof(RectTransform));
            bubbleGo.transform.SetParent(transform, false);
            var bubbleImg = bubbleGo.AddComponent<Image>();
            bubbleImg.color = BubbleBg;
            bubbleImg.sprite = MakeRoundedSprite();
            bubbleImg.type = Image.Type.Sliced;
            _bubbleRt = bubbleGo.GetComponent<RectTransform>();
            _bubbleRt.anchorMin = new Vector2(0, 0);
            _bubbleRt.anchorMax = new Vector2(1, 0);
            _bubbleRt.pivot = new Vector2(0.5f, 0);
            _bubbleRt.offsetMin = new Vector2(12, 90);
            _bubbleRt.offsetMax = new Vector2(-12, 240);

            // Title
            var titleGo = new GameObject("Title", typeof(RectTransform));
            titleGo.transform.SetParent(_bubbleRt, false);
            _titleLabel = titleGo.AddComponent<TextMeshProUGUI>();
            _titleLabel.fontSize = 14;
            _titleLabel.fontStyle = FontStyles.Bold;
            _titleLabel.color = new Color(1f, 0.85f, 0.30f);
            _titleLabel.alignment = TextAlignmentOptions.TopLeft;
            _titleLabel.raycastTarget = false;
            var titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0, 1);
            titleRt.anchorMax = new Vector2(1, 1);
            titleRt.pivot = new Vector2(0.5f, 1);
            titleRt.offsetMin = new Vector2(14, -34);
            titleRt.offsetMax = new Vector2(-14, -8);

            // Message
            var msgGo = new GameObject("Message", typeof(RectTransform));
            msgGo.transform.SetParent(_bubbleRt, false);
            _messageLabel = msgGo.AddComponent<TextMeshProUGUI>();
            _messageLabel.fontSize = 12;
            _messageLabel.color = new Color(0.88f, 0.90f, 0.94f);
            _messageLabel.alignment = TextAlignmentOptions.TopLeft;
            _messageLabel.enableWordWrapping = true;
            _messageLabel.raycastTarget = false;
            var msgRt = msgGo.GetComponent<RectTransform>();
            msgRt.anchorMin = new Vector2(0, 0);
            msgRt.anchorMax = new Vector2(1, 1);
            msgRt.offsetMin = new Vector2(14, 44);
            msgRt.offsetMax = new Vector2(-14, -38);

            // Next button
            var btnGo = new GameObject("NextBtn", typeof(RectTransform));
            btnGo.transform.SetParent(_bubbleRt, false);
            var btnImg = btnGo.AddComponent<Image>();
            btnImg.color = new Color(0.30f, 0.75f, 1f);
            btnImg.sprite = MakeRoundedSprite();
            btnImg.type = Image.Type.Sliced;
            _nextButton = btnGo.AddComponent<Button>();
            _nextButton.targetGraphic = btnImg;
            _nextButton.transition = Selectable.Transition.None;
            _nextButton.onClick.AddListener(OnNextClicked);
            var btnRt = btnGo.GetComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(1, 0);
            btnRt.anchorMax = new Vector2(1, 0);
            btnRt.pivot = new Vector2(1, 0);
            btnRt.sizeDelta = new Vector2(88, 32);
            btnRt.anchoredPosition = new Vector2(-10, 8);
            var btnLblGo = new GameObject("Lbl", typeof(RectTransform));
            btnLblGo.transform.SetParent(btnRt, false);
            var btnLbl = btnLblGo.AddComponent<TextMeshProUGUI>();
            btnLbl.text = "OK";
            btnLbl.fontSize = 13;
            btnLbl.fontStyle = FontStyles.Bold;
            btnLbl.color = new Color(0.05f, 0.10f, 0.15f);
            btnLbl.alignment = TextAlignmentOptions.Center;
            btnLbl.raycastTarget = false;
            var btnLblRt = btnLblGo.GetComponent<RectTransform>();
            btnLblRt.anchorMin = Vector2.zero;
            btnLblRt.anchorMax = Vector2.one;
            btnLblRt.offsetMin = Vector2.zero;
            btnLblRt.offsetMax = Vector2.zero;
        }

        private void OnStepChanged(TutorialSystem.StepData step)
        {
            _currentStep = step;
            gameObject.SetActive(true);
            _titleLabel.text = step.advisorTitle;
            _messageLabel.text = step.advisorMessage;
            UpdateHaloPosition();
        }

        private void OnCompleted()
        {
            gameObject.SetActive(false);
        }

        private void OnNextClicked()
        {
            // Auto-advance only used for purely informational steps.
            // Action-based steps advance via GameEvents listeners (see TutorialDriver).
            if (_currentStep == null) return;
            if (_currentStep.id == TutorialStep.CompanyCreate) return; // requires form
            _tutorial.Advance(_currentStep.id);
        }

        private void Update()
        {
            UpdateHaloPosition();
            if (_haloImg != null)
            {
                float t = Mathf.PingPong(Time.unscaledTime * 1.2f, 1f);
                float scale = Mathf.Lerp(0.85f, 1.15f, t);
                _haloRt.localScale = new Vector3(scale, scale, 1f);
                var c = HaloColor;
                c.a = Mathf.Lerp(0.45f, 0.95f, t);
                _haloImg.color = c;
            }
        }

        private void UpdateHaloPosition()
        {
            if (_currentStep == null || _haloRt == null) return;
            var target = TutorialTargetRegistry.Get(_currentStep.highlightTarget);
            if (target == null)
            {
                _haloImg.enabled = false;
                return;
            }
            _haloImg.enabled = true;

            var targetCorners = new Vector3[4];
            target.GetWorldCorners(targetCorners);
            var center = (targetCorners[0] + targetCorners[2]) * 0.5f;
            var size = new Vector2(targetCorners[2].x - targetCorners[0].x, targetCorners[2].y - targetCorners[0].y);
            _haloRt.position = center;
            _haloRt.sizeDelta = size * 1.25f;
        }

        private static Sprite _ringSprite;
        private static Sprite MakeRingSprite()
        {
            if (_ringSprite != null) return _ringSprite;
            const int size = 128;
            float outer = size * 0.5f;
            float inner = outer * 0.78f;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            float cx = (size - 1) * 0.5f;
            float cy = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - cx;
                    float dy = y - cy;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = 0f;
                    if (d <= outer && d >= inner) a = 1f;
                    else if (d < inner && d >= inner - 4) a = Mathf.Clamp01((d - (inner - 4)) / 4f);
                    else if (d > outer && d <= outer + 2) a = Mathf.Clamp01(1f - (d - outer) / 2f);
                    tex.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            }
            tex.Apply();
            _ringSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return _ringSprite;
        }

        private static Sprite _roundedSprite;
        private static Sprite MakeRoundedSprite()
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
