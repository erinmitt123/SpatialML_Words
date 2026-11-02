using System.Text;
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
    /// Detail card (DESIGN.md section 4): tap a label to see the word, its meaning in the interface
    /// language, a grammar row (article and gender, or measure word) and an example sentence. Opens
    /// next to the label, slightly toward the user, and closes on the close button or after 20 s.
    /// Opening collects the word into the Word Bank.
    /// </summary>
    [AddComponentMenu("Word World/Detail Card")]
    public sealed class DetailCard : MonoBehaviour
    {
        [SerializeField] private LearnerSettings m_settings;
        [SerializeField] private UiStrings m_strings;
        [SerializeField] private Vocabulary m_vocabulary;
        [SerializeField] private WordBank m_wordBank;
        [SerializeField] private ObjectLabels m_labels;
        [SerializeField] private TrackingManager m_tracking;
        [SerializeField] private WordWorldTheme m_theme;

        [SerializeField] private GameObject m_card;
        [SerializeField] private SpatialAdapterNativeText m_header;
        [SerializeField] private SpatialAdapterNativeText m_subheader;
        [SerializeField] private SpatialAdapterNativeText m_body;
        [SerializeField] private SpatialButton m_saveButton;
        [SerializeField] private SpatialButton m_notRightButton;
        [SerializeField] private SpatialButton m_closeButton;

        [SerializeField] private float m_closeAfterSeconds = 20f;
        [Tooltip("Sideways and toward-the-user offset from the label, in metres.")]
        [SerializeField] private Vector2 m_offsetFromLabel = new Vector2(0.24f, 0.12f);
        [Tooltip("DESIGN.md section 10: nothing closer than 40 cm to the eyes.")]
        [SerializeField] private float m_minDistanceFromEyes = 0.45f;

        private readonly StringBuilder m_builder = new StringBuilder(256);
        private TrackedObject m_track;
        private float m_lastInteraction;

        public bool IsOpen => m_track != null;

        private void Awake()
        {
            m_card.SetActive(false);
            m_saveButton.Clicked += _ => ToggleSave();
            m_notRightButton.Clicked += _ => NotRight();
            m_closeButton.Clicked += _ => Close();
        }

        private void OnEnable()
        {
            if (m_labels != null) m_labels.LabelTapped += Open;
            if (m_tracking != null) m_tracking.ObjectRemoved += OnObjectRemoved;
            if (m_settings != null) m_settings.Changed += OnSettingsChanged;
        }

        private void OnDisable()
        {
            if (m_labels != null) m_labels.LabelTapped -= Open;
            if (m_tracking != null) m_tracking.ObjectRemoved -= OnObjectRemoved;
            if (m_settings != null) m_settings.Changed -= OnSettingsChanged;
        }

        private void Update()
        {
            if (m_track != null && Time.unscaledTime - m_lastInteraction > m_closeAfterSeconds) Close();
        }

        public void Open(TrackedObject track, LabelView view)
        {
            if (!m_vocabulary.TryGet(track.ClassId, out Vocabulary.Entry entry)) return;
            m_track = track;
            m_lastInteraction = Time.unscaledTime;
            m_wordBank.RecordTap(track.ClassId);
            Fill(entry);
            Place(view.transform.position);
            m_card.SetActive(true);
            m_labels.SetSelected(track);
        }

        public void Close()
        {
            if (m_track == null) return;
            m_track = null;
            m_card.SetActive(false);
            m_labels.SetSelected(null);
        }

        private void ToggleSave()
        {
            if (m_track == null) return;
            m_lastInteraction = Time.unscaledTime;
            m_wordBank.SetSaved(m_track.ClassId, !m_wordBank.IsSaved(m_track.ClassId));
            RefreshSaveButton();
        }

        // Hides that label for the session and logs nothing else.
        private void NotRight()
        {
            if (m_track == null) return;
            TrackedObject track = m_track;
            Close();
            m_labels.HideForSession(track);
        }

        private void OnObjectRemoved(TrackedObject track)
        {
            if (track == m_track) Close();
        }

        private void OnSettingsChanged()
        {
            if (m_track != null && m_vocabulary.TryGet(m_track.ClassId, out Vocabulary.Entry entry)) Fill(entry);
        }

        private void Fill(Vocabulary.Entry entry)
        {
            string lang = m_settings.TargetLanguage;
            string ui = m_settings.InterfaceLanguage;
            StringBuilder b = m_builder;

            if (lang == "zh")
            {
                Vocabulary.ChineseEntry zh = entry.zh;
                m_header.Text = zh.characters;
                m_subheader.Text = $"{zh.pinyin}   {zh.meaning}";
                b.Clear()
                    .Append(S("detail.measure_word", ui)).Append("   ").Append(zh.phrase).Append("  ").Append(zh.phrasePinyin).Append('\n')
                    .Append(S("detail.example", ui)).Append("   ").Append(zh.sentence).Append('\n')
                    .Append(zh.sentencePinyin);
            }
            else
            {
                Vocabulary.LatinEntry latin = Vocabulary.LatinFor(entry, lang);
                m_header.Text = latin.word;
                b.Clear().Append(latin.meaning);
                // DESIGN.md: show the Latin American alternative under the word when one exists.
                if (lang == "es" && !string.IsNullOrEmpty(latin.alternatives))
                    b.Append("   ").Append(S("detail.latam", ui)).Append(": ").Append(latin.alternatives);
                m_subheader.Text = b.ToString();

                b.Clear();
                if (lang == "en")
                    b.Append(S("detail.phrase", ui)).Append(": ").Append(latin.phrase);
                else
                    b.Append(S("detail.gender", ui)).Append(": ").Append(S(GenderKey(latin.gender), ui))
                        .Append(" · ").Append(S("detail.article", ui)).Append(": ").Append(latin.article)
                        .Append(" · ").Append(S("detail.plural", ui)).Append(": ").Append(S(latin.plural ? "common.yes" : "common.no", ui));
                b.Append('\n').Append(S("detail.example", ui)).Append("   ").Append(Vocabulary.Wrap(latin.sentence, 34, 2));
            }
            m_body.Text = b.ToString();

            m_header.TextColor = m_theme.Ink;
            m_subheader.TextColor = m_theme.InkMuted;
            m_body.TextColor = m_theme.Ink;
            m_notRightButton.Text.Text = S("detail.not_right", ui);
            m_closeButton.Text.Text = S("common.close", ui);
            RefreshSaveButton();
        }

        private void RefreshSaveButton()
        {
            bool saved = m_track != null && m_wordBank.IsSaved(m_track.ClassId);
            m_saveButton.Text.Text = S(saved ? "detail.saved" : "detail.save", m_settings.InterfaceLanguage);
            m_saveButton.Text.TextColor = saved ? m_theme.Accent : m_theme.Ink;
            m_saveButton.Text.BackgroundColor = saved ? m_theme.AccentSoft : m_theme.Card;
        }

        private static string GenderKey(string gender) =>
            gender == "m" ? "gender.m" : gender == "f" ? "gender.f" : gender == "n" ? "gender.n" : "gender.none";

        private string S(string key, string ui) => m_strings.Get(key, ui);

        // Beside the label and a little toward the user, facing them, never too close.
        private void Place(Vector3 labelPosition)
        {
            if (!HeadPose.TryGet(out Pose head)) return;
            Vector3 toUser = head.position - labelPosition;
            toUser.y = 0f;
            if (toUser.sqrMagnitude < 1e-4f) toUser = Vector3.back;
            toUser.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, -toUser);
            Vector3 position = labelPosition + right * m_offsetFromLabel.x + toUser * m_offsetFromLabel.y;
            Vector3 fromEyes = position - head.position;
            if (fromEyes.magnitude < m_minDistanceFromEyes) position = head.position + fromEyes.normalized * m_minDistanceFromEyes;
            m_card.transform.SetPositionAndRotation(position, ObjectLabels.Billboard(head.position, position));
        }
    }
}
