using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace WordWorld.App
{
    /// <summary>
    /// Collected words and their mastery (CLAUDE.md section 9). Words are collected on first tap or
    /// with Save on the detail card (DESIGN.md section 4). Saved as JSON in persistentDataPath.
    /// </summary>
    [AddComponentMenu("Word World/Word Bank")]
    public sealed class WordBank : MonoBehaviour
    {
        public enum Mastery { New, Learning, Mastered }

        [Serializable]
        public sealed class Word
        {
            public int classId;
            public bool saved;
            public int taps;
            public int quizCorrect;
            public int quizAttempts;
            public string firstCollected;
            // Bit (level - 1) set once answered correctly at that reading level.
            public int correctAtLevels;
        }

        [Serializable]
        private sealed class SaveData
        {
            public List<Word> words = new List<Word>();
            public int bestStreak;
            public int[] levelAttempts = new int[3];
            public int[] levelCorrect = new int[3];
            public string lastActiveDay = "";
            public int dayStreak;
        }

        [Tooltip("Correct quiz answers needed for a word to count as mastered.")]
        [SerializeField] private int m_masteredAfter = 3;

        private SaveData m_data = new SaveData();
        private readonly Dictionary<int, Word> m_byClass = new Dictionary<int, Word>();

        public event Action Changed;

        public IReadOnlyList<Word> Words => m_data.words;
        public int BestStreak => m_data.bestStreak;
        /// <summary>Consecutive days with the app opened, today included.</summary>
        public int DayStreak => m_data.dayStreak;

        public int TotalAttempts
        {
            get
            {
                int total = 0;
                foreach (Word word in m_data.words) total += word.quizAttempts;
                return total;
            }
        }

        public int TotalCorrect
        {
            get
            {
                int total = 0;
                foreach (Word word in m_data.words) total += word.quizCorrect;
                return total;
            }
        }

        /// <summary>
        /// DESIGN.md section 5.3: ready for the next level after 80%+ accuracy on 20 words at this one.
        /// </summary>
        public bool ReadyForNextLevel(int level)
        {
            if (level < 1 || level >= 3) return false;
            int attempts = m_data.levelAttempts[level - 1];
            if (attempts == 0 || m_data.levelCorrect[level - 1] < 0.8f * attempts) return false;
            int words = 0;
            foreach (Word word in m_data.words)
                if ((word.correctAtLevels & (1 << (level - 1))) != 0) words++;
            return words >= 20;
        }

        public bool TryGet(int classId, out Word word) => m_byClass.TryGetValue(classId, out word);

        private static string SavePath => Path.Combine(Application.persistentDataPath, "wordbank.json");

        private void Awake()
        {
            Load();
            TouchDay();
        }

        public bool Contains(int classId) => m_byClass.ContainsKey(classId);

        public bool IsSaved(int classId) => m_byClass.TryGetValue(classId, out Word word) && word.saved;

        public Mastery MasteryOf(int classId)
        {
            if (!m_byClass.TryGetValue(classId, out Word word) || word.quizCorrect == 0) return Mastery.New;
            return word.quizCorrect >= m_masteredAfter ? Mastery.Mastered : Mastery.Learning;
        }

        /// <summary>Tapping a label collects its word.</summary>
        public void RecordTap(int classId)
        {
            GetOrAdd(classId).taps++;
            SaveAndNotify();
        }

        public void SetSaved(int classId, bool saved)
        {
            Word word = GetOrAdd(classId);
            if (word.saved == saved) return;
            word.saved = saved;
            SaveAndNotify();
        }

        public void RecordQuizAnswer(int classId, bool correct, int level)
        {
            Word word = GetOrAdd(classId);
            word.quizAttempts++;
            int index = Mathf.Clamp(level, 1, 3) - 1;
            m_data.levelAttempts[index]++;
            if (correct)
            {
                word.quizCorrect++;
                word.correctAtLevels |= 1 << index;
                m_data.levelCorrect[index]++;
            }
            SaveAndNotify();
        }

        public void RecordStreak(int streak)
        {
            if (streak <= m_data.bestStreak) return;
            m_data.bestStreak = streak;
            SaveAndNotify();
        }

        // Counted on launch: today extends the streak if the last active day was yesterday.
        private void TouchDay()
        {
            DateTime today = DateTime.Now.Date;
            string key = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (m_data.lastActiveDay == key) return;
            bool yesterday = DateTime.TryParseExact(m_data.lastActiveDay, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out DateTime last) && last.Date == today.AddDays(-1);
            m_data.dayStreak = yesterday ? m_data.dayStreak + 1 : 1;
            m_data.lastActiveDay = key;
            Save();
        }

        private Word GetOrAdd(int classId)
        {
            if (m_byClass.TryGetValue(classId, out Word word)) return word;
            word = new Word { classId = classId, firstCollected = DateTime.UtcNow.ToString("o") };
            m_data.words.Add(word);
            m_byClass.Add(classId, word);
            return word;
        }

        private void SaveAndNotify()
        {
            Save();
            Changed?.Invoke();
        }

        private void Load()
        {
            try
            {
                if (!File.Exists(SavePath)) return;
                SaveData loaded = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
                if (loaded?.words == null) return;
                if (loaded.levelAttempts == null || loaded.levelAttempts.Length != 3) loaded.levelAttempts = new int[3];
                if (loaded.levelCorrect == null || loaded.levelCorrect.Length != 3) loaded.levelCorrect = new int[3];
                m_data = loaded;
                foreach (Word word in m_data.words) m_byClass[word.classId] = word;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[WordWorld.WordBank] could not read {SavePath}: {e.Message}");
            }
        }

        private void Save()
        {
            try
            {
                File.WriteAllText(SavePath, JsonUtility.ToJson(m_data, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[WordWorld.WordBank] could not write {SavePath}: {e.Message}");
            }
        }
    }
}
