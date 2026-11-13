using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TransportManager.Core
{
    /// <summary>
    /// Échelle unique de toute l'interface (scène + popups créées par code), calibrée mobile.
    ///
    /// Repères (téléphone paysage, ex. iPhone 844×390 pt) :
    ///   • Apple HIG : texte ≥ 11 pt, corps 17 pt · Material : ≥ 12 sp, corps 14–16 sp
    ///   • Accessibilité jeux mobiles : tout texte ≥ ~2,5 % de la hauteur d'écran
    /// Avec Reference = 640 de haut, 1 unité ≈ 0,61 pt. Après TextBoost :
    ///   légendes 15–16 u ≈ 9–10 pt · corps 16–18 u ≈ 10–11 pt · titres 27+ u ≈ 17 pt+.
    ///   (compromis tycoon paysage dense : au-dessus des jeux du genre, un peu sous le HIG « app »)
    ///
    /// Mode « Expand » : le canvas fait toujours au moins Reference en unités → la mise en page
    /// tient sur tous les formats (20:9, 16:9, tablette 4:3) ; l'excédent est de la place en plus.
    /// </summary>
    public static class UiScale
    {
        /// Plus petit = interface plus grosse (boutons, cartes ET textes).
        public static readonly Vector2 Reference = new Vector2(1400f, 640f);

        /// Agrandissement appliqué à toutes les tailles de texte écrites dans le code.
        public const float TextBoost = 1.25f;

        /// Aucun texte en dessous (unités canvas) : ≈ 9 pt sur téléphone.
        public const float MinFontSize = 15f;

        public static float Boosted(float size) => Mathf.Max(MinFontSize, Mathf.Round(size * TextBoost));

        public static void Configure(CanvasScaler scaler)
        {
            if (scaler == null) return;
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = Reference;
            scaler.screenMatchMode     = CanvasScaler.ScreenMatchMode.Expand;
            if (scaler.GetComponent<UiTextBoost>() == null) scaler.gameObject.AddComponent<UiTextBoost>();
        }

        // Canvas de la scène (réglés dans l'inspecteur) : même agrandissement des textes.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void HookSceneCanvases()
        {
            foreach (var s in Object.FindObjectsByType<CanvasScaler>(FindObjectsSortMode.None))
                if (s.GetComponent<UiTextBoost>() == null) s.gameObject.AddComponent<UiTextBoost>();
        }
    }

    /// Marqueur : ce texte garde sa taille (pas d agrandissement automatique).
    public class UiNoTextBoost : MonoBehaviour { }

    /// <summary>
    /// Agrandit UNE fois chaque texte TMP du canvas (×TextBoost, plancher MinFontSize) — l'UI est
    /// construite par code un peu partout, ce point unique évite de retoucher chaque étiquette.
    /// Si la ligne du texte a une hauteur fixe (LayoutElement) trop basse, elle est relevée aussi.
    /// </summary>
    public class UiTextBoost : MonoBehaviour
    {
        private float _next;
        private readonly HashSet<int> _done = new HashSet<int>();
        private readonly List<TMP_Text> _buf = new List<TMP_Text>();

        private void OnEnable() => _next = 0f;

        private void LateUpdate()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.1f;   // les popups construites à la volée sont traitées très vite

            if (_done.Count > 50000) _done.Clear();
            GetComponentsInChildren(true, _buf);
            _roots.Clear();
            foreach (var t in _buf)
            {
                if (t == null || !_done.Add(t.GetInstanceID())) continue;
                if (t.GetComponent<UiNoTextBoost>() != null) continue;   // ex. mention légale de la carte
                _roots.Add(LayoutRoot(t.transform));

                float size;
                if (t.enableAutoSizing)
                {
                    t.fontSizeMax = UiScale.Boosted(t.fontSizeMax);
                    t.fontSizeMin = Mathf.Min(t.fontSizeMax, Mathf.Max(UiScale.MinFontSize, t.fontSizeMin * UiScale.TextBoost));
                    size = t.fontSizeMax;
                }
                else
                {
                    t.fontSize = UiScale.Boosted(t.fontSize);
                    size = t.fontSize;
                }

                // Ligne à hauteur fixe : on la relève pour que le texte agrandi ne déborde pas.
                float need = Mathf.Ceil(size * 1.25f);
                RaiseFixedHeight(t.GetComponent<LayoutElement>(), need);
                // …et la rangée qui le contient (rangées HRow à hauteur fixe d'une seule ligne).
                if (t.transform.parent != null && t.transform.parent.GetComponent<HorizontalLayoutGroup>() != null)
                    RaiseFixedHeight(t.transform.parent.GetComponent<LayoutElement>(), need);
            }
            _buf.Clear();

            // Layouts imbriqués (carte à ContentSizeFitter dans une liste à ContentSizeFitter…) :
            // Unity les recalcule dans le désordre → éléments qui se chevauchent. On force deux
            // passes complètes depuis la racine de chaque layout touché.
            foreach (var root in _roots)
            {
                if (root == null) continue;
                LayoutRebuilder.ForceRebuildLayoutImmediate(root);
                LayoutRebuilder.ForceRebuildLayoutImmediate(root);
            }
            _roots.Clear();
        }

        private readonly HashSet<RectTransform> _roots = new HashSet<RectTransform>();

        // Remonte tant que le parent participe au layout (LayoutGroup / ContentSizeFitter).
        private static RectTransform LayoutRoot(Transform t)
        {
            var cur = t as RectTransform;
            while (cur != null && cur.parent is RectTransform p
                   && (p.GetComponent<LayoutGroup>() != null || p.GetComponent<ContentSizeFitter>() != null))
                cur = p;
            return cur;
        }

        private static void RaiseFixedHeight(LayoutElement le, float need)
        {
            if (le == null) return;
            if (le.preferredHeight > 0f && le.preferredHeight < need) le.preferredHeight = need;
            if (le.minHeight > 0f && le.minHeight < need) le.minHeight = need;
        }
    }
}
