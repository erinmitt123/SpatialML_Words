using System.Collections.Generic;
using System.Linq;
using SpatialMLPipelineZooAsset = ByteDance.PICO.SecureMR.SpatialMLPipelineZooAsset;
using ByteDance.PICO.XR;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using WordWorld.DebugTools;
using WordWorld.Detection;
using WordWorld.Labels;
using WordWorld.Spatial;
using WordWorld.Tracking;

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
        private const string FontPath = "Assets/Fonts/SpatialUI/LiberationSans Spatial SDF.asset";
        private const int LabelPoolSize = 24;

        [MenuItem("Word World/Rebuild Word World Scene")]
        public static void BuildScene()
        {
            EnsureProjectSettings();

            var package = AssetDatabase.LoadAssetAtPath<SpatialMLPipelineZooAsset>(PackagePath);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (package == null) throw new System.InvalidOperationException($"Missing {PackagePath}; run tools/spatialml import first.");
            if (font == null) throw new System.InvalidOperationException($"Missing {FontPath}.");

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null) AssetDatabase.DeleteAsset(ScenePath);
            if (!AssetDatabase.CopyAsset(TemplateScenePath, ScenePath))
                throw new System.InvalidOperationException($"Could not copy {TemplateScenePath}.");

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == TemplateSampleObject) Object.DestroyImmediate(root);

            var app = new GameObject("Word World");

            var roomMesh = new GameObject("Room Mesh").AddComponent<RoomMesh>();
            roomMesh.transform.SetParent(app.transform, false);

            var detector = new GameObject("Object Detector").AddComponent<YoloxSpatialMLDetector>();
            detector.transform.SetParent(app.transform, false);

            var tracking = new GameObject("Tracking").AddComponent<TrackingManager>();
            tracking.transform.SetParent(app.transform, false);
            Set(tracking, "m_detector", detector);

            var labels = new GameObject("Labels").AddComponent<DebugLabels>();
            labels.transform.SetParent(app.transform, false);
            var pool = new List<LabelView>(LabelPoolSize);
            for (int i = 0; i < LabelPoolSize; i++) pool.Add(CreateLabel(labels.transform, i, font));
            Set(labels, "m_tracking", tracking);
            SetArray(labels, "m_pool", pool.Cast<Object>().ToArray());

            var panel = new GameObject("Debug").AddComponent<DetectionDebugPanel>();
            panel.transform.SetParent(app.transform, false);
            CreatePanel(panel.transform, font, out GameObject panelRoot, out TextMeshProUGUI panelText);
            Set(panel, "m_panelRoot", panelRoot);
            Set(panel, "m_text", panelText);
            Set(panel, "m_detector", detector);
            Set(panel, "m_tracking", tracking);
            Set(panel, "m_roomMesh", roomMesh);

            EditorSceneManager.MarkSceneDirty(scene);
            // Assigned last: the reference was found cleared when set before the label pool was built.
            Set(detector, "m_package", AssetDatabase.LoadAssetAtPath<SpatialMLPipelineZooAsset>(PackagePath));
            EditorSceneManager.SaveScene(scene);
            AddToBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log($"[WordWorld] built {ScenePath}");
        }

        private static LabelView CreateLabel(Transform parent, int index, TMP_FontAsset font)
        {
            GameObject canvasGo = CreateCanvas($"Label {index:00}", parent, new Vector2(420, 72));
            Image background = CreateImage(canvasGo.transform, new Color(0.08f, 0.1f, 0.16f, 0.82f));
            TextMeshProUGUI text = CreateText(background.transform, "label", font, 44, TextAlignmentOptions.Center);
            text.enableAutoSizing = true;
            text.fontSizeMin = 20;
            text.fontSizeMax = 44;
            var view = canvasGo.AddComponent<LabelView>();
            view.Bind(text, background);
            canvasGo.SetActive(false);
            return view;
        }

        private static void CreatePanel(Transform parent, TMP_FontAsset font, out GameObject root, out TextMeshProUGUI text)
        {
            root = CreateCanvas("Debug Panel", parent, new Vector2(620, 520));
            root.transform.localPosition = new Vector3(-0.45f, 1.3f, 1.1f);
            Image background = CreateImage(root.transform, new Color(0.04f, 0.06f, 0.12f, 0.9f));
            text = CreateText(background.transform, "Starting...", font, 22, TextAlignmentOptions.TopLeft);
            var rect = text.rectTransform;
            rect.offsetMin = new Vector2(20, 20);
            rect.offsetMax = new Vector2(-20, -20);
        }

        // Same setup as the sample's authored scene selector: world-space, 1 px = 1 mm.
        private static GameObject CreateCanvas(string name, Transform parent, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            go.AddComponent<CanvasScaler>();
            go.AddComponent<GraphicRaycaster>();
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one * 0.001f;
            return go;
        }

        private static Image CreateImage(Transform parent, Color color)
        {
            var go = new GameObject("Background", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Stretch((RectTransform)go.transform);
            var image = go.AddComponent<Image>();
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TextMeshProUGUI CreateText(Transform parent, string value, TMP_FontAsset font, float size,
            TextAlignmentOptions alignment)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Stretch((RectTransform)go.transform);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSharedMaterial = font.material;
            text.text = value;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            text.richText = true;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
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
