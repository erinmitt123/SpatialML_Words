using UnityEngine;
using WordWorld.App;

namespace WordWorld.Spatial
{
    /// <summary>
    /// A few slow, faint motes of light drifting around the learner (V2 review: "make the space feel
    /// alive"). Passthrough stays the main view, so they are sparse, small and never closer than
    /// about 1.4 m. The emitter keeps to the head's position (not its rotation) while the motes live
    /// in world space, so turning the head never drags them along: no motion-sickness cue.
    /// </summary>
    [AddComponentMenu("Word World/Ambient Motes")]
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class AmbientMotes : MonoBehaviour
    {
        [SerializeField] private LearnerSettings m_settings;
        [SerializeField] private Material m_material;
        [Tooltip("The emitter re-centres on the head once it has moved this far, not every frame.")]
        [SerializeField] private float m_recentreMetres = 0.5f;

        private ParticleSystem m_particles;

        private void Awake()
        {
            m_particles = GetComponent<ParticleSystem>();
            m_particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void Update()
        {
            if (!HeadPose.TryGet(out Pose head)) return;
            bool show = m_settings == null || m_settings.SessionReady;
            if (show && !m_particles.isPlaying)
            {
                transform.position = head.position;
                m_particles.Play();
            }
            else if (!show && m_particles.isPlaying) m_particles.Stop();
            if ((transform.position - head.position).sqrMagnitude > m_recentreMetres * m_recentreMetres)
                transform.position = head.position;
        }

        /// <summary>Authored by the scene builder so the settings are serialized into the scene.</summary>
        public static void Configure(ParticleSystem particles, Material material)
        {
            ParticleSystem.MainModule main = particles.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 10f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(10f, 16f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.005f, 0.03f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.008f, 0.018f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.96f, 0.86f, 0.55f), new Color(0.8f, 0.95f, 0.9f, 0.55f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 120;
            main.gravityModifier = 0f;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 8f;

            // A shell from 1.4 to 3 m around the head.
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 3f;
            shape.radiusThickness = 0.53f;

            ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
            color.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f),
                        new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
            color.color = fade;

            ParticleSystem.NoiseModule noise = particles.noise;
            noise.enabled = true;
            noise.strength = 0.02f;
            noise.frequency = 0.2f;
            noise.scrollSpeed = 0.05f;

            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }
}
