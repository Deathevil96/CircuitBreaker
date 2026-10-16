using UnityEngine;

namespace CircuitBreaker.Fx
{
    /// <summary>Tiny synth so the game has sound without audio assets.</summary>
    public class Sfx : MonoBehaviour
    {
        public static Sfx Instance { get; private set; }
        public bool Muted;

        private AudioSource _src;
        private AudioClip _blip, _thud, _click, _win, _hint, _zap;

        private void Awake()
        {
            Instance = this;
            _src = gameObject.AddComponent<AudioSource>();
            _src.playOnAwake = false;
            _blip = Tone("blip", 0.16f, t => Mathf.Sin(2 * Mathf.PI * (520f + 900f * t) * t) * 0.5f + Mathf.Sin(2 * Mathf.PI * 1040f * t) * 0.15f, 14f);
            _thud = Tone("thud", 0.28f, t => Mathf.Sin(2 * Mathf.PI * (120f - 70f * t) * t) * 0.8f + (Random.value - 0.5f) * 0.3f * Mathf.Exp(-t * 30f), 9f);
            _click = Tone("click", 0.06f, t => Mathf.Sin(2 * Mathf.PI * 1400f * t) * 0.4f, 40f);
            _hint = Tone("hint", 0.35f, t => (Mathf.Sin(2 * Mathf.PI * 880f * t) + Mathf.Sin(2 * Mathf.PI * 1320f * t)) * 0.25f, 6f);
            _zap = Tone("zap", 0.22f, t => (Random.value - 0.5f) * 0.9f * Mathf.Exp(-t * 18f) + Mathf.Sin(2 * Mathf.PI * 300f * t) * 0.3f, 10f);
            _win = Arpeggio();
        }

        public static void Blip(float pitch) => Instance?.Play(Instance._blip, pitch, 0.7f);
        public static void Thud() => Instance?.Play(Instance._thud, 1f, 0.9f);
        public static void Click() => Instance?.Play(Instance._click, 1f, 0.6f);
        public static void Hint() => Instance?.Play(Instance._hint, 1f, 0.7f);
        public static void Zap() => Instance?.Play(Instance._zap, 1f, 0.7f);
        public static void Win() => Instance?.Play(Instance._win, 1f, 0.8f);

        private void Play(AudioClip c, float pitch, float vol)
        {
            if (Muted || c == null) return;
            _src.pitch = pitch;
            _src.PlayOneShot(c, vol);
        }

        private delegate float Wave(float t);

        private static AudioClip Tone(string name, float seconds, Wave w, float decay)
        {
            int rate = 44100, n = Mathf.CeilToInt(rate * seconds);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                data[i] = Mathf.Clamp(w(t) * Mathf.Exp(-t * decay) * Mathf.Min(1f, i / 60f), -1f, 1f);
            }
            var clip = AudioClip.Create(name, n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip Arpeggio()
        {
            float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
            int rate = 44100;
            float step = 0.11f, total = step * notes.Length + 0.5f;
            int n = Mathf.CeilToInt(rate * total);
            var data = new float[n];
            for (int k = 0; k < notes.Length; k++)
            {
                int start = Mathf.RoundToInt(k * step * rate);
                for (int i = 0; start + i < n && i < rate * 0.5f; i++)
                {
                    float t = i / (float)rate;
                    data[start + i] += (Mathf.Sin(2 * Mathf.PI * notes[k] * t) * 0.45f + Mathf.Sin(2 * Mathf.PI * notes[k] * 2f * t) * 0.12f) * Mathf.Exp(-t * 6f);
                }
            }
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(data[i], -1f, 1f);
            var clip = AudioClip.Create("win", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
