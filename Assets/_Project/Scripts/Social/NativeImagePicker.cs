using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace TransportManager.Social
{
    /// <summary>
    /// Sélecteur d'image natif (galerie photo).
    /// - Éditeur : ouvre un explorateur de fichiers (test sur Mac/PC).
    /// - iOS : plugin natif UIImagePickerController (NativeImagePicker.mm).
    /// - Android : nécessite un plugin de galerie ; repli sans plantage.
    /// </summary>
    public class NativeImagePicker : MonoBehaviour
    {
        private const string ReceiverName = "NativeImagePickerReceiver";

        private static NativeImagePicker _receiver;
        private static Action<Texture2D> _callback;

        /// <param name="callback">Reçoit la texture choisie, ou null si annulé/échoué.</param>
        public static void Pick(Action<Texture2D> callback)
        {
            _callback = callback;

#if UNITY_EDITOR
            PickInEditor();
#elif UNITY_IOS
            EnsureReceiver();
            _PickImage(ReceiverName);
#elif UNITY_ANDROID
            Debug.LogWarning("[NativeImagePicker] Import depuis la galerie Android non implémenté (plugin requis).");
            Done(null);
#else
            Debug.LogWarning("[NativeImagePicker] Plateforme non supportée.");
            Done(null);
#endif
        }

        private static void EnsureReceiver()
        {
            if (_receiver != null) return;
            var go = new GameObject(ReceiverName);
            DontDestroyOnLoad(go);
            _receiver = go.AddComponent<NativeImagePicker>();
        }

        private static void Done(Texture2D tex)
        {
            var cb = _callback;
            _callback = null;
            cb?.Invoke(tex);
        }

        private static Texture2D LoadTexture(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            try
            {
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                return tex.LoadImage(File.ReadAllBytes(path)) ? tex : null;
            }
            catch (Exception e)
            {
                Debug.LogError($"[NativeImagePicker] Lecture échouée : {e.Message}");
                return null;
            }
        }

        // Appelé par le plugin natif via UnitySendMessage.
        public void OnNativeImagePicked(string path)
        {
            Done(LoadTexture(path));
        }

#if UNITY_EDITOR
        private static void PickInEditor()
        {
            string path = UnityEditor.EditorUtility.OpenFilePanel("Choisir un logo", "", "png,jpg,jpeg");
            Done(LoadTexture(path));
        }
#endif

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void _PickImage(string gameObjectName);
#endif
    }
}
