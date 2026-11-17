using ByteDance.PICO.SpatialAdapter.Component;
using UnityEngine;
using WordWorld.App;
using WordWorld.Audio;
using WordWorld.Data;
using WordWorld.Labels;
using WordWorld.Spatial;
using WordWorld.Tracking;

namespace WordWorld.UI
{
    /// <summary>
    /// The guided tour: nine short steps that each point at one part of the app with a pulsing
    /// green halo, a caption card beside it and a spoken line (English or Mandarin, by interface
    /// language). Plays on every launch once the languages are known (Skip ends it), and again from
    /// the menu's Tour button.
    ///
    /// Steps that ask for an action (tap a word, close its card, tap Home) move on when the learner
    /// does it; every step also has Next. While it runs, anything the current step is not about is
    /// locked (no hover, no tap, no sound) or hidden: see <see cref="Active"/> and
    /// <see cref="ActiveStep"/>. Captions and narration are the same text: ui_strings.json "tour.*",
    /// spoken by tools/generate_tour_audio.py.
    /// </summary>
    [AddComponentMenu("Word World/Tour")]
    public sealed class Tour : MonoBehaviour
    {
        /// <summary>Step keys, in order. Must match STEPS in tools/generate_tour_audio.py.</summary>
        public static readonly string[] Steps =
            { "welcome", "labels", "detail", "status", "home", "journey", "practice", "settings", "replay" };

        private const int Welcome = 0, Labels = 1, Detail = 2, Status = 3, Home = 4, Journey = 5, Replay = 8;

        /// <summary>True while the tour runs; other components lock what the step is not about.</summary>
        public static bool Active { get; private set; }

        /// <summary>The running step's key (see <see cref="Steps"/>), or null.</summary>
        public static string ActiveStep { get; private set; }

        /// <summary>The running step's index, or -1.</summary>
        public static int ActiveIndex { get; private set; } = -1;

        /// <summary>Dock buttons: the recognition pill is explained at this step, Home at the next.</summary>
        public const int StatusStep = Status, HomeStep = Home;

        /// <summary>The caption card, which the dock lets sit beside it on the dock steps.</summary>
        public static Transform CardTransform { get; private set; }

        [SerializeField] private LearnerSettings m_settings;
        [SerializeField] private UiStrings m_strings;
        [SerializeField] private WordWorldTheme m_theme;
        [SerializeField] private ObjectLabels m_labels;
        [SerializeField] private DetailCard m_detail;
        [SerializeField] private StatusChip m_dock;
        [SerializeField] private HomeMenu m_home;
        [SerializeField] private AudioTurn m_turn;

        [Header("Caption card")]
        [SerializeField] private GameObject m_card;
        [SerializeField] private SpatialAdapterNativeText m_counter;
        [SerializeField] private SpatialAdapterNativeText m_body;
        [SerializeField] private SpatialButton m_next;
        [SerializeField] private SpatialButton m_skip;
        [Tooltip("Tooltip tails: one on the card's left edge (target to the left), one under it (target below).")]
        [SerializeField] private GameObject m_tailLeft;
        [SerializeField] private GameObject m_tailDown;
        [SerializeField] private Vector2 m_cardSize = new Vector2(0.36f, 0.16f);
        [SerializeField] private Vector2 m_bodyBox = new Vector2(0.33f, 0.07f);
        [SerializeField] private float m_bodyPoints = 42f;
        [Tooltip("Where the card sits when the step points at the room: low in view, so the words the learner looks at stay clear of it.")]
        [SerializeField] private Vector3 m_headOffset = new Vector3(0f, -0.2f, 0.85f);

        [Header("Halo")]
        [SerializeField] private GameObject m_halo;
        [SerializeField] private SpatialAdapterNativeText m_haloText;
        [SerializeField] private float m_haloMargin = 0.022f;

        [Header("Narration (one clip per step, in Steps order)")]
        [SerializeField] private AudioClip[] m_clipsEn = System.Array.Empty<AudioClip>();
        [SerializeField] private AudioClip[] m_clipsZh = System.Array.Empty<AudioClip>();
        [Tooltip("A word the learner just tapped finishes before the next line starts, up to this long.")]
        [SerializeField] private float m_narrationWaitSeconds = 2.5f;

        private int m_step = -1;
        private bool m_firstRun;
        private float m_stepStarted;
        private AudioClip m_pendingClip;
        private bool m_spoke;
        private Vector2 m_haloSize;

        public bool Running => m_step >= 0;

        /// <summary>The tour has been offered this launch (it plays once per launch).</summary>
        public bool RanThisSession { get; private set; }

        private void Awake()
        {
            CardTransform = m_card.transform;
            m_card.SetActive(false);
            m_halo.SetActive(false);
            m_next.Clicked += _ => Advance();
            m_skip.Clicked += _ =>
            {
                Sfx.Dismiss(m_card.transform.position);
                End();
            };
        }

        private void OnEnable()
        {
            if (m_labels != null) m_labels.LabelTapped += OnLabelTapped;
        }

        private void OnDisable()
        {
            if (m_labels != null) m_labels.LabelTapped -= OnLabelTapped;
        }

        /// <summary>Starts from the first step. Ends in the Journey, or (endInJourney false) at the menu.</summary>
        public void Begin(bool endInJourney)
        {
            m_firstRun = endInJourney;
            RanThisSession = true;
            Debug.Log($"[WordWorld.Tour] begin (then {(endInJourney ? "Journey" : "menu")})");
            // Labels only: windows would hide them and the tour points at the room first.
            m_home.EnterMode(HomeMenu.Mode.Explore);
            m_card.SetActive(true);
            Sfx.Appear(m_card.transform.position);
            Show(Welcome);
        }

        /// <summary>Ends the tour; with restore, puts the learner back where it leads (Journey or the menu).</summary>
        private void End(bool restore = true)
        {
            if (m_step < 0) return;
            Debug.Log($"[WordWorld.Tour] end at {Steps[m_step]}");
            m_step = -1;
            Active = false;
            ActiveStep = null;
            ActiveIndex = -1;
            m_home.SetTourLock(false);
            m_pendingClip = null;
            if (m_spoke && m_turn.State == TurnState.Speaking) m_turn.Stop();
            m_card.SetActive(false);
            m_halo.SetActive(false);
            if (!restore) return;
            if (m_firstRun) m_home.EnterMode(HomeMenu.Mode.Journey);
            else if (!m_home.Window.activeSelf) m_home.EnterMode(HomeMenu.Mode.Menu);
        }

        private void Advance()
        {
            int next = m_step + 1;
            // Next on "tap a word" skips explaining a card that never opened.
            if (next == Detail && !m_detail.IsOpen) next++;
            if (next >= Steps.Length)
            {
                End();
                return;
            }
            Show(next);
        }

        private void Show(int step)
        {
            m_step = step;
            Active = true;
            ActiveStep = Steps[step];
            ActiveIndex = step;
            m_home.SetTourLock(true);
            m_stepStarted = Time.unscaledTime;
            string ui = m_settings.InterfaceLanguage;
            // The menu steps point at its buttons, so it opens (on its main page) for them.
            if (step == Journey && !m_home.Window.activeSelf) m_home.EnterMode(HomeMenu.Mode.Menu);
            if (step >= Journey) m_home.ShowMainPage();

            string body = m_strings.Get($"tour.{Steps[step]}", ui);
            m_body.Text = TextFit.Wrap(body, m_bodyPoints, m_bodyBox, out float bodyPoints);
            m_body.TextColor = m_theme.Ink;
            m_body.TextSize = TextFit.ToNative(bodyPoints);
            m_counter.Text = string.Format(m_strings.Get("tour.step", ui), step + 1, Steps.Length);
            m_counter.TextColor = m_theme.InkMuted;
            m_next.SetLabel(m_strings.Get(step == Replay ? "tour.done" : "tour.next", ui));
            m_next.Text.TextColor = m_theme.Ink;
            m_next.Background = m_theme.Primary;
            m_skip.SetLabel(m_strings.Get("tour.skip", ui));
            m_skip.Text.TextColor = m_theme.InkMuted;
            m_skip.Background = m_theme.Button;
            m_skip.gameObject.SetActive(step != Replay);

            if (step == Welcome || step == Labels) WindowPlacement.PlaceInFront(m_card.transform, m_headOffset);
            m_haloSize = Vector2.zero;

            AudioClip[] clips = ui == "zh" ? m_clipsZh : m_clipsEn;
            m_pendingClip = step < clips.Length ? clips[step] : null;
            Debug.Log($"[WordWorld.Tour] step {step + 1} {Steps[step]}");
        }

        // A word tapped during "tap a word" opens its card: explain the card next.
        private void OnLabelTapped(TrackedObject track, LabelView view)
        {
            if (m_step == Labels) Show(Detail);
        }

        private void Update()
        {
            if (m_step < 0) return;
            Speak();

            switch (m_step)
            {
                case Detail when !m_detail.IsOpen && Time.unscaledTime - m_stepStarted > 0.5f:
                    Advance(); // closed it: that was the action asked for
                    return;
                case Home when m_home.Window.activeSelf:
                    Show(Home + 1); // they tapped Home
                    return;
                // Left the menu (tapped a mode): the learner has taken over, so the tour gets out of the way.
                case >= Journey when !m_home.Window.activeSelf:
                    End(restore: false);
                    return;
            }

            Transform target = Target(out Vector2 size);
            PlaceCard(target);
            PlaceHalo(target, size);
        }

        private void Speak()
        {
            if (m_pendingClip == null) return;
            if (!m_settings.SpokenWords)
            {
                m_pendingClip = null;
                return;
            }
            // Let a word the learner just tapped finish first.
            if (m_turn.State != TurnState.Idle && Time.unscaledTime - m_stepStarted < m_narrationWaitSeconds) return;
            if (m_turn.State == TurnState.Listening || m_turn.State == TurnState.Checking) return;
            m_spoke = m_turn.Speak(m_pendingClip, m_card.transform.position) || m_spoke;
            m_pendingClip = null;
        }

        /// <summary>What this step points at, and its size in metres; null for a step about the room.</summary>
        private Transform Target(out Vector2 size)
        {
            size = Vector2.zero;
            switch (m_step)
            {
                case Labels:
                    if (!m_labels.TryGetLabelNearGaze(out LabelView view) || view.Collider == null) return null;
                    return Measure(view.Collider, out size);
                case Detail: return Measure(m_detail.CloseButton, out size);
                case Status: return Measure(m_dock.StatusButton, out size);
                case Home: return Measure(m_dock.HomeButton, out size);
                case Welcome: return null;
                default:
                    string value = m_step == Replay ? "tour" : Steps[m_step];
                    return Measure(m_home.FindButton(value), out size);
            }
        }

        private static Transform Measure(Component target, out Vector2 size)
        {
            size = Vector2.zero;
            if (target == null || !target.gameObject.activeInHierarchy ||
                !target.TryGetComponent(out BoxCollider box)) return null;
            Vector3 scale = box.transform.lossyScale;
            size = new Vector2(box.size.x * scale.x, box.size.y * scale.y);
            return box.transform;
        }

        // The card sits beside what it explains: right of a dock button or the menu, above the word
        // card. A step about the room keeps it in front of the learner.
        private void PlaceCard(Transform target)
        {
            bool left = false, down = false;
            if (m_step == Welcome || m_step == Labels)
            {
                WindowPlacement.KeepInView(m_card.transform, m_headOffset, 35f);
            }
            else if (target != null && HeadPose.TryGet(out Pose head))
            {
                switch (m_step)
                {
                    case Detail:
                        Transform anchor = m_detail.Card;
                        // DetailCard is 24 cm tall; nudged toward the learner so it never sinks into it.
                        Vector3 above = anchor.position + anchor.up * (0.12f + m_cardSize.y * 0.5f + 0.02f) - anchor.forward * 0.01f;
                        m_card.transform.SetPositionAndRotation(above, anchor.rotation);
                        down = true;
                        break;
                    case Status:
                    case Home:
                        // Beside the whole dock, level with the button: next to the narrower Home
                        // button the card ran into the pill above it and, turned toward the learner,
                        // swung over Home itself (Nov 16 recording). Kept in the dock's plane.
                        Transform dock = m_dock.Dock;
                        Vector3 beside = dock.position + dock.right * (StatusChip.HalfWidth + m_cardSize.x * 0.5f + 0.03f);
                        beside += dock.up * Vector3.Dot(target.position - dock.position, dock.up);
                        m_card.transform.SetPositionAndRotation(beside, dock.rotation);
                        left = true;
                        break;
                    default:
                        SideOf(m_home.Window.transform, 0.21f, head); // the menu is 42 cm wide
                        left = true;
                        break;
                }
            }
            if (m_tailLeft.activeSelf != left) m_tailLeft.SetActive(left);
            if (m_tailDown.activeSelf != down) m_tailDown.SetActive(down);
        }

        private void SideOf(Transform anchor, float half, Pose head)
        {
            Vector3 position = anchor.position + anchor.right * (half + m_cardSize.x * 0.5f + 0.02f);
            Vector3 flat = position - head.position;
            flat.y = 0f;
            // Turned to face the learner, since it sits off to the side.
            Quaternion facing = flat.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(flat.normalized, Vector3.up) : anchor.rotation;
            m_card.transform.SetPositionAndRotation(position, facing);
        }

        // A soft green plate just behind the target, pulsing gently, so it reads as a glow around it.
        private void PlaceHalo(Transform target, Vector2 size)
        {
            if (target == null)
            {
                if (m_halo.activeSelf) m_halo.SetActive(false);
                return;
            }
            Vector2 wanted = size + new Vector2(m_haloMargin, m_haloMargin) * 2f;
            if ((wanted - m_haloSize).sqrMagnitude > 0.003f * 0.003f)
            {
                m_haloSize = wanted;
                m_haloText.BackgroundSize = wanted;
            }
            Vector3 centre = target.TransformPoint(target.GetComponent<BoxCollider>().center);
            m_halo.transform.SetPositionAndRotation(centre + target.forward * 0.004f, target.rotation);
            float pulse = 1f + 0.06f * Mathf.Sin((Time.unscaledTime - m_stepStarted) * Mathf.PI * 2f * 0.9f);
            m_halo.transform.localScale = new Vector3(pulse, pulse, 1f);
            if (!m_halo.activeSelf) m_halo.SetActive(true);
        }
    }
}
