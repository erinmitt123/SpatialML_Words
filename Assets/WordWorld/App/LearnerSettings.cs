using System;
using System.IO;
using UnityEngine;

namespace WordWorld.App
{
    /// <summary>
    /// The Sound setting. Off: silent. Alerts: short sound effects only (ripples, chimes, pings).
    /// All: effects, background music and spoken words.
    /// </summary>
    public enum SoundMode { Off, Alerts, All }

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
            // Before the Sound setting: on/off for pronunciation clips. Read only to migrate.
            public bool audioOn = true;
            // off, alerts or all; empty in files saved before it existed.
            public string soundMode = "";
            // Was the practice objects switch; they are session only now, so this is never read.
            public bool practiceObjects;
            // Journey (DESIGN.md section 5): 0 = read labels, 1 = Find It round, 2 = Say It round.
            public int journeyStep;
            // Total label taps when the current read step began.
            public int journeyBaseTaps;
            // Where the learner moved the dock, relative to the direction they face (metres:
            // right, up, forward). Unset until they move it.
            public bool dockMoved;
            public Vector3 dockOffset;
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
        /// <summary>Off, Alerts (effects only) or All (effects, music and spoken words).</summary>
        public SoundMode Sound => ParseSound(m_data.soundMode, m_data.audioOn);
        /// <summary>Pronunciation clips play only with Sound on All; otherwise Listen buttons hide.</summary>
        public bool SpokenWords => Sound == SoundMode.All;
        public const float MinConfidence = 0.35f;
        public const float MaxConfidence = 0.85f;
        /// <summary>Journey step: 0 = read labels, 1 = pass a Find It round, 2 = pass a Say It round.</summary>
        public int JourneyStep => m_data.journeyStep;
        /// <summary>Total label taps when the read step began; taps are counted from here.</summary>
        public int JourneyBaseTaps => m_data.journeyBaseTaps;
        /// <summary>False until a language is picked this session, so labels wait for the picker.</summary>
        public bool SessionReady { get; private set; }

        /// <summary>English learners get a Chinese interface; everyone else gets English.</summary>
        public static string InterfaceFor(string target) => target == "en" ? "zh" : "en";

        /// <summary>
        /// The headset's language as an interface language: zh for any Chinese system language,
        /// otherwise en (the only two interfaces). Pre-selects "You speak" on the first question.
        /// </summary>
        public static string SystemLanguage => SystemLanguageOverride ??
            (Application.systemLanguage == UnityEngine.SystemLanguage.Chinese ||
             Application.systemLanguage == UnityEngine.SystemLanguage.ChineseSimplified ||
             Application.systemLanguage == UnityEngine.SystemLanguage.ChineseTraditional ? "zh" : "en");

        /// <summary>For editor checks: stands in for the headset language when set.</summary>
        public static string SystemLanguageOverride { get; set; }

        /// <summary>Where the learner put the dock, relative to the way they face; false until moved.</summary>
        public bool TryGetDockOffset(out Vector3 offset)
        {
            offset = m_data.dockOffset;
            return m_data.dockMoved;
        }

        // Saved without raising Changed: nothing else depends on where the dock sits.
        public void SetDockOffset(Vector3 offset)
        {
            m_data.dockMoved = true;
            m_data.dockOffset = offset;
            Save();
        }

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

        // Session only: practice objects are placed for the room the learner is in now, so they
        // never carry over to the next launch (where the room, or the furniture, may differ).
        private bool m_practiceObjects;

        public bool PracticeObjects => m_practiceObjects;

        public void SetPracticeObjects(bool on)
        {
            if (m_practiceObjects == on) return;
            m_practiceObjects = on;
            Changed?.Invoke();
        }

        public void SetSound(SoundMode mode)
        {
            string value = SoundKey(mode);
            Apply(() => m_data.soundMode = value, m_data.soundMode != value);
        }

        public static string SoundKey(SoundMode mode) =>
            mode == SoundMode.Off ? "off" : mode == SoundMode.Alerts ? "alerts" : "all";

        // Files from before the Sound setting keep what they had: audio off stays silent.
        private static SoundMode ParseSound(string value, bool legacyAudioOn) =>
            value == "off" ? SoundMode.Off
            : value == "alerts" ? SoundMode.Alerts
            : value == "all" ? SoundMode.All
            : legacyAudioOn ? SoundMode.All : SoundMode.Off;

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

        public void SetJourney(int step, int baseTaps) =>
            Apply(() => { m_data.journeyStep = step; m_data.journeyBaseTaps = baseTaps; },
                m_data.journeyStep != step || m_data.journeyBaseTaps != baseTaps);

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
                loaded.journeyStep = Mathf.Clamp(loaded.journeyStep, 0, 2);
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
