# Spatial-mode scenes

This directory contains the prepared Spatial sample scenes. Build them with
**PICO > SpatialML > Build Variants > Build Spatial APK**.

Requires PICO Unity SDK **6.2.0 or newer**, provided with its exporter dependencies
in [SDKPackages](../../../SDKPackages/README.md). See the
[project requirements](../../../README.md#requirements) for package resolution
before building.

The build infrastructure treats enabled scenes under this directory as Spatial scenes. Enabled scenes
outside this directory remain XR scenes. A Spatial APK build fails early until at least one enabled scene
exists here.

Each Spatial scene must contain exactly one active Main Camera with a `SpatialCamera` component.

## Samples

- `SpatialSceneLoader.unity` is the Spatial-mode runtime selector. It lists only enabled scenes in
  this directory, launches the selected sample, and receives Back navigation from those samples.
  Its authored world-space Unity UI Canvas is exported by Spatial Adapter, while its Canvas and
  EventSystem remain alive with the navigation controller during single-scene loads. Keep it enabled
  in Unity Build Settings; Spatial builds place it first automatically.

  The selector uses `TextMeshProUGUI` with the static, readable Alpha8 SDF font in
  `Assets/Fonts/SpatialUI`. The 101-glyph atlas covers printable ASCII, navigation arrows,
  dashes, ellipsis, and the multiplication sign. TMP resources and the Liberation Sans license
  are included under `Assets/TextMesh Pro`.

  The selector labels already reference the included font and material. Keep the atlas in
  Alpha8 SDF format: Spatial
  Adapter converts it to red with its native `1.7` distance adjustment, then swaps TMP materials
  to SpatialEngine's distance-field font material at runtime.

  Reliable build-time font export requires the SDK's exporter to perform that
  same conversion directly on readable Alpha8 pixels instead of a GPU blit.
  The installed exporter must support this conversion to avoid corrupted font
  readback.

  While `StylizePortalSpatial.unity` is active, the selector becomes a two-row, three-column
  texture picker. Its square previews are authored as `Image` sprites so Spatial Adapter embeds
  their textures in the loader scene glTF; do not replace them at runtime with texture lookups.

- `RotatingHelmetSpatial.unity` renders `DamagedHelmet.gltf.bytes` in a Spatial container. It makes
  the scene graph visible once, applies position, rotation, and scale through separate
  `UpdateComponentOperator` properties, then updates only rotation each frame. The position property
  receives Unity local-position components directly; rotation uses `SpatialMLSceneGraphTransform`.

  The rotation path calls
  `SpatialMLSceneGraphTransform.UnityToSpatialEngineLocalTransformRotation`, which was introduced in
  `6.1.0` when the upright reorientation moved out of `UnityToSpatialEngineRotationQuaternion`.

- `StylizePortalSpatial.unity` is the Spatial counterpart of `Assets/Scenes/StylizeDemo.unity`. It runs
  the same LiteRT style prediction and transfer pipeline on the rectified VST feed, then renders the
  stylized image onto a `portal.gltf` panel inside a portal Spatial container. The portal container is
  created with `SpatialContainerConfiguration(..., isPortal: true)`. Because the XR render operators
  (`LoadTexture` / `UpdateGltf` / `SwitchGltfRenderStatus`) are rejected by the Spatial backend, the
  sample renders through the scene graph instead: `ScenegraphVisibilityOperator` plus transform
  `UpdateComponentOperator` properties place the portal once, then each stylized frame is pushed into
  the panel material's base color texture (`SceneGraphProperty.PBRMaterials[1].BaseColorTexture`).
  The persistent Spatial UI presents five square texture previews in a three-column grid; gaze and
  controller selection highlight the focused preview and immediately select that style.

  It relies on the portal
  `SpatialContainerConfiguration` overload and the local-transform rotation helper added in `6.1.0`.

- `FaceDetectionFullSpaceSpatial.unity` appears as **Face Tracking (Full Space)** in the
  Spatial scene selector. It uses an Unconstrained SpatialCamera and the SDK-imported
  `face-full-space` package with one `tracking` pipeline: 338x256 VST, GPU center crop and
  normalization, NPU face inference, CPU decoding, native UV-to-3D projection and exact
  256-byte publication. Unity places and smooths the four-bar frame using capture-time
  head-pose history. No application code constructs pipelines or operators.

  Both this scene and `SpatialSceneLoader.unity` must be enabled in Build
  Settings; normal Spatial builds start at the selector. See
  [the Full Space face tracking guide](../../../docs/samples/full-space-face-tracking.md)
  for build/run instructions, camera geometry and the packet contract.

- `DepthPortalSpatial.unity` is a model-free depth visualization. `DepthOperator` writes its
  single-channel floating-point metre values into a 256x320 tensor (the raw sensor aspect ratio is
  128x160). The graph maps a fixed 0.25-to-4.0-metre range from red (near) to blue (far), preserving
  stable absolute-distance colors from frame to frame. It writes a double-buffered `DynamicTexture`
  onto the full rectangular panel in `depth_frame.gltf.bytes`, so the map is no longer clipped by a
  circular portal mesh. Its portal container is 1500x1200x1000 so the window matches the 5:4 frame.
  It does not load or run any ML model and uses the Spatial scene-graph
  material update path rather than XR-only render operators. The persistent loader canvas supplies
  the Back button when the scene is entered from `SpatialSceneLoader.unity`.
