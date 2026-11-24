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
    /// the learner has looked well away from it. Move lets the learner put it somewhere else: it
    /// follows the head until tapped, and keeps that spot (relative to the way they face) from then on.
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
        [SerializeField] private SpatialButton m_moveButton;

        public SpatialButton StatusButton => m_statusButton;
        public SpatialButton HomeButton => m_homeButton;
        public SpatialButton MoveButton => m_moveButton;
        /// <summary>True while the dock follows the head, waiting to be tapped into place.</summary>
        public bool Moving => m_moving;
        /// <summary>The whole dock (pill with Home under it), for things placed beside it.</summary>
        public Transform Dock => m_chip.transform;
        /// <summary>Half the dock's width: the pill is 26 cm wide, Home under it narrower.</summary>
        public const float HalfWidth = 0.13f;

        [SerializeField] private Vector3 m_offsetFromHead = new Vector3(-0.40f, -0.2f, 0.85f);
        [SerializeField] private float m_recentreDegrees = 40f;
        [SerializeField] private float m_troubleAfterSeconds = 20f;

        // Moving (Nov 19 review: let the learner put the dock where they like). Where it may go:
        // within reach of the re-centre rule sideways, and from waist to just above the eyes.
        private const float MoveDistance = 0.85f, MoveFollow = 12f, MaxMoveDegrees = 34f, LowestMove = -0.45f, HighestMove = 0.15f;
        private bool m_moving;
        private Quaternion m_moveReference = Quaternion.identity;

        // A short message on the pill (e.g. "Label hidden"), then back to the status.
        private string m_flashKey;
        private float m_flashUntil;

        /// <summary>Shows a ui_strings message on the pill for a few seconds.</summary>
        public void Flash(string key, float seconds)
        {
            m_flashKey = key;
            m_flashUntil = Time.unscaledTime + seconds;
        }

        private string m_shownKey;
        private string m_shownLanguage;
        private float m_readySince = -1f;

        private void Awake()
        {
            m_chip.SetActive(false);
            m_statusButton.Clicked += _ => { if (m_moving) FinishMove(); else TogglePause(); };
            m_homeButton.Clicked += _ => { if (m_moving) FinishMove(); else m_home.OpenMenu(); };
            if (m_moveButton != null) m_moveButton.Clicked += _ => { if (m_moving) FinishMove(); else StartMove(); };
        }

        // Where the learner put it last time. In Start, once LearnerSettings has loaded its file.
        private void Start()
        {
            if (m_settings.TryGetDockOffset(out Vector3 saved)) m_offsetFromHead = saved;
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
            if (m_moveButton != null) m_moveButton.Interactable = !tour;
            if (!m_chip.activeSelf)
            {
                // Wait for a tracked head so the dock lands in view.
                if (!Place()) return;
                m_chip.SetActive(true);
            }
            if (m_moving) FollowHead();
            else
            {
                KeepInView();
                FaceHead();
            }
            Refresh();
        }

        private void StartMove()
        {
            m_moving = true;
            // Offsets are kept relative to the way the learner faced when the dock was last placed,
            // which is the dock's own facing; turning the head toward the new spot then moves it there.
            m_moveReference = m_chip.transform.rotation;
            m_shownKey = null; // show the moving hint now
            Debug.Log("[WordWorld.Status] moving the dock");
        }

        // The dock rides at the centre of the view, eased so it does not jitter with the head.
        private void FollowHead()
        {
            if (!HeadPose.TryGet(out Pose head)) return;
            Vector3 forward = head.rotation * Vector3.forward;
            Vector3 flat = new Vector3(forward.x, 0f, forward.z);
            if (flat.sqrMagnitude < 1e-4f) return;
            Vector3 target = head.position + forward * MoveDistance;
            Quaternion facing = Quaternion.LookRotation(flat.normalized, Vector3.up);
            float t = 1f - Mathf.Exp(-MoveFollow * Time.unscaledDeltaTime);
            Transform dock = m_chip.transform;
            dock.SetPositionAndRotation(Vector3.Lerp(dock.position, target, t), Quaternion.Slerp(dock.rotation, facing, t));
        }

        private void FinishMove()
        {
            m_moving = false;
            m_shownKey = null;
            if (!HeadPose.TryGet(out Pose head)) return;
            Vector3 offset = Quaternion.Inverse(m_moveReference) * (m_chip.transform.position - head.position);
            m_offsetFromHead = ClampOffset(offset);
            m_settings.SetDockOffset(m_offsetFromHead);
            // Settle on the saved spot as seen from the original facing, not from where the head
            // points now (it is turned toward the dock, which would push it further aside).
            m_chip.transform.SetPositionAndRotation(head.position + m_moveReference * m_offsetFromHead, m_moveReference);
            m_nextAvoidCheck = 0f;
            Debug.Log($"[WordWorld.Status] dock placed at {m_offsetFromHead}");
        }

        // Kept where re-centring will not throw it back: within MaxMoveDegrees of straight ahead,
        // between waist and just above eye height, at the usual distance.
        private static Vector3 ClampOffset(Vector3 offset)
        {
            float degrees = Mathf.Clamp(Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg, -MaxMoveDegrees, MaxMoveDegrees);
            Vector3 flat = Quaternion.Euler(0f, degrees, 0f) * Vector3.forward * MoveDistance;
            return new Vector3(flat.x, Mathf.Clamp(offset.y, LowestMove, HighestMove), flat.z);
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
            string key = m_moving ? "dock.moving"
                : micOn ? "dock.mic_on"
                : now < m_flashUntil ? m_flashKey
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
            if (m_moveButton != null)
            {
                m_moveButton.SetLabel(m_strings.Get(m_moving ? "dock.done" : "dock.move", ui));
                m_moveButton.Text.TextColor = m_theme.Ink;
                m_moveButton.Background = m_moving ? m_theme.Primary : m_theme.Button;
            }
            if (m_moving) m_statusButton.Background = m_theme.Primary;
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

        // On its two steps the tour's card sits beside the dock on purpose; it keeps clear itself.
        private static Transform IgnoredPanel() =>
            Tour.Active && (Tour.ActiveIndex == Tour.StatusStep || Tour.ActiveIndex == Tour.HomeStep) ? Tour.CardTransform : null;

        // Always turned toward the learner's eyes, eased, like a label (Nov 23 review: from the side
        // it was hard to read). A turn is only kept if the dock does not then cross any open window
        // or card as seen from the eyes; two panels that never overlap from the eyes cannot cut
        // through each other either. Otherwise it stays as it was and slides clear first.
        private const float FaceSpeed = 8f, FaceMinDegrees = 0.5f;

        private void FaceHead()
        {
            if (!HeadPose.TryGet(out Pose head)) return;
            Transform dock = m_chip.transform;
            Vector3 away = dock.position - head.position;
            if (away.sqrMagnitude < 1e-4f) return;
            Quaternion target = Quaternion.LookRotation(away.normalized, Vector3.up);
            if (Quaternion.Angle(dock.rotation, target) < FaceMinDegrees) return;
            Quaternion previous = dock.rotation;
            dock.rotation = Quaternion.Slerp(previous, target, 1f - Mathf.Exp(-FaceSpeed * Time.unscaledDeltaTime));
            if (m_occluder == null && !m_chip.TryGetComponent(out m_occluder)) return;
            Transform ignore = IgnoredPanel();
            if (m_occluder.FirstOverlap(head.position, AvoidMargin, ignore) == null) return;
            // The turn (or a window opening) put it across a panel: undo the turn and slide clear first.
            dock.rotation = previous;
            m_nextAvoidCheck = 0f;
        }

        private void AvoidPanels()
        {
            float now = Time.unscaledTime;
            if (now < m_nextAvoidCheck) return;
            m_nextAvoidCheck = now + AvoidInterval;
            Transform ignore = IgnoredPanel();
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
