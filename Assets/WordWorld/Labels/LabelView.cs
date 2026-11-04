using System;
using ByteDance.PICO.SpatialAdapter.Component;
using UnityEngine;
using UnityEngine.EventSystems;
using WordWorld.UI;

namespace WordWorld.Labels
{
    /// <summary>
    /// One pooled object label (DESIGN.md section 3): a card with the word and, under it, a smaller
    /// line with its meaning; an optional pinyin ruby line above; and a thin leader line down to a
    /// dot on the object. A background-only child behind both lines makes them one panel. Spatial Adapter only
    /// exports objects that exist in the scene at build time, so labels are pooled scene objects
    /// that get re-texted and moved.
    ///
    /// Text is drawn by the Spatial Engine (SpatialAdapterNativeText), not TextMeshPro: TMP glyph
    /// meshes changed at runtime rendered as blocks on Swan, and the native renderer also covers
    /// Chinese without a giant SDF atlas. Every setter is a native update, so values are only
    /// pushed when they change. The leader and dot are text backgrounds with no text.
    /// </summary>
    [AddComponentMenu("Word World/Label View")]
    public sealed class LabelView : MonoBehaviour, IPointerClickHandler
    {
        // DESIGN.md: card padding 8 mm horizontal, 5 mm vertical.
        private const float PadX = 0.008f;
        private const float PadY = 0.005f;
        private const float RubyScale = 0.55f;
        private const float GlossScale = 0.6f;
        private const float LeaderWidth = 0.001f;
        private const float DotSize = 0.006f;

        [SerializeField] private SpatialAdapterNativeText m_text;
        [SerializeField] private SpatialAdapterNativeText m_ruby;
        [Tooltip("The meaning in the interface language, under the word.")]
        [SerializeField] private SpatialAdapterNativeText m_gloss;
        [Tooltip("Background-only panel behind the word and its meaning.")]
        [SerializeField] private SpatialAdapterNativeText m_backing;
        [SerializeField] private SpatialAdapterNativeText m_leader;
        [SerializeField] private SpatialAdapterNativeText m_dot;
        [SerializeField] private BoxCollider m_collider;
        [Tooltip("Unit trigger box scaled to the real object in quiz mode, so touching the object answers.")]
        [SerializeField] private Transform m_bounds;
        [Tooltip("Card background and Ink text (DESIGN.md section 2).")]
        [SerializeField] private WordWorldTheme m_theme;

        private string m_main;
        private string m_rubyText;
        private string m_glossText;
        private float m_glossHeight;
        private bool m_accent;
        private int m_points = -1;
        private Vector2 m_card;
        private float m_rubyHeight;
        private float m_leaderLength = -1f;
        private float m_opacity = -1f;
        private bool m_showText = true;
        // Card width only grows while one word is shown, so changing digits never resize it.
        private float m_cardEms;

        /// <summary>Raised when the label is poked or pinched.</summary>
        public event Action<LabelView> Tapped;

        /// <summary>Height of the card (word and meaning) plus its ruby line, for de-overlap.</summary>
        public float HeightMetres => m_card.y + m_rubyHeight;

        public void Bind(SpatialAdapterNativeText text, SpatialAdapterNativeText ruby, SpatialAdapterNativeText gloss,
            SpatialAdapterNativeText backing, SpatialAdapterNativeText leader, SpatialAdapterNativeText dot,
            BoxCollider collider, Transform bounds, WordWorldTheme theme)
        {
            m_gloss = gloss;
            m_backing = backing;
            m_bounds = bounds;
            m_text = text;
            m_ruby = ruby;
            m_leader = leader;
            m_dot = dot;
            m_collider = collider;
            m_theme = theme;
        }

        private void Awake()
        {
            if (m_text != null) m_text.Justification = TextJustification.Center;
            if (m_dot != null) m_dot.BackgroundSize = new Vector2(DotSize, DotSize);
        }

        public void OnPointerClick(PointerEventData eventData) => Tapped?.Invoke(this);

        /// <summary>
        /// Main text, optional ruby above and optional meaning below. With <paramref name="suffix"/>
        /// (debug confidence) the card is sized for the widest suffix so it does not resize as the
        /// digits change.
        /// </summary>
        public void SetContent(string main, string ruby, bool accent, string gloss, string suffix = null, string widestSuffix = null)
        {
            if (main != m_main) m_cardEms = 0f;
            string shown = string.IsNullOrEmpty(suffix) ? main : main + suffix;
            if (!string.IsNullOrEmpty(widestSuffix)) m_cardEms = Mathf.Max(m_cardEms, TextFit.WidestLineEms(main + widestSuffix, out _));
            bool changed = shown != CurrentText || ruby != m_rubyText || accent != m_accent || gloss != m_glossText;
            m_main = main;
            CurrentText = shown;
            m_rubyText = ruby;
            if (!changed) return;
            m_accent = accent;
            m_glossText = gloss;
            if (m_text != null) m_text.Text = shown;
            if (m_ruby != null)
            {
                m_ruby.Text = ruby ?? "";
                m_ruby.gameObject.SetActive(m_showText && !string.IsNullOrEmpty(ruby));
            }
            if (m_gloss != null)
            {
                m_gloss.Text = gloss ?? "";
                m_gloss.gameObject.SetActive(m_showText && !string.IsNullOrEmpty(gloss));
            }
            ApplyColours();
            Layout();
        }

        public string CurrentText { get; private set; }

        public void SetPoints(int points)
        {
            if (points == m_points) return;
            m_points = points;
            if (m_text != null) m_text.TextSize = TextFit.ToNative(points);
            if (m_ruby != null) m_ruby.TextSize = TextFit.ToNative(points * RubyScale);
            if (m_gloss != null) m_gloss.TextSize = TextFit.ToNative(points * GlossScale);
            Layout();
        }

        /// <summary>Quiz mode shows only the dot on the object (DESIGN.md section 3).</summary>
        public void SetTextVisible(bool visible)
        {
            if (visible == m_showText) return;
            m_showText = visible;
            m_text.gameObject.SetActive(visible);
            if (m_ruby != null) m_ruby.gameObject.SetActive(visible && !string.IsNullOrEmpty(m_rubyText));
            if (m_gloss != null) m_gloss.gameObject.SetActive(visible && !string.IsNullOrEmpty(m_glossText));
            if (m_backing != null) m_backing.gameObject.SetActive(visible);
            if (m_leader != null) m_leader.gameObject.SetActive(visible);
        }

        /// <summary>Multiplies every colour's alpha. Small changes are skipped: each is a native update.</summary>
        public void SetOpacity(float opacity)
        {
            if (Mathf.Abs(opacity - m_opacity) < 0.02f && !(opacity == 0f && m_opacity != 0f) && !(opacity == 1f && m_opacity != 1f)) return;
            m_opacity = opacity;
            ApplyColours();
        }

        /// <summary>
        /// Quiz mode: a box the size of the real object around the dot, so a direct touch or a
        /// pinch-ray on the object counts as picking it. Collapsed otherwise. Scaled rather than
        /// resized because Spatial Adapter syncs transforms at runtime.
        /// </summary>
        public void SetObjectBounds(Vector3 size)
        {
            if (m_bounds == null) return;
            Vector3 parent = transform.localScale;
            Vector3 local = size == Vector3.zero ? new Vector3(0.001f, 0.001f, 0.001f)
                : new Vector3(size.x / parent.x, size.y / parent.y, size.z / parent.z);
            if ((m_bounds.localScale - local).sqrMagnitude < 1e-5f) return;
            m_bounds.localScale = local;
        }

        /// <summary>Distance from the card centre down to the dot on the object.</summary>
        public void SetLeaderLength(float metres)
        {
            if (Mathf.Abs(metres - m_leaderLength) < 0.005f) return;
            m_leaderLength = metres;
            LayoutLeader();
        }

        private void ApplyColours()
        {
            if (m_theme == null || m_text == null) return;
            float a = m_opacity < 0f ? 1f : m_opacity;
            // Dimmed and lost labels keep a solid card and fade their text toward it: text over a
            // see-through background rendered as dark blocks on Swan. Only the last part of a
            // fade in or out makes the card itself transparent.
            float card = Mathf.Clamp01(a / 0.3f);
            float ink = Mathf.Lerp(0.3f, 1f, a);
            m_text.TextColor = Dim(m_accent ? m_theme.Accent : m_theme.Ink, ink, card);
            m_text.BackgroundColor = Fade(m_theme.Card, card);
            if (m_backing != null) m_backing.BackgroundColor = Fade(m_theme.Card, card);
            if (m_gloss != null)
            {
                m_gloss.TextColor = Dim(m_theme.InkMuted, ink, card);
                m_gloss.BackgroundColor = Fade(m_theme.Card, card);
            }
            if (m_ruby != null)
            {
                m_ruby.TextColor = Dim(m_theme.Accent, ink, card);
                m_ruby.BackgroundColor = Fade(m_theme.Card, card);
            }
            if (m_leader != null) m_leader.BackgroundColor = Fade(m_theme.InkMuted, a * 0.8f);
            if (m_dot != null) m_dot.BackgroundColor = Fade(m_theme.Accent, a);
        }

        private static Color Fade(Color c, float a) => new Color(c.r, c.g, c.b, c.a * a);

        // Blends toward white (the card) by 1 - strength, with the given alpha.
        private static Color Dim(Color c, float strength, float alpha)
        {
            Color mixed = Color.Lerp(Color.white, c, strength);
            mixed.a = c.a * alpha;
            return mixed;
        }

        private void Layout()
        {
            if (m_points <= 0 || m_text == null) return;
            float ems = Mathf.Max(m_cardEms, TextFit.WidestLineEms(CurrentText, out int lines), 1f);
            float metresPerEm = m_points * TextFit.MetresPerPoint;
            float mainHeight = lines * metresPerEm * 1.3f + 2f * PadY;
            float width = ems * metresPerEm;

            // The meaning sits under the word, inside the same card.
            m_glossHeight = 0f;
            if (m_gloss != null && !string.IsNullOrEmpty(m_glossText))
            {
                float glossEm = metresPerEm * GlossScale;
                float glossWidth = TextFit.WidestLineEms(m_glossText, out int glossLines) * glossEm;
                m_glossHeight = glossLines * glossEm * 1.3f + PadY;
                width = Mathf.Max(width, glossWidth);
            }
            m_card = new Vector2(width + 2f * PadX, mainHeight + m_glossHeight);
            // The word and meaning backgrounds are full width and flush, so together they read as
            // one solid card; the backing behind them only adds the rounded outline.
            m_text.BackgroundSize = new Vector2(m_card.x, mainHeight);
            if (m_glossHeight > 0f)
            {
                // Both parts have rounded corners. The meaning's background reaches up under the
                // word's by one radius and sits in front, hiding the word's lower corners, so the
                // two read as one rounded card with no notches at the seam.
                float r = m_theme != null ? m_theme.LabelCornerRadius : 0.008f;
                m_gloss.BackgroundSize = new Vector2(m_card.x, m_glossHeight + r);
                m_gloss.transform.localPosition = new Vector3(0f, -(mainHeight + m_glossHeight) * 0.5f + r * 0.5f, -0.001f);
            }
            float centreY = -m_glossHeight * 0.5f;
            if (m_backing != null)
            {
                // One panel behind the word and the meaning.
                m_backing.BackgroundSize = m_card;
                m_backing.transform.localPosition = new Vector3(0f, centreY, 0.001f);
            }
            if (m_collider != null)
            {
                m_collider.size = new Vector3(m_card.x, m_card.y, 0.01f);
                m_collider.center = new Vector3(0f, centreY, 0f);
            }

            m_rubyHeight = 0f;
            if (m_ruby != null && !string.IsNullOrEmpty(m_rubyText))
            {
                float rubyEm = metresPerEm * RubyScale;
                m_rubyHeight = rubyEm * 1.4f;
                m_ruby.BackgroundSize = new Vector2(TextFit.WidestLineEms(m_rubyText, out _) * rubyEm + 2f * PadX, m_rubyHeight);
                m_ruby.transform.localPosition = new Vector3(0f, (mainHeight + m_rubyHeight) * 0.5f, 0f);
            }
            LayoutLeader();
        }

        // The leader runs from the bottom of the card to the dot, slightly behind the card.
        private void LayoutLeader()
        {
            if (m_leader == null || m_dot == null || m_leaderLength < 0f) return;
            // From the bottom of the card, which extends below the word by the meaning line.
            float top = (m_card.y - m_glossHeight) * 0.5f + m_glossHeight;
            float length = Mathf.Max(0f, m_leaderLength - top);
            m_leader.BackgroundSize = new Vector2(LeaderWidth, length);
            m_leader.transform.localPosition = new Vector3(0f, -(top + length * 0.5f), 0.002f);
            m_dot.transform.localPosition = new Vector3(0f, -m_leaderLength, 0.001f);
            if (m_bounds != null) m_bounds.localPosition = new Vector3(0f, -m_leaderLength, 0f);
        }
    }
}
