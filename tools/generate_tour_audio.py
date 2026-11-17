"""Generates the guided tour's narration (English and Mandarin) with Piper text-to-speech.

Runs on a PC as a build step, like tools/generate_audio.py, whose voices, settings and Mandarin
pinyin handling it reuses. Each tour step's caption in Assets/Data/ui_strings.json is also its
narration, so what is heard always matches what is shown. Writes
  Assets/Audio/Tour/<en|zh>/<step>.ogg       e.g. Assets/Audio/Tour/zh/labels.ogg
  Assets/Audio/Tour/manifest.json            text, voice and hash per clip
A clip is regenerated only when its text, pinyin or voice changes.

Run (same setup as generate_audio.py):
  Builds/piper-venv/Scripts/python.exe tools/generate_tour_audio.py [--force]
"""
import argparse, json, os, sys

import soundfile as sf
from piper import PiperVoice

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import generate_audio as ga  # noqa: E402  (voices, synthesize, pinyin_phonemes)

ROOT = ga.ROOT
STRINGS = os.path.join(ROOT, "Assets", "Data", "ui_strings.json")
OUT = os.path.join(ROOT, "Assets", "Audio", "Tour")

# Must match Tour.Steps in Assets/WordWorld/UI/Tour.cs.
STEPS = ["welcome", "labels", "detail", "status", "home", "journey", "practice", "settings", "replay"]

# Tone-marked pinyin for each Mandarin line, one syllable per character (with 一 tone changes
# applied), so the voice says exactly this rather than guessing readings. Keep in step with
# ui_strings.json; a line that does not line up falls back to Piper's own reading with a warning.
PINYIN = {
    "welcome": "Huānyíng láidào dāncí shìjiè! Wǒ dài nǐ kàn yi kàn.",
    "labels": "Fángjiān lǐ de wùpǐn shàngfāng huì chūxiàn dāncí. Diǎn yíxià fāguāng de dāncí lái xuéxí.",
    "detail": "Zhèlǐ yǒu yìsi hé lìjù. Kàn wán hòu guāndiào kǎpiàn.",
    "status": "Zhèlǐ xiǎnshì zhèngzài shíbié wùpǐn. Diǎn tā kěyǐ zàntíng huò jìxù.",
    "home": "Zhǔyè ànniǔ suíshí dǎkāi càidān. Xiànzài diǎn yíxià.",
    "journey": "Xuéxí zhī lǚ yí bù yí bù dài nǐ wánchéng měi gè děngjí.",
    "practice": "Liànxí lǐ yǒu zhǎo yi zhǎo, shuō yi shuō hé nǐ de dāncíběn.",
    "settings": "Shèzhì lǐ kěyǐ gǎi yǔyán, yuèdú děngjí hé biāoqiān dàxiǎo.",
    "replay": "Suíshí diǎn jiàochéng kěyǐ zài kàn yí biàn. Xué de kāixīn!",
}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--force", action="store_true", help="regenerate every clip")
    args = parser.parse_args()

    with open(STRINGS, encoding="utf-8") as f:
        table = {row["key"]: row for row in json.load(f)["strings"]}

    manifest_path = os.path.join(OUT, "manifest.json")
    manifest = {}
    if os.path.exists(manifest_path):
        with open(manifest_path, encoding="utf-8") as f:
            manifest = json.load(f).get("clips", {})

    made = skipped = 0
    warnings = []
    for lang in ("en", "zh"):
        voice_name = ga.VOICES[lang]
        model = os.path.join(ga.VOICE_DIR, voice_name + ".onnx")
        if not os.path.exists(model):
            sys.exit(f"missing voice {model}; see the setup notes in tools/generate_audio.py")
        voice = PiperVoice.load(model, download_dir=ga.VOICE_DIR)
        os.makedirs(os.path.join(OUT, lang), exist_ok=True)
        for step in STEPS:
            # Captions break lines for the card; spoken, a line break is just a space.
            text = " ".join(table[f"tour.{step}"][lang].split("\n")) if lang == "en" else table[f"tour.{step}"][lang].replace("\n", "")
            pinyin = PINYIN[step] if lang == "zh" else ""
            key = f"{lang}/{step}"
            path = os.path.join(OUT, lang, f"{step}.ogg")
            h = ga.clip_hash(f"{text}|{pinyin}", voice_name)
            if not args.force and manifest.get(key, {}).get("hash") == h and os.path.exists(path):
                skipped += 1
                continue
            phonemes = None
            if lang == "zh":
                phonemes = ga.pinyin_phonemes(pinyin, text)
                if phonemes is None:
                    warnings.append(f"{key}: pinyin doesn't line up with '{text}'; used Piper's own reading")
            audio, rate = ga.synthesize(voice, text, phonemes)
            sf.write(path, audio, rate, format="OGG", subtype="VORBIS")
            manifest[key] = {"text": text, "voice": voice_name, "hash": h, "seconds": round(len(audio) / rate, 2)}
            made += 1
            print(f"{key}  {manifest[key]['seconds']} s", flush=True)

    with open(manifest_path, "w", encoding="utf-8") as f:
        json.dump({"notes": "Written by tools/generate_tour_audio.py; do not edit by hand.",
                   "clips": dict(sorted(manifest.items()))}, f, ensure_ascii=False, indent=2)
    for w in warnings:
        print("warning:", w)
    print(f"made {made} clips, {skipped} unchanged, {len(warnings)} warnings")


if __name__ == "__main__":
    main()
