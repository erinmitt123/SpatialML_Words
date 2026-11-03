using System.Collections.Generic;
using ByteDance.PICO.SpatialAdapter.Component;
using UnityEngine;
using WordWorld.App;
using WordWorld.Data;
using WordWorld.Detection;

namespace WordWorld.UI
{
    /// <summary>
    /// Progress window (DESIGN.md section 5.3): words learned, reading level, quiz accuracy, daily
    /// streak, a bar per category, and a "Ready for Level N?" card once the learner gets 80%+ on 20
    /// words at their level.
    /// </summary>
    [AddComponentMenu("Word World/Progress Window")]
    public sealed class ProgressWindow : MonoBehaviour
    {
        [SerializeField] private LearnerSettings m_settings;
        [SerializeField] private UiStrings m_strings;
        [SerializeField] private Vocabulary m_vocabulary;
        [SerializeField] private WordBank m_wordBank;
        [SerializeField] private WordWorldTheme m_theme;

        [SerializeField] private GameObject m_window;
        [SerializeField] private SpatialAdapterNativeText m_title;
        [SerializeField] private SpatialAdapterNativeText m_bigNumber;
        [SerializeField] private SpatialAdapterNativeText m_caption;
        [SerializeField] private SpatialAdapterNativeText m_stats;
        [SerializeField] private SpatialAdapterNativeText[] m_barNames = System.Array.Empty<SpatialAdapterNativeText>();
        [SerializeField] private SpatialAdapterNativeText[] m_barFills = System.Array.Empty<SpatialAdapterNativeText>();
        [SerializeField] private SpatialAdapterNativeText[] m_barCounts = System.Array.Empty<SpatialAdapterNativeText>();
        [SerializeField] private float m_barLeft = -0.02f;
        [SerializeField] private float m_barWidth = 0.13f;
        [SerializeField] private float m_barHeight = 0.012f;

        [Header("Level up card")]
        [SerializeField] private GameObject m_levelUp;
        [SerializeField] private SpatialAdapterNativeText m_levelUpText;
        [SerializeField] private SpatialButton m_tryIt;
        [SerializeField] private SpatialButton m_notYet;

        private readonly List<string> m_categories = new List<string>();
        private readonly Dictionary<string, int> m_available = new Dictionary<string, int>();
        private readonly Dictionary<string, int> m_learned = new Dictionary<string, int>();
        private bool m_levelUpDismissed;

        public GameObject Window => m_window;

        private void Awake()
        {
            m_window.SetActive(false);
            m_levelUp.SetActive(false);
            m_tryIt.Clicked += _ =>
            {
                m_settings.SetReadingLevel(m_settings.ReadingLevel + 1);
                m_levelUpDismissed = false;
            };
            m_notYet.Clicked += _ =>
            {
                m_levelUpDismissed = true;
                Refresh();
            };
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

        public void Show()
        {
            m_window.SetActive(true);
            Refresh();
        }

        private void Refresh()
        {
            if (!m_window.activeSelf || !m_settings.HasLanguage) return;
            int words = m_wordBank.Words.Count;
            m_title.Text = S("progress.title");
            m_title.TextColor = m_theme.Ink;
            m_bigNumber.Text = words.ToString();
            m_bigNumber.TextColor = m_theme.Accent;
            m_caption.Text = S("progress.words_learned");
            m_caption.TextColor = m_theme.InkMuted;

            int attempts = m_wordBank.TotalAttempts;
            string accuracy = attempts > 0 ? $"{Mathf.RoundToInt(100f * m_wordBank.TotalCorrect / attempts)}%" : "-";
            m_stats.Text = $"{S("progress.level")} {m_settings.ReadingLevel}   ·   {S("progress.accuracy")} {accuracy}\n" +
                           string.Format(S("progress.day_streak"), m_wordBank.DayStreak);
            m_stats.TextColor = m_theme.Ink;

            RefreshBars();

            int level = m_settings.ReadingLevel;
            bool ready = !m_levelUpDismissed && m_wordBank.ReadyForNextLevel(level);
            m_levelUp.SetActive(ready);
            if (ready)
            {
                m_levelUpText.Text = string.Format(S("progress.level_up"), level + 1);
                m_levelUpText.TextColor = m_theme.Ink;
                m_tryIt.Text.Text = S("progress.try_it");
                m_tryIt.Text.TextColor = m_theme.Ink;
                m_tryIt.Text.BackgroundColor = m_theme.Primary;
                m_notYet.Text.Text = S("progress.not_yet");
            }
        }

        // Words learned out of those the detector can find, for the categories with the most words.
        private void RefreshBars()
        {
            m_available.Clear();
            m_learned.Clear();
            m_categories.Clear();
            for (int classId = 0; classId < CocoClasses.Names.Length; classId++)
            {
                if (!CocoClasses.IsHousehold(classId) || !m_vocabulary.TryGet(classId, out Vocabulary.Entry entry) || !entry.enabled)
                    continue;
                string category = entry.category ?? "";
                m_available.TryGetValue(category, out int available);
                m_available[category] = available + 1;
                if (m_wordBank.Contains(classId))
                {
                    m_learned.TryGetValue(category, out int learned);
                    m_learned[category] = learned + 1;
                }
            }
            m_categories.AddRange(m_available.Keys);
            m_categories.Sort((a, b) => m_available[b] != m_available[a] ? m_available[b].CompareTo(m_available[a]) : string.CompareOrdinal(a, b));

            for (int i = 0; i < m_barNames.Length; i++)
            {
                bool used = i < m_categories.Count;
                m_barNames[i].gameObject.SetActive(used);
                m_barCounts[i].gameObject.SetActive(used);
                if (!used)
                {
                    m_barFills[i].gameObject.SetActive(false);
                    continue;
                }
                string category = m_categories[i];
                m_learned.TryGetValue(category, out int learned);
                int available = m_available[category];
                m_barNames[i].Text = S($"category.{category}");
                m_barNames[i].TextColor = m_theme.Ink;
                m_barCounts[i].Text = $"{learned}/{available}";
                m_barCounts[i].TextColor = m_theme.InkMuted;

                // The fill grows from the left end of its track.
                float width = m_barWidth * learned / Mathf.Max(1, available);
                SpatialAdapterNativeText fill = m_barFills[i];
                fill.gameObject.SetActive(learned > 0);
                if (learned == 0) continue;
                fill.BackgroundSize = new Vector2(width, m_barHeight);
                Vector3 p = fill.transform.localPosition;
                fill.transform.localPosition = new Vector3(m_barLeft + width * 0.5f, p.y, p.z);
            }
        }

        private string S(string key) => m_strings.Get(key, m_settings.InterfaceLanguage);
    }
}
