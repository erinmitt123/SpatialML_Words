using ByteDance.PICO.SpatialAdapter.Component;
using UnityEngine;
using UnityEngine.XR;
using WordWorld.App;
using WordWorld.Data;
using WordWorld.Labels;

namespace WordWorld.UI
{
    /// <summary>
    /// The one menu (DESIGN.md section 5), and the owner of the current mode. Progressive disclosure:
    /// the main page offers only Journey and Practice; Practice opens a second page with the
    /// exercises. Only one window is open at a time; the dock's Home button (or the left controller's
    /// X) brings this menu back from anywhere. First launch goes straight into the Journey.
    /// </summary>
    [AddComponentMenu("Word World/Home Menu")]
    public sealed class HomeMenu : MonoBehaviour
    {
        public enum Mode { None, Menu, Journey, Explore, FindIt, SayIt, WordBank, Progress, Settings }

        [SerializeField] private LearnerSettings m_settings;
        [SerializeField] private UiStrings m_strings;
        [SerializeField] private WordBank m_wordBank;
        [SerializeField] private ObjectLabels m_labels;
        [SerializeField] private WordWorldTheme m_theme;

        [Header("Modes")]
        [SerializeField] private JourneyCard m_journey;
        [SerializeField] private FindItQuiz m_quiz;
        [SerializeField] private SayItCard m_sayIt;
        [SerializeField] private WordBankWindow m_wordBankWindow;
        [SerializeField] private ProgressWindow m_progressWindow;
        [SerializeField] private SettingsWindow m_settingsWindow;
        [SerializeField] private DetailCard m_detailCard;

        [Header("Menu window")]
        [SerializeField] private GameObject m_window;
        [SerializeField] private SpatialAdapterNativeText m_title;
        [SerializeField] private SpatialAdapterNativeText m_subtitle;
        [SerializeField] private GameObject m_mainPage;
        [SerializeField] private GameObject m_practicePage;
        [Tooltip("Main page: journey, practice, progress, settings, exit. Practice page: back, explore, find_it, say_it, word_bank, round:5, round:10.")]
        [SerializeField] private SpatialButton[] m_buttons = System.Array.Empty<SpatialButton>();
        [SerializeField] private SpatialAdapterNativeText m_roundLabel;

        [SerializeField] private Vector3 m_menuOffset = new Vector3(0f, -0.08f, 0.9f);
        [SerializeField] private Vector3 m_windowOffset = new Vector3(0f, -0.1f, 1.0f);
        [SerializeField] private float m_exitConfirmSeconds = 3f;

        private Mode m_mode = Mode.None;
        private bool m_practice;
        private int m_roundLength = 10;
        private float m_exitArmedUntil = -1f;
        private bool m_xWasDown;

        public Mode Current => m_mode;

        private void Awake()
        {
            m_window.SetActive(false);
            foreach (SpatialButton button in m_buttons) button.Clicked += OnClicked;
        }

        private void OnEnable()
        {
            m_settings.Changed += Refresh;
            m_quiz.Closed += OnQuizClosed;
            m_sayIt.Closed += OnQuizClosed;
        }

        private void OnDisable()
        {
            m_settings.Changed -= Refresh;
            m_quiz.Closed -= OnQuizClosed;
            m_sayIt.Closed -= OnQuizClosed;
        }

        private void Update()
        {
            if (!m_settings.SessionReady || !m_settings.OnboardingDone) return;
            // Nothing is placed until the head is tracked, so the menu lands in front of the learner.
            if (m_mode == Mode.None && !WordWorld.Spatial.HeadPose.TryGet(out _)) return;
            // A brand-new learner starts the Journey right away; returning learners get the menu.
            if (m_mode == Mode.None)
            {
                if (m_wordBank.Words.Count == 0) Enter(Mode.Journey);
                else OpenMenu();
            }
            if (m_mode == Mode.Settings && !m_settingsWindow.IsOpen) OpenMenu();
            if (m_exitArmedUntil > 0f && Time.unscaledTime > m_exitArmedUntil)
            {
                m_exitArmedUntil = -1f;
                Refresh();
            }
            PollController();
        }

        /// <summary>Home: closes whatever is open and shows the main page in front of the learner.</summary>
        public void OpenMenu()
        {
            CloseAll();
            m_mode = Mode.Menu;
            m_practice = false;
            WindowPlacement.PlaceInFront(m_window.transform, m_menuOffset);
            m_window.SetActive(true);
            Refresh();
        }

        private void CloseAll()
        {
            m_window.SetActive(false);
            m_journey.Hide();
            m_quiz.Stop();
            m_sayIt.Stop();
            m_wordBankWindow.Window.SetActive(false);
            m_progressWindow.Window.SetActive(false);
            m_settingsWindow.Close();
            m_detailCard.Close();
            m_exitArmedUntil = -1f;
        }

        private void Enter(Mode mode)
        {
            CloseAll();
            m_mode = mode;
            Debug.Log($"[WordWorld.Home] mode {mode}");
            switch (mode)
            {
                case Mode.Journey:
                    m_journey.Show();
                    break;
                case Mode.FindIt:
                    m_quiz.Begin(m_roundLength);
                    break;
                case Mode.SayIt:
                    // Practice has no pass mark; the summary just lists words to practise.
                    m_sayIt.BeginRound(m_roundLength, 0);
                    break;
                case Mode.WordBank:
                    WindowPlacement.PlaceInFront(m_wordBankWindow.Window.transform, m_windowOffset);
                    m_wordBankWindow.Show();
                    break;
                case Mode.Progress:
                    WindowPlacement.PlaceInFront(m_progressWindow.Window.transform, m_windowOffset);
                    m_progressWindow.Show();
                    break;
                case Mode.Settings:
                    m_settingsWindow.Open();
                    break;
                // Explore: the labels are the whole exercise; nothing else is shown.
            }
        }

        // After a Journey round (Find It or Say It) the card comes back; after a Practice round, the menu.
        private void OnQuizClosed()
        {
            if (m_mode == Mode.Journey) m_journey.Show();
            else OpenMenu();
        }

        private void OnClicked(SpatialButton button)
        {
            switch (button.Value)
            {
                case "journey": Enter(Mode.Journey); break;
                case "practice": m_practice = true; Refresh(); break;
                case "back": m_practice = false; Refresh(); break;
                case "progress": Enter(Mode.Progress); break;
                case "settings": Enter(Mode.Settings); break;
                case "explore": Enter(Mode.Explore); break;
                case "find_it": Enter(Mode.FindIt); break;
                case "say_it": Enter(Mode.SayIt); break;
                case "word_bank": Enter(Mode.WordBank); break;
                case "round:5": m_roundLength = 5; Refresh(); break;
                case "round:10": m_roundLength = 10; Refresh(); break;
                case "exit": OnExit(); break;
            }
        }

        // The first tap arms Exit so a stray poke does not quit.
        private void OnExit()
        {
            if (Time.unscaledTime < m_exitArmedUntil)
            {
                Debug.Log("[WordWorld.Home] exit");
                Application.Quit();
                return;
            }
            m_exitArmedUntil = Time.unscaledTime + m_exitConfirmSeconds;
            Refresh();
        }

        private void Refresh()
        {
            if (!m_window.activeSelf || !m_settings.HasLanguage) return;
            m_mainPage.SetActive(!m_practice);
            m_practicePage.SetActive(m_practice);

            m_title.Text = S(m_practice ? "practice.title" : "home.title");
            m_title.TextColor = m_theme.Ink;
            m_subtitle.Text = string.Format(S("home.subtitle"), S($"lang.{m_settings.TargetLanguage}"), m_settings.ReadingLevel);
            m_subtitle.TextColor = m_theme.InkMuted;
            m_subtitle.gameObject.SetActive(!m_practice);
            m_roundLabel.Text = S("practice.round");
            m_roundLabel.TextColor = m_theme.InkMuted;

            bool armed = Time.unscaledTime < m_exitArmedUntil;
            foreach (SpatialButton button in m_buttons)
            {
                string value = button.Value;
                Color background = m_theme.Card;
                string text;
                switch (value)
                {
                    case "journey": text = S("home.journey"); background = m_theme.Primary; break;
                    case "practice": text = S("home.practice"); break;
                    case "exit":
                        text = S(armed ? "status.exit_confirm" : "status.exit");
                        if (armed) background = m_theme.Wrong;
                        break;
                    case "settings": text = S("settings.title"); break;
                    case "round:5":
                    case "round:10":
                        int length = value == "round:5" ? 5 : 10;
                        text = string.Format(S("practice.round_n"), length);
                        if (length == m_roundLength) background = m_theme.AccentSoft;
                        break;
                    case "progress": text = S("progress.title"); break;
                    case "back": text = S("common.back"); break;
                    default: text = S($"practice.{value}"); break; // explore, find_it, say_it, word_bank
                }
                // Say It only where this build can check speech in the learner's language.
                if (value == "say_it") button.gameObject.SetActive(m_sayIt.Available);
                button.Text.Text = text;
                button.Text.TextColor = m_theme.Ink;
                button.Text.BackgroundColor = background;
            }
        }

        private void PollController()
        {
            InputDevice left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            bool down = left.isValid && left.TryGetFeatureValue(CommonUsages.primaryButton, out bool pressed) && pressed;
            if (down && !m_xWasDown) OpenMenu();
            m_xWasDown = down;
        }

        private string S(string key) => m_strings.Get(key, m_settings.InterfaceLanguage);
    }
}
