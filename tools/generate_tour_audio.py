"""Generates the guided tour's narration (English and Mandarin) with text-to-speech.

Three engines:
  kokoro      (default) Kokoro-82M, an open (Apache-2.0) model that runs offline and sounds far
              more natural than Piper (Nov 23 review: the narration sounded stiff). English uses
              Kokoro v1.0's "af_heart" voice; Mandarin uses the Mandarin-tuned v1.1-zh model.
  piper       the older offline Piper voices, shared with tools/generate_audio.py.
  elevenlabs  ElevenLabs' multilingual model. Needs an API key in the ELEVENLABS_API_KEY
              environment variable (never stored in the repo); only the tour captions are sent.
              Commercial use of the audio needs a paid ElevenLabs plan.

Runs on a PC as a build step, like tools/generate_audio.py (whose voices and Mandarin pinyin
handling the piper engine reuses). Each tour step's caption in Assets/Data/ui_strings.json is also its
narration, so what is heard always matches what is shown. Writes
  Assets/Audio/Tour/<en|zh>/<step>.ogg       e.g. Assets/Audio/Tour/zh/labels.ogg
  Assets/Audio/Tour/manifest.json            text, voice and hash per clip
A clip is regenerated only when its text, pinyin or voice changes.

Run:
  Kokoro (setup once; Python 3.10-3.12, about 1.5 GB with CPU-only PyTorch):
    py -3.12 -m venv Builds/kokoro-venv
    Builds/kokoro-venv/Scripts/python.exe -m pip install torch --index-url https://download.pytorch.org/whl/cpu
    Builds/kokoro-venv/Scripts/python.exe -m pip install "kokoro>=0.9.4" "misaki[zh]" soundfile
    Builds/kokoro-venv/Scripts/python.exe tools/generate_tour_audio.py [--force]
  Piper (same setup as generate_audio.py):
    Builds/piper-venv/Scripts/python.exe tools/generate_tour_audio.py --engine piper [--force]
  ElevenLabs ($env:ELEVENLABS_API_KEY = "..." first):
    Builds/piper-venv/Scripts/python.exe tools/generate_tour_audio.py --engine elevenlabs [--voice ID] [--force]
"""
import argparse, hashlib, json, os, sys, urllib.error, urllib.request

import numpy as np
import soundfile as sf

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
# Loaded only for the piper engine (each engine lives in its own virtual environment).
ga = PiperVoice = None
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


# Kokoro: a warm, natural American English narrator, and a Mandarin voice from the v1.1-zh model,
# which reads Mandarin far better than v1.0's. Both at Kokoro's 24 kHz.
KOKORO = {
    "en": {"lang": "a", "repo": "hexgrad/Kokoro-82M", "voice": "af_heart", "speed": 1.0},
    "zh": {"lang": "z", "repo": "hexgrad/Kokoro-82M-v1.1-zh", "voice": "zf_001", "speed": 1.0},
}
KOKORO_RATE = 24000


def kokoro_pipeline(lang):
    from kokoro import KPipeline  # only in Builds/kokoro-venv
    settings = KOKORO[lang]
    return KPipeline(lang_code=settings["lang"], repo_id=settings["repo"])


def kokoro_synthesize(pipeline, lang, text):
    """Returns (float32 samples, rate); Kokoro speaks long text in chunks, joined with short pauses."""
    settings = KOKORO[lang]
    gap = np.zeros(int(KOKORO_RATE * 0.12), dtype=np.float32)
    parts = []
    for result in pipeline(text, voice=settings["voice"], speed=settings["speed"]):
        audio = result.audio if hasattr(result, "audio") else result[2]
        if audio is None:
            continue
        parts.append(np.asarray(audio, dtype=np.float32))
        parts.append(gap)
    if not parts:
        sys.exit(f"Kokoro produced no audio for '{text}'")
    audio = np.concatenate(parts[:-1])
    # Quiet lead-in and tail trimmed to 80 ms, so steps start promptly.
    loud = np.flatnonzero(np.abs(audio) > 0.01)
    if len(loud):
        pad = int(KOKORO_RATE * 0.08)
        audio = audio[max(0, loud[0] - pad):loud[-1] + pad]
    peak = float(np.max(np.abs(audio)))
    if peak > 0:
        audio = audio * (0.89 / peak)  # about -1 dBFS, like the Piper clips
    return audio, KOKORO_RATE


def clip_hash(text, voice):
    return hashlib.sha1(f"{voice}|{text}".encode("utf-8")).hexdigest()[:16]


# ElevenLabs: one warm, conversational voice for both languages (the multilingual model speaks
# Mandarin with the same voice). Override with --voice to try others from the ElevenLabs library.
ELEVEN_MODEL = "eleven_multilingual_v2"
ELEVEN_VOICE = "21m00Tcm4TlvDq8ikWAM"  # "Rachel", an ElevenLabs premade voice
ELEVEN_RATE = 24000
# Slightly more expressive than the defaults, but steady enough for instructions.
ELEVEN_SETTINGS = {"stability": 0.45, "similarity_boost": 0.8, "style": 0.25, "use_speaker_boost": True}


def eleven_synthesize(text, voice_id, key):
    """Returns (float32 samples, rate) for one line from the ElevenLabs text-to-speech API."""
    url = f"https://api.elevenlabs.io/v1/text-to-speech/{voice_id}?output_format=pcm_{ELEVEN_RATE}"
    body = json.dumps({"text": text, "model_id": ELEVEN_MODEL, "voice_settings": ELEVEN_SETTINGS}).encode("utf-8")
    request = urllib.request.Request(url, data=body, method="POST", headers={
        "xi-api-key": key, "Content-Type": "application/json", "Accept": "audio/pcm"})
    try:
        with urllib.request.urlopen(request, timeout=60) as response:
            pcm = response.read()
    except urllib.error.HTTPError as e:
        sys.exit(f"ElevenLabs refused the request ({e.code}): {e.read().decode('utf-8', 'replace')[:300]}")
    audio = np.frombuffer(pcm, dtype="<i2").astype(np.float32) / 32768.0
    # A short fade at each end, so clips never start or stop with a click.
    fade = min(len(audio) // 2, ELEVEN_RATE // 100)
    if fade:
        ramp = np.linspace(0.0, 1.0, fade, dtype=np.float32)
        audio[:fade] *= ramp
        audio[-fade:] *= ramp[::-1]
    return audio, ELEVEN_RATE


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--force", action="store_true", help="regenerate every clip")
    parser.add_argument("--engine", choices=["kokoro", "piper", "elevenlabs"], default="kokoro")
    parser.add_argument("--voice", default=ELEVEN_VOICE, help="ElevenLabs voice ID (elevenlabs engine)")
    args = parser.parse_args()
    eleven_key = None
    if args.engine == "elevenlabs":
        eleven_key = os.environ.get("ELEVENLABS_API_KEY")
        if not eleven_key:
            sys.exit("set ELEVENLABS_API_KEY to use --engine elevenlabs")

    with open(STRINGS, encoding="utf-8") as f:
        table = {row["key"]: row for row in json.load(f)["strings"]}

    manifest_path = os.path.join(OUT, "manifest.json")
    manifest = {}
    if os.path.exists(manifest_path):
        with open(manifest_path, encoding="utf-8") as f:
            manifest = json.load(f).get("clips", {})

    made = skipped = 0
    warnings = []
    if args.engine == "piper":
        global ga, PiperVoice
        from piper import PiperVoice  # only in Builds/piper-venv
        import generate_audio as ga  # voices, synthesize, pinyin_phonemes

    for lang in ("en", "zh"):
        voice = None
        if args.engine == "kokoro":
            k = KOKORO[lang]
            voice_name = f"kokoro:{k['repo'].split('/')[-1]}:{k['voice']}:{k['speed']}"
            voice = kokoro_pipeline(lang)
        elif eleven_key:
            voice_name = f"elevenlabs:{ELEVEN_MODEL}:{args.voice}"
        else:
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
            h = ga.clip_hash(f"{text}|{pinyin}", voice_name) if args.engine == "piper" else clip_hash(text, voice_name)
            if not args.force and manifest.get(key, {}).get("hash") == h and os.path.exists(path):
                skipped += 1
                continue
            if args.engine == "kokoro":
                # Kokoro reads the characters itself (misaki G2P); listen for polyphones.
                audio, rate = kokoro_synthesize(voice, lang, text)
            elif eleven_key:
                # ElevenLabs reads the characters itself; listen for polyphones (e.g. 一, 了).
                audio, rate = eleven_synthesize(text, args.voice, eleven_key)
            else:
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
