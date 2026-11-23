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
    /// First-time experience (DESIGN.md section 7): language window, then a short intro card, then a
    /// scanning hint until the first object is confirmed. Later launches skip the title and the
    /// intro but still ask both questions (with last time's answers highlighted), so every session
    /// starts by choosing what to speak and learn.
    /// Each card appears once where the user is looking (about 1.2 m away, slightly below eye
    /// level) and stays there; windows never follow the head.
    /// </summary>
    [AddComponentMenu("Word World/Entry Flow")]
    public sealed class EntryFlow : MonoBehaviour
    {
        [SerializeField] private LearnerSettings m_settings;
        [SerializeField] private UiStrings m_strings;
        [SerializeField] private TrackingManager m_tracking;
        [SerializeField] private WordWorldTheme m_theme;
        [Tooltip("The guided tour, started once onboarding is done. It replaces the scanning hint, " +
                 "the Tap me tooltip and the five-language card, which would crowd what it points at.")]
        [SerializeField] private Tour m_tour;

        [Header("Title (first launch, V2 review: black, fade to the room, then the name and purpose)")]
        [SerializeField] private GameObject m_veil;
        [SerializeField] private SpatialAdapterNativeText m_veilText;
        [SerializeField] private GameObject m_titleCard;
        [SerializeField] private SpatialAdapterNativeText m_titleName;
        [SerializeField] private SpatialAdapterNativeText m_titleNameZh;
        [SerializeField] private SpatialAdapterNativeText m_titleTagline;
        [SerializeField] private Vector3 m_titleOffset = new Vector3(0f, -0.02f, 1.0f);
        [SerializeField] private Vector3 m_veilOffset = new Vector3(0f, 0f, 1.4f);

        [Header("Guided first word (the first object, shown in all five languages)")]
        [SerializeField] private Vocabulary m_vocabulary;
        [SerializeField] private GameObject m_showcase;
        [SerializeField] private SpatialAdapterNativeText m_showcaseHeader;
        [SerializeField] private SpatialAdapterNativeText[] m_showcaseRows = System.Array.Empty<SpatialAdapterNativeText>();
        [SerializeField] private float m_showcaseSeconds = 20f;

        [Header("Onboarding window: 1 your language, 2 what to learn, 3 start (V3 review)")]
        [SerializeField] private GameObject m_languageWindow;
        [SerializeField] private SpatialAdapterNativeText m_languageTitle;
        [Tooltip("Step 1 asks in both languages, one line each, the Chinese line in the accent colour.")]
        [SerializeField] private SpatialAdapterNativeText m_languageTitleZh;
        [Tooltip("One button per language; values are language codes, reused for both questions.")]
        [SerializeField] private SpatialButton[] m_languageButtons = System.Array.Empty<SpatialButton>();
        [Tooltip("The breadcrumb: one chip per step; finished steps can be tapped to go back.")]
        [SerializeField] private SpatialButton[] m_crumbs = System.Array.Empty<SpatialButton>();
        [SerializeField] private SpatialButton m_startButton;
        [SerializeField] private float m_optionsTop = 0.1f;
        [SerializeField] private float m_optionsStep = 0.095f;

        [Header("Scanning hint")]
        [SerializeField] private GameObject m_scanningHint;
        [SerializeField] private SpatialAdapterNativeText m_scanningText;

        [Header("First label (DESIGN.md section 7 step 4)")]
        [SerializeField] private ObjectLabels m_labels;
        [SerializeField] private WordBank m_wordBank;
        [SerializeField] private GameObject m_tapMe;
        [SerializeField] private SpatialAdapterNativeText m_tapMeText;

        [Header("Placement")]
        [SerializeField] private Vector3 m_windowOffset = new Vector3(0f, -0.1f, 1.2f);
        [SerializeField] private Vector3 m_hintOffset = new Vector3(0f, -0.3f, 1.2f);
        [Tooltip("The hint line re-centres only once it is this far outside the user's view direction.")]
        [SerializeField] private float m_hintRecentreDegrees = 35f;

        private const int PersonClassId = 0;

        private enum Step { Waiting, Title, Speak, Learn, Start, Scanning, Done }
        private Step m_step = Step.Waiting;
        private TrackedObject m_tapMeTrack;
        private TrackedObject m_firstTrack;
        private float m_titleStart;
        private bool m_guided;
        private bool m_titleChimed;
        // The title card's rim and shadow (built by the scene builder) fade with the card, or they
        // would hang in the air as an empty frame before the name appears.
        private SpatialAdapterNativeText[] m_titleEdge;
        private Color[] m_titleEdgeColors;
        private float m_showcaseUntil = -1f;
        // The language the learner speaks (step 1): the interface language.
        private string m_speak = "en";

        private void Awake()
        {
            SetActive(m_languageWindow, false);
            SetActive(m_scanningHint, false);
            SetActive(m_tapMe, false);
            SetActive(m_veil, false);
            SetActive(m_titleCard, false);
            SetActive(m_showcase, false);
            foreach (SpatialButton button in m_languageButtons) button.Clicked += OnLanguageClicked;
            foreach (SpatialButton crumb in m_crumbs) crumb.Clicked += OnCrumbClicked;
            if (m_startButton != null) m_startButton.Clicked += OnIntroDone;
        }

        private void OnEnable()
        {
            if (m_tracking != null) m_tracking.ObjectConfirmed += OnObjectConfirmed;
            if (m_labels != null)
            {
                m_labels.FirstLabelShown += OnFirstLabelShown;
                m_labels.LabelTapped += OnLabelTapped;
            }
        }

        private void OnDisable()
        {
            if (m_tracking != null) m_tracking.ObjectConfirmed -= OnObjectConfirmed;
            if (m_labels != null)
            {
                m_labels.FirstLabelShown -= OnFirstLabelShown;
                m_labels.LabelTapped -= OnLabelTapped;
            }
        }

        private void Update()
        {
            FollowFirstLabel();
            if (m_showcaseUntil > 0f && Time.unscaledTime > m_showcaseUntil) HideShowcase();
            switch (m_step)
            {
                // Wait for a head pose so the first card lands in front of the user, not at the origin.
                case Step.Waiting:
                    if (!HeadPose.TryGet(out _)) return;
                    // Returning learners skip the title but answer both questions again (Nov 16
                    // review: launching straight into the saved language was confusing).
                    if (m_settings.HasLanguage && m_settings.OnboardingDone) ShowLanguageWindow();
                    else ShowTitle();
                    break;
                case Step.Title:
                    UpdateTitle();
                    break;
                case Step.Scanning:
                    KeepHintInView();
                    break;
            }
        }

        // ---- Onboarding window ----------------------------------------------------------------
        // V3 review: one question asking what to learn, with English among the answers, read as
        // "what do you speak?". Now two separate questions, then the intro, with a breadcrumb.
        // Learning paths (CLAUDE.md section 4): English speakers learn Mandarin, Spanish, French or
        // German; Chinese speakers learn English.

        private static readonly string[] s_spoken = { "en", "zh" };
        private static readonly string[] s_forEnglish = { "zh", "es", "fr", "de" };
        private static readonly string[] s_forChinese = { "en" };

        private void ShowLanguageWindow()
        {
            if (m_settings.HasLanguage) m_speak = m_settings.InterfaceLanguage;
            PlaceInFront(m_languageWindow, m_windowOffset);
            m_languageWindow.SetActive(true);
            Sfx.Appear(m_languageWindow.transform.position);
            ShowStep(Step.Speak);
        }

        private void ShowStep(Step step)
        {
            m_step = step;
            int current = StepIndex(step);
            // Step 1 is asked before the interface language is known, so it is asked in both.
            bool both = step == Step.Speak;
            string ui = m_speak;

            for (int i = 0; i < m_crumbs.Length; i++)
            {
                string key = i == 0 ? "onboarding.step_speak" : i == 1 ? "onboarding.step_learn" : "onboarding.step_start";
                SpatialButton crumb = m_crumbs[i];
                // Both languages share the step number: "1 You speak 你的语言".
                string zh = m_strings.Get(key, "zh");
                crumb.SetLabel(both ? $"{m_strings.Get(key, "en")} {zh.Substring(zh.IndexOf(' ') + 1)}" : m_strings.Get(key, ui));
                // Current step: mint. Done: a button (tap to go back). Still to come: muted.
                crumb.Text.TextColor = i > current ? m_theme.InkMuted : m_theme.Ink;
                crumb.Background = i == current ? m_theme.AccentSoft : i < current ? m_theme.Button : m_theme.Card;
                // Only finished steps can be tapped; the others must not light up or tick on hover.
                crumb.Interactable = i < current;
            }

            string title = step == Step.Speak ? m_strings.Get("onboarding.speak_title", "en")
                : step == Step.Learn ? m_strings.Get("picker.title", ui)
                : m_strings.Get("intro.body", ui);
            // Step 1: "What language do you speak?" on one line and 你说哪种语言？ under it, in
            // slightly different colours, so either reader finds their question at a glance.
            Vector2 box = both ? HalfTitleBox : TitleBox;
            Vector3 tp = m_languageTitle.transform.localPosition;
            m_languageTitle.transform.localPosition = new Vector3(tp.x, both ? TitleY + 0.025f : TitleY, tp.z);
            m_languageTitle.BackgroundSize = box;
            m_languageTitle.Text = title;
            m_languageTitle.TextColor = m_theme.Ink;
            // The Start step's two-line sentence is wider than the questions; shrink it to the box
            // rather than let it spill toward the breadcrumb.
            m_languageTitle.TextSize = TextFit.ToNative(TextFit.FitPoints(title, TitlePoints, box));
            if (m_languageTitleZh != null)
            {
                m_languageTitleZh.gameObject.SetActive(both);
                if (both)
                {
                    string zh = m_strings.Get("onboarding.speak_title", "zh");
                    m_languageTitleZh.Text = zh;
                    m_languageTitleZh.TextColor = m_theme.Accent;
                    m_languageTitleZh.TextSize = TextFit.ToNative(TextFit.FitPoints(zh, TitlePoints, HalfTitleBox));
                }
            }

            // Step 2 lists every language; the one they speak is greyed out, and so are those this
            // interface can't teach yet (Chinese speakers learn English: meanings and grammar notes
            // exist in English and Chinese only for that path).
            string[] options = step == Step.Speak ? s_spoken
                : step == Step.Learn ? LearnerSettings.TargetLanguages
                : System.Array.Empty<string>();
            string[] teachable = m_speak == "zh" ? s_forChinese : s_forEnglish;
            int shown = 0;
            foreach (SpatialButton button in m_languageButtons)
            {
                bool visible = System.Array.IndexOf(options, button.Value) >= 0;
                button.gameObject.SetActive(visible);
                if (!visible) continue;
                Vector3 p = button.transform.localPosition;
                button.transform.localPosition = new Vector3(p.x, m_optionsTop - shown++ * m_optionsStep, p.z);
                // Step 1 names each language in itself; step 2 adds its name in the learner's language.
                string endonym = m_strings.Get($"lang.{button.Value}.endonym", ui);
                button.SetLabel(step == Step.Speak ? endonym : $"{endonym} · {m_strings.Get($"lang.{button.Value}", ui)}");
                bool usable = step == Step.Speak || System.Array.IndexOf(teachable, button.Value) >= 0;
                button.Interactable = usable;
                button.Text.TextColor = usable ? m_theme.Ink : m_theme.InkMuted;
                bool selected = step == Step.Speak ? button.Value == m_speak && m_settings.HasLanguage
                    : usable && button.Value == m_settings.TargetLanguage;
                button.Background = !usable ? m_theme.Card : selected ? m_theme.AccentSoft : m_theme.Button;
            }

            if (m_startButton != null)
            {
                m_startButton.gameObject.SetActive(step == Step.Start);
                m_startButton.SetLabel(m_strings.Get("intro.got_it", ui));
                m_startButton.Text.TextColor = m_theme.Ink;
                m_startButton.Background = m_theme.Primary;
            }
        }

        // As authored by the scene builder (40 pt scaled 1.5x for windows, in a 40 x 10 cm box at
        // y 0.195); step 1 splits it into two 5 cm lines.
        private const float TitlePoints = 60f;
        private const float TitleY = 0.195f;
        private static readonly Vector2 TitleBox = new Vector2(0.4f, 0.1f);
        private static readonly Vector2 HalfTitleBox = new Vector2(0.4f, 0.05f);

        private static int StepIndex(Step step) => step == Step.Speak ? 0 : step == Step.Learn ? 1 : 2;

        private void OnLanguageClicked(SpatialButton button)
        {
            if (m_step == Step.Speak)
            {
                m_speak = button.Value;
                ShowStep(Step.Learn);
                return;
            }
            if (m_step != Step.Learn) return;
            m_settings.ChooseLanguage(button.Value);
            m_settings.SetInterfaceLanguage(m_speak);
            if (m_settings.OnboardingDone)
            {
                m_languageWindow.SetActive(false);
                m_step = Step.Done;
                RoomFx.Ripple();
                return;
            }
            ShowStep(Step.Start);
        }

        // Breadcrumb: a finished step can be revisited; later ones cannot be skipped to.
        private void OnCrumbClicked(SpatialButton crumb)
        {
            if (m_step != Step.Speak && m_step != Step.Learn && m_step != Step.Start) return;
            int index = System.Array.IndexOf(m_crumbs, crumb);
            if (index < 0 || index >= StepIndex(m_step)) return;
            ShowStep(index == 0 ? Step.Speak : Step.Learn);
        }

        private void OnIntroDone(SpatialButton button)
        {
            if (m_step != Step.Start) return;
            m_languageWindow.SetActive(false);
            m_settings.CompleteOnboarding();
            m_guided = true;
            RoomFx.Ripple();
            // The menu starts the tour now that onboarding is done; it takes the place of the hints.
            if (m_tour != null)
            {
                m_guided = false;
                m_step = Step.Done;
                return;
            }
            if (m_tracking != null && m_tracking.Confirmed.Count > 0)
            {
                m_step = Step.Done;
                // A label is already up: show it in five languages straight away.
                if (m_firstTrack != null) ShowShowcase(m_firstTrack);
                return;
            }
            // A concrete first task (V2 review): name objects most rooms have.
            m_scanningText.SetFitted(m_strings.Get("scanning.try", m_settings.InterfaceLanguage));
            m_scanningText.TextColor = m_theme.InkMuted;
            PlaceInFront(m_scanningHint, m_hintOffset);
            m_scanningHint.SetActive(true);
            m_step = Step.Scanning;
        }

        // The hint disappears when the first label appears.
        private void OnObjectConfirmed(TrackedObject track)
        {
            if (m_step != Step.Scanning) return;
            if (track.ClassId == PersonClassId && !m_settings.LabelPeople) return; // no label, so keep hinting
            m_scanningHint.SetActive(false);
            m_step = Step.Done;
        }

        // A "Tap me" tooltip beside the first label until the learner has tapped any label ever.
        private void OnFirstLabelShown(TrackedObject track, LabelView view)
        {
            m_firstTrack = track;
            if (m_guided) ShowShowcase(track);
            if (m_tapMe == null || m_wordBank == null || m_wordBank.Words.Count > 0) return;
            if (m_tour != null && m_tour.Running) return; // the tour's halo already points at it
            m_tapMeTrack = track;
            m_tapMeText.SetFitted(m_strings.Get("onboarding.tap_me", m_settings.InterfaceLanguage));
            m_tapMeText.TextColor = m_theme.Ink;
            m_tapMeText.BackgroundColor = m_theme.Primary;
            m_tapMe.SetActive(true);
        }

        private void OnLabelTapped(TrackedObject track, LabelView view)
        {
            m_tapMeTrack = null;
            SetActive(m_tapMe, false);
            HideShowcase();
        }

        // ---- Title ------------------------------------------------------------------------------

        // First launch only: black, the room fades in, then the name and what the app is for. About
        // 7 s, so a returning user still reaches labels well inside the 10 s launch budget.
        private const float VeilHold = 0.6f, VeilFade = 1.6f, NameIn = 1.6f, TaglineIn = 2.8f, FadeIn = 0.8f,
            OutAt = 6.4f, OutFade = 0.6f;

        private void ShowTitle()
        {
            if (m_veil == null || m_titleCard == null)
            {
                ShowLanguageWindow();
                return;
            }
            bool known = m_settings.HasLanguage;
            string ui = known ? m_settings.InterfaceLanguage : "en";
            m_titleName.Text = m_strings.Get("title.name", "en");
            m_titleNameZh.Text = m_strings.Get("title.name", "zh");
            // The interface language is unknown on first launch, so the purpose is given in both.
            m_titleTagline.SetFitted(known ? m_strings.Get("title.tagline", ui)
                : $"{m_strings.Get("title.tagline", "en")}\n{m_strings.Get("title.tagline", "zh")}");
            PlaceInFront(m_veil, m_veilOffset);
            PlaceInFront(m_titleCard, m_titleOffset);
            ApplyTitle(1f, 0f, 0f);
            m_veil.SetActive(true);
            m_titleCard.SetActive(true);
            m_titleStart = Time.unscaledTime;
            m_step = Step.Title;
        }

        private void UpdateTitle()
        {
            float t = Time.unscaledTime - m_titleStart;
            float veil = 1f - Ease((t - VeilHold) / VeilFade);
            float fadeOut = 1f - Ease((t - OutAt) / OutFade);
            float name = Ease((t - NameIn) / FadeIn) * fadeOut;
            float tagline = Ease((t - TaglineIn) / FadeIn) * fadeOut;
            ApplyTitle(veil, name, tagline);
            if (!m_titleChimed && t >= NameIn)
            {
                m_titleChimed = true;
                Sfx.Appear(m_titleCard.transform.position);
            }
            if (t < OutAt + OutFade) return;
            m_veil.SetActive(false);
            m_titleCard.SetActive(false);
            ShowLanguageWindow();
        }

        private void ApplyTitle(float veil, float name, float tagline)
        {
            m_veilText.BackgroundColor = new Color(0f, 0f, 0f, veil);
            Color card = m_theme.Card;
            m_titleCard.GetComponent<SpatialAdapterNativeText>().BackgroundColor = WithAlpha(card, name);
            m_titleName.TextColor = WithAlpha(m_theme.Ink, name);
            m_titleName.BackgroundColor = WithAlpha(card, name);
            m_titleNameZh.TextColor = WithAlpha(m_theme.Accent, name);
            m_titleNameZh.BackgroundColor = WithAlpha(card, name);
            m_titleTagline.TextColor = WithAlpha(m_theme.InkMuted, tagline);
            m_titleTagline.BackgroundColor = WithAlpha(card, name);
            if (m_titleEdge == null) FindTitleEdge();
            for (int i = 0; i < m_titleEdge.Length; i++)
                m_titleEdge[i].BackgroundColor = WithAlpha(m_titleEdgeColors[i], name);
        }

        private void FindTitleEdge()
        {
            string[] names = { "Shadow", "Rim", "Rim Lower", "Top Highlight" };
            Color[] colors = { m_theme.PanelShadow, m_theme.PanelRimTop, m_theme.PanelRimBottom, m_theme.PanelHighlight };
            var texts = new System.Collections.Generic.List<SpatialAdapterNativeText>();
            var found = new System.Collections.Generic.List<Color>();
            for (int i = 0; i < names.Length; i++)
            {
                Transform child = m_titleCard.transform.Find(names[i]);
                if (child == null || !child.TryGetComponent(out SpatialAdapterNativeText text)) continue;
                texts.Add(text);
                found.Add(colors[i]);
            }
            m_titleEdge = texts.ToArray();
            m_titleEdgeColors = found.ToArray();
        }

        private static float Ease(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        private static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, c.a * a);

        // ---- Guided first word ------------------------------------------------------------------

        // The first object the learner finds, named in all five languages, with their target language
        // highlighted. Beside the object and within reach; it closes when they tap the label.
        private void ShowShowcase(TrackedObject track)
        {
            m_guided = false;
            if (m_showcase == null || m_vocabulary == null || !m_vocabulary.TryGet(track.ClassId, out Vocabulary.Entry entry)) return;
            string ui = m_settings.InterfaceLanguage;
            m_showcaseHeader.SetFitted(m_strings.Get("showcase.title", ui));
            m_showcaseHeader.TextColor = m_theme.Ink;
            for (int i = 0; i < m_showcaseRows.Length && i < LearnerSettings.TargetLanguages.Length; i++)
            {
                string code = LearnerSettings.TargetLanguages[i];
                bool target = code == m_settings.TargetLanguage;
                SpatialAdapterNativeText row = m_showcaseRows[i];
                row.SetFitted($"{m_strings.Get($"lang.{code}.endonym", ui)}   {WordFor(entry, code)}");
                row.TextColor = target ? m_theme.Accent : m_theme.Ink;
                row.BackgroundColor = target ? m_theme.AccentSoft : m_theme.Card;
            }
            if (!WindowPlacement.PlaceNear(m_showcase.transform, track.Position, 0.85f, -0.02f, -0.25f)) return;
            // To the side, so the object and its label stay in sight.
            m_showcase.transform.position += m_showcase.transform.right * 0.3f;
            m_showcase.SetActive(true);
            Sfx.Appear(m_showcase.transform.position);
            m_showcaseUntil = Time.unscaledTime + m_showcaseSeconds;
        }

        private void HideShowcase()
        {
            m_showcaseUntil = -1f;
            SetActive(m_showcase, false);
        }

        private static string WordFor(Vocabulary.Entry entry, string code)
        {
            if (code == "zh") return entry.zh == null ? "" : $"{entry.zh.characters}  {entry.zh.pinyin}";
            Vocabulary.LatinEntry latin = code == "es" ? entry.es : code == "fr" ? entry.fr : code == "de" ? entry.de : entry.en;
            return latin == null ? "" : latin.word;
        }

        private void FollowFirstLabel()
        {
            if (m_tapMeTrack == null) return;
            if (!m_labels.TryGetLabel(m_tapMeTrack, out LabelView view) || !view.gameObject.activeInHierarchy ||
                !HeadPose.TryGet(out Pose head))
            {
                SetActive(m_tapMe, false);
                if (view == null) m_tapMeTrack = null;
                return;
            }
            Transform label = view.transform;
            // Just above the card (and its pinyin line), facing the user like the label.
            Vector3 position = label.position + Vector3.up * (view.HeightMetres + 0.03f);
            m_tapMe.transform.SetPositionAndRotation(position, label.rotation);
            if (!m_tapMe.activeSelf) m_tapMe.SetActive(true);
        }

        // The hint is a status line, not a window: it re-centres when the user has looked well away
        // from it, in one step rather than trailing the head.
        private void KeepHintInView()
        {
            if (!HeadPose.TryGet(out Pose head)) return;
            Vector3 toHint = m_scanningHint.transform.position - head.position;
            Vector3 forward = head.rotation * Vector3.forward;
            toHint.y = 0f;
            forward.y = 0f;
            if (Vector3.Angle(toHint, forward) > m_hintRecentreDegrees) PlaceInFront(m_scanningHint, m_hintOffset);
        }

        private static void PlaceInFront(GameObject target, Vector3 offset)
        {
            if (target == null || !HeadPose.TryGet(out Pose head)) return;
            Vector3 forward = head.rotation * Vector3.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-4f) forward = Vector3.forward;
            Quaternion yaw = Quaternion.LookRotation(forward.normalized, Vector3.up);
            target.transform.SetPositionAndRotation(head.position + yaw * offset, yaw);
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null) target.SetActive(active);
        }
    }
}
