# DESIGN.md: Word World (单词世界)

How Word World should look, feel and flow. Read this before building any UI, labels, panels or feedback effects. `CLAUDE.md` covers what to build; this file covers how it should look and behave. If they conflict on visual or UX details, this file wins.

---

## 1. Design goals

1. **Reading comes first.** Every label must be easy to read at a glance, from across a room, in both Latin script and Chinese characters.
2. **The real room is the lesson.** Virtual elements stay small, light and attached to real objects. Never cover the room with large UI.
3. **Calm and clear.** A study tool, not an arcade. Motion is short and purposeful.
4. **Always show what the app is doing:** scanning, recognized, unsure, correct, wrong.
5. **Works for a first-time headset user** within 30 seconds.

---

## 2. Visual style

**Theme name:** "Field notebook." Clean paper-like cards floating in the room, with one green ink accent.

### Colors (define once as tokens in a single `WordWorldTheme` ScriptableObject)
| Token | Hex | Use |
|---|---|---|
| `Card` | `#FFFFFF` at 88% opacity | Label and panel backgrounds |
| `CardDark` | `#1A201D` at 88% opacity | Optional dark-room theme |
| `Ink` | `#1D2420` | Main text |
| `InkMuted` | `#5F6B64` | Secondary text (meaning, pinyin on detail card) |
| `Accent` | `#1F6B52` | Pinyin on labels, selected states, progress |
| `AccentSoft` | `#E2EFE8` | Hover and selected backgrounds |
| `Correct` | `#2E9E5B` | Quiz correct |
| `Hint` | `#E5B765` | Hint glow, "unsure" state |
| `Wrong` | `#C8553D` | Quiz wrong (used briefly, never as a big fill) |

Gender color coding (optional, toggle in Settings, off by default): masculine `#3A6EA5`, feminine `#B0436E`, neuter `#6B7F3A`. Applied only to the article text, never the whole label.

### Typography
- **One font for everything:** Noto Sans SC (covers Latin, accented letters and simplified Chinese). Create a TextMeshPro dynamic font asset with fallback enabled. Test that é, ü, ñ, ç, ß and tone-marked pinyin (ǎ, ǚ, ǜ) all render.
- Weights: Medium for label words, Regular for everything else, Bold only for panel titles and quiz prompts.
- Size in **angular terms**, not fixed meters, so labels read the same at any distance (see section 3).

### Shape and depth
- Rounded corners: 12 mm radius on panels, 8 mm on labels.
- Soft shadow under cards, no hard outlines.
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

Text comes from `VocabularyDatabase.GetLabel()`. Never hard-code vocabulary in UI scripts. Level-3 sentences wrap at about 18 characters per line, maximum 2 lines.

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

## 5. Panels (multi-window, spatial mode)

Four separate windows through the spatial adapter. They open as a **curved row at about 1.2 m from the user, slightly below eye level**, and the user can grab and move each one. Window positions are saved between sessions.

Default layout (user's view):
```
   [Word Bank]   [Quiz]   [Progress]
                [Settings]  (opened from a gear button, not shown by default)
```
Window sizes: Word Bank 45 × 55 cm, Quiz 40 × 30 cm, Progress 35 × 40 cm, Settings 40 × 50 cm.

All windows share one state store. Any change updates every window and the labels immediately.

### 5.1 Word Bank
- Header: "Word Bank" + count ("23 words").
- Filters as chips: **All · This room · Kitchen · Office · Living room**, and **New · Learning · Mastered**.
- List rows: word in target language (at current reading level) · meaning · mastery dots (○○○ to ●●●).
- Tapping a row highlights its real object if it's in view: the object's dot pulses `Accent` and a light beam points to it for 2 s. If not in view: "Not in this room."
- Empty state: "Look around your room. Words appear here as you tap labels."

### 5.2 Quiz
- Idle: "Find It" title, round length (10), best streak, big **Start** button.
- During a round: the prompt in large text (target language, current reading level), timer bar, score (3/10), streak flame count.
- After a round: score, accuracy, words to review, **Play again** and **Done**.

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

1. User taps **Start** in the Quiz window. Labels fade out; small dots stay on recognized objects so the user knows what counts.
2. A prompt appears both in the Quiz window and floating 60 cm in front of the user: *la taza* / 杯子.
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
5. After the first tap, the Word Bank, Quiz and Progress windows open in their default row.

Skip all of this on later launches (go straight to labels + windows).

---

## 8. Status and system feedback

- **Recognition indicator:** a small chip at the bottom of the user's view: "● Recognizing objects" (Accent dot). Tapping it pauses recognition ("Paused" in grey). This doubles as the privacy indicator.
- **Not enough light / nothing found for 20 s:** "Having trouble seeing. Try turning on a light or moving closer."
- **Errors** say what happened and what to do: "Object recognition stopped. Tap to restart."
- **Hand menu:** palm-up gesture shows three buttons on the wrist: Windows (show/hide all), Pause recognition, Settings. Controller fallback: menu button.

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
- Don't fill the room with windows on launch; the first-time flow opens them one step at a time.

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
