using ByteDance.PICO.SpatialAdapter.Component;
using UnityEngine;

namespace WordWorld.UI
{
    /// <summary>Native text does not size its own background, so cards are sized from a width estimate.</summary>
    public static class TextFit
    {
        /// <summary>One typographic point. Sizes in code are in points, so they read like a font size.</summary>
        public const float MetresPerPoint = 0.000352806f;

        /// <summary>
        /// What one unit of SpatialAdapterNativeText.TextSize measures on Swan. Measured from a headset
        /// recording (Nov 2026): text at N "points" drew about 2.4 times as large as N typographic
        /// points, so it overflowed its cards and the clipped glyphs rendered as black blocks.
        /// </summary>
        public const float NativeMetresPerUnit = 0.00085f;

        /// <summary>Converts a size in typographic points to the native TextSize that draws at that size.</summary>
        public static int ToNative(float points) => Mathf.Max(1, Mathf.RoundToInt(points * MetresPerPoint / NativeMetresPerUnit));

        /// <summary>Rough advance width in ems: ~0.58 for Latin, a full em for CJK.</summary>
        public static float WidestLineEms(string value, out int lines)
        {
            lines = 1;
            if (string.IsNullOrEmpty(value)) return 0f;
            float widest = 0f, line = 0f;
            foreach (char c in value)
            {
                if (c == '\n')
                {
                    lines++;
                    line = 0f;
                    continue;
                }
                line += GlyphEms(c);
                if (line > widest) widest = line;
            }
            return widest * DrawnWidthScale;
        }

        /// <summary>
        /// Swan draws text about 13% wider than the per-glyph table says (Nov 16 recording: the
        /// recognition notice's sentence measured 0.76 m against an estimate of 0.68 m), which is why
        /// the dock pill and the notice ran past their backgrounds.
        /// </summary>
        private const float DrawnWidthScale = 1.13f;

        // Rough advance widths for Noto Sans: CJK is a full em; Latin varies a lot, and a flat 0.58 em
        // left labels like "la silla" with a wide empty margin (V3 review: "minimize empty space").
        private static float GlyphEms(char c)
        {
            if (c >= 0x2E80) return 1f;
            switch (c)
            {
                case ' ': case 'i': case 'l': case 'j': case '.': case ',': case '\x27': case ':': case ';': case '!': case '|':
                case 'í': case 'ì': case 'ï': case 'î': case 'ī': case 'ǐ': case 'I':
                    return 0.27f;
                case 'f': case 't': case 'r': case '(': case ')': case '-':
                    return 0.36f;
                case 'm': case 'w': case 'M': case 'W': case '%':
                    return 0.85f;
            }
            if (c >= 'A' && c <= 'Z') return 0.66f;
            if (c >= '0' && c <= '9') return 0.56f;
            return 0.54f;
        }

        /// <summary>Below this share of its authored size, a shrunk label is logged as too long.</summary>
        public const float MinShrink = 0.7f;

        /// <summary>
        /// The largest size up to <paramref name="points"/> at which <paramref name="value"/> fits inside
        /// <paramref name="box"/> with 8 mm side and 4 mm top/bottom padding. Widths are estimates, so
        /// a 10% margin covers wide glyphs.
        /// </summary>
        public static float FitPoints(string value, float points, Vector2 box)
        {
            float ems = WidestLineEms(value, out int lines) * 1.05f;
            if (ems <= 0f) return points;
            float byWidth = (box.x - 0.016f) / (ems * MetresPerPoint);
            float byHeight = (box.y - 0.008f) / (lines * 1.3f * MetresPerPoint);
            return Mathf.Max(1f, Mathf.Min(points, byWidth, byHeight));
        }

        /// <summary>
        /// Native text never wraps by itself: a long sentence runs on as one line past its card
        /// (Nov 16 recording, the recognition notice). This breaks <paramref name="value"/> into lines
        /// and picks the largest size up to <paramref name="points"/> at which the lines fit
        /// <paramref name="box"/>. Breaks go at spaces, or between any two CJK characters.
        /// </summary>
        public static string Wrap(string value, float points, Vector2 box, out float fitted)
        {
            fitted = points;
            if (string.IsNullOrEmpty(value) || box.x <= 0f || box.y <= 0f) return value ?? "";
            for (float p = points; p >= 1f; p = p > 12f ? p * 0.95f : p - 1f)
            {
                float width = (box.x - 0.016f) / (p * MetresPerPoint * 1.05f);
                string wrapped = WrapToEms(value, width);
                WidestLineEms(wrapped, out int lines);
                if (lines * 1.3f * p * MetresPerPoint <= box.y - 0.008f && WidestLineEms(wrapped, out _) <= width)
                {
                    fitted = p;
                    return wrapped;
                }
            }
            fitted = 1f;
            return value;
        }

        /// <summary>Greedy line breaking so no line is wider than <paramref name="maxEms"/>.</summary>
        public static string WrapToEms(string value, float maxEms)
        {
            var result = new System.Text.StringBuilder(value.Length + 8);
            foreach (string paragraph in value.Split('\n'))
            {
                if (result.Length > 0) result.Append('\n');
                float line = 0f;
                int lineStart = result.Length;
                int i = 0;
                while (i < paragraph.Length)
                {
                    // Next breakable unit: a word with its trailing space, or one CJK character.
                    int end = i;
                    if (paragraph[i] >= 0x2E80) end = i + 1;
                    else
                    {
                        while (end < paragraph.Length && paragraph[end] != ' ' && paragraph[end] < 0x2E80) end++;
                        while (end < paragraph.Length && paragraph[end] == ' ') end++;
                    }
                    // CJK punctuation stays with the character before it.
                    while (end < paragraph.Length && IsClosingPunctuation(paragraph[end])) end++;
                    string unit = paragraph.Substring(i, end - i);
                    float ems = WidestLineEms(unit.TrimEnd(' '), out _);
                    if (line > 0f && line + ems > maxEms)
                    {
                        // Drop the space the line ended on, then start a new one.
                        while (result.Length > lineStart && result[result.Length - 1] == ' ') result.Length--;
                        result.Append('\n');
                        lineStart = result.Length;
                        line = 0f;
                    }
                    result.Append(unit);
                    line += WidestLineEms(unit, out _);
                    i = end;
                }
                while (result.Length > lineStart && result[result.Length - 1] == ' ') result.Length--;
            }
            return result.ToString();
        }

        private static bool IsClosingPunctuation(char c) =>
            c == '，' || c == '。' || c == '、' || c == '：' || c == '；' || c == '！' || c == '？' || c == '）' || c == '」' || c == '”';

        /// <summary>Sizes the background to the text plus 8 mm side padding; keeps the collider in step.</summary>
        public static void FitBackground(SpatialAdapterNativeText text, string value, int points, float minHeight)
        {
            float em = points * MetresPerPoint;
            float width = WidestLineEms(value, out int lines) * em + 0.016f;
            var size = new Vector2(Mathf.Max(width, 0.04f), Mathf.Max(minHeight, lines * em * 1.3f + 0.01f));
            text.BackgroundSize = size;
            var box = text.GetComponent<BoxCollider>();
            if (box != null) box.size = new Vector3(size.x, size.y, box.size.z);
        }
    }
}
