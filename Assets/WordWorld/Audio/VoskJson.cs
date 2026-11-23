using System;
using System.Collections.Generic;
using UnityEngine;
using WordWorld.Speech;

namespace WordWorld.Audio
{
    /// <summary>Reads Vosk's final result: {"result":[{"conf":0.98,"word":"silla",...}],"text":"..."}.</summary>
    public static class VoskJson
    {
        [Serializable]
        private sealed class Word
        {
            public float conf;
            public string word;
        }

        [Serializable]
        private sealed class Result
        {
            public Word[] result;
            public string text;
        }

        public static List<HeardWord> Words(string json)
        {
            var words = new List<HeardWord>();
            Result parsed = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<Result>(json);
            if (parsed?.result == null) return words;
            foreach (Word w in parsed.result)
                if (!string.IsNullOrEmpty(w.word)) words.Add(new HeardWord(w.word, w.conf));
            return words;
        }
    }
}
