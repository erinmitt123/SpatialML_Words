"""Downloads what the Say It speech check needs and puts it where the Unity build picks it up.

  Assets/Plugins/Android/vosk-android.aar   Vosk speech recognition (Apache-2.0), arm64-v8a only
  Assets/Plugins/Android/jna.aar            JNA, which Vosk calls through (Apache-2.0 / LGPL-2.1), arm64-v8a only
  Assets/StreamingAssets/vosk/<lang>.zip    the five small Vosk models (Apache-2.0), unpacked on the headset
                                            the first time Say It is used in that language
  Builds/vosk/models/<model>/               the same models unzipped, for tools/build_speech.py and tools/test_speech.py

The models are about 210 MB together, so they are not committed (see .gitignore); run this once
after cloning. Without them the app still builds and runs, and simply hides Say It.
Run: py -3 tools/fetch_speech.py
"""
import os, shutil, sys, urllib.request, zipfile

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DOWNLOADS = os.path.join(ROOT, "Builds", "vosk")
PLUGINS = os.path.join(ROOT, "Assets", "Plugins", "Android")
STREAMING = os.path.join(ROOT, "Assets", "StreamingAssets", "vosk")

LIBRARIES = {
    "vosk-android.aar": "https://repo1.maven.org/maven2/com/alphacephei/vosk-android/0.3.75/vosk-android-0.3.75.aar",
    "jna.aar": "https://repo1.maven.org/maven2/net/java/dev/jna/jna/5.18.1/jna-5.18.1.aar",
}
MODELS = {
    "zh": "vosk-model-small-cn-0.22",
    "es": "vosk-model-small-es-0.42",
    "fr": "vosk-model-small-fr-0.22",
    "de": "vosk-model-small-de-0.15",
    "en": "vosk-model-small-en-us-0.15",
}
ABI = "arm64-v8a"  # Swan is arm64; the other ABIs would only make the APK bigger.


def download(url, path):
    if os.path.exists(path):
        return
    print("downloading", url, flush=True)
    tmp = path + ".part"
    with urllib.request.urlopen(url) as response, open(tmp, "wb") as f:
        shutil.copyfileobj(response, f)
    os.replace(tmp, path)


def strip_abis(source, target):
    """Copies an .aar, keeping native libraries for ABI only."""
    with zipfile.ZipFile(source) as src, zipfile.ZipFile(target, "w", zipfile.ZIP_DEFLATED) as dst:
        for item in src.infolist():
            if item.filename.startswith("jni/") and not item.filename.startswith(f"jni/{ABI}/") and item.filename != "jni/":
                continue
            dst.writestr(item, src.read(item.filename))


def main():
    os.makedirs(DOWNLOADS, exist_ok=True)
    os.makedirs(PLUGINS, exist_ok=True)
    os.makedirs(STREAMING, exist_ok=True)
    for name, url in LIBRARIES.items():
        raw = os.path.join(DOWNLOADS, os.path.basename(url))
        download(url, raw)
        strip_abis(raw, os.path.join(PLUGINS, name))
        print("wrote", os.path.join(PLUGINS, name))
    for lang, model in MODELS.items():
        archive = os.path.join(DOWNLOADS, model + ".zip")
        download(f"https://alphacephei.com/vosk/models/{model}.zip", archive)
        shutil.copyfile(archive, os.path.join(STREAMING, lang + ".zip"))
        unpacked = os.path.join(DOWNLOADS, "models")
        if not os.path.isdir(os.path.join(unpacked, model)):
            with zipfile.ZipFile(archive) as z:
                z.extractall(unpacked)
        print("wrote", os.path.join(STREAMING, lang + ".zip"))


if __name__ == "__main__":
    sys.exit(main())
