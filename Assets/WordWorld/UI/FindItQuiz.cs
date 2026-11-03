using System;
using System.Collections.Generic;
using System.Text;
using ByteDance.PICO.SpatialAdapter.Component;
using UnityEngine;
using WordWorld.App;
using WordWorld.Data;
using WordWorld.Labels;
using WordWorld.Spatial;
using WordWorld.Tracking;

namespace WordWorld.UI
{
    /// <summary>
    /// The Find It exercise (DESIGN.md section 6). Started from Practice or the Journey with a round
    /// length; during a round only the floating prompt is shown (word, score and timer), and the
    /// results card appears when it ends. Prompts are for objects confirmed this session. Labels hide and only dots stay; the learner touches the real
    /// object or pinches its dot. Correct: a green ring and the word. Wrong: the dot shakes and shows
    /// its own word; the first miss costs nothing. Hint glow after 8 s, Skip after 15 s.
    /// </summary>
    [AddComponentMenu("Word World/Find It Quiz")]
    public sealed class FindItQuiz : MonoBehaviour
    {
        private enum State { Idle, NotEnough, Playing, Results }

        /// <summary>Raised when a round ends normally, with the score and the number of prompts asked.</summary>
        public event Action<int, int> RoundFinished;
        /// <summary>Raised when the learner leaves the results (or not-enough) card.</summary>
        public event Action Closed;

        [SerializeField] private LearnerSettings m_settings;
        [SerializeField] private UiStrings m_strings;
        [SerializeField] private Vocabulary m_vocabulary;
        [SerializeField] private WordBank m_wordBank;
        [SerializeField] private ObjectLabels m_labels;
        [SerializeField] private DetailCard m_detailCard;
        [SerializeField] private WordWorldTheme m_theme;

        [Header("Results card")]
        [SerializeField] private GameObject m_window;
        [SerializeField] private SpatialAdapterNativeText m_title;
        [SerializeField] private SpatialAdapterNativeText m_body;
        [SerializeField] private SpatialButton m_primary;
        [SerializeField] private SpatialButton m_secondary;
        [SerializeField] private Vector3 m_windowOffset = new Vector3(0f, -0.1f, 1.0f);

        [Header("In the room")]
        [SerializeField] private GameObject m_floatingPrompt;
        [SerializeField] private SpatialAdapterNativeText m_floatingText;
        [SerializeField] private SpatialAdapterNativeText m_floatingStatus;
        [SerializeField] private SpatialAdapterNativeText m_timerTrack;
        [SerializeField] private SpatialAdapterNativeText m_timerFill;
        [SerializeField] private float m_timerWidth = 0.3f;
        [SerializeField] private SpatialButton m_skip;
        [SerializeField] private SpatialAdapterNativeText m_ring;
        [SerializeField] private SpatialAdapterNativeText m_glow;
        [SerializeField] private Vector3 m_promptOffset = new Vector3(0f, -0.12f, 0.6f);

        [Header("DESIGN.md section 6")]
        [SerializeField] private int m_roundLength = 10;
        [SerializeField] private int m_minObjects = 4;
        [SerializeField] private float m_hintAfterSeconds = 8f;
        [SerializeField] private float m_skipAfterSeconds = 15f;
        [SerializeField] private float m_nextPromptDelay = 1f;
        [SerializeField] private float m_wrongWordSeconds = 1.5f;

        private readonly List<TrackedObject> m_objects = new List<TrackedObject>();
        private readonly List<int> m_classes = new List<int>();
        private readonly List<int> m_review = new List<int>();
        private readonly StringBuilder m_builder = new StringBuilder(128);
        private State m_state = State.Idle;
        private int m_promptIndex;
        private int m_targetClass = -1;
        private int m_lastClass = -1;
        private int m_misses;
        private int m_score;
        private int m_firstTry;
        private int m_streak;
        private int m_bestThisRound;
        private float m_promptStart;
        private float m_nextPromptAt = -1f;
        private float m_ringStart = -1f;
        private float m_shownTimer = -1f;
        private TrackedObject m_hintTrack;

        public GameObject Window => m_window;
        public bool IsActive => m_state != State.Idle;

        private void Awake()
        {
            m_window.SetActive(false);
            m_floatingPrompt.SetActive(false);
            m_ring.gameObject.SetActive(false);
            m_glow.gameObject.SetActive(false);
            m_primary.Clicked += _ => OnPrimary();
            m_secondary.Clicked += _ => OnSecondary();
            m_skip.Clicked += _ => Skip();
        }

        private void OnEnable()
        {
            m_labels.ObjectPicked += OnPicked;
            m_settings.Changed += Refresh;
        }

        private void OnDisable()
        {
            m_labels.ObjectPicked -= OnPicked;
            m_settings.Changed -= Refresh;
        }

        /// <summary>Starts a round straight away (Practice or Journey).</summary>
        public void Begin(int roundLength)
        {
            m_roundLength = Mathf.Max(1, roundLength);
            StartRound();
        }

        /// <summary>Leaves the exercise without results (the dock's Home button).</summary>
        public void Stop()
        {
            if (m_state == State.Playing) EndRound(showResults: false);
            m_state = State.Idle;
            m_window.SetActive(false);
        }

        private void OnPrimary()
        {
            if (m_state == State.Results || m_state == State.NotEnough) StartRound();
        }

        private void OnSecondary()
        {
            Stop();
            Closed?.Invoke();
        }

        private void ShowWindow()
        {
            if (!m_window.activeSelf) WindowPlacement.PlaceInFront(m_window.transform, m_windowOffset);
            m_window.SetActive(true);
        }

        private void StartRound()
        {
            m_labels.GetLabelled(m_objects);
            m_classes.Clear();
            foreach (TrackedObject track in m_objects)
                if (!m_classes.Contains(track.ClassId) && m_vocabulary.TryGetLabel(track.ClassId, m_settings.TargetLanguage,
                        m_settings.ReadingLevel, m_settings.ShowPinyin, out _))
                    m_classes.Add(track.ClassId);
            // DESIGN.md: needs at least four recognized objects.
            if (m_objects.Count < m_minObjects || m_classes.Count < 2)
            {
                m_state = State.NotEnough;
                ShowWindow();
                Refresh();
                return;
            }
            m_window.SetActive(false);
            m_detailCard.Close();
            m_labels.QuizMode = true;
            m_state = State.Playing;
            m_promptIndex = 0;
            m_score = m_firstTry = m_streak = m_bestThisRound = 0;
            m_lastClass = -1;
            m_review.Clear();
            WindowPlacement.PlaceInFront(m_floatingPrompt.transform, m_promptOffset);
            m_floatingPrompt.SetActive(true);
            NextPrompt();
        }

        private void NextPrompt()
        {
            m_nextPromptAt = -1f;
            HideHint();
            if (m_promptIndex >= m_roundLength)
            {
                EndRound(showResults: true);
                return;
            }
            // Only objects still tracked; never the same word twice in a row.
            m_labels.GetLabelled(m_objects);
            m_classes.Clear();
            foreach (TrackedObject track in m_objects)
                if (track.ClassId != m_lastClass && !m_classes.Contains(track.ClassId) &&
                    m_vocabulary.TryGetLabel(track.ClassId, m_settings.TargetLanguage, m_settings.ReadingLevel, m_settings.ShowPinyin, out _))
                    m_classes.Add(track.ClassId);
            if (m_classes.Count == 0)
            {
                EndRound(showResults: true);
                return;
            }
            m_targetClass = m_classes[UnityEngine.Random.Range(0, m_classes.Count)];
            m_lastClass = m_targetClass;
            m_promptIndex++;
            m_misses = 0;
            m_promptStart = Time.unscaledTime;
            m_skip.gameObject.SetActive(false);
            Refresh();
        }

        private void OnPicked(TrackedObject track)
        {
            if (m_state != State.Playing || m_nextPromptAt > 0f) return;
            if (track.ClassId == m_targetClass)
            {
                // No penalty on the first miss.
                bool counts = m_misses <= 1;
                if (counts)
                {
                    m_score++;
                    m_streak++;
                    m_bestThisRound = Mathf.Max(m_bestThisRound, m_streak);
                }
                if (m_misses == 0) m_firstTry++;
                else if (!m_review.Contains(m_targetClass)) m_review.Add(m_targetClass);
                m_wordBank.RecordQuizAnswer(m_targetClass, counts, m_settings.ReadingLevel);
                StartRing(track.Position);
                m_labels.Flash(track, m_nextPromptDelay + 0.2f);
                m_nextPromptAt = Time.unscaledTime + m_nextPromptDelay;
                HideHint();
                Refresh();
                return;
            }
            // Wrong object: it still teaches its own word.
            m_misses++;
            if (m_misses == 2) m_streak = 0;
            m_labels.Shake(track);
            m_labels.Flash(track, m_wrongWordSeconds);
            Refresh();
        }

        private void Skip()
        {
            if (m_state != State.Playing || m_nextPromptAt > 0f) return;
            m_streak = 0;
            if (!m_review.Contains(m_targetClass)) m_review.Add(m_targetClass);
            m_wordBank.RecordQuizAnswer(m_targetClass, false, m_settings.ReadingLevel);
            if (m_labels.TryFindByClass(m_targetClass, out TrackedObject track)) m_labels.Flash(track, m_nextPromptDelay + 0.5f);
            m_nextPromptAt = Time.unscaledTime + m_nextPromptDelay;
        }

        private void EndRound(bool showResults)
        {
            m_labels.QuizMode = false;
            m_floatingPrompt.SetActive(false);
            HideHint();
            m_wordBank.RecordStreak(m_bestThisRound);
            m_state = showResults ? State.Results : State.Idle;
            if (!showResults) return;
            ShowWindow();
            Refresh();
            RoundFinished?.Invoke(m_score, m_promptIndex);
        }

        private void Update()
        {
            UpdateRing();
            if (m_state != State.Playing) return;
            float now = Time.unscaledTime;
            if (m_nextPromptAt > 0f)
            {
                if (now >= m_nextPromptAt) NextPrompt();
                return;
            }
            float elapsed = now - m_promptStart;
            if (elapsed > m_hintAfterSeconds) ShowHint(now);
            if (elapsed > m_skipAfterSeconds && !m_skip.gameObject.activeSelf)
            {
                m_skip.Text.Text = S("quiz.skip");
                m_skip.gameObject.SetActive(true);
            }
            UpdateTimer(1f - elapsed / m_skipAfterSeconds);
            WindowPlacement.KeepInView(m_floatingPrompt.transform, m_promptOffset, 45f);
        }

        // Steps of 5% keep native updates to a few a second.
        private void UpdateTimer(float fraction)
        {
            fraction = Mathf.Round(Mathf.Clamp01(fraction) * 20f) / 20f;
            if (Mathf.Approximately(fraction, m_shownTimer)) return;
            m_shownTimer = fraction;
            m_timerFill.gameObject.SetActive(fraction > 0f);
            float width = m_timerWidth * fraction;
            m_timerFill.BackgroundSize = new Vector2(Mathf.Max(width, 0.001f), 0.008f);
            Vector3 p = m_timerFill.transform.localPosition;
            m_timerFill.transform.localPosition = new Vector3(-m_timerWidth * 0.5f + width * 0.5f, p.y, p.z);
        }

        // A soft hint-coloured glow pulsing on the nearest object of the right kind.
        private void ShowHint(float now)
        {
            if (m_hintTrack == null && !m_labels.TryFindByClass(m_targetClass, out m_hintTrack)) return;
            if (!HeadPose.TryGet(out Pose head)) return;
            if (!m_glow.gameObject.activeSelf)
            {
                m_glow.BackgroundColor = new Color(m_theme.Hint.r, m_theme.Hint.g, m_theme.Hint.b, 0.45f);
                m_glow.gameObject.SetActive(true);
            }
            float scale = 1f + 0.25f * Mathf.Sin(now * 4f);
            Vector3 p = m_hintTrack.Position;
            m_glow.transform.SetPositionAndRotation(p, ObjectLabels.Billboard(head.position, p));
            m_glow.transform.localScale = new Vector3(scale, scale, 1f);
        }

        private void HideHint()
        {
            m_hintTrack = null;
            if (m_glow.gameObject.activeSelf) m_glow.gameObject.SetActive(false);
        }

        private void StartRing(Vector3 position)
        {
            if (!HeadPose.TryGet(out Pose head)) return;
            m_ring.transform.SetPositionAndRotation(position, ObjectLabels.Billboard(head.position, position));
            m_ringStart = Time.unscaledTime;
            m_ring.gameObject.SetActive(true);
        }

        // The green ring expands from the object and fades over 0.6 s.
        private void UpdateRing()
        {
            if (m_ringStart < 0f) return;
            float t = (Time.unscaledTime - m_ringStart) / 0.6f;
            if (t >= 1f)
            {
                m_ringStart = -1f;
                m_ring.gameObject.SetActive(false);
                return;
            }
            float eased = 1f - (1f - t) * (1f - t);
            float scale = 0.3f + 2.2f * eased;
            m_ring.transform.localScale = new Vector3(scale, scale, 1f);
            m_ring.BackgroundColor = new Color(m_theme.Correct.r, m_theme.Correct.g, m_theme.Correct.b, 0.7f * (1f - t));
        }

        private string PromptText()
        {
            if (!m_vocabulary.TryGetLabel(m_targetClass, m_settings.TargetLanguage, m_settings.ReadingLevel,
                    m_settings.ShowPinyin, out Vocabulary.LabelContent content)) return "";
            return string.IsNullOrEmpty(content.Ruby) ? content.Main : $"{content.Ruby}\n{content.Main}";
        }

        private void Refresh()
        {
            if (!m_settings.HasLanguage) return;
            m_title.Text = S("quiz.title");
            m_title.TextColor = m_theme.Ink;
            m_body.TextColor = m_theme.Ink;
            bool playing = m_state == State.Playing;
            if (!playing) m_timerFill.gameObject.SetActive(false);
            m_shownTimer = -1f;

            StyleButton(m_primary, true);
            StyleButton(m_secondary, false);
            StringBuilder b = m_builder.Clear();
            switch (m_state)
            {
                case State.Idle:
                    return;
                case State.NotEnough:
                    b.Append(S("quiz.not_enough"));
                    m_primary.Text.Text = S("quiz.try_again");
                    m_primary.gameObject.SetActive(true);
                    m_secondary.Text.Text = S("quiz.done");
                    m_secondary.gameObject.SetActive(true);
                    break;
                case State.Playing:
                    m_floatingText.Text = PromptText();
                    m_floatingText.TextColor = m_theme.Ink;
                    b.Append(string.Format(S("quiz.score"), m_score, m_promptIndex)).Append("   ")
                        .Append(string.Format(S("quiz.streak"), m_streak));
                    m_floatingStatus.Text = b.ToString();
                    m_floatingStatus.TextColor = m_theme.InkMuted;
                    return;
                case State.Results:
                    int accuracy = Mathf.RoundToInt(100f * m_firstTry / Mathf.Max(1, m_promptIndex));
                    b.Append(string.Format(S("quiz.score"), m_score, m_promptIndex)).Append("     ")
                        .Append(S("progress.accuracy")).Append(' ').Append(accuracy).Append('%');
                    if (m_review.Count > 0)
                    {
                        b.Append('\n').Append(S("quiz.review")).Append(' ');
                        // Two fit on the line; the Word Bank has the rest.
                        for (int i = 0; i < m_review.Count && i < 2; i++)
                        {
                            if (i > 0) b.Append(", ");
                            if (m_vocabulary.TryGetLabel(m_review[i], m_settings.TargetLanguage, 1, true, out Vocabulary.LabelContent c))
                                b.Append(c.Main);
                        }
                    }
                    m_primary.Text.Text = S("quiz.play_again");
                    m_primary.gameObject.SetActive(true);
                    m_secondary.Text.Text = S("quiz.done");
                    m_secondary.gameObject.SetActive(true);
                    break;
            }
            m_body.Text = b.ToString();
        }

        private void StyleButton(SpatialButton button, bool primary)
        {
            button.Text.TextColor = m_theme.Ink;
            button.Text.BackgroundColor = primary ? m_theme.Primary : m_theme.Card;
        }

        private string S(string key) => m_strings.Get(key, m_settings.InterfaceLanguage);
    }
}
