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
using WordWorld.Audio;
using WordWorld.Data;
using WordWorld.DebugTools;
using WordWorld.Detection;
using WordWorld.Labels;
using WordWorld.Practice;
using WordWorld.Spatial;
using WordWorld.Tracking;
using WordWorld.UI;

namespace WordWorld.Editor
{
    /// <summary>
    /// Regenerates Assets/Scenes/Spatial/WordWorld.unity around its existing Main Camera, which has
    /// the Spatial camera setup that works on Swan. Spatial Adapter only renders objects that are
    /// authored into the scene, so the label pool and panels are created here rather than at runtime.
    /// Headless: -executeMethod WordWorld.Editor.WordWorldSceneBuilder.BuildScene
    /// </summary>
    public static class WordWorldSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Spatial/WordWorld.unity";
        // The scene was first copied from PICO's face sample for its Swan-tested Spatial camera.
        // That sample is gone, so a rebuild keeps this camera root and regenerates everything else.
        private const string CameraRootName = "Main Camera";
        private const string PackagePath = "Assets/SpatialMLPipelineZoo/yolo-detection/yolo-detection.asset";
        private const int LabelPoolSize = 24;
        private const int ScanDotCount = 8;
        // One knob for authored text size (DESIGN.md: panels read at about 1.2 m). Labels size
        // themselves at runtime and are not affected.
        private const float WindowTextScale = 1.5f;
        private const string VocabularyPath = "Assets/Data/vocabulary.json";
        private const string UiStringsPath = "Assets/Data/ui_strings.json";
        private const string SpeechPath = "Assets/Data/speech.json";
        private const string ThemePath = "Assets/Data/WordWorldTheme.asset";
        // Set at the start of a build so the helpers can fill text backgrounds.
        private static WordWorldTheme s_theme;

        [MenuItem("Word World/Rebuild Word World Scene")]
        public static void BuildScene()
        {
            EnsureProjectSettings();

            var package = AssetDatabase.LoadAssetAtPath<SpatialMLPipelineZooAsset>(PackagePath);
            if (package == null) throw new System.InvalidOperationException($"Missing {PackagePath}; run tools/spatialml import first.");

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                throw new System.InvalidOperationException($"Missing {ScenePath}; restore it from git (it holds the Spatial camera).");

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            bool hasCamera = false;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == CameraRootName) hasCamera = true;
                else Object.DestroyImmediate(root);
            }
            if (!hasCamera)
                throw new System.InvalidOperationException($"{ScenePath} has no '{CameraRootName}' root.");

            WordWorldTheme theme = LoadOrCreateTheme();
            s_theme = theme;
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

            VocabAudio vocabAudio = CreateAudio(app.transform, settings, out AudioTurn turn, out SpeechData speechData,
                out SpeechCheck speech);

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
            Set(detail, "m_audio", vocabAudio);

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
            Set(quiz, "m_audio", vocabAudio);

            var sayIt = new GameObject("Say It").AddComponent<SayItCard>();
            sayIt.transform.SetParent(app.transform, false);
            CreateSayItCard(sayIt, theme);
            Set(sayIt, "m_settings", settings);
            Set(sayIt, "m_strings", strings);
            Set(sayIt, "m_vocabulary", vocabulary);
            Set(sayIt, "m_wordBank", wordBank);
            Set(sayIt, "m_labels", labels);
            Set(sayIt, "m_speechData", speechData);
            Set(sayIt, "m_speech", speech);
            Set(sayIt, "m_turn", turn);
            Set(sayIt, "m_audio", vocabAudio);
            Set(sayIt, "m_theme", theme);
            Set(detail, "m_sayIt", sayIt);

            var journey = new GameObject("Journey").AddComponent<JourneyCard>();
            journey.transform.SetParent(app.transform, false);
            CreateJourneyCard(journey, theme);
            Set(journey, "m_settings", settings);
            Set(journey, "m_strings", strings);
            Set(journey, "m_wordBank", wordBank);
            Set(journey, "m_quiz", quiz);
            Set(journey, "m_sayIt", sayIt);
            Set(journey, "m_theme", theme);

            var home = new GameObject("Home").AddComponent<HomeMenu>();
            home.transform.SetParent(app.transform, false);
            CreateHomeMenu(home, theme);
            Set(home, "m_settings", settings);
            Set(home, "m_strings", strings);
            Set(home, "m_wordBank", wordBank);
            Set(home, "m_labels", labels);
            Set(home, "m_theme", theme);
            Set(home, "m_journey", journey);
            Set(home, "m_quiz", quiz);
            Set(home, "m_sayIt", sayIt);
            Set(home, "m_wordBankWindow", wordBankWindow);
            Set(home, "m_progressWindow", progress);
            Set(home, "m_settingsWindow", settingsWindow);
            Set(home, "m_detailCard", detail);

            var status = new GameObject("Dock").AddComponent<StatusChip>();
            status.transform.SetParent(app.transform, false);
            CreateStatusChip(status, theme);
            Set(status, "m_settings", settings);
            Set(detail, "m_status", status);
            Set(status, "m_strings", strings);
            Set(status, "m_tracking", tracking);
            Set(status, "m_home", home);
            Set(status, "m_turn", turn);
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
            Set(entry, "m_vocabulary", vocabulary);

            // V2 review: make the space feel alive. A few slow motes of light around the learner.
            var motes = new GameObject("Ambient Motes").AddComponent<AmbientMotes>();
            motes.transform.SetParent(app.transform, false);
            Set(motes, "m_settings", settings);
            Material moteMaterial = CreateMoteMaterial();
            Set(motes, "m_material", moteMaterial);
            AmbientMotes.Configure(motes.GetComponent<ParticleSystem>(), moteMaterial);

            // Room ripple and light-up particles.
            var fx = new GameObject("Room Fx").AddComponent<RoomFx>();
            fx.transform.SetParent(app.transform, false);
            var ring = new GameObject("Ripple Ring").AddComponent<ParticleSystem>();
            ring.transform.SetParent(fx.transform, false);
            RoomFx.ConfigureRing(ring, moteMaterial, new Color(theme.Correct.r, theme.Correct.g, theme.Correct.b, 0.9f), 1.8f);
            var sparkle = new GameObject("Sparkle").AddComponent<ParticleSystem>();
            sparkle.transform.SetParent(fx.transform, false);
            RoomFx.ConfigureSparkle(sparkle, moteMaterial, new Color(1f, 0.9f, 0.6f, 1f));
            Set(fx, "m_tracking", tracking);
            Set(fx, "m_ring", ring);
            Set(fx, "m_sparkle", sparkle);

            // Practice objects: five CC0 models standing in for real ones (Settings).
            var practice = new GameObject("Practice Objects").AddComponent<PracticeObjects>();
            practice.transform.SetParent(app.transform, false);
            Set(practice, "m_settings", settings);
            Set(practice, "m_tracking", tracking);
            Set(practice, "m_labels", labels);
            SetArray(practice, "m_models", CreatePracticeModels(practice.transform));
            Set(settingsWindow, "m_practice", practice);

            // Says plainly when object recognition is not working, and offers the practice objects.
            var notice = new GameObject("Recognition Notice").AddComponent<RecognitionNotice>();
            notice.transform.SetParent(app.transform, false);
            CreateRecognitionNotice(notice, theme);
            Set(notice, "m_settings", settings);
            Set(notice, "m_strings", strings);
            Set(notice, "m_tracking", tracking);
            Set(notice, "m_theme", theme);
            Set(status, "m_notice", notice);

            // The guided tour: after onboarding, and from the menu's Tour button.
            var tour = new GameObject("Tour").AddComponent<Tour>();
            tour.transform.SetParent(app.transform, false);
            CreateTour(tour, theme);
            Set(tour, "m_settings", settings);
            Set(tour, "m_strings", strings);
            Set(tour, "m_theme", theme);
            Set(tour, "m_labels", labels);
            Set(tour, "m_detail", detail);
            Set(tour, "m_dock", status);
            Set(tour, "m_home", home);
            Set(tour, "m_turn", turn);
            Set(home, "m_tour", tour);
            Set(entry, "m_tour", tour);

            // A ray from each index fingertip, always on while the hand is tracked.
            var fingerRay = new GameObject("Finger Ray").AddComponent<FingerRay>();
            fingerRay.transform.SetParent(app.transform, false);
            var beams = new List<Object>();
            var dots = new List<Object>();
            foreach (string side in new[] { "Left", "Right" })
            {
                var beam = new GameObject($"{side} Beam");
                beam.transform.SetParent(fingerRay.transform, false);
                beams.Add(CreateNativeText(beam, "", 10, TextJustification.Center, Color.white,
                    new Color(1f, 1f, 1f, 0.55f), new Vector2(0.0035f, 0.6f), 0.00175f));
                AddSyncCollider(beam, new Vector3(0.001f, 0.001f, 0.001f));
                beam.SetActive(false);
                var dot = new GameObject($"{side} Dot");
                dot.transform.SetParent(fingerRay.transform, false);
                dots.Add(CreateNativeText(dot, "", 10, TextJustification.Center, theme.Accent, theme.Accent,
                    new Vector2(0.014f, 0.014f), 0.007f));
                AddSyncCollider(dot, new Vector3(0.001f, 0.001f, 0.001f));
                dot.SetActive(false);
            }
            SetArray(fingerRay, "m_beams", beams.ToArray());
            SetArray(fingerRay, "m_dots", dots.ToArray());
            var raySo = new SerializedObject(fingerRay);
            raySo.FindProperty("m_targetColor").colorValue = theme.Accent;
            raySo.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            // Assigned last: the reference was found cleared when set before the label pool was built.
            Set(detector, "m_package", AssetDatabase.LoadAssetAtPath<SpatialMLPipelineZooAsset>(PackagePath));
            EditorSceneManager.SaveScene(scene);
            AddToBuildSettings();
            SetAppIcon();
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
            // is one panel covering both, so the seam between them never shows.
            SpatialAdapterNativeText text = CreateNativeText(go, "label", 90, TextJustification.Center,
                theme.Ink, theme.Card, new Vector2(0.3f, 0.07f), theme.LabelCornerRadius);
            AddSyncCollider(go, new Vector3(0.3f, 0.07f, 0.01f));
            SpatialAdapterNativeText backing = CreateChildText(go.transform, "Card", new Vector3(0f, 0f, 0.001f), "", 10,
                theme.Ink, theme.Card, new Vector2(0.3f, 0.09f), theme.LabelCornerRadius);
            AddSyncCollider(backing.gameObject, new Vector3(0.001f, 0.001f, 0.001f));
            // Hover ring: an empty text behind the card, clear until the label is looked or pointed at.
            SpatialAdapterNativeText ring = CreateChildText(go.transform, "Hover Ring", new Vector3(0f, 0f, 0.002f), "", 10,
                Color.clear, Color.clear, new Vector2(0.31f, 0.1f), theme.LabelCornerRadius + 0.005f);
            AddSyncCollider(ring.gameObject, new Vector3(0.001f, 0.001f, 0.001f));
            SpatialAdapterNativeText gloss = CreateChildText(go.transform, "Meaning", new Vector3(0f, -0.04f, -0.001f), "", 54,
                theme.InkMuted, theme.Card, new Vector2(0.2f, 0.025f), theme.LabelCornerRadius);
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
            view.Bind(text, ruby, gloss, backing, leader, dot, go.GetComponent<BoxCollider>(), bounds.transform, theme, ring);
            go.AddComponent<SpatialAdapterHoverEffect>();
            go.SetActive(false);
            return view;
        }

        // docs/audio-plan.md: one AudioTurn owns the voice. The template's listener sits on the window
        // camera, which isn't the user's head in spatial mode, so it is replaced by one that follows
        // the head. The voice is mostly 3D (it comes from the object) but never fades out completely.
        private static VocabAudio CreateAudio(Transform parent, LearnerSettings settings, out AudioTurn turn,
            out SpeechData speechData, out SpeechCheck speech)
        {
            foreach (AudioListener existing in Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(existing);

            var root = new GameObject("Audio");
            root.transform.SetParent(parent, false);
            var listener = new GameObject("Listener", typeof(AudioListener));
            listener.transform.SetParent(root.transform, false);
            var voiceObject = new GameObject("Voice");
            voiceObject.transform.SetParent(root.transform, false);
            var voice = voiceObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 0.6f;
            voice.rolloffMode = AudioRolloffMode.Linear;
            voice.minDistance = 2f;
            voice.maxDistance = 12f;
            voice.dopplerLevel = 0f;

            // Say It: what to expect (speech.json) and the Vosk recognizer.
            speechData = root.AddComponent<SpeechData>();
            Set(speechData, "m_table", LoadText(SpeechPath));
            speech = root.AddComponent<SpeechCheck>();
            Set(speech, "m_data", speechData);

            turn = root.AddComponent<AudioTurn>();
            Set(turn, "m_voice", voice);
            Set(turn, "m_listener", listener.transform);
            Set(turn, "m_speech", speech);
            Set(turn, "m_speechData", speechData);
            var audio = root.AddComponent<VocabAudio>();
            Set(audio, "m_clips", VocabAudioImport.BuildIndex());
            Set(audio, "m_turn", turn);
            Set(audio, "m_settings", settings);
            // Ripples, chimes and music (made in code); its own sources, silenced while the mic is open.
            var fx = root.AddComponent<SoundFx>();
            Set(fx, "m_settings", settings);
            Set(fx, "m_turn", turn);
            return audio;
        }

        // DESIGN.md section 4: 36 x 28 cm; word, pinyin and meaning, grammar and example rows with
        // the example's meaning, then Save and Not right?, with a close button in the corner.
        private static void CreateDetailCard(DetailCard detail, WordWorldTheme theme)
        {
            GameObject card = CreateCard(detail.transform, "Card", theme, new Vector2(0.36f, 0.28f));
            SpatialAdapterNativeText header = CreateChildText(card.transform, "Word", new Vector3(-0.08f, 0.1f, -0.005f),
                "word", 44, theme.Ink, Color.clear, new Vector2(0.18f, 0.05f), 0f, TextJustification.Left);
            SpatialAdapterNativeText sub = CreateChildText(card.transform, "Meaning", new Vector3(0f, 0.06f, -0.005f),
                "meaning", 26, theme.InkMuted, Color.clear, new Vector2(0.34f, 0.03f), 0f, TextJustification.Left);
            // Grammar, example and the example's meaning: up to five lines.
            SpatialAdapterNativeText body = CreateChildText(card.transform, "Body", new Vector3(0f, -0.015f, -0.005f),
                "body", 22, theme.Ink, Color.clear, new Vector2(0.34f, 0.11f), 0f, TextJustification.Left);
            SpatialButton close = CreateButton(card.transform, "Close", new Vector3(0.15f, 0.11f, -0.01f),
                "close", "×", 40, theme, new Vector2(0.04f, 0.04f));
            // Pronunciation (docs/audio-plan.md): Listen and Say it beside the word, the example
            // sentence in the bottom row. Everything plays or listens only on a tap.
            SpatialButton listen = CreateButton(card.transform, "Listen", new Vector3(0.04f, 0.11f, -0.01f),
                "listen", "audio.listen", 20, theme, new Vector2(0.055f, 0.04f));
            SpatialButton sayIt = CreateButton(card.transform, "Say It", new Vector3(0.1f, 0.11f, -0.01f),
                "say_it", "audio.say_it", 20, theme, new Vector2(0.055f, 0.04f));
            SpatialButton example = CreateButton(card.transform, "Listen Example", new Vector3(0f, -0.113f, -0.01f),
                "listen_example", "audio.example", 26, theme, new Vector2(0.11f, 0.04f));
            SpatialButton save = CreateButton(card.transform, "Save", new Vector3(-0.12f, -0.113f, -0.01f),
                "save", "detail.save", 26, theme, new Vector2(0.11f, 0.04f));
            SpatialButton notRight = CreateButton(card.transform, "Not Right", new Vector3(0.12f, -0.113f, -0.01f),
                "not_right", "detail.not_right", 26, theme, new Vector2(0.11f, 0.04f));

            Set(detail, "m_card", card);
            Set(detail, "m_header", header);
            Set(detail, "m_subheader", sub);
            Set(detail, "m_body", body);
            Set(detail, "m_saveButton", save);
            Set(detail, "m_notRightButton", notRight);
            Set(detail, "m_closeButton", close);
            Set(detail, "m_listenButton", listen);
            Set(detail, "m_exampleButton", example);
            Set(detail, "m_sayItButton", sayIt);
        }

        // DESIGN.md section 5.4: 42 x 72 cm (taller than the 40 x 50 cm sketch, to keep 1 cm between
        // 4 cm buttons and to explain the reading levels under their title).
        private static void CreateSettingsWindow(SettingsWindow window, WordWorldTheme theme)
        {
            GameObject card = CreateCard(window.transform, "Settings Window", theme, new Vector2(0.42f, 0.72f));
            Transform t = card.transform;
            var rowSize = new Vector2(0.38f, 0.03f);
            var buttons = new List<Object>();
            const float z = -0.01f;
            SpatialButton Button(string value, float x, float y, float width, int points = 28, float height = 0.04f) =>
                CreateButton(t, $"Button {value}", new Vector3(x, y, z), value, value, points, theme, new Vector2(width, height));
            SpatialAdapterNativeText Row(string name, float y, int points = 26) =>
                CreateChildText(t, name, new Vector3(0f, y, -0.005f), name, points, theme.InkMuted, Color.clear, rowSize, 0f,
                    TextJustification.Left);

            SpatialAdapterNativeText title = CreateChildText(t, "Title", new Vector3(-0.03f, 0.325f, -0.005f), "settings.title",
                44, theme.Ink, Color.clear, new Vector2(0.32f, 0.05f), 0f, TextJustification.Left);
            buttons.Add(Button("close", 0.175f, 0.325f, 0.04f, 40));

            SpatialAdapterNativeText learning = Row("I'm learning", 0.225f);
            for (int i = 0; i < LearnerSettings.TargetLanguages.Length; i++)
                buttons.Add(Button($"lang:{LearnerSettings.TargetLanguages[i]}", -0.16f + i * 0.08f, 0.185f, 0.07f, 24));

            SpatialAdapterNativeText ui = Row("Interface language", 0.135f);
            buttons.Add(Button("ui:en", -0.125f, 0.095f, 0.12f));
            buttons.Add(Button("ui:zh", 0.005f, 0.095f, 0.12f));

            SpatialAdapterNativeText level = Row("Reading level", 0.045f);
            // What the levels change, in two short lines under the title.
            SpatialAdapterNativeText levelHint = CreateChildText(t, "Level Hint", new Vector3(0f, 0.008f, -0.005f),
                "settings.level_hint", 18, theme.InkMuted, Color.clear, new Vector2(0.38f, 0.04f), 0f, TextJustification.Left);
            for (int i = 1; i <= 3; i++) buttons.Add(Button($"level:{i}", -0.165f + (i - 1) * 0.055f, -0.04f, 0.045f));
            SpatialAdapterNativeText preview = CreateChildText(t, "Preview", new Vector3(0.09f, -0.04f, -0.005f), "preview",
                32, theme.Ink, theme.AccentSoft, new Vector2(0.18f, 0.06f), theme.LabelCornerRadius);

            buttons.Add(Button("meaning", -0.13f, -0.1f, 0.125f, 18));
            buttons.Add(Button("pinyin", 0f, -0.1f, 0.125f, 18));
            buttons.Add(Button("people", 0.13f, -0.1f, 0.125f, 18));

            SpatialAdapterNativeText size = Row("Label size", -0.16f);
            for (int i = 0; i < 3; i++) buttons.Add(Button($"size:{"SML"[i]}", -0.005f + i * 0.08f, -0.16f, 0.07f, 32, 0.05f));

            SpatialAdapterNativeText confidence = Row("Detection confidence", -0.225f);
            buttons.Add(Button("conf:-", 0.085f, -0.225f, 0.07f, 40, 0.05f));
            buttons.Add(Button("conf:+", 0.165f, -0.225f, 0.07f, 40, 0.05f));

            buttons.Add(Button("audio", -0.13f, -0.28f, 0.125f, 18));
            buttons.Add(Button("practice", 0f, -0.28f, 0.125f, 18));
            buttons.Add(Button("practice:here", 0.13f, -0.28f, 0.125f, 18));

            SpatialAdapterNativeText privacy = Row("Privacy", -0.325f, 20);

            SetArray(window, "m_buttons", buttons.ToArray());
            Set(window, "m_window", card);
            Set(window, "m_title", title);
            Set(window, "m_learningRow", learning);
            Set(window, "m_interfaceRow", ui);
            Set(window, "m_levelRow", level);
            Set(window, "m_levelHint", levelHint);
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
                    theme.Info, new Vector2(barWidth, 0.012f), 0.006f);
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

        // DESIGN.md section 6: the floating prompt (word, score, timer, Skip) shown during a round,
        // the results card shown after it, and the correct ring and hint glow in the room.
        private static void CreateQuiz(FindItQuiz quiz, WordWorldTheme theme)
        {
            GameObject card = CreateCard(quiz.transform, "Results Card", theme, new Vector2(0.4f, 0.24f));
            Transform t = card.transform;
            SpatialAdapterNativeText title = CreateChildText(t, "Title", new Vector3(0f, 0.085f, -0.005f), "quiz.title",
                44, theme.Ink, Color.clear, new Vector2(0.36f, 0.05f), 0f, TextJustification.Left);
            SpatialAdapterNativeText body = CreateChildText(t, "Body", new Vector3(0f, 0.01f, -0.005f), "body",
                28, theme.Ink, Color.clear, new Vector2(0.36f, 0.08f), 0f, TextJustification.Left);
            SpatialButton primary = CreateButton(t, "Primary", new Vector3(-0.08f, -0.08f, -0.01f), "primary", "quiz.play_again",
                30, theme, new Vector2(0.17f, 0.05f));
            SpatialButton secondary = CreateButton(t, "Secondary", new Vector3(0.1f, -0.08f, -0.01f), "secondary", "quiz.done",
                30, theme, new Vector2(0.13f, 0.05f));

            var floating = new GameObject("Floating Prompt");
            floating.transform.SetParent(quiz.transform, false);
            floating.transform.localPosition = new Vector3(0f, 1.3f, 0.6f);
            SpatialAdapterNativeText floatingText = CreateNativeText(floating, "prompt", 48, TextJustification.Center,
                theme.Ink, theme.Card, new Vector2(0.36f, 0.075f), theme.LabelCornerRadius);
            AddSyncCollider(floating, new Vector3(0.36f, 0.075f, 0.005f));
            SpatialAdapterNativeText floatingStatus = CreateChildText(floating.transform, "Score", new Vector3(0f, -0.06f, -0.002f),
                "score", 20, theme.InkMuted, theme.Card, new Vector2(0.22f, 0.03f), theme.LabelCornerRadius);
            SpatialAdapterNativeText track = CreateChildText(floating.transform, "Timer Track", new Vector3(0f, -0.088f, -0.002f),
                "", 10, theme.Ink, theme.Info, new Vector2(0.3f, 0.008f), 0.004f);
            SpatialAdapterNativeText fill = CreateChildText(floating.transform, "Timer Fill", new Vector3(0f, -0.088f, -0.004f),
                "", 10, theme.Accent, theme.Accent, new Vector2(0.3f, 0.008f), 0.004f);
            AddSyncCollider(fill.gameObject, new Vector3(0.001f, 0.001f, 0.001f));
            SpatialButton skip = CreateButton(floating.transform, "Skip", new Vector3(0f, -0.125f, -0.005f), "skip",
                "quiz.skip", 28, theme, new Vector2(0.1f, 0.045f));
            // Optional audio: beside the prompt, 1 cm clear of it.
            SpatialButton listen = CreateButton(floating.transform, "Listen", new Vector3(0.235f, 0f, -0.005f), "listen",
                "audio.listen", 24, theme, new Vector2(0.09f, 0.045f));
            // End the exercise: mirrors Listen on the other side.
            SpatialButton cancel = CreateButton(floating.transform, "End", new Vector3(-0.235f, 0f, -0.005f), "end",
                "quiz.end", 24, theme, new Vector2(0.09f, 0.045f));

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
            Set(quiz, "m_primary", primary);
            Set(quiz, "m_secondary", secondary);
            Set(quiz, "m_floatingPrompt", floating);
            Set(quiz, "m_cancel", cancel);
            Set(quiz, "m_floatingText", floatingText);
            Set(quiz, "m_floatingStatus", floatingStatus);
            Set(quiz, "m_timerTrack", track);
            Set(quiz, "m_timerFill", fill);
            Set(quiz, "m_skip", skip);
            Set(quiz, "m_listen", listen);
            Set(quiz, "m_ring", ringText);
            Set(quiz, "m_glow", glowText);
        }

        // docs/audio-plan.md: the Say It card. The line to say (with pinyin above for Mandarin Level 2),
        // its meaning, a row of word chips after each try, the feedback line, then Listen, Speak and
        // Next. Parts shown, hidden or moved at runtime get sync colliders.
        private static void CreateSayItCard(SayItCard sayIt, WordWorldTheme theme)
        {
            GameObject card = CreateCard(sayIt.transform, "Say It Card", theme, new Vector2(0.4f, 0.34f));
            Transform t = card.transform;
            var tiny = new Vector3(0.001f, 0.001f, 0.001f);
            SpatialAdapterNativeText Text(string name, Vector3 position, string value, int points, Color color, Vector2 size,
                TextJustification justification = TextJustification.Center, bool toggled = true)
            {
                SpatialAdapterNativeText text = CreateChildText(t, name, position, value, points, color, Color.clear, size, 0f, justification);
                if (toggled) AddSyncCollider(text.gameObject, tiny);
                return text;
            }

            SpatialAdapterNativeText title = Text("Title", new Vector3(-0.06f, 0.14f, -0.005f), "sayit.title", 30, theme.Ink,
                new Vector2(0.26f, 0.04f), TextJustification.Left, toggled: false);
            SpatialAdapterNativeText progress = Text("Progress", new Vector3(0.11f, 0.14f, -0.005f), "1 / 5", 20, theme.InkMuted,
                new Vector2(0.08f, 0.03f));
            SpatialButton close = CreateButton(t, "Close", new Vector3(0.175f, 0.14f, -0.01f), "close", "×", 40, theme,
                new Vector2(0.04f, 0.04f));
            SpatialAdapterNativeText ruby = Text("Ruby", new Vector3(0f, 0.1f, -0.005f), "pinyin", 22, theme.Accent,
                new Vector2(0.36f, 0.03f));
            SpatialAdapterNativeText prompt = Text("Prompt", new Vector3(0f, 0.062f, -0.005f), "prompt", 28, theme.Ink,
                new Vector2(0.37f, 0.06f));
            SpatialAdapterNativeText meaning = Text("Meaning", new Vector3(0f, 0.018f, -0.005f), "meaning", 20, theme.InkMuted,
                new Vector2(0.36f, 0.025f));

            var chips = new List<Object>();
            for (int i = 0; i < 8; i++)
            {
                SpatialAdapterNativeText chip = CreateChildText(t, $"Chip {i}", new Vector3(0f, -0.035f, -0.006f), "", 20,
                    theme.Ink, theme.AccentSoft, new Vector2(0.05f, 0.026f), theme.LabelCornerRadius);
                AddSyncCollider(chip.gameObject, new Vector3(0.05f, 0.026f, 0.002f));
                chip.gameObject.SetActive(false);
                chips.Add(chip);
            }

            SpatialAdapterNativeText result = Text("Result", new Vector3(0f, -0.1f, -0.005f), "result", 20, theme.InkMuted,
                new Vector2(0.37f, 0.04f), toggled: false);
            // Speak is the widest: it also shows "Listening… tap when done". 1.5 cm gaps, inside the 40 cm card.
            SpatialButton listen = CreateButton(t, "Listen", new Vector3(-0.14f, -0.145f, -0.01f), "listen", "audio.listen",
                24, theme, new Vector2(0.09f, 0.04f));
            SpatialButton speak = CreateButton(t, "Speak", new Vector3(0f, -0.145f, -0.01f), "speak", "sayit.speak",
                24, theme, new Vector2(0.16f, 0.04f));
            SpatialButton next = CreateButton(t, "Next", new Vector3(0.14f, -0.145f, -0.01f), "next", "sayit.next",
                24, theme, new Vector2(0.09f, 0.04f));

            Set(sayIt, "m_card", card);
            Set(sayIt, "m_title", title);
            Set(sayIt, "m_progress", progress);
            Set(sayIt, "m_ruby", ruby);
            Set(sayIt, "m_prompt", prompt);
            Set(sayIt, "m_meaning", meaning);
            Set(sayIt, "m_result", result);
            SetArray(sayIt, "m_chips", chips.ToArray());
            Set(sayIt, "m_listen", listen);
            Set(sayIt, "m_speak", speak);
            Set(sayIt, "m_next", next);
            Set(sayIt, "m_close", close);
        }

        // DESIGN.md section 8: the dock. The recognition pill, and Home under it.
        private static void CreateStatusChip(StatusChip status, WordWorldTheme theme)
        {
            var chip = new GameObject("Dock");
            chip.transform.SetParent(status.transform, false);
            chip.transform.localPosition = new Vector3(0f, 0.9f, 1f);
            SpatialButton recognising = CreateButton(chip.transform, "Recognition", Vector3.zero, "pause",
                "status.recognizing", 24, theme, new Vector2(0.26f, 0.062f));
            // Home and Move side by side under the pill, together as wide as it.
            SpatialButton home = CreateButton(chip.transform, "Home Button", new Vector3(-0.0675f, -0.085f, 0f), "home",
                "dock.home", 28, theme, new Vector2(0.125f, 0.045f));
            SpatialButton move = CreateButton(chip.transform, "Move Button", new Vector3(0.0675f, -0.085f, 0f), "move",
                "dock.move", 28, theme, new Vector2(0.125f, 0.045f));
            // The pill and the Home button under it.
            chip.AddComponent<PanelOccluder>().Configure(new Vector2(0.27f, 0.15f), new Vector2(0f, -0.04f));
            Set(status, "m_chip", chip);
            Set(status, "m_statusButton", recognising);
            Set(status, "m_homeButton", home);
            Set(status, "m_moveButton", move);
        }

        // DESIGN.md section 5: the Journey card, low and to the right.
        private static void CreateJourneyCard(JourneyCard journey, WordWorldTheme theme)
        {
            GameObject card = CreateCard(journey.transform, "Journey Card", theme, new Vector2(0.34f, 0.17f));
            Transform t = card.transform;
            SpatialAdapterNativeText title = CreateChildText(t, "Title", new Vector3(0f, 0.058f, -0.005f), "journey.title",
                30, theme.Ink, Color.clear, new Vector2(0.3f, 0.035f), 0f, TextJustification.Left);
            SpatialAdapterNativeText goal = CreateChildText(t, "Goal", new Vector3(0f, 0.008f, -0.005f), "goal",
                22, theme.Ink, Color.clear, new Vector2(0.3f, 0.06f), 0f, TextJustification.Left);
            SpatialAdapterNativeText barTrack = CreateChildText(t, "Bar Track", new Vector3(0f, -0.055f, -0.006f), "", 10,
                theme.Ink, theme.Info, new Vector2(0.28f, 0.012f), 0.006f);
            AddSyncCollider(barTrack.gameObject, new Vector3(0.001f, 0.001f, 0.001f));
            SpatialAdapterNativeText barFill = CreateChildText(t, "Bar Fill", new Vector3(-0.14f, -0.055f, -0.008f), "", 10,
                theme.Accent, theme.Accent, new Vector2(0.001f, 0.012f), 0.006f);
            AddSyncCollider(barFill.gameObject, new Vector3(0.001f, 0.001f, 0.001f));
            SpatialButton start = CreateButton(t, "Start", new Vector3(0f, -0.052f, -0.01f), "start", "journey.start",
                28, theme, new Vector2(0.2f, 0.045f));
            Set(journey, "m_card", card);
            Set(journey, "m_title", title);
            Set(journey, "m_goal", goal);
            Set(journey, "m_barTrack", barTrack);
            Set(journey, "m_barFill", barFill);
            Set(journey, "m_startButton", start);
        }

        // DESIGN.md section 5: the one menu. Main page: Journey and Practice, with Progress, Settings
        // and Exit small underneath, then the Sound row. Practice page: the exercises and the round length.
        private static void CreateHomeMenu(HomeMenu home, WordWorldTheme theme)
        {
            // 62 cm tall: the Sound row needs one more row of 4.5 cm buttons below the small actions.
            GameObject card = CreateCard(home.transform, "Menu", theme, new Vector2(0.42f, 0.62f));
            Transform t = card.transform;
            SpatialAdapterNativeText title = CreateChildText(t, "Title", new Vector3(0f, 0.25f, -0.005f), "home.title",
                44, theme.Ink, Color.clear, new Vector2(0.2f, 0.05f), 0f);
            SpatialAdapterNativeText subtitle = CreateChildText(t, "Subtitle", new Vector3(0f, 0.195f, -0.005f), "home.subtitle",
                24, theme.InkMuted, Color.clear, new Vector2(0.38f, 0.035f), 0f);
            AddSyncCollider(subtitle.gameObject, new Vector3(0.001f, 0.001f, 0.001f));
            var buttons = new List<Object>();

            GameObject Page(string name)
            {
                var page = new GameObject(name);
                page.transform.SetParent(t, false);
                // Pages are shown and hidden at runtime.
                AddSyncCollider(page, new Vector3(0.001f, 0.001f, 0.001f));
                return page;
            }
            void Button(GameObject page, string value, float x, float y, Vector2 size, int points) =>
                buttons.Add(CreateButton(page.transform, $"Button {value}", new Vector3(x, y, -0.01f), value, value, points, theme, size));

            GameObject main = Page("Main Page");
            Button(main, "tour", 0.155f, 0.25f, new Vector2(0.08f, 0.045f), 22);
            Button(main, "journey", 0f, 0.05f, new Vector2(0.36f, 0.11f), 32);
            Button(main, "practice", 0f, -0.075f, new Vector2(0.36f, 0.11f), 32);
            Button(main, "progress", -0.125f, -0.195f, new Vector2(0.11f, 0.045f), 24);
            Button(main, "settings", 0f, -0.195f, new Vector2(0.11f, 0.045f), 24);
            Button(main, "exit", 0.125f, -0.195f, new Vector2(0.11f, 0.045f), 24);
            // Sound: Off, Alerts only (effects), All (effects, music and spoken words).
            SpatialAdapterNativeText soundLabel = CreateChildText(main.transform, "Sound Label", new Vector3(-0.14f, -0.255f, -0.005f),
                "sound.title", 22, theme.InkMuted, Color.clear, new Vector2(0.1f, 0.035f), 0f, TextJustification.Left);
            Button(main, "sound:off", -0.04f, -0.255f, new Vector2(0.085f, 0.045f), 22);
            Button(main, "sound:alerts", 0.055f, -0.255f, new Vector2(0.085f, 0.045f), 20);
            Button(main, "sound:all", 0.15f, -0.255f, new Vector2(0.085f, 0.045f), 22);

            GameObject practice = Page("Practice Page");
            // Four exercises; Say It hides where speech can't be checked.
            Button(practice, "back", -0.15f, 0.25f, new Vector2(0.09f, 0.045f), 24);
            Button(practice, "explore", 0f, 0.135f, new Vector2(0.36f, 0.075f), 24);
            Button(practice, "find_it", 0f, 0.05f, new Vector2(0.36f, 0.075f), 24);
            Button(practice, "say_it", 0f, -0.035f, new Vector2(0.36f, 0.075f), 24);
            Button(practice, "word_bank", 0f, -0.12f, new Vector2(0.36f, 0.075f), 24);
            SpatialAdapterNativeText roundLabel = CreateChildText(practice.transform, "Round Label", new Vector3(-0.075f, -0.215f, -0.005f),
                "practice.round", 22, theme.InkMuted, Color.clear, new Vector2(0.2f, 0.035f), 0f, TextJustification.Left);
            Button(practice, "round:5", 0.07f, -0.215f, new Vector2(0.08f, 0.045f), 22);
            Button(practice, "round:10", 0.155f, -0.215f, new Vector2(0.08f, 0.045f), 22);
            practice.SetActive(false);

            Set(home, "m_window", card);
            Set(home, "m_title", title);
            Set(home, "m_subtitle", subtitle);
            Set(home, "m_mainPage", main);
            Set(home, "m_practicePage", practice);
            Set(home, "m_roundLabel", roundLabel);
            Set(home, "m_soundLabel", soundLabel);
            SetArray(home, "m_buttons", buttons.ToArray());
        }

        private const string ArtFolder = "Assets/WordWorld/Art";
        private const string MoteTexturePath = ArtFolder + "/Mote.png";
        private const string MoteMaterialPath = ArtFolder + "/Mote.mat";
        private const string AppIconPath = ArtFolder + "/AppIcon.png";

        // A soft round dot, written once as an asset so Spatial Adapter can export it with the material.
        private static Material CreateMoteMaterial()
        {
            if (!AssetDatabase.IsValidFolder(ArtFolder)) AssetDatabase.CreateFolder("Assets/WordWorld", "Art");
            if (!System.IO.File.Exists(MoteTexturePath))
            {
                const int size = 64;
                var dot = new Texture2D(size, size, TextureFormat.RGBA32, false);
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = new Vector2(x - size / 2f + 0.5f, y - size / 2f + 0.5f).magnitude / (size / 2f);
                    float a = Mathf.Clamp01(1f - d);
                    dot.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
                System.IO.File.WriteAllBytes(MoteTexturePath, dot.EncodeToPNG());
                Object.DestroyImmediate(dot);
                AssetDatabase.ImportAsset(MoteTexturePath);
                var importer = (TextureImporter)AssetImporter.GetAtPath(MoteTexturePath);
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(MoteTexturePath);
            var material = AssetDatabase.LoadAssetAtPath<Material>(MoteMaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                AssetDatabase.CreateAsset(material, MoteMaterialPath);
            }
            // Transparent, alpha-blended, no depth write: the URP particle shader's own keywords.
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(material);
            return material;
        }

        // V2 review: the app had no icon. AppIcon.png is the default icon for every platform.
        private static void SetAppIcon()
        {
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(AppIconPath);
            if (icon == null)
            {
                Debug.LogWarning($"[WordWorld] no app icon at {AppIconPath}");
                return;
            }
            PlayerSettings.SetIcons(UnityEditor.Build.NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            var android = UnityEditor.Build.NamedBuildTarget.Android;
            int count = PlayerSettings.GetIconSizes(android, IconKind.Any).Length;
            PlayerSettings.SetIcons(android, Enumerable.Repeat(icon, count).ToArray(), IconKind.Any);
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
        // ---- Practice objects -------------------------------------------------------------------

        private const string ModelsFolder = "Assets/WordWorld/Models";

        // Folder, COCO class, degrees right of the learner's view, distance (m), and the model it
        // stands on (by index) or -1 for the floor. Spread wide so they never sit under the windows.
        private static readonly (string folder, string coco, float angle, float distance, int standOn)[] s_practice =
        {
            ("dining_chair_02", "chair", -55f, 1.7f, -1),
            ("potted_plant_04", "potted plant", -25f, 1.5f, -1),
            ("round_wooden_table_01", "dining table", 20f, 2.0f, -1),
            ("ceramic_vase_01", "vase", 0f, 0f, 2),
            ("Sofa_01", "couch", 58f, 2.4f, -1),
        };

        private static Object[] CreatePracticeModels(Transform parent)
        {
            var models = new List<PracticeModel>();
            foreach (var spec in s_practice)
            {
                string fbxPath = $"{ModelsFolder}/{spec.folder}/{spec.folder}.fbx";
                var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
                if (fbx == null) throw new System.InvalidOperationException($"missing practice model {fbxPath}");
                int classId = System.Array.IndexOf(CocoClasses.Names, spec.coco);
                if (classId < 0) throw new System.InvalidOperationException($"unknown COCO class '{spec.coco}'");

                var root = new GameObject($"Practice {spec.coco}");
                root.transform.SetParent(parent, false);
                // Out of sight until placed beside the learner at runtime.
                root.transform.localPosition = new Vector3(0f, -20f, 0f);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
                model.transform.SetParent(root.transform, false);

                Material material = PracticeMaterial(spec.folder);
                foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
                {
                    var materials = new Material[renderer.sharedMaterials.Length];
                    for (int i = 0; i < materials.Length; i++) materials[i] = material;
                    renderer.sharedMaterials = materials;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }

                // Poly Haven models are true to scale in metres; guard against a unit mix-up anyway.
                Bounds bounds = ModelBounds(model);
                if (bounds.size.y > 4f) model.transform.localScale *= 0.01f;
                else if (bounds.size.y < 0.05f) model.transform.localScale *= 100f;
                bounds = ModelBounds(model);
                // Root origin at the model's bottom centre, so placing it puts it on the floor.
                Vector3 local = root.transform.InverseTransformPoint(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z));
                model.transform.localPosition -= local;
                Vector3 size = bounds.size;

                // The model itself is tappable, like its label.
                var box = root.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, size.y * 0.5f, 0f);
                box.size = size;
                var practice = root.AddComponent<PracticeModel>();
                practice.Configure(classId, size, 0f, spec.angle, spec.distance, spec.standOn >= 0 ? models[spec.standOn] : null);
                EditorUtility.SetDirty(practice);
                models.Add(practice);
            }
            return models.ToArray();
        }

        private static Bounds ModelBounds(GameObject model)
        {
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(model.transform.position, Vector3.one * 0.5f);
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        // One URP Lit material per model with its 1K diffuse texture (normals dropped to keep it light).
        private static Material PracticeMaterial(string folder)
        {
            string path = $"{ModelsFolder}/{folder}/{folder}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ModelsFolder}/{folder}/textures/{folder}_diff_1k.jpg");
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", 0.25f);
            EditorUtility.SetDirty(material);
            return material;
        }

        // DESIGN.md section 8: the "recognition isn't working" card.
        private static void CreateRecognitionNotice(RecognitionNotice notice, WordWorldTheme theme)
        {
            GameObject card = CreateCard(notice.transform, "Recognition Notice Card", theme, new Vector2(0.5f, 0.32f));
            Transform t = card.transform;
            SpatialAdapterNativeText title = CreateChildText(t, "Title", new Vector3(0f, 0.115f, -0.005f), "notice.title",
                34, theme.Ink, Color.clear, new Vector2(0.46f, 0.045f), 0f, TextJustification.Left);
            SpatialAdapterNativeText body = CreateChildText(t, "Body", new Vector3(0f, 0.02f, -0.005f), "notice.restart",
                24, theme.Ink, Color.clear, new Vector2(0.46f, 0.13f), 0f, TextJustification.Left);
            SpatialButton practice = CreateButton(t, "Use Practice", new Vector3(-0.07f, -0.11f, -0.01f), "practice",
                "notice.use_practice", 28, theme, new Vector2(0.26f, 0.05f));
            SpatialButton ok = CreateButton(t, "OK", new Vector3(0.15f, -0.11f, -0.01f), "ok", "common.ok", 28, theme,
                new Vector2(0.12f, 0.05f));
            Set(notice, "m_card", card);
            Set(notice, "m_title", title);
            Set(notice, "m_body", body);
            Set(notice, "m_practiceButton", practice);
            Set(notice, "m_okButton", ok);
        }

        private static void CreateEntryFlow(EntryFlow entry, WordWorldTheme theme, LearnerSettings settings,
            UiStrings strings, TrackingManager tracking)
        {
            // Language window: one card, the question and five large buttons.
            GameObject window = CreateCard(entry.transform, "Language Window", theme, new Vector2(0.42f, 0.66f));
            SpatialAdapterNativeText title = CreateChildText(window.transform, "Title", new Vector3(0f, 0.195f, -0.005f),
                "picker.title", 40, theme.Ink, Color.clear, new Vector2(0.4f, 0.1f), theme.PanelCornerRadius);
            // Resized at runtime, so it needs the sync collider too.
            AddSyncCollider(title.gameObject, new Vector3(0.001f, 0.001f, 0.001f));
            var buttons = new List<Object>();
            for (int i = 0; i < LearnerSettings.TargetLanguages.Length; i++)
            {
                string language = LearnerSettings.TargetLanguages[i];
                buttons.Add(CreateButton(window.transform, $"Button {language}", new Vector3(0f, 0.1f - i * 0.095f, -0.01f),
                    language, $"lang.{language}", 52, theme, new Vector2(0.34f, 0.075f)));
            }

            // Breadcrumb across the top: 1 your language › 2 learn › 3 start.
            var crumbs = new List<Object>();
            for (int i = 0; i < 3; i++)
            {
                crumbs.Add(CreateButton(window.transform, $"Crumb {i + 1}", new Vector3(-0.135f + i * 0.135f, 0.285f, -0.01f),
                    $"crumb:{i}", "onboarding.step", 18, theme, new Vector2(0.115f, 0.036f)));
                if (i < 2)
                    CreateChildText(window.transform, $"Crumb Arrow {i + 1}", new Vector3(-0.0675f + i * 0.135f, 0.285f, -0.005f),
                        "›", 24, theme.InkMuted, Color.clear, new Vector2(0.016f, 0.03f), 0f);
            }
            SpatialButton start = CreateButton(window.transform, "Start", new Vector3(0f, -0.08f, -0.01f), "start",
                "intro.got_it", 44, theme, new Vector2(0.2f, 0.07f));

            // Scanning hint: one small line.
            var hint = new GameObject("Scanning Hint");
            hint.transform.SetParent(entry.transform, false);
            SpatialAdapterNativeText hintText = CreateNativeText(hint, "scanning.try", 36, TextJustification.Center,
                theme.InkMuted, theme.Card, new Vector2(0.56f, 0.05f), theme.LabelCornerRadius);
            AddSyncCollider(hint, new Vector3(0.56f, 0.05f, 0.01f));

            Set(entry, "m_settings", settings);
            Set(entry, "m_strings", strings);
            Set(entry, "m_tracking", tracking);
            Set(entry, "m_theme", theme);
            Set(entry, "m_languageWindow", window);
            Set(entry, "m_languageTitle", title);
            SetArray(entry, "m_languageButtons", buttons.ToArray());
            SetArray(entry, "m_crumbs", crumbs.ToArray());
            Set(entry, "m_startButton", start);
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

            // First-launch title: a black veil that fades to reveal the room, then the name and purpose.
            var veil = new GameObject("Title Veil");
            veil.transform.SetParent(entry.transform, false);
            SpatialAdapterNativeText veilText = CreateNativeText(veil, "", 10, TextJustification.Center, Color.black,
                Color.black, new Vector2(4.2f, 3f), 0f);
            AddSyncCollider(veil, new Vector3(0.001f, 0.001f, 0.001f));
            GameObject titleCard = CreateCard(entry.transform, "Title Card", theme, new Vector2(0.66f, 0.3f));
            SpatialAdapterNativeText titleName = CreateChildText(titleCard.transform, "Name", new Vector3(0f, 0.05f, -0.005f),
                "title.name", 96, theme.Ink, Color.clear, new Vector2(0.62f, 0.09f), 0f);
            SpatialAdapterNativeText tagline = CreateChildText(titleCard.transform, "Tagline", new Vector3(0f, -0.065f, -0.005f),
                "title.tagline", 28, theme.InkMuted, Color.clear, new Vector2(0.62f, 0.09f), 0f);
            Set(entry, "m_veil", veil);
            Set(entry, "m_veilText", veilText);
            Set(entry, "m_titleCard", titleCard);
            Set(entry, "m_titleName", titleName);
            Set(entry, "m_titleTagline", tagline);

            // Guided first word: the first object in all five languages.
            GameObject showcase = CreateCard(entry.transform, "Showcase Card", theme, new Vector2(0.38f, 0.3f));
            SpatialAdapterNativeText showcaseHeader = CreateChildText(showcase.transform, "Header", new Vector3(0f, 0.115f, -0.005f),
                "showcase.title", 30, theme.Ink, Color.clear, new Vector2(0.34f, 0.04f), 0f, TextJustification.Left);
            var rows = new List<Object>();
            for (int i = 0; i < LearnerSettings.TargetLanguages.Length; i++)
                rows.Add(CreateChildText(showcase.transform, $"Row {LearnerSettings.TargetLanguages[i]}",
                    new Vector3(0f, 0.06f - i * 0.045f, -0.005f), "word", 30, theme.Ink, theme.Card,
                    new Vector2(0.34f, 0.04f), theme.LabelCornerRadius, TextJustification.Left));
            Set(entry, "m_showcase", showcase);
            Set(entry, "m_showcaseHeader", showcaseHeader);
            SetArray(entry, "m_showcaseRows", rows.ToArray());
        }

        private static GameObject CreateCard(Transform parent, string name, WordWorldTheme theme, Vector2 size)
        {
            var card = new GameObject(name);
            card.transform.SetParent(parent, false);
            card.transform.localPosition = new Vector3(0f, 1.2f, 1.2f);
            CreateNativeText(card, "", 10, TextJustification.Center, theme.Ink, theme.Card, size, theme.PanelCornerRadius);
            AddSyncCollider(card, new Vector3(size.x, size.y, 0.005f));
            AddPanelEdge(card.transform, size, theme);
            // Word labels that line up with an open card fade until it closes or moves.
            card.AddComponent<PanelOccluder>().Configure(size, Vector2.zero);
            return card;
        }

        // V3 review: panels should stand out from the room. Behind the face: a soft shadow, then a
        // solid rim whose lower half is darker, so the edge shades from light to dark like a bevel.
        // On the face: a bright line just inside the top edge. All empty native text backgrounds.
        private static void AddPanelEdge(Transform card, Vector2 size, WordWorldTheme theme)
        {
            float rim = theme.PanelRimWidth;
            float radius = theme.PanelCornerRadius;
            Plate(card, "Shadow", new Vector3(0f, -0.006f, 0.004f), size + new Vector2(0.012f, 0.012f), theme.PanelShadow, radius + 0.006f);
            Plate(card, "Rim", new Vector3(0f, 0f, 0.002f), size + new Vector2(2f * rim, 2f * rim), theme.PanelRimTop, radius + rim);
            // The lower half of the rim, in front of the upper colour but still behind the face.
            Vector2 lower = new Vector2(size.x + 2f * rim, size.y * 0.5f + rim);
            Plate(card, "Rim Lower", new Vector3(0f, -lower.y * 0.5f, 0.0015f), lower, theme.PanelRimBottom, radius + rim);
            Plate(card, "Top Highlight", new Vector3(0f, size.y * 0.5f - 0.003f, -0.001f),
                new Vector2(size.x - 2f * radius, 0.0015f), theme.PanelHighlight, 0.00075f);
        }

        private static GameObject Plate(Transform parent, string name, Vector3 position, Vector2 size, Color color, float radius)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            CreateNativeText(go, "", 10, TextJustification.Center, color, color, size, radius);
            // Moves with its window; a tiny trigger so Spatial Adapter syncs it, never a tap target.
            AddSyncCollider(go, new Vector3(0.001f, 0.001f, 0.001f));
            return go;
        }

        private const string TourAudioFolder = "Assets/Audio/Tour";

        // The tour's caption card (36 x 16 cm): step count, two lines of text, Skip and Next, and two
        // tooltip tails (a card-coloured diamond half hidden behind the face) toggled by where the
        // target is. The halo is a rounded green plate the tour sizes to its target at runtime.
        private static void CreateTour(Tour tour, WordWorldTheme theme)
        {
            GameObject card = CreateCard(tour.transform, "Tour Card", theme, new Vector2(0.36f, 0.16f));
            Transform t = card.transform;
            SpatialAdapterNativeText counter = CreateChildText(t, "Step", new Vector3(-0.12f, 0.058f, -0.005f), "1 / 9",
                18, theme.InkMuted, Color.clear, new Vector2(0.1f, 0.025f), 0f, TextJustification.Left);
            // 28 pt authored is 42 pt drawn (Tour.m_bodyPoints); the box matches Tour.m_bodyBox.
            SpatialAdapterNativeText body = CreateChildText(t, "Body", new Vector3(0f, 0.005f, -0.005f), "tour.welcome",
                28, theme.Ink, Color.clear, new Vector2(0.33f, 0.07f), 0f);
            SpatialButton skip = CreateButton(t, "Skip", new Vector3(-0.1f, -0.052f, -0.01f), "skip", "tour.skip", 22, theme,
                new Vector2(0.12f, 0.04f));
            SpatialButton next = CreateButton(t, "Next", new Vector3(0.1f, -0.052f, -0.01f), "next", "tour.next", 26, theme,
                new Vector2(0.14f, 0.04f));
            GameObject tailLeft = Plate(t, "Tail Left", new Vector3(-0.18f, 0f, 0.001f), new Vector2(0.022f, 0.022f), theme.Card, 0.002f);
            tailLeft.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            GameObject tailDown = Plate(t, "Tail Down", new Vector3(0f, -0.08f, 0.001f), new Vector2(0.022f, 0.022f), theme.Card, 0.002f);
            tailDown.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);

            var halo = new GameObject("Tour Halo");
            halo.transform.SetParent(tour.transform, false);
            var glow = new Color(theme.Accent.r, theme.Accent.g, theme.Accent.b, 0.85f);
            SpatialAdapterNativeText haloText = CreateNativeText(halo, "", 10, TextJustification.Center, glow, glow,
                new Vector2(0.1f, 0.05f), 0.014f);
            AddSyncCollider(halo, new Vector3(0.001f, 0.001f, 0.001f));

            Set(tour, "m_card", card);
            Set(tour, "m_counter", counter);
            Set(tour, "m_body", body);
            Set(tour, "m_next", next);
            Set(tour, "m_skip", skip);
            Set(tour, "m_tailLeft", tailLeft);
            Set(tour, "m_tailDown", tailDown);
            Set(tour, "m_halo", halo);
            Set(tour, "m_haloText", haloText);
            SetArray(tour, "m_clipsEn", TourClips("en"));
            SetArray(tour, "m_clipsZh", TourClips("zh"));
        }

        // tools/generate_tour_audio.py writes one clip per step; a missing one leaves that step silent.
        private static Object[] TourClips(string language)
        {
            var clips = new Object[Tour.Steps.Length];
            for (int i = 0; i < clips.Length; i++)
            {
                clips[i] = AssetDatabase.LoadAssetAtPath<AudioClip>($"{TourAudioFolder}/{language}/{Tour.Steps[i]}.ogg");
                if (clips[i] == null) Debug.LogWarning($"[WordWorld] no tour narration {language}/{Tour.Steps[i]}");
            }
            return clips;
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
                theme.Button, size, theme.LabelCornerRadius);
            GameObject go = text.gameObject;
            go.AddComponent<BoxCollider>().size = new Vector3(size.x, size.y, 0.02f);
            go.AddComponent<SpatialAdapterHoverEffect>();
            // The hover ring: 6 mm wider on every side, just behind the button, invisible until hovered
            // (4 mm was still hard to see past a gaze, Nov 23 review).
            SpatialAdapterNativeText ring = CreateChildText(go.transform, "Hover Ring", new Vector3(0f, 0f, 0.003f), "", 10,
                Color.clear, Color.clear, size + new Vector2(0.012f, 0.012f), theme.LabelCornerRadius + 0.006f);
            var button = go.AddComponent<SpatialButton>();
            // Same scaling CreateNativeText applied, so runtime fitting starts from the drawn size.
            button.Bind(value, text, points > 12 ? Mathf.RoundToInt(points * WindowTextScale) : points, size,
                theme.Button, theme.Accent, ring);
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
            if (background.a <= 0f) background = s_theme != null ? s_theme.Card : Color.white;
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
            // So runtime strings can be wrapped and shrunk into this box (TextBoxExtensions.SetFitted).
            go.AddComponent<TextBox>().Bind(points, backgroundSize);
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
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
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
            // Hand tracking on, for the finger ray (FingerRay reads the hands' aim and joints).
            if (!config.handTracking)
            {
                config.handTracking = true;
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
