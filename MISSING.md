# Missing from base Unity project

| Date | Missing | Fix | Status |
|---|---|---|---|
| 2026-10-28 | `SDKPackages/`: pico.xr, unitygltf, formats.usd absent; spatialadapter had 241/9204 files | Erin re-copied from PICO's source | ✅ checksums OK |
| 2026-10-28 | pico.xr SpatialAudio: 8 files were git LFS pointers | 6 copied from PICO SDK 6.1.1 (identical hashes) | ⚠️ `libPicoSpatializer.so`, `libPicoAmbisonicDecoder.so` still pointers (unused) |
| 2026-10-28 | Opened on Windows target; Standalone defines XR+MS SDK together → CS0122 in PXR_Loader.cs | Switch to Android, then Configure Spatial | ✅ compiles clean |
| 2026-10-28 | `Assets/Settings/Build Profiles/Android™.asset` overrides scene list with empty list → "No Spatial scenes" build error | Set `m_OverrideGlobalSceneList: 0` | ✅ |
| 2026-10-28 | No room mesh code | Port from source B | ⏳ |
| 2026-10-28 | No `docs/` folder (README links `docs/samples/...`) | n/a | ⏳ |
| 2026-10-28 | No Noto Sans SC font | Add for P1 | ⏳ |
