using System.Collections.Generic;
using UnityEngine;
using WordWorld.Tracking;

namespace WordWorld.Labels
{
    /// <summary>
    /// Motion and glow shared by an object's label and, for practice objects, its 3D model, so both
    /// move together: a shake for a wrong pick, a small jump for a right one, and waves that travel
    /// out from the learner and lift each object as they pass (the room ripple when an activity
    /// starts, and the end-of-round light-up of every collected word).
    /// </summary>
    public static class ObjectEffects
    {
        public enum WaveKind { Ripple, LightUp }

        public struct Motion
        {
            /// <summary>World-space offset; the shake is along <c>right</c> as passed to Evaluate.</summary>
            public Vector3 Offset;
            /// <summary>0..1 tint toward the "correct" green.</summary>
            public float Glow;
            public float Scale;
            /// <summary>Forces the label visible (a light-up shows even far or dimmed labels).</summary>
            public bool Show;
        }

        private struct Wave
        {
            public WaveKind Kind;
            public Vector3 Origin;
            public float Start;
            public float Speed;
            public HashSet<int> Classes; // null = every object
        }

        private const float ShakeSeconds = 0.5f, ShakeMetres = 0.02f, ShakeHz = 9f;
        private const float JumpSeconds = 0.5f, JumpMetres = 0.06f;
        private const float RippleSeconds = 0.6f, RippleMetres = 0.035f;
        private const float LightUpSeconds = 2.4f;
        private const float MaxWaveRange = 8f;

        private static readonly Dictionary<TrackedObject, float> s_shakeStart = new Dictionary<TrackedObject, float>();
        private static readonly Dictionary<TrackedObject, float> s_jumpStart = new Dictionary<TrackedObject, float>();
        private static readonly List<Wave> s_waves = new List<Wave>();

        public static void Shake(TrackedObject track)
        {
            if (track != null) s_shakeStart[track] = Time.unscaledTime;
        }

        public static void Jump(TrackedObject track)
        {
            if (track != null) s_jumpStart[track] = Time.unscaledTime;
        }

        /// <summary>A wave travelling out from <paramref name="origin"/> at <paramref name="speed"/> m/s.</summary>
        public static void StartWave(WaveKind kind, Vector3 origin, float speed, HashSet<int> classes = null)
        {
            Prune(Time.unscaledTime);
            s_waves.Add(new Wave { Kind = kind, Origin = origin, Start = Time.unscaledTime, Speed = Mathf.Max(0.1f, speed), Classes = classes });
        }

        public static Motion Evaluate(TrackedObject track, Vector3 position, Vector3 right, float now)
        {
            var m = new Motion { Scale = 1f };
            if (track == null) return m;

            if (s_shakeStart.TryGetValue(track, out float shake))
            {
                float t = (now - shake) / ShakeSeconds;
                if (t >= 1f) s_shakeStart.Remove(track);
                else if (t >= 0f) m.Offset += right * (ShakeMetres * (1f - t) * Mathf.Sin(t * ShakeSeconds * ShakeHz * 2f * Mathf.PI));
            }

            if (s_jumpStart.TryGetValue(track, out float jump))
            {
                float t = (now - jump) / JumpSeconds;
                if (t >= 1f) s_jumpStart.Remove(track);
                else if (t >= 0f)
                {
                    m.Offset += Vector3.up * (JumpMetres * Mathf.Sin(t * Mathf.PI));
                    m.Glow = Mathf.Max(m.Glow, 1f - t);
                }
            }

            foreach (Wave w in s_waves)
            {
                if (w.Classes != null && !w.Classes.Contains(track.ClassId)) continue;
                float local = now - (w.Start + Flat(position - w.Origin) / w.Speed);
                if (local < 0f) continue;
                if (w.Kind == WaveKind.Ripple)
                {
                    if (local >= RippleSeconds) continue;
                    float t = local / RippleSeconds;
                    m.Offset += Vector3.up * (RippleMetres * Mathf.Sin(t * Mathf.PI));
                    m.Glow = Mathf.Max(m.Glow, 1f - t);
                }
                else
                {
                    if (local >= LightUpSeconds) continue;
                    float t = local / LightUpSeconds;
                    // Lift and brighten fast, hold, then settle.
                    float glow = t < 0.1f ? t / 0.1f : t > 0.7f ? (1f - t) / 0.3f : 1f;
                    m.Glow = Mathf.Max(m.Glow, glow);
                    if (local < JumpSeconds) m.Offset += Vector3.up * (JumpMetres * Mathf.Sin(local / JumpSeconds * Mathf.PI));
                    m.Scale *= 1f + 0.08f * glow;
                    m.Show = true;
                }
            }
            return m;
        }

        private static void Prune(float now)
        {
            for (int i = s_waves.Count - 1; i >= 0; i--)
                if (now - s_waves[i].Start > MaxWaveRange / s_waves[i].Speed + LightUpSeconds) s_waves.RemoveAt(i);
        }

        private static float Flat(Vector3 v)
        {
            v.y = 0f;
            return v.magnitude;
        }
    }
}
