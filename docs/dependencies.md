# Dependencies

## Sources

| Source | Repo / origin | Version | Local path | License |
|---|---|---|---|---|
| A. PICO SpatialML Unity samples (base project) | Provided by PICO's SpatialML team (private repo; older public mirror: github.com/Pico-Developer/SpatialML-Unity-Samples, `prerelease`) | Snapshot received 2026-10-28; baseline commit `b1b6fc3` | `C:\Users\Erin Mitt\Documents\GitHub\SpatialML_Words` | Apache-2.0 (`LICENSE`) |
| B. YOLO + room mesh sample (Android Studio, reference only) | Provided by PICO, `yolo-mesh-sample-buildable-2026-10-28.zip` | Spatial SDK `0.99.99-SNAPSHOT` | `C:\Users\Erin Mitt\Downloads\yolo-mesh-sample-buildable-2026-10-28` | Proprietary PICO notice on Kotlin sources. **PICO gave verbal permission to port it into Word World (2026-10-29); get it in writing before release.** |

## Unity packages (`SDKPackages/`, verified against `SHA256SUMS`)

| Package | Version | Notes |
|---|---|---|
| `com.bytedance.pico.spatialadapter` | 6.2.0 | |
| `com.bytedance.pico.xr` | 6.2.0 | `SpatialAudio` `libPicoSpatializer.so` / `libPicoAmbisonicDecoder.so` are LFS pointers (unused) |
| `com.plattar.unitygltf` | 1.142.0 | |
| `com.unity.formats.usd` | 3.0.0-exp.5 | |

Unity editor: project made with 6000.3.13f1; currently opened with 6000.3.15f1.

## Detection model (from source B)

| Item | Location in B | Notes |
|---|---|---|
| YOLOX-M model | `app/src/main/assets/SpatialML/yolo-detection-pipeline/model/yolox_m.tflite` | 51 MB, NPU, input 1×640×640×3 FP32, output 1×8400×85. YOLOX (Megvii) is Apache-2.0; confirm PICO's conversion carries no extra terms |
| Postprocess model | `.../model/yolox_postprocess.tflite` | CPU, top-128 candidates |
| Pipeline JSON | `.../pipeline/inference.json` | Includes NMS and its own 2D→3D projection |
| Pipeline manifest | `.../manifest.json` | id `yolo_detection_spatial`, spatial mode only |
| Class list | `.../labels.json` | 80 COCO classes |

## Room mesh (from source B)

| Item | Location in B |
|---|---|
| Mesh collection (`MeshTrackingManager`, `MeshAnchor`) | `app/src/main/java/.../yolodetectionpipeline/YoloRoomScan.kt` |
| Mesh raycast (BVH, ray-triangle) | `.../YoloMeshSurface.kt` |
| Multi-view ray localization | `.../YoloRayLocalization.kt` |
| Capture-time head pose | `.../YoloHeadPoseHistory.kt` |

## Fonts, voices

| Item | Source | License |
|---|---|---|
| Inter (base project) | `Assets/Fonts/Spatial` | OFL (`Inter-LICENSE.txt`) |
| Noto Sans SC | not yet added | OFL |
| Piper voices | see below | per voice |

## Pronunciation voices (Piper, used on a PC by `tools/generate_audio.py`; only the generated clips ship)

Piper itself (`piper-tts` 1.8.0) is GPL-3.0 and is not shipped. Licenses below are copied from each voice's `MODEL_CARD` on https://huggingface.co/rhasspy/piper-voices (checked 2026-11-04).

| Language | Voice | Dataset license | Notes |
|---|---|---|---|
| Mandarin | `zh_CN-chaowen-medium` | CC0 | Fine-tuned from `zh_CN-xiao_ya-medium`, whose dataset (Data Baker BZNSYP) is non-commercial only. Kept because Word World is free and non-commercial (Erin, 2026-11-04); revisit if that changes. The other Mandarin voices are no better: `xiao_ya` is non-commercial and `huayan`'s license is "Unknown". Clips are spoken from the sheet's reviewed pinyin, not Piper's own guess. |
| Spanish (Mexico) | `es_MX-claude-high` | Apache-2.0 | Alternative: `es_MX-ald-medium` (Unlicense) |
| French | `fr_FR-siwis-medium` | CC-BY 4.0 | **Attribution required:** SIWIS French Speech Synthesis Database, University of Edinburgh. Credit it in the app's about text and README. |
| German | `de_DE-thorsten-medium` | CC0 | Thorsten-Voice |
| English | `en_US-ljspeech-medium` | Public domain | LJ Speech dataset |

## Speech recognition (Say It)

| Item | Version | License | Where |
|---|---|---|---|
| Vosk Android library | `com.alphacephei:vosk-android` 0.3.75 | Apache-2.0 | `Assets/Plugins/Android/vosk-android.aar` (arm64 only) |
| JNA | `net.java.dev.jna:jna` 5.18.1 | Apache-2.0 or LGPL-2.1 | `Assets/Plugins/Android/jna.aar` (arm64 only) |
| Vosk models | `vosk-model-small-cn-0.22`, `-es-0.42`, `-fr-0.22`, `-de-0.15`, `-en-us-0.15` | Apache-2.0 (alphacephei.com/vosk/models) | `Assets/StreamingAssets/vosk/<lang>.zip`, not committed; `py -3 tools/fetch_speech.py` |
| Vosk Python package (tools only) | 0.3.45 | Apache-2.0 | used by `tools/build_speech.py`, `tools/test_speech.py` |

Rejected voices: `fr_FR-tom-medium` (AGPLv3), `en_US-hfc_female-medium` (CC BY-NC-SA), `en_US-lessac-medium` (custom Blizzard 2013 dataset license; public-domain voices exist, so not pursued).
