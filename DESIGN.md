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
- **The room stays in front.** Main content sits about 1 m away, slightly below eye level, directly ahead. Persistent UI (the dock and the Journey card) sits low in the field of view, where it is reachable and out of the way.
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
| `Card` | `#EEF1F6`, solid | Label, button and window surfaces. Must be solid: on Swan, text over a see-through background renders as dark blocks. |
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
- One solid background panel per surface: no shadow or highlight layers, no hard outlines.
- Selected options fill with mint; the main action on a card fills with lavender.
- No gradients, no glow on resting elements. Glow is reserved for feedback (found, hint).

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
- Card padding 8 mm horizontal, 5 mm vertical. Width fits the text.

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
| **Hover** (hand ray or gaze over it) | Background turns `AccentSoft`, card scales to 105%. |
| **Selected** | Detail card opens (section 4). Other labels dim to 60% opacity. |
| **Lost** (object out of view) | Stays in place at 40% opacity for 10 s, then fades out. |
| **Quiz mode** | All labels hide. Only dots remain (see section 6). |

### Crowding rules
- Labels never overlap. If two cards would overlap from the user's view, stack them vertically with leader lines, closest object on the bottom.
- Show at most **8 labels** at once, preferring the closest objects. A small "+3 more" chip appears at the edge of view if more are hidden.
- Labels farther than 4 m are hidden.

---

## 4. Detail card (tap a label)

Opens next to the label, slightly toward the user, about 25 cm wide.

```
┌───────────────────────────────────┐
│  椅子              yǐzi        ✕  │  ← word + pinyin, close button
│  chair                             │  ← meaning (interface language)
│ ───────────────────────────────── │
│  Measure word   一把椅子  yì bǎ yǐzi │  ← grammar row (article/gender for es/fr/de)
│  Example        这是我的椅子。       │
│                 Zhè shì wǒ de yǐzi. │
│ ───────────────────────────────── │
│  [🔊 Listen]  [★ Save]  [Not right?]│
└───────────────────────────────────┘
```
- **Spanish/French/German grammar row:** "Gender: feminine · Article: la · Plural: no". Show the Latin American alternative under the word when one exists (*LatAm: el carro*).
- **English (Chinese interface):** meaning in Chinese, all row titles in Chinese.
- **Listen** only appears if audio is on and a clip exists.
- **Save** adds the word to the Word Bank (words are also auto-collected on first tap).
- **Not right?** hides that label for the session and logs nothing else.
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
│ │ Choose an exercise       │ │      │ ┌ Word Bank ───────────────┐ │
│ └──────────────────────────┘ │      │ │ Review the words you…    │ │
│ [Progress] [Settings] [Exit] │      │ └──────────────────────────┘ │
└──────────────────────────────┘      │ Find It round   [5] [10]     │
                                      └──────────────────────────────┘
```
- **Journey** (progression mode): a guided loop through the reading levels. Each level has two steps: **read 5 labels** (tap them), then **pass a Find It round** (8 of 10). Passing unlocks the next level. A small Journey card low and to the left shows the level, the current goal and a progress bar; when the round is due it shows **Start Find It**. Progress is saved.
- **Practice** (choose an exercise): **Explore** (labels only, tap to learn), **Find It** (with a 5 or 10 word round), **Word Bank** (review and find words in the room).
- Small actions under the main choices: **Progress**, **Settings**, **Exit** (tap twice: the first tap turns it rose and reads "Exit?").
- A brand-new learner skips the menu and starts the Journey, so the first thing they do is read labels. Later launches open the menu.

Window sizes: Menu 42 × 50 cm, Word Bank 45 × 55 cm, Progress 35 × 40 cm, Settings 42 × 56 cm, Journey card 34 × 17 cm, Find It results 40 × 24 cm.

### 5.1 Word Bank
- Header: "Word Bank" + count ("23 words").
- Filters as chips: **All · This room · Kitchen · Office · Living room**, and **New · Learning · Mastered**.
- List rows: word in target language (at current reading level) · meaning · mastery dots (○○○ to ●●●).
- Tapping a row highlights its real object if it's in view: the object's dot pulses `Accent` and a light beam points to it for 2 s. If not in view: "Not in this room."
- Empty state: "Look around your room. Words appear here as you tap labels."

### 5.2 Find It
- Starts straight from Practice or the Journey card; there is no idle window.
- During a round only the floating prompt shows: the word (target language, current reading level), score and streak under it, a timer bar, and Skip after 15 s.
- After a round: a results card with score, accuracy, words to review, **Play again** and **Done**. Done returns to where the round started (menu or Journey).

### 5.3 Progress
- Words learned (big number), current reading level, quiz accuracy, rooms explored, daily streak.
- A simple bar per category showing words learned out of available.
- "Level up" suggestion card when the user gets 80%+ accuracy on 20 words at the current level: "Ready for Level 2?" with **Try it** / **Not yet**.

### 5.4 Settings
- **I'm learning:** Mandarin / Spanish / French / German / English.
- **Interface language:** English / 简体中文 (auto-set to Chinese when learning English, can be changed).
- **Reading level:** 1 / 2 / 3 with a live preview of the chair label.
- **Show pinyin** (Mandarin only), **Gender colors**, **Audio**, **Label size** (S / M / L).
- **Advanced:** detection confidence slider, "Show room mesh" (debug, hidden in release).
- **Privacy note** (always visible at the bottom): "Object recognition runs on your headset. Word World never saves or sends camera images."

---

## 6. Find It quiz flow

1. User picks **Find It** in Practice or taps **Start Find It** on the Journey card. Labels fade out; small dots stay on recognized objects so the user knows what counts.
2. A prompt floats 60 cm in front of the user, slightly low: *la taza* / 杯子.
3. User touches the real object (hand overlaps the object's bounds) or points and pinches its dot.
4. **Correct:** a green ring expands from the object, the label pops in showing the word, a soft chime plays (if audio on), streak +1. Next prompt after 1 s.
5. **Wrong object:** that object's dot shakes briefly and shows its own word for 1.5 s (so wrong answers still teach). No penalty on the first miss.
6. **Hint** after 8 s: a soft `Hint`-colored glow pulses near the correct object.
7. **Skip** button after 15 s.
8. Prompts only use objects confirmed in this session. If fewer than 4 objects are recognized, show: "Look around a bit more so I can find some objects to quiz you on."

---

## 7. First-time experience (under 30 seconds)

1. **Language pick** (one window): "What do you want to learn?" Five large buttons with the language name in that language (中文, Español, Français, Deutsch, English).
2. **Short intro card:** "Look around your room. Objects you can learn will get labels." Animated hand showing a pinch. **Got it** button.
3. **Scanning hint:** while no objects are confirmed, a small floating line says "Look slowly around the room..." with a subtle sweep animation. Disappears when the first label appears.
4. **First label moment:** the first confirmed object gets a slightly larger label with a "Tap me" tooltip.
5. The Journey card is already showing "Tap 5 labels to read them", so the first tap is also the first Journey step.

Skip all of this on later launches (go straight to the menu).

---

## 8. Status and system feedback

- **Dock:** low in the user's view, two things only: the recognition pill and a **Home** button under it.
- **Recognition pill:** "● Recognizing objects" on mint. Tapping it pauses recognition ("Paused" on the plain surface). This doubles as the privacy indicator.
- **Not enough light / nothing found for 20 s:** "Having trouble seeing. Try turning on a light or moving closer."
- **Errors** say what happened and what to do: "Object recognition stopped. Tap to restart."
- **Home from anywhere:** the dock's Home button, or the left controller's X button, closes whatever is open and shows the menu in front of the user.

---

## 9. Motion and sound

- All transitions 0.2–0.3 s, ease-out. Nothing loops except the scanning dot pulse and the recognition indicator.
- Respect a "Reduce motion" setting: replace scaling and shaking with simple fades.
- Sounds (only if Audio is on): soft click on tap, chime on correct, low soft tone on wrong. Keep all UI sounds quiet (−18 dB relative to pronunciation audio).
- Pronunciation plays from the object's position in the room.

---

## 10. Interaction rules

- Every button works with **poke** (direct touch), **pinch-ray** and **controller trigger**.
- Minimum button size 3 × 3 cm; minimum spacing 1 cm.
- Hover state on everything interactive; light haptic on controllers for press.
- No artificial movement of the user. Windows never follow the head; the user moves them.
- Nothing appears closer than 40 cm to the user's eyes.

---

## 11. Bilingual UI

- All interface text comes from `ui_strings.json` (`en` and `zh`). No hard-coded strings.
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
