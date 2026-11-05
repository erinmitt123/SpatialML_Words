"""Builds Assets/Data/speech.json: what the learner is expected to say for every word, phrase and
sentence, split into the words each Vosk speech model knows (docs/audio-plan.md, "Checking an answer").

The app's speech check compares what Vosk heard against these word lists, and builds Vosk's grammar
(the list of things it may hear) from them. Splitting is done here, against the real model
vocabularies, so the headset never has to guess and every unknown word is reported now.

Setup: the tools venv from tools/generate_audio.py, plus
  Builds/piper-venv/Scripts/python.exe -m pip install vosk
  the five small models unzipped into Builds/vosk/models (see MODELS below; https://alphacephei.com/vosk/models)
Run:
  Builds/piper-venv/Scripts/python.exe tools/build_speech.py
"""
import csv, json, os, re, sys, unicodedata

import vosk

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SHEET = os.path.join(ROOT, "Assets", "Languages", "vocabulary.csv")
OUT = os.path.join(ROOT, "Assets", "Data", "speech.json")
MODEL_DIR = os.path.join(ROOT, "Builds", "vosk", "models")

MODELS = {
    "zh": "vosk-model-small-cn-0.22",
    "es": "vosk-model-small-es-0.42",
    "fr": "vosk-model-small-fr-0.22",
    "de": "vosk-model-small-de-0.15",
    "en": "vosk-model-small-en-us-0.15",
}
PUNCTUATION = ".,!?¿¡;:«»\"“”()…—–"
ZH_PUNCTUATION = "，。？！；：、“”（）《》…—"


def latin_tokens(text):
    """'Siéntate en la silla, por favor.' -> ['siéntate', 'en', 'la', 'silla', 'por', 'favor']"""
    text = unicodedata.normalize("NFC", text).lower().replace("’", "'")
    tokens = []
    for raw in text.split():
        token = raw.strip(PUNCTUATION)
        if token:
            tokens.append(token)
    return tokens


def split_unknown(model, tokens):
    """Hyphenated words the model doesn't know ('est-ce') are tried as their parts."""
    out = []
    for token in tokens:
        if model.vosk_model_find_word(token) < 0 and "-" in token:
            out.extend(t for t in token.split("-") if t)
        else:
            out.append(token)
    return out


def zh_tokens(model, text):
    """Longest-match split of characters into words the Mandarin model knows."""
    chars = [c for c in unicodedata.normalize("NFC", text) if c not in ZH_PUNCTUATION and not c.isspace()]
    tokens, i = [], 0
    while i < len(chars):
        for n in range(min(6, len(chars) - i), 0, -1):
            word = "".join(chars[i:i + n])
            if n == 1 or model.vosk_model_find_word(word) >= 0:
                tokens.append(word)
                i += n
                break
    return tokens


def find_key(tokens, noun_tokens):
    """The words of a phrase or sentence that name the object: the noun, also when it is elided
    (l'oiseau) or inflected (den Teddybären). A phrase or sentence only passes if these were said."""
    if not noun_tokens:
        return []
    head = noun_tokens[-1]
    key = []
    for t in tokens:
        bare = t.split("'")[-1]
        common = 0
        for a, b in zip(bare, head):
            if a != b:
                break
            common += 1
        if bare == head or (common >= 3 and common >= min(len(bare), len(head)) - 2):
            key.append(t)
    return key


def main():
    vosk.SetLogLevel(-1)
    with open(SHEET, encoding="utf-8-sig", newline="") as f:
        rows = [r for r in csv.DictReader(f)]

    db = {"notes": "Built from Assets/Languages/vocabulary.csv by tools/build_speech.py; do not edit by hand. "
                   "Each line is split into the words its Vosk model knows.",
          "models": MODELS, "languages": []}
    unknown = []
    for lang, model_name in MODELS.items():
        path = os.path.join(MODEL_DIR, model_name)
        if not os.path.isdir(path):
            sys.exit(f"missing model {path}; see the setup notes at the top of this file")
        model = vosk.Model(path)
        entries = []
        for row in rows:
            index = int(row["classIndex"])
            if lang == "zh":
                lines = {"word": zh_tokens(model, row["zh_characters"]),
                         "phrase": zh_tokens(model, row["zh_phrase"]),
                         "sentence": zh_tokens(model, row["zh_sentence"])}
            else:
                lines = {kind: split_unknown(model, latin_tokens(row[f"{lang}_{kind}"]))
                         for kind in ("word", "phrase", "sentence")}
                # Level 1 also accepts the noun without its article (docs/audio-plan.md).
                noun = row.get(f"{lang}_noun", "").strip()
                lines["noun"] = split_unknown(model, latin_tokens(noun)) if noun else []
            if lang == "zh":
                # Mandarin is scored per character; the key is the word's characters.
                keys = {k: lines["word"] for k in ("phrase", "sentence")}
            else:
                noun = lines["noun"] or lines["word"][-1:]
                keys = {k: find_key(lines[k], noun) for k in ("phrase", "sentence")}
                for k, key in keys.items():
                    if not key:
                        unknown.append(f"{lang} {index} {row['cocoLabel']} {k}: no word names the object; check the sheet")
            missing = set()
            for kind, tokens in lines.items():
                for token in tokens:
                    if model.vosk_model_find_word(token) < 0:
                        missing.add(token)
                        unknown.append(f"{lang} {index} {row['cocoLabel']} {kind}: '{token}'")
            # The app leaves unknown words out of the grammar and the score (they can never be heard).
            entries.append({"classIndex": index, **{k: " ".join(v) for k, v in lines.items()},
                            "phraseKey": " ".join(keys["phrase"]), "sentenceKey": " ".join(keys["sentence"]),
                            "unknown": " ".join(sorted(missing))})
        # A list rather than a dictionary: Unity's JsonUtility can't read dictionaries.
        db["languages"].append({"language": lang, "model": model_name, "entries": entries})
        print(f"{lang}: {len(entries)} entries", flush=True)

    db["unknownWords"] = unknown
    with open(OUT, "w", encoding="utf-8") as f:
        json.dump(db, f, ensure_ascii=False, indent=1)
    for line in unknown:
        print("unknown to the model:", line)
    print(f"wrote {OUT}; {len(unknown)} words the models don't know (those can't be recognized)")


if __name__ == "__main__":
    main()
