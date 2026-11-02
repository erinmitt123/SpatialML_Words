using UnityEngine;
using UnityEngine.XR;
using WordWorld.App;
using WordWorld.Data;
using WordWorld.Tracking;

namespace WordWorld.UI
{
    /// <summary>
    /// Recognition chip at the bottom of the view (DESIGN.md section 8): "Recognizing objects",
    /// tap to pause (it doubles as the privacy indicator), plus Windows (show or hide all) and
    /// Settings buttons, standing in for the hand menu, and Exit (tap twice). Says so when
    /// nothing has been detected for 20 s. Like the scanning hint it is a status line, not a window,
    /// so it re-centres in one step once the user has looked well away from it.
    /// Until the hand menu exists, the left controller's X button also opens Settings.
    /// </summary>
    [AddComponentMenu("Word World/Status Chip")]
    public sealed class StatusChip : MonoBehaviour
    {
        [SerializeField] private LearnerSettings m_settings;
        [SerializeField] private UiStrings m_strings;
        [SerializeField] private TrackingManager m_tracking;
        [SerializeField] private SettingsWindow m_settingsWindow;
        [SerializeField] private WindowRow m_windowRow;
        [SerializeField] private WordWorldTheme m_theme;

        [SerializeField] private GameObject m_chip;
        [SerializeField] private SpatialButton m_statusButton;
        [SerializeField] private SpatialButton m_settingsButton;
        [SerializeField] private SpatialButton m_windowsButton;
        [SerializeField] private SpatialButton m_exitButton;
        [Tooltip("How long Exit waits for the confirming second tap.")]
        [SerializeField] private float m_exitConfirmSeconds = 3f;

        [SerializeField] private Vector3 m_offsetFromHead = new Vector3(0f, -0.42f, 1.0f);
        [SerializeField] private float m_recentreDegrees = 40f;
        [SerializeField] private float m_troubleAfterSeconds = 20f;
        [Tooltip("The status text's authored size in points (after the builder's window text scale).")]
        [SerializeField] private int m_statusPoints = 45;

        private string m_shownKey;
        private string m_shownLanguage;
        private float m_readySince = -1f;
        private bool m_xWasDown;
        private float m_exitArmedUntil = -1f;

        private void Awake()
        {
            m_chip.SetActive(false);
            m_statusButton.Clicked += _ => TogglePause();
            m_settingsButton.Clicked += _ => m_settingsWindow.Toggle();
            m_windowsButton.Clicked += _ => m_windowRow.Toggle();
            m_exitButton.Clicked += _ => OnExit();
        }

        private void Update()
        {
            bool ready = m_settings.SessionReady && m_settings.OnboardingDone;
            if (!ready) return;
            if (m_readySince < 0f) m_readySince = Time.unscaledTime;
            if (!m_chip.activeSelf)
            {
                Place();
                m_chip.SetActive(true);
            }
            KeepInView();
            Refresh();
            PollControllerShortcut();
        }

        private void TogglePause()
        {
            m_tracking.Paused = !m_tracking.Paused;
            Debug.Log($"[WordWorld.Status] recognition {(m_tracking.Paused ? "paused" : "resumed")}");
        }

        // The first tap arms Exit ("Exit?") so a stray poke while reaching past the chip does not quit.
        private void OnExit()
        {
            if (Time.unscaledTime < m_exitArmedUntil)
            {
                Debug.Log("[WordWorld.Status] exit");
                Application.Quit();
                return;
            }
            m_exitArmedUntil = Time.unscaledTime + m_exitConfirmSeconds;
            m_shownKey = null;
        }

        private void Refresh()
        {
            float now = Time.unscaledTime;
            bool armed = now < m_exitArmedUntil;
            if (!armed && m_exitArmedUntil > 0f)
            {
                m_exitArmedUntil = -1f;
                m_shownKey = null;
            }
            float lastSeen = Mathf.Max(m_tracking.LastDetectionTime, m_readySince);
            string key = m_tracking.Paused ? "status.paused"
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
            status.TextColor = key == "status.paused" ? m_theme.InkMuted : key == "status.trouble" ? m_theme.Ink : m_theme.Accent;
            status.BackgroundColor = key == "status.trouble" ? m_theme.Hint : m_theme.Card;
            TextFit.FitBackground(status, text, m_statusPoints, 0.05f);
            m_settingsButton.Text.Text = m_strings.Get("settings.title", ui);
            m_settingsButton.Text.TextColor = m_theme.Ink;
            m_windowsButton.Text.Text = m_strings.Get("status.windows", ui);
            m_windowsButton.Text.TextColor = m_theme.Ink;
            m_exitButton.Text.Text = m_strings.Get(armed ? "status.exit_confirm" : "status.exit", ui);
            m_exitButton.Text.TextColor = armed ? m_theme.Wrong : m_theme.Ink;
        }

        private void Place() => WindowPlacement.PlaceInFront(m_chip.transform, m_offsetFromHead);

        private void KeepInView() => WindowPlacement.KeepInView(m_chip.transform, m_offsetFromHead, m_recentreDegrees);

        private void PollControllerShortcut()
        {
            InputDevice left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            bool down = left.isValid && left.TryGetFeatureValue(CommonUsages.primaryButton, out bool pressed) && pressed;
            if (down && !m_xWasDown) m_settingsWindow.Toggle();
            m_xWasDown = down;
        }
    }
}
