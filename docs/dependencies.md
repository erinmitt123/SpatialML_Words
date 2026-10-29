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
| Piper voices | not yet added | per voice |
