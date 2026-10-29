# SPATIALML_SETUP.md: Word World

Setup notes for the SpatialML (SecureMR) part of Word World, based on guidance from PICO's SpatialML team (October 27, 2026).

**For the AI agent:** read this file before doing any SpatialML, SecureMR, room mesh, detection, text-to-speech or speech-to-text work. Where this file and `CLAUDE.md` disagree, this file wins. Do not guess package names, file paths or versions. If something listed here can't be found in the project, stop and ask Erin.

---

## 1. How this project is built

- **Base project:** Word World is built on top of a **Unity project provided by PICO's SpatialML team**. That project is the starting point. Word World's features are plugged into it.
- **Spatial mode from the start:** the app runs in PICO's **spatial mode** through the **spatial adapter**, which is what PICO wants apps to use. Do not build a full stage mode version.
- **Keep the base project's setup:** reuse its SpatialML, spatial adapter, Spatial SDK and XR configuration. Add Word World on top. Don't rebuild or replace that setup unless Erin approves.
- **No Palette Room code.** Ignore Palette Room entirely.

---

## 2. Summary of PICO's guidance

| # | Guidance | What it means for this project |
|---|---|---|
| 1 | **SpatialML is the same thing as SecureMR.** | Treat the names as interchangeable in docs, code and APIs. |
| 2 | **Map the 3D scene first, then use SpatialML to label detected objects.** | The room mesh is the foundation. Detections are placed in the room by intersecting with that mesh. |
| 3 | **The room mesh is important.** | Use the room mesh (spatial mesh) as the surface that detections are projected onto. |
| 4 | **Use spatial mode via the spatial adapter.** | The app runs in spatial mode from day one. |
| 5 | **Use the Spatial SDK to integrate with Unity.** | The Spatial SDK connects the room mesh and spatial features to the Unity project. |
| 6 | **Start SpatialML in a 0-size container, then read back from it.** | The SpatialML pipeline runs inside an invisible, zero-size container, and the app reads the results back and draws the labels itself. Follow how the base project does this. |
| 7 | **Add object detection and the room mesh to the base project.** | Object recognition (a YOLO model) replaces any placeholder detection model (such as face detection) in the base project. Face detection does not run in the final app. |
| 8 | **TFLite models can be used.** | The detector (and any later model) should be in TFLite/LiteRT format. |
| 9 | **Ray intersection decides where the anchor goes.** | Cast a ray from the camera through the detected object's 2D position; where it hits the room mesh is where the label's anchor goes. See section 5. |
| 10 | **Meta's Boxer model lifts 2D detections into 3D, but may be too big.** | Don't use it. Use the ray-plus-room-mesh method. |
| 11 | **The Android Studio Spatial SDK project has the room mesh, spatial parts and object recognition SDK.** | Reference for porting into Unity. Testing the room mesh in Android Studio first is **optional**. |
| 12 | **Use adb to connect Claude to the headset.** | The agent can install builds and read logs through adb. See section 7. |
| 13 | **Text-to-speech: Android's built-in TTS or Piper (open source).** | Pronunciation audio is allowed. See section 8. |
| 14 | **Speech-to-text: possible, but it can't listen while the app is talking. Use prompted speech.** | Optional stretch goal, turn-based only. See section 9. |
| 15 | **Don't do translation, because the model is too big.** | No translation model. All text is written ahead of time. |

---

## 3. Sources

Local paths on Erin's Windows computer:

| Source | Path |
|---|---|
| **A** (the Word World project) | `C:\Users\Erin Mitt\Documents\GitHub\SpatialML_Words` |
| **B** (reference only) | `C:\Users\Erin Mitt\Downloads\yolo-mesh-sample-buildable-2026-10-28` |

**`SpatialML_Words` is the Word World working project.** It's Erin's copy of PICO's SpatialML Unity samples, so build Word World directly in it. **Source B is read-only:** copy files out of it into `SpatialML_Words`, but never edit it. Paths contain a space ("Erin Mitt"), so always quote them in terminal commands.

Record each source's name, repo URL (if any), version or commit, and local path in `docs/dependencies.md`.

| Source | Format | What to take from it |
|---|---|---|
| **A. Base Unity project from PICO's SpatialML team** (`SpatialML_Words`) | Unity project | **The project Word World is built in.** Spatial mode via the spatial adapter, SpatialML setup (0-size container + readback), and Spatial SDK integration. Any placeholder model in it (such as face detection) gets replaced by object recognition. |
| **B. YOLO + room mesh sample (Android Studio)** (`yolo-mesh-sample-buildable-2026-10-28`) | Android/C++ project | Reference to port from: (1) the **room mesh and spatial parts**: room mesh, camera-to-world projection, coordinate conversion; and (2) the **object recognition SDK**: a YOLO TFLite model with its pipeline JSON, preprocessing, postprocessing and class list. Only port what source A doesn't already provide. |

Docs and resources from PICO may be shared through **Lark** (PICO's document platform). If a link needs a login, ask Erin to open it with her Lark account and share the file.

If a source is missing or ambiguous, stop and ask Erin. Don't substitute a different repo.

---

## 4. Target architecture

```
[1. Room mesh]                ← from source A if present, otherwise ported from source B
  the app builds/loads a 3D mesh of the room
      │
[2. SpatialML pipeline]       ← runs in a 0-size container, the way source A sets it up
  camera (never exposed) → preprocess → YOLO TFLite inference → postprocess
      │  2D boxes + class + confidence
      ▼
[3. Readback into C#]         ← same mechanism source A uses
      │
      ▼
[4. Ray intersection]         ← section 5
  ray from camera through the box center → hit point on the room mesh = anchor position
      │
      ▼
[5. TrackingManager.cs]       ← CLAUDE.md section 2: confirmation, smoothing, anchors
      │
      ▼
[6. Labels / Quiz / Panels]   ← rendered by the app in spatial mode, through the spatial adapter
```

**Privacy rule (do not break):** the app only receives labels, confidences and positions. It never reads, saves or sends camera frames.

---

## 5. Placing labels: ray intersection with the room mesh

For each confirmed detection:
1. Take the 2D box center (or a point slightly below center, which lands on the object rather than behind it).
2. Using the camera's intrinsics and pose at the moment the frame was captured (not the current frame), build a ray in world space.
3. Raycast against the room mesh collider. The first hit is the object's surface position.
4. Place the label anchor at the hit point, raised slightly so the label sits just above the object.
5. If the ray misses the mesh, skip that detection for this frame. Don't fall back to a guessed distance unless Erin approves it.

Use the camera pose matching the frame's timestamp. Using the current head pose makes labels drift when the head moves.

---

## 6. Setup steps (in order; don't skip the checkpoints)

### Step 0: Inventory
- Work in `SpatialML_Words` (see section 3). Before changing anything: if it's a git repo, create a new branch `wordworld`; if not, run `git init` and commit the untouched project first.
- List what the project already includes: spatial mode/spatial adapter setup, SpatialML pipeline and readback, Spatial SDK, room mesh, any placeholder model, panels/windows. If it contains several samples, list each one and ask Erin which sample scene to build from.
- Inspect source B (read only) and fill in `docs/dependencies.md`, including where the YOLO model file, pipeline JSON and room mesh code live.
- Confirm adb can see the headset (section 7).
- **Checkpoint:** report the inventory to Erin, including what's missing from source A that must come from source B.

### Step 1: Run the base project unchanged
- Build and run source A on the headset in spatial mode, **without changing anything**.
- Note how it creates the 0-size SpatialML container and does readback.
- **Checkpoint:** Erin confirms it works on Swan.

### Step 2 (optional): Test the room mesh in Android Studio
- Only if Erin asks, or if the room mesh isn't in source A and porting runs into problems: build source B's room mesh in Android Studio and run it on the headset to confirm it works natively.

### Step 3: Study source B and write porting notes
- Only for what source A doesn't already have. Find the code and pipeline files for:
  - the room mesh (how it's requested, updated and represented)
  - camera intrinsics/extrinsics and coordinate conversion
  - the object recognition SDK: YOLO TFLite model file, pipeline JSON, preprocessing (input size, color order, normalization), postprocessing (box decoding, non-max suppression), class list, YOLO version and license
- Write `docs/porting-notes.md`: for each native piece, note the file, what it does, and its Unity equivalent (through the Spatial SDK for Unity where possible).
- Leave out native app setup (OpenXR session, Vulkan renderer, window code). Unity and source A handle those.
- **Checkpoint:** Erin reviews `docs/porting-notes.md` before porting begins.

### Step 4: Add the room mesh
- If source A doesn't include it, port it from source B. Represent it in Unity as a mesh with a collider (invisible in release, with a debug toggle to show it).
- **Checkpoint:** the room mesh lines up with the real room on the headset.

### Step 5: Plug in object recognition
- Put the YOLO TFLite pipeline into the base project's SpatialML setup, using its 0-size container, loader and readback pattern, replacing any placeholder model.
- Remove placeholder models (such as face detection) from the build once object detection works. They must not run in the final app.
- Put the model behind an interface (`IObjectDetector`) so it can be swapped later.
- **Checkpoint:** readback logs show correct class names and confidences for real objects.

### Step 6: Ray intersection and tracking
- Implement section 5, then CLAUDE.md section 2 (threshold, 5-of-8-frame confirmation, smoothing, merging, spatial anchors).
- Run inference at a fixed 5–10 Hz, not every frame.
- **Checkpoint:** English debug labels sit above real objects (chair, cup, laptop) in the right places and stay put when the head moves.

### Step 7: Word World features
- Continue with CLAUDE.md phases P1 onward, building the labels and four linked panels in spatial mode.

### Step 8: Text-to-speech (after CLAUDE.md phase P2 works)
See section 8.

### Step 9: Speech-to-text (stretch goal)
See section 9.

---

## 7. Connecting to the headset with adb

The agent can use adb to install builds, read logs and check the device. Erin must first enable developer mode on the headset and connect it by USB (or wireless adb).

Common commands:
```
adb devices                                  # confirm the headset is connected
adb install -r <path-to-apk>                 # install or update the build
adb logcat -s Unity SecureMR <AppTag>        # filtered logs (adjust tags to what the project uses)
adb shell am start -n <package>/<activity>   # launch the app
```

Rules:
- Ask Erin before running `adb uninstall`, clearing app data, or changing device settings.
- Give Word World its own package name, different from the base project's original name, so builds don't overwrite PICO's sample on the headset.
- When a step can only be verified by looking through the headset, say so and ask Erin to check.

---

## 8. Known gotchas

- **Coordinate systems:** native/OpenXR code is usually right-handed (−Z forward); Unity is left-handed (+Z forward). Converting positions usually means negating Z, and rotations need matching changes. Wrong signs put labels behind or mirrored from objects.
- **Camera pose timing:** use the camera pose from when the frame was captured, not the current pose.
- **Preprocessing must match the model exactly:** RGB vs BGR, normalization range, letterbox vs stretch, input resolution. A mismatch causes low-confidence results.
- **Model license:** check the YOLO model's license. If it's AGPL (common for Ultralytics YOLO), flag it to Erin, since the repo will be open-sourced and may go to the store.
- **Spatial mode only:** don't add a full stage mode path.
- **Test device:** all on-device testing happens on **Swan** (Project Swan, the PICO Space Pro).

---

## 9. Text-to-speech

Two allowed options:
1. **Piper, pre-generated (recommended):** the vocabulary is fixed and there's no runtime translation, so every word, phrase and sentence can be generated once on a computer with a script (`tools/generate_audio.py`) and shipped as audio clips in `Assets/Audio/Vocab/<lang>/<class>_<level>.ogg`. No model on the headset and the same audio every time.
2. **Android's built-in text-to-speech:** no extra files, but voice availability and quality depend on the headset's OS. Check which languages Swan supports (Mandarin, Spanish, French, German, English) before relying on it.

Either way:
- Play audio from the object's position.
- Audio plays only when the user taps the speaker icon on the detail card; reading stays the main focus.
- Add an "Audio on/off" setting.
- For Piper: record each voice's name and license in `docs/dependencies.md` (each voice has its own license), and have a native speaker check Mandarin tones.

---

## 10. Speech-to-text (optional stretch goal)

**Use case:** "Say it" practice. The app prompts the user to read a label aloud and checks whether they said the expected word.

**Prompted, turn-based only (the headset can't listen while it's talking):**
1. The app shows the prompt (and plays TTS, if on).
2. Wait until any audio has finished.
3. Show a clear "Listening..." indicator and record for a short, fixed window.
4. Stop listening, then check the result.
Never record while TTS is playing.

**Approach**
- Use a small on-device model (TFLite if possible). Compare only to the expected word with fuzzy matching; for Mandarin, compare pinyin.
- Request microphone permission only when "Say it" starts. Never save or send audio.
- If accuracy is poor for a language, hide "Say it" for that language.
- Don't start until CLAUDE.md phase P2 and TTS work.

---

## 11. No translation

- No machine translation model, on device or in the cloud.
- All learner-facing text is written ahead of time in `Assets/Data/vocabulary.json` and `Assets/Data/ui_strings.json` and checked by native speakers (`"reviewed": true`).

---

## 12. Open questions

1. If source A has several samples, which one to build from (ask in step 0).
2. Which YOLO version is in source B, and its license (read it from the package).
3. What the base project (source A) already includes (answered in step 0).

Settled:
- Word World is built on the Unity project from PICO's SpatialML team, in spatial mode via the spatial adapter, from the start.
- The object recognition SDK and room mesh reference come from the Android Studio Spatial SDK project; testing it in Android Studio is optional.
- Placeholder models such as face detection don't run in the final app.
- SpatialML runs in a 0-size container with readback; labels are rendered by the app.
- Labels are placed by ray intersection with the room mesh, not Meta's Boxer model.
- Models are TFLite. The detector is YOLO.
- The test device is Swan (PICO Space Pro).
- Palette Room is not involved.
