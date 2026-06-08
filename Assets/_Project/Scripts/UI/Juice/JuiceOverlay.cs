using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TransportManager.Audio;
using TransportManager.Core;
using TransportManager.Entities.Contracts;
using TransportManager.Entities.Progression;
using TransportManager.Entities.Vehicles;
using TransportManager.Events;

namespace TransportManager.UI.Juice
{
    /// <summary>
    /// Couche « game feel » : confettis, gerbe d'argent, pop de niveau et toasts,
    /// déclenchés par les événements de jeu. Entièrement procédural (aucun asset).
    /// </summary>
    public class JuiceOverlay : MonoBehaviour
    {
        private static JuiceOverlay _i;
        private RectTransform _root;
        private int _activeToasts;

        private static readonly Color[] Confetti =
        {
            new Color(0.24f, 0.80f, 0.44f), new Color(0.22f, 0.52f, 1.00f),
            new Color(0.97f, 0.85f, 0.30f), new Color(0.95f, 0.40f, 0.45f),
            new Color(0.60f, 0.45f, 1.00f), new Color(0.40f, 0.85f, 0.95f),
        };
        private static readonly Color Gold  = new Color(0.97f, 0.82f, 0.36f);
        private static readonly Color Green = new Color(0.30f, 0.86f, 0.50f);
        private static readonly Color TextPri = new Color(0.93f, 0.95f, 0.99f);
        private static readonly Color TextSec = new Color(0.62f, 0.68f, 0.78f);

        // ── Bootstrap ──────────────────────────────────────────────────────────────
        public static void Ensure()
        {
            if (_i != null) return;
            var go = new GameObject("JuiceOverlay");
            DontDestroyOnLoad(go);
            _i = go.AddComponent<JuiceOverlay>();
        }

        private void Awake()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 7000;   // au-dessus des popups, sous le tutoriel/splash
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight  = 0.5f;
            // Pas de GraphicRaycaster : la couche ne doit jamais intercepter les clics.

            _root = gameObject.GetComponent<RectTransform>();
            Sfx.Ensure();
        }

        private void OnEnable()
        {
            GameEvents.OnContractDelivered += OnContractDelivered;
            GameEvents.OnCompanyLevelUp    += OnLevelUp;
            GameEvents.OnSkillNodeUnlocked += OnSkillUnlocked;
            GameEvents.OnVehicleAdded      += OnVehicleAdded;
            GameEvents.OnReputationTierUp  += OnReputationTierUp;
            GameEvents.OnAchievementUnlocked += OnAchievementUnlocked;
        }

        private void OnDisable()
        {
            GameEvents.OnContractDelivered -= OnContractDelivered;
            GameEvents.OnCompanyLevelUp    -= OnLevelUp;
            GameEvents.OnSkillNodeUnlocked -= OnSkillUnlocked;
            GameEvents.OnVehicleAdded      -= OnVehicleAdded;
            GameEvents.OnReputationTierUp  -= OnReputationTierUp;
            GameEvents.OnAchievementUnlocked -= OnAchievementUnlocked;
        }

        // ── Handlers ───────────────────────────────────────────────────────────────
        private void OnContractDelivered(ContractInstance inst, int reward)
        {
            MoneyBurst(reward);
            SpawnConfetti(18);
            Sfx.Cash();
            Haptics.Success();
        }

        private void OnLevelUp(int oldLevel, int newLevel)
        {
            LevelPop(newLevel);
            SpawnConfetti(40);
            Sfx.Success();
            Haptics.Success();
        }

        private void OnSkillUnlocked(string nodeId)
        {
            string title = SkillTreeCatalog.GetById(nodeId)?.title ?? "Compétence";
            Toast("COMPÉTENCE DÉBLOQUÉE", title, Green);
            Sfx.Pop();
        }

        private void OnReputationTierUp(string tierName)
        {
            Toast("RÉPUTATION", $"Nouveau palier : {tierName}", Gold);
            SpawnConfetti(24);
            Sfx.Success();
            Haptics.Success();
        }

        private void OnAchievementUnlocked(Systems.Achievements.AchievementDef def)
        {
            if (def == null) return;
            Toast("SUCCÈS DÉBLOQUÉ", def.title, Gold);
            SpawnConfetti(28);
            Sfx.Success();
            Haptics.Success();
        }

        private void OnVehicleAdded(VehicleInstance v)
        {
            string name = ServiceLocator.Get<VehicleCatalog>()?.GetById(v?.vehicleDataId)?.displayName ?? "Nouveau véhicule";
            Toast("NOUVEAU VÉHICULE", name, new Color(0.22f, 0.52f, 1.00f));
            Sfx.Pop();
            Haptics.Light();
        }

        // ── Effets ─────────────────────────────────────────────────────────────────
        private void MoneyBurst(int amount)
        {
            var go = NewChild("MoneyBurst");
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, 120f);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = $"+{amount:N0} $";
            tmp.fontSize = 64;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = Gold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            tmp.outlineWidth = 0.18f;
            tmp.outlineColor = new Color32(0, 0, 0, 200);
            StartCoroutine(FloatUpFade(rt, tmp, 1.15f, 170f));
        }

        private void LevelPop(int level)
        {
            var go = NewChild("LevelPop");
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, 60f);
            var col = go.AddComponent<VerticalLayoutGroup>();
            col.childAlignment = TextAnchor.MiddleCenter; col.spacing = 2;
            col.childForceExpandWidth = false; col.childControlWidth = true;
            col.childForceExpandHeight = false; col.childControlHeight = true;

            AddText(go.transform, "NIVEAU SUPÉRIEUR", 26, FontStyles.Bold, Green);
            AddText(go.transform, level.ToString(), 120, FontStyles.Bold, TextPri);

            StartCoroutine(PopScale(rt, 1.7f));
        }

        private void Toast(string eyebrow, string label, Color accent)
        {
            int slot = _activeToasts++;
            var go = NewChild("Toast");
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot     = new Vector2(0.5f, 1f);
            float y = -150f - slot * 96f;
            rt.anchoredPosition = new Vector2(0f, y + 40f);
            rt.sizeDelta = new Vector2(660f, 84f);

            var bg = go.AddComponent<Image>();
            bg.sprite = Rounded(); bg.type = Image.Type.Sliced;
            bg.color  = new Color(0.16f, 0.18f, 0.22f, 0.97f);
            var sh = go.AddComponent<Shadow>();
            sh.effectColor = new Color(0, 0, 0, 0.5f); sh.effectDistance = new Vector2(0, -3f);

            // Barre d'accent à gauche
            var bar = NewChild("Bar", go.transform);
            var barImg = bar.AddComponent<Image>();
            barImg.sprite = Rounded(); barImg.type = Image.Type.Sliced; barImg.color = accent;
            var barRt = bar.GetComponent<RectTransform>();
            barRt.anchorMin = new Vector2(0, 0.5f); barRt.anchorMax = new Vector2(0, 0.5f);
            barRt.pivot = new Vector2(0, 0.5f); barRt.sizeDelta = new Vector2(5f, 52f);
            barRt.anchoredPosition = new Vector2(14f, 0f);

            // Textes
            var texts = NewChild("Txt", go.transform);
            var tRt = texts.GetComponent<RectTransform>();
            tRt.anchorMin = Vector2.zero; tRt.anchorMax = Vector2.one;
            tRt.offsetMin = new Vector2(34f, 8f); tRt.offsetMax = new Vector2(-18f, -8f);
            var v = texts.AddComponent<VerticalLayoutGroup>();
            v.childAlignment = TextAnchor.MiddleLeft; v.spacing = 1;
            v.childForceExpandHeight = false; v.childControlHeight = true;
            v.childForceExpandWidth = true; v.childControlWidth = true;
            var eb = AddText(texts.transform, eyebrow, 14, FontStyles.Bold, accent);
            eb.characterSpacing = 3f;
            AddText(texts.transform, label, 22, FontStyles.Bold, TextPri);

            StartCoroutine(ToastRoutine(rt, new Vector2(0f, y)));
        }

        // ── Coroutines d'animation ───────────────────────────────────────────────────
        private IEnumerator FloatUpFade(RectTransform rt, TMP_Text tmp, float dur, float rise)
        {
            Vector2 start = rt.anchoredPosition;
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float k = t / dur;
                rt.anchoredPosition = start + new Vector2(0f, rise * k);
                float pop = 1f + 0.15f * Mathf.Clamp01(k * 4f);
                rt.localScale = Vector3.one * (k < 0.25f ? pop : 1.15f);
                var c = tmp.color; c.a = 1f - Mathf.Clamp01((k - 0.55f) / 0.45f); tmp.color = c;
                yield return null;
            }
            Destroy(rt.gameObject);
        }

        private IEnumerator PopScale(RectTransform rt, float dur)
        {
            float t = 0f;
            var group = rt.gameObject.AddComponent<CanvasGroup>();
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float k = t / dur;
                float s = k < 0.3f ? Overshoot(k / 0.3f) : 1f;     // entrée avec rebond
                rt.localScale = Vector3.one * s;
                group.alpha = 1f - Mathf.Clamp01((k - 0.7f) / 0.3f); // fondu sortie
                yield return null;
            }
            Destroy(rt.gameObject);
        }

        private IEnumerator ToastRoutine(RectTransform rt, Vector2 target)
        {
            var group = rt.gameObject.AddComponent<CanvasGroup>();
            Vector2 from = rt.anchoredPosition;
            // entrée (slide + fade)
            float t = 0f;
            while (t < 0.25f)
            {
                t += Time.unscaledDeltaTime; float k = t / 0.25f;
                rt.anchoredPosition = Vector2.Lerp(from, target, EaseOut(k));
                group.alpha = k; yield return null;
            }
            rt.anchoredPosition = target;
            yield return new WaitForSecondsRealtime(2.2f);
            // sortie (slide up + fade)
            t = 0f;
            while (t < 0.3f)
            {
                t += Time.unscaledDeltaTime; float k = t / 0.3f;
                rt.anchoredPosition = target + new Vector2(0f, 40f * k);
                group.alpha = 1f - k; yield return null;
            }
            _activeToasts = Mathf.Max(0, _activeToasts - 1);
            Destroy(rt.gameObject);
        }

        private void SpawnConfetti(int count)
        {
            for (int n = 0; n < count; n++)
            {
                var go = NewChild("Cf");
                var img = go.AddComponent<Image>();
                img.color = Confetti[Random.Range(0, Confetti.Length)];
                img.raycastTarget = false;
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                float size = Random.Range(10f, 20f);
                rt.sizeDelta = new Vector2(size, size * Random.Range(0.5f, 1f));
                rt.anchoredPosition = new Vector2(Random.Range(-120f, 120f), Random.Range(260f, 360f));
                var vel = new Vector2(Random.Range(-260f, 260f), Random.Range(120f, 360f));
                StartCoroutine(ConfettiRoutine(rt, img, vel, Random.Range(-360f, 360f)));
            }
        }

        private IEnumerator ConfettiRoutine(RectTransform rt, Image img, Vector2 vel, float spin)
        {
            float life = Random.Range(1.0f, 1.5f);
            float t = 0f; float rot = Random.Range(0f, 360f);
            while (t < life)
            {
                float dt = Time.unscaledDeltaTime; t += dt;
                vel.y -= 900f * dt;                          // gravité
                vel.x *= (1f - 1.4f * dt);                   // friction air
                rt.anchoredPosition += vel * dt;
                rot += spin * dt;
                rt.localRotation = Quaternion.Euler(0, 0, rot);
                var c = img.color; c.a = 1f - Mathf.Clamp01((t - life * 0.6f) / (life * 0.4f)); img.color = c;
                yield return null;
            }
            Destroy(rt.gameObject);
        }

        // ── Helpers ──────────────────────────────────────────────────────────────────
        private static float EaseOut(float k)   => 1f - (1f - k) * (1f - k);
        private static float Overshoot(float k)  // 0→1 avec léger dépassement
        {
            const float s = 1.70158f;
            k -= 1f; return k * k * ((s + 1f) * k + s) + 1f;
        }

        private GameObject NewChild(string name) => NewChild(name, _root);
        private static GameObject NewChild(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static TMP_Text AddText(Transform parent, string text, float size, FontStyles style, Color color)
        {
            var go = NewChild("T", parent);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text; tmp.fontSize = size; tmp.fontStyle = style; tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.raycastTarget = false;
            return tmp;
        }

        // Sprite arrondi (toasts) — généré une fois.
        private static Sprite _rounded;
        private static Sprite Rounded()
        {
            if (_rounded != null) return _rounded;
            const int size = 48, r = 14;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float a = 1f;
                    int cx = -1, cy = -1;
                    if (x < r && y < r) { cx = r; cy = r; }
                    else if (x >= size - r && y < r) { cx = size - r; cy = r; }
                    else if (x < r && y >= size - r) { cx = r; cy = size - r; }
                    else if (x >= size - r && y >= size - r) { cx = size - r; cy = size - r; }
                    if (cx >= 0) a = Mathf.Clamp01(r - Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) + 0.5f);
                    tex.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            tex.Apply();
            _rounded = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                                     SpriteMeshType.FullRect, new Vector4(r, r, r, r));
            return _rounded;
        }
    }
}
