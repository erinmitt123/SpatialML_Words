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

        /// <summary>
        /// A soft two-tone chime (about 0.4 s) that says "over here". Bright partials make it easy to
        /// locate in 3D; the quick fade keeps it gentle when it repeats every Say It item.
        /// </summary>
        public static AudioClip Ping => s_ping != null ? s_ping : s_ping = CreatePing();

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
            AudioClip clip = AudioClip.Create("Ping", count, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
