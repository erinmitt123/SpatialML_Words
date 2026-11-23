using System;
using System.Collections.Generic;
using UnityEngine;
using WordWorld.Audio;
using WordWorld.Spatial;
using WordWorld.Tracking;

namespace WordWorld.Labels
{
    /// <summary>
    /// Room-scale feedback. <see cref="Ripple"/> (when an activity starts): a ring of light runs out
    /// across the floor from the learner, and each object lifts and glows as it passes.
    /// <see cref="LightUp"/> (end of a Find It round): a slower wave lights up every collected word
    /// in turn, nearest first, with a sparkle and a chime at each object.
    /// Callers use the static methods; this component draws the particles and times the sparkles.
    /// </summary>
    [AddComponentMenu("Word World/Room Fx")]
    public sealed class RoomFx : MonoBehaviour
    {
        [SerializeField] private TrackingManager m_tracking;
        [SerializeField] private ParticleSystem m_ring;
        [SerializeField] private ParticleSystem m_sparkle;
        [Tooltip("Ring speed across the floor; the ripple particles are authored to match.")]
        [SerializeField] private float m_rippleSpeed = 1.8f;
        [SerializeField] private float m_lightUpSpeed = 1.2f;
        [Tooltip("A second ripple this soon after the last is skipped, so mode changes do not stack them.")]
        [SerializeField] private float m_minGapSeconds = 1.5f;

        private static event Action<ObjectEffects.WaveKind, HashSet<int>> Requested;

        private struct Pending
        {
            public TrackedObject Track;
            public float At;
        }

        private readonly List<Pending> m_pending = new List<Pending>();
        private float m_lastRipple = -10f;

        /// <summary>Plays the start-of-activity ripple from the learner.</summary>
        public static void Ripple() => Requested?.Invoke(ObjectEffects.WaveKind.Ripple, null);

        /// <summary>Lights up each object whose class is in <paramref name="classes"/> (null = all).</summary>
        public static void LightUp(ICollection<int> classes) =>
            Requested?.Invoke(ObjectEffects.WaveKind.LightUp, classes == null ? null : new HashSet<int>(classes));

        private void OnEnable() => Requested += OnRequested;

        private void OnDisable() => Requested -= OnRequested;

        private void OnRequested(ObjectEffects.WaveKind kind, HashSet<int> classes)
        {
            if (!HeadPose.TryGet(out Pose head)) return;
            float now = Time.unscaledTime;
            if (kind == ObjectEffects.WaveKind.Ripple)
            {
                if (now - m_lastRipple < m_minGapSeconds) return;
                m_lastRipple = now;
            }
            float speed = kind == ObjectEffects.WaveKind.Ripple ? m_rippleSpeed : m_lightUpSpeed;
            ObjectEffects.StartWave(kind, head.position, speed, classes);

            if (kind == ObjectEffects.WaveKind.Ripple)
            {
                if (m_ring != null)
                {
                    m_ring.transform.position = new Vector3(head.position.x, RoomFloor.Below(head.position) + 0.02f, head.position.z);
                    m_ring.Play();
                }
                Sfx.Appear(head.position + head.rotation * new Vector3(0f, -0.4f, 0.6f));
                return;
            }

            // Light-up: a sparkle and a chime as the wave reaches each collected object.
            m_pending.Clear();
            if (m_tracking == null) return;
            foreach (TrackedObject track in m_tracking.Confirmed)
            {
                if (classes != null && !classes.Contains(track.ClassId)) continue;
                Vector3 flat = track.Position - head.position;
                flat.y = 0f;
                m_pending.Add(new Pending { Track = track, At = now + flat.magnitude / speed });
            }
        }

        private void Update()
        {
            if (m_pending.Count == 0) return;
            float now = Time.unscaledTime;
            for (int i = m_pending.Count - 1; i >= 0; i--)
            {
                Pending p = m_pending[i];
                if (now < p.At) continue;
                m_pending.RemoveAt(i);
                Vector3 top = p.Track.Position + Vector3.up * (p.Track.Height * 0.5f);
                Burst(top);
                Sfx.Appear(top);
            }
        }

        private void Burst(Vector3 at)
        {
            if (m_sparkle == null) return;
            var emit = new ParticleSystem.EmitParams { position = at, applyShapeToPosition = true };
            m_sparkle.Emit(emit, 24);
        }

        // ---- Authoring (called by the scene builder, so the settings are serialized) -------------

        public static void ConfigureRing(ParticleSystem ps, Material material, Color color, float speed)
        {
            ParticleSystem.MainModule main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 1f;
            main.startLifetime = 2.4f;
            main.startSpeed = speed;
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.04f);
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 400;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 0f;
            // Two rings, like a drop in water.
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 180), new ParticleSystem.Burst(0.35f, 120) });

            // A flat circle on the floor, emitting outward.
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.3f;
            shape.radiusThickness = 0f;
            shape.arcMode = ParticleSystemShapeMultiModeValue.BurstSpread;
            shape.rotation = new Vector3(90f, 0f, 0f);

            ParticleSystem.ColorOverLifetimeModule fade = ps.colorOverLifetime;
            fade.enabled = true;
            fade.color = FadeOut(0.15f);

            SetRenderer(ps, material);
        }

        public static void ConfigureSparkle(ParticleSystem ps, Material material, Color color)
        {
            ParticleSystem.MainModule main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.45f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.028f);
            main.startColor = color;
            main.gravityModifier = -0.05f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 300;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 0f;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.06f;

            ParticleSystem.ColorOverLifetimeModule fade = ps.colorOverLifetime;
            fade.enabled = true;
            fade.color = FadeOut(0.05f);

            SetRenderer(ps, material);
        }

        private static Gradient FadeOut(float fadeIn)
        {
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, fadeIn), new GradientAlphaKey(0f, 1f) });
            return g;
        }

        private static void SetRenderer(ParticleSystem ps, Material material)
        {
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }
}
