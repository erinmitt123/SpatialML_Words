using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using WordWorld.Speech;

namespace WordWorld.Audio
{
    /// <summary>
    /// What the learner is expected to say for each word, phrase and sentence, already split into the
    /// words each Vosk model knows (Assets/Data/speech.json, made by tools/build_speech.py), and the
    /// grammar Vosk listens with.
    /// </summary>
    [AddComponentMenu("Word World/Speech Data")]
    public sealed class SpeechData : MonoBehaviour
    {
        [SerializeField] private TextAsset m_table;

        [Serializable]
        private sealed class Entry
        {
            public int classIndex;
            public string word;
            public string noun;
            public string phrase;
            public string sentence;
            public string phraseKey;
            public string sentenceKey;
            public string unknown;
        }

        [Serializable]
        private sealed class Language
        {
            public string language;
            public string model;
            public Entry[] entries;
        }

        [Serializable]
        private sealed class Table
        {
            public Language[] languages;
        }

        private readonly Dictionary<string, Dictionary<int, Entry>> m_languages = new Dictionary<string, Dictionary<int, Entry>>();
        private readonly Dictionary<string, string> m_models = new Dictionary<string, string>();

        private void Awake()
        {
            if (m_table == null) return;
            Table table = JsonUtility.FromJson<Table>(m_table.text);
            if (table?.languages == null) return;
            foreach (Language language in table.languages)
            {
                var entries = new Dictionary<int, Entry>();
                foreach (Entry entry in language.entries) entries[entry.classIndex] = entry;
                m_languages[language.language] = entries;
                m_models[language.language] = language.model;
            }
        }

        /// <summary>Vosk model folder name for a language, e.g. vosk-model-small-es-0.42.</summary>
        public string ModelName(string language) => m_models.TryGetValue(language, out string name) ? name : null;

        /// <summary>
        /// The target for an object at a reading level: what its label shows (docs/audio-plan.md,
        /// "What is spoken and said"). Null when the words that name the object can't be recognized.
        /// </summary>
        public SpeechTarget Target(string language, int classIndex, ClipKind kind)
        {
            if (!m_languages.TryGetValue(language, out Dictionary<int, Entry> entries) ||
                !entries.TryGetValue(classIndex, out Entry e)) return null;
            var target = new SpeechTarget { Language = language, Noun = e.noun ?? "", Unknown = e.unknown ?? "" };
            switch (kind)
            {
                case ClipKind.Word: target.Line = e.word; target.IsWord = true; target.Key = e.word; break;
                case ClipKind.Phrase: target.Line = e.phrase; target.Key = e.phraseKey; break;
                default: target.Line = e.sentence; target.Key = e.sentenceKey; break;
            }
            // Nothing left to listen for, or the object's own word is one the model doesn't know.
            if (string.IsNullOrEmpty(Known(target.Line, target.Unknown))) return null;
            if (!target.IsWord && string.IsNullOrEmpty(Known(target.Key, target.Unknown))) return null;
            if (target.IsWord && language != "zh" && !string.IsNullOrEmpty(target.Noun) &&
                string.IsNullOrEmpty(Known(target.Noun, target.Unknown))) return null;
            return target;
        }

        /// <summary>
        /// Vosk grammar (a JSON list of phrases): the expected line, each of its words alone (so a
        /// partly right answer is still heard word by word), every other object's word (so a different
        /// real word is caught), and [unk] for anything else. Mirrors tools/test_speech.py.
        /// </summary>
        public string Grammar(SpeechTarget target)
        {
            var phrases = new SortedSet<string>(StringComparer.Ordinal);
            string line = Known(target.Line, target.Unknown);
            phrases.Add(line);
            foreach (string word in line.Split(' ')) phrases.Add(word);
            if (m_languages.TryGetValue(target.Language, out Dictionary<int, Entry> entries))
                foreach (Entry e in entries.Values)
                {
                    phrases.Add(Known(e.word, e.unknown));
                    phrases.Add(Known(e.noun, e.unknown));
                }
            phrases.Remove("");

            var json = new StringBuilder(4096).Append('[');
            foreach (string phrase in phrases) AppendString(json, phrase).Append(',');
            return json.Append("\"[unk]\"]").ToString();
        }

        private static string Known(string line, string unknown)
        {
            if (string.IsNullOrEmpty(line)) return "";
            if (string.IsNullOrEmpty(unknown)) return line;
            var skip = new HashSet<string>(unknown.Split(' '));
            var kept = new List<string>();
            foreach (string word in line.Split(' ')) if (word.Length > 0 && !skip.Contains(word)) kept.Add(word);
            return string.Join(" ", kept);
        }

        private static StringBuilder AppendString(StringBuilder json, string value)
        {
            json.Append('"');
            foreach (char c in value)
            {
                if (c == '"' || c == '\\') json.Append('\\');
                json.Append(c);
            }
            return json.Append('"');
        }
    }
}
