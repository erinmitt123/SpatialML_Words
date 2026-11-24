using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using ByteDance.PICO.SpatialAdapter.Component;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WordWorld.App;
using WordWorld.Labels;
using WordWorld.Spatial;
using WordWorld.Tracking;
using WordWorld.UI;
using Object = UnityEngine.Object;

namespace WordWorld.Editor
{
    /// <summary>
    /// Drives the Word World scene through play mode without a headset and checks the Nov 19 review
    /// items: one language on panels, system language pre-selected, hover ring, example meanings,
    /// label descenders, the movable dock and the reading level hint. Saves screenshots and a
    /// report. Runs from the menu or in batchmode:
    /// Unity -batchmode -projectPath . -executeMethod WordWorld.Editor.PlayModeCheck.Run -logFile check.log
    /// The learner's saved settings and word bank are backed up first and restored afterwards.
    /// </summary>
    [InitializeOnLoad]
    public static class PlayModeCheck
    {
        private const string Flag = "WordWorld.PlayModeCheck";
        private const string ScenePath = "Assets/Scenes/Spatial/WordWorld.unity";
        private static readonly string[] SavedFiles = { "settings.json", "wordbank.json" };

        // Steps, with any wait it is inside on top.
        private static readonly Stack<IEnumerator> s_routine = new Stack<IEnumerator>();
        private static double s_waitUntil;
        private static readonly List<string> s_results = new List<string>();
        private static int s_failures;
        private static string s_outDir;
        private static readonly Pose Head = new Pose(new Vector3(0f, 1.6f, 0f), Quaternion.identity);

        static PlayModeCheck()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("Word World/Run Play Mode Check")]
        public static void Run()
        {
            string dir = Application.persistentDataPath;
            foreach (string file in SavedFiles)
            {
                string path = Path.Combine(dir, file);
                if (File.Exists(path))
                {
                    File.Copy(path, path + ".check-backup", true);
                    File.Delete(path); // a first-time learner
                }
            }
            SessionState.SetBool(Flag, true);
            EditorSceneManager.OpenScene(ScenePath);
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (!SessionState.GetBool(Flag, false)) return;
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                HeadPose.EditorOverride = Head;
                s_outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "PlayModeCheck"));
                Directory.CreateDirectory(s_outDir);
                s_results.Clear();
                s_failures = 0;
                s_routine.Clear();
                s_routine.Push(Steps());
                s_waitUntil = 0;
                EditorApplication.update += Tick;
            }
            else if (change == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.EraseBool(Flag);
                Restore();
                string report = ReadReport();
                Debug.Log($"[WordWorld.Check] finished\n{report}");
                if (Application.isBatchMode) EditorApplication.Exit(report.Contains("FAIL") ? 1 : 0);
            }
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < s_waitUntil) return;
            try
            {
                while (s_routine.Count > 0)
                {
                    IEnumerator top = s_routine.Peek();
                    if (!top.MoveNext())
                    {
                        s_routine.Pop();
                        continue;
                    }
                    if (top.Current is IEnumerator inner) s_routine.Push(inner);
                    else
                    {
                        if (top.Current is float seconds) s_waitUntil = EditorApplication.timeSinceStartup + seconds;
                        return;
                    }
                }
            }
            catch (Exception e)
            {
                Check(false, $"driver threw {e}");
                s_routine.Clear();
            }
            EditorApplication.update -= Tick;
            var report = new StringBuilder();
            foreach (string line in s_results) report.AppendLine(line);
            report.AppendLine(s_failures == 0 ? "ALL PASSED" : $"{s_failures} FAILED");
            File.WriteAllText(Path.Combine(s_outDir, "report.txt"), report.ToString());
            HeadPose.EditorOverride = null;
            LearnerSettings.SystemLanguageOverride = null;
            EditorApplication.ExitPlaymode();
        }

        private static string ReadReport()
        {
            string path = Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "PlayModeCheck")), "report.txt");
            return File.Exists(path) ? File.ReadAllText(path) : "FAIL: no report";
        }

        private static void Restore()
        {
            string dir = Application.persistentDataPath;
            foreach (string file in SavedFiles)
            {
                string path = Path.Combine(dir, file), backup = path + ".check-backup";
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(backup)) File.Move(backup, path);
            }
        }

        // ---- The checks ---------------------------------------------------------------------------

        private static IEnumerator Steps()
        {
            var settings = Find<LearnerSettings>();
            var entry = Find<EntryFlow>();
            var strings = Find<WordWorld.Data.UiStrings>();
            string system = LearnerSettings.SystemLanguage;
            Log($"system language on this machine: {system}");

            // Item 2 + 1: the first question, in the system language, with it pre-selected.
            yield return WaitFor(() => StepOf(entry) == "Speak", 25f, "onboarding reaches 'You speak'");
            yield return 0.5f;
            CheckSpeakStep(entry, strings, system);
            Shot("1_speak_" + system);

            // The other system language too, by re-asking the question.
            string other = system == "zh" ? "en" : "zh";
            LearnerSettings.SystemLanguageOverride = other;
            Call(entry, "ShowLanguageWindow");
            yield return 0.3f;
            CheckSpeakStep(entry, strings, other);
            Shot("1_speak_" + other);
            LearnerSettings.SystemLanguageOverride = null;
            Call(entry, "ShowLanguageWindow");
            yield return 0.3f;

            // Through onboarding: English speaker learning Spanish.
            Click(Button(entry, "m_languageButtons", "en"));
            yield return 0.5f;
            Check(StepOf(entry) == "Learn", "tapping English goes to 'Learn'");
            var esButton = Button(entry, "m_languageButtons", "es");
            Check(TextOf(esButton.Text) == "Spanish", $"Learn step names languages in English only: '{TextOf(esButton.Text)}'");
            CheckNoCjk(Get<GameObject>(entry, "m_languageWindow"), "Learn step");
            Shot("2_learn");
            Click(esButton);
            yield return 0.5f;
            Click(Get<SpatialButton>(entry, "m_startButton"));
            yield return 1.5f;

            // Skip the tour so the rest can be reached.
            var tour = Find<Tour>();
            if (tour != null && tour.Running)
            {
                Click(Get<SpatialButton>(tour, "m_skip"));
                yield return 1f;
            }
            Check(!Tour.Active, "tour skipped");

            // Item 3: hover ring.
            var home = Find<HomeMenu>();
            home.OpenMenu();
            yield return 0.5f;
            SpatialButton hover = home.FindButton("settings");
            Check(hover != null, "menu has a button to hover");
            if (hover != null)
            {
                Vector3 rest = hover.transform.localScale;
                var ring = Get<SpatialAdapterNativeText>(hover, "m_ring");
                Check(ring != null, $"button '{hover.name}' has a hover ring");
                Check(ring == null || ColourOf(ring).a < 0.01f, "hover ring hidden at rest");
                hover.OnPointerEnter(null);
                yield return 0.2f;
                Check(hover.Hovered, "hover registers");
                Check(ring != null && ColourOf(ring).a > 0.9f, $"hover ring shows while hovered ({(ring != null ? ColourOf(ring).ToString() : "none")})");
                Check(hover.transform.localScale.x > rest.x * 1.07f, "hovered button grows 8%");
                Shot("3_hover", hover.transform.position, 0.35f);
                hover.OnPointerExit(null);
                yield return 0.1f;
                Shot("3_hover_off", hover.transform.position, 0.35f);
                Check(ring == null || ColourOf(ring).a < 0.01f, "hover ring hides again");
            }

            // Item 1 + 7: Settings in one language, with the reading level explained.
            home.EnterMode(HomeMenu.Mode.Settings);
            yield return 0.5f;
            var window = Find<SettingsWindow>();
            Check(window.IsOpen, "Settings opens");
            var hint = Get<SpatialAdapterNativeText>(window, "m_levelHint");
            Check(hint != null && Flat(hint) == strings.Get("settings.level_hint", "en"), $"level hint under Reading level: '{TextOf(hint)}'");
            CheckNoCjk(Get<GameObject>(window, "m_window"), "Settings (English interface)");
            Shot("4_settings_es");
            settings.ChooseLanguage("zh");
            yield return 0.3f;
            Check(Flat(hint) == strings.Get("settings.level_hint_zh", "en"), $"Mandarin level hint: '{TextOf(hint)}'");
            Shot("4_settings_zh");
            settings.ChooseLanguage("es");
            window.Close();
            home.EnterMode(HomeMenu.Mode.Explore);
            yield return 0.5f;

            // Item 5: a two-line sentence label with its meaning under it.
            settings.SetReadingLevel(3);
            settings.SetShowMeaning(true);
            var tracking = Find<TrackingManager>();
            var labels = Find<ObjectLabels>();
            TrackedObject cup = tracking.AddVirtual(Array.IndexOf(WordWorld.Detection.CocoClasses.Names, "cup"),
                Head.position + new Vector3(0f, -0.25f, 0.8f), 0.1f, 0.12f);
            LabelView view = null;
            yield return WaitFor(() => labels.TryGetLabel(cup, out view), 10f, "label appears on a virtual cup");
            yield return 1.5f; // fade in
            if (view != null)
            {
                CheckLabel(view);
                Shot("5_label", view.transform.position, 0.45f);
                var ring = Get<SpatialAdapterNativeText>(view, "m_ring");
                var card = Get<SpatialAdapterNativeText>(view, "m_backing");
                var theme = Get<WordWorldTheme>(view, "m_theme");
                Check(ring != null && ColourOf(ring).a < 0.01f, "label hover ring hidden at rest");
                view.OnPointerEnter(null);
                yield return 0.2f;
                Check(ring != null && ColourOf(ring) == theme.Accent && SizeOf(ring).x > SizeOf(card).x + 0.008f,
                    $"hovered label shows an accent ring ({SizeOf(card).x * 100f:F1} cm card, {SizeOf(ring).x * 100f:F1} cm ring)");
                Shot("5_label_hover", view.transform.position, 0.45f);
                view.OnPointerExit(null);
                yield return 0.2f;
                Check(ring != null && ColourOf(ring).a < 0.01f, "label ring goes away after hover");
            }

            // Item 4: the detail card with the example's meaning.
            var detail = Find<DetailCard>();
            if (view != null) detail.Open(cup, view);
            yield return 0.6f;
            string body = TextOf(Get<SpatialAdapterNativeText>(detail, "m_body"));
            Log($"detail body:\n{body}");
            Check(body.Contains(strings.Get("detail.means", "en")), "detail card shows 'Means' for the example");
            Shot("6_detail_es");
            detail.Close();
            settings.ChooseLanguage("zh");
            yield return 0.5f;
            if (labels.TryGetLabel(cup, out view)) detail.Open(cup, view);
            yield return 0.6f;
            body = TextOf(Get<SpatialAdapterNativeText>(detail, "m_body"));
            Log($"detail body (zh):\n{body}");
            Check(body.Contains(strings.Get("detail.means", "en")), "Mandarin detail card shows 'Means'");
            Shot("6_detail_zh");
            detail.Close();
            settings.ChooseLanguage("es");
            yield return 0.3f;

            var dock = Find<StatusChip>();
            if (labels.TryGetLabel(cup, out view)) detail.Open(cup, view);
            yield return 0.6f;
            var notRight = Get<SpatialButton>(detail, "m_notRightButton");
            Check(Flat(notRight.Text) == strings.Get("detail.not_right", "en"), $"'Not right?' now reads '{Flat(notRight.Text)}'");
            Shot("6_detail_wrong_object");
            Click(notRight);
            yield return 0.5f;
            Check(!labels.TryGetLabel(cup, out _), "Wrong object? hides the label");
            Check(Flat(dock.StatusButton.Text) == strings.Get("detail.hidden", "en"), $"dock confirms: '{Flat(dock.StatusButton.Text)}'");
            yield return 4f; // let the message clear

            // Item 6: move the dock.
            yield return WaitFor(() => dock.Dock.gameObject.activeInHierarchy, 5f, "dock shows");
            Check(dock.MoveButton != null, "dock has a Move button");
            Vector3 before = dock.Dock.position;
            Shot("7_dock_before", Head.position + new Vector3(-0.2f, -0.2f, 0.8f), 0.9f);
            Click(dock.MoveButton);
            yield return 0.3f;
            Check(dock.Moving, "Move starts moving");
            Check(Flat(dock.StatusButton.Text) == strings.Get("dock.moving", "en"), $"pill explains moving: '{TextOf(dock.StatusButton.Text)}'");
            Check(TextOf(dock.MoveButton.Text) == strings.Get("dock.done", "en"), "Move becomes Done");
            // Turn the head 25 degrees right and a little down; the dock follows.
            HeadPose.EditorOverride = new Pose(Head.position, Quaternion.Euler(10f, 25f, 0f));
            yield return 1.2f;
            Shot("7_dock_moving", Head.position + Quaternion.Euler(10f, 25f, 0f) * Vector3.forward * 0.85f, 0.9f);
            Click(dock.MoveButton);
            yield return 0.4f;
            Check(!dock.Moving, "Done places the dock");
            bool saved = settings.TryGetDockOffset(out Vector3 offset);
            Check(saved && offset.x > 0.2f, $"new spot saved to the right ({offset})");
            Check((dock.Dock.position - before).magnitude > 0.3f, $"dock moved {(dock.Dock.position - before).magnitude:F2} m");
            HeadPose.EditorOverride = Head;
            yield return 1.5f;
            Shot("7_dock_after", Head.position + new Vector3(0.2f, -0.1f, 0.8f), 0.9f);
            Check(FacingError(dock.Dock) < 3f, $"dock faces the eyes ({FacingError(dock.Dock):F1} deg off)");

            // Step to the side: the dock turns to keep facing the learner.
            HeadPose.EditorOverride = new Pose(Head.position + new Vector3(-0.6f, 0f, 0f), Head.rotation);
            yield return 1.5f;
            Check(FacingError(dock.Dock) < 3f, $"dock turns to face a learner who moved ({FacingError(dock.Dock):F1} deg off)");

            // Open the menu over the dock: it must slide clear and never overlap while turning.
            var occluder = dock.Dock.GetComponent<WordWorld.Spatial.PanelOccluder>();
            home.OpenMenu();
            yield return 0.2f;
            // Overlapping the dock's right half, so there is a clear spot to its left.
            home.Window.transform.SetPositionAndRotation(dock.Dock.position + dock.Dock.right * 0.25f + dock.Dock.forward * 0.05f, dock.Dock.rotation);
            int overlapFrames = 0;
            for (int i = 0; i < 40; i++)
            {
                yield return 0.05f;
                if (i > 10 && occluder.FirstOverlap(HeadPose.EditorOverride.Value.position, 0f) != null) overlapFrames++;
            }
            Check(overlapFrames == 0, $"dock slid clear of the menu and stayed clear while turning ({overlapFrames} overlapping checks)");
            Shot("8_dock_clear_of_menu", home.Window.transform.position, 1.1f);
            HeadPose.EditorOverride = Head;
        }

        private static void CheckSpeakStep(EntryFlow entry, WordWorld.Data.UiStrings strings, string system)
        {
            string ui = system;
            var title = Get<SpatialAdapterNativeText>(entry, "m_languageTitle");
            Check(TextOf(title) == strings.Get("onboarding.speak_title", ui), $"[{system}] question asked in the system language: '{TextOf(title)}'");
            foreach (string code in new[] { "en", "zh" })
            {
                var button = Button(entry, "m_languageButtons", code);
                string name = strings.Get(code == "zh" ? "settings.ui_zh" : "settings.ui_en", ui);
                string expected = code == system ? string.Format(strings.Get("onboarding.system_option", ui), name) : name;
                Check(TextOf(button.Text) == expected, $"[{system}] '{code}' button reads '{TextOf(button.Text)}' (want '{expected}')");
            }
            var mine = Button(entry, "m_languageButtons", system);
            var otherButton = Button(entry, "m_languageButtons", system == "zh" ? "en" : "zh");
            Check(mine.transform.localPosition.y > otherButton.transform.localPosition.y,
                $"[{system}] system language is the top option");
            string speak = Get<string>(entry, "m_speak");
            Check(speak == system, $"[{system}] system language pre-selected (m_speak={speak})");
            GameObject languageWindow = Get<GameObject>(entry, "m_languageWindow");
            if (system == "en") CheckNoCjk(languageWindow, "[en] first question");
            else CheckNoLatinWords(languageWindow, "[zh] first question");
        }

        // Degrees between where the dock faces and the line from the eyes to it.
        private static float FacingError(Transform dock) =>
            Vector3.Angle(dock.forward, dock.position - HeadPose.EditorOverride.Value.position);

        private static void CheckLabel(LabelView view)
        {
            var text = Get<SpatialAdapterNativeText>(view, "m_text");
            var gloss = Get<SpatialAdapterNativeText>(view, "m_gloss");
            Log($"label: '{TextOf(text)}' / '{(gloss != null ? TextOf(gloss) : "")}'");
            Vector2 main = SizeOf(text);
            // The meaning is a child of the word's text, so both are measured in the text's frame.
            float mainBottom = -main.y * 0.5f;
            int lines = TextOf(text).Split('\n').Length;
            // The main box must hold its lines at Noto Sans' 1.36 em line height.
            float em = TextFit.MetresPerPoint * NativeToPoints(text);
            Check(main.y >= lines * em * 1.36f - 1e-4f, $"label box {main.y * 100f:F1} cm holds {lines} line(s) of {em * 100f:F2} cm em");
            if (gloss != null && gloss.gameObject.activeSelf)
            {
                Vector2 g = SizeOf(gloss);
                float glossTop = gloss.transform.localPosition.y + g.y * 0.5f;
                Check(glossTop <= mainBottom + 1e-4f, $"meaning starts below the word (gap {(mainBottom - glossTop) * 1000f:F1} mm), so descenders show");
                Check(gloss.transform.parent == text.transform && gloss.transform.localPosition.z <= 0f,
                    $"meaning sits on or in front of the card (z {gloss.transform.localPosition.z * 1000f:F1} mm)");
            }
        }

        // ---- Helpers ----------------------------------------------------------------------------

        private static IEnumerator WaitFor(Func<bool> condition, float seconds, string what)
        {
            double end = EditorApplication.timeSinceStartup + seconds;
            while (!condition())
            {
                if (EditorApplication.timeSinceStartup > end)
                {
                    Check(false, what + " (timed out)");
                    yield break;
                }
                yield return 0.1f;
            }
            Check(true, what);
        }

        private static void Check(bool ok, string what)
        {
            if (!ok) s_failures++;
            string line = (ok ? "PASS " : "FAIL ") + what;
            s_results.Add(line);
            Debug.Log("[WordWorld.Check] " + line);
        }

        private static void Log(string what)
        {
            s_results.Add("     " + what);
            Debug.Log("[WordWorld.Check] " + what);
        }

        private static void CheckNoCjk(GameObject root, string where)
        {
            foreach (var t in root.GetComponentsInChildren<SpatialAdapterNativeText>())
            {
                string s = TextOf(t);
                foreach (char c in s)
                    if (c >= 0x4E00 && c <= 0x9FFF)
                    {
                        Check(false, $"{where}: Chinese in '{t.name}': '{s}'");
                        return;
                    }
            }
            Check(true, $"{where}: no Chinese mixed in");
        }

        private static void CheckNoLatinWords(GameObject root, string where)
        {
            foreach (var t in root.GetComponentsInChildren<SpatialAdapterNativeText>())
            {
                string s = TextOf(t);
                for (int i = 0, run = 0; i < s.Length; i++)
                {
                    run = s[i] < 0x250 && char.IsLetter(s[i]) ? run + 1 : 0;
                    if (run >= 3)
                    {
                        Check(false, $"{where}: English in '{t.name}': '{s}'");
                        return;
                    }
                }
            }
            Check(true, $"{where}: no English mixed in");
        }

        private static T Find<T>() where T : Object => Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);

        private static object Raw(object o, string field)
        {
            for (Type t = o.GetType(); t != null; t = t.BaseType)
            {
                FieldInfo f = t.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (f != null) return f.GetValue(o);
                PropertyInfo p = t.GetProperty(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (p != null && p.CanRead) return p.GetValue(o);
            }
            return null;
        }

        private static T Get<T>(object o, string field) => o == null ? default : Raw(o, field) is T v ? v : default;

        private static void Call(object o, string method) =>
            o.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Invoke(o, null);

        private static string StepOf(EntryFlow entry) => Raw(entry, "m_step")?.ToString();

        private static SpatialButton Button(object owner, string arrayField, string value)
        {
            if (Raw(owner, arrayField) is Array array)
                foreach (object o in array)
                    if (o is SpatialButton b && b.Value == value) return b;
            Check(false, $"no '{value}' button in {arrayField}");
            return null;
        }

        private static void Click(SpatialButton button)
        {
            if (button == null) return;
            button.OnPointerClick(null);
        }

        // Native text has setters only on device; in the editor its serialized fields hold the values.
        private static string TextOf(SpatialAdapterNativeText t) => t == null ? "" : (Get<string>(t, "_text") ?? Get<string>(t, "Text") ?? "");
        // Wrapped text compared as written: SetFitted breaks long lines.
        private static string Flat(SpatialAdapterNativeText t) => TextOf(t).Replace('\n', ' ');
        private static Color ColourOf(SpatialAdapterNativeText t) => Raw(t, "_backgroundColor") is Color c ? c : Raw(t, "BackgroundColor") is Color d ? d : Color.magenta;
        private static Vector2 SizeOf(SpatialAdapterNativeText t) => Raw(t, "_backgroundSize") is Vector2 v ? v : Raw(t, "BackgroundSize") is Vector2 w ? w : Vector2.zero;

        private static float NativeToPoints(SpatialAdapterNativeText t)
        {
            object size = Raw(t, "_textSize") ?? Raw(t, "TextSize");
            float native = size is int i ? i : size is float f ? f : 0f;
            // TextFit.ToNative is linear; invert it from two samples.
            float a = TextFit.ToNative(0f), b = TextFit.ToNative(100f);
            return Mathf.Approximately(a, b) ? native : (native - a) * 100f / (b - a);
        }

        // Renders the main camera from the learner's head (or looking at a point from a distance).
        private static void Shot(string name, Vector3? lookAt = null, float distance = 0f)
        {
            Camera cam = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
            if (cam == null)
            {
                Log($"no camera for screenshot {name}");
                return;
            }
            Vector3 position = Head.position;
            Quaternion rotation = HeadPose.EditorOverride?.rotation ?? Head.rotation;
            if (lookAt.HasValue)
            {
                Vector3 dir = (lookAt.Value - Head.position).normalized;
                position = lookAt.Value - dir * distance;
                rotation = Quaternion.LookRotation(dir, Vector3.up);
            }
            Vector3 oldPos = cam.transform.position;
            Quaternion oldRot = cam.transform.rotation;
            CameraClearFlags oldFlags = cam.clearFlags;
            Color oldBg = cam.backgroundColor;
            cam.transform.SetPositionAndRotation(position, rotation);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.35f, 0.38f, 0.42f, 1f); // stands in for the room
            var rt = new RenderTexture(1280, 960, 24);
            RenderTexture old = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(s_outDir, name + ".png"), tex.EncodeToPNG());
            RenderTexture.active = null;
            cam.targetTexture = old;
            cam.clearFlags = oldFlags;
            cam.backgroundColor = oldBg;
            cam.transform.SetPositionAndRotation(oldPos, oldRot);
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
        }
    }
}
