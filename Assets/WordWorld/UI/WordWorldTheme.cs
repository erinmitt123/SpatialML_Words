using UnityEngine;

namespace WordWorld.UI
{
    /// <summary>
    /// "Soft light" colour tokens (DESIGN.md section 2): a light neumorphic surface with dark ink and
    /// pastel highlights. Every UI script takes its colours from here so the look changes in one place.
    /// Text is always dark ink, on the surface or on a pastel, never pastel on light: neumorphism's
    /// soft edges carry no contrast, so legibility and state come from ink and pastel fills.
    /// </summary>
    [CreateAssetMenu(menuName = "Word World/Theme", fileName = "WordWorldTheme")]
    public sealed class WordWorldTheme : ScriptableObject
    {
        [Header("Surfaces")]
        // Solid: on Swan, text over a see-through background rendered as dark blocks.
        public Color Card = new Color32(0xEE, 0xF1, 0xF6, 0xFF);
        public Color CardDark = new Color32(0x1A, 0x20, 0x1D, 224); // #1A201D at 88%
        [Tooltip("Soft shadow behind raised surfaces, offset down and right.")]
        public Color Shadow = new Color32(0xA3, 0xAE, 0xBF, 0x99);
        [Tooltip("Soft highlight behind raised surfaces, offset up and left.")]
        public Color Highlight = new Color32(0xFF, 0xFF, 0xFF, 0xE6);

        [Header("Text")]
        public Color Ink = new Color32(0x1F, 0x26, 0x33, 0xFF);
        public Color InkMuted = new Color32(0x4A, 0x55, 0x68, 0xFF);

        [Header("Accent")]
        [Tooltip("Deep teal for accent text (pinyin, big numbers). Dark enough for 7:1 on the surface.")]
        public Color Accent = new Color32(0x1E, 0x6E, 0x58, 0xFF);
        [Tooltip("Pastel mint: selected options.")]
        public Color AccentSoft = new Color32(0xCD, 0xEF, 0xE0, 0xFF);
        [Tooltip("Pastel lavender: the one primary action on a card.")]
        public Color Primary = new Color32(0xDC, 0xD3, 0xFA, 0xFF);
        [Tooltip("Pastel sky: progress bars and information.")]
        public Color Info = new Color32(0xC9, 0xE3, 0xFA, 0xFF);

        [Header("Feedback")]
        public Color Correct = new Color32(0x86, 0xD9, 0xB6, 0xFF);
        public Color Hint = new Color32(0xFF, 0xCF, 0xAE, 0xFF);
        public Color Wrong = new Color32(0xF6, 0xC1, 0xCA, 0xFF);

        [Header("Gender (article text only, off by default)")]
        public Color Masculine = new Color32(0x3A, 0x6E, 0xA5, 0xFF);
        public Color Feminine = new Color32(0xB0, 0x43, 0x6E, 0xFF);
        public Color Neuter = new Color32(0x6B, 0x7F, 0x3A, 0xFF);

        [Header("Shape (metres)")]
        public float PanelCornerRadius = 0.016f;
        public float LabelCornerRadius = 0.01f;
        [Tooltip("How far the shadow and highlight sit from a raised surface.")]
        public float SoftOffset = 0.004f;
    }
}
