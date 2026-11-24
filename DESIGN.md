# DESIGN.md: Word World (单词世界)

How Word World should look, feel and flow. Read this before building any UI, labels, panels or feedback effects. `CLAUDE.md` covers what to build; this file covers how it should look and behave. If they conflict on visual or UX details, this file wins.

---

## 1. Design goals

1. **Reading comes first.** Every label must be easy to read at a glance, from across a room, in both Latin script and Chinese characters.
2. **The real room is the lesson.** Virtual elements stay small, light and attached to real objects. Never cover the room with large UI.
3. **Calm and clear.** A study tool, not an arcade. Motion is short and purposeful.
4. **Always show what the app is doing:** scanning, recognized, unsure, correct, wrong.
5. **Works for a first-time headset user** within 30 seconds.
6. **Only what is needed, when it is needed.** One window at a time; everything else is one tap away.

### Mixed reality HCI principles we follow
These come from Apple's visionOS spatial design guidance, Meta's hand-tracking UI guidance, and comfort-zone research for head-worn displays.
- **Progressive disclosure.** Working memory is small and the room is already busy. The menu offers two choices; exercises sit one level down. Never more than one window open at a time.
- **The room stays in front.** Main content sits about 1 m away, slightly below eye level, directly ahead. Persistent UI sits out of the way: the dock to the left of the windows at chest height (out of the path of a resting hand's ray), the Journey card low in view.
- **Horizontal over vertical.** Turning the head left and right is easier than up and down, so secondary items go to the side, not above.
- **Nothing in personal space.** No window closer than 0.5 m. Labels and the quiz prompt are the only things closer than a window.
- **Big targets.** At least 3 × 3 cm (about 22 mm is the hand-tracking minimum), with 1 cm gaps. Every button works with poke, pinch-ray and controller.
- **Legibility over style.** Dark ink on light, solid surfaces (at least 7:1 contrast for text). State is shown by words and fills, never by depth effects.
- **One way home.** The dock's Home button (or the controller's X) returns to the menu from anywhere.
- **Windows stay where they are put.** Status lines re-centre in one step when far out of view; windows never trail the head.

---

## 2. Visual style

**Theme name:** "Soft light." Solid light panels with dark ink, and pastel fills for highlights.

Each surface is one solid background panel. We tried neumorphism (a highlight and a shadow layer behind each surface), but in passthrough the extra layers read as stray grey outlines rather than depth, so they were removed. Text is always dark ink on a solid light or pastel surface, and every state changes the words or the fill.

### Colors (define once as tokens in a single `WordWorldTheme` ScriptableObject)
| Token | Hex | Use |
|---|---|---|
| `Card` | `#EEF1F6`, solid | Label and window surfaces. Must be solid: on Swan, text over a see-through background renders as dark blocks. |
| `Button` | `#D3DBE7`, solid | Unselected buttons. Darker than `Card` so every button shows its edges on a card. |
| `Ink` | `#1F2633` | All text, including on pastels |
| `InkMuted` | `#4A5568` | Secondary text (meaning, captions). Still 7:1 on `Card`. |
| `Accent` | `#1E6E58` (deep teal) | Pinyin text, big numbers, progress fills |
| `AccentSoft` | `#CDEFE0` (mint) | Selected options, "Recognizing" pill |
| `Primary` | `#DCD3FA` (lavender) | The one main action on a card (Journey, Start, Play again) |
| `Info` | `#C9E3FA` (sky) | Progress and timer tracks |
| `Correct` | `#86D9B6` | Quiz correct ring |
| `Hint` | `#FFCFAE` (peach) | Hint glow, "having trouble" pill |
| `Wrong` | `#F6C1CA` (rose) | Exit confirm, wrong answers (briefly) |

Gender color coding (optional, toggle in Settings, off by default): masculine `#3A6EA5`, feminine `#B0436E`, neuter `#6B7F3A`. Applied only to the article text, never the whole label.

### Typography
- **One font for everything:** Noto Sans SC (covers Latin, accented letters and simplified Chinese). Create a TextMeshPro dynamic font asset with fallback enabled. Test that é, ü, ñ, ç, ß and tone-marked pinyin (ǎ, ǚ, ǜ) all render.
- Weights: Medium for label words, Regular for everything else, Bold only for panel titles and quiz prompts.
- Size in **angular terms**, not fixed meters, so labels read the same at any distance (see section 3).

### Shape and depth
- Rounded corners: 16 mm radius on windows, 10 mm on labels and buttons.
- Windows and cards have a solid 3 mm rim so they stand out from a busy room (V3 review). The rim runs from light blue-grey (`PanelRimTop`) on the upper half to a darker slate (`PanelRimBottom`) on the lower half, a white 1.5 mm line runs just inside the top edge, and a soft shadow sits 6 mm below and behind. Together they give a slight top-to-bottom gradient and depth. The face itself stays one solid colour, because text over a non-solid background renders as blocks on Swan. Labels and buttons have no rim.
- Selected options fill with mint; the main action on a card fills with lavender.
- No glow on resting elements. Glow is reserved for feedback (found, hint).

---

## 3. Object labels (the core visual)

### Layout
```
          yǐzi            ← ruby line (pinyin), Accent color, 55% of main size
        ┌────────┐
        │  椅子   │        ← main text, Ink, Medium weight
        └────┬───┘
             ·           ← thin leader line (1 mm) + 6 mm dot on the object
          [chair]
```
- Card floats **15 cm above** the hit point on the object, with a thin leader line down to a small dot on the object's surface.
- Always **billboarded** to face the user (rotate on Y only, so text never tilts).
- **Size:** main text height ≈ 1.2° of visual angle (about 2 cm at 1 m, 6 cm at 3 m). Clamp: minimum 1.5 cm, maximum 8 cm.
- Card padding 8 mm horizontal, 5 mm vertical. Width fits the text. Lines are sized at 1.36 em (Noto Sans' ascender to descender); at 1.2 em two-line sentences lost the bottoms of their descenders (Nov 19 review).
- The meaning line sits flush under the word, never overlapping it; the backing panel behind both fills the rounded corners at the seam.

### What the label shows
| Level | Spanish/French/German/English | Mandarin |
|---|---|---|
| 1 | Word with article: *la silla* | Pinyin only: *yǐzi* |
| 2 | Phrase: *una silla* | Characters with pinyin above: 椅子 / *yǐzi* |
| 3 | Sentence: *Esta es mi silla.* | Characters only: 椅子 |

Text comes from `Vocabulary.TryGetLabel()`, which reads `Assets/Data/vocabulary.json`. That file is built from the vocabulary sheet, `Assets/Languages/vocabulary.csv`, where every word, phrase and sentence is written by hand; nothing is generated. Never hard-code vocabulary in UI scripts. Level-3 sentences wrap at about 18 characters per line, maximum 2 lines.

### Label states
| State | Look |
|---|---|
| **Scanning** (object seen, not yet confirmed) | Small pulsing dot on the object only, no text. |
| **Confirmed** | Card fades in over 0.25 s and scales from 90% to 100%. |
| **Hover** (gaze, hand ray or controller over it) | A 5 mm `Accent` ring appears around the card and the card leans toward `AccentSoft`, with the hover tick. Labels are the main thing people pinch, so this is how a learner knows gaze has landed and a pinch will open it (Nov 23 review). |
| **Selected** | Detail card opens (section 4). Other labels dim to 60% opacity. |
| **Lost** (object out of view) | Stays in place at 40% opacity for 10 s, then fades out. |
| **Quiz mode** | Words hide; each label becomes a **This one** button over its dot (see section 6). |
| **Behind a panel** | Labels are always up while their object is tracked. A label that lines up with an open window, card or the dock from the learner's eyes (in front of it or behind it, 3 cm margin) fades out so it neither blocks the panel nor shows through it, and comes back when the panel closes or moves. Labels that are part of the current exercise never fade: every label in Find It, the word whose card or Say It is open, and labels that are flashing or lit up as feedback. |

Label cards are sized to their text with tight padding (per-character width estimates, 1.2 line height), so a short word does not sit in a wide empty card.

### Crowding rules
- Labels never overlap. If two cards would overlap from the user's view, stack them vertically with leader lines, closest object on the bottom.
- Show at most **8 labels** at once, preferring the closest objects. A small "+3 more" chip appears at the edge of view if more are hidden.
- Labels farther than 4 m are hidden.

---

## 4. Detail card (tap a label)

Opens 60 cm from the user on the line to the object (in front of it, within poke reach), between 10 and 32 cm below eye level. The Say It card is placed the same way.

```
┌───────────────────────────────────┐
│  椅子     [Listen] [Say it]     ✕  │  ← word, audio, close button
│  yǐzi   chair                      │  ← pinyin + meaning (interface language)
│ ───────────────────────────────── │
│  Measure word   一把椅子  yì bǎ yǐzi │  ← grammar row (article/gender for es/fr/de)
│  Example        这是我的椅子。       │
│                 Zhè shì wǒ de yǐzi. │
│  Means          This is my chair.   │  ← the example in the learner's language
│ ───────────────────────────────── │
│  [Save]  [Hear example] [Wrong object? Hide it]│
└───────────────────────────────────┘
```
- **Spanish/French/German grammar row:** "Gender: feminine · Article: la · Plural: no". Spanish is Mexican Spanish; when Spain uses a different word, show it under the word (*Spain: el coche*).
- **English (Chinese interface):** meaning in Chinese, all row titles in Chinese.
- **Means:** what the example sentence says, in the learner's own language (`sentenceMeaning` in the vocabulary sheet; Nov 19 review: the example alone could not be understood). The card is 36 × 28 cm to fit it.
- **Listen** plays the label's own line (word, phrase or sentence at the reading level; Mandarin: the word) from the object. **Hear example** plays the example sentence. Both only appear if audio is on and a clip exists, and only ever play on a tap.
- **Say it** (lavender) opens the Say It card for this word (section 5.5). It only appears where speech can be checked for the word.
- **Save** adds the word to the Word Bank (words are also auto-collected on first tap).
- **Wrong object? Hide it** (was "Not right?", which did not say what it did, Nov 23 review) hides that label for the session; the dock's pill confirms "Label hidden for this session" for 4 s. Nothing else is logged.
- Closes on ✕, tapping elsewhere, or after 20 s without interaction.

---

## 5. Modes and windows (multi-window, spatial mode)

Separate windows through the spatial adapter, but **only one is open at a time**. Each opens about 1 m in front of the user, slightly below eye level. All windows share one state store; any change updates every window and the labels immediately.

### The menu
```
┌──────────────────────────────┐      ┌──────────────────────────────┐
│          Word World          │      │ ‹ Back      Practice         │
│      Spanish · Level 2       │      │ ┌──────────────────────────┐ │
│ ┌──────────────────────────┐ │      │ │ Explore                  │ │
│ │ Journey      (lavender)  │ │      │ │ Read the labels around you│ │
│ │ Level up step by step    │ │      │ └──────────────────────────┘ │
│ └──────────────────────────┘ │      │ ┌ Find It ─────────────────┐ │
│ ┌──────────────────────────┐ │ ───► │ │ Touch the object a word… │ │
│ │ Practice                 │ │      │ └──────────────────────────┘ │
│ │ Choose an exercise       │ │      │ ┌ Say It ──────────────────┐ │
│ └──────────────────────────┘ │      │ │ Read words out loud      │ │
│ [Progress] [Settings] [Exit] │      │ └──────────────────────────┘ │
└──────────────────────────────┘      │ ┌ Word Bank ───────────────┐ │
                                      │ │ Review the words you…    │ │
                                      │ └──────────────────────────┘ │
                                      │ Round length    [5] [10]     │
                                      └──────────────────────────────┘
```
- **Journey** (progression mode): a guided loop through the reading levels. Each level has three steps: **read 5 labels** (tap them), **pass a Find It round** (8 of 10), then **pass a Say It round** (say 4 of 5 words correctly; skipped where Say It isn't available). Passing unlocks the next level. A small Journey card low and to the right (clear of the dock on the left) shows the level, the current goal and a progress bar; when a round is due it shows **Start Find It** or **Start Say It**. Progress is saved.
- **Practice** (choose an exercise): **Explore** (labels only, tap to learn), **Find It** and **Say It** (rounds of 5 or 10), **Word Bank** (review and find words in the room). Say It is hidden where speech can't be checked.
- Small actions under the main choices: **Progress**, **Settings**, **Exit** (tap twice: the first tap turns it rose and reads "Exit?").
- Under those, the **Sound** row: **Off**, **Alerts only**, **All** (the selected one is mint). See section 9.
- A brand-new learner skips the menu and starts the Journey, so the first thing they do is read labels. Later launches open the menu.

Window sizes: Menu 42 × 62 cm, Word Bank 45 × 55 cm, Progress 35 × 40 cm, Settings 42 × 72 cm (label size and confidence buttons 7 × 5 cm), Journey card 34 × 17 cm, Find It results 40 × 24 cm, Say It card 40 × 34 cm.

### 5.1 Word Bank
- Header: "Word Bank" + count ("23 words").
- Filters as chips: **All · This room · Kitchen · Office · Living room**, and **New · Learning · Mastered**.
- List rows: word in target language (at current reading level) · meaning · mastery dots (○○○ to ●●●).
- Tapping a row highlights its real object if it's in view: the object's dot pulses `Accent` and a light beam points to it for 2 s. If not in view: "Not in this room."
- Empty state: "Look around your room. Words appear here as you tap labels."

### 5.2 Find It
- Starts straight from Practice or the Journey card; there is no idle window.
- During a round only the floating prompt shows: the word (target language, current reading level), score and streak under it, a timer bar, Skip after 15 s, **Listen** to its right and **End** to its left. End stops the round at once, without results, and returns to where it was started (the Journey card or the menu).
- After a round: a results card with score, accuracy, words to review, **Play again** and **Done**. Done returns to where the round started (menu or Journey).

### 5.5 Say It
Details in `docs/audio-plan.md`.
```
┌──────────────────────────────────────┐
│ Say it                     2 / 5   ✕ │
│          yì bǎ yǐzi                  │  ← pinyin above (Mandarin Level 2)
│            一把椅子                   │  ← exactly what to say
│             chair                    │
│      [一] [把] [椅] [子]              │  ← chips after a try: mint clear,
│  Almost: say 椅 again.               │     peach say again, rose wrong/missing
│  [Listen]   [  Speak  ]   [Next]     │
└──────────────────────────────────────┘
```
- What to say is always written on the card: the label's line at the reading level (word, phrase or sentence). Mandarin always says the measure-word phrase.
- **Listen** plays it first if the learner wants. **Speak** is the only way the microphone opens; it reads **Listening… tap when done**, then **Checking…**. Listening also stops after 1 s of quiet.
- The feedback line always says what happened in words; the chip colours are a second signal, never the only one.
- Unlimited tries, no penalty. **Next** turns lavender once the word has been said correctly.
- The round summary shows one chip per word (mint said correctly, rose not yet), "To practise: …", and a one-line honest note: Say It checks the words it hears, not every sound.
- First use in a language shows **Getting ready…** while the speech model is unpacked in the background.

### 5.3 Progress
- Words learned (big number), current reading level, quiz accuracy, rooms explored, daily streak.
- A simple bar per category showing words learned out of available.
- "Level up" suggestion card when the user gets 80%+ accuracy on 20 words at the current level: "Ready for Level 2?" with **Try it** / **Not yet**.

### 5.4 Settings
- **I'm learning:** Mandarin / Spanish / French / German / English.
- **Interface language:** English / Chinese, named in the interface language (auto-set to Chinese when learning English, can be changed).
- **Reading level:** 1 / 2 / 3 with a live preview of the chair label, and a line under the title saying what the levels change: "1 the word, 2 a short phrase, 3 a full sentence", or for Mandarin "1 pinyin only, 2 characters with pinyin, 3 characters only".
- **Show pinyin** (Mandarin only), **Gender colors**, **Sound** (Off / Alerts only / All, same as the menu row; Say It still works with it off), **Label size** (S / M / L).
- **Advanced:** detection confidence slider, "Show room mesh" (debug, hidden in release).
- **Privacy note** (always visible at the bottom): "Object recognition runs on your headset. Word World never saves or sends camera images."

---

## 6. Find It quiz flow

1. User picks **Find It** in Practice or taps **Start Find It** on the Journey card. Each label's word is replaced by a **This one** button (`Button` color, at least 3 × 3 cm), still joined to its object by the leader line and dot, so every recognized object can be picked.
2. A prompt floats 60 cm in front of the user, slightly low: *la taza* / 杯子. An optional **Listen** button beside it plays the prompt aloud from the prompt (never from the object, which would give the answer away). Sound on All only.
3. User taps an object's **This one** button (poke, pinch-ray or controller), or touches the real object (hand overlaps the object's bounds).
4. **Correct:** a green ring expands from the object, the label pops in showing the word, a soft chime plays from the object (unless Sound is Off), streak +1. Next prompt after 1 s.
5. **Wrong object:** that object's button shakes briefly and shows its own word for 1.5 s (so wrong answers still teach). No penalty on the first miss; the prompt says "Not that one. One more try."
   **Two tries per word.** After the second miss the correct object shows its word and pulses for 2.5 s ("Here it is."), the word goes on the review list, the streak resets, and the next prompt follows.
6. **Hint** after 8 s: a soft `Hint`-colored glow pulses near the correct object.
7. **Skip** button after 15 s.
8. Prompts only use objects confirmed in this session. If fewer than 4 objects are recognized, show: "Look around a bit more so I can find some objects to quiz you on."

---

## 7. First-time experience (under 30 seconds)

0. **Title** (about 7 s): black, then the room fades in, then a card with the name and the purpose, "Learn another language by seeing the names of objects around you.", in the headset's system language (Chinese if the system is Chinese, otherwise English). A soft ripple as the name appears.
1–2. **Two questions, one at a time, in one window with a breadcrumb** across the top: **1 You speak › 2 Learn › 3 Start**. The current step is mint, finished steps can be tapped to go back, and later steps are greyed. The V3 review showed why: with a single "What do you want to learn?" list, a tester tapped English meaning "I speak English".
   1. **"What language do you speak?"** in the headset's system language, with that language already selected, marked and listed first (Nov 23 review): **English (System)** / **Chinese**, or **英文** / **中文（系统）**. One tap confirms it. Until Nov 19 the question was shown in both languages at once, which was distracting.
   2. **"What do you want to learn?"** in the language just chosen, listing all five languages by their name in that language. The language they speak is greyed out and can't be chosen; so are Spanish, French and German for Chinese speakers, since meanings and grammar notes exist in Chinese only for the English path. The choice sets both the target and the interface language.
3. **Start:** "Look around your room. Objects you can learn will get labels." **Got it** starts scanning.
3. **Scanning hint:** while no objects are confirmed, a small floating line gives a concrete task: "Look at a chair, a cup or a book near you." Disappears when the first label appears.
4. **First label moment:** the first confirmed object gets a slightly larger label with a "Tap me" tooltip. Beside it, within reach, a card shows that word in all five languages with the learner's language highlighted; it closes when they tap the label (or after 20 s).
5. The Journey card is already showing "Tap 5 labels to read them", so the first tap is also the first Journey step.

Steps 3–5 apply only when the guided tour is unavailable; normally **Got it** starts the tour (below), which replaces the scanning hint, the "Tap me" tooltip and the five-language card so nothing crowds what it points at.

On later launches skip the title, intro and steps 3–5, but still ask both questions, with last time's answers highlighted in mint (one tap each to confirm); picking the language goes straight on to the tour and menu.

### 7a. Guided tour
Nine short steps. Each points at one part of the app with a pulsing green halo behind it, a caption card beside it (with a tooltip tail toward it, a step count, **Skip** and **Next**), and a spoken line in the interface language (English or Mandarin; Sound must be All, otherwise captions only). Caption and narration are the same text (`tour.*` in ui_strings.json); `tools/generate_tour_audio.py` speaks them with Kokoro-82M (open, Apache-2.0, generated offline): the "af_heart" voice for English and the Mandarin-tuned v1.1-zh model for Mandarin. The Piper narration sounded stiff (Nov 23 review); vocabulary clips still use Piper with hand-written pinyin. Kokoro reads the Mandarin characters itself; its tones were checked against the pinyin table (only the expected third-tone changes differ).

| # | Points at | Says | Moves on when |
|---|---|---|---|
| 1 | nothing (card in front) | Welcome to Word World! | Next |
| 2 | the word label nearest the gaze (card in front until one appears) | Words appear above objects. Tap a glowing word to learn it. | a word is tapped |
| 3 | the detail card's close button (caption above the card) | Here are the meaning and an example. Close the card when you are done. | the card closes (skipped if none opened) |
| 4 | the dock's recognition pill | This shows the camera is finding objects. Tap it to pause or resume. | Next |
| 5 | the dock's Home button | Home brings back the menu from anywhere. Tap it now. | the menu opens (Next opens it) |
| 6–8 | the menu's Journey, Practice and Settings buttons (caption right of the menu) | what each is for | Next |
| 9 | the menu's **Tour** button | Tap Tour any time to see this again. | Done |

Plays on **every launch**, right after the language questions on the first one (then ends in the Journey) and straight away on later ones (ends in the Journey while the word bank is empty, otherwise at the menu); **Skip** ends it at any time. **Tour** at the top right of the menu plays it again. The narration waits up to 2.5 s for a word the learner just tapped to finish.

**Only what the step is about can be used.** While the tour runs:
- Labels stay visible, but can be tapped only on step 2, and the word is not spoken (the narration explains the card next).
- The word card shows only its meaning and the close button (no Listen, Say it, Save or Wrong object).
- The dock appears at step 4; only the recognition pill works at step 4 and only Home at step 5.
- The menu appears for steps 6–9 to be pointed at; none of its buttons can be hovered, heard or pressed, and the controller's X does not open it.
- The recognition notice waits until the tour ends.

**Ambience:** sparse, faint motes of light drift 1.4–3 m around the learner (world space, so they never move with the head).

**App icon:** `Assets/WordWorld/Art/AppIcon.png`, a label card (词 / word) with its leader line and dot; the scene builder assigns it in Player Settings.

---

## 8. Status and system feedback

- **Dock:** to the left of the windows (40 cm left, 20 cm below the eyes, 85 cm away, clear of every window, the quiz prompt and the scanning hint), the recognition pill with **Home** and **Move** side by side 3 cm under it. **Move** makes the dock follow the centre of the view ("Turn to where you want this, then tap it", Move becomes **Done**); tapping any part of it places it there. The spot is kept relative to the way the learner faces (within 34° of straight ahead, from waist to just above eye height) and saved, so re-centring brings it back to the same place. Move is locked during the tour. The dock always turns to face the learner's eyes (eased, like a label), so it stays readable from any position; a turn that would make it cross an open window or card (as seen from the eyes, which also rules out cutting through one) is held back and the dock slides clear first. It sat directly under the windows until the Nov 7 recording showed low-hand rays pausing recognition three times.
- **Recognition pill:** "● Recognizing objects" on mint. Tapping it pauses recognition ("Paused" on the plain surface). This doubles as the privacy indicator.
- **Microphone:** while Say It is listening the pill reads "● Microphone on" on lavender; tapping it stops listening. The microphone is never on without this.
- **Not enough light / nothing found for 20 s:** "Having trouble seeing. Try turning on a light or moving closer."
- **Errors** say what happened and what to do. If the detection model will not load, or stops delivering frames for 25 s (on Swan only a headset restart fixes this), the pill reads "Recognition off: tap for help" on peach, **practice objects come on at once** (even mid-tour, so the room is never empty of words), and once the tour is over a card says so plainly once per session: restart the headset; meanwhile the practice objects keep the session going. A refused camera permission gets its own wording. Tapping the pill while recognition is down shows the card again instead of pausing (not during the tour). The pill is a fixed 26 × 6.2 cm; long states wrap onto two lines.
- **Home from anywhere:** the dock's Home button, or the left controller's X button, closes whatever is open and shows the menu in front of the user.

---

## 9. Motion and sound

- **Room ripple:** when an activity starts (session start, Explore, Find It, Say It, practice objects placed), a double ring of light runs out across the floor from the learner at 1.8 m/s, and each object lifts 3.5 cm and glows green as the ring reaches it.
- **Right and wrong:** a right answer (Find It, or a word said right in Say It) makes the label and object hop 6 cm and glow; a wrong one shakes them sideways for half a second. A practice object's 3D model moves with its label.
- **Light-up:** at the end of a Find It round, a slower wave lights up every collected word in the room, nearest first, each with a sparkle and a chime.

- All transitions 0.2–0.3 s, ease-out. Nothing loops except the scanning dot pulse and the recognition indicator.
- Respect a "Reduce motion" setting: replace scaling and shaking with simple fades.
- **Sound setting** (a row on the menu's main page, and cycled from Settings): **Off** (silent; Say It still listens), **Alerts only** (sound effects only), **All** (effects, background music and spoken words). Default All.
- **Ripples** (all sounds are made in code, no audio files): a soft water ripple plays from the exact spot in the room whenever something pops up (a label the first time its object is confirmed, a window or card opening) and a brighter one wherever the learner taps (any button, any label). A burst within 0.12 s makes one ripple.
- **Hover tick:** a very short, quiet tick when gaze or a pointer first lands on a button, together with its highlight. Skipped right after another sound so a tap never ticks too.
- **Close:** a soft falling glide when a card or window is dismissed (✕, OK, closing Settings).
- **Chime** on a correct Find It answer, from the object.
- **Music** (All only): a calm 24 s looping pad with a sparse bell melody, quiet and non-spatial. It ducks to 30% while a word is spoken.
- **The microphone hears only the learner:** while Say It is listening, every effect stops and the music goes silent.
- Pronunciation plays from the object's position in the room. Tapping a label (exploring) says the object's name from the object (All only).

---

## 9a. Practice objects

Settings → **Practice objects: On** places five 3D models (CC0, Poly Haven: chair, sofa, potted plant, table with a vase on it) into gaps in the room mesh: open floor at the learner's floor level (not a tabletop or outside the scan) with nothing of the mesh in the space the model would fill, a clear line of sight from the learner, and room from the other models and from real tracked objects. Gaps near each model's preferred spot (spread 55° either side, 1.5–2.4 m) win; a model with no gap is left out rather than put inside furniture, and without a room mesh they fall back to those fixed spots. They last **one session only**: the switch is not saved, so they never carry over to the next launch. Each is a confirmed track, so it gets a label and works in Explore, Find It and Say It like a real object; tapping the model is the same as tapping its label. **Move them here** re-places them around where the learner stands. They make a sparse room, or a headset whose recognition is down, good for a full session.

## 10. Interaction rules

- Every button works with **poke** (direct touch), **pinch-ray** and **controller trigger**.
- **Finger ray:** in spatial mode the system targets with eye gaze and draws no hand ray, so the app draws one from each tracked index fingertip, always while the hand is tracked (`FingerRay`, Nov 16 review). It is a thin white line 60 cm long that turns green and ends in a green dot when it lands on something tappable, gives that thing the same hover highlight, and taps it on an index pinch. One pinch is one tap: whichever of the ray, a gaze-pinch or a poke delivers it first wins, and while a ray is on something a gaze-pinch elsewhere is ignored (`ClickGate`).
- **Text stays inside its background.** Native text never wraps by itself, so every runtime string goes through `SetFitted` (or `SpatialButton.SetLabel`): it breaks onto more lines at spaces (between any two characters in Chinese) and shrinks only if the lines still don't fit. Width estimates carry Swan's measured 13% extra width. Each text's box is recorded at build time (`TextBox`).
- Minimum button size 3 × 3 cm; minimum spacing 1 cm.
- Hover state on everything interactive; light haptic on controllers for press. Buttons: while gazed at or pointed at, the background leans 40% toward `Accent`, a solid `Accent` ring appears 6 mm around it and the button scales to 108% (`SpatialButton`; the Nov 19 review found the old 28% tint and 6% growth too faint to tell where the gaze was; set button colours through `SpatialButton.Background` so the hover can restore them).
- No artificial movement of the user. Windows never follow the head; the user moves them.
- Nothing appears closer than 40 cm to the user's eyes.

---

## 11. Bilingual UI

- All interface text comes from `ui_strings.json` (`en` and `zh`). No hard-coded strings.
- **One language at a time.** Panels show only the interface language, never English and Chinese side by side (Nov 19 review). Before the learner has chosen, that is the headset's system language.
- Chinese text is usually shorter; English can be 2× longer. Design layouts for the English length and check Chinese doesn't look empty.
- When the interface is Chinese, the target-language word still uses its own script (English learner sees "chair", with meaning 椅子).

---

## 12. Don'ts

- Don't put labels on people (`person` class): only show the dot, no text, unless the user turns on "Label people" in Settings (off by default).
- Don't show confidence numbers or class IDs outside debug mode.
- Don't cover the object with the label.
- Don't use emoji in the UI; use simple line icons.
- Don't show labels during the quiz (it would give away answers).
- Don't fill the room with windows; only one window is open at a time.
- Don't put light or pastel text on a light surface; text is always dark ink.

---

## 13. Build order for UI

1. Theme ScriptableObject + font asset + label prefab with all states (section 3).
2. Detail card (section 4).
3. Settings window, wired to language and reading level (live preview).
4. Word Bank and Progress windows.
5. Quiz window + Find It flow (section 6).
6. First-time experience (section 7) and status chip (section 8).
7. Motion/sound polish and reduce-motion setting.

**Checkpoint after each step:** screenshot or describe the result on the headset to Erin before moving on.
