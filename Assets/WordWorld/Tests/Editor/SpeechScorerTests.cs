using System.Collections.Generic;
using NUnit.Framework;

namespace WordWorld.Speech.Tests
{
    public sealed class SpeechScorerTests
    {
        private static SpeechTarget Word(string lang, string line, string noun = "") =>
            new SpeechTarget { Language = lang, Line = line, Noun = noun, Key = noun, IsWord = true, Unknown = "" };

        private static SpeechTarget Sentence(string lang, string line, string key, string unknown = "") =>
            new SpeechTarget { Language = lang, Line = line, Key = key, IsWord = false, Unknown = unknown };

        private static List<HeardWord> Heard(params (string word, float conf)[] words)
        {
            var list = new List<HeardWord>();
            foreach (var (word, conf) in words) list.Add(new HeardWord(word, conf));
            return list;
        }

        [Test]
        public void WordWithArticlePassesClear()
        {
            SpeechScore s = SpeechScorer.Score(Word("es", "la silla", "silla"), Heard(("la", 1f), ("silla", 0.95f)));
            Assert.IsTrue(s.Passed);
            Assert.AreEqual(2, s.Count(WordMark.Clear));
        }

        [Test]
        public void NounWithoutArticleStillPasses()
        {
            SpeechScore s = SpeechScorer.Score(Word("es", "la silla", "silla"), Heard(("silla", 0.9f)));
            Assert.IsTrue(s.Passed);
            Assert.AreEqual(WordMark.Missing, s.Units[0].Mark);
            Assert.AreEqual(WordMark.Clear, s.Units[1].Mark);
        }

        [Test]
        public void DifferentWordFailsAndIsReported()
        {
            SpeechScore s = SpeechScorer.Score(Word("es", "la silla", "silla"), Heard(("la", 1f), ("mesa", 0.9f)));
            Assert.IsFalse(s.Passed);
            Assert.AreEqual(WordMark.Wrong, s.Units[1].Mark);
            Assert.AreEqual("mesa", s.Units[1].Heard);
        }

        [Test]
        public void LowConfidenceIsUnclearButCorrect()
        {
            SpeechScore s = SpeechScorer.Score(Word("en", "chair", "chair"), Heard(("chair", 0.4f)));
            Assert.IsTrue(s.Passed);
            Assert.AreEqual(WordMark.Unclear, s.Units[0].Mark);
        }

        [Test]
        public void NothingHeard()
        {
            SpeechScore s = SpeechScorer.Score(Word("en", "chair", "chair"), Heard(("[unk]", 1f)));
            Assert.IsFalse(s.Passed);
            Assert.IsTrue(s.HeardNothing);
        }

        [Test]
        public void SentencePassesAtEightyPercentWithKeyNoun()
        {
            var target = Sentence("es", "siéntate en la silla por favor", "silla");
            // Five of six words, the noun among them.
            SpeechScore s = SpeechScorer.Score(target, Heard(("siéntate", 1f), ("en", 1f), ("silla", 1f), ("por", 1f), ("favor", 1f)));
            Assert.IsTrue(s.Passed);
            Assert.AreEqual(WordMark.Missing, s.Units[2].Mark);
        }

        [Test]
        public void SentenceFailsWithoutKeyNoun()
        {
            var target = Sentence("es", "siéntate en la silla por favor", "silla");
            SpeechScore s = SpeechScorer.Score(target, Heard(("siéntate", 1f), ("en", 1f), ("la", 1f), ("mesa", 1f), ("por", 1f), ("favor", 1f)));
            Assert.IsFalse(s.Passed);
            Assert.AreEqual(WordMark.Wrong, s.Units[3].Mark);
        }

        [Test]
        public void ShortPhraseAllowsOneMissingWord()
        {
            var target = Sentence("en", "a cup", "cup");
            Assert.IsTrue(SpeechScorer.Score(target, Heard(("cup", 1f))).Passed);
            Assert.IsFalse(SpeechScorer.Score(target, Heard(("a", 1f))).Passed);
        }

        [Test]
        public void UnknownWordsAreNotExpected()
        {
            var target = Sentence("es", "el hidrante es rojo", "hidrante", unknown: "hidrante");
            SpeechScore s = SpeechScorer.Score(target, Heard(("el", 1f), ("es", 1f), ("rojo", 1f)));
            Assert.AreEqual(3, s.Units.Count);
            Assert.IsTrue(s.Passed);
        }

        [Test]
        public void MandarinIsScoredPerCharacter()
        {
            SpeechScore s = SpeechScorer.Score(Word("zh", "杯子"), Heard(("杯子", 0.9f)));
            Assert.IsTrue(s.Passed);
            Assert.AreEqual(2, s.Units.Count);

            SpeechScore wrong = SpeechScorer.Score(Word("zh", "杯子"), Heard(("被子", 0.9f)));
            Assert.IsFalse(wrong.Passed);
            Assert.AreEqual(WordMark.Wrong, wrong.Units[0].Mark);
            Assert.AreEqual("被", wrong.Units[0].Heard);
            Assert.AreEqual(WordMark.Clear, wrong.Units[1].Mark);
        }

        [Test]
        public void MandarinSentenceSegmentedDifferentlyStillMatches()
        {
            // speech.json split it 请坐 在 椅子 上; Vosk may hear 请 坐在 椅子上.
            var target = Sentence("zh", "请坐 在 椅子 上", "椅子");
            SpeechScore s = SpeechScorer.Score(target, Heard(("请", 1f), ("坐在", 1f), ("椅子上", 1f)));
            Assert.IsTrue(s.Passed);
            Assert.AreEqual(6, s.Count(WordMark.Clear));
        }
    }
}
