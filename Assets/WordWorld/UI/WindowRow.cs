using UnityEngine;
using WordWorld.App;
using WordWorld.Labels;
using WordWorld.Tracking;

namespace WordWorld.UI
{
    /// <summary>
    /// The Word Bank, Quiz and Progress windows as a curved row about 1.2 m away, slightly below eye
    /// level (DESIGN.md section 5). First time they open after the first label tap (section 7 step
    /// 5); on later launches they open with the labels. The status chip's Windows button shows or
    /// hides all three, and re-opens them where the user is looking.
    /// </summary>
    [AddComponentMenu("Word World/Window Row")]
    public sealed class WindowRow : MonoBehaviour
    {
        [SerializeField] private LearnerSettings m_settings;
        [SerializeField] private WordBank m_wordBank;
        [SerializeField] private ObjectLabels m_labels;
        [SerializeField] private WordBankWindow m_wordBankWindow;
        [SerializeField] private FindItQuiz m_quiz;
        [SerializeField] private ProgressWindow m_progress;

        [SerializeField] private float m_radius = 1.2f;
        [SerializeField] private float m_height = -0.2f;
        [Tooltip("Yaw of the Word Bank (left) and Progress (right) windows; the Quiz is in the middle.")]
        [SerializeField] private float m_sideDegrees = 23f;

        private bool m_opened;

        public bool AnyVisible => m_wordBankWindow.Window.activeSelf || m_quiz.Window.activeSelf || m_progress.Window.activeSelf;

        private void OnEnable() => m_labels.LabelTapped += OnLabelTapped;

        private void OnDisable() => m_labels.LabelTapped -= OnLabelTapped;

        private void Update()
        {
            if (m_opened || !m_settings.SessionReady || !m_settings.OnboardingDone || m_wordBank.Words.Count == 0) return;
            Open();
        }

        private void OnLabelTapped(TrackedObject track, LabelView view)
        {
            if (!m_opened && m_settings.OnboardingDone) Open();
        }

        public void Toggle()
        {
            if (AnyVisible) HideAll();
            else Open();
        }

        public void HideAll()
        {
            m_wordBankWindow.Window.SetActive(false);
            m_quiz.Window.SetActive(false);
            m_progress.Window.SetActive(false);
        }

        public void Open()
        {
            if (!WindowPlacement.PlaceOnArc(m_quiz.Window.transform, 0f, m_radius, m_height)) return;
            WindowPlacement.PlaceOnArc(m_wordBankWindow.Window.transform, -m_sideDegrees, m_radius, m_height + 0.08f);
            WindowPlacement.PlaceOnArc(m_progress.Window.transform, m_sideDegrees, m_radius, m_height + 0.04f);
            m_wordBankWindow.Show();
            m_quiz.Show();
            m_progress.Show();
            m_opened = true;
        }
    }
}
