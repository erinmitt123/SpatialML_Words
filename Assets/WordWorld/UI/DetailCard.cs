using System.Text;
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
    /// Detail card (DESIGN.md section 4): tap a label to see the word, its meaning in the interface
    /// language, a grammar row (article and gender, or measure word), an example sentence and what
    /// that sentence means in the learner's own language. Opens
    /// next to the label, slightly toward the user, and closes on the close button or after 20 s.
    /// Opening collects the word into the Word Bank and says the word aloud (Sound on All).
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
        [Tooltip("Confirms on the dock that a wrongly recognised object's label was hidden.")]
        [SerializeField] private StatusChip m_status;

        [SerializeField] private GameObject m_card;
        [SerializeField] private SpatialAdapterNativeText m_header;
        [SerializeField] private SpatialAdapterNativeText m_subheader;
        [SerializeField] private SpatialAdapterNativeText m_body;
        [SerializeField] private SpatialButton m_saveButton;
        [SerializeField] private SpatialButton m_notRightButton;
        [SerializeField] private SpatialButton m_closeButton;
        [SerializeField] private SpatialButton m_listenButton;
        [SerializeField] private SpatialButton m_exampleButton;
        [SerializeField] private SpatialButton m_sayItButton;
        [SerializeField] private VocabAudio m_audio;
        [SerializeField] private SayItCard m_sayIt;

        [SerializeField] private float m_closeAfterSeconds = 20f;
        [Tooltip("Distance from the eyes: in front of the object but within poke reach. DESIGN.md section 10 keeps it over 40 cm.")]
        [SerializeField] private float m_distanceFromEyes = 0.6f;

        private readonly StringBuilder m_builder = new StringBuilder(256);
        private TrackedObject m_track;
        private float m_lastInteraction;

        public bool IsOpen => m_track != null;
        public Transform Card => m_card.transform;
        public SpatialButton CloseButton => m_closeButton;

        private void Awake()
        {
            m_card.SetActive(false);
            m_saveButton.Clicked += _ => ToggleSave();
            m_notRightButton.Clicked += _ => NotRight();
            m_closeButton.Clicked += _ =>
            {
                Sfx.Dismiss(m_card.transform.position);
                Close();
            };
            m_listenButton.Clicked += _ => Listen(VocabClips.ForLevel(m_settings.TargetLanguage, m_settings.ReadingLevel));
            m_exampleButton.Clicked += _ => Listen(ClipKind.Sentence);
            m_sayItButton.Clicked += _ => SayIt();
        }

        // The Say It card takes over from this one for the same word.
        private void SayIt()
        {
            if (m_track == null || m_sayIt == null) return;
            int classId = m_track.ClassId;
            Close();
            m_sayIt.BeginSingle(classId);
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
            WindowPlacement.PlaceNear(m_card.transform, track.Position, Mathf.Max(0.45f, m_distanceFromEyes));
            m_card.SetActive(true);
            m_labels.SetSelected(track);
            Sfx.Appear(m_card.transform.position);
            // Exploring: tapping a label says the object's name from the object (Sound on All only).
            // Listen on the card still plays the label's full line at the reading level. Not during
            // the tour, whose narration explains the card next.
            if (m_audio != null && !Tour.Active) m_audio.PlayIfFree(track.ClassId, ClipKind.Word, track.Position);
        }

        public void Close()
        {
            if (m_track == null) return;
            m_track = null;
            m_card.SetActive(false);
            m_labels.SetSelected(null);
            if (m_audio != null) m_audio.Stop();
        }

        // From the object itself, so the sound comes from the thing the word names.
        private void Listen(ClipKind kind)
        {
            if (m_track == null || m_audio == null) return;
            m_lastInteraction = Time.unscaledTime;
            m_audio.Play(m_track.ClassId, kind, m_track.Position);
        }

        private void ToggleSave()
        {
            if (m_track == null) return;
            m_lastInteraction = Time.unscaledTime;
            m_wordBank.SetSaved(m_track.ClassId, !m_wordBank.IsSaved(m_track.ClassId));
            RefreshSaveButton();
        }

        // "Wrong object? Hide it": the recogniser named the wrong thing, so its label is hidden for
        // the session. The dock says so, since a label silently vanishing read as a bug (Nov 23 review:
        // the old "Not right?" did not say what it would do).
        private void NotRight()
        {
            if (m_track == null) return;
            TrackedObject track = m_track;
            Close();
            m_labels.HideForSession(track);
            if (m_status != null) m_status.Flash("detail.hidden", 4f);
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
                m_header.SetFitted(zh.characters);
                m_subheader.SetFitted($"{zh.pinyin}   {zh.meaning}");
                b.Clear()
                    .Append(S("detail.measure_word", ui)).Append("   ").Append(zh.phrase).Append("  ").Append(zh.phrasePinyin).Append('\n')
                    .Append(S("detail.example", ui)).Append("   ").Append(zh.sentence).Append('\n')
                    .Append(zh.sentencePinyin);
                AppendMeaning(b, zh.sentenceMeaning, ui);
            }
            else
            {
                Vocabulary.LatinEntry latin = Vocabulary.LatinFor(entry, lang);
                m_header.SetFitted(latin.word);
                b.Clear().Append(latin.meaning);
                // DESIGN.md: Spanish is Mexican; show the Spain word under it when it differs.
                if (lang == "es" && !string.IsNullOrEmpty(latin.alternatives))
                    b.Append("   ").Append(S("detail.spain", ui)).Append(": ").Append(latin.alternatives);
                m_subheader.SetFitted(b.ToString());

                b.Clear();
                if (lang == "en")
                    b.Append(S("detail.phrase", ui)).Append(": ").Append(latin.phrase);
                else
                    b.Append(S("detail.gender", ui)).Append(": ").Append(S(GenderKey(latin.gender), ui))
                        .Append(" · ").Append(S("detail.article", ui)).Append(": ").Append(latin.article)
                        .Append(" · ").Append(S("detail.plural", ui)).Append(": ").Append(S(latin.plural ? "common.yes" : "common.no", ui));
                b.Append('\n').Append(S("detail.example", ui)).Append("   ").Append(Vocabulary.Wrap(latin.sentence, 34, 2));
                AppendMeaning(b, latin.sentenceMeaning, ui);
            }
            m_body.SetFitted(b.ToString());

            m_header.TextColor = m_theme.Ink;
            m_subheader.TextColor = m_theme.InkMuted;
            m_body.TextColor = m_theme.Ink;
            m_notRightButton.SetLabel(S("detail.not_right", ui));
            m_closeButton.SetLabel(S("common.close", ui));
            RefreshSaveButton();
            RefreshListenButtons(entry.classIndex, ui);
        }

        // Hidden when audio is off, the clip is missing or speech can't be checked, so a button never
        // does nothing. Say It doesn't depend on the Audio setting: it works without the model clip.
        private void RefreshListenButtons(int classId, string ui)
        {
            ClipKind kind = VocabClips.ForLevel(m_settings.TargetLanguage, m_settings.ReadingLevel);
            bool word = m_audio != null && m_audio.Has(classId, kind);
            bool example = m_audio != null && m_audio.Has(classId, ClipKind.Sentence);
            bool say = m_sayIt != null && m_sayIt.CanSay(classId);
            // The tour only asks the learner to read the card and close it: nothing else shows.
            bool tour = Tour.Active;
            m_listenButton.gameObject.SetActive(word && !tour);
            m_exampleButton.gameObject.SetActive(example && !tour);
            m_sayItButton.gameObject.SetActive(say && !tour);
            m_saveButton.gameObject.SetActive(!tour);
            m_notRightButton.gameObject.SetActive(!tour);
            m_listenButton.SetLabel(S("audio.listen", ui));
            m_exampleButton.SetLabel(S("audio.example", ui));
            m_sayItButton.SetLabel(S("audio.say_it", ui));
            m_listenButton.Text.TextColor = m_theme.Ink;
            m_exampleButton.Text.TextColor = m_theme.Ink;
            m_sayItButton.Text.TextColor = m_theme.Ink;
            m_sayItButton.Background = m_theme.Primary;
        }

        private void RefreshSaveButton()
        {
            bool saved = m_track != null && m_wordBank.IsSaved(m_track.ClassId);
            m_saveButton.SetLabel(S(saved ? "detail.saved" : "detail.save", m_settings.InterfaceLanguage));
            m_saveButton.Text.TextColor = m_theme.Ink;
            m_saveButton.Background = saved ? m_theme.AccentSoft : m_theme.Button;
        }

        // The example in the learner's own language (Nov 19 review: the sentence alone could not be
        // understood). Written in the vocabulary sheet; skipped where a row has none yet.
        private void AppendMeaning(StringBuilder b, string meaning, string ui)
        {
            if (string.IsNullOrEmpty(meaning)) return;
            b.Append('\n').Append(S("detail.means", ui)).Append("   ").Append(meaning);
        }

        private static string GenderKey(string gender) =>
            gender == "m" ? "gender.m" : gender == "f" ? "gender.f" : gender == "n" ? "gender.n" : "gender.none";

        private string S(string key, string ui) => m_strings.Get(key, ui);

    }
}
