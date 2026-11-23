using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using WordWorld.Audio;

namespace WordWorld.Editor
{
    /// <summary>
    /// Import settings for the pronunciation clips in Assets/Audio/Vocab, and the VocabClips index the
    /// scene points at. About 1,200 short clips: kept compressed in memory and not preloaded, so the
    /// scene loads none of them until one is played.
    /// </summary>
    public sealed class VocabAudioImport : AssetPostprocessor
    {
        public const string ClipFolder = "Assets/Audio/Vocab";
        public const string IndexPath = "Assets/Data/VocabClips.asset";

        private void OnPreprocessAudio()
        {
            // The tour narration (Assets/Audio/Tour) is imported the same way.
            if (!assetPath.StartsWith(ClipFolder + "/") && !assetPath.StartsWith("Assets/Audio/Tour/")) return;
            var importer = (AudioImporter)assetImporter;
            importer.forceToMono = true;
            importer.loadInBackground = true;
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.CompressedInMemory;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.6f;
            settings.preloadAudioData = false;
            importer.defaultSampleSettings = settings;
        }

        /// <summary>Rebuilds the index from the files on disk: &lt;lang&gt;/&lt;classIndex&gt;_&lt;word|phrase|sentence&gt;.ogg.</summary>
        public static VocabClips BuildIndex()
        {
            var items = new List<VocabClips.Item>();
            if (AssetDatabase.IsValidFolder(ClipFolder))
            {
                foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { ClipFolder }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    string language = Path.GetFileName(Path.GetDirectoryName(path));
                    string[] parts = Path.GetFileNameWithoutExtension(path).Split('_');
                    if (parts.Length != 2 || !int.TryParse(parts[0], out int classIndex)) continue;
                    ClipKind kind;
                    switch (parts[1])
                    {
                        case "word": kind = ClipKind.Word; break;
                        case "phrase": kind = ClipKind.Phrase; break;
                        case "sentence": kind = ClipKind.Sentence; break;
                        default: continue;
                    }
                    items.Add(new VocabClips.Item
                    {
                        language = language, classIndex = classIndex, kind = kind,
                        clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path),
                    });
                }
            }

            var index = AssetDatabase.LoadAssetAtPath<VocabClips>(IndexPath);
            if (index == null)
            {
                index = ScriptableObject.CreateInstance<VocabClips>();
                AssetDatabase.CreateAsset(index, IndexPath);
            }
            index.SetItems(items.ToArray());
            EditorUtility.SetDirty(index);
            AssetDatabase.SaveAssets();
            Debug.Log($"[WordWorld] indexed {items.Count} pronunciation clips");
            return index;
        }
    }
}
