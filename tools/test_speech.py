"""Offline check of the Say It speech check: plays every generated pronunciation clip into Vosk with
the same grammar and scoring the app uses (Assets/WordWorld/Audio/SpeechScorer.cs), and reports
  - accept: the clip of the expected line passes;
  - reject: the clip of a *different* object's word, checked against this word, fails.
A synthetic voice is not a learner, so this only proves the grammar and scoring are wired correctly
and gives starting thresholds; docs/audio-plan.md step 6 still needs real speakers.

Run (after tools/generate_audio.py and tools/build_speech.py):
  Builds/piper-venv/Scripts/python.exe tools/test_speech.py [--lang es] [--level 1]
"""
import argparse, csv, json, os, random

import numpy as np
import soundfile as sf
import vosk

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SPEECH = os.path.join(ROOT, "Assets", "Data", "speech.json")
SHEET = os.path.join(ROOT, "Assets", "Languages", "vocabulary.csv")
CLIPS = os.path.join(ROOT, "Assets", "Audio", "Vocab")
MODEL_DIR = os.path.join(ROOT, "Builds", "vosk", "models")
RATE = 16000

# Mirrors SpeechScorer.cs.
CLEAR = 0.75
PASS_RATIO = 0.8


def kind_for(lang, level):
    """Mirrors VocabClips.ForSayIt: Mandarin always says the measure-word phrase."""
    if lang == "zh":
        return "phrase"
    return "word" if level <= 1 else "phrase" if level == 2 else "sentence"


def known(entry, line):
    unknown = set(entry.get("unknown", "").split())
    return " ".join(t for t in line.split() if t not in unknown)


def grammar(entries, target, kind):
    """What Vosk may hear: the expected line, each of its words on its own (so a partly right
    answer is still heard word by word), every other object's word, and [unk] for anything else."""
    phrases = {known(target, target[kind])}
    phrases.update(known(target, target[kind]).split())
    for e in entries:
        for k in ("word", "noun"):
            phrases.add(known(e, e.get(k, "")))
    phrases.discard("")
    return sorted(phrases) + ["[unk]"]


def units(lang, line):
    """Scoring units: characters for Mandarin, words otherwise."""
    return [c for c in line.replace(" ", "")] if lang == "zh" else line.split()


def heard_units(lang, words):
    out = []
    for w in words:
        if w["word"] == "[unk]":
            out.append(("?", w["conf"]))
        elif lang == "zh":
            out.extend((c, w["conf"]) for c in w["word"])
        else:
            out.append((w["word"], w["conf"]))
    return out


def align(expected, heard):
    """LCS alignment: for each expected unit, the index of the heard unit matched to it, or -1."""
    n, m = len(expected), len(heard)
    dp = [[0] * (m + 1) for _ in range(n + 1)]
    for i in range(n - 1, -1, -1):
        for j in range(m - 1, -1, -1):
            dp[i][j] = dp[i + 1][j + 1] + 1 if expected[i] == heard[j][0] else max(dp[i + 1][j], dp[i][j + 1])
    match, i, j = [-1] * n, 0, 0
    while i < n and j < m:
        if expected[i] == heard[j][0]:
            match[i] = j
            i += 1
            j += 1
        elif dp[i + 1][j] >= dp[i][j + 1]:
            i += 1
        else:
            j += 1
    return match


def score(lang, kind, entry, words):
    unknown = set(entry.get("unknown", "").split())
    expected = [u for u in units(lang, entry[kind]) if u not in unknown]
    heard = heard_units(lang, words)
    match = align(expected, heard)
    matched = sum(1 for k in match if k >= 0)
    if kind == "word":
        ok = matched == len(expected)
        noun = [u for u in units(lang, entry.get("noun", "")) if u not in unknown]
        if not ok and noun and all(k >= 0 for k in align(noun, heard)):
            ok = True
        return ok
    key = [u for u in units(lang, entry[kind + "Key"]) if u not in unknown]
    key_ok = all(k >= 0 for k in align(key, heard))
    return key_ok and matched >= needed(len(expected))


def needed(n):
    """80% of the words, rounded down, so 'a cup' passes on 'cup' (the missing 'a' is still flagged)."""
    return max(1, int(PASS_RATIO * n))


def load(path):
    audio, rate = sf.read(path, dtype="float32")
    if audio.ndim > 1:
        audio = audio.mean(axis=1)
    if rate != RATE:
        t = np.arange(0, len(audio) * RATE / rate) * rate / RATE
        audio = np.interp(t, np.arange(len(audio)), audio).astype(np.float32)
    # A real recording has room noise and silence around the speech; the clips are trimmed tight.
    pad = np.zeros(int(RATE * 0.4), dtype=np.float32)
    audio = np.concatenate([pad, audio, pad]) + np.random.normal(0, 0.002, len(audio) + 2 * len(pad)).astype(np.float32)
    return (np.clip(audio, -1, 1) * 32767).astype(np.int16).tobytes()


def recognize(model, phrases, pcm):
    rec = vosk.KaldiRecognizer(model, RATE, json.dumps(phrases, ensure_ascii=False))
    rec.SetWords(True)
    rec.AcceptWaveform(pcm)
    return json.loads(rec.FinalResult()).get("result", [])


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--lang")
    parser.add_argument("--level", type=int, choices=(1, 2, 3))
    args = parser.parse_args()
    vosk.SetLogLevel(-1)
    random.seed(1)
    with open(SPEECH, encoding="utf-8") as f:
        speech = json.load(f)
    with open(SHEET, encoding="utf-8-sig", newline="") as f:
        enabled = {int(r["classIndex"]) for r in csv.DictReader(f) if r["enabled"].strip().upper() == "TRUE"}

    for block in speech["languages"]:
        lang = block["language"]
        if args.lang and lang != args.lang:
            continue
        model = vosk.Model(os.path.join(MODEL_DIR, block["model"]))
        entries = [e for e in block["entries"] if e["classIndex"] in enabled]
        for level in (1, 2, 3):
            if (args.level and level != args.level) or (lang == "zh" and level > 1):
                continue
            kind = kind_for(lang, level)
            accept = reject = 0
            misses = []
            for entry in entries:
                phrases = grammar(entries, entry, kind)
                own = load(os.path.join(CLIPS, lang, f"{entry['classIndex']}_{kind}.ogg"))
                words = recognize(model, phrases, own)
                if score(lang, kind, entry, words):
                    accept += 1
                else:
                    misses.append(f"{entry['classIndex']} '{entry[kind]}' heard '{' '.join(w['word'] for w in words)}'")
                other = random.choice([e for e in entries if e["classIndex"] != entry["classIndex"]])
                wrong = load(os.path.join(CLIPS, lang, f"{other['classIndex']}_{kind}.ogg"))
                if not score(lang, kind, entry, recognize(model, phrases, wrong)):
                    reject += 1
            n = len(entries)
            print(f"{lang} level {level} ({kind}): accept {accept}/{n}, reject {reject}/{n}", flush=True)
            for m in misses:
                print("   missed", m)


if __name__ == "__main__":
    main()
