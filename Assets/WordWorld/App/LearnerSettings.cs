using System;
using System.IO;
using UnityEngine;

namespace WordWorld.App
{
    /// <summary>
    /// The shared learner state store (CLAUDE.md section 8): target language and reading level.
    /// Panels and labels listen to <see cref="Changed"/>. Saved as JSON in persistentDataPath.
    /// </summary>
    [AddComponentMenu("Word World/Learner Settings")]
    public sealed class LearnerSettings : MonoBehaviour
    {
        public static readonly string[] TargetLanguages = { "zh", "es", "fr", "de", "en" };

        [Serializable]
        private sealed class SaveData
        {
            public string targetLanguage = "";
            public string interfaceLanguage = "en";
            public int readingLevel = 1;
            public bool onboardingDone;
            public bool labelPeople;
            public bool showPinyin = true;
            public bool showMeaning = true;
            public string labelSize = "M";
            public float confidenceThreshold = 0.55f;
        }

        private SaveData m_data = new SaveData();

        public event Action Changed;

        /// <summary>zh, es, fr, de or en; empty until the learner has chosen once.</summary>
        public string TargetLanguage => m_data.targetLanguage;
        public bool HasLanguage => !string.IsNullOrEmpty(m_data.targetLanguage);
        /// <summary>en or zh. Set automatically from the target language; Settings can change it later.</summary>
        public string InterfaceLanguage => m_data.interfaceLanguage;
        /// <summary>The first-time flow (DESIGN.md section 7) runs until this is set.</summary>
        public bool OnboardingDone => m_data.onboardingDone;
        /// <summary>DESIGN.md section 12: people get no label text unless the learner turns this on.</summary>
        public bool LabelPeople => m_data.labelPeople;
        public int ReadingLevel => m_data.readingLevel;
        /// <summary>Mandarin only: the pinyin ruby line over characters at level 2.</summary>
        public bool ShowPinyin => m_data.showPinyin;
        /// <summary>The meaning in the interface language under each label's word.</summary>
        public bool ShowMeaning => m_data.showMeaning;
        /// <summary>S, M or L.</summary>
        public string LabelSize => m_data.labelSize;
        public float LabelScale => m_data.labelSize == "S" ? 0.8f : m_data.labelSize == "L" ? 1.25f : 1f;
        public float ConfidenceThreshold => m_data.confidenceThreshold;
        public const float MinConfidence = 0.35f;
        public const float MaxConfidence = 0.85f;
        /// <summary>False until a language is picked this session, so labels wait for the picker.</summary>
        public bool SessionReady { get; private set; }

        /// <summary>English learners get a Chinese interface; everyone else gets English.</summary>
        public static string InterfaceFor(string target) => target == "en" ? "zh" : "en";

        private static string SavePath => Path.Combine(Application.persistentDataPath, "settings.json");

        private void Awake() => Load();

        public void ChooseLanguage(string target)
        {
            if (Array.IndexOf(TargetLanguages, target) < 0)
            {
                Debug.LogError($"[WordWorld.Settings] unknown language '{target}'");
                return;
            }
            m_data.targetLanguage = target;
            m_data.interfaceLanguage = InterfaceFor(target);
            SessionReady = true;
            Save();
            Debug.Log($"[WordWorld.Settings] learning '{target}' with a '{InterfaceLanguage}' interface, level {ReadingLevel}");
            Changed?.Invoke();
        }

        /// <summary>Later launches skip the picker and continue with the saved language.</summary>
        public void ResumeSession()
        {
            if (!HasLanguage || SessionReady) return;
            SessionReady = true;
            Debug.Log($"[WordWorld.Settings] resuming '{TargetLanguage}' with a '{InterfaceLanguage}' interface, level {ReadingLevel}");
            Changed?.Invoke();
        }

        public void CompleteOnboarding()
        {
            if (m_data.onboardingDone) return;
            m_data.onboardingDone = true;
            Save();
        }

        public void SetInterfaceLanguage(string language)
        {
            if (language != "en" && language != "zh") return;
            Apply(() => m_data.interfaceLanguage = language, m_data.interfaceLanguage != language);
        }

        public void SetShowPinyin(bool show) => Apply(() => m_data.showPinyin = show, m_data.showPinyin != show);

        public void SetShowMeaning(bool show) => Apply(() => m_data.showMeaning = show, m_data.showMeaning != show);

        public void SetLabelPeople(bool label) => Apply(() => m_data.labelPeople = label, m_data.labelPeople != label);

        public void SetLabelSize(string size)
        {
            if (size != "S" && size != "M" && size != "L") return;
            Apply(() => m_data.labelSize = size, m_data.labelSize != size);
        }

        public void SetConfidenceThreshold(float value)
        {
            value = Mathf.Clamp(Mathf.Round(value * 20f) / 20f, MinConfidence, MaxConfidence);
            Apply(() => m_data.confidenceThreshold = value, !Mathf.Approximately(m_data.confidenceThreshold, value));
        }

        private void Apply(Action change, bool changed)
        {
            if (!changed) return;
            change();
            Save();
            Changed?.Invoke();
        }

        public void SetReadingLevel(int level)
        {
            level = Mathf.Clamp(level, 1, 3);
            if (level == m_data.readingLevel) return;
            m_data.readingLevel = level;
            Save();
            Changed?.Invoke();
        }

        private void Load()
        {
            try
            {
                if (!File.Exists(SavePath)) return;
                SaveData loaded = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
                if (loaded == null) return;
                if (Array.IndexOf(TargetLanguages, loaded.targetLanguage) < 0) loaded.targetLanguage = "";
                loaded.readingLevel = Mathf.Clamp(loaded.readingLevel, 1, 3);
                if (loaded.interfaceLanguage != "zh") loaded.interfaceLanguage = "en";
                if (loaded.labelSize != "S" && loaded.labelSize != "L") loaded.labelSize = "M";
                loaded.confidenceThreshold = Mathf.Clamp(loaded.confidenceThreshold, MinConfidence, MaxConfidence);
                m_data = loaded;
            }
            catch (Exception e)
            {
                // A corrupt file should cost the learner their settings, not the session.
                Debug.LogWarning($"[WordWorld.Settings] could not read {SavePath}: {e.Message}");
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
                Debug.LogWarning($"[WordWorld.Settings] could not write {SavePath}: {e.Message}");
            }
        }
    }
}
