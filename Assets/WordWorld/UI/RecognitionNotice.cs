using ByteDance.PICO.SpatialAdapter.Component;
using UnityEngine;
using WordWorld.App;
using WordWorld.Audio;
using WordWorld.Data;
using WordWorld.Detection;
using WordWorld.Tracking;

namespace WordWorld.UI
{
    /// <summary>
    /// Says plainly when object recognition is not working and what to do about it. On Swan the
    /// SecureMR GPU model sometimes will not load (or stops delivering frames) until the headset
    /// restarts; without this the app just sat on "Having trouble seeing". The card offers practice
    /// objects so the session can go on meanwhile. Shown once per session; tapping the dock's pill
    /// while recognition is down brings it back.
    /// </summary>
    [AddComponentMenu("Word World/Recognition Notice")]
    public sealed class RecognitionNotice : MonoBehaviour
    {
        [SerializeField] private LearnerSettings m_settings;
        [SerializeField] private UiStrings m_strings;
        [SerializeField] private TrackingManager m_tracking;
        [SerializeField] private WordWorldTheme m_theme;

        [SerializeField] private GameObject m_card;
        [SerializeField] private SpatialAdapterNativeText m_title;
        [SerializeField] private SpatialAdapterNativeText m_body;
        [SerializeField] private SpatialButton m_practiceButton;
        [SerializeField] private SpatialButton m_okButton;
        [SerializeField] private Vector3 m_offset = new Vector3(0f, -0.08f, 0.95f);

        private bool m_shownThisSession;
        private bool m_autoPractice;

        /// <summary>True while recognition is down (failed or no camera permission).</summary>
        public bool RecognitionDown
        {
            get
            {
                DetectorHealth health = m_tracking != null ? m_tracking.DetectorHealth : DetectorHealth.Starting;
                return health == DetectorHealth.Failed || health == DetectorHealth.NoPermission;
            }
        }

        private void Awake()
        {
            m_card.SetActive(false);
            m_practiceButton.Clicked += _ =>
            {
                m_settings.SetPracticeObjects(true);
                m_card.SetActive(false);
            };
            m_okButton.Clicked += _ =>
            {
                Sfx.Dismiss(m_card.transform.position);
                m_card.SetActive(false);
            };
        }

        private void Update()
        {
            // Practice objects come on straight away, even mid-tour, so step 2 has words to tap.
            if (!m_autoPractice && m_settings.SessionReady && RecognitionDown)
            {
                m_autoPractice = true;
                if (!m_settings.PracticeObjects) m_settings.SetPracticeObjects(true);
            }
            // Not over the tour: it waits until the tour is done or skipped.
            if (m_shownThisSession || Tour.Active || !m_settings.SessionReady || !m_settings.OnboardingDone || !RecognitionDown) return;
            Show();
        }

        public void Show()
        {
            m_shownThisSession = true;
            bool noPermission = m_tracking.DetectorHealth == DetectorHealth.NoPermission;
            Debug.Log($"[WordWorld.Notice] recognition down ({m_tracking.DetectorHealth})");            string ui = m_settings.InterfaceLanguage;
            m_title.SetFitted(m_strings.Get("notice.title", ui));
            m_title.TextColor = m_theme.Ink;
            m_body.SetFitted(m_strings.Get(noPermission ? "notice.permission" : "notice.restart", ui));
            m_body.TextColor = m_theme.Ink;
            m_practiceButton.gameObject.SetActive(!m_settings.PracticeObjects);
            m_practiceButton.SetLabel(m_strings.Get("notice.use_practice", ui));
            m_practiceButton.Text.TextColor = m_theme.Ink;
            m_practiceButton.Background = m_theme.Primary;
            m_okButton.SetLabel(m_strings.Get("common.ok", ui));
            m_okButton.Text.TextColor = m_theme.Ink;
            WindowPlacement.PlaceInFront(m_card.transform, m_offset);
            m_card.SetActive(true);
            Sfx.Appear(m_card.transform.position);
        }
    }
}
