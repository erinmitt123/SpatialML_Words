# SpatialML-Unity-Samples

This Unity project demonstrates PICO SpatialML camera access, on-device machine
learning, 2D-to-3D mapping, and rendering. It builds two Android apps from one
source tree: **XR Samples** and **Spatial Samples**. Each app opens its own sample
selector and provides Back navigation between samples and the list.

## Requirements

- Unity **6000.3.13f1** with Android Build Support, Android SDK/NDK tools, and OpenJDK.
- A SpatialML-capable PICO headset and a compatible OpenMR/SpatialML service.
- PICO Unity SDK **6.2.0 or newer**, including XR and Spatial Adapter, from the
  bundled folders in [SDKPackages](SDKPackages/README.md). The upstream SDK is
  maintained in the [PICO Unity SDK repository](https://github.com/Pico-Developer/PICO-Unity-SDK).
  The Spatial examples require local-transform APIs, and Full Space face tracking
  also uses `ReadbackIntoAsync`.

[Packages/manifest.json](Packages/manifest.json) declares the main PICO XR SDK,
Spatial Adapter, and its required glTF exporter and USD dependencies using
repository-relative paths to the bundled package folders. A separate SDK checkout
is not required. Both sample apps use the main SDK's
`PXR_Loader`; the separate `com.bytedance.pico.spatialadapter.xr` bridge is not
required.

Resolve the package dependencies and wait for Unity to finish importing and
compiling before configuring either app. Standard Unity dependencies are resolved
through Unity Package Manager's registry as usual. The bundled SDK, exporter,
and USD folders preserve the fixes used by these samples; see the
[package inventory and checksums](SDKPackages/README.md).

The scenes, fonts, materials, imported pipeline packages, and models are included
in the project. Ordinary builds use those prepared assets.

## The two apps

| App | Application ID | Default APK | Startup scene |
| --- | --- | --- | --- |
| XR Samples | `com.pico.spatialml.samples` | `Builds/XR/SpatialML-Samples-XR.apk` | `Assets/Scenes/SceneLoader.unity` |
| Spatial Samples | `com.pico.spatialml.samples.spatial` | `Builds/Spatial/SpatialML-Samples-Spatial.apk` | `Assets/Scenes/Spatial/SpatialSceneLoader.unity` |

The distinct application IDs allow both apps to be installed on the same headset.
XR and Spatial are project-wide build modes: configuring a mode changes the SDK
mode, compiler defines, Android settings, and application identity for the next
build. Switch modes before building the other app.

## Choose which samples to include

Open Unity's global Scene List / Build Settings and add and enable the scenes for
the app you want to build. Include its startup selector and at least one sample
from the tables below. The selector lists the samples actually packaged in that
APK.

This branch enables both selectors, all seven XR samples, and all four Spatial
samples. `Configure XR` and `Configure Spatial` change the build mode; they do
not add scenes to the list.

Both sets can stay enabled together. The build helper filters the list by mode:
scenes under `Assets/Scenes/Spatial/` go into the Spatial APK, and scenes outside
that directory go into the XR APK. It places the matching selector first.

## Build in Unity

Keep the active target on **Android**, then use the two-step workflow for each app:

| App | Configure first | Build after compilation finishes |
| --- | --- | --- |
| XR Samples | **PICO > SpatialML > Build Variants > Configure XR** | **PICO > SpatialML > Build Variants > Build XR APK** |
| Spatial Samples | **PICO > SpatialML > Build Variants > Configure Spatial** | **PICO > SpatialML > Build Variants > Build Spatial APK** |

Wait for the mode change, package import, and script compilation to finish before
selecting Build. The outputs are the APK paths listed above. Use the matching
selector scene to browse the samples in the Editor.

Configuring XR enables the SDK's SecureMR capability, which the XR samples need
to create SpatialML providers. The XR selector enables passthrough. Spatial mode
uses the Spatial Adapter backend instead of the XR SecureMR capability.

For Spatial iteration with Unity open, **Build Spatial Development APK and Run**
in the same menu creates an LZ4 Development APK, installs it on the selected
Android device, and launches it with managed `Debug.Log` output enabled.

## Build from the command line

These examples use a Unix shell. Set `UNITY_EDITOR` to the installed Unity Editor
executable, run from the repository root, and enable the desired scenes in Unity
first. Configuration and build run as separate Unity processes so compilation
can finish after the mode change.

```sh
UNITY_EDITOR="/Applications/Unity/Hub/Editor/6000.3.13f1/Unity.app/Contents/MacOS/Unity"
SAMPLES_PROJECT="$PWD"
mkdir -p "$SAMPLES_PROJECT/Builds/XR" "$SAMPLES_PROJECT/Builds/Spatial"
```

### XR Samples

```sh
"$UNITY_EDITOR" -batchmode -quit -projectPath "$SAMPLES_PROJECT" -buildTarget Android \
  -executeMethod SpatialML.Samples.Editor.SpatialMLBuildVariants.ConfigureXR \
  -logFile "$SAMPLES_PROJECT/Builds/XR/configure.log"

"$UNITY_EDITOR" -batchmode -quit -projectPath "$SAMPLES_PROJECT" -buildTarget Android \
  -executeMethod SpatialML.Samples.Editor.SpatialMLBuildVariants.BuildXR \
  -spatialmlDevelopment \
  -logFile "$SAMPLES_PROJECT/Builds/XR/build.log"
```

### Spatial Samples

```sh
"$UNITY_EDITOR" -batchmode -quit -projectPath "$SAMPLES_PROJECT" -buildTarget Android \
  -executeMethod SpatialML.Samples.Editor.SpatialMLBuildVariants.ConfigureSpatial \
  -logFile "$SAMPLES_PROJECT/Builds/Spatial/configure.log"

"$UNITY_EDITOR" -batchmode -quit -projectPath "$SAMPLES_PROJECT" -buildTarget Android \
  -executeMethod SpatialML.Samples.Editor.SpatialMLBuildVariants.BuildSpatial \
  -spatialmlDevelopment \
  -logFile "$SAMPLES_PROJECT/Builds/Spatial/build.log"
```

Run the build command after configuration exits successfully. Remove
`-spatialmlDevelopment` for a non-Development APK. Add `-spatialmlScriptDebugging`
only when attaching a managed debugger; it requires `-spatialmlDevelopment`.
Use `-spatialmlOutput /path/to/app.apk` to override the default APK path.

Both APKs normally open at their selector. For a direct sample launch, add
`-spatialmlStartScene` with an enabled scene belonging to that build mode. For
example, add this to `BuildSpatial` to start in face tracking:

```text
-spatialmlStartScene Assets/Scenes/Spatial/FaceDetectionFullSpaceSpatial.unity
```

## Install and launch

With `pico-cli` installed, select the intended headset serial and install either
or both APKs:

```sh
PICO_SERIAL="YOUR_HEADSET_SERIAL"

pico-cli app install Builds/XR/SpatialML-Samples-XR.apk \
  --device "$PICO_SERIAL" --replace --grant-permissions
pico-cli app launch com.pico.spatialml.samples --device "$PICO_SERIAL"

pico-cli app install Builds/Spatial/SpatialML-Samples-Spatial.apk \
  --device "$PICO_SERIAL" --replace --grant-permissions
pico-cli app launch com.pico.spatialml.samples.spatial --device "$PICO_SERIAL"
```

Use `--device` to target the intended headset when multiple devices are connected.
Grant the permissions requested by the app;
Full Space face tracking needs camera and spatial-data permissions for normal
readback.

## XR samples

The XR app starts at [SceneLoader.unity](Assets/Scenes/SceneLoader.unity), whose
UI Toolkit selector lists the enabled XR samples. Select a sample and use Back
to return to the list. The source links below show each sample's implementation.

| Sample | What it demonstrates | Scene |
| --- | --- | --- |
| [Minimal App](Assets/Scripts/MinimalApp.cs) | Creates a provider, a global glTF tensor, a pipeline reference, and a transform, then renders a helmet. A small introduction to SpatialML ownership and tensor mapping. | [MinimalApp.unity](Assets/Scenes/MinimalApp.unity) |
| [SpatialML Sample](Assets/Scripts/SpatialMLSample.cs) | Extends the basic glTF example with protected text rendering: a helmet and a “Hello World” texture using `RenderTextOperator`. | [SpatialMLSample.unity](Assets/Scenes/SpatialMLSample.unity) |
| [Color Picker](Assets/Scripts/ColorPicker.cs) | Projects a controller-positioned cube into the VST image, samples the color behind it, and updates the cube's material. Demonstrates camera sampling, color conversion, and glTF updates. | [ColorPickerDemo.unity](Assets/Scenes/ColorPickerDemo.unity) |
| [MNIST Wild](Assets/Scripts/MnistwildDemo.cs) | Crops and normalizes a camera image for a LiteRT handwritten-digit classifier, selects the winning class and score, and displays the crop and results on glTF surfaces. | [MnistwildDemo.unity](Assets/Scenes/MnistwildDemo.unity) |
| [UFO](Assets/Scripts/UFODemo.cs) | Runs LiteRT face detection, projects a detected landmark through depth and the capture-time camera transform, and places a smoothed 3D UFO. Demonstrates acquisition, inference, 2D-to-3D mapping, and rendering across dependent pipelines. | [UFODemo.unity](Assets/Scenes/UFODemo.unity) |
| [Stylize](Assets/Scripts/StylizeDemo.cs) | Runs LiteRT style prediction and transfer on passthrough images and displays stylized portals. Controller triggers cycle the configured styles. | [StylizeDemo.unity](Assets/Scenes/StylizeDemo.unity) |
| [Face Detection — Pipeline Zoo](Assets/Scripts/FaceDetectionPipelineZooDemo.cs) | Loads the imported `face` package through the SDK, executes its detection pipeline, and runs the display pipeline after detection. Demonstrates package loading, shared tensor bindings, and dependent rendering. | [FaceDetectionPipelineZooDemo.unity](Assets/Scenes/FaceDetectionPipelineZooDemo.unity) |

## Spatial samples

The Spatial app starts at
[SpatialSceneLoader.unity](Assets/Scenes/Spatial/SpatialSceneLoader.unity). Its
exported world-space Unity UI Canvas receives Spatial pointer events through a
persistent EventSystem. Select a sample and use the persistent Back control to
return to the list. Each Spatial scene has one Main Camera with a `SpatialCamera`.

| Sample and guide | What it demonstrates | Scene |
| --- | --- | --- |
| [Rotating Helmet](Assets/Scenes/Spatial/README.md#samples) | Loads a glTF helmet in a Spatial container, places it through scene-graph component properties, and updates its rotation each frame. No ML inference is needed. | [RotatingHelmetSpatial.unity](Assets/Scenes/Spatial/RotatingHelmetSpatial.unity) |
| [Stylize Portal](Assets/Scenes/Spatial/README.md#samples) | Runs style prediction and transfer on VST images and updates a panel texture inside a portal Spatial container. The sample UI offers five style previews. | [StylizePortalSpatial.unity](Assets/Scenes/Spatial/StylizePortalSpatial.unity) |
| [Face Tracking — Full Space](docs/samples/full-space-face-tracking.md) | Uses the imported `face-full-space` package for GPU preprocessing, NPU face detection, CPU decoding, and native depth projection. Unity places a four-bar frame using capture-time head poses and smoothing. | [FaceDetectionFullSpaceSpatial.unity](Assets/Scenes/Spatial/FaceDetectionFullSpaceSpatial.unity) |
| [Depth Portal](Assets/Scenes/Spatial/README.md#samples) | Visualizes depth on a rectangular portal panel, mapping 0.25–4.0 metres from red (near) to blue (far). Demonstrates depth access and double-buffered dynamic texture updates without a model. | [DepthPortalSpatial.unity](Assets/Scenes/Spatial/DepthPortalSpatial.unity) |

See the [Spatial scene notes](Assets/Scenes/Spatial/README.md) for rendering and
font requirements, and the [Full Space face-tracking guide](docs/samples/full-space-face-tracking.md)
for its pipeline, packet format, placement settings, and troubleshooting.

## Project layout

```text
Assets/
├── Editor/                 # Variant configuration, builds, and exporter settings
├── Fonts/                  # Prepared Spatial UI fonts
├── Gltf/                   # 3D assets used by the samples
├── MLModels/               # LiteRT/TFLite models and metadata
├── Resources/              # SDK and runtime resources
├── Scenes/                 # XR scenes and selector
│   └── Spatial/            # Spatial scenes and selector
├── Scripts/                # Runtime sample behavior and navigation
│   └── Spatial/            # Spatial runtime sample components
├── SpatialMLPipelineZoo/   # Imported pipeline packages and model assets
├── Settings/               # Rendering and Spatial Adapter settings
└── Textures/               # Style-transfer source textures
Packages/                   # Unity package dependencies
SDKPackages/                # Bundled SDK, exporter, and USD packages
ProjectSettings/            # Unity project configuration
```

Model assets use the `.tflite.bytes` suffix so Unity imports them as `TextAsset`
instances. Pipeline Zoo samples consume the imported `SpatialMLPipelineZooAsset`.

## SDK and SpatialML documentation

See the [PICO Unity SDK import documentation](https://developer.picoxr.com/document/pico-unity-quickstart/import-the-sdk-and-select-a-rendering-mode/)
before changing the SDK package revision, and the
[SpatialML Guide](https://developer.picoxr.com/document/pico-unity-spatialml/)
for the platform APIs and additional examples.

## Credits

Word World is built on the work below. Versions, local paths and notes are in
[docs/dependencies.md](docs/dependencies.md).

### Base project and SDKs

| Dependency | Use | License |
| --- | --- | --- |
| [PICO SpatialML Unity samples](https://github.com/Pico-Developer/SpatialML-Unity-Samples) | Base Unity project, SpatialML and Spatial Adapter setup | Apache-2.0 |
| PICO YOLO + room mesh sample (Android Studio) | Detection pipeline, room mesh and ray localization, ported with PICO's permission | PICO proprietary |
| [PICO Unity Integration SDK](https://github.com/Pico-Developer/PICO-Unity-SDK) (`com.bytedance.pico.xr`, `com.bytedance.pico.spatialadapter` 6.2.0) | XR, passthrough, SecureMR, spatial mode | PICO SDK license |
| [UnityGLTF](https://github.com/KhronosGroup/UnityGLTF) (`com.plattar.unitygltf` 1.142.0) | glTF export for Spatial Adapter | MIT |
| [USD for Unity](https://docs.unity3d.com/Packages/com.unity.formats.usd@latest) (`com.unity.formats.usd` 3.0.0-exp.5) | Spatial Adapter dependency | Unity Companion License |
| Unity packages (URP, Input System, uGUI, Test Framework and others in `Packages/manifest.json`) | Engine features | Unity Companion License |

### Detection

| Dependency | Use | License |
| --- | --- | --- |
| [YOLOX](https://github.com/Megvii-BaseDetection/YOLOX) (YOLOX-M, Megvii), converted by PICO | On-device object detection, 80 COCO classes | Apache-2.0 |
| [COCO](https://cocodataset.org) class names | Vocabulary keys | CC BY 4.0 |

### Fonts

| Dependency | License |
| --- | --- |
| [Inter](https://rsms.me/inter/) by Rasmus Andersson | SIL Open Font License 1.1 ([Inter-LICENSE.txt](Assets/Fonts/Spatial/Inter-LICENSE.txt)) |

### Pronunciation audio

The clips in `Assets/Audio/Vocab` were generated on a PC with
[Piper](https://github.com/OHF-Voice/piper1-gpl) (GPL-3.0, a build tool only; Piper
is not shipped in the app). Voices come from
[rhasspy/piper-voices](https://huggingface.co/rhasspy/piper-voices).

| Language | Voice | License and credit |
| --- | --- | --- |
| Mandarin | `zh_CN-chaowen-medium` | CC0. Fine-tuned from `zh_CN-xiao_ya-medium`, trained on the Data Baker BZNSYP corpus, which is licensed for non-commercial use only. Word World is free and non-commercial. |
| Spanish (Mexico) | `es_MX-claude-high` | Apache-2.0 |
| French | `fr_FR-siwis-medium` | CC BY 4.0. Uses the **SIWIS French Speech Synthesis Database**, University of Edinburgh (Junichi Yamagishi, Pierre-Edouard Honnet, Philip Garner, Alexandros Lazaridis). |
| German | `de_DE-thorsten-medium` | CC0. [Thorsten-Voice](https://www.thorsten-voice.de) by Thorsten Müller |
| English | `en_US-ljspeech-medium` | Public domain. [LJ Speech dataset](https://keithito.com/LJ-Speech-Dataset/) by Keith Ito |

Mandarin phonemes were prepared with [g2pW](https://github.com/GitYCC/g2pW)
(Apache-2.0) and [unicode-rbnf](https://github.com/rhasspy/unicode-rbnf) (MIT),
both build tools only.

### Speech recognition (Say It)

Speech is recognized on the headset. Audio is never saved or sent anywhere.

| Dependency | Use | License |
| --- | --- | --- |
| [Vosk](https://github.com/alphacep/vosk-api) Android library 0.3.75 (Alpha Cephei) | Offline speech recognition | Apache-2.0 |
| [Vosk models](https://alphacephei.com/vosk/models): `small-cn-0.22`, `small-es-0.42`, `small-fr-0.22`, `small-de-0.15`, `small-en-us-0.15` | One model per learning language | Apache-2.0 |
| [JNA](https://github.com/java-native-access/jna) 5.18.1 | Native bridge used by Vosk | Apache-2.0 (dual-licensed with LGPL-2.1) |
| Vosk Python package 0.3.45 | Offline checks in `tools/` only | Apache-2.0 |

## License

See [LICENSE](LICENSE).
