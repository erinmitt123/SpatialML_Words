using System;
using System.Collections.Generic;
using UnityEngine;

namespace WordWorld.Audio
{
    /// <summary>Which line of a vocabulary entry a clip says.</summary>
    public enum ClipKind { Word, Phrase, Sentence }

    /// <summary>
    /// Every pronunciation clip (Assets/Audio/Vocab, made by tools/generate_audio.py), indexed by
    /// language, class and kind. Built by the scene builder; clips don't preload, so only the ones
    /// played are ever loaded into memory.
    /// </summary>
    [CreateAssetMenu(menuName = "Word World/Vocab Clips")]
    public sealed class VocabClips : ScriptableObject
    {
        [Serializable]
        public struct Item
        {
            public string language;
            public int classIndex;
            public ClipKind kind;
            public AudioClip clip;
        }

        [SerializeField] private Item[] m_items = Array.Empty<Item>();

        private Dictionary<(string, int, ClipKind), AudioClip> m_lookup;

        public int Count => m_items.Length;

        public void SetItems(Item[] items)
        {
            m_items = items;
            m_lookup = null;
        }

        public AudioClip Get(string language, int classIndex, ClipKind kind)
        {
            if (m_lookup == null)
            {
                m_lookup = new Dictionary<(string, int, ClipKind), AudioClip>(m_items.Length);
                foreach (Item item in m_items)
                    if (item.clip != null) m_lookup[(item.language, item.classIndex, item.kind)] = item.clip;
            }
            return m_lookup.TryGetValue((language, classIndex, kind), out AudioClip clip) ? clip : null;
        }

        /// <summary>
        /// The clip that matches what a label shows (docs/audio-plan.md): Latin script says the word,
        /// phrase or sentence of the reading level; every Mandarin level names the word itself.
        /// </summary>
        public static ClipKind ForLevel(string language, int level) =>
            language == "zh" || level <= 1 ? ClipKind.Word : level == 2 ? ClipKind.Phrase : ClipKind.Sentence;

        /// <summary>
        /// What Say It asks for. Latin script: what the label shows. Mandarin: the measure-word phrase
        /// (一只猫), written on the card, because one-syllable words alone were recognized unreliably
        /// (tools/test_speech.py: 48/58 for words, 58/58 for phrases); decided 2026-11-04.
        /// </summary>
        public static ClipKind ForSayIt(string language, int level) =>
            language == "zh" ? ClipKind.Phrase : ForLevel(language, level);
    }
}
