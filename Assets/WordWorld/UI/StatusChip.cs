using UnityEngine;
using WordWorld.App;
using WordWorld.Audio;
using WordWorld.Data;
using WordWorld.Spatial;
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
        [SerializeField] private RecognitionNotice m_notice;

        [SerializeField] private GameObject m_chip;
        [SerializeField] private SpatialButton m_statusButton;
        [SerializeField] private SpatialButton m_homeButton;

        public SpatialButton StatusButton => m_statusButton;
        public SpatialButton HomeButton => m_homeButton;
        /// <summary>The whole dock (pill with Home under it), for things placed beside it.</summary>
        public Transform Dock => m_chip.transform;
        /// <summary>Half the dock's width: the pill is 26 cm wide, Home under it narrower.</summary>
        public const float HalfWidth = 0.13f;

        [SerializeField] private Vector3 m_offsetFromHead = new Vector3(-0.40f, -0.2f, 0.85f);
        [SerializeField] private float m_recentreDegrees = 40f;
        [SerializeField] private float m_troubleAfterSeconds = 20f;

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
            // The tour brings the dock in at the step that explains it, and only that step's button works.
            bool tour = Tour.Active;
            if (tour && Tour.ActiveIndex < Tour.StatusStep)
            {
                if (m_chip.activeSelf) m_chip.SetActive(false);
                return;
            }
            m_statusButton.Interactable = !tour || Tour.ActiveIndex == Tour.StatusStep;
            m_homeButton.Interactable = !tour || Tour.ActiveIndex == Tour.HomeStep;
            if (!m_chip.activeSelf)
            {
                // Wait for a tracked head so the dock lands in view.
                if (!Place()) return;
                m_chip.SetActive(true);
            }
            KeepInView();
            Refresh();
        }

        // Time asleep is not "nothing found": start the trouble clock again on waking.
        private void OnApplicationFocus(bool focused)
        {
            if (focused && m_readySince >= 0f) m_readySince = Time.unscaledTime;
        }

        private void TogglePause()
        {
            // While listening the pill is the microphone indicator; tapping it stops listening.
            if (m_turn != null && m_turn.State == TurnState.Listening)
            {
                m_turn.FinishListening();
                return;
            }
            // Recognition is down: the pill explains why instead of pausing something that is not running.
            if (m_notice != null && m_notice.RecognitionDown)
            {
                // Not over the tour (Nov 16 recording: the card covered the tour's caption and the
                // dock); RecognitionNotice shows it by itself once the tour ends.
                if (!Tour.Active) m_notice.Show();
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
                : m_notice != null && m_notice.RecognitionDown ? "status.down"
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
            // A fixed pill: long states wrap onto two lines or shrink rather than grow past the
            // background (Nov 16 recording: "Recognition off: tap for help" ran out of the pill).
            m_statusButton.SetLabel(text);
            // Colour never carries the state alone: the words change too.
            m_statusButton.Text.TextColor = key == "status.paused" ? m_theme.InkMuted : m_theme.Ink;
            m_statusButton.Background = micOn ? m_theme.Primary
                : key == "status.trouble" || key == "status.down" ? m_theme.Hint : key == "status.paused" ? m_theme.Button : m_theme.AccentSoft;
            m_homeButton.SetLabel(m_strings.Get("dock.home", ui));
            m_homeButton.Text.TextColor = m_theme.Ink;
        }

        private bool Place() => WindowPlacement.PlaceInFront(m_chip.transform, m_offsetFromHead);

        private void KeepInView()
        {
            WindowPlacement.KeepInView(m_chip.transform, m_offsetFromHead, m_recentreDegrees);
            AvoidPanels();
        }

        // The dock is placed from the head and windows open wherever the learner looks, so the two
        // could land on each other (Nov 16 recording: the pill and Home ran into the menu's left
        // edge). When an open window or card overlaps it, the dock slides around the learner to the
        // nearest clear spot that is still in view. Checked a few times a second, not every frame.
        private const float AvoidInterval = 0.25f, AvoidStepDegrees = 4f, AvoidMargin = 0.015f;
        private float m_nextAvoidCheck;
        private PanelOccluder m_occluder;

        private void AvoidPanels()
        {
            float now = Time.unscaledTime;
            if (now < m_nextAvoidCheck) return;
            m_nextAvoidCheck = now + AvoidInterval;
            // On its two steps the tour's card sits beside the dock on purpose; it keeps clear itself.
            Transform ignore = Tour.Active && (Tour.ActiveIndex == Tour.StatusStep || Tour.ActiveIndex == Tour.HomeStep)
                ? Tour.CardTransform : null;
            if (m_occluder == null && !m_chip.TryGetComponent(out m_occluder)) return;
            if (!HeadPose.TryGet(out Pose head)) return;
            if (m_occluder.FirstOverlap(head.position, AvoidMargin, ignore) == null) return;

            Transform dock = m_chip.transform;
            Vector3 start = dock.position;
            Quaternion startRotation = dock.rotation;
            Vector3 forward = head.rotation * Vector3.forward;
            forward.y = 0f;
            // Try left and right alternately, nearest first, staying short of the re-centre angle so
            // KeepInView does not throw it straight back.
            float limit = m_recentreDegrees - 5f;
            for (int i = 1; i * AvoidStepDegrees <= 2f * limit; i++)
            {
                float degrees = (i + 1) / 2 * AvoidStepDegrees * (i % 2 == 1 ? -1f : 1f);
                dock.SetPositionAndRotation(start, startRotation);
                dock.RotateAround(head.position, Vector3.up, degrees);
                Vector3 toDock = dock.position - head.position;
                toDock.y = 0f;
                if (Vector3.Angle(toDock, forward) > limit) continue;
                if (m_occluder.FirstOverlap(head.position, AvoidMargin, ignore) == null) return;
            }
            dock.SetPositionAndRotation(start, startRotation); // nowhere clear in view: stay put
        }
    }
}
