using System.Collections.Generic;
using System.Linq;
using SpatialMLPipelineZooAsset = ByteDance.PICO.SecureMR.SpatialMLPipelineZooAsset;
using ByteDance.PICO.SpatialAdapter.Component;
using ByteDance.PICO.XR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
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
        private const int LabelPoolSize = 24;

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
            for (int i = 0; i < LabelPoolSize; i++) pool.Add(CreateLabel(labels.transform, i));
            Set(labels, "m_tracking", tracking);
            SetArray(labels, "m_pool", pool.Cast<Object>().ToArray());

            var panel = new GameObject("Debug").AddComponent<DetectionDebugPanel>();
            panel.transform.SetParent(app.transform, false);
            CreatePanel(panel.transform, out GameObject panelRoot, out SpatialAdapterNativeText panelText);
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

        // Text is drawn natively by the Spatial Engine (see LabelView): TextMeshPro rendered as blocks.
        private static LabelView CreateLabel(Transform parent, int index)
        {
            var go = new GameObject($"Label {index:00}");
            go.transform.SetParent(parent, false);
            SpatialAdapterNativeText text = CreateNativeText(go, "label", 125, TextJustification.Center,
                new Color(0.08f, 0.1f, 0.16f, 0.82f), new Vector2(0.3f, 0.07f));
            var view = go.AddComponent<LabelView>();
            view.Bind(text);
            AddSyncCollider(go, new Vector3(0.3f, 0.07f, 0.01f));
            go.SetActive(false);
            return view;
        }

        private static void CreatePanel(Transform parent, out GameObject root, out SpatialAdapterNativeText text)
        {
            root = new GameObject("Debug Panel");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(-0.45f, 1.3f, 1.1f);
            // 42 pt is about 15 mm tall: comfortable at the panel's 1.1 m, and the longest status
            // line still fits the card.
            text = CreateNativeText(root, "Starting...", 42, TextJustification.Left,
                new Color(0.04f, 0.06f, 0.12f, 0.9f), new Vector2(0.8f, 0.8f));
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
            TextJustification justification, Color background, Vector2 backgroundSize)
        {
            var text = go.AddComponent<SpatialAdapterNativeText>();
            var so = new SerializedObject(text);
            so.FindProperty("_text").stringValue = value;
            so.FindProperty("_textColor").colorValue = Color.white;
            so.FindProperty("_textSize").intValue = points;
            so.FindProperty("_justification").enumValueIndex = (int)justification;
            so.FindProperty("_backgroundColor").colorValue = background;
            so.FindProperty("_backgroundSize").vector2Value = backgroundSize;
            so.FindProperty("_backgroundCornerRadius").floatValue = 0.012f;
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
