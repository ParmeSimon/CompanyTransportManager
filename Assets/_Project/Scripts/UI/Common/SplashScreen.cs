using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TransportManager.Core;

namespace TransportManager.UI.Common
{
    public class SplashScreen : MonoBehaviour
    {
        private static readonly Color BackgroundColor = new Color(0x1B / 255f, 0x22 / 255f, 0x28 / 255f, 1f);
        private static readonly Color AccentColor = new Color(0.30f, 0.75f, 1f, 1f);
        private static readonly Color TextColor = new Color(0.78f, 0.82f, 0.88f, 1f);

        private CanvasGroup _canvasGroup;
        private Image _progressFill;
        private TMP_Text _statusLabel;
        private RectTransform _progressBarBg;

        private void Awake()
        {
            BuildUI();
        }

        private void Start()
        {
            StartCoroutine(RunLoadingSequence());
        }

        private void BuildUI()
        {
            // Root canvas with very high sort order so it covers everything
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(390, 844);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = 1f;
            _canvasGroup.blocksRaycasts = true;

            // Background fullscreen
            var bgGo = new GameObject("Background", typeof(RectTransform));
            bgGo.transform.SetParent(transform, false);
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.color = BackgroundColor;
            bgImg.raycastTarget = true;
            var bgRt = bgGo.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;

            // Logo centered (slightly above center)
            var logoSprite = LoadLogo();
            var logoGo = new GameObject("Logo", typeof(RectTransform));
            logoGo.transform.SetParent(transform, false);
            var logoImg = logoGo.AddComponent<Image>();
            logoImg.sprite = logoSprite;
            logoImg.preserveAspect = true;
            logoImg.raycastTarget = false;
            var logoRt = logoGo.GetComponent<RectTransform>();
            logoRt.anchorMin = new Vector2(0.5f, 0.5f);
            logoRt.anchorMax = new Vector2(0.5f, 0.5f);
            logoRt.pivot = new Vector2(0.5f, 0.5f);
            logoRt.sizeDelta = new Vector2(220, 220);
            logoRt.anchoredPosition = new Vector2(0, 60);

            // Status label (above progress bar)
            var statusGo = new GameObject("Status", typeof(RectTransform));
            statusGo.transform.SetParent(transform, false);
            _statusLabel = statusGo.AddComponent<TextMeshProUGUI>();
            _statusLabel.text = "Chargement...";
            _statusLabel.fontSize = 14;
            _statusLabel.fontStyle = FontStyles.Bold;
            _statusLabel.color = TextColor;
            _statusLabel.alignment = TextAlignmentOptions.Center;
            _statusLabel.raycastTarget = false;
            var statusRt = statusGo.GetComponent<RectTransform>();
            statusRt.anchorMin = new Vector2(0, 0);
            statusRt.anchorMax = new Vector2(1, 0);
            statusRt.pivot = new Vector2(0.5f, 0.5f);
            statusRt.sizeDelta = new Vector2(0, 24);
            statusRt.anchoredPosition = new Vector2(0, 160);

            // Progress bar background
            var progressBgGo = new GameObject("ProgressBg", typeof(RectTransform));
            progressBgGo.transform.SetParent(transform, false);
            var progressBgImg = progressBgGo.AddComponent<Image>();
            progressBgImg.color = new Color(1f, 1f, 1f, 0.10f);
            progressBgImg.sprite = MakeRoundedSprite();
            progressBgImg.type = Image.Type.Sliced;
            progressBgImg.raycastTarget = false;
            _progressBarBg = progressBgGo.GetComponent<RectTransform>();
            _progressBarBg.anchorMin = new Vector2(0.5f, 0);
            _progressBarBg.anchorMax = new Vector2(0.5f, 0);
            _progressBarBg.pivot = new Vector2(0.5f, 0.5f);
            _progressBarBg.sizeDelta = new Vector2(280, 10);
            _progressBarBg.anchoredPosition = new Vector2(0, 120);

            // Progress bar fill
            var fillGo = new GameObject("Fill", typeof(RectTransform));
            fillGo.transform.SetParent(progressBgGo.transform, false);
            _progressFill = fillGo.AddComponent<Image>();
            _progressFill.color = AccentColor;
            _progressFill.sprite = MakeRoundedSprite();
            _progressFill.type = Image.Type.Sliced;
            _progressFill.raycastTarget = false;
            var fillRt = fillGo.GetComponent<RectTransform>();
            fillRt.anchorMin = new Vector2(0, 0);
            fillRt.anchorMax = new Vector2(0, 1);
            fillRt.pivot = new Vector2(0, 0.5f);
            fillRt.offsetMin = new Vector2(2, 2);
            fillRt.offsetMax = new Vector2(2, -2);
            fillRt.sizeDelta = new Vector2(0, 0);
        }

        private static Sprite LoadLogo()
        {
            var srcTex = Resources.Load<Texture2D>("UI/LogoFull");
            if (srcTex == null)
            {
                var s = Resources.Load<Sprite>("UI/LogoFull");
                if (s != null) srcTex = s.texture;
            }
            if (srcTex == null) return null;

            return ApplyRoundedCorners(srcTex);
        }

        private static Sprite ApplyRoundedCorners(Texture2D src)
        {
            int w = src.width;
            int h = src.height;

            Color[] pixels;
            try { pixels = src.GetPixels(); }
            catch
            {
                return Sprite.Create(src, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
            }

            float radius = Mathf.Min(w, h) * 0.08f;
            float r2 = radius * radius;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float distX = -1f, distY = -1f;
                    if (x < radius && y < radius) { distX = radius - x; distY = radius - y; }
                    else if (x >= w - radius && y < radius) { distX = x - (w - 1 - radius); distY = radius - y; }
                    else if (x < radius && y >= h - radius) { distX = radius - x; distY = y - (h - 1 - radius); }
                    else if (x >= w - radius && y >= h - radius) { distX = x - (w - 1 - radius); distY = y - (h - 1 - radius); }

                    if (distX < 0f) continue;

                    float d2 = distX * distX + distY * distY;
                    float a = 1f;
                    if (d2 >= r2) a = 0f;
                    else
                    {
                        float d = Mathf.Sqrt(d2);
                        float edge = radius - d;
                        a = Mathf.Clamp01(edge);
                    }
                    var c = pixels[y * w + x];
                    c.a *= a;
                    pixels[y * w + x] = c;
                }
            }

            var rounded = new Texture2D(w, h, TextureFormat.RGBA32, false);
            rounded.wrapMode = TextureWrapMode.Clamp;
            rounded.filterMode = FilterMode.Bilinear;
            rounded.SetPixels(pixels);
            rounded.Apply();

            return Sprite.Create(rounded, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
        }

        private static Sprite _roundedSprite;
        private static Sprite MakeRoundedSprite()
        {
            if (_roundedSprite != null) return _roundedSprite;
            const int size = 32;
            const int radius = 8;
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

        private IEnumerator RunLoadingSequence()
        {
            var steps = new (string label, float target, float duration)[]
            {
                ("Initialisation...", 0.15f, 0.4f),
                ("Chargement de la sauvegarde...", 0.45f, 0.6f),
                ("Connexion au serveur...", 0.75f, 0.7f),
                ("Préparation de la flotte...", 0.95f, 0.4f),
                ("Prêt !", 1.0f, 0.2f),
            };

            float current = 0f;
            foreach (var step in steps)
            {
                _statusLabel.text = step.label;
                float elapsed = 0f;
                float start = current;
                while (elapsed < step.duration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / step.duration);
                    current = Mathf.Lerp(start, step.target, t);
                    UpdateProgress(current);
                    yield return null;
                }
                current = step.target;
                UpdateProgress(current);
            }

            yield return new WaitForSeconds(0.2f);

            // Fade out
            float fadeDuration = 0.4f;
            float fadeElapsed = 0f;
            while (fadeElapsed < fadeDuration)
            {
                fadeElapsed += Time.deltaTime;
                _canvasGroup.alpha = 1f - Mathf.Clamp01(fadeElapsed / fadeDuration);
                yield return null;
            }

            Destroy(gameObject);
        }

        private void UpdateProgress(float t)
        {
            if (_progressFill == null || _progressBarBg == null) return;
            float barWidth = _progressBarBg.sizeDelta.x - 4f;
            var rt = _progressFill.rectTransform;
            rt.sizeDelta = new Vector2(barWidth * Mathf.Clamp01(t), 0);
        }
    }
}
