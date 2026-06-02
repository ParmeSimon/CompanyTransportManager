using UnityEngine;

namespace TransportManager.Core
{
    /// <summary>
    /// Retour haptique léger (mobile uniquement). Respecte le réglage audio « son activé ».
    /// </summary>
    public static class Haptics
    {
        public static bool Enabled = true;

        // Évite de faire vibrer en rafale (ex. plusieurs contrats finis d'un coup).
        private static float _lastVibe;
        private const float MinInterval = 0.12f;

        public static void Light()   => Vibrate();
        public static void Success() => Vibrate();
        public static void Heavy()   => Vibrate();

        private static void Vibrate()
        {
#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
            if (!Enabled) return;
            if (PlayerPrefs.GetInt("s_audio_on", 1) != 1) return; // pas de buzz si tout le son est coupé
            float now = Time.unscaledTime;
            if (now - _lastVibe < MinInterval) return;
            _lastVibe = now;
            Handheld.Vibrate();
#endif
        }
    }
}
