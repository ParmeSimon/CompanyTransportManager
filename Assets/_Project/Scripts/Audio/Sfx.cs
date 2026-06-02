using UnityEngine;

namespace TransportManager.Audio
{
    /// <summary>
    /// Lecteur d'effets sonores. Les sons sont générés procéduralement (aucun asset requis)
    /// et respectent les réglages audio (PlayerPrefs : s_audio_on, s_fx_vol).
    /// </summary>
    public class Sfx : MonoBehaviour
    {
        private static Sfx _i;

        private AudioSource _src;
        private AudioClip _cash, _success, _pop, _click;

        public static void Ensure()
        {
            if (_i != null) return;
            var go = new GameObject("Sfx");
            Object.DontDestroyOnLoad(go);
            _i = go.AddComponent<Sfx>();
        }

        private void Awake()
        {
            _src = gameObject.AddComponent<AudioSource>();
            _src.playOnAwake = false;
            _src.spatialBlend = 0f;

            _cash    = Arpeggio(new[] { 784f, 988f, 1319f }, 0.07f, 0.45f);   // sol-si-mi montant (caisse)
            _success = Chord(new[] { 523f, 659f, 784f, 1047f }, 0.55f, 0.40f); // accord majeur (réussite)
            _pop     = Blip(880f, 0.09f, 0.40f);                               // petit pop
            _click   = Blip(440f, 0.05f, 0.30f);
        }

        private static bool On  => PlayerPrefs.GetInt("s_audio_on", 1) == 1;
        private static float Vol => Mathf.Clamp01(PlayerPrefs.GetFloat("s_fx_vol", 1f));

        private void PlayClip(AudioClip c)
        {
            if (c == null || _src == null || !On) return;
            _src.PlayOneShot(c, Vol);
        }

        public static void Cash()    => _i?.PlayClip(_i._cash);
        public static void Success() => _i?.PlayClip(_i._success);
        public static void Pop()     => _i?.PlayClip(_i._pop);
        public static void Click()   => _i?.PlayClip(_i._click);

        // ── Génération procédurale ─────────────────────────────────────────────────
        private const int SampleRate = 44100;

        private static AudioClip Blip(float freq, float dur, float gain)
        {
            int n = Mathf.Max(1, (int)(SampleRate * dur));
            var data = new float[n];
            for (int s = 0; s < n; s++)
            {
                float t = (float)s / SampleRate;
                float env = Mathf.Exp(-t * 14f);                 // attaque vive + décroissance
                data[s] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * gain;
            }
            var clip = AudioClip.Create("blip", n, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip Chord(float[] freqs, float dur, float gain)
        {
            int n = Mathf.Max(1, (int)(SampleRate * dur));
            var data = new float[n];
            for (int s = 0; s < n; s++)
            {
                float t = (float)s / SampleRate;
                float env = Mathf.Exp(-t * 4.5f);
                float v = 0f;
                foreach (var f in freqs) v += Mathf.Sin(2f * Mathf.PI * f * t);
                data[s] = (v / freqs.Length) * env * gain;
            }
            var clip = AudioClip.Create("chord", n, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip Arpeggio(float[] freqs, float noteDur, float gain)
        {
            int per = Mathf.Max(1, (int)(SampleRate * noteDur));
            int n = per * freqs.Length;
            var data = new float[n];
            for (int k = 0; k < freqs.Length; k++)
            {
                for (int s = 0; s < per; s++)
                {
                    float t = (float)s / SampleRate;
                    float env = Mathf.Exp(-t * 16f);
                    data[k * per + s] = Mathf.Sin(2f * Mathf.PI * freqs[k] * t) * env * gain;
                }
            }
            var clip = AudioClip.Create("arp", n, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
