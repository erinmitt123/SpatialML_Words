using ByteDance.PICO.SpatialAdapter.Component;
using UnityEngine;

namespace WordWorld.Labels
{
    /// <summary>
    /// One pooled world-space label. Spatial Adapter only exports objects that exist in the scene at
    /// build time, so labels are pooled scene objects that get re-texted and moved.
    ///
    /// Text is drawn by the Spatial Engine (SpatialAdapterNativeText), not TextMeshPro: TMP glyph
    /// meshes changed at runtime rendered as blocks on Swan, and the native renderer also covers
    /// Chinese without a giant SDF atlas. Every setter marks the native text dirty, so values are
    /// only pushed when they change.
    /// </summary>
    [AddComponentMenu("Word World/Label View")]
    public sealed class LabelView : MonoBehaviour
    {
        // SpatialAdapterNativeText's unit: one typographic point.
        public const float MetresPerPoint = 0.000352806f;
        // Default native insets (10 pt each side), which the component does not expose.
        private const float InsetPoints = 10f;

        [SerializeField] private SpatialAdapterNativeText m_text;
        [SerializeField] private Color m_textColor = Color.white;
        [SerializeField] private Color m_backgroundColor = new Color(0.08f, 0.1f, 0.16f, 0.82f);
        [SerializeField] private TextJustification m_justification = TextJustification.Center;

        private string m_currentText;
        private int m_currentPoints = -1;
        private Vector2 m_currentBackground;
        private string m_currentName;
        private int m_currentPercent = -1;

        /// <summary>Height of the card at the current size, for de-overlap.</summary>
        public float HeightMetres => m_currentBackground.y;

        public void Bind(SpatialAdapterNativeText text) => m_text = text;

        private void Awake()
        {
            if (m_text == null) return;
            m_text.TextColor = m_textColor;
            m_text.BackgroundColor = m_backgroundColor;
            m_text.Justification = m_justification;
        }

        /// <summary>Label text: the word plus the detector's confidence as a percentage.</summary>
        public void SetWord(string word, float confidence)
        {
            int percent = Mathf.Clamp(Mathf.RoundToInt(confidence * 100f), 0, 100);
            if (word == m_currentName && percent == m_currentPercent) return;
            m_currentName = word;
            m_currentPercent = percent;
            SetText($"{word}  {percent}%");
        }

        public void SetText(string value)
        {
            if (m_text == null || value == m_currentText) return;
            m_currentText = value;
            m_text.Text = value;
            Resize(m_currentPoints);
        }

        /// <summary>Sets the font size and sizes the background card to fit one line of text.</summary>
        public void SetPoints(int points)
        {
            if (m_text == null || points == m_currentPoints) return;
            m_currentPoints = points;
            m_text.TextSize = points;
            Resize(points);
        }

        private void Resize(int points)
        {
            if (points <= 0) return;
            int characters = string.IsNullOrEmpty(m_currentText) ? 1 : m_currentText.Length;
            // ~0.58 em per character covers lower-case Latin; CJK is wider and gets its own size in P1.
            float width = (characters * 0.58f * points + 2f * InsetPoints + points * 0.6f) * MetresPerPoint;
            float height = (points * 1.45f + 2f * InsetPoints) * MetresPerPoint;
            var size = new Vector2(width, height);
            if (size == m_currentBackground) return;
            m_currentBackground = size;
            m_text.BackgroundSize = size;
        }
    }
}
