"""Generates Word World's pronunciation clips from the vocabulary sheet with Piper text-to-speech.

Runs on a PC as a build step; nothing here ships in the app except the .ogg files it writes.
Reads Assets/Languages/vocabulary.csv and writes, per language and class,
  Assets/Audio/Vocab/<lang>/<classIndex>_word.ogg      the Level 1 word (Mandarin: the characters)
  Assets/Audio/Vocab/<lang>/<classIndex>_phrase.ogg    the Level 2 phrase
  Assets/Audio/Vocab/<lang>/<classIndex>_sentence.ogg  the Level 3 / example sentence
plus Assets/Audio/Vocab/manifest.json (text, voice and hash per clip). A clip is regenerated only
when its text or voice changes, so re-running after a sheet edit is quick.

Setup (once; Piper's Python package needs Python 3.9-3.13):
  py -3.12 -m venv Builds/piper-venv
  Builds/piper-venv/Scripts/python.exe -m pip install piper-tts soundfile numpy
  download the voices listed in VOICES into Builds/piper-voices (.onnx and .onnx.json) from
  https://huggingface.co/rhasspy/piper-voices
Run:
  Builds/piper-venv/Scripts/python.exe tools/generate_audio.py [--lang es] [--force]

Voice licenses are recorded in docs/dependencies.md; check a voice's model card before changing it.
"""
import argparse, csv, hashlib, json, os, re, sys, unicodedata

import numpy as np
import soundfile as sf
from piper import PiperVoice, SynthesisConfig

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SHEET = os.path.join(ROOT, "Assets", "Languages", "vocabulary.csv")
OUT = os.path.join(ROOT, "Assets", "Audio", "Vocab")
VOICE_DIR = os.path.join(ROOT, "Builds", "piper-voices")

VOICES = {
    "zh": "zh_CN-chaowen-medium",
    "es": "es_MX-claude-high",
    "fr": "fr_FR-siwis-medium",
    "de": "de_DE-thorsten-medium",
    "en": "en_US-ljspeech-medium",
}
# Which sheet column each clip reads. Mandarin's Level 1-3 labels all name the word itself, so its
# "word" clip is the characters; PINYIN names the column whose pinyin is actually spoken.
COLUMNS = {
    "zh": {"word": "zh_characters", "phrase": "zh_phrase", "sentence": "zh_sentence"},
    **{lang: {"word": f"{lang}_word", "phrase": f"{lang}_phrase", "sentence": f"{lang}_sentence"}
       for lang in ("es", "fr", "de", "en")},
}
PINYIN = {"word": "zh_pinyin", "phrase": "zh_phrasePinyin", "sentence": "zh_sentencePinyin"}
# Slightly slower than Piper's default: these are for learners repeating after the voice.
LENGTH_SCALE = 1.12
TARGET_PEAK = 0.89          # about -1 dBFS, so every clip plays at a similar loudness
SILENCE = 0.01              # amplitude treated as silence when trimming
PAD_SECONDS = 0.08          # kept before and after the speech so starts are not clipped


def clip_hash(text, voice):
    return hashlib.sha1(f"{voice}|{LENGTH_SCALE}|{text}".encode("utf-8")).hexdigest()[:16]


# --- Mandarin: speak the sheet's reviewed pinyin, not Piper's guess --------------------------------
# Piper's Mandarin voices turn characters into pinyin with a model (g2pW), which can pick the wrong
# reading of a polyphonic character and never applies the 一/不 tone changes. The sheet already has
# hand-written, tone-marked pinyin for every word, phrase and sentence, so we feed that to the voice
# instead and only fall back to g2pW (with a warning) if the pinyin can't be lined up with the text.
TONE_MARKS = {}
for _plain, _marked in {"a": "āáǎà", "e": "ēéěè", "i": "īíǐì", "o": "ōóǒò", "u": "ūúǔù", "v": "ǖǘǚǜ"}.items():
    for _tone, _ch in enumerate(_marked, start=1):
        TONE_MARKS[_ch] = (_plain, _tone)
INITIALS = ["zh", "ch", "sh", "b", "p", "m", "f", "d", "t", "n", "l", "g", "k", "h",
            "j", "q", "x", "r", "z", "c", "s", "y", "w"]
# Finals as pinyin writes them after an initial (ü as v); the voice has a phoneme for each.
FINALS = ["a", "o", "e", "ai", "ei", "ao", "ou", "an", "en", "ang", "eng", "ong", "er",
          "i", "ia", "ie", "iao", "iu", "ian", "in", "iang", "ing", "iong",
          "u", "ua", "uo", "uai", "ui", "uan", "un", "uang", "ueng", "ue", "v", "ve", "van", "vn"]
PUNCTUATION = {",": ",", ".": ".", "?": "?", "!": "!", ";": ";", ":": ":"}


def _strip_tones(word):
    """'yǐzi' -> ('yizi', [0, 3, 0, 0]): plain letters (ü as v) and the tone mark on each letter."""
    letters, tones = [], []
    for ch in unicodedata.normalize("NFC", word.lower()):
        if ch in TONE_MARKS:
            plain, tone = TONE_MARKS[ch]
        else:
            plain, tone = ("v" if ch == "ü" else ch), 0
        letters.append(plain)
        tones.append(tone)
    return "".join(letters), tones


def _syllables(word):
    """Every way to split one pinyin word into initial+final syllables with at most one tone mark each.
    A syllable after the first must start with a consonant; pinyin writes an apostrophe otherwise."""
    letters, tones = _strip_tones(word.replace("'", "").replace("’", ""))
    # Apostrophe positions, counted in letters with the apostrophes removed.
    breaks, seen = set(), 0
    for ch in word.replace("’", "'"):
        if ch == "'":
            breaks.add(seen)
        else:
            seen += 1
    results = []

    def walk(start, parts):
        if start == len(letters):
            results.append(parts)
            return
        for end in range(len(letters), start, -1):  # longest first
            chunk = letters[start:end]
            initial = next((i for i in INITIALS if chunk.startswith(i) and len(chunk) > len(i)), "")
            final = chunk[len(initial):]
            # Erhua: 'nǎr' is two characters (哪儿), spoken as na3 + a light er.
            erhua = final.endswith("r") and final != "er" and final[:-1] in FINALS
            if erhua:
                final = final[:-1]
            if final not in FINALS or (start and not initial and start not in breaks):
                continue
            marks = [t for t in tones[start:end] if t]
            if len(marks) > 1:
                continue
            syllable = [(initial or "Ø", final, str(marks[0] if marks else 5))]
            if erhua:
                syllable.append(("Ø", "er", "5"))
            walk(end, parts + syllable)

    walk(0, [])
    return results


def pinyin_phonemes(pinyin, characters):
    """Phonemes for Piper's Mandarin voices from tone-marked pinyin, or None if the pinyin doesn't have
    exactly one syllable per Han character in `characters`."""
    han = sum(1 for ch in characters if "一" <= ch <= "鿿")
    tokens = re.findall(r"[^\s,.?!;:，。？！；：]+|[,.?!;:，。？！；：]", pinyin)
    options = []
    for token in tokens:
        if token in PUNCTUATION or token in "，。？！；：":
            options.append([[("punct", {"，": ",", "。": ".", "？": "?", "！": "!", "；": ";", "：": ":"}.get(token, token))]])
            continue
        splits = _syllables(token)
        if not splits:
            return None
        options.append(splits)
    # Pick the split whose total syllable count matches the characters; prefer the longest syllables.
    best = None

    def choose(i, picked, count):
        nonlocal best
        if best is not None or count > han:
            return
        if i == len(options):
            if count == han:
                best = picked
            return
        for split in options[i]:
            n = 0 if split and split[0][0] == "punct" else len(split)
            choose(i + 1, picked + [split], count + n)

    choose(0, [], 0)
    if best is None:
        return None
    phonemes = []
    for split in best:
        for part in split:
            if part[0] == "punct":
                phonemes.append(part[1])
            else:
                phonemes.extend(part)
    return [phonemes]


def synthesize(voice, text, phonemes=None):
    config = SynthesisConfig(length_scale=LENGTH_SCALE)
    if phonemes is not None:
        voice.phonemize = lambda _text: phonemes  # speak exactly these phonemes
    try:
        chunks = list(voice.synthesize(text, syn_config=config))
    finally:
        voice.__dict__.pop("phonemize", None)
    rate = chunks[0].sample_rate
    gap = np.zeros(int(rate * 0.15), dtype=np.float32)  # between sentences, if Piper splits the text
    parts = []
    for i, chunk in enumerate(chunks):
        if i:
            parts.append(gap)
        parts.append(chunk.audio_float_array.astype(np.float32))
    audio = np.concatenate(parts)

    loud = np.flatnonzero(np.abs(audio) > SILENCE)
    if len(loud):
        pad = int(rate * PAD_SECONDS)
        audio = audio[max(0, loud[0] - pad): loud[-1] + pad]
    peak = float(np.max(np.abs(audio))) if len(audio) else 0.0
    if peak > 0:
        audio = audio * (TARGET_PEAK / peak)
    return audio, rate


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--lang", choices=sorted(VOICES), help="only this language")
    parser.add_argument("--force", action="store_true", help="regenerate every clip")
    args = parser.parse_args()

    with open(SHEET, encoding="utf-8-sig", newline="") as f:
        rows = list(csv.DictReader(f))

    manifest_path = os.path.join(OUT, "manifest.json")
    manifest = {}
    if os.path.exists(manifest_path):
        with open(manifest_path, encoding="utf-8") as f:
            manifest = json.load(f).get("clips", {})

    made = skipped = 0
    warnings = []
    for lang, voice_name in VOICES.items():
        if args.lang and lang != args.lang:
            continue
        model = os.path.join(VOICE_DIR, voice_name + ".onnx")
        if not os.path.exists(model):
            sys.exit(f"missing voice {model}; see the setup notes at the top of this file")
        # Mandarin's phonemizer downloads its g2pW model into download_dir on first use.
        voice = PiperVoice.load(model, download_dir=VOICE_DIR)
        os.makedirs(os.path.join(OUT, lang), exist_ok=True)
        for row in rows:
            index = int(row["classIndex"])
            for kind, column in COLUMNS[lang].items():
                text = row[column].strip()
                pinyin = row[PINYIN[kind]].strip() if lang == "zh" else ""
                key = f"{lang}/{index}_{kind}"
                path = os.path.join(OUT, lang, f"{index}_{kind}.ogg")
                h = clip_hash(f"{text}|{pinyin}", voice_name)
                if not args.force and manifest.get(key, {}).get("hash") == h and os.path.exists(path):
                    skipped += 1
                    continue
                phonemes = None
                if lang == "zh":
                    phonemes = pinyin_phonemes(pinyin, text)
                    if phonemes is None:
                        warnings.append(f"{key}: pinyin '{pinyin}' doesn't line up with '{text}'; used Piper's own reading")
                audio, rate = synthesize(voice, text, phonemes)
                sf.write(path, audio, rate, format="OGG", subtype="VORBIS")
                manifest[key] = {"text": text, "voice": voice_name, "hash": h,
                                 "seconds": round(len(audio) / rate, 2)}
                made += 1
            print(f"{lang} {index:2d} {row['cocoLabel']}", flush=True)

    with open(manifest_path, "w", encoding="utf-8") as f:
        json.dump({"notes": "Written by tools/generate_audio.py; do not edit by hand.",
                   "voices": VOICES, "lengthScale": LENGTH_SCALE,
                   "clips": dict(sorted(manifest.items()))}, f, ensure_ascii=False, indent=2)
    for w in warnings:
        print("warning:", w)
    print(f"made {made} clips, {skipped} unchanged, {len(warnings)} warnings")


if __name__ == "__main__":
    main()
