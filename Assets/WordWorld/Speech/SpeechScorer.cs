using System;
using System.Collections.Generic;

namespace WordWorld.Speech
{
    /// <summary>How one written word (one character, for Mandarin) came out.</summary>
    public enum WordMark
    {
        /// <summary>Heard, with confidence at or above the clarity threshold.</summary>
        Clear,
        /// <summary>Heard, but the recognizer wasn't sure: "say again".</summary>
        Unclear,
        /// <summary>Something else was heard in its place.</summary>
        Wrong,
        /// <summary>Not heard at all.</summary>
        Missing,
    }

    /// <summary>One recognized word and Vosk's confidence in it (0-1).</summary>
    public struct HeardWord
    {
        public string Word;
        public float Confidence;

        public HeardWord(string word, float confidence)
        {
            Word = word;
            Confidence = confidence;
        }
    }

    /// <summary>What the learner was asked to say, already split into model words (speech.json).</summary>
    public sealed class SpeechTarget
    {
        public string Language;
        /// <summary>The words to say, space separated, lower case, no punctuation.</summary>
        public string Line;
        /// <summary>Words naming the object; a phrase or sentence passes only if these were said.</summary>
        public string Key;
        /// <summary>Level 1, Latin script: the noun without its article also passes.</summary>
        public string Noun;
        /// <summary>Words the model doesn't know; never expected, never scored.</summary>
        public string Unknown;
        /// <summary>Single word (Level 1) rather than a phrase or sentence.</summary>
        public bool IsWord;
    }

    public struct ScoredUnit
    {
        public string Text;
        public WordMark Mark;
        /// <summary>For Wrong: what was heard instead.</summary>
        public string Heard;
    }

    public sealed class SpeechScore
    {
        public bool Passed;
        /// <summary>Nothing (or only noise) was recognized.</summary>
        public bool HeardNothing;
        public readonly List<ScoredUnit> Units = new List<ScoredUnit>();
        public string HeardText = "";

        public int Count(WordMark mark)
        {
            int n = 0;
            foreach (ScoredUnit unit in Units) if (unit.Mark == mark) n++;
            return n;
        }
    }

    /// <summary>
    /// Scores a Say It answer (docs/audio-plan.md, "Checking an answer"). Correctness: the expected
    /// words in order (aligned with a longest common subsequence), 80% of them for a phrase or
    /// sentence, always including the words that name the object. Pronunciation: each matched word is
    /// clear or unclear by Vosk's confidence. Mandarin is scored per character. Mirrored by
    /// tools/test_speech.py, which checks the same rules offline against the generated clips.
    /// </summary>
    public static class SpeechScorer
    {
        public const float DefaultClearConfidence = 0.75f;
        public const float PassRatio = 0.8f;
        private const string UnknownWord = "[unk]";

        public static SpeechScore Score(SpeechTarget target, IReadOnlyList<HeardWord> heardWords,
            float clearConfidence = DefaultClearConfidence)
        {
            var score = new SpeechScore();
            bool zh = target.Language == "zh";
            var unknown = new HashSet<string>(Split(target.Unknown));
            List<string> expected = Units(Split(target.Line), unknown, zh);
            var heard = new List<HeardWord>();
            var heardText = new List<string>();
            foreach (HeardWord word in heardWords)
            {
                if (string.IsNullOrEmpty(word.Word)) continue;
                heardText.Add(word.Word);
                if (word.Word == UnknownWord) heard.Add(new HeardWord("?", word.Confidence));
                else if (zh) foreach (char c in word.Word) heard.Add(new HeardWord(c.ToString(), word.Confidence));
                else heard.Add(new HeardWord(word.Word.ToLowerInvariant(), word.Confidence));
            }
            score.HeardText = string.Join(zh ? "" : " ", heardText);
            score.HeardNothing = heard.TrueForAll(h => h.Word == "?");

            int[] match = Align(expected, heard);
            int matched = 0;
            int previousHeard = -1;
            // Heard words already reported as the substitute for an earlier missing word.
            var claimed = new HashSet<int>();
            for (int i = 0; i < expected.Count; i++)
            {
                var unit = new ScoredUnit { Text = expected[i] };
                if (match[i] >= 0)
                {
                    matched++;
                    unit.Mark = heard[match[i]].Confidence >= clearConfidence ? WordMark.Clear : WordMark.Unclear;
                    previousHeard = match[i];
                }
                else
                {
                    // Something heard between the neighbouring matches took this word's place.
                    int nextHeard = heard.Count;
                    for (int j = i + 1; j < expected.Count; j++)
                        if (match[j] >= 0) { nextHeard = match[j]; break; }
                    string substitute = null;
                    for (int k = previousHeard + 1; k < nextHeard; k++)
                        if (heard[k].Word != "?" && claimed.Add(k)) { substitute = heard[k].Word; break; }
                    unit.Mark = substitute != null ? WordMark.Wrong : WordMark.Missing;
                    unit.Heard = substitute;
                }
                score.Units.Add(unit);
            }

            if (expected.Count == 0)
            {
                score.Passed = false;
            }
            else if (target.IsWord)
            {
                score.Passed = matched == expected.Count;
                List<string> noun = Units(Split(target.Noun), unknown, zh);
                if (!score.Passed && noun.Count > 0 && AllMatched(Align(noun, heard))) score.Passed = true;
            }
            else
            {
                List<string> key = Units(Split(target.Key), unknown, zh);
                score.Passed = AllMatched(Align(key, heard)) && matched >= Needed(expected.Count);
            }
            return score;
        }

        /// <summary>80% of the words, rounded down: "a cup" passes on "cup", with "a" still flagged.</summary>
        public static int Needed(int count) => Math.Max(1, (int)(PassRatio * count));

        private static bool AllMatched(int[] match)
        {
            foreach (int m in match) if (m < 0) return false;
            return true;
        }

        private static string[] Split(string line) =>
            string.IsNullOrEmpty(line) ? Array.Empty<string>() : line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        private static List<string> Units(string[] words, HashSet<string> unknown, bool perCharacter)
        {
            var units = new List<string>();
            foreach (string word in words)
            {
                if (unknown.Contains(word)) continue;
                if (perCharacter) foreach (char c in word) units.Add(c.ToString());
                else units.Add(word.ToLowerInvariant());
            }
            return units;
        }

        /// <summary>For each expected unit, the heard index it lines up with, or -1.</summary>
        public static int[] Align(List<string> expected, List<HeardWord> heard)
        {
            int n = expected.Count, m = heard.Count;
            var dp = new int[n + 1, m + 1];
            for (int i = n - 1; i >= 0; i--)
                for (int j = m - 1; j >= 0; j--)
                    dp[i, j] = expected[i] == heard[j].Word ? dp[i + 1, j + 1] + 1 : Math.Max(dp[i + 1, j], dp[i, j + 1]);
            var match = new int[n];
            for (int i = 0; i < n; i++) match[i] = -1;
            int a = 0, b = 0;
            while (a < n && b < m)
            {
                if (expected[a] == heard[b].Word) { match[a] = b; a++; b++; }
                else if (dp[a + 1, b] >= dp[a, b + 1]) a++;
                else b++;
            }
            return match;
        }
    }
}
