using UnityEngine;

namespace WordWorld.Audio
{
    /// <summary>
    /// Short UI sounds made in code, so the repo needs no extra audio assets or licences.
    /// </summary>
    public static class AudioCues
    {
        private const int SampleRate = 48000;
        private static AudioClip s_ping;
        private static AudioClip s_appear;
        private static AudioClip s_tap;
        private static AudioClip s_correct;
        private static AudioClip s_hover;
        private static AudioClip s_dismiss;

        /// <summary>
        /// A soft two-tone chime (about 0.4 s) that says "over here". Bright partials make it easy to
        /// locate in 3D; the quick fade keeps it gentle when it repeats every Say It item.
        /// </summary>
        public static AudioClip Ping => s_ping != null ? s_ping : s_ping = CreatePing();

        /// <summary>A low, soft water ripple: something new appeared here (a label or a window).</summary>
        public static AudioClip Appear => s_appear != null ? s_appear : s_appear = CreateRipple("Appear", 380f, 980f, 0.22f);

        /// <summary>A brighter, shorter ripple: the learner touched something here.</summary>
        public static AudioClip Tap => s_tap != null ? s_tap : s_tap = CreateRipple("Tap", 620f, 1550f, 0.2f);

        /// <summary>A rising three-note bell for a correct Find It answer (DESIGN.md section 6).</summary>
        public static AudioClip Correct => s_correct != null ? s_correct : s_correct = CreateCorrect();

        /// <summary>Looking or pointing at a button: a tiny, soft tick (gaze feedback, V3 review).</summary>
        public static AudioClip Hover => s_hover != null ? s_hover : s_hover = CreateTick();

        /// <summary>A window closing: a short falling note, the opposite of the appear ripple.</summary>
        public static AudioClip Dismiss => s_dismiss != null ? s_dismiss : s_dismiss = CreateGlide("Dismiss", 820f, 470f, 0.12f);

        private static AudioClip CreateTick()
        {
            var samples = new float[Mathf.CeilToInt(0.06f * SampleRate)];
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / SampleRate;
                float envelope = Mathf.Min(1f, t / 0.002f) * Mathf.Exp(-80f * t);
                samples[i] = 0.07f * envelope * Mathf.Sin(2f * Mathf.PI * 1900f * t);
            }
            return Clip("Hover", samples);
        }

        private static AudioClip CreateGlide(string name, float from, float to, float amplitude)
        {
            var samples = new float[Mathf.CeilToInt(0.35f * SampleRate)];
            float phase = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / SampleRate;
                float frequency = to + (from - to) * Mathf.Exp(-t / 0.05f);
                phase += 2f * Mathf.PI * frequency / SampleRate;
                float envelope = Mathf.Min(1f, t / 0.004f) * Mathf.Exp(-14f * t);
                samples[i] = amplitude * envelope * Mathf.Sin(phase);
            }
            return Clip(name, samples);
        }

        private static AudioClip CreatePing()
        {
            const float seconds = 0.42f;
            const float attack = 0.006f;   // no click at the start
            const float decay = 9f;        // per second
            int count = Mathf.CeilToInt(seconds * SampleRate);
            var samples = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / SampleRate;
                float envelope = Mathf.Min(1f, t / attack) * Mathf.Exp(-decay * t);
                // A fifth apart (E6 and B6), the upper note slightly delayed so it rises like a chime.
                float low = Mathf.Sin(2f * Mathf.PI * 1318.5f * t);
                float high = t < 0.06f ? 0f : Mathf.Sin(2f * Mathf.PI * 1975.5f * (t - 0.06f));
                samples[i] = 0.32f * envelope * (low + 0.7f * high);
            }
            return Clip("Ping", samples);
        }

        // A water drop is a sine whose pitch rises fast as the bubble closes, then dies away. Two
        // quieter, higher echoes after it read as the rings spreading out.
        private static AudioClip CreateRipple(string name, float from, float to, float amplitude)
        {
            var samples = new float[Mathf.CeilToInt(0.5f * SampleRate)];
            Drop(samples, 0f, from, to, amplitude);
            Drop(samples, 0.075f, from * 1.12f, to * 1.12f, amplitude * 0.42f);
            Drop(samples, 0.16f, from * 1.26f, to * 1.26f, amplitude * 0.18f);
            return Clip(name, samples);
        }

        private static void Drop(float[] samples, float start, float from, float to, float amplitude)
        {
            const float rise = 0.028f;   // how fast the pitch climbs
            const float decay = 26f;     // per second
            const float attack = 0.003f;
            int first = Mathf.RoundToInt(start * SampleRate);
            float phase = 0f;
            for (int i = first; i < samples.Length; i++)
            {
                float t = (float)(i - first) / SampleRate;
                float frequency = to - (to - from) * Mathf.Exp(-t / rise);
                phase += 2f * Mathf.PI * frequency / SampleRate;
                float envelope = Mathf.Min(1f, t / attack) * Mathf.Exp(-decay * t);
                if (envelope < 1e-4f && t > attack) break;
                samples[i] += amplitude * envelope * Mathf.Sin(phase);
            }
        }

        // C6, E6, G6, 70 ms apart; each a sine with a faint inharmonic partial so it rings like a bell.
        private static AudioClip CreateCorrect()
        {
            float[] notes = { 1046.5f, 1318.5f, 1568f };
            var samples = new float[Mathf.CeilToInt(0.9f * SampleRate)];
            for (int n = 0; n < notes.Length; n++)
            {
                int first = Mathf.RoundToInt(n * 0.07f * SampleRate);
                for (int i = first; i < samples.Length; i++)
                {
                    float t = (float)(i - first) / SampleRate;
                    float envelope = Mathf.Min(1f, t / 0.004f) * Mathf.Exp(-5.5f * t);
                    float bell = Mathf.Sin(2f * Mathf.PI * notes[n] * t) + 0.18f * Mathf.Sin(2f * Mathf.PI * notes[n] * 2.76f * t);
                    samples[i] += 0.13f * envelope * bell;
                }
            }
            return Clip("Correct", samples);
        }

        private static AudioClip Clip(string name, float[] samples)
        {
            AudioClip clip = AudioClip.Create(name, samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
