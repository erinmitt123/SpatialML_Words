using UnityEngine;
using WordWorld.App;

namespace WordWorld.Audio
{
    /// <summary>
    /// Plays the UI sound effects (<see cref="Sfx"/>) from where they happen in the room, and the
    /// background music, according to the Sound setting (Off, Alerts only, All).
    ///
    /// Effects use their own small pool of 3D sources, so they never interrupt a word that
    /// <see cref="AudioTurn"/> is speaking. While the microphone is open (Say It) everything here
    /// goes silent at once, so Vosk only hears the learner.
    /// </summary>
    [AddComponentMenu("Word World/Sound Fx")]
    public sealed class SoundFx : MonoBehaviour
    {
        [SerializeField] private LearnerSettings m_settings;
        [SerializeField] private AudioTurn m_turn;

        [Header("Effects")]
        [SerializeField] private int m_voices = 4;
        [SerializeField, Range(0f, 1f)] private float m_effectsVolume = 0.8f;
        [Tooltip("A burst of labels appearing together makes one ripple, not a pile of them.")]
        [SerializeField] private float m_minGapSeconds = 0.12f;
        [SerializeField] private float m_hoverGapSeconds = 0.06f;

        [Header("Music")]
        [SerializeField, Range(0f, 1f)] private float m_musicVolume = 0.5f;
        [Tooltip("Music level while a word is being spoken, as a share of the normal level.")]
        [SerializeField, Range(0f, 1f)] private float m_duckedShare = 0.3f;
        [SerializeField] private float m_musicFadeSeconds = 1.5f;

        private AudioSource[] m_pool;
        private AudioSource m_music;
        private int m_next;
        private float m_lastPlayed = -1f;

        private bool MicOpen => m_turn != null && (m_turn.State == TurnState.Listening || m_turn.State == TurnState.Checking);

        private void Awake()
        {
            // Audio sources aren't drawn, so they can be made at runtime even in spatial mode.
            m_pool = new AudioSource[Mathf.Max(1, m_voices)];
            for (int i = 0; i < m_pool.Length; i++)
            {
                var go = new GameObject($"Effect {i}");
                go.transform.SetParent(transform, false);
                AudioSource source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 1f; // fully 3D: the ear finds where it happened
                source.rolloffMode = AudioRolloffMode.Logarithmic;
                source.minDistance = 1f;
                source.maxDistance = 15f;
                source.dopplerLevel = 0f;
                m_pool[i] = source;
            }

            var music = new GameObject("Music");
            music.transform.SetParent(transform, false);
            m_music = music.AddComponent<AudioSource>();
            m_music.playOnAwake = false;
            m_music.loop = true;
            m_music.spatialBlend = 0f; // music is around the learner, not at a place
            m_music.volume = 0f;
            m_music.clip = AmbientMusic.CreateClip();
        }

        private void OnEnable()
        {
            Sfx.Requested += OnRequested;
            if (m_turn != null) m_turn.StateChanged += OnTurnChanged;
        }

        private void OnDisable()
        {
            Sfx.Requested -= OnRequested;
            if (m_turn != null) m_turn.StateChanged -= OnTurnChanged;
        }

        private float m_lastHover = -1f;

        private void OnRequested(SfxKind kind, Vector3 position)
        {
            if (m_settings == null || m_settings.Sound == SoundMode.Off || MicOpen) return;
            float now = Time.unscaledTime;
            // Hover ticks keep their own, shorter gap and never hold back a tap or ripple; one is
            // skipped right after another sound so a tap does not tick too.
            if (kind == SfxKind.Hover)
            {
                if (now - m_lastHover < m_hoverGapSeconds || now - m_lastPlayed < m_minGapSeconds) return;
                m_lastHover = now;
            }
            else
            {
                // A correct answer always plays. Anything else within the gap is dropped, so a tap that
                // opens a window, or labels confirmed together, make one ripple.
                if (kind != SfxKind.Correct && kind != SfxKind.Dismiss && now - m_lastPlayed < m_minGapSeconds) return;
                m_lastPlayed = now;
            }

            AudioSource source = m_pool[m_next];
            m_next = (m_next + 1) % m_pool.Length;
            source.Stop();
            source.transform.position = position;
            switch (kind)
            {
                case SfxKind.Appear:
                    source.clip = AudioCues.Appear;
                    source.pitch = Random.Range(0.94f, 1.06f); // small variation so repeats feel natural
                    source.volume = m_effectsVolume * 0.8f;
                    break;
                case SfxKind.Tap:
                    source.clip = AudioCues.Tap;
                    source.pitch = Random.Range(0.96f, 1.04f);
                    source.volume = m_effectsVolume;
                    break;
                case SfxKind.Hover:
                    source.clip = AudioCues.Hover;
                    source.pitch = Random.Range(0.98f, 1.02f);
                    source.volume = m_effectsVolume * 0.5f;
                    break;
                case SfxKind.Dismiss:
                    source.clip = AudioCues.Dismiss;
                    source.pitch = 1f;
                    source.volume = m_effectsVolume * 0.8f;
                    break;
                default:
                    source.clip = AudioCues.Correct;
                    source.pitch = 1f;
                    source.volume = m_effectsVolume;
                    break;
            }
            source.Play();
        }

        // The microphone must hear only the learner: cut everything the moment it opens.
        private void OnTurnChanged(TurnState state)
        {
            if (state != TurnState.Listening) return;
            foreach (AudioSource source in m_pool) source.Stop();
            m_music.volume = 0f;
        }

        // Only the music fades, a few float operations a frame.
        private void Update()
        {
            bool wanted = m_settings != null && m_settings.Sound == SoundMode.All;
            float target = !wanted || MicOpen ? 0f
                : m_turn != null && m_turn.State == TurnState.Speaking ? m_musicVolume * m_duckedShare
                : m_musicVolume;
            if (wanted && !m_music.isPlaying) m_music.Play();
            // Ducks quickly so the word is clear from its start; comes back slowly.
            float rate = m_musicVolume / m_musicFadeSeconds * (target < m_music.volume ? 5f : 1f);
            m_music.volume = Mathf.MoveTowards(m_music.volume, target, Time.unscaledDeltaTime * rate);
            // Stopped (not just silent) once faded out, so a streamed clip isn't generating for nothing.
            if (!wanted && m_music.isPlaying && m_music.volume <= 0f) m_music.Stop();
        }
    }
}
