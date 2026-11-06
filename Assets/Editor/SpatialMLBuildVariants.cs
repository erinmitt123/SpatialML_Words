using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ByteDance.PICO.XR;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.Rendering;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace SpatialML.Samples.Editor
{
    public enum SpatialMLBuildMode
    {
        XR,
        Spatial,
    }

    /// <summary>
    /// Owns the project-wide differences between XR and Spatial builds.
    /// Mode changes intentionally persist so Unity can recompile before a build starts.
    /// </summary>
    public static class SpatialMLBuildVariants
    {
        private const string XrDefine = "ENABLE_PICO_XR_SDK";
        private const string SpatialDefine = "PICO_MS_SDK";
        private const string SpatialAdapterPackageName = "com.bytedance.pico.spatialadapter";
        private const string SpatialAdapterRuntimeSettingsPath = "Assets/Settings/SpatialAdapterRuntimeSettings.asset";
        private const string SpatialSceneRoot = "Assets/Scenes/Spatial/";
        // Word World is the only app this project ships, so Spatial builds contain just this scene
        // and launch straight into it (PICO's sample selector and sample scenes were removed).
        private const string WordWorldScenePath = "Assets/Scenes/Spatial/WordWorld.unity";
        private const string MainManifestPath = "Assets/Plugins/Android/AndroidManifest.xml";
        private const string LauncherManifestPath = "Assets/Plugins/Android/LauncherManifest.xml";
        private const string DefaultOutputRoot = "Builds";
        private const string DefaultProductName = "SpatialML_Samples";
        private const string XrApplicationIdentifier = "com.pico.spatialml.samples";
        // Word World ships as its own app so it never overwrites the PICO samples on the headset.
        private const string SpatialApplicationIdentifier = "com.wordworld.app";
        private const string SpatialProductName = "WordWorld";

        [MenuItem("PICO/SpatialML/Build Variants/Configure XR", priority = 20)]
        public static void ConfigureXR()
        {
            Configure(SpatialMLBuildMode.XR);
        }

        [MenuItem("PICO/SpatialML/Build Variants/Configure Spatial", priority = 21)]
        public static void ConfigureSpatial()
        {
            Configure(SpatialMLBuildMode.Spatial);
        }

        [MenuItem("PICO/SpatialML/Build Variants/Build XR APK", priority = 40)]
        public static void BuildXR()
        {
            Build(SpatialMLBuildMode.XR);
        }

        [MenuItem("PICO/SpatialML/Build Variants/Build Spatial APK", priority = 41)]
        public static void BuildSpatial()
        {
            Build(SpatialMLBuildMode.Spatial);
        }

        [MenuItem("PICO/SpatialML/Build Variants/Build Spatial Development APK and Run", priority = 42)]
        public static void BuildSpatialDevelopmentAndRun()
        {
            Build(SpatialMLBuildMode.Spatial, forceDevelopment: true, autoRun: true);
        }

        public static void Configure(SpatialMLBuildMode mode)
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                throw new BuildFailedException("Wait for Unity to finish compiling and importing before switching build mode.");
            }

            EnsureAndroidTarget();
            ApplyPicoMode(mode);
            ApplyAndroidSettings(mode);
            ApplyModeIdentity(mode);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"Configured SpatialML samples for {mode}. " +
                "If Unity starts compiling, wait for it to finish before entering Play mode or building.");
        }

        public static void Build(SpatialMLBuildMode mode)
        {
            Build(mode, forceDevelopment: false, autoRun: false);
        }

        private static void Build(SpatialMLBuildMode mode, bool forceDevelopment, bool autoRun)
        {
            ValidateConfiguredMode(mode);

            string[] scenes = GetBuildScenes(mode);
            if (scenes.Length == 0)
            {
                throw new BuildFailedException(
                    mode == SpatialMLBuildMode.Spatial
                        ? $"Missing {WordWorldScenePath}. Run Word World > Rebuild Word World Scene."
                        : "No enabled XR scenes were found outside Assets/Scenes/Spatial.");
            }

            string defaultOutput = Path.Combine(DefaultOutputRoot, mode.ToString(), GetDefaultApkName(mode));
            string outputPath = GetCommandLineArgument("-spatialmlOutput") ?? defaultOutput;
            string outputDirectory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            // Opt-in development build (-spatialmlDevelopment) routes managed Debug.Log output to
            // logcat under the "Unity" tag. Script debugging is separately opt-in because it
            // increases build and deployment cost and is only needed when attaching a debugger.
            bool developmentBuild = forceDevelopment || HasCommandLineFlag("-spatialmlDevelopment");
            bool scriptDebugging = HasCommandLineFlag("-spatialmlScriptDebugging");
            if (scriptDebugging && !developmentBuild)
            {
                throw new BuildFailedException(
                    "SpatialML script debugging requires -spatialmlDevelopment.");
            }

            BuildOptions buildOptions = developmentBuild
                ? BuildOptions.Development | BuildOptions.CompressWithLz4
                : BuildOptions.None;
            if (scriptDebugging)
            {
                buildOptions |= BuildOptions.AllowDebugging;
            }
            if (autoRun)
            {
                buildOptions |= BuildOptions.AutoRunPlayer;
            }

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = buildOptions,
            };

            // The Spatial Adapter exporter walks every scene enabled in EditorBuildSettings
            // (not just the scenes handed to BuildPlayer) and assumes that set matches the
            // scenes being built. Leaving the other mode's scenes enabled makes it export
            // unrelated assets (e.g. the XR Interaction Toolkit FresnelHighlight shader graph
            // crashes its MaterialX converter) and its remaining-scene counter never reaches
            // zero, so the exported .glb assets are never copied into StreamingAssets. That
            // produces an APK that crashes natively on launch when the backend cannot find the
            // scene .glb. Narrow the enabled build scenes to this mode's scenes for the build.
            EditorBuildSettingsScene[] originalBuildScenes = EditorBuildSettings.scenes;
            EditorBuildSettings.scenes = scenes
                .Select(path => new EditorBuildSettingsScene(path, true))
                .ToArray();

            try
            {
                BuildReport report = BuildPipeline.BuildPlayer(options);
                if (report.summary.result != BuildResult.Succeeded)
                {
                    throw new BuildFailedException(
                        $"{mode} build failed with result {report.summary.result} and {report.summary.totalErrors} errors.");
                }
            }
            finally
            {
                EditorBuildSettings.scenes = originalBuildScenes;
            }

            Debug.Log($"Built SpatialML {mode} APK: {Path.GetFullPath(outputPath)}");
        }

        public static string[] GetBuildScenes(SpatialMLBuildMode mode)
        {
            if (mode == SpatialMLBuildMode.Spatial)
            {
                // Ignore Build Settings so a stray enabled scene can never put a menu in front of
                // Word World.
                return File.Exists(WordWorldScenePath) ? new[] { WordWorldScenePath } : Array.Empty<string>();
            }

            return EditorBuildSettings.scenes
                .Where(scene => scene.enabled && IsSceneForMode(scene.path, mode))
                .Select(scene => scene.path)
                .ToArray();
        }

        private static bool IsSceneForMode(string scenePath, SpatialMLBuildMode mode)
        {
            bool isSpatialScene = scenePath.Replace('\\', '/').StartsWith(SpatialSceneRoot, StringComparison.Ordinal);
            return mode == SpatialMLBuildMode.Spatial ? isSpatialScene : !isSpatialScene;
        }

        private static string[] ReplaceModeDefine(IEnumerable<string> defines, SpatialMLBuildMode mode)
        {
            var result = new HashSet<string>(defines.Where(define => !string.IsNullOrWhiteSpace(define)), StringComparer.Ordinal);
            result.Remove(XrDefine);
            result.Remove(SpatialDefine);
            result.Add(mode == SpatialMLBuildMode.Spatial ? SpatialDefine : XrDefine);
            return result.OrderBy(define => define, StringComparer.Ordinal).ToArray();
        }

        private static void EnsureAndroidTarget()
        {
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android)
            {
                return;
            }

            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
            {
                throw new BuildFailedException("Failed to switch the active build target to Android.");
            }
        }

        private static void ApplyPicoMode(SpatialMLBuildMode mode)
        {
            PXR_Settings settings = PXR_Settings.GetSettings();
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<PXR_Settings>();
                EditorBuildSettings.AddConfigObject("ByteDance.PICO.XR.Settings", settings, true);
            }

            bool spatial = mode == SpatialMLBuildMode.Spatial;
            settings.appMode = spatial ? PXR_Settings.AppMode.Spatial : PXR_Settings.AppMode.XR;
            EditorUtility.SetDirty(settings);

            PXR_ProjectSetting projectSettings = PXR_ProjectSetting.GetProjectConfig();
            projectSettings.isSpatialAdapter = spatial;
            // XR providers require the SecureMR OpenXR extension. Spatial uses
            // the Spatial Adapter backend instead of that XR capability.
            projectSettings.secureMR = !spatial;
            EditorUtility.SetDirty(projectSettings);
            PXR_ProjectSetting.SaveAssets();

            string[] currentDefines = PlayerSettings
                .GetScriptingDefineSymbols(NamedBuildTarget.Android)
                .Split(';', StringSplitOptions.RemoveEmptyEntries);
            string finalDefines = string.Join(";", ReplaceModeDefine(currentDefines, mode));
            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Android, finalDefines);
            ApplySpatialAdapterRuntimeSetting(spatial);
        }

        private static void ApplySpatialAdapterRuntimeSetting(bool enabled)
        {
            UnityEngine.Object runtimeSettings =
                AssetDatabase.LoadMainAssetAtPath(SpatialAdapterRuntimeSettingsPath);
            if (runtimeSettings == null)
            {
                throw new BuildFailedException(
                    $"Spatial Adapter runtime settings are missing at {SpatialAdapterRuntimeSettingsPath}.");
            }

            var serializedSettings = new SerializedObject(runtimeSettings);
            SerializedProperty enabledProperty = serializedSettings.FindProperty("m_enableSpatialAdapter");
            if (enabledProperty == null)
            {
                throw new BuildFailedException(
                    "Spatial Adapter runtime settings do not expose m_enableSpatialAdapter. Check the installed SDK revision.");
            }

            enabledProperty.boolValue = enabled;
            serializedSettings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(runtimeSettings);
        }

        private static void ApplyAndroidSettings(SpatialMLBuildMode mode)
        {
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            if (mode == SpatialMLBuildMode.Spatial)
            {
                PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
                PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
                PlayerSettings.SplashScreen.show = false;
#if UNITY_6000_0_OR_NEWER
                PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.Activity;
#endif
                CopySpatialManifests();
            }
            else
            {
                PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
                PlayerSettings.SetGraphicsAPIs(
                    BuildTarget.Android,
                    new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 });
                DeleteAssetIfPresent(MainManifestPath);
                DeleteAssetIfPresent(LauncherManifestPath);
                DeleteEmptyManifestDirectories();
                PlayerSettings.SplashScreen.show = true;
            }
        }

        private static void ApplyModeIdentity(SpatialMLBuildMode mode)
        {
            bool spatial = mode == SpatialMLBuildMode.Spatial;
            PlayerSettings.productName = spatial ? SpatialProductName : DefaultProductName;
            PlayerSettings.SetApplicationIdentifier(
                NamedBuildTarget.Android,
                spatial ? SpatialApplicationIdentifier : XrApplicationIdentifier);
        }

        private static void CopySpatialManifests()
        {
            PackageInfo packageInfo = PackageInfo.FindForPackageName(SpatialAdapterPackageName);
            if (packageInfo == null || string.IsNullOrEmpty(packageInfo.resolvedPath))
            {
                throw new BuildFailedException($"Cannot resolve installed package {SpatialAdapterPackageName}.");
            }

            string sourceDirectory = Path.Combine(packageInfo.resolvedPath, "Manifest");
            string destinationDirectory = Path.Combine(Application.dataPath, "Plugins/Android");
            Directory.CreateDirectory(destinationDirectory);

            CopyFile(
                Path.Combine(sourceDirectory, "AndroidManifest.xml"),
                Path.Combine(destinationDirectory, "AndroidManifest.xml"));
            CopyFile(
                Path.Combine(sourceDirectory, "LauncherManifest.xml"),
                Path.Combine(destinationDirectory, "LauncherManifest.xml"));
        }

        private static void CopyFile(string sourcePath, string destinationPath)
        {
            if (!File.Exists(sourcePath))
            {
                throw new BuildFailedException($"Required Spatial Adapter file is missing: {sourcePath}");
            }

            if (File.Exists(destinationPath) && !IsGeneratedSpatialManifest(destinationPath))
            {
                throw new BuildFailedException(
                    $"Refusing to overwrite custom Android manifest {destinationPath}. " +
                    "Merge the Spatial Adapter manifest requirements manually or remove the custom manifest.");
            }

            File.Copy(sourcePath, destinationPath, true);
        }

        private static void DeleteAssetIfPresent(string assetPath)
        {
            if (!File.Exists(assetPath) && !File.Exists($"{assetPath}.meta"))
            {
                return;
            }

            if (File.Exists(assetPath) && !IsGeneratedSpatialManifest(assetPath))
            {
                throw new BuildFailedException(
                    $"Refusing to remove custom Android manifest {assetPath} while switching to XR mode.");
            }

            AssetDatabase.DeleteAsset(assetPath);
        }

        private static void DeleteEmptyManifestDirectories()
        {
            DeleteDirectoryAssetIfEmpty("Assets/Plugins/Android");
            DeleteDirectoryAssetIfEmpty("Assets/Plugins");
        }

        private static void DeleteDirectoryAssetIfEmpty(string assetPath)
        {
            string absolutePath = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
            if (Directory.Exists(absolutePath) && !Directory.EnumerateFileSystemEntries(absolutePath).Any())
            {
                AssetDatabase.DeleteAsset(assetPath);
            }
        }

        private static bool IsGeneratedSpatialManifest(string path)
        {
            string contents = File.ReadAllText(path);
            string fileName = Path.GetFileName(path);
            return fileName == "AndroidManifest.xml"
                ? contents.Contains("com.unity3d.player.SpatialAdapterPlayerActivity")
                : fileName == "LauncherManifest.xml" &&
                  contents.Contains("android.permission.MANAGE_EXTERNAL_STORAGE");
        }

        private static void ValidateConfiguredMode(SpatialMLBuildMode mode)
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                throw new BuildFailedException("Unity is still compiling or importing after a mode change. Retry the build when it is ready.");
            }

            PXR_Settings settings = PXR_Settings.GetSettings();
            PXR_ProjectSetting projectSettings = PXR_ProjectSetting.GetProjectConfig();
            bool expectSpatial = mode == SpatialMLBuildMode.Spatial;
            bool settingsAreSpatial = settings != null && settings.appMode == PXR_Settings.AppMode.Spatial;
            bool defineIsSpatial = HasDefine(SpatialDefine);
            bool defineIsXr = HasDefine(XrDefine);

            if (settingsAreSpatial != expectSpatial || projectSettings.isSpatialAdapter != expectSpatial ||
                defineIsSpatial != expectSpatial || defineIsXr == expectSpatial)
            {
                throw new BuildFailedException(
                    $"Project is not configured for {mode}. Run Configure {mode} first, wait for compilation, then build.");
            }

            if (expectSpatial)
            {
                string absoluteMainManifest = Path.Combine(Directory.GetCurrentDirectory(), MainManifestPath);
                string absoluteLauncherManifest = Path.Combine(Directory.GetCurrentDirectory(), LauncherManifestPath);
                if (!File.Exists(absoluteMainManifest) || !File.Exists(absoluteLauncherManifest))
                {
                    throw new BuildFailedException("Spatial Android manifests are missing. Run Configure Spatial again.");
                }
            }
            else if (!projectSettings.secureMR)
            {
                throw new BuildFailedException(
                    "XR SpatialML samples require SecureMR. Run Configure XR before building.");
            }
        }

        private static bool HasDefine(string define)
        {
            return PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android)
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Contains(define, StringComparer.Ordinal);
        }

        private static string GetDefaultApkName(SpatialMLBuildMode mode)
        {
            return mode == SpatialMLBuildMode.Spatial
                ? "WordWorld.apk"
                : "SpatialML-Samples-XR.apk";
        }

        private static string GetCommandLineArgument(string name)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index < arguments.Length - 1; index++)
            {
                if (string.Equals(arguments[index], name, StringComparison.Ordinal))
                {
                    return arguments[index + 1];
                }
            }

            return null;
        }

        private static bool HasCommandLineFlag(string name)
        {
            return Environment.GetCommandLineArgs().Contains(name, StringComparer.Ordinal);
        }
    }

    /// <summary>
    /// Keeps the Spatial Adapter's default container metadata compatible with
    /// ROMs that require pico.spatial.windowcontainer.id to be a String.
    /// The adapter currently emits the numeric literal 0, which aapt stores as
    /// an Integer and causes the activity to be classified as a 2D app.
    /// </summary>
    internal sealed class SpatialMLSpatialManifestProcessor : IPostGenerateGradleAndroidProject
    {
        private const string NumericContainerId =
            "<meta-data android:name=\"pico.spatial.windowcontainer.id\" android:value=\"0\" />";
        private const string StringContainerId =
            "<meta-data android:name=\"pico.spatial.windowcontainer.id\" android:value=\"WindowGroup\" />";
        private const string SpatialApplicationMetadata =
            "<meta-data android:name=\"pico.spatial.isspatial\" android:value=\"1\" />";
        // Spatial samples read back VST-derived tensors, which the service gates on CAMERA.
        private const string CameraPermission =
            "<uses-permission android:name=\"android.permission.CAMERA\" />";
        private const string SpatialDataPermission =
            "<uses-permission android:name=\"com.picovr.permission.SPATIAL_DATA\" />";

        public int callbackOrder => 100;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string manifestPath = Path.Combine(path, "src/main/AndroidManifest.xml");
            if (!File.Exists(manifestPath))
            {
                return;
            }

            string manifest = File.ReadAllText(manifestPath);
            if (!manifest.Contains("com.unity3d.player.SpatialAdapterPlayerActivity", StringComparison.Ordinal))
            {
                return;
            }

            // Stage mode emits pico.spatial.stage.id and no windowcontainer.id. The permissions
            // below are still required there, so only the container-id fix depends on that key.
            if (manifest.Contains(NumericContainerId, StringComparison.Ordinal))
            {
                manifest = manifest.Replace(NumericContainerId, StringContainerId, StringComparison.Ordinal);
            }
            else if (manifest.Contains("pico.spatial.windowcontainer.id", StringComparison.Ordinal) &&
                     !manifest.Contains(StringContainerId, StringComparison.Ordinal))
            {
                throw new BuildFailedException(
                    "Spatial Adapter emitted an unexpected pico.spatial.windowcontainer.id value. " +
                    "Refusing to build an APK that may be classified as a 2D app.");
            }

            if (!manifest.Contains(SpatialApplicationMetadata, StringComparison.Ordinal))
            {
                const string applicationEnd = "</application>";
                int applicationEndIndex = manifest.LastIndexOf(applicationEnd, StringComparison.Ordinal);
                if (applicationEndIndex < 0)
                {
                    throw new BuildFailedException(
                        "Spatial Android manifest does not contain an application element.");
                }

                manifest = manifest.Insert(
                    applicationEndIndex,
                    $"        {SpatialApplicationMetadata}{Environment.NewLine}    ");
            }

            if (!manifest.Contains("android.permission.CAMERA", StringComparison.Ordinal))
            {
                const string manifestEnd = "</manifest>";
                int manifestEndIndex = manifest.LastIndexOf(manifestEnd, StringComparison.Ordinal);
                if (manifestEndIndex < 0)
                {
                    throw new BuildFailedException(
                        "Spatial Android manifest does not contain a manifest element.");
                }

                manifest = manifest.Insert(
                    manifestEndIndex,
                    $"  {CameraPermission}{Environment.NewLine}");
            }

            if (!manifest.Contains("com.picovr.permission.SPATIAL_DATA", StringComparison.Ordinal))
            {
                const string manifestEnd = "</manifest>";
                int manifestEndIndex = manifest.LastIndexOf(manifestEnd, StringComparison.Ordinal);
                if (manifestEndIndex < 0)
                {
                    throw new BuildFailedException(
                        "Spatial Android manifest does not contain a manifest element.");
                }

                manifest = manifest.Insert(
                    manifestEndIndex,
                    $"  {SpatialDataPermission}{Environment.NewLine}");
            }

            File.WriteAllText(manifestPath, manifest);
        }
    }
}
