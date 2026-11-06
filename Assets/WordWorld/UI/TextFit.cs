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
                line += c >= 0x2E80 ? 1f : 0.58f;
                if (line > widest) widest = line;
            }
            return widest;
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
            float ems = WidestLineEms(value, out int lines) * 1.1f;
            if (ems <= 0f) return points;
            float byWidth = (box.x - 0.016f) / (ems * MetresPerPoint);
            float byHeight = (box.y - 0.008f) / (lines * 1.3f * MetresPerPoint);
            return Mathf.Max(1f, Mathf.Min(points, byWidth, byHeight));
        }

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
