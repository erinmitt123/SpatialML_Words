using System;
using System.Collections.Generic;
using System.Text;
using ByteDance.PICO.SpatialAdapter.Component;
using UnityEngine;
using WordWorld.App;
using WordWorld.Audio;
using WordWorld.Data;
using WordWorld.Labels;
using WordWorld.Speech;
using WordWorld.Tracking;

namespace WordWorld.UI
{
    /// <summary>
    /// Say It (docs/audio-plan.md): the line to say is written on the card, the learner may tap Listen
    /// to hear it, then taps Speak; the microphone never opens any other way. Each written word gets a
    /// chip (mint clear, peach say again, rose wrong or missing) and a line of text that says what
    /// happened, so colour is never the only signal. Used for one word from the detail card, for a
    /// Practice round, and for the Journey's Say step (five words, four to pass).
    /// </summary>
    [AddComponentMenu("Word World/Say It Card")]
    public sealed class SayItCard : MonoBehaviour
    {
        private enum Phase { Closed, Item, Summary, NotEnough }

        /// <summary>A round finished: words said correctly, and words asked. Not raised for a single word.</summary>
        public event Action<int, int> RoundFinished;
        /// <summary>The learner closed a round's card (not raised for a single word).</summary>
        public event Action Closed;

        [SerializeField] private LearnerSettings m_settings;
        [SerializeField] private UiStrings m_strings;
        [SerializeField] private Vocabulary m_vocabulary;
        [SerializeField] private WordBank m_wordBank;
        [SerializeField] private ObjectLabels m_labels;
        [SerializeField] private SpeechData m_speechData;
        [SerializeField] private SpeechCheck m_speech;
        [SerializeField] private AudioTurn m_turn;
        [SerializeField] private VocabAudio m_audio;
        [SerializeField] private WordWorldTheme m_theme;

        [SerializeField] private GameObject m_card;
        [SerializeField] private SpatialAdapterNativeText m_title;
        [SerializeField] private SpatialAdapterNativeText m_progress;
        [SerializeField] private SpatialAdapterNativeText m_ruby;
        [SerializeField] private SpatialAdapterNativeText m_prompt;
        [SerializeField] private SpatialAdapterNativeText m_meaning;
        [SerializeField] private SpatialAdapterNativeText m_result;
        [SerializeField] private SpatialAdapterNativeText[] m_chips = Array.Empty<SpatialAdapterNativeText>();
        [SerializeField] private SpatialButton m_listen;
        [SerializeField] private SpatialButton m_speak;
        [SerializeField] private SpatialButton m_next;
        [SerializeField] private SpatialButton m_close;

        [SerializeField] private Vector3 m_offset = new Vector3(0f, -0.1f, 0.8f);
        [Tooltip("Chip rows, as local y on the card.")]
        [SerializeField] private float m_chipRow1 = -0.035f;
        [SerializeField] private float m_chipRow2 = -0.066f;
        [SerializeField] private float m_chipWidthLimit = 0.36f;
        [SerializeField] private int m_chipPoints = 20;

        private readonly List<int> m_items = new List<int>();
        private readonly List<bool> m_passed = new List<bool>();
        private readonly List<TrackedObject> m_objects = new List<TrackedObject>();
        private readonly StringBuilder m_builder = new StringBuilder(128);
        private Phase m_phase = Phase.Closed;
        private int m_index;
        private bool m_single;
        private int m_passNeeded;
        private SpeechTarget m_target;
        private SpeechScore m_score;
        private string m_message; // a strings key shown in place of a score
        private bool m_besideObject;

        public bool IsOpen => m_phase != Phase.Closed;

        private void Awake()
        {
            m_card.SetActive(false);
            m_listen.Clicked += _ => Listen();
            m_speak.Clicked += _ => Speak();
            m_next.Clicked += _ => Next();
            m_close.Clicked += _ => Close();
        }

        private void OnEnable()
        {
            m_settings.Changed += OnSettingsChanged;
            m_turn.StateChanged += OnTurnChanged;
            m_speech.StatusChanged += Refresh;
        }

        private void OnDisable()
        {
            m_settings.Changed -= OnSettingsChanged;
            m_turn.StateChanged -= OnTurnChanged;
            m_speech.StatusChanged -= Refresh;
        }

        /// <summary>True when Say It can run in the learner's language in this build.</summary>
        public bool Available => m_settings.HasLanguage && m_speech.Supports(m_settings.TargetLanguage);

        /// <summary>Can this object be said? (Its naming words must be ones the model knows.)</summary>
        public bool CanSay(int classId) => Available && TargetFor(classId) != null;

        /// <summary>One word, from the detail card.</summary>
        public void BeginSingle(int classId)
        {
            m_items.Clear();
            m_items.Add(classId);
            m_single = true;
            m_passNeeded = 1;
            Open();
        }

        /// <summary>
        /// A round of up to <paramref name="count"/> objects labelled in the room, words in the Word Bank
        /// first. <paramref name="passNeeded"/> is out of a full round and scales down if fewer objects
        /// are in view.
        /// </summary>
        public void BeginRound(int count, int passNeeded)
        {
            RoomFx.Ripple();
            m_items.Clear();
            m_labels.GetLabelled(m_objects);
            var known = new List<int>();
            var others = new List<int>();
            foreach (TrackedObject track in m_objects)
            {
                int id = track.ClassId;
                if (known.Contains(id) || others.Contains(id) || TargetFor(id) == null ||
                    !m_vocabulary.IsEnabled(id)) continue;
                (m_wordBank.Contains(id) ? known : others).Add(id);
            }
            Shuffle(known);
            Shuffle(others);
            foreach (int id in known) if (m_items.Count < count) m_items.Add(id);
            foreach (int id in others) if (m_items.Count < count) m_items.Add(id);
            m_single = false;
            m_passNeeded = Mathf.Max(1, Mathf.FloorToInt(passNeeded * (float)m_items.Count / count));
            if (m_items.Count < Mathf.Min(3, count))
            {
                m_phase = Phase.NotEnough;
                Show();
                return;
            }
            Open();
        }

        /// <summary>Closes without raising events (Home, another mode).</summary>
        public void Stop()
        {
            if (m_phase == Phase.Closed) return;
            m_turn.Stop();
            m_phase = Phase.Closed;
            m_besideObject = false;
            m_card.SetActive(false);
            m_labels.SetSelected(null);
        }

        // A single word from the detail card closes quietly: the learner stays where they were.
        private void Close()
        {
            bool round = !m_single;
            Stop();
            if (round) Closed?.Invoke();
        }

        private void Open()
        {
            m_passed.Clear();
            for (int i = 0; i < m_items.Count; i++) m_passed.Add(false);
            m_index = 0;
            // First use in a language unpacks its model in the background; the card shows "Getting ready".
            m_speech.Prepare(m_settings.TargetLanguage);
            StartItem();
            Show();
        }

        private void Show()
        {
            // Items with an object were already placed beside it in StartItem.
            if (!m_card.activeSelf && !m_besideObject) WindowPlacement.PlaceInFront(m_card.transform, m_offset);
            if (!m_card.activeSelf) Sfx.Appear(m_card.transform.position);
            m_card.SetActive(true);
            Refresh();
        }

        private void StartItem()
        {
            m_phase = Phase.Item;
            m_score = null;
            m_message = null;
            int id = m_items[m_index];
            m_target = TargetFor(id);
            m_besideObject = false;
            if (m_labels.TryFindByClass(id, out TrackedObject track))
            {
                m_labels.SetSelected(track);
                m_labels.Pulse(track, 3f);
                // Ping from the object so the ear finds it, then put the card asking for the word
                // beside it. The word itself still plays only on a Listen tap.
                m_audio.Ping(track.Position);
                m_besideObject = WindowPlacement.PlaceNear(m_card.transform, track.Position);
            }
        }

        private SpeechTarget TargetFor(int classId) =>
            m_speechData.Target(m_settings.TargetLanguage, classId, Kind);

        private ClipKind Kind => VocabClips.ForSayIt(m_settings.TargetLanguage, m_settings.ReadingLevel);

        private void Listen()
        {
            if (m_phase != Phase.Item) return;
            int id = m_items[m_index];
            Vector3 position = m_labels.TryFindByClass(id, out TrackedObject track) ? track.Position : m_card.transform.position;
            m_audio.Play(id, Kind, position);
        }

        private void Speak()
        {
            // In the summary the middle button is Done.
            if (m_phase == Phase.Summary)
            {
                Close();
                return;
            }
            if (m_phase != Phase.Item || m_target == null) return;
            if (m_turn.State == TurnState.Listening)
            {
                m_turn.FinishListening();
                return;
            }
            if (m_turn.State == TurnState.Checking || !m_speech.IsReady(m_settings.TargetLanguage)) return;
            m_score = null;
            m_message = null;
            float seconds = Kind == ClipKind.Word ? 4f : Kind == ClipKind.Phrase ? 6f : 9f;
            SpeechTarget target = m_target;
            m_turn.Listen(target, seconds, (score, problem) =>
            {
                if (m_phase != Phase.Item || m_target != target) return;
                OnScored(score, problem);
            });
            Refresh();
        }

        private void OnScored(SpeechScore score, AudioTurn.ListenProblem problem)
        {
            m_score = score;
            m_message = problem == AudioTurn.ListenProblem.NoPermission ? "sayit.no_permission"
                : problem != AudioTurn.ListenProblem.None ? "sayit.failed"
                : score.HeardNothing ? "sayit.nothing"
                : null;
            if (score != null && !score.HeardNothing)
            {
                int id = m_items[m_index];
                // Speaking counts toward mastery like a quiz answer; a pass on any try counts.
                if (score.Passed && !m_passed[m_index]) m_wordBank.RecordQuizAnswer(id, true, m_settings.ReadingLevel);
                m_passed[m_index] |= score.Passed;
                // The object answers too: a hop when said right, a shake when not.
                if (m_labels.TryFindByClass(id, out TrackedObject track))
                {
                    if (score.Passed) m_labels.Jump(track);
                    else m_labels.Shake(track);
                }
            }
            Refresh();
        }

        private void Next()
        {
            switch (m_phase)
            {
                case Phase.Item:
                    m_turn.Stop();
                    if (m_single) { Close(); return; }
                    if (m_index + 1 < m_items.Count)
                    {
                        m_index++;
                        StartItem();
                        Refresh();
                        return;
                    }
                    m_phase = Phase.Summary;
                    m_labels.SetSelected(null);
                    int passed = 0;
                    foreach (bool p in m_passed) if (p) passed++;
                    Debug.Log($"[WordWorld.SayIt] round {passed}/{m_items.Count}");
                    Refresh();
                    RoundFinished?.Invoke(passed, m_items.Count);
                    break;
                case Phase.Summary:
                    // Try again: the same words, so the learner can fix the ones they missed.
                    Open();
                    break;
                case Phase.NotEnough:
                    Close();
                    break;
            }
        }

        /// <summary>Words said correctly in the last round, and how many were needed to pass.</summary>
        public bool LastRoundPassed
        {
            get
            {
                int passed = 0;
                foreach (bool p in m_passed) if (p) passed++;
                return m_items.Count > 0 && passed >= m_passNeeded;
            }
        }

        private void OnTurnChanged(TurnState state)
        {
            if (m_phase == Phase.Item) Refresh();
        }

        private void OnSettingsChanged()
        {
            if (m_phase == Phase.Item)
            {
                m_target = TargetFor(m_items[m_index]);
                m_score = null;
            }
            Refresh();
        }

        private void Refresh()
        {
            if (m_phase == Phase.Closed || !m_settings.HasLanguage) return;
            string lang = m_settings.TargetLanguage;
            Style(m_title, S("sayit.title"), m_theme.Ink);
            m_close.SetLabel(S("common.close"));
            m_close.Text.TextColor = m_theme.Ink;

            switch (m_phase)
            {
                case Phase.NotEnough:
                    SetVisible(prompt: false, buttons: false);
                    Style(m_result, S("quiz.not_enough"), m_theme.Ink);
                    StyleButton(m_next, S("quiz.done"), true);
                    m_next.gameObject.SetActive(true);
                    HideChips();
                    return;
                case Phase.Summary:
                    RefreshSummary();
                    return;
            }

            SetVisible(prompt: true, buttons: true);
            int id = m_items[m_index];
            Style(m_progress, m_single ? "" : string.Format(S("sayit.progress"), m_index + 1, m_items.Count), m_theme.InkMuted);
            FillPrompt(id, lang);

            bool ready = m_speech.IsReady(lang);
            TurnState turn = m_turn.State;
            m_listen.gameObject.SetActive(m_audio.Has(id, Kind));
            StyleButton(m_listen, S("audio.listen"), false);
            string speak = !ready ? (m_speech.Status == SpeechCheck.ModelStatus.Failed ? S("sayit.unavailable") : S("sayit.preparing"))
                : turn == TurnState.Listening ? S("sayit.listening")
                : turn == TurnState.Checking ? S("sayit.checking")
                : S("sayit.speak");
            StyleButton(m_speak, speak, ready && turn != TurnState.Checking);
            if (turn == TurnState.Listening) m_speak.Background = m_theme.AccentSoft;

            bool passed = m_passed[m_index];
            m_next.gameObject.SetActive(true);
            StyleButton(m_next, S(m_single ? "quiz.done" : m_index + 1 < m_items.Count ? "sayit.next" : "sayit.finish"), passed);

            if (m_score != null && m_message == null) ShowChips(m_score);
            else HideChips();
            string line = m_message != null ? S(m_message)
                : m_score != null ? Feedback(m_score)
                : !ready ? S("sayit.preparing_hint")
                : S("sayit.instruction");
            Color color = m_score != null && m_message == null ? m_theme.Ink : m_theme.InkMuted;
            Style(m_result, line, color);
        }

        private void FillPrompt(int id, string lang)
        {
            m_ruby.gameObject.SetActive(false);
            if (!m_vocabulary.TryGet(id, out Vocabulary.Entry entry)) return;
            if (lang == "zh")
            {
                Vocabulary.ChineseEntry zh = entry.zh;
                int level = m_settings.ReadingLevel;
                // Mirrors the label levels: pinyin, then characters with pinyin above, then characters.
                if (level <= 1) Style(m_prompt, zh.phrasePinyin, m_theme.Accent);
                else
                {
                    Style(m_prompt, zh.phrase, m_theme.Ink);
                    if (level == 2 && m_settings.ShowPinyin)
                    {
                        Style(m_ruby, zh.phrasePinyin, m_theme.Accent);
                        m_ruby.gameObject.SetActive(true);
                    }
                }
                Style(m_meaning, zh.meaning, m_theme.InkMuted);
                return;
            }
            Vocabulary.LatinEntry latin = Vocabulary.LatinFor(entry, lang);
            string text = Kind == ClipKind.Sentence ? Vocabulary.Wrap(latin.sentence, 30, 2)
                : Kind == ClipKind.Phrase ? latin.phrase : latin.word;
            Style(m_prompt, text, m_theme.Ink);
            Style(m_meaning, latin.meaning, m_theme.InkMuted);
        }

        // "Correct! Every word was clear." / "Almost: say cuchara again." / "That sounded like pero; try pelo."
        private string Feedback(SpeechScore score)
        {
            ScoredUnit? wrong = null;
            var again = new List<string>();
            foreach (ScoredUnit unit in score.Units)
            {
                if (unit.Mark == WordMark.Wrong && wrong == null) wrong = unit;
                if (unit.Mark != WordMark.Clear && again.Count < 2) again.Add(unit.Text);
            }
            string join = m_settings.TargetLanguage == "zh" ? "" : " ";
            if (score.Passed)
                return again.Count == 0 ? S("sayit.correct_clear") : string.Format(S("sayit.correct_unclear"), string.Join(join, again));
            if (wrong != null) return string.Format(S("sayit.sounded_like"), wrong.Value.Heard, wrong.Value.Text);
            return string.Format(S("sayit.almost"), string.Join(join, again));
        }

        private void RefreshSummary()
        {
            SetVisible(prompt: true, buttons: false);
            int passed = 0;
            foreach (bool p in m_passed) if (p) passed++;
            Style(m_progress, "", m_theme.InkMuted);
            Style(m_prompt, string.Format(S("sayit.summary"), passed, m_items.Count), m_theme.Ink);
            Style(m_meaning, "", m_theme.InkMuted);

            // One chip per word, mint for said correctly and rose for not yet; the line lists the misses.
            int shown = 0;
            var misses = new List<string>();
            float x = 0f, y = m_chipRow1;
            foreach (SpatialAdapterNativeText chip in m_chips) chip.gameObject.SetActive(false);
            for (int i = 0; i < m_items.Count && shown < m_chips.Length; i++)
            {
                string word = ShortWord(m_items[i]);
                if (!m_passed[i]) misses.Add(word);
                PlaceChip(m_chips[shown++], word, m_passed[i] ? m_theme.AccentSoft : m_theme.Wrong, ref x, ref y);
            }
            CentreChips(shown);
            string line = misses.Count == 0 ? S("sayit.summary_all") : string.Format(S("sayit.summary_practise"), string.Join(", ", misses));
            Style(m_result, line + "\n" + S("sayit.honest"), m_theme.Ink);

            m_next.gameObject.SetActive(true);
            StyleButton(m_next, S("quiz.try_again"), false);
            m_listen.gameObject.SetActive(false);
            m_speak.gameObject.SetActive(true);
            StyleButton(m_speak, S("quiz.done"), true);
        }

        private string ShortWord(int id)
        {
            if (!m_vocabulary.TryGet(id, out Vocabulary.Entry entry)) return "?";
            string lang = m_settings.TargetLanguage;
            if (lang == "zh") return entry.zh.characters;
            return Vocabulary.LatinFor(entry, lang).word;
        }

        private void ShowChips(SpeechScore score)
        {
            float x = 0f, y = m_chipRow1;
            int shown = 0;
            foreach (SpatialAdapterNativeText chip in m_chips) chip.gameObject.SetActive(false);
            foreach (ScoredUnit unit in score.Units)
            {
                if (shown >= m_chips.Length) break;
                Color color = unit.Mark == WordMark.Clear ? m_theme.AccentSoft
                    : unit.Mark == WordMark.Unclear ? m_theme.Hint : m_theme.Wrong;
                PlaceChip(m_chips[shown++], unit.Text, color, ref x, ref y);
            }
            CentreChips(shown);
        }

        // Lays chips left to right in up to two rows; CentreChips shifts each row to the middle.
        private void PlaceChip(SpatialAdapterNativeText chip, string text, Color color, ref float x, ref float y)
        {
            chip.Text = text;
            chip.TextColor = m_theme.Ink;
            chip.BackgroundColor = color;
            TextFit.FitBackground(chip, text, m_chipPoints, 0.026f);
            float width = Width(chip);
            if (x > 0f && x + width > m_chipWidthLimit && Mathf.Approximately(y, m_chipRow1))
            {
                x = 0f;
                y = m_chipRow2;
            }
            Vector3 p = chip.transform.localPosition;
            chip.transform.localPosition = new Vector3(x + width * 0.5f, y, p.z);
            x += width + 0.006f;
            chip.gameObject.SetActive(true);
        }

        // The background size can't be read back from native text; FitBackground keeps the chip's
        // collider the same size, so that is the width.
        private static float Width(SpatialAdapterNativeText chip) => chip.GetComponent<BoxCollider>().size.x;

        private void CentreChips(int count)
        {
            foreach (float row in new[] { m_chipRow1, m_chipRow2 })
            {
                float right = 0f;
                for (int i = 0; i < count; i++)
                    if (Mathf.Approximately(m_chips[i].transform.localPosition.y, row))
                        right = Mathf.Max(right, m_chips[i].transform.localPosition.x + Width(m_chips[i]) * 0.5f);
                for (int i = 0; i < count; i++)
                {
                    Vector3 p = m_chips[i].transform.localPosition;
                    if (Mathf.Approximately(p.y, row)) m_chips[i].transform.localPosition = new Vector3(p.x - right * 0.5f, p.y, p.z);
                }
            }
        }

        private void HideChips()
        {
            foreach (SpatialAdapterNativeText chip in m_chips)
                if (chip.gameObject.activeSelf) chip.gameObject.SetActive(false);
        }

        private void SetVisible(bool prompt, bool buttons)
        {
            m_prompt.gameObject.SetActive(prompt);
            m_meaning.gameObject.SetActive(prompt);
            m_progress.gameObject.SetActive(prompt);
            if (!prompt) m_ruby.gameObject.SetActive(false);
            m_listen.gameObject.SetActive(buttons);
            m_speak.gameObject.SetActive(buttons);
        }

        private void StyleButton(SpatialButton button, string text, bool primary)
        {
            button.SetLabel(text);
            button.Text.TextColor = m_theme.Ink;
            button.Background = primary ? m_theme.Primary : m_theme.Button;
        }

        private static void Style(SpatialAdapterNativeText text, string value, Color color)
        {
            text.SetFitted(value ?? "");
            text.TextColor = color;
        }

        private static void Shuffle(List<int> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        private string S(string key) => m_strings.Get(key, m_settings.InterfaceLanguage);
    }
}
