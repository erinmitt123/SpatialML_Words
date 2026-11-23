using UnityEngine;

namespace WordWorld.Audio
{
    /// <summary>
    /// Calm background music made in code (no audio assets or licences): a slow pad through four
    /// chords (C, Am, F, G) with a sparse pentatonic bell melody on top, looping every 24 s.
    /// Samples are generated as the clip streams, so it costs no memory and no load time.
    /// Every voice's phase is measured from when that voice started, so the loop point is seamless.
    /// </summary>
    public static class AmbientMusic
    {
        private const int SampleRate = 22050;
        private const float ChordSeconds = 6f;
        private const float FadeSeconds = 2f;   // chords crossfade into each other
        private const float StepSeconds = 1.5f; // melody grid
        private const float LoopSeconds = ChordSeconds * 4f;
        private const float TwoPi = 2f * Mathf.PI;

        // Warm, open voicings in the low-middle range.
        private static readonly float[][] s_chords =
        {
            new[] { 130.81f, 196.00f, 329.63f, 493.88f }, // Cmaj7
            new[] { 110.00f, 164.81f, 261.63f, 392.00f }, // Am7
            new[] { 87.31f, 130.81f, 220.00f, 329.63f },  // Fmaj7
            new[] { 98.00f, 146.83f, 246.94f, 329.63f },  // G6
        };

        // C major pentatonic, one octave up; -1 is a rest. 16 steps = one loop.
        private static readonly float[] s_bells = { 523.25f, 587.33f, 659.25f, 783.99f, 880f };
        private static readonly int[] s_melody = { 2, -1, 4, -1, 3, -1, -1, 1, 0, -1, 2, -1, 1, -1, -1, 3 };

        private static int s_position;

        /// <summary>A looping, streamed clip. Create it once.</summary>
        public static AudioClip CreateClip()
        {
            int length = Mathf.RoundToInt(LoopSeconds * SampleRate);
            return AudioClip.Create("Ambient Music", length, 1, SampleRate, true, OnRead, OnSetPosition);
        }

        // May run on the audio thread: plain arithmetic only.
        private static void OnRead(float[] data)
        {
            int length = Mathf.RoundToInt(LoopSeconds * SampleRate);
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = Sample((float)s_position / SampleRate);
                if (++s_position >= length) s_position = 0;
            }
        }

        private static void OnSetPosition(int position) => s_position = position;

        private static float Sample(float t)
        {
            float pad = 0f;
            for (int c = 0; c < s_chords.Length; c++)
            {
                // Seconds since chord c started, wrapping around the loop.
                float u = Mathf.Repeat(t - c * ChordSeconds, LoopSeconds);
                if (u >= ChordSeconds + FadeSeconds) continue;
                float window = u < FadeSeconds ? Rise(u / FadeSeconds)
                    : u > ChordSeconds ? 1f - Rise((u - ChordSeconds) / FadeSeconds)
                    : 1f;
                float[] notes = s_chords[c];
                float sum = 0f;
                for (int n = 0; n < notes.Length; n++)
                {
                    float f = notes[n];
                    // A slightly detuned twin makes the tone breathe; a soft octave adds warmth.
                    sum += Mathf.Sin(TwoPi * f * u) + 0.6f * Mathf.Sin(TwoPi * (f + 0.7f) * u)
                        + 0.12f * Mathf.Sin(TwoPi * 2f * f * u);
                }
                pad += window * sum;
            }
            // Slow swell, three times per loop so it repeats exactly.
            pad *= 0.85f + 0.15f * Mathf.Sin(TwoPi * 3f * t / LoopSeconds);

            // The bell on this step and the tail of the one before.
            int step = (int)(t / StepSeconds);
            float since = t - step * StepSeconds;
            float bells = Bell(step, since) + Bell(step - 1, since + StepSeconds);

            return 0.03f * pad + bells;
        }

        private static float Bell(int step, float u)
        {
            int index = s_melody[(step % s_melody.Length + s_melody.Length) % s_melody.Length];
            if (index < 0) return 0f;
            float f = s_bells[index];
            float envelope = Mathf.Min(1f, u / 0.01f) * Mathf.Exp(-2.2f * u);
            return 0.055f * envelope * (Mathf.Sin(TwoPi * f * u) + 0.15f * Mathf.Sin(TwoPi * f * 2.76f * u));
        }

        // Raised cosine, so a fading chord and a rising one always sum to full level.
        private static float Rise(float x) => 0.5f - 0.5f * Mathf.Cos(Mathf.PI * Mathf.Clamp01(x));
    }
}
