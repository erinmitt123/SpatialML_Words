# CLAUDE.md: Word World (单词世界)

Read this file at the start of every session. It defines what we are building and the rules for building it.

**Also read `SPATIALML_SETUP.md` before any SpatialML/SecureMR, detection, text-to-speech or speech-to-text work. It has PICO's setup guidance and overrides this file where they conflict.**

## What we're building
**Word World** is a mixed reality reading app for language learners on PICO. On-device object detection (SecureMR) recognizes real objects in the user's room and attaches floating text labels in the target language. Users read labels, tap them to learn more, and take a "Find It" quiz by touching the real object a word names.

One-line pitch: *Learn to read a new language from the objects in your own room.*

- **Engine:** Unity + PICO Unity Integration SDK (latest), SecureMR module
- **Headset:** PICO Space Pro (codename Project Swan). All on-device testing happens on Swan.
- **Mode:** mixed reality passthrough
- **Input:** hand tracking first; controllers as fallback. Every interaction must work with both.
- **Reading is the main focus.** Optional pronunciation audio uses pre-generated Piper TTS clips (see SPATIALML_SETUP.md section 6). Speech-to-text is a stretch goal.
- **Deadline:** November 13, 2026

## Hard requirements (Vibe Lab admission bar)
1. **Multi-window:** at least two linked spatial windows (we use four).
2. **Stability:** no crashes, black screens, lag or motion sickness.
3. **At least two spatial features:** SecureMR detection, MR passthrough, hand interaction, spatial anchors.
4. **10–20 minute session** with a closed loop and no "demo ends here" moments.

## Project origin
Word World is built on top of the Unity project provided by PICO's SpatialML team. It runs in spatial mode via the spatial adapter, from the start. Keep that project's SpatialML, spatial adapter, Spatial SDK and XR setup, and plug Word World's features into it. Do not bring in any Palette Room code. See SPATIALML_SETUP.md for the full setup.

## Architecture

### 1. Detection (SecureMR)
- Follow SPATIALML_SETUP.md: the SpatialML Unity package is the base, and its face detection slot is replaced by the YOLO object recognition SDK from PICO's Android Studio package.
- The app must receive, per detection: **class ID, confidence, and a 3D world position** (projected from the 2D box). Use the SecureMR readback / global tensor mechanism the SDK provides. The app must never read or store raw camera images.
- **Model licensing:** the sample's YOLO model is fine for prototyping, but plan to swap in an Apache-2.0 model (e.g. MediaPipe EfficientDet-Lite, COCO classes) before release. Keep the model behind an interface so the swap is one change.

### 2. Tracking & stability (critical: past projects had low-confidence, flickering results)
- Verify preprocessing matches the model exactly: RGB vs BGR order, input normalization, letterbox padding (not stretching), input resolution.
- **Confidence threshold:** default 0.55, configurable.
- **Multi-frame confirmation:** an object becomes a `TrackedObject` only after it is detected in ≥5 of the last 8 frames within 0.25 m of the same position.
- Smooth positions over time; merge detections of the same class within 0.25 m.
- Drop a `TrackedObject` if unseen for 10 s while in view.
- Confirmed objects get a spatial anchor so labels stay put.
- Low-confidence detections are never labeled.

### 3. Vocabulary data
- `Assets/Data/vocabulary.json`, keyed by COCO class name.
- **Only include household/office classes** (about 40): e.g. person, chair, couch, bed, dining table, potted plant, tv, laptop, mouse, keyboard, remote, cell phone, book, clock, vase, cup, bottle, wine glass, bowl, fork, knife, spoon, banana, apple, orange, scissors, backpack, handbag, umbrella, teddy bear, toothbrush, sink, refrigerator, microwave, oven, toaster, suitcase. Exclude classes like giraffe, airplane, traffic light; if detected, ignore.
- Per entry, per language:
  - `es`, `fr`, `de`: word with article, gender, phrase, sentence, English meaning
  - `zh`: simplified characters, pinyin **with tone marks**, measure word, phrase, sentence, English meaning
  - `en`: word, phrase, sentence, plus Chinese meaning (for the English-with-Chinese-interface mode)
- Add `"reviewed": false` to every entry; all entries need native-speaker review before submission.

### 4. Learning paths
- Learn **Mandarin, Spanish, French or German** with an **English** interface.
- Learn **English** with a **simplified Chinese** interface.
- All UI strings live in a localization table (`Assets/Data/ui_strings.json`) with `en` and `zh` columns. No hard-coded UI text.

### 5. Reading levels
- **Latin-script languages:** Level 1 word → Level 2 phrase → Level 3 sentence.
- **Mandarin:** Level 1 pinyin only → Level 2 characters with pinyin above (ruby-style) → Level 3 characters only.

### 6. Labels (the core visual)
- TextMeshPro, billboarded toward the user, placed just above the object.
- Font: **Noto Sans SC** (covers Latin + simplified Chinese). Generate a dynamic font asset.
- Readable at 3 m; scale with distance within limits; never overlap (simple vertical offset when crowded).
- Frosted-glass card background, subtle fade-in when an object is confirmed.
- Tap (poke or pinch-ray) opens the detail card: meaning, grammar note (article/gender or measure word), example sentence.
- "Not right?" button on the detail card hides that label.

### 7. Find It quiz
- Prompt shows a word (at the user's reading level) for an object currently tracked in the room.
- User touches the real object (hand collider overlapping the TrackedObject's bounds) or points and pinches.
- Correct: green glow burst + streak. Wrong: gentle shake, no penalty on first miss.
- Hint after 8 s: a soft glow pulse near the correct object.
- Rounds of 10 prompts; only prompt for objects tracked in this session.

### 8. Panels (linked windows)
1. **Word Bank:** collected words, filter by room/mastery; tapping a word highlights its real object.
2. **Quiz:** prompt, timer, score, streak.
3. **Progress:** words learned, reading level, accuracy, rooms explored.
4. **Settings:** target language, interface language, reading level, pinyin toggle, label size, confidence threshold (advanced).
All panels share one state store; changes in one update the others and the labels.

### 9. Persistence
Save word bank, mastery, reading level and settings locally (JSON in `Application.persistentDataPath`). No images, ever.

## Build order
1. **P0, detection spike (highest risk, do first):** SecureMR pipeline running; detections reach Unity as class + confidence + 3D position; multi-frame confirmation; plain English debug labels on real objects. On-screen debug panel showing FPS, raw detections and confirmed objects (toggleable, hidden in release).
2. **P1:** vocabulary data, styled labels, Spanish + Mandarin, tap-to-learn detail card, Settings panel.
3. **P2:** Find It quiz, Quiz + Word Bank + Progress panels, persistence, reading levels.
4. **P3:** French, German, English-with-Chinese-interface mode, room grouping, pronunciation audio (pre-generated Piper clips), onboarding, polish.
5. **Stretch:** "Say it" speech-to-text practice (SPATIALML_SETUP.md section 7).

Finish and test each phase on device before starting the next.

## Code conventions
- Event-driven; avoid heavy per-frame work in `Update()`; run inference at a fixed rate (e.g. 5–10 Hz), not every frame.
- Pool label objects; avoid allocations in hot paths.
- Brief comments explaining *why*.
- Keep the project buildable at every commit; commit at the end of each phase.
- The repo will be open-sourced: no secrets or keys in code.

## Definition of done
- [ ] Launches into a working state in under 10 seconds
- [ ] First-time user understands what to do within 30 seconds (onboarding card)
- [ ] Labels are stable: no flicker, no labels on low-confidence detections
- [ ] All four panels work and stay in sync
- [ ] Works with hands only AND controllers only
- [ ] All five learning paths work; Chinese text renders correctly with tone marks
- [ ] All vocabulary marked `"reviewed": true`
- [ ] Smooth frame rate with detection running
- [ ] No debug UI, placeholder text or raw camera data in release

## Out of scope
- Machine translation models (all text is written ahead of time)
- Open-ended dictation (speech-to-text only checks the expected word)
- Custom model training
- Traditional Chinese (possible later toggle)
- Multiplayer, cloud sync
