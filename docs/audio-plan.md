# Audio plan: Listen and Say It

How Word World reads words aloud and checks the learner saying them. Follows SPATIALML_SETUP.md sections 9 and 10 and DESIGN.md.

Status (2026-11-04): steps 2–5 built and passing their offline checks; nothing has been tried on Swan yet, so the step 1 risk checks (audio, microphone and Vosk in spatial mode) happen on the first install.

Offline check (`tools/test_speech.py`, every generated clip through Vosk with the app's grammar and scoring, plus each clip checked against a different word): every language and level accepts 58 of 58 and rejects 57–58 of 58. Mandarin words alone were only 48/58, so Mandarin Say It uses the measure-word phrase (58/58). This only proves the wiring with a synthetic voice; step 6 still needs real speakers.

## Decisions (agreed 2026-11-04)

| Question | Decision |
|---|---|
| Spanish variety | **Mexican Spanish.** The vocabulary sheet uses Mexican words and phrasing; the Spain word, where different, is in `es_alternatives` and shown on the detail card as "Spain: …". |
| Spanish voice | A Mexican Piper voice (`es_MX-*`). |
| Speech models | **All five languages bundled** in the app, so Say It works offline with no download step. Only the language being learned is unpacked and loaded. |
| What the learner says | **Exactly what is written in front of them.** If the label shows more than the word (a Level 2 phrase or a Level 3 sentence), they say the whole phrase or sentence. |
| What is checked | **Correctness** (the right words, in order) and **pronunciation** (how clearly each word was recognized). See "Checking an answer". |
| Microphone | **Never opens without a tap.** The app can prompt ("Your turn: tap Speak"), but the learner always starts listening. |

## Tools

| Part | Tool | Runs on | License (verify before release) |
|---|---|---|---|
| Making the audio | [Piper](https://github.com/OHF-Voice/piper1-gpl) text-to-speech | PC only, as a build step | Piper is GPL-3.0, but it is never shipped; only its output is. Each voice has its own license (see below). |
| Playing the audio | Unity `AudioSource`, positioned at the object | Headset | — |
| Checking speech | [Vosk](https://github.com/alphacep/vosk-api) speech recognition, small models | Headset, offline | Library and small models: Apache 2.0 |

Why Vosk rather than Whisper: Vosk small models are about 40–50 MB each, run in real time on a phone CPU, and accept a **grammar** (a fixed list of allowed answers). Checking "did they say *la silla*?" against a short list is much faster and more accurate than open transcription. Whisper's smallest model invents words on one-word answers and is slow on the headset. (CLAUDE.md asks for TFLite where possible; there is no suitable multilingual TFLite recognizer, so Vosk is the documented exception.)

### Voices

Chosen by license first (details and rejected voices in `docs/dependencies.md`); swap one by changing `VOICES` in `tools/generate_audio.py` and re-running it.

| Language | Voice | License |
|---|---|---|
| Mandarin | `zh_CN-chaowen-medium` | CC0, but fine-tuned from a non-commercial voice: confirm before release |
| Spanish (Mexico) | `es_MX-claude-high` (alternative `es_MX-ald-medium`) | Apache-2.0 |
| French | `fr_FR-siwis-medium` | CC-BY 4.0, needs a credit |
| German | `de_DE-thorsten-medium` | CC0 |
| English | `en_US-ljspeech-medium` | Public domain |

Mandarin clips are spoken from the sheet's tone-marked pinyin rather than Piper's own character-to-pinyin model, which misreads polyphonic characters (艘 *sōu* as *sāo*, 匹 *pǐ* as *pī*, 胡萝卜 *bo* as *bǔ*) and never applies the 一 tone change. The script falls back to Piper's reading, with a warning, only if a row's pinyin can't be matched syllable-for-character.

### Speech models

`vosk-model-small-cn`, `-es`, `-fr`, `-de`, `-en-us` (latest small versions). The Spanish model is general Spanish and handles Mexican speech; test it with Mexican speakers in step 6.

## Turn-taking (never speaking and listening at once)

One component, `AudioTurn`, owns both the speaker and the microphone. Nothing else plays vocabulary audio or opens the microphone, so overlapping is impossible by design.

```
             tap Listen                       clip ends + 0.3 s
   Idle ──────────────────► Speaking ───────────────────────────► Idle
    │                                                              ▲
    │ tap Speak  (a clip is playing? stop it, wait 0.3 s first)    │
    ▼                                                              │
 Listening ── 1 s of silence, the time limit, or tap again ──► Checking ──► Result
```

- **Time limits:** 4 s for a word, 6 s for a phrase, 9 s for a sentence.
- **Tapping Speak while a clip plays** stops the clip and waits 0.3 s before opening the microphone, so the app never hears its own voice.
- **Tapping Listen while listening** cancels listening and plays the clip.
- **Visible state:**
  - The button reads **Speak** (lavender), then **Listening…** (mint, pulsing), then **Checking…**, then the result.
  - While the microphone is open, the dock pill reads **● Microphone on**. It is the privacy indicator, like "Recognizing objects".
- **Leaving:** Home or closing the card cancels any turn immediately.

## What is spoken and said

The written text is always on screen while the learner speaks.

| Level | Latin-script label shows | Clip and expected answer | Mandarin label shows | Clip and expected answer |
|---|---|---|---|---|
| 1 | *la silla* | *la silla* (the noun alone also counts) | *yǐzi* | Listen: 椅子. Say It: 一把椅子, shown as *yì bǎ yǐzi* |
| 2 | *una silla* | *una silla* | 椅子 with *yǐzi* above | Listen: 椅子. Say It: 一把椅子 with pinyin |
| 3 | *Siéntate en la silla, por favor.* | the whole sentence | 椅子 | Listen: 椅子. Say It: 一把椅子 |

Mandarin Say It uses the measure-word phrase, written on the Say It card (decided 2026-11-04): in the offline test the small Mandarin model recognized one-syllable words alone (猫, 鸟, 碗) only 48 times in 58, but every phrase. It also practises the measure word.

The detail card's example sentence also gets a Listen button (the sentence clip) in every language, Mandarin included.

## Checking an answer

Vosk runs with a grammar made of:
1. the expected text;
2. the other vocabulary words of the same language (so a different real word is caught);
3. **confusables** (not added yet): hand-written near-misses from the sheet, such as tone pairs for Mandarin (杯子 *bēizi* cup vs 被子 *bèizi* quilt) or minimal pairs for Spanish (*pelo* vs *pero*);
4. `[unk]` for anything else.

It returns the words it heard, each with a confidence. `SpeechScorer` (plain C#, unit-tested in the editor) then works out two results.

**Correctness: did they say the right words?**
- Words are compared after lower-casing and removing punctuation.
- Level 1 passes on the word, with or without its article.
- Phrases and sentences are aligned word by word; they pass at 80% of words and must include the key noun.

**Pronunciation: how clearly?**
- Each word is **clear** at confidence 0.75 or more, **unclear** below that, or **wrong** if a confusable won.
- For Mandarin, a tone confusable winning is reported as a tone problem: "Check the tone on *bēi*".

**Feedback**
- Each word of the written text gets a chip. Colour is never the only signal; the text always says what happened.
  - mint: "clear"
  - peach: "say again"
  - rose: "wrong" or "missing"
- Example result lines: "Correct! Every word was clear." / "Almost: say *cuchara* again." / "That sounded like *pero*; try *pelo*."
- Results go into the Word Bank, so speaking counts toward mastery.

**Limits, stated honestly in the app's help text**
- This checks that the right words were recognized and how confidently. It is not a teacher grading every sound.
- Mandarin tone checking only covers words that have a tone confusable in the sheet.

## Privacy

- Microphone permission is requested the first time the learner taps Speak, never at launch.
- Audio is processed in memory on the headset, then discarded. It is never saved, never sent, and never logged. Logs record only the recognized words and scores.
- The microphone can only be on while the dock says **● Microphone on**.

## Where it appears

- **Detail card:** **Listen** (the clip for the label's level) and **Say it** (speak the label's text). The example row gets its own Listen.
- **Practice → Say It:** a new exercise.
  - An object's dot pulses and its text (at the reading level) is shown in the floating prompt, with Listen and Speak.
  - Rounds of 5 or 10; Next after each result.
- **Settings:** **Audio on/off.** When off, Listen buttons hide, and Say It still works without a model clip.
- **Journey:** a third step per level that shows five words to say and checks each one (see "Journey: Say step").
- **Dock:** the **● Microphone on** state.

## Files

| Path | What |
|---|---|
| `tools/generate_audio.py` | Reads `Assets/Languages/vocabulary.csv` and runs Piper for every word, phrase and sentence. Writes 22.05 kHz mono Ogg Vorbis, trims silence and evens out loudness. Skips cells whose text is unchanged (hash in the manifest). |
| `Assets/Audio/Vocab/<lang>/<classIndex>_<word\|phrase\|sentence>.ogg` | 1,200 clips (80 × 5 × 3). Imported mono, compressed, not preloaded (`Assets/WordWorld/Editor/VocabAudioImport.cs`). |
| `Assets/Audio/Vocab/manifest.json` | Text, hash and voice for each clip. |
| `tools/generate_tour_audio.py` | Speaks the guided tour's captions (`tour.*` in ui_strings.json) in English and Mandarin with the same voices, and for Mandarin the same pinyin-driven phonemes. Re-run after editing a `tour.*` string (and its pinyin in the script). |
| `Assets/Audio/Tour/<en\|zh>/<step>.ogg` | 18 narration clips (9 steps × 2), imported like the vocabulary clips; the scene builder assigns them to the Tour. |
| `Assets/Data/VocabClips.asset` | Index of the clips, rebuilt by the scene builder. |
| `tools/fetch_speech.py` | Downloads the Vosk library (arm64 only) and the five small models. Run once after cloning; the models (about 210 MB) are gitignored. |
| `tools/build_speech.py` | Writes `Assets/Data/speech.json`: every word, phrase and sentence split into the words its model knows, the words that name the object, and the words a model doesn't know (listed for review). |
| `tools/test_speech.py` | The offline check above. |
| `Assets/StreamingAssets/vosk/<lang>.zip` | The five small models, zipped; unpacked to `persistentDataPath/vosk/<lang>` on first use in a language. |
| `Assets/Plugins/Android/vosk-android.aar`, `jna.aar` | Vosk library and its JNA dependency (committed, 3.6 MB). |
| `Assets/WordWorld/Audio/AudioTurn.cs` | The turn-taking state machine; the only owner of speaker and microphone. Records with Unity's `Microphone`, ends on 1 s of quiet. |
| `Assets/WordWorld/Audio/VocabAudio.cs` | Finds and plays a clip at an object's position. |
| `Assets/WordWorld/Audio/SpeechData.cs` | Reads `speech.json`; builds targets and the grammar. |
| `Assets/WordWorld/Audio/SpeechCheck.cs` | Vosk bridge: unpacks and loads the model and recognizes, all on worker threads. Unavailable in the editor, so Say It hides there. |
| `Assets/WordWorld/Speech/SpeechScorer.cs` | Correctness and pronunciation scoring (own assembly, no Unity dependencies). Tests: `Assets/WordWorld/Tests/Editor/SpeechScorerTests.cs`. |
| `Assets/WordWorld/UI/SayItCard.cs` | The Say It card: one word (detail card), a Practice round, or the Journey's Say step. |
| Sheet columns `<lang>_confusables` | Not added yet. Hand-written near-misses (tone pairs, minimal pairs) would go here; the grammar already catches every other vocabulary word. |
| `docs/dependencies.md` | Every voice and model with its license. |

## Build order (each step checked on Swan before the next)

1. **Risk checks** (about a day each):
   - A Unity `AudioSource` plays in spatial mode, from a 3D position.
   - The microphone records in spatial mode (Unity `Microphone`, or Vosk's own Android `SpeechService` if Unity's does not work).
   - Vosk with a grammar recognizes "chair" and "la silla" on Swan, and we measure the delay.
   - Bundling 230 MB of models: check the APK still installs and launches in under 10 s, and check the PICO store's size limit.
2. **Clips:** `tools/generate_audio.py`, audition and choose voices, write `docs/dependencies.md`, and listen to a sample from each language. A native speaker checks the Mandarin tones.
3. **Playback:** `AudioTurn` (speaking half), `VocabAudio`, Listen on the detail card, and the Audio setting.
4. **Recognition:** add Vosk, model unpacking (a "Preparing…" message on first use), `SpeechCheck`, and `SpeechScorer` with tests.
5. **Interface:** Say it on the detail card, the Say It exercise, the Journey's Say step (`JourneyCard.cs` step 2), the microphone pill, word chips, and the strings in English and Chinese.
6. **Tuning:**
   - Record a test list: 20 items per language at each level, said correctly, said with a near-miss, and said wrongly; ideally by native speakers, including Mexican Spanish.
   - Set the clarity threshold per language, and fill in confusables.
   - Hide Say It for any language that cannot reach about 90% correct accept and 90% correct reject.

## Risks

| Risk | Fallback |
|---|---|
| Spatial mode does not route Unity audio or the microphone | Android `MediaPlayer` for playback; Vosk's Java `SpeechService` for recording, called from a small plugin |
| 230 MB of models is too large for the store or slows launch | Ship models as an Android asset pack, or install only the chosen language's model on first Say It |
| Vosk is weak for a language or accent | Raise the threshold, rely on the grammar, or hide Say It for that language |
| A voice's license does not allow redistribution | Choose another voice for that language |
| Echo: the microphone hears the end of a clip | The 0.3 s guard after playback; lengthen it if needed |

## Journey: Say step

Each Journey level gets a third step after Find It.

- **What it shows:** five words, chosen from objects tracked in this session (preferring the ones the learner tapped in step 1). Each is written out at the learner's reading level, so at Levels 2 and 3 it's the phrase or sentence. The word's object dot pulses so the learner can see what it names.
- **Pointing at the object:** each word starts with a soft ping played from its object (fully 3D, through `AudioTurn` so it never overlaps the microphone), and the Say It card moves beside that object, on the side the learner is looking. The word's pronunciation still plays only on a Listen tap.
- **How it runs:** one word at a time. The learner can tap Listen to hear it first, then taps Speak. The microphone never opens on its own, even between words.
- **What it checks:** every word gets the full check from "Checking an answer": the correctness result plus the per-word clear / say again / wrong chips.
- **Retries:** after each result, Try again (unlimited, no penalty) or Next.
- **Passing:** 4 of the 5 must be correct, counting each word's best try. Then the level goes up. Otherwise, Try again repeats the step with the same five words.
- **Summary card:** the five words with how each went, so the learner sees which ones to practise.
- **If Say It is hidden for the language** (see Build order step 6), the Journey skips this step and levels up after Find It, as it does now.
