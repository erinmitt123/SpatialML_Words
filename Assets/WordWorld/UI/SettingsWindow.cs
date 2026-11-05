using ByteDance.PICO.SpatialAdapter.Component;
using UnityEngine;
using WordWorld.App;
using WordWorld.Data;
using WordWorld.Tracking;

namespace WordWorld.UI
{
    /// <summary>
    /// Settings window (DESIGN.md section 5.4). Every button writes to the shared LearnerSettings
    /// store; labels, the detail card and this window all update from its Changed event. Opens in
    /// front of the user and stays where it opened.
    /// </summary>
    [AddComponentMenu("Word World/Settings Window")]
    public sealed class SettingsWindow : MonoBehaviour
    {
        // Chair: the live preview DESIGN.md asks for.
        private const int PreviewClassId = 56;

        [SerializeField] private LearnerSettings m_settings;
        [SerializeField] private UiStrings m_strings;
        [SerializeField] private Vocabulary m_vocabulary;
        [SerializeField] private TrackingManager m_tracking;
        [SerializeField] private WordWorldTheme m_theme;

        [SerializeField] private GameObject m_window;
        [SerializeField] private SpatialButton[] m_buttons = System.Array.Empty<SpatialButton>();
        [SerializeField] private SpatialAdapterNativeText m_title;
        [SerializeField] private SpatialAdapterNativeText m_learningRow;
        [SerializeField] private SpatialAdapterNativeText m_interfaceRow;
        [SerializeField] private SpatialAdapterNativeText m_levelRow;
        [SerializeField] private SpatialAdapterNativeText m_preview;
        [SerializeField] private SpatialAdapterNativeText m_sizeRow;
        [SerializeField] private SpatialAdapterNativeText m_confidenceRow;
        [SerializeField] private SpatialAdapterNativeText m_privacy;

        [Tooltip("In front of the window row (1.2 m) so the two never intersect.")]
        [SerializeField] private Vector3 m_offsetFromHead = new Vector3(0f, -0.08f, 0.95f);

        public bool IsOpen => m_window.activeSelf;

        private void Awake()
        {
            m_window.SetActive(false);
            foreach (SpatialButton button in m_buttons) button.Clicked += OnClicked;
        }

        private void OnEnable()
        {
            if (m_settings != null) m_settings.Changed += OnChanged;
        }

        private void OnDisable()
        {
            if (m_settings != null) m_settings.Changed -= OnChanged;
        }

        // The detector threshold lives in the store so it survives restarts.
        private void Start() => ApplyToTracking();

        private void OnChanged()
        {
            ApplyToTracking();
            if (IsOpen) Refresh();
        }

        private void ApplyToTracking()
        {
            if (m_tracking != null) m_tracking.ConfidenceThreshold = m_settings.ConfidenceThreshold;
        }

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        public void Open()
        {
            Refresh();
            WindowPlacement.PlaceInFront(m_window.transform, m_offsetFromHead);
            m_window.SetActive(true);
        }

        public void Close() => m_window.SetActive(false);

        private void OnClicked(SpatialButton button)
        {
            string value = button.Value;
            int colon = value.IndexOf(':');
            string kind = colon < 0 ? value : value.Substring(0, colon);
            string arg = colon < 0 ? "" : value.Substring(colon + 1);
            switch (kind)
            {
                case "lang": m_settings.ChooseLanguage(arg); break;
                case "ui": m_settings.SetInterfaceLanguage(arg); break;
                case "level": m_settings.SetReadingLevel(arg[0] - '0'); break;
                case "pinyin": m_settings.SetShowPinyin(!m_settings.ShowPinyin); break;
                case "meaning": m_settings.SetShowMeaning(!m_settings.ShowMeaning); break;
                case "people": m_settings.SetLabelPeople(!m_settings.LabelPeople); break;
                case "audio": m_settings.SetAudioOn(!m_settings.AudioOn); break;
                case "size": m_settings.SetLabelSize(arg); break;
                case "conf": m_settings.SetConfidenceThreshold(m_settings.ConfidenceThreshold + (arg == "+" ? 0.05f : -0.05f)); break;
                case "close": Close(); break;
            }
        }

        private void Refresh()
        {
            string ui = m_settings.InterfaceLanguage;
            Set(m_title, S("settings.title"), m_theme.Ink);
            Set(m_learningRow, S("settings.learning"), m_theme.InkMuted);
            Set(m_interfaceRow, S("settings.interface"), m_theme.InkMuted);
            Set(m_levelRow, S("settings.level"), m_theme.InkMuted);
            Set(m_sizeRow, S("settings.label_size"), m_theme.InkMuted);
            Set(m_confidenceRow, $"{S("settings.confidence")}   {Mathf.RoundToInt(m_settings.ConfidenceThreshold * 100f)}%", m_theme.InkMuted);
            Set(m_privacy, S("settings.privacy"), m_theme.InkMuted);

            if (m_vocabulary.TryGetLabel(PreviewClassId, m_settings.TargetLanguage, m_settings.ReadingLevel,
                    m_settings.ShowPinyin, out Vocabulary.LabelContent preview))
            {
                string text = string.IsNullOrEmpty(preview.Ruby) ? preview.Main : $"{preview.Ruby}\n{preview.Main}";
                Set(m_preview, text, preview.MainIsPinyin ? m_theme.Accent : m_theme.Ink);
            }

            foreach (SpatialButton button in m_buttons)
            {
                string value = button.Value;
                bool selected = false;
                string text = null;
                if (value.StartsWith("lang:"))
                {
                    string lang = value.Substring(5);
                    selected = lang == m_settings.TargetLanguage;
                    text = m_strings.Get($"lang.{lang}.endonym", ui);
                }
                else if (value.StartsWith("ui:"))
                {
                    selected = value.Substring(3) == ui;
                    text = value == "ui:zh" ? m_strings.Get("settings.ui_zh", ui) : m_strings.Get("settings.ui_en", ui);
                }
                else if (value.StartsWith("level:"))
                {
                    selected = value[6] - '0' == m_settings.ReadingLevel;
                    text = value.Substring(6);
                }
                else if (value.StartsWith("size:"))
                {
                    selected = value.Substring(5) == m_settings.LabelSize;
                    text = value.Substring(5);
                }
                else if (value == "pinyin")
                {
                    selected = m_settings.ShowPinyin;
                    text = $"{S("settings.show_pinyin")}: {S(selected ? "common.on" : "common.off")}";
                }
                else if (value == "meaning")
                {
                    selected = m_settings.ShowMeaning;
                    text = $"{S("settings.show_meaning")}: {S(selected ? "common.on" : "common.off")}";
                }
                else if (value == "people")
                {
                    selected = m_settings.LabelPeople;
                    text = $"{S("settings.label_people")}: {S(selected ? "common.on" : "common.off")}";
                }
                else if (value == "audio")
                {
                    selected = m_settings.AudioOn;
                    text = $"{S("settings.audio")}: {S(selected ? "common.on" : "common.off")}";
                }
                else if (value == "close") text = S("common.close");
                else text = value.Substring(value.IndexOf(':') + 1) == "+" ? "+" : "−";

                button.Text.Text = text;
                button.Text.TextColor = m_theme.Ink;
                button.Text.BackgroundColor = selected ? m_theme.AccentSoft : m_theme.Card;
            }
        }

        private string S(string key) => m_strings.Get(key, m_settings.InterfaceLanguage);

        private static void Set(SpatialAdapterNativeText text, string value, Color color)
        {
            if (text == null) return;
            text.Text = value;
            text.TextColor = color;
        }
    }
}
