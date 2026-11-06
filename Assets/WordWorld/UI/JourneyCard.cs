using ByteDance.PICO.SpatialAdapter.Component;
using UnityEngine;
using WordWorld.App;
using WordWorld.Data;

namespace WordWorld.UI
{
    /// <summary>
    /// Journey mode (DESIGN.md section 5): a guided loop through the reading levels, shown as one small
    /// card low and to the left so the room and its labels stay in front. Each level has three steps:
    /// read labels (tap them), pass a Find It round, then say five words out loud (docs/audio-plan.md,
    /// "Journey: Say step"; skipped where Say It isn't available). Passing moves up a level. Progress
    /// is saved, so the Journey continues where the learner left it.
    /// </summary>
    [AddComponentMenu("Word World/Journey Card")]
    public sealed class JourneyCard : MonoBehaviour
    {
        [SerializeField] private LearnerSettings m_settings;
        [SerializeField] private UiStrings m_strings;
        [SerializeField] private WordBank m_wordBank;
        [SerializeField] private FindItQuiz m_quiz;
        [SerializeField] private SayItCard m_sayIt;
        [SerializeField] private WordWorldTheme m_theme;

        [SerializeField] private GameObject m_card;
        [SerializeField] private SpatialAdapterNativeText m_title;
        [SerializeField] private SpatialAdapterNativeText m_goal;
        [SerializeField] private SpatialAdapterNativeText m_barTrack;
        [SerializeField] private SpatialAdapterNativeText m_barFill;
        [SerializeField] private SpatialButton m_startButton;
        [SerializeField] private float m_barWidth = 0.28f;

        [Header("Rules")]
        [SerializeField] private int m_tapsPerLevel = 5;
        [SerializeField] private int m_roundLength = 10;
        [SerializeField] private int m_passScore = 8;
        [SerializeField] private int m_sayCount = 5;
        [SerializeField] private int m_sayPass = 4;

        [Header("Placement")]
        [Tooltip("Low and to the left: in view, but never between the learner and the room.")]
        [SerializeField] private Vector3 m_offset = new Vector3(-0.34f, -0.24f, 0.95f);
        [SerializeField] private float m_recentreDegrees = 50f;

        // Shown once after a round, until the next step's goal replaces it.
        private string m_messageKey;
        private float m_shownFill = -1f;

        public bool IsOpen => m_card.activeSelf;

        private void Awake()
        {
            m_card.SetActive(false);
            m_startButton.Clicked += _ => StartRound();
        }

        private void OnEnable()
        {
            m_wordBank.Changed += OnWordBankChanged;
            m_settings.Changed += Refresh;
            m_quiz.RoundFinished += OnRoundFinished;
            m_sayIt.RoundFinished += OnSayFinished;
        }

        private void OnDisable()
        {
            m_wordBank.Changed -= OnWordBankChanged;
            m_settings.Changed -= Refresh;
            m_quiz.RoundFinished -= OnRoundFinished;
            m_sayIt.RoundFinished -= OnSayFinished;
        }

        public void Show()
        {
            if (!m_card.activeSelf) WindowPlacement.PlaceInFront(m_card.transform, m_offset);
            m_card.SetActive(true);
            m_shownFill = -1f;
            Refresh();
        }

        public void Hide()
        {
            m_card.SetActive(false);
            m_messageKey = null;
        }

        private void Update()
        {
            if (m_card.activeSelf) WindowPlacement.KeepInView(m_card.transform, m_offset, m_recentreDegrees);
        }

        private int TapsThisStep => Mathf.Max(0, m_wordBank.TotalTaps - m_settings.JourneyBaseTaps);

        private void OnWordBankChanged()
        {
            if (m_settings.JourneyStep == 0 && TapsThisStep >= m_tapsPerLevel)
            {
                m_messageKey = null;
                m_settings.SetJourney(1, m_settings.JourneyBaseTaps);
            }
            if (m_card.activeSelf) Refresh();
        }

        private void StartRound()
        {
            // The card steps aside during the round; the results card says how it went.
            m_card.SetActive(false);
            if (m_settings.JourneyStep == 2) m_sayIt.BeginRound(m_sayCount, m_sayPass);
            else m_quiz.Begin(m_roundLength);
        }

        private void OnRoundFinished(int score, int asked)
        {
            if (m_settings.JourneyStep != 1 || asked < m_roundLength) return;
            if (score < m_passScore)
            {
                m_messageKey = "journey.try_again";
                return;
            }
            Debug.Log($"[WordWorld.Journey] Find It passed at level {m_settings.ReadingLevel} with {score}/{asked}");
            if (m_sayIt.Available)
            {
                m_messageKey = "journey.find_passed";
                m_settings.SetJourney(2, m_settings.JourneyBaseTaps);
            }
            else LevelUp();
        }

        private void OnSayFinished(int passed, int asked)
        {
            if (m_settings.JourneyStep != 2) return;
            if (!m_sayIt.LastRoundPassed)
            {
                m_messageKey = "journey.try_again";
                return;
            }
            Debug.Log($"[WordWorld.Journey] Say It passed at level {m_settings.ReadingLevel} with {passed}/{asked}");
            LevelUp();
        }

        private void LevelUp()
        {
            int level = m_settings.ReadingLevel;
            m_messageKey = level < 3 ? "journey.level_up" : "journey.complete";
            m_settings.SetJourney(0, m_wordBank.TotalTaps);
            if (level < 3) m_settings.SetReadingLevel(level + 1);
            Debug.Log($"[WordWorld.Journey] passed level {level}");
        }

        private void Refresh()
        {
            if (!m_card.activeSelf || !m_settings.HasLanguage) return;
            // Say It became unavailable (another language, or its model failed): the level is done.
            if (m_settings.JourneyStep == 2 && !m_sayIt.Available)
            {
                LevelUp();
                return;
            }
            int level = m_settings.ReadingLevel;
            int step = m_settings.JourneyStep;
            bool roundStep = step >= 1;
            m_title.Text = string.Format(S("journey.title"), level);
            m_title.TextColor = m_theme.Ink;

            string goal = step == 2 ? string.Format(S("journey.say"), m_sayCount, m_sayPass)
                : step == 1 ? string.Format(S("journey.quiz"), m_passScore, m_roundLength)
                : string.Format(S("journey.read"), m_tapsPerLevel, Mathf.Min(TapsThisStep, m_tapsPerLevel));
            if (!string.IsNullOrEmpty(m_messageKey)) goal = string.Format(S(m_messageKey), level) + "\n" + goal;
            m_goal.Text = goal;
            m_goal.TextColor = m_theme.Ink;

            m_startButton.gameObject.SetActive(roundStep);
            m_startButton.SetLabel(S(step == 2 ? "journey.start_say" : "journey.start"));
            m_startButton.Text.TextColor = m_theme.Ink;
            m_startButton.Text.BackgroundColor = m_theme.Primary;

            m_barTrack.gameObject.SetActive(!roundStep);
            SetFill(roundStep ? 0f : (float)Mathf.Min(TapsThisStep, m_tapsPerLevel) / m_tapsPerLevel);
        }

        private void SetFill(float fraction)
        {
            if (Mathf.Approximately(fraction, m_shownFill)) return;
            m_shownFill = fraction;
            m_barFill.gameObject.SetActive(fraction > 0f);
            float width = Mathf.Max(0.001f, m_barWidth * fraction);
            m_barFill.BackgroundSize = new Vector2(width, 0.012f);
            Vector3 p = m_barFill.transform.localPosition;
            m_barFill.transform.localPosition = new Vector3(-m_barWidth * 0.5f + width * 0.5f, p.y, p.z);
        }

        private string S(string key) => m_strings.Get(key, m_settings.InterfaceLanguage);
    }
}
