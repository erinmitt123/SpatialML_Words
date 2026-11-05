using System;
using UnityEngine;
using WordWorld.Spatial;
using WordWorld.Speech;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace WordWorld.Audio
{
    /// <summary>Who has the floor: the app speaking, the learner speaking, or nobody.</summary>
    public enum TurnState { Idle, Speaking, Listening, Checking }

    /// <summary>
    /// The only owner of the speaker and the microphone, so the app can never talk and listen at once
    /// (docs/audio-plan.md, "Turn-taking"). Playing a clip moves the voice to the object and the
    /// listener to the user's head; the turn stays Speaking until the clip ends plus a short guard, so
    /// the microphone never hears the tail of a clip. The microphone opens only from
    /// <see cref="Listen"/>, which UI calls from a Speak tap and nothing else.
    /// </summary>
    [AddComponentMenu("Word World/Audio Turn")]
    public sealed class AudioTurn : MonoBehaviour
    {
        [SerializeField] private AudioSource m_voice;
        [Tooltip("Follows the head while a clip plays. In spatial mode the scene camera is the window " +
                 "camera, not the user's ears, so the listener can't live on it.")]
        [SerializeField] private Transform m_listener;
        [SerializeField] private SpeechCheck m_speech;
        [SerializeField] private SpeechData m_speechData;
        [SerializeField] private float m_guardSeconds = 0.3f;
        [Tooltip("Listening stops after this much quiet once the learner has started speaking.")]
        [SerializeField] private float m_endSilenceSeconds = 1f;
        [SerializeField] private int m_preferredSampleRate = 16000;

        /// <summary>Why a listen ended without a score.</summary>
        public enum ListenProblem { None, NoPermission, NotReady, Failed }

        private TurnState m_state;
        private AudioClip m_pending;
        private float m_quietAt;

        // Listening.
        private SpeechTarget m_target;
        private Action<SpeechScore, ListenProblem> m_done;
        private float m_maxSeconds;
        private bool m_listenQueued;
        private AudioClip m_recording;
        private int m_sampleRate;
        private float m_listenStarted;
        private int m_readPosition;
        private float m_noiseFloor;
        private bool m_heardSpeech;
        private float m_lastLoud;
        private readonly float[] m_levelBuffer = new float[1024];

        public TurnState State => m_state;
        public event Action<TurnState> StateChanged;

        /// <summary>Plays a clip from a point in the room. Interrupts a clip, and cancels listening.</summary>
        public bool Speak(AudioClip clip, Vector3 position)
        {
            if (clip == null || m_voice == null) return false;
            CancelListening();
            m_voice.Stop();
            m_voice.transform.position = position;
            FollowHead();
            m_pending = clip;
            m_quietAt = float.MaxValue;
            // Clips don't preload; ask for the data now and start once it is in memory.
            if (clip.loadState != AudioDataLoadState.Loaded) clip.LoadAudioData();
            SetState(TurnState.Speaking);
            Debug.Log($"[WordWorld.Audio] speak {clip.name} at {position}");
            return true;
        }

        /// <summary>Stops a clip or listening at once (Home, closing a card). The guard still applies.</summary>
        public void Stop()
        {
            CancelListening();
            if (m_state != TurnState.Speaking) return;
            m_voice.Stop();
            m_pending = null;
            m_quietAt = Mathf.Min(m_quietAt, Time.unscaledTime + m_guardSeconds);
        }

        /// <summary>
        /// Starts listening for <paramref name="target"/> — only ever from the learner's Speak tap. If a
        /// clip is playing it is stopped and the microphone opens after the guard. Tapping Speak again
        /// while listening finishes early (<see cref="FinishListening"/>).
        /// <paramref name="done"/> gets the score, or null and the reason.
        /// </summary>
        public void Listen(SpeechTarget target, float maxSeconds, Action<SpeechScore, ListenProblem> done)
        {
            if (m_state == TurnState.Listening || m_state == TurnState.Checking) return;
            if (m_speech == null || !m_speech.IsReady(target.Language))
            {
                done(null, ListenProblem.NotReady);
                return;
            }
            m_target = target;
            m_done = done;
            m_maxSeconds = maxSeconds;
            if (m_state == TurnState.Speaking)
            {
                m_voice.Stop();
                m_pending = null;
                m_quietAt = Mathf.Min(m_quietAt, Time.unscaledTime + m_guardSeconds);
                m_listenQueued = true; // opened from Update once the guard has passed
                return;
            }
            RequestMicrophone();
        }

        /// <summary>The learner tapped again: stop recording now and check what was said.</summary>
        public void FinishListening()
        {
            if (m_state == TurnState.Listening) EndRecording();
        }

        private void RequestMicrophone()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // DESIGN: permission is asked for on the first Speak tap, never at launch.
            if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
            {
                var callbacks = new PermissionCallbacks();
                callbacks.PermissionGranted += _ => StartRecording();
                callbacks.PermissionDenied += _ => Complete(null, ListenProblem.NoPermission);
                callbacks.PermissionDeniedAndDontAskAgain += _ => Complete(null, ListenProblem.NoPermission);
                Permission.RequestUserPermission(Permission.Microphone, callbacks);
                return;
            }
#endif
            StartRecording();
        }

        private void StartRecording()
        {
            if (m_done == null) return; // cancelled while the permission dialog was up
            if (Microphone.devices.Length == 0)
            {
                Complete(null, ListenProblem.Failed);
                return;
            }
            Microphone.GetDeviceCaps(null, out int min, out int max);
            m_sampleRate = min == 0 && max == 0 ? m_preferredSampleRate : Mathf.Clamp(m_preferredSampleRate, min, max);
            m_recording = Microphone.Start(null, false, Mathf.CeilToInt(m_maxSeconds) + 1, m_sampleRate);
            if (m_recording == null)
            {
                Complete(null, ListenProblem.Failed);
                return;
            }
            m_listenStarted = Time.unscaledTime;
            m_readPosition = 0;
            m_noiseFloor = -1f;
            m_heardSpeech = false;
            m_lastLoud = Time.unscaledTime;
            SetState(TurnState.Listening);
            Debug.Log($"[WordWorld.Audio] microphone on ({m_sampleRate} Hz, up to {m_maxSeconds:0} s)");
        }

        private void Update()
        {
            switch (m_state)
            {
                case TurnState.Speaking: UpdateSpeaking(); break;
                case TurnState.Listening: UpdateListening(); break;
            }
        }

        private void UpdateSpeaking()
        {
            FollowHead();
            if (m_pending != null)
            {
                switch (m_pending.loadState)
                {
                    case AudioDataLoadState.Loaded:
                        m_voice.clip = m_pending;
                        m_voice.Play();
                        m_pending = null;
                        break;
                    case AudioDataLoadState.Failed:
                        Debug.LogWarning($"[WordWorld.Audio] could not load {m_pending.name}");
                        m_pending = null;
                        m_quietAt = Time.unscaledTime;
                        break;
                }
                return;
            }
            if (m_quietAt == float.MaxValue && !m_voice.isPlaying) m_quietAt = Time.unscaledTime + m_guardSeconds;
            if (Time.unscaledTime < m_quietAt) return;
            SetState(TurnState.Idle);
            if (m_listenQueued)
            {
                m_listenQueued = false;
                RequestMicrophone();
            }
        }

        // Cheap end-of-speech detection: the level of the samples recorded since the last frame,
        // against the room's noise floor measured at the start.
        private void UpdateListening()
        {
            float elapsed = Time.unscaledTime - m_listenStarted;
            int position = Microphone.GetPosition(null);
            while (position - m_readPosition >= m_levelBuffer.Length)
            {
                m_recording.GetData(m_levelBuffer, m_readPosition);
                m_readPosition += m_levelBuffer.Length;
                float sum = 0f;
                foreach (float s in m_levelBuffer) sum += s * s;
                float rms = Mathf.Sqrt(sum / m_levelBuffer.Length);
                if (m_noiseFloor < 0f) m_noiseFloor = rms;
                else if (!m_heardSpeech) m_noiseFloor = Mathf.Min(m_noiseFloor, rms);
                if (rms > Mathf.Max(0.02f, m_noiseFloor * 3f))
                {
                    m_heardSpeech = true;
                    m_lastLoud = Time.unscaledTime;
                }
            }
            bool quietAfterSpeech = m_heardSpeech && Time.unscaledTime - m_lastLoud >= m_endSilenceSeconds;
            if (quietAfterSpeech || elapsed >= m_maxSeconds || !Microphone.IsRecording(null)) EndRecording();
        }

        private void EndRecording()
        {
            int count = Mathf.Min(Microphone.GetPosition(null), m_recording.samples);
            if (count <= 0) count = m_recording.samples;
            Microphone.End(null);
            var samples = new float[count];
            m_recording.GetData(samples, 0);
            Destroy(m_recording);
            m_recording = null;
            var pcm = new short[count];
            for (int i = 0; i < count; i++) pcm[i] = (short)(Mathf.Clamp(samples[i], -1f, 1f) * 32767f);
            Debug.Log($"[WordWorld.Audio] microphone off ({count / (float)m_sampleRate:0.0} s)");

            SetState(TurnState.Checking);
            SpeechTarget target = m_target;
            m_speech.Recognize(pcm, count, m_sampleRate, m_speechData.Grammar(target), json =>
            {
                if (m_state != TurnState.Checking || m_target != target) return; // cancelled meanwhile
                if (json == null)
                {
                    Complete(null, ListenProblem.Failed);
                    return;
                }
                SpeechScore score = SpeechScorer.Score(target, VoskJson.Words(json));
                Debug.Log($"[WordWorld.Audio] heard '{score.HeardText}', passed {score.Passed}");
                Complete(score, ListenProblem.None);
            });
        }

        private void CancelListening()
        {
            m_listenQueued = false;
            if (m_state == TurnState.Listening)
            {
                Microphone.End(null);
                if (m_recording != null) Destroy(m_recording);
                m_recording = null;
            }
            m_target = null;
            m_done = null;
            if (m_state == TurnState.Listening || m_state == TurnState.Checking) SetState(TurnState.Idle);
        }

        private void Complete(SpeechScore score, ListenProblem problem)
        {
            Action<SpeechScore, ListenProblem> done = m_done;
            m_done = null;
            m_target = null;
            SetState(TurnState.Idle);
            done?.Invoke(score, problem);
        }

        private void FollowHead()
        {
            if (m_listener != null && HeadPose.TryGet(out Pose head))
                m_listener.SetPositionAndRotation(head.position, head.rotation);
        }

        private void SetState(TurnState state)
        {
            if (m_state == state) return;
            m_state = state;
            StateChanged?.Invoke(state);
        }

        private void OnDisable() => CancelListening();
    }
}
