using System.Collections.Generic;
using System.Text;
using ByteDance.PICO.SpatialAdapter.Component;
using UnityEngine;
using WordWorld.App;
using WordWorld.Data;
using WordWorld.Labels;
using WordWorld.Tracking;

namespace WordWorld.UI
{
    /// <summary>
    /// Word Bank window (DESIGN.md section 5.1): collected words with meaning and mastery dots,
    /// filter chips, and paging. Tapping a word makes its real object pulse, or says it is not in
    /// this room. Rebuilt from the WordBank and LearnerSettings stores whenever either changes.
    /// </summary>
    [AddComponentMenu("Word World/Word Bank Window")]
    public sealed class WordBankWindow : MonoBehaviour
    {
        private enum Filter { All, ThisRoom, New, Learning, Mastered }

        [SerializeField] private LearnerSettings m_settings;
        [SerializeField] private UiStrings m_strings;
        [SerializeField] private Vocabulary m_vocabulary;
        [SerializeField] private WordBank m_wordBank;
        [SerializeField] private ObjectLabels m_labels;
        [SerializeField] private WordWorldTheme m_theme;

        [SerializeField] private GameObject m_window;
        [SerializeField] private SpatialAdapterNativeText m_header;
        [SerializeField] private SpatialAdapterNativeText m_empty;
        [SerializeField] private SpatialAdapterNativeText m_message;
        [SerializeField] private SpatialAdapterNativeText m_page;
        [SerializeField] private SpatialButton[] m_filters = System.Array.Empty<SpatialButton>();
        [SerializeField] private SpatialButton[] m_rows = System.Array.Empty<SpatialButton>();
        [SerializeField] private SpatialButton m_previous;
        [SerializeField] private SpatialButton m_next;

        [SerializeField] private float m_highlightSeconds = 2f;

        private readonly List<WordBank.Word> m_shown = new List<WordBank.Word>();
        private readonly int[] m_rowClass = new int[16];
        private readonly StringBuilder m_builder = new StringBuilder(64);
        private Filter m_filter;
        private int m_pageIndex;
        private float m_messageUntil;

        public GameObject Window => m_window;

        private void Awake()
        {
            m_window.SetActive(false);
            foreach (SpatialButton filter in m_filters) filter.Clicked += OnFilter;
            for (int i = 0; i < m_rows.Length; i++)
            {
                int row = i;
                m_rows[i].Clicked += _ => OnRow(row);
            }
            m_previous.Clicked += _ => Page(-1);
            m_next.Clicked += _ => Page(+1);
        }

        private void OnEnable()
        {
            m_settings.Changed += Refresh;
            m_wordBank.Changed += Refresh;
        }

        private void OnDisable()
        {
            m_settings.Changed -= Refresh;
            m_wordBank.Changed -= Refresh;
        }

        private void Update()
        {
            if (m_message.gameObject.activeSelf && Time.unscaledTime > m_messageUntil) m_message.gameObject.SetActive(false);
        }

        public void Show()
        {
            m_window.SetActive(true);
            Refresh();
        }

        private void OnFilter(SpatialButton button)
        {
            m_filter = (Filter)System.Enum.Parse(typeof(Filter), button.Value);
            m_pageIndex = 0;
            Refresh();
        }

        private void Page(int delta)
        {
            m_pageIndex += delta;
            Refresh();
        }

        private void OnRow(int row)
        {
            int classId = m_rowClass[row];
            if (m_labels.TryFindByClass(classId, out TrackedObject track))
            {
                m_labels.Pulse(track, m_highlightSeconds);
                return;
            }
            m_message.Text = S("wordbank.not_here");
            m_message.TextColor = m_theme.InkMuted;
            m_message.gameObject.SetActive(true);
            m_messageUntil = Time.unscaledTime + m_highlightSeconds;
        }

        private bool Passes(WordBank.Word word)
        {
            switch (m_filter)
            {
                case Filter.ThisRoom: return m_labels.TryFindByClass(word.classId, out _);
                case Filter.New: return m_wordBank.MasteryOf(word.classId) == WordBank.Mastery.New;
                case Filter.Learning: return m_wordBank.MasteryOf(word.classId) == WordBank.Mastery.Learning;
                case Filter.Mastered: return m_wordBank.MasteryOf(word.classId) == WordBank.Mastery.Mastered;
                default: return true;
            }
        }

        private void Refresh()
        {
            if (!m_window.activeSelf || !m_settings.HasLanguage) return;
            IReadOnlyList<WordBank.Word> words = m_wordBank.Words;
            m_header.Text = $"{S("wordbank.title")}   {string.Format(S("wordbank.count"), words.Count)}";
            m_header.TextColor = m_theme.Ink;

            foreach (SpatialButton filter in m_filters)
            {
                bool selected = filter.Value == m_filter.ToString();
                filter.SetLabel(S($"wordbank.filter.{filter.Value}"));
                filter.Text.TextColor = m_theme.Ink;
                filter.Text.BackgroundColor = selected ? m_theme.AccentSoft : m_theme.Button;
            }

            // Newest first.
            m_shown.Clear();
            for (int i = words.Count - 1; i >= 0; i--)
                if (m_vocabulary.TryGet(words[i].classId, out _) && Passes(words[i])) m_shown.Add(words[i]);

            int perPage = m_rows.Length;
            int pages = Mathf.Max(1, (m_shown.Count + perPage - 1) / perPage);
            m_pageIndex = Mathf.Clamp(m_pageIndex, 0, pages - 1);
            for (int r = 0; r < perPage; r++)
            {
                int index = m_pageIndex * perPage + r;
                bool used = index < m_shown.Count;
                m_rows[r].gameObject.SetActive(used);
                if (!used) continue;
                m_rowClass[r] = m_shown[index].classId;
                m_rows[r].SetLabel(RowText(m_shown[index].classId));
                m_rows[r].Text.TextColor = m_theme.Ink;
            }

            m_empty.gameObject.SetActive(words.Count == 0);
            if (words.Count == 0)
            {
                m_empty.Text = S("wordbank.empty");
                m_empty.TextColor = m_theme.InkMuted;
            }
            m_page.Text = pages > 1 ? $"{m_pageIndex + 1} / {pages}" : "";
            m_page.TextColor = m_theme.InkMuted;
            m_previous.gameObject.SetActive(m_pageIndex > 0);
            m_next.gameObject.SetActive(m_pageIndex < pages - 1);
        }

        // "la silla   chair   ●●○": the word at the learner's reading level, its meaning, mastery.
        private string RowText(int classId)
        {
            m_builder.Clear();
            string lang = m_settings.TargetLanguage;
            if (m_vocabulary.TryGetLabel(classId, lang, m_settings.ReadingLevel, m_settings.ShowPinyin,
                    out Vocabulary.LabelContent content))
            {
                string main = content.Main.Replace('\n', ' ');
                if (main.Length > 26) main = main.Substring(0, 25) + "…";
                m_builder.Append(main);
                if (!string.IsNullOrEmpty(content.Ruby)) m_builder.Append("  ").Append(content.Ruby);
            }
            if (m_vocabulary.TryGet(classId, out Vocabulary.Entry entry))
            {
                string meaning = lang == "zh" ? entry.zh?.meaning : Vocabulary.LatinFor(entry, lang)?.meaning;
                if (!string.IsNullOrEmpty(meaning)) m_builder.Append("   ").Append(meaning);
            }
            m_builder.Append("   ").Append(MasteryDots(classId));
            return m_builder.ToString();
        }

        private string MasteryDots(int classId)
        {
            switch (m_wordBank.MasteryOf(classId))
            {
                case WordBank.Mastery.Mastered: return "●●●";
                case WordBank.Mastery.Learning:
                    return m_wordBank.TryGet(classId, out WordBank.Word word) && word.quizCorrect >= 2 ? "●●○" : "●○○";
                default: return "○○○";
            }
        }

        private string S(string key) => m_strings.Get(key, m_settings.InterfaceLanguage);
    }
}
