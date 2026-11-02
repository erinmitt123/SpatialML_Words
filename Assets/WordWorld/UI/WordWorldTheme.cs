using UnityEngine;

namespace WordWorld.UI
{
    /// <summary>
    /// "Field notebook" colour tokens (DESIGN.md section 2). Every UI script takes its colours from
    /// here so the look changes in one place.
    /// </summary>
    [CreateAssetMenu(menuName = "Word World/Theme", fileName = "WordWorldTheme")]
    public sealed class WordWorldTheme : ScriptableObject
    {
        [Header("Surfaces")]
        // Solid: on Swan, text over a see-through background rendered as dark blocks.
        public Color Card = new Color32(0xFF, 0xFF, 0xFF, 0xFF);
        public Color CardDark = new Color32(0x1A, 0x20, 0x1D, 224); // #1A201D at 88%

        [Header("Text")]
        public Color Ink = new Color32(0x1D, 0x24, 0x20, 0xFF);
        public Color InkMuted = new Color32(0x5F, 0x6B, 0x64, 0xFF);

        [Header("Accent")]
        public Color Accent = new Color32(0x1F, 0x6B, 0x52, 0xFF);
        public Color AccentSoft = new Color32(0xE2, 0xEF, 0xE8, 0xFF);

        [Header("Feedback")]
        public Color Correct = new Color32(0x2E, 0x9E, 0x5B, 0xFF);
        public Color Hint = new Color32(0xE5, 0xB7, 0x65, 0xFF);
        public Color Wrong = new Color32(0xC8, 0x55, 0x3D, 0xFF);

        [Header("Gender (article text only, off by default)")]
        public Color Masculine = new Color32(0x3A, 0x6E, 0xA5, 0xFF);
        public Color Feminine = new Color32(0xB0, 0x43, 0x6E, 0xFF);
        public Color Neuter = new Color32(0x6B, 0x7F, 0x3A, 0xFF);

        [Header("Shape (metres)")]
        public float PanelCornerRadius = 0.012f;
        public float LabelCornerRadius = 0.008f;
    }
}
