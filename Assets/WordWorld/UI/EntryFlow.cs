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
    /// First-time experience (DESIGN.md section 7): language window, then a short intro card, then a
    /// scanning hint until the first object is confirmed. Later launches skip straight to labels.
    /// Each card appears once where the user is looking (about 1.2 m away, slightly below eye
    /// level) and stays there; windows never follow the head.
    /// </summary>
    [AddComponentMenu("Word World/Entry Flow")]
    public sealed class EntryFlow : MonoBehaviour
    {
        [SerializeField] private LearnerSettings m_settings;
        [SerializeField] private UiStrings m_strings;
        [SerializeField] private TrackingManager m_tracking;
        [SerializeField] private WordWorldTheme m_theme;

        [Header("Language window")]
        [SerializeField] private GameObject m_languageWindow;
        [SerializeField] private SpatialAdapterNativeText m_languageTitle;
        [SerializeField] private SpatialButton[] m_languageButtons = System.Array.Empty<SpatialButton>();

        [Header("Intro card")]
        [SerializeField] private GameObject m_introCard;
        [SerializeField] private SpatialAdapterNativeText m_introText;
        [SerializeField] private SpatialButton m_introButton;

        [Header("Scanning hint")]
        [SerializeField] private GameObject m_scanningHint;
        [SerializeField] private SpatialAdapterNativeText m_scanningText;

        [Header("First label (DESIGN.md section 7 step 4)")]
        [SerializeField] private ObjectLabels m_labels;
        [SerializeField] private WordBank m_wordBank;
        [SerializeField] private GameObject m_tapMe;
        [SerializeField] private SpatialAdapterNativeText m_tapMeText;

        [Header("Placement")]
        [SerializeField] private Vector3 m_windowOffset = new Vector3(0f, -0.1f, 1.2f);
        [SerializeField] private Vector3 m_hintOffset = new Vector3(0f, -0.3f, 1.2f);
        [Tooltip("The hint line re-centres only once it is this far outside the user's view direction.")]
        [SerializeField] private float m_hintRecentreDegrees = 35f;

        private const int PersonClassId = 0;

        private enum Step { Waiting, Language, Intro, Scanning, Done }
        private Step m_step = Step.Waiting;
        private TrackedObject m_tapMeTrack;

        private void Awake()
        {
            SetActive(m_languageWindow, false);
            SetActive(m_introCard, false);
            SetActive(m_scanningHint, false);
            SetActive(m_tapMe, false);
            foreach (SpatialButton button in m_languageButtons) button.Clicked += OnLanguageClicked;
            if (m_introButton != null) m_introButton.Clicked += OnIntroDone;
        }

        private void OnEnable()
        {
            if (m_tracking != null) m_tracking.ObjectConfirmed += OnObjectConfirmed;
            if (m_labels != null)
            {
                m_labels.FirstLabelShown += OnFirstLabelShown;
                m_labels.LabelTapped += OnLabelTapped;
            }
        }

        private void OnDisable()
        {
            if (m_tracking != null) m_tracking.ObjectConfirmed -= OnObjectConfirmed;
            if (m_labels != null)
            {
                m_labels.FirstLabelShown -= OnFirstLabelShown;
                m_labels.LabelTapped -= OnLabelTapped;
            }
        }

        private void Update()
        {
            FollowFirstLabel();
            switch (m_step)
            {
                // Wait for a head pose so the first card lands in front of the user, not at the origin.
                case Step.Waiting:
                    if (!HeadPose.TryGet(out _)) return;
                    if (m_settings.HasLanguage && m_settings.OnboardingDone)
                    {
                        m_settings.ResumeSession();
                        m_step = Step.Done;
                    }
                    else ShowLanguageWindow();
                    break;
                case Step.Scanning:
                    KeepHintInView();
                    break;
            }
        }

        private void ShowLanguageWindow()
        {
            string ui = m_settings.HasLanguage ? m_settings.InterfaceLanguage : "en";
            // The interface language is unknown on first launch, so the question is asked in both.
            m_languageTitle.Text = m_settings.HasLanguage
                ? m_strings.Get("picker.title", ui)
                : $"{m_strings.Get("picker.title", "en")}\n{m_strings.Get("picker.title", "zh")}";
            m_languageTitle.TextColor = m_theme.Ink;
            foreach (SpatialButton button in m_languageButtons)
            {
                bool selected = button.Value == m_settings.TargetLanguage;
                button.Text.Text = m_strings.Get($"lang.{button.Value}.endonym", ui);
                button.Text.TextColor = selected ? m_theme.Accent : m_theme.Ink;
                button.Text.BackgroundColor = selected ? m_theme.AccentSoft : m_theme.Card;
            }
            PlaceInFront(m_languageWindow, m_windowOffset);
            m_languageWindow.SetActive(true);
            m_step = Step.Language;
        }

        private void OnLanguageClicked(SpatialButton button)
        {
            if (m_step != Step.Language) return;
            m_languageWindow.SetActive(false);
            m_settings.ChooseLanguage(button.Value);
            if (m_settings.OnboardingDone)
            {
                m_step = Step.Done;
                return;
            }
            string ui = m_settings.InterfaceLanguage;
            m_introText.Text = m_strings.Get("intro.body", ui);
            m_introText.TextColor = m_theme.Ink;
            m_introButton.Text.Text = m_strings.Get("intro.got_it", ui);
            m_introButton.Text.TextColor = m_theme.Card;
            m_introButton.Text.BackgroundColor = m_theme.Accent;
            PlaceInFront(m_introCard, m_windowOffset);
            m_introCard.SetActive(true);
            m_step = Step.Intro;
        }

        private void OnIntroDone(SpatialButton button)
        {
            if (m_step != Step.Intro) return;
            m_introCard.SetActive(false);
            m_settings.CompleteOnboarding();
            if (m_tracking != null && m_tracking.Confirmed.Count > 0)
            {
                m_step = Step.Done;
                return;
            }
            m_scanningText.Text = m_strings.Get("scanning.hint", m_settings.InterfaceLanguage);
            m_scanningText.TextColor = m_theme.InkMuted;
            PlaceInFront(m_scanningHint, m_hintOffset);
            m_scanningHint.SetActive(true);
            m_step = Step.Scanning;
        }

        // The hint disappears when the first label appears.
        private void OnObjectConfirmed(TrackedObject track)
        {
            if (m_step != Step.Scanning) return;
            if (track.ClassId == PersonClassId && !m_settings.LabelPeople) return; // no label, so keep hinting
            m_scanningHint.SetActive(false);
            m_step = Step.Done;
        }

        // A "Tap me" tooltip beside the first label until the learner has tapped any label ever.
        private void OnFirstLabelShown(TrackedObject track, LabelView view)
        {
            if (m_tapMe == null || m_wordBank == null || m_wordBank.Words.Count > 0) return;
            m_tapMeTrack = track;
            m_tapMeText.Text = m_strings.Get("onboarding.tap_me", m_settings.InterfaceLanguage);
            m_tapMeText.TextColor = m_theme.Card;
            m_tapMeText.BackgroundColor = m_theme.Accent;
            m_tapMe.SetActive(true);
        }

        private void OnLabelTapped(TrackedObject track, LabelView view)
        {
            m_tapMeTrack = null;
            SetActive(m_tapMe, false);
        }

        private void FollowFirstLabel()
        {
            if (m_tapMeTrack == null) return;
            if (!m_labels.TryGetLabel(m_tapMeTrack, out LabelView view) || !view.gameObject.activeInHierarchy ||
                !HeadPose.TryGet(out Pose head))
            {
                SetActive(m_tapMe, false);
                if (view == null) m_tapMeTrack = null;
                return;
            }
            Transform label = view.transform;
            // Just above the card (and its pinyin line), facing the user like the label.
            Vector3 position = label.position + Vector3.up * (view.HeightMetres + 0.03f);
            m_tapMe.transform.SetPositionAndRotation(position, label.rotation);
            if (!m_tapMe.activeSelf) m_tapMe.SetActive(true);
        }

        // The hint is a status line, not a window: it re-centres when the user has looked well away
        // from it, in one step rather than trailing the head.
        private void KeepHintInView()
        {
            if (!HeadPose.TryGet(out Pose head)) return;
            Vector3 toHint = m_scanningHint.transform.position - head.position;
            Vector3 forward = head.rotation * Vector3.forward;
            toHint.y = 0f;
            forward.y = 0f;
            if (Vector3.Angle(toHint, forward) > m_hintRecentreDegrees) PlaceInFront(m_scanningHint, m_hintOffset);
        }

        private static void PlaceInFront(GameObject target, Vector3 offset)
        {
            if (target == null || !HeadPose.TryGet(out Pose head)) return;
            Vector3 forward = head.rotation * Vector3.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-4f) forward = Vector3.forward;
            Quaternion yaw = Quaternion.LookRotation(forward.normalized, Vector3.up);
            target.transform.SetPositionAndRotation(head.position + yaw * offset, yaw);
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null) target.SetActive(active);
        }
    }
}
