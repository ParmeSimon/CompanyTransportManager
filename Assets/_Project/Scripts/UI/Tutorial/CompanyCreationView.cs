using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TransportManager.Core;
using TransportManager.Events;
using TransportManager.Systems.Tutorial;

namespace TransportManager.UI.Tutorial
{
    public class CompanyCreationView : MonoBehaviour
    {
        private static readonly Color Bg = new Color(0x1B / 255f, 0x22 / 255f, 0x28 / 255f, 1f);
        private static readonly Color Accent = new Color(0.30f, 0.75f, 1f);

        private TMP_InputField _nameInput;
        private Button _createBtn;
        private CanvasGroup _canvasGroup;

        private void Awake() => Build();

        private void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 7000;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(390, 844);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();

            // Background fullscreen
            var bgGo = new GameObject("Bg", typeof(RectTransform));
            bgGo.transform.SetParent(transform, false);
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.color = Bg;
            StretchFull(bgGo.GetComponent<RectTransform>());

            // Title
            var titleGo = new GameObject("Title", typeof(RectTransform));
            titleGo.transform.SetParent(transform, false);
            var title = titleGo.AddComponent<TextMeshProUGUI>();
            title.text = "Créer votre entreprise";
            title.fontSize = 22;
            title.fontStyle = FontStyles.Bold;
            title.color = Color.white;
            title.alignment = TextAlignmentOptions.Center;
            var titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0, 1);
            titleRt.anchorMax = new Vector2(1, 1);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.offsetMin = new Vector2(20, -160);
            titleRt.offsetMax = new Vector2(-20, -100);

            // Subtitle
            var subGo = new GameObject("Sub", typeof(RectTransform));
            subGo.transform.SetParent(transform, false);
            var sub = subGo.AddComponent<TextMeshProUGUI>();
            sub.text = "Donnez-lui un nom marquant.";
            sub.fontSize = 13;
            sub.color = new Color(0.78f, 0.82f, 0.88f);
            sub.alignment = TextAlignmentOptions.Center;
            var subRt = subGo.GetComponent<RectTransform>();
            subRt.anchorMin = new Vector2(0, 1);
            subRt.anchorMax = new Vector2(1, 1);
            subRt.pivot = new Vector2(0.5f, 1f);
            subRt.offsetMin = new Vector2(20, -200);
            subRt.offsetMax = new Vector2(-20, -160);

            // Input
            var inputGo = new GameObject("Input", typeof(RectTransform));
            inputGo.transform.SetParent(transform, false);
            var inputImg = inputGo.AddComponent<Image>();
            inputImg.color = new Color(1f, 1f, 1f, 0.08f);
            inputImg.sprite = MakeRounded();
            inputImg.type = Image.Type.Sliced;
            var inputRt = inputGo.GetComponent<RectTransform>();
            inputRt.anchorMin = new Vector2(0.5f, 0.5f);
            inputRt.anchorMax = new Vector2(0.5f, 0.5f);
            inputRt.sizeDelta = new Vector2(320, 48);
            inputRt.anchoredPosition = new Vector2(0, 40);

            var inputField = inputGo.AddComponent<TMP_InputField>();
            inputField.targetGraphic = inputImg;
            inputField.textViewport = inputRt;

            // Text component
            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(inputGo.transform, false);
            var textTmp = textGo.AddComponent<TextMeshProUGUI>();
            textTmp.fontSize = 16;
            textTmp.color = Color.white;
            textTmp.alignment = TextAlignmentOptions.MidlineLeft;
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(14, 6);
            textRt.offsetMax = new Vector2(-14, -6);
            inputField.textComponent = textTmp;

            // Placeholder
            var phGo = new GameObject("Placeholder", typeof(RectTransform));
            phGo.transform.SetParent(inputGo.transform, false);
            var phTmp = phGo.AddComponent<TextMeshProUGUI>();
            phTmp.text = "Ex. Logistik Express";
            phTmp.fontSize = 16;
            phTmp.color = new Color(1f, 1f, 1f, 0.35f);
            phTmp.alignment = TextAlignmentOptions.MidlineLeft;
            var phRt = phGo.GetComponent<RectTransform>();
            phRt.anchorMin = Vector2.zero;
            phRt.anchorMax = Vector2.one;
            phRt.offsetMin = new Vector2(14, 6);
            phRt.offsetMax = new Vector2(-14, -6);
            inputField.placeholder = phTmp;

            _nameInput = inputField;

            // Register as tutorial target so the halo points to it
            TutorialTargetRegistry.Register("ui:company_name_input", inputRt);

            // Create button
            var btnGo = new GameObject("Create", typeof(RectTransform));
            btnGo.transform.SetParent(transform, false);
            var btnImg = btnGo.AddComponent<Image>();
            btnImg.color = Accent;
            btnImg.sprite = MakeRounded();
            btnImg.type = Image.Type.Sliced;
            _createBtn = btnGo.AddComponent<Button>();
            _createBtn.targetGraphic = btnImg;
            _createBtn.transition = Selectable.Transition.None;
            _createBtn.onClick.AddListener(OnCreateClicked);

            var btnRt = btnGo.GetComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(0.5f, 0.5f);
            btnRt.anchorMax = new Vector2(0.5f, 0.5f);
            btnRt.sizeDelta = new Vector2(320, 48);
            btnRt.anchoredPosition = new Vector2(0, -30);

            var btnLblGo = new GameObject("Lbl", typeof(RectTransform));
            btnLblGo.transform.SetParent(btnGo.transform, false);
            var btnLbl = btnLblGo.AddComponent<TextMeshProUGUI>();
            btnLbl.text = "Créer mon entreprise";
            btnLbl.fontSize = 15;
            btnLbl.fontStyle = FontStyles.Bold;
            btnLbl.color = new Color(0.05f, 0.10f, 0.15f);
            btnLbl.alignment = TextAlignmentOptions.Center;
            var btnLblRt = btnLblGo.GetComponent<RectTransform>();
            btnLblRt.anchorMin = Vector2.zero;
            btnLblRt.anchorMax = Vector2.one;
            btnLblRt.offsetMin = Vector2.zero;
            btnLblRt.offsetMax = Vector2.zero;
        }

        private void OnCreateClicked()
        {
            var name = string.IsNullOrWhiteSpace(_nameInput.text) ? "Mon Entreprise" : _nameInput.text.Trim();
            var gm = GameManager.Instance;
            if (gm == null || gm.Save == null) return;
            gm.Save.company.companyName = name;
            gm.Save.company.createdAtUtcTicks = DateTime.UtcNow.Ticks;
            gm.SaveNow();
            TutorialTargetRegistry.Unregister("ui:company_name_input");
            GameEvents.RaiseCompanyCreated();
            Destroy(gameObject);
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
