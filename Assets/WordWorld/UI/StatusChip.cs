using UnityEngine;
using WordWorld.App;
using WordWorld.Audio;
using WordWorld.Data;
using WordWorld.Tracking;

namespace WordWorld.UI
{
    /// <summary>
    /// The dock low in the view (DESIGN.md section 8): a Home button and the recognition pill. Home
    /// is the one way back to the menu from any mode. The pill says "Recognizing objects" (it doubles
    /// as the privacy indicator), pauses on tap, and says so when nothing has been seen for 20 s.
    /// Like the scanning hint it is a status line, not a window, so it re-centres in one step once
    /// the learner has looked well away from it.
    /// </summary>
    [AddComponentMenu("Word World/Status Chip")]
    public sealed class StatusChip : MonoBehaviour
    {
        [SerializeField] private LearnerSettings m_settings;
        [SerializeField] private UiStrings m_strings;
        [SerializeField] private TrackingManager m_tracking;
        [SerializeField] private HomeMenu m_home;
        [SerializeField] private AudioTurn m_turn;
        [SerializeField] private WordWorldTheme m_theme;

        [SerializeField] private GameObject m_chip;
        [SerializeField] private SpatialButton m_statusButton;
        [SerializeField] private SpatialButton m_homeButton;

        [SerializeField] private Vector3 m_offsetFromHead = new Vector3(0f, -0.42f, 0.9f);
        [SerializeField] private float m_recentreDegrees = 40f;
        [SerializeField] private float m_troubleAfterSeconds = 20f;
        [Tooltip("The status text's authored size in points (after the builder's window text scale).")]
        [SerializeField] private int m_statusPoints = 39;

        private string m_shownKey;
        private string m_shownLanguage;
        private float m_readySince = -1f;

        private void Awake()
        {
            m_chip.SetActive(false);
            m_statusButton.Clicked += _ => TogglePause();
            m_homeButton.Clicked += _ => m_home.OpenMenu();
        }

        private void Update()
        {
            bool ready = m_settings.SessionReady && m_settings.OnboardingDone;
            if (!ready) return;
            if (m_readySince < 0f) m_readySince = Time.unscaledTime;
            if (!m_chip.activeSelf)
            {
                // Wait for a tracked head so the dock lands in view.
                if (!Place()) return;
                m_chip.SetActive(true);
            }
            KeepInView();
            Refresh();
        }

        private void TogglePause()
        {
            // While listening the pill is the microphone indicator; tapping it stops listening.
            if (m_turn != null && m_turn.State == TurnState.Listening)
            {
                m_turn.FinishListening();
                return;
            }
            m_tracking.Paused = !m_tracking.Paused;
            Debug.Log($"[WordWorld.Status] recognition {(m_tracking.Paused ? "paused" : "resumed")}");
        }

        private void Refresh()
        {
            float now = Time.unscaledTime;
            float lastSeen = Mathf.Max(m_tracking.LastDetectionTime, m_readySince);
            // While the microphone is open the pill says so: it is the privacy indicator for both
            // the camera and the microphone (docs/audio-plan.md, "Privacy").
            bool micOn = m_turn != null && m_turn.State == TurnState.Listening;
            string key = micOn ? "dock.mic_on"
                : m_tracking.Paused ? "status.paused"
                : now - lastSeen > m_troubleAfterSeconds ? "status.trouble"
                : "status.recognizing";
            string ui = m_settings.InterfaceLanguage;
            if (key == m_shownKey && ui == m_shownLanguage) return;
            if (key != m_shownKey)
                Debug.Log($"[WordWorld.Status] {key} (last detection {now - m_tracking.LastDetectionTime:0.0}s ago)");
            m_shownKey = key;
            m_shownLanguage = ui;

            string text = m_strings.Get(key, ui);
            var status = m_statusButton.Text;
            status.Text = text;
            // Colour never carries the state alone: the words change too.
            status.TextColor = key == "status.paused" ? m_theme.InkMuted : m_theme.Ink;
            status.BackgroundColor = micOn ? m_theme.Primary
                : key == "status.trouble" ? m_theme.Hint : key == "status.paused" ? m_theme.Card : m_theme.AccentSoft;
            TextFit.FitBackground(status, text, m_statusPoints, 0.045f);
            m_homeButton.Text.Text = m_strings.Get("dock.home", ui);
            m_homeButton.Text.TextColor = m_theme.Ink;
        }

        private bool Place() => WindowPlacement.PlaceInFront(m_chip.transform, m_offsetFromHead);

        private void KeepInView() => WindowPlacement.KeepInView(m_chip.transform, m_offsetFromHead, m_recentreDegrees);
    }
}
