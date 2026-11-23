# Missing from base Unity project

| Date | Missing | Fix | Status |
|---|---|---|---|
| 2026-10-28 | `SDKPackages/`: pico.xr, unitygltf, formats.usd absent; spatialadapter had 241/9204 files | Erin re-copied from PICO's source | ✅ checksums OK |
| 2026-10-28 | pico.xr SpatialAudio: 8 files were git LFS pointers | 6 copied from PICO SDK 6.1.1 (identical hashes) | ⚠️ `libPicoSpatializer.so`, `libPicoAmbisonicDecoder.so` still pointers (unused) |
| 2026-10-28 | Opened on Windows target; Standalone defines XR+MS SDK together → CS0122 in PXR_Loader.cs | Switch to Android, then Configure Spatial | ✅ compiles clean |
| 2026-10-28 | `Assets/Settings/Build Profiles/Android™.asset` overrides scene list with empty list → "No Spatial scenes" build error | Set `m_OverrideGlobalSceneList: 0` | ✅ |
| 2026-10-28 | No room mesh code | Ported from B (`Assets/WordWorld/Spatial`) | ✅ |
| 2026-10-28 | No `docs/` folder (README links `docs/samples/...`) | n/a | ⏳ |
| 2026-10-28 | No Noto Sans SC font | Add for P1 | ⏳ |
| 2026-10-29 | Room mesh in Spatial mode: PXR_Loader only makes "PICO Mesh" with AR Foundation 6 (not installed); manual `Create()` returns null | Ask PICO | ⏳ |
| 2026-10-29 | YOLOX-M fails on Swan GPU ("LiteRt create input buffer failed"); NPU broken | GPU works after a reboot (~7.4 Hz). A failed or earlier GPU session breaks it until reboot (tell PICO). NPU still broken | ✅ GPU / ⚠️ service bug |
| 2026-10-29 | TextMeshPro renders as blocks in Spatial mode | Labels and panel use `SpatialAdapterNativeText`; Chinese rendering still to check for P1 | ⚠️ needs visual check |
| 2026-10-29 | Spatial Adapter manifest used the Constrained camera config (2.5 m volume) while the runtime camera is Unconstrained, so content was boxed in a window instead of the room | `SpatialAdapterRuntimeSettings` now points at "Default Unconstrained Spatial Camera Configuration" (stage, like the Android sample) | ✅ stage on Swan; labels need a visual check |
| 2026-10-29 | Room mesh: two "PICO Mesh" descriptors (PxrPlatform, not loaded in Spatial mode, and StagePlatform) | RoomMesh tries each; StagePlatform's creates and reaches RUNNING, but no mesh data arrives (no MESH_UPDATE events, TryGetMeshInfos false) | ⚠️ ask PICO |
| 2026-11-16 | Spatial mode draws no hand ray (system targets with eye gaze); hand tracking off in PXR_ProjectSetting; `PXR_HandTracking` is compiled out on Android (`ENABLE_PICO_XR_SDK` not defined) | Builder turns `handTracking` on; `FingerRay` calls `PXR_Plugin.HandTracking` directly and draws its own ray | ⚠️ needs on-device check that hand data reaches a spatial app |
| 2026-11-16 | PICO "Back to Home" only stops the activity; the process stays alive, so re-opening shows the old session with recognition dead (camera feed does not resume after backgrounding) | `AppLifecycle` ends the process when the app is paused with the screen on (sleep and permission prompts keep the session) | ⚠️ needs on-device check |
