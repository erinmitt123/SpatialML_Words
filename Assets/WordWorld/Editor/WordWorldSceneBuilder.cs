using System.Collections.Generic;
using System.Linq;
using SpatialMLPipelineZooAsset = ByteDance.PICO.SecureMR.SpatialMLPipelineZooAsset;
using ByteDance.PICO.SpatialAdapter.Component;
using ByteDance.PICO.XR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using WordWorld.App;
using WordWorld.Data;
using WordWorld.DebugTools;
using WordWorld.Detection;
using WordWorld.Labels;
using WordWorld.Spatial;
using WordWorld.Tracking;
using WordWorld.UI;

namespace WordWorld.Editor
{
    /// <summary>
    /// Generates Assets/Scenes/Spatial/WordWorld.unity from the Full Space face scene, which already
    /// has the Spatial camera setup that works on Swan. Spatial Adapter only renders objects that are
    /// authored into the scene, so the label pool and panels are created here rather than at runtime.
    /// Headless: -executeMethod WordWorld.Editor.WordWorldSceneBuilder.BuildScene
    /// </summary>
    public static class WordWorldSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Spatial/WordWorld.unity";
        private const string TemplateScenePath = "Assets/Scenes/Spatial/FaceDetectionFullSpaceSpatial.unity";
        private const string TemplateSampleObject = "Full Space Face Tracking";
        private const string PackagePath = "Assets/SpatialMLPipelineZoo/yolo-detection/yolo-detection.asset";
        private const int LabelPoolSize = 24;
        private const int ScanDotCount = 8;
        // One knob for authored text size (DESIGN.md: panels read at about 1.2 m). Labels size
        // themselves at runtime and are not affected.
        private const float WindowTextScale = 1.5f;
        private const string VocabularyPath = "Assets/Data/vocabulary.json";
        private const string UiStringsPath = "Assets/Data/ui_strings.json";
        private const string ThemePath = "Assets/Data/WordWorldTheme.asset";

        [MenuItem("Word World/Rebuild Word World Scene")]
        public static void BuildScene()
        {
            EnsureProjectSettings();

            var package = AssetDatabase.LoadAssetAtPath<SpatialMLPipelineZooAsset>(PackagePath);
            if (package == null) throw new System.InvalidOperationException($"Missing {PackagePath}; run tools/spatialml import first.");

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null) AssetDatabase.DeleteAsset(ScenePath);
            if (!AssetDatabase.CopyAsset(TemplateScenePath, ScenePath))
                throw new System.InvalidOperationException($"Could not copy {TemplateScenePath}.");

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == TemplateSampleObject) Object.DestroyImmediate(root);

            WordWorldTheme theme = LoadOrCreateTheme();
            var app = new GameObject("Word World");

            // Spatial Adapter forwards poke and pinch hits as uGUI pointer events, but only when an
            // EventSystem exists.
            var events = new GameObject("Event System", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.transform.SetParent(app.transform, false);

            var learner = new GameObject("Learner");
            learner.transform.SetParent(app.transform, false);
            var settings = learner.AddComponent<LearnerSettings>();
            var wordBank = learner.AddComponent<WordBank>();
            var vocabulary = learner.AddComponent<Vocabulary>();
            Set(vocabulary, "m_table", LoadText(VocabularyPath));
            var strings = learner.AddComponent<UiStrings>();
            Set(strings, "m_table", LoadText(UiStringsPath));

            var roomMesh = new GameObject("Room Mesh").AddComponent<RoomMesh>();
            roomMesh.transform.SetParent(app.transform, false);

            var detector = new GameObject("Object Detector").AddComponent<YoloxSpatialMLDetector>();
            detector.transform.SetParent(app.transform, false);

            var tracking = new GameObject("Tracking").AddComponent<TrackingManager>();
            tracking.transform.SetParent(app.transform, false);
            Set(tracking, "m_detector", detector);

            var labels = new GameObject("Labels").AddComponent<ObjectLabels>();
            labels.transform.SetParent(app.transform, false);
            var pool = new List<LabelView>(LabelPoolSize);
            for (int i = 0; i < LabelPoolSize; i++) pool.Add(CreateLabel(labels.transform, i, theme));
            var scanDots = new List<Object>(ScanDotCount);
            for (int i = 0; i < ScanDotCount; i++)
            {
                var dot = new GameObject($"Scan Dot {i}");
                dot.transform.SetParent(labels.transform, false);
                scanDots.Add(CreateNativeText(dot, "", 10, TextJustification.Center, theme.Accent,
                    new Color(theme.Accent.r, theme.Accent.g, theme.Accent.b, 0.7f), new Vector2(0.008f, 0.008f), 0.004f));
                AddSyncCollider(dot, new Vector3(0.008f, 0.008f, 0.002f));
                dot.SetActive(false);
            }
            Set(labels, "m_tracking", tracking);
            Set(labels, "m_settings", settings);
            Set(labels, "m_vocabulary", vocabulary);
            SetArray(labels, "m_pool", pool.Cast<Object>().ToArray());
            SetArray(labels, "m_scanDots", scanDots.ToArray());

            var detail = new GameObject("Detail Card").AddComponent<DetailCard>();
            detail.transform.SetParent(app.transform, false);
            CreateDetailCard(detail, theme);
            Set(detail, "m_settings", settings);
            Set(detail, "m_strings", strings);
            Set(detail, "m_vocabulary", vocabulary);
            Set(detail, "m_wordBank", wordBank);
            Set(detail, "m_labels", labels);
            Set(detail, "m_tracking", tracking);
            Set(detail, "m_theme", theme);

            var settingsWindow = new GameObject("Settings").AddComponent<SettingsWindow>();
            settingsWindow.transform.SetParent(app.transform, false);
            CreateSettingsWindow(settingsWindow, theme);
            Set(settingsWindow, "m_settings", settings);
            Set(settingsWindow, "m_strings", strings);
            Set(settingsWindow, "m_vocabulary", vocabulary);
            Set(settingsWindow, "m_tracking", tracking);
            Set(settingsWindow, "m_theme", theme);

            var wordBankWindow = new GameObject("Word Bank").AddComponent<WordBankWindow>();
            wordBankWindow.transform.SetParent(app.transform, false);
            CreateWordBankWindow(wordBankWindow, theme);
            Set(wordBankWindow, "m_settings", settings);
            Set(wordBankWindow, "m_strings", strings);
            Set(wordBankWindow, "m_vocabulary", vocabulary);
            Set(wordBankWindow, "m_wordBank", wordBank);
            Set(wordBankWindow, "m_labels", labels);
            Set(wordBankWindow, "m_theme", theme);

            var progress = new GameObject("Progress").AddComponent<ProgressWindow>();
            progress.transform.SetParent(app.transform, false);
            CreateProgressWindow(progress, theme);
            Set(progress, "m_settings", settings);
            Set(progress, "m_strings", strings);
            Set(progress, "m_vocabulary", vocabulary);
            Set(progress, "m_wordBank", wordBank);
            Set(progress, "m_theme", theme);

            var quiz = new GameObject("Quiz").AddComponent<FindItQuiz>();
            quiz.transform.SetParent(app.transform, false);
            CreateQuiz(quiz, theme);
            Set(quiz, "m_settings", settings);
            Set(quiz, "m_strings", strings);
            Set(quiz, "m_vocabulary", vocabulary);
            Set(quiz, "m_wordBank", wordBank);
            Set(quiz, "m_labels", labels);
            Set(quiz, "m_detailCard", detail);
            Set(quiz, "m_theme", theme);

            var row = new GameObject("Window Row").AddComponent<WindowRow>();
            row.transform.SetParent(app.transform, false);
            Set(row, "m_settings", settings);
            Set(row, "m_wordBank", wordBank);
            Set(row, "m_labels", labels);
            Set(row, "m_wordBankWindow", wordBankWindow);
            Set(row, "m_quiz", quiz);
            Set(row, "m_progress", progress);

            var status = new GameObject("Status").AddComponent<StatusChip>();
            status.transform.SetParent(app.transform, false);
            CreateStatusChip(status, theme);
            Set(status, "m_settings", settings);
            Set(status, "m_strings", strings);
            Set(status, "m_tracking", tracking);
            Set(status, "m_settingsWindow", settingsWindow);
            Set(status, "m_windowRow", row);
            Set(status, "m_theme", theme);

            var panel = new GameObject("Debug").AddComponent<DetectionDebugPanel>();
            panel.transform.SetParent(app.transform, false);
            CreatePanel(panel.transform, out GameObject panelRoot, out SpatialAdapterNativeText panelText);
            Set(panel, "m_panelRoot", panelRoot);
            Set(panel, "m_text", panelText);
            Set(panel, "m_detector", detector);
            Set(panel, "m_tracking", tracking);
            Set(panel, "m_roomMesh", roomMesh);

            var entry = new GameObject("Entry Flow").AddComponent<EntryFlow>();
            entry.transform.SetParent(app.transform, false);
            CreateEntryFlow(entry, theme, settings, strings, tracking);
            Set(entry, "m_labels", labels);
            Set(entry, "m_wordBank", wordBank);

            EditorSceneManager.MarkSceneDirty(scene);
            // Assigned last: the reference was found cleared when set before the label pool was built.
            Set(detector, "m_package", AssetDatabase.LoadAssetAtPath<SpatialMLPipelineZooAsset>(PackagePath));
            EditorSceneManager.SaveScene(scene);
            AddToBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log($"[WordWorld] built {ScenePath}");
        }

        // Text is drawn natively by the Spatial Engine (see LabelView): TextMeshPro rendered as blocks.
        // DESIGN.md section 3: card, pinyin ruby line, leader line and dot. The leader and dot are
        // text backgrounds without text. Every part moves at runtime, so each has a sync collider; taps
        // on any part bubble up to the LabelView.
        private static LabelView CreateLabel(Transform parent, int index, WordWorldTheme theme)
        {
            var go = new GameObject($"Label {index:00}");
            go.transform.SetParent(parent, false);
            // The word and meaning each draw a solid card background; the Card child behind them
            // rounds the corners of the whole label.
            SpatialAdapterNativeText text = CreateNativeText(go, "label", 90, TextJustification.Center,
                theme.Ink, theme.Card, new Vector2(0.3f, 0.07f), 0f);
            AddSyncCollider(go, new Vector3(0.3f, 0.07f, 0.01f));
            SpatialAdapterNativeText backing = CreateChildText(go.transform, "Card", new Vector3(0f, 0f, 0.001f), "", 10,
                theme.Ink, theme.Card, new Vector2(0.3f, 0.09f), theme.LabelCornerRadius);
            AddSyncCollider(backing.gameObject, new Vector3(0.001f, 0.001f, 0.001f));
            SpatialAdapterNativeText gloss = CreateChildText(go.transform, "Meaning", new Vector3(0f, -0.04f, -0.001f), "", 54,
                theme.InkMuted, theme.Card, new Vector2(0.2f, 0.025f), 0f);
            AddSyncCollider(gloss.gameObject, new Vector3(0.001f, 0.001f, 0.001f));
            gloss.gameObject.SetActive(false);

            SpatialAdapterNativeText ruby = CreateChildText(go.transform, "Ruby", new Vector3(0f, 0.05f, 0f), "", 50,
                theme.Accent, Color.clear, new Vector2(0.2f, 0.03f), 0f);
            AddSyncCollider(ruby.gameObject, new Vector3(0.01f, 0.01f, 0.002f));
            ruby.gameObject.SetActive(false);
            SpatialAdapterNativeText leader = CreateChildText(go.transform, "Leader", new Vector3(0f, -0.1f, 0.002f), "", 10,
                theme.InkMuted, theme.InkMuted, new Vector2(0.001f, 0.1f), 0f);
            AddSyncCollider(leader.gameObject, new Vector3(0.001f, 0.001f, 0.001f));
            SpatialAdapterNativeText dot = CreateChildText(go.transform, "Dot", new Vector3(0f, -0.15f, 0.001f), "", 10,
                theme.Accent, theme.Accent, new Vector2(0.006f, 0.006f), 0.003f);
            AddSyncCollider(dot.gameObject, new Vector3(0.006f, 0.006f, 0.002f));

            // Quiz: a unit trigger box scaled to the real object so touching it answers.
            var bounds = new GameObject("Object Bounds");
            bounds.transform.SetParent(go.transform, false);
            bounds.transform.localScale = new Vector3(0.001f, 0.001f, 0.001f);
            bounds.AddComponent<BoxCollider>().isTrigger = true;

            var view = go.AddComponent<LabelView>();
            view.Bind(text, ruby, gloss, backing, leader, dot, go.GetComponent<BoxCollider>(), bounds.transform, theme);
            go.AddComponent<SpatialAdapterHoverEffect>();
            go.SetActive(false);
            return view;
        }

        // DESIGN.md section 4: about 25 cm wide; word, pinyin and meaning, grammar and example rows,
        // then Save and Not right?, with a close button in the corner.
        private static void CreateDetailCard(DetailCard detail, WordWorldTheme theme)
        {
            GameObject card = CreateCard(detail.transform, "Card", theme, new Vector2(0.36f, 0.24f));
            SpatialAdapterNativeText header = CreateChildText(card.transform, "Word", new Vector3(-0.02f, 0.08f, -0.005f),
                "word", 44, theme.Ink, Color.clear, new Vector2(0.3f, 0.05f), 0f, TextJustification.Left);
            SpatialAdapterNativeText sub = CreateChildText(card.transform, "Meaning", new Vector3(0f, 0.04f, -0.005f),
                "meaning", 26, theme.InkMuted, Color.clear, new Vector2(0.34f, 0.03f), 0f, TextJustification.Left);
            SpatialAdapterNativeText body = CreateChildText(card.transform, "Body", new Vector3(0f, -0.025f, -0.005f),
                "body", 22, theme.Ink, Color.clear, new Vector2(0.34f, 0.08f), 0f, TextJustification.Left);
            SpatialButton close = CreateButton(card.transform, "Close", new Vector3(0.15f, 0.09f, -0.01f),
                "close", "×", 40, theme, new Vector2(0.04f, 0.04f));
            SpatialButton save = CreateButton(card.transform, "Save", new Vector3(-0.07f, -0.093f, -0.01f),
                "save", "detail.save", 30, theme, new Vector2(0.12f, 0.04f));
            SpatialButton notRight = CreateButton(card.transform, "Not Right", new Vector3(0.07f, -0.093f, -0.01f),
                "not_right", "detail.not_right", 30, theme, new Vector2(0.12f, 0.04f));

            Set(detail, "m_card", card);
            Set(detail, "m_header", header);
            Set(detail, "m_subheader", sub);
            Set(detail, "m_body", body);
            Set(detail, "m_saveButton", save);
            Set(detail, "m_notRightButton", notRight);
            Set(detail, "m_closeButton", close);
        }

        // DESIGN.md section 5.4: 40 x 50 cm (a little taller here to keep 1 cm between 4 cm buttons).
        private static void CreateSettingsWindow(SettingsWindow window, WordWorldTheme theme)
        {
            GameObject card = CreateCard(window.transform, "Settings Window", theme, new Vector2(0.42f, 0.56f));
            Transform t = card.transform;
            var rowSize = new Vector2(0.38f, 0.03f);
            var buttons = new List<Object>();
            const float z = -0.01f;
            SpatialButton Button(string value, float x, float y, float width, int points = 28) =>
                CreateButton(t, $"Button {value}", new Vector3(x, y, z), value, value, points, theme, new Vector2(width, 0.04f));
            SpatialAdapterNativeText Row(string name, float y, int points = 26) =>
                CreateChildText(t, name, new Vector3(0f, y, -0.005f), name, points, theme.InkMuted, Color.clear, rowSize, 0f,
                    TextJustification.Left);

            SpatialAdapterNativeText title = CreateChildText(t, "Title", new Vector3(-0.03f, 0.245f, -0.005f), "settings.title",
                44, theme.Ink, Color.clear, new Vector2(0.32f, 0.05f), 0f, TextJustification.Left);
            buttons.Add(Button("close", 0.175f, 0.245f, 0.04f, 40));

            SpatialAdapterNativeText learning = Row("I'm learning", 0.19f);
            for (int i = 0; i < LearnerSettings.TargetLanguages.Length; i++)
                buttons.Add(Button($"lang:{LearnerSettings.TargetLanguages[i]}", -0.16f + i * 0.08f, 0.15f, 0.07f, 24));

            SpatialAdapterNativeText ui = Row("Interface language", 0.1f);
            buttons.Add(Button("ui:en", -0.125f, 0.06f, 0.12f));
            buttons.Add(Button("ui:zh", 0.005f, 0.06f, 0.12f));

            SpatialAdapterNativeText level = Row("Reading level", 0.01f);
            for (int i = 1; i <= 3; i++) buttons.Add(Button($"level:{i}", -0.165f + (i - 1) * 0.055f, -0.03f, 0.045f));
            SpatialAdapterNativeText preview = CreateChildText(t, "Preview", new Vector3(0.09f, -0.03f, -0.005f), "preview",
                32, theme.Ink, theme.AccentSoft, new Vector2(0.18f, 0.06f), theme.LabelCornerRadius);

            buttons.Add(Button("meaning", -0.13f, -0.09f, 0.125f, 18));
            buttons.Add(Button("pinyin", 0f, -0.09f, 0.125f, 18));
            buttons.Add(Button("people", 0.13f, -0.09f, 0.125f, 18));

            SpatialAdapterNativeText size = Row("Label size", -0.14f);
            for (int i = 0; i < 3; i++) buttons.Add(Button($"size:{"SML"[i]}", 0.035f + i * 0.055f, -0.14f, 0.045f));

            SpatialAdapterNativeText confidence = Row("Detection confidence", -0.19f);
            buttons.Add(Button("conf:-", 0.09f, -0.19f, 0.045f, 36));
            buttons.Add(Button("conf:+", 0.145f, -0.19f, 0.045f, 36));

            SpatialAdapterNativeText privacy = Row("Privacy", -0.245f, 20);

            SetArray(window, "m_buttons", buttons.ToArray());
            Set(window, "m_window", card);
            Set(window, "m_title", title);
            Set(window, "m_learningRow", learning);
            Set(window, "m_interfaceRow", ui);
            Set(window, "m_levelRow", level);
            Set(window, "m_preview", preview);
            Set(window, "m_sizeRow", size);
            Set(window, "m_confidenceRow", confidence);
            Set(window, "m_privacy", privacy);
        }

        // DESIGN.md section 5.1: 45 x 55 cm. Header, filter chips, six word rows and a pager.
        private static void CreateWordBankWindow(WordBankWindow window, WordWorldTheme theme)
        {
            GameObject card = CreateCard(window.transform, "Word Bank Window", theme, new Vector2(0.45f, 0.55f));
            Transform t = card.transform;
            SpatialAdapterNativeText header = CreateChildText(t, "Header", new Vector3(0f, 0.235f, -0.005f), "wordbank.title",
                40, theme.Ink, Color.clear, new Vector2(0.41f, 0.05f), 0f, TextJustification.Left);

            string[] filters = { "All", "ThisRoom", "New", "Learning", "Mastered" };
            var filterButtons = new List<Object>();
            for (int i = 0; i < filters.Length; i++)
                filterButtons.Add(CreateButton(t, $"Filter {filters[i]}", new Vector3(-0.172f + i * 0.086f, 0.18f, -0.01f),
                    filters[i], filters[i], 22, theme, new Vector2(0.078f, 0.04f)));

            var rows = new List<Object>();
            for (int i = 0; i < 6; i++)
            {
                SpatialButton rowButton = CreateButton(t, $"Row {i}", new Vector3(0f, 0.115f - i * 0.058f, -0.01f),
                    $"row{i}", "row", 28, theme, new Vector2(0.41f, 0.048f));
                Set(rowButton.Text, "_justification", TextJustification.Left);
                rows.Add(rowButton);
            }
            SpatialAdapterNativeText empty = CreateChildText(t, "Empty", new Vector3(0f, 0.02f, -0.006f), "wordbank.empty",
                28, theme.InkMuted, Color.clear, new Vector2(0.41f, 0.08f), 0f);
            AddSyncCollider(empty.gameObject, new Vector3(0.001f, 0.001f, 0.001f));
            SpatialAdapterNativeText message = CreateChildText(t, "Message", new Vector3(0f, -0.2f, -0.012f), "wordbank.not_here",
                28, theme.InkMuted, theme.AccentSoft, new Vector2(0.3f, 0.04f), theme.LabelCornerRadius);
            AddSyncCollider(message.gameObject, new Vector3(0.001f, 0.001f, 0.001f));
            message.gameObject.SetActive(false);
            SpatialButton previous = CreateButton(t, "Previous", new Vector3(-0.17f, -0.245f, -0.01f), "prev", "<", 32, theme,
                new Vector2(0.05f, 0.04f));
            SpatialButton next = CreateButton(t, "Next", new Vector3(0.17f, -0.245f, -0.01f), "next", ">", 32, theme,
                new Vector2(0.05f, 0.04f));
            SpatialAdapterNativeText page = CreateChildText(t, "Page", new Vector3(0f, -0.245f, -0.005f), "1 / 1", 24,
                theme.InkMuted, Color.clear, new Vector2(0.12f, 0.03f), 0f);

            Set(window, "m_window", card);
            Set(window, "m_header", header);
            Set(window, "m_empty", empty);
            Set(window, "m_message", message);
            Set(window, "m_page", page);
            SetArray(window, "m_filters", filterButtons.ToArray());
            SetArray(window, "m_rows", rows.ToArray());
            Set(window, "m_previous", previous);
            Set(window, "m_next", next);
        }

        // DESIGN.md section 5.3: 35 x 40 cm. Big number, stats, category bars, level-up card.
        private static void CreateProgressWindow(ProgressWindow window, WordWorldTheme theme)
        {
            GameObject card = CreateCard(window.transform, "Progress Window", theme, new Vector2(0.35f, 0.4f));
            Transform t = card.transform;
            SpatialAdapterNativeText title = CreateChildText(t, "Title", new Vector3(0f, 0.17f, -0.005f), "progress.title",
                40, theme.Ink, Color.clear, new Vector2(0.31f, 0.045f), 0f, TextJustification.Left);
            SpatialAdapterNativeText big = CreateChildText(t, "Big Number", new Vector3(-0.1f, 0.11f, -0.005f), "0",
                96, theme.Accent, Color.clear, new Vector2(0.11f, 0.06f), 0f);
            SpatialAdapterNativeText caption = CreateChildText(t, "Caption", new Vector3(0.06f, 0.11f, -0.005f),
                "progress.words_learned", 28, theme.InkMuted, Color.clear, new Vector2(0.2f, 0.04f), 0f, TextJustification.Left);
            SpatialAdapterNativeText stats = CreateChildText(t, "Stats", new Vector3(0f, 0.05f, -0.005f), "stats",
                24, theme.Ink, Color.clear, new Vector2(0.31f, 0.05f), 0f, TextJustification.Left);

            const int bars = 6;
            const float barLeft = -0.02f, barWidth = 0.13f;
            var names = new List<Object>();
            var fills = new List<Object>();
            var counts = new List<Object>();
            for (int i = 0; i < bars; i++)
            {
                float y = 0.0f - i * 0.03f;
                // Shown and hidden at runtime, so each needs a sync collider.
                SpatialAdapterNativeText name = CreateChildText(t, $"Bar {i} Name", new Vector3(-0.095f, y, -0.005f), "category",
                    22, theme.Ink, Color.clear, new Vector2(0.14f, 0.025f), 0f, TextJustification.Left);
                AddSyncCollider(name.gameObject, new Vector3(0.001f, 0.001f, 0.001f));
                names.Add(name);
                CreateChildText(t, $"Bar {i} Track", new Vector3(barLeft + barWidth * 0.5f, y, -0.004f), "", 10, theme.Ink,
                    theme.AccentSoft, new Vector2(barWidth, 0.012f), 0.006f);
                SpatialAdapterNativeText fill = CreateChildText(t, $"Bar {i} Fill", new Vector3(barLeft, y, -0.006f), "", 10,
                    theme.Accent, theme.Accent, new Vector2(0.001f, 0.012f), 0.006f);
                AddSyncCollider(fill.gameObject, new Vector3(0.001f, 0.001f, 0.001f));
                fills.Add(fill);
                SpatialAdapterNativeText count = CreateChildText(t, $"Bar {i} Count", new Vector3(0.145f, y, -0.005f), "0/0", 20,
                    theme.InkMuted, Color.clear, new Vector2(0.05f, 0.025f), 0f);
                AddSyncCollider(count.gameObject, new Vector3(0.001f, 0.001f, 0.001f));
                counts.Add(count);
            }

            // Level-up card over the bottom of the window.
            var levelUp = new GameObject("Level Up");
            levelUp.transform.SetParent(t, false);
            levelUp.transform.localPosition = new Vector3(0f, -0.15f, -0.012f);
            CreateNativeText(levelUp, "", 10, TextJustification.Center, theme.Ink, theme.AccentSoft, new Vector2(0.33f, 0.085f),
                theme.LabelCornerRadius);
            AddSyncCollider(levelUp, new Vector3(0.33f, 0.085f, 0.002f));
            SpatialAdapterNativeText levelText = CreateChildText(levelUp.transform, "Text", new Vector3(0f, 0.02f, -0.002f),
                "progress.level_up", 28, theme.Ink, Color.clear, new Vector2(0.3f, 0.035f), 0f);
            SpatialButton tryIt = CreateButton(levelUp.transform, "Try It", new Vector3(-0.07f, -0.02f, -0.004f), "try",
                "progress.try_it", 26, theme, new Vector2(0.12f, 0.035f));
            SpatialButton notYet = CreateButton(levelUp.transform, "Not Yet", new Vector3(0.07f, -0.02f, -0.004f), "not_yet",
                "progress.not_yet", 26, theme, new Vector2(0.12f, 0.035f));

            Set(window, "m_window", card);
            Set(window, "m_title", title);
            Set(window, "m_bigNumber", big);
            Set(window, "m_caption", caption);
            Set(window, "m_stats", stats);
            SetArray(window, "m_barNames", names.ToArray());
            SetArray(window, "m_barFills", fills.ToArray());
            SetArray(window, "m_barCounts", counts.ToArray());
            SetFloat(window, "m_barLeft", barLeft);
            SetFloat(window, "m_barWidth", barWidth);
            Set(window, "m_levelUp", levelUp);
            Set(window, "m_levelUpText", levelText);
            Set(window, "m_tryIt", tryIt);
            Set(window, "m_notYet", notYet);
        }

        // DESIGN.md sections 5.2 and 6: the 40 x 30 cm Quiz window, plus the floating prompt, the
        // correct ring and the hint glow that appear in the room.
        private static void CreateQuiz(FindItQuiz quiz, WordWorldTheme theme)
        {
            GameObject card = CreateCard(quiz.transform, "Quiz Window", theme, new Vector2(0.4f, 0.3f));
            Transform t = card.transform;
            SpatialAdapterNativeText title = CreateChildText(t, "Title", new Vector3(0f, 0.115f, -0.005f), "quiz.title",
                44, theme.Ink, Color.clear, new Vector2(0.36f, 0.05f), 0f, TextJustification.Left);
            SpatialAdapterNativeText prompt = CreateChildText(t, "Prompt", new Vector3(0f, 0.05f, -0.005f), "prompt",
                56, theme.Ink, Color.clear, new Vector2(0.36f, 0.07f), 0f);
            AddSyncCollider(prompt.gameObject, new Vector3(0.001f, 0.001f, 0.001f));
            SpatialAdapterNativeText track = CreateChildText(t, "Timer Track", new Vector3(0f, 0.005f, -0.004f), "", 10,
                theme.Ink, theme.AccentSoft, new Vector2(0.34f, 0.008f), 0.004f);
            AddSyncCollider(track.gameObject, new Vector3(0.001f, 0.001f, 0.001f));
            SpatialAdapterNativeText fill = CreateChildText(t, "Timer Fill", new Vector3(0f, 0.005f, -0.006f), "", 10,
                theme.Accent, theme.Accent, new Vector2(0.34f, 0.008f), 0.004f);
            AddSyncCollider(fill.gameObject, new Vector3(0.001f, 0.001f, 0.001f));
            SpatialAdapterNativeText body = CreateChildText(t, "Body", new Vector3(0f, -0.04f, -0.005f), "body",
                28, theme.Ink, Color.clear, new Vector2(0.36f, 0.07f), 0f, TextJustification.Left);
            SpatialButton primary = CreateButton(t, "Primary", new Vector3(-0.08f, -0.11f, -0.01f), "primary", "quiz.start",
                32, theme, new Vector2(0.15f, 0.045f));
            SpatialButton secondary = CreateButton(t, "Secondary", new Vector3(0.09f, -0.11f, -0.01f), "secondary", "quiz.done",
                28, theme, new Vector2(0.13f, 0.045f));

            var floating = new GameObject("Floating Prompt");
            floating.transform.SetParent(quiz.transform, false);
            floating.transform.localPosition = new Vector3(0f, 1.3f, 0.6f);
            SpatialAdapterNativeText floatingText = CreateNativeText(floating, "prompt", 48, TextJustification.Center,
                theme.Ink, theme.Card, new Vector2(0.36f, 0.075f), theme.LabelCornerRadius);
            AddSyncCollider(floating, new Vector3(0.36f, 0.075f, 0.005f));
            SpatialButton skip = CreateButton(floating.transform, "Skip", new Vector3(0f, -0.065f, -0.005f), "skip",
                "quiz.skip", 28, theme, new Vector2(0.1f, 0.04f));

            var ring = new GameObject("Correct Ring");
            ring.transform.SetParent(quiz.transform, false);
            SpatialAdapterNativeText ringText = CreateNativeText(ring, "", 10, TextJustification.Center, theme.Correct,
                theme.Correct, new Vector2(0.12f, 0.12f), 0.06f);
            AddSyncCollider(ring, new Vector3(0.001f, 0.001f, 0.001f));
            var glow = new GameObject("Hint Glow");
            glow.transform.SetParent(quiz.transform, false);
            SpatialAdapterNativeText glowText = CreateNativeText(glow, "", 10, TextJustification.Center, theme.Hint,
                theme.Hint, new Vector2(0.16f, 0.16f), 0.08f);
            AddSyncCollider(glow, new Vector3(0.001f, 0.001f, 0.001f));

            Set(quiz, "m_window", card);
            Set(quiz, "m_title", title);
            Set(quiz, "m_body", body);
            Set(quiz, "m_prompt", prompt);
            Set(quiz, "m_timerTrack", track);
            Set(quiz, "m_timerFill", fill);
            Set(quiz, "m_primary", primary);
            Set(quiz, "m_secondary", secondary);
            Set(quiz, "m_floatingPrompt", floating);
            Set(quiz, "m_floatingText", floatingText);
            Set(quiz, "m_skip", skip);
            Set(quiz, "m_ring", ringText);
            Set(quiz, "m_glow", glowText);
        }

        // DESIGN.md section 8: recognition chip with Windows, Settings and Exit buttons under it.
        private static void CreateStatusChip(StatusChip status, WordWorldTheme theme)
        {
            var chip = new GameObject("Chip");
            chip.transform.SetParent(status.transform, false);
            chip.transform.localPosition = new Vector3(0f, 0.9f, 1f);
            SpatialButton recognising = CreateButton(chip.transform, "Recognition", Vector3.zero, "pause",
                "status.recognizing", 30, theme, new Vector2(0.2f, 0.04f));
            SpatialButton windows = CreateButton(chip.transform, "Windows Button", new Vector3(-0.13f, -0.05f, 0f), "windows",
                "status.windows", 28, theme, new Vector2(0.12f, 0.04f));
            SpatialButton settings = CreateButton(chip.transform, "Settings Button", new Vector3(0f, -0.05f, 0f), "settings",
                "settings.title", 28, theme, new Vector2(0.12f, 0.04f));
            Set(status, "m_chip", chip);
            Set(status, "m_statusButton", recognising);
            Set(status, "m_settingsButton", settings);
            Set(status, "m_windowsButton", windows);
            SpatialButton exit = CreateButton(chip.transform, "Exit Button", new Vector3(0.13f, -0.05f, 0f), "exit",
                "status.exit", 28, theme, new Vector2(0.12f, 0.04f));
            Set(status, "m_exitButton", exit);
        }

        private static TextAsset LoadText(string path)
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            if (asset == null) throw new System.InvalidOperationException($"Missing {path}.");
            return asset;
        }

        // The tokens live in an asset so the look can be tuned without code (DESIGN.md section 2).
        private static WordWorldTheme LoadOrCreateTheme()
        {
            var theme = AssetDatabase.LoadAssetAtPath<WordWorldTheme>(ThemePath);
            if (theme != null) return theme;
            theme = ScriptableObject.CreateInstance<WordWorldTheme>();
            AssetDatabase.CreateAsset(theme, ThemePath);
            return theme;
        }

        // DESIGN.md section 7. Text is filled in at runtime from ui_strings.json; the authored text is
        // only a placeholder. Children sit 1 cm in front of their card so they win the hit test.
        private static void CreateEntryFlow(EntryFlow entry, WordWorldTheme theme, LearnerSettings settings,
            UiStrings strings, TrackingManager tracking)
        {
            // Language window: one card, the question and five large buttons.
            GameObject window = CreateCard(entry.transform, "Language Window", theme, new Vector2(0.42f, 0.66f));
            SpatialAdapterNativeText title = CreateChildText(window.transform, "Title", new Vector3(0f, 0.25f, -0.005f),
                "picker.title", 44, theme.Ink, Color.clear, new Vector2(0.4f, 0.1f), theme.PanelCornerRadius);
            var buttons = new List<Object>();
            for (int i = 0; i < LearnerSettings.TargetLanguages.Length; i++)
            {
                string language = LearnerSettings.TargetLanguages[i];
                buttons.Add(CreateButton(window.transform, $"Button {language}", new Vector3(0f, 0.12f - i * 0.095f, -0.01f),
                    language, $"lang.{language}.endonym", 64, theme, new Vector2(0.34f, 0.075f)));
            }

            // Intro card: two short lines and a Got it button.
            GameObject intro = CreateCard(entry.transform, "Intro Card", theme, new Vector2(0.6f, 0.24f));
            SpatialAdapterNativeText introText = CreateChildText(intro.transform, "Body", new Vector3(0f, 0.04f, -0.005f),
                "intro.body", 40, theme.Ink, Color.clear, new Vector2(0.58f, 0.1f), theme.PanelCornerRadius);
            SpatialButton gotIt = CreateButton(intro.transform, "Got It", new Vector3(0f, -0.06f, -0.01f),
                "got_it", "intro.got_it", 44, theme, new Vector2(0.16f, 0.06f));

            // Scanning hint: one small line.
            var hint = new GameObject("Scanning Hint");
            hint.transform.SetParent(entry.transform, false);
            SpatialAdapterNativeText hintText = CreateNativeText(hint, "scanning.hint", 36, TextJustification.Center,
                theme.InkMuted, theme.Card, new Vector2(0.4f, 0.05f), theme.LabelCornerRadius);
            AddSyncCollider(hint, new Vector3(0.4f, 0.05f, 0.01f));

            Set(entry, "m_settings", settings);
            Set(entry, "m_strings", strings);
            Set(entry, "m_tracking", tracking);
            Set(entry, "m_theme", theme);
            Set(entry, "m_languageWindow", window);
            Set(entry, "m_languageTitle", title);
            SetArray(entry, "m_languageButtons", buttons.ToArray());
            Set(entry, "m_introCard", intro);
            Set(entry, "m_introText", introText);
            Set(entry, "m_introButton", gotIt);
            Set(entry, "m_scanningHint", hint);
            Set(entry, "m_scanningText", hintText);

            // "Tap me" tooltip over the first label.
            var tapMe = new GameObject("Tap Me");
            tapMe.transform.SetParent(entry.transform, false);
            SpatialAdapterNativeText tapMeText = CreateNativeText(tapMe, "onboarding.tap_me", 30, TextJustification.Center,
                theme.Card, theme.Accent, new Vector2(0.09f, 0.035f), theme.LabelCornerRadius);
            AddSyncCollider(tapMe, new Vector3(0.001f, 0.001f, 0.001f));
            Set(entry, "m_tapMe", tapMe);
            Set(entry, "m_tapMeText", tapMeText);
        }

        private static GameObject CreateCard(Transform parent, string name, WordWorldTheme theme, Vector2 size)
        {
            var card = new GameObject(name);
            card.transform.SetParent(parent, false);
            card.transform.localPosition = new Vector3(0f, 1.2f, 1.2f);
            CreateNativeText(card, "", 10, TextJustification.Center, theme.Ink, theme.Card, size, theme.PanelCornerRadius);
            AddSyncCollider(card, new Vector3(size.x, size.y, 0.005f));
            return card;
        }

        private static SpatialAdapterNativeText CreateChildText(Transform parent, string name, Vector3 position, string value,
            int points, Color textColor, Color background, Vector2 size, float cornerRadius,
            TextJustification justification = TextJustification.Center)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            return CreateNativeText(go, value, points, justification, textColor, background, size, cornerRadius);
        }

        // DESIGN.md section 10: buttons are at least 3 x 3 cm and work with poke, pinch-ray and
        // controller. The solid (non-trigger) box is slightly thick so pokes register.
        private static SpatialButton CreateButton(Transform parent, string name, Vector3 position, string value,
            string placeholder, int points, WordWorldTheme theme, Vector2 size)
        {
            SpatialAdapterNativeText text = CreateChildText(parent, name, position, placeholder, points, theme.Ink,
                theme.Card, size, theme.LabelCornerRadius);
            GameObject go = text.gameObject;
            go.AddComponent<BoxCollider>().size = new Vector3(size.x, size.y, 0.02f);
            go.AddComponent<SpatialAdapterHoverEffect>();
            var button = go.AddComponent<SpatialButton>();
            button.Bind(value, text);
            return button;
        }

        private static void CreatePanel(Transform parent, out GameObject root, out SpatialAdapterNativeText text)
        {
            root = new GameObject("Debug Panel");
            root.transform.SetParent(parent, false);
            // Left of the entry cards so the two never overlap.
            root.transform.localPosition = new Vector3(-0.9f, 1.3f, 1.1f);
            // 42 pt is about 15 mm tall: comfortable at the panel's 1.1 m, and the longest status
            // line still fits the card.
            text = CreateNativeText(root, "Starting...", 42, TextJustification.Left, Color.white,
                new Color(0.04f, 0.06f, 0.12f, 0.9f), new Vector2(0.8f, 0.8f), 0.012f);
            AddSyncCollider(root, new Vector3(0.8f, 0.8f, 0.01f));
        }

        // Spatial Adapter only syncs runtime transform changes for objects that had a renderer,
        // canvas, text or collider when the scene was registered; native text does not count, so
        // without this the object stays at its build-time pose. The trigger also serves later taps.
        private static void AddSyncCollider(GameObject go, Vector3 size)
        {
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = size;
        }

        // Sizes are physical: text in points, background in metres, corner radius in metres. The
        // component's runtime corner-radius setter takes an int, so the radius is authored here.
        private static SpatialAdapterNativeText CreateNativeText(GameObject go, string value, int points,
            TextJustification justification, Color textColor, Color background, Vector2 backgroundSize, float cornerRadius)
        {
            // Text needs a solid background on Swan: over a transparent one (black or white) small
            // or distant glyphs rendered as dark blocks. All text sits on white cards, so use white.
            if (background.a <= 0f) background = Color.white;
            // Window and card text was too small to read at 1.2 m once sizes were true to scale.
            if (points > 12) points = Mathf.RoundToInt(points * WindowTextScale);
            var text = go.AddComponent<SpatialAdapterNativeText>();
            var so = new SerializedObject(text);
            so.FindProperty("_text").stringValue = value;
            so.FindProperty("_textColor").colorValue = textColor;
            so.FindProperty("_textSize").intValue = TextFit.ToNative(points);
            so.FindProperty("_justification").enumValueIndex = (int)justification;
            so.FindProperty("_backgroundColor").colorValue = background;
            so.FindProperty("_backgroundSize").vector2Value = backgroundSize;
            so.FindProperty("_backgroundCornerRadius").floatValue = cornerRadius;
            so.ApplyModifiedPropertiesWithoutUndo();
            return text;
        }

        private static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty property = so.FindProperty(field);
            if (property == null) throw new System.InvalidOperationException($"{target.GetType().Name}.{field} not found");
            property.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            // SerializedProperty silently drops references whose type it rejects; fail loudly instead.
            if (value != null && new SerializedObject(target).FindProperty(field).objectReferenceValue != value)
                throw new System.InvalidOperationException($"{target.GetType().Name}.{field} rejected {value.GetType().FullName}");
        }

        private static void Set(Object target, string field, TextJustification value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).enumValueIndex = (int)value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(Object target, string field, float value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetArray(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            SerializedProperty property = so.FindProperty(field);
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddToBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        /// <summary>Room mesh on, and a dedicated physics layer so placement rays only hit the mesh.</summary>
        public static void EnsureProjectSettings()
        {
            PXR_ProjectSetting config = PXR_ProjectSetting.GetProjectConfig();
            if (!config.spatialMesh)
            {
                config.spatialMesh = true;
                EditorUtility.SetDirty(config);
            }

            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            for (int i = 0; i < layers.arraySize; i++)
                if (layers.GetArrayElementAtIndex(i).stringValue == RoomMesh.LayerName) return;
            for (int i = 8; i < layers.arraySize; i++)
            {
                SerializedProperty layer = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(layer.stringValue)) continue;
                layer.stringValue = RoomMesh.LayerName;
                tagManager.ApplyModifiedPropertiesWithoutUndo();
                return;
            }
            Debug.LogError("[WordWorld] no free user layer for RoomMesh");
        }
    }
}
