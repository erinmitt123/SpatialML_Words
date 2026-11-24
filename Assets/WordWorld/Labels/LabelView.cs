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
    public sealed class LabelView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        // DESIGN.md: card padding 8 mm horizontal, 5 mm vertical.
        private const float PadX = 0.008f;
        private const float PadY = 0.005f;
        private const float RubyScale = 0.55f;
        private const float GlossScale = 0.6f;
        private const float LeaderWidth = 0.001f;
        private const float DotSize = 0.006f;
        // Noto Sans draws a line about 1.36 em tall (ascender + descender). Sizing lines at 1.2 em
        // left two-line sentences taller than their card, so descenders were cut off (Nov 19 review).
        private const float LineHeight = 1.36f;
        // Hover (eye gaze, hand ray or controller): an accent ring this wide appears around the card
        // and the card leans toward mint, so it is clear the label is targeted and a pinch will open
        // it (Nov 23 review: no way to tell whether gaze had landed on a label).
        private const float HoverRing = 0.005f;
        private const float HoverTint = 0.6f;

        [SerializeField] private SpatialAdapterNativeText m_text;
        [SerializeField] private SpatialAdapterNativeText m_ruby;
        [Tooltip("The meaning in the interface language, under the word.")]
        [SerializeField] private SpatialAdapterNativeText m_gloss;
        [Tooltip("Background-only panel behind the word and its meaning.")]
        [SerializeField] private SpatialAdapterNativeText m_backing;
        [Tooltip("Empty text behind the card; its background is the hover ring.")]
        [SerializeField] private SpatialAdapterNativeText m_ring;
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
        // 0..1: the card leans toward the "correct" green (room ripple, light-up, right answer).
        private float m_glow;
        // Find It: the card becomes a "This one" button instead of showing the word.
        private bool m_pick;
        private string m_pickText = "";
        // Card width only grows while one word is shown, so changing digits never resize it.
        private float m_cardEms;
        private bool m_hovered;

        /// <summary>Raised when the label is poked or pinched.</summary>
        public event Action<LabelView> Tapped;

        /// <summary>The tap target; its size follows the card.</summary>
        public BoxCollider Collider => m_collider;

        /// <summary>Height of the card (word and meaning) plus its ruby line, for de-overlap.</summary>
        public float HeightMetres => m_card.y + m_rubyHeight;

        public void Bind(SpatialAdapterNativeText text, SpatialAdapterNativeText ruby, SpatialAdapterNativeText gloss,
            SpatialAdapterNativeText backing, SpatialAdapterNativeText leader, SpatialAdapterNativeText dot,
            BoxCollider collider, Transform bounds, WordWorldTheme theme, SpatialAdapterNativeText ring = null)
        {
            m_ring = ring;
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

        public void OnPointerClick(PointerEventData eventData)
        {
            if (WordWorld.Spatial.ClickGate.Allow(gameObject, eventData)) Tapped?.Invoke(this);
        }

        public void OnPointerEnter(PointerEventData eventData) => SetHovered(true);
        public void OnPointerExit(PointerEventData eventData) => SetHovered(false);

        // Pooled labels must not come back still lit.
        private void OnDisable() => SetHovered(false);

        private void SetHovered(bool hovered)
        {
            if (hovered == m_hovered) return;
            m_hovered = hovered;
            if (hovered) WordWorld.Audio.Sfx.Hover(transform.position);
            ApplyColours();
            Layout();
        }

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
            if (m_text != null && !m_pick) m_text.Text = shown;
            if (m_ruby != null)
            {
                m_ruby.Text = ruby ?? "";
                m_ruby.gameObject.SetActive(!m_pick && !string.IsNullOrEmpty(ruby));
            }
            if (m_gloss != null)
            {
                m_gloss.Text = gloss ?? "";
                m_gloss.gameObject.SetActive(!m_pick && !string.IsNullOrEmpty(gloss));
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

        /// <summary>
        /// Find It: the card shows <paramref name="pickText"/> on a button colour instead of the word,
        /// with the leader and dot still pointing at the object, so every object has something to
        /// tap. The word text lives on this label's root object, so it is re-texted, never disabled
        /// (disabling it hid the whole label, dot and touch box included).
        /// </summary>
        public void SetPickMode(bool pick, string pickText)
        {
            pickText ??= "";
            if (pick == m_pick && (!pick || pickText == m_pickText)) return;
            m_pick = pick;
            m_pickText = pickText;
            if (m_text != null) m_text.Text = pick ? pickText : CurrentText ?? "";
            if (m_ruby != null) m_ruby.gameObject.SetActive(!pick && !string.IsNullOrEmpty(m_rubyText));
            if (m_gloss != null) m_gloss.gameObject.SetActive(!pick && !string.IsNullOrEmpty(m_glossText));
            ApplyColours();
            Layout();
        }

        /// <summary>Tints the card toward the theme's Correct green, in tenths: each step is a native update.</summary>
        public void SetGlow(float glow)
        {
            glow = Mathf.Round(Mathf.Clamp01(glow) * 10f) / 10f;
            if (Mathf.Approximately(glow, m_glow)) return;
            m_glow = glow;
            ApplyColours();
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
            Color surface = Color.Lerp(m_pick ? m_theme.Button : m_theme.Card, m_theme.Correct, m_glow * 0.75f);
            if (m_hovered) surface = Color.Lerp(surface, m_theme.AccentSoft, HoverTint);
            m_text.TextColor = Dim(m_accent && !m_pick ? m_theme.Accent : m_theme.Ink, ink, card);
            m_text.BackgroundColor = Fade(surface, card);
            if (m_backing != null) m_backing.BackgroundColor = Fade(surface, card);
            if (m_ring != null) m_ring.BackgroundColor = m_hovered ? Fade(m_theme.Accent, card) : Color.clear;
            if (m_gloss != null)
            {
                m_gloss.TextColor = Dim(m_theme.InkMuted, ink, card);
                m_gloss.BackgroundColor = Fade(m_hovered ? surface : m_theme.Card, card);
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
            string glossText = m_pick ? null : m_glossText;
            string rubyText = m_pick ? null : m_rubyText;
            float ems = TextFit.WidestLineEms(m_pick ? m_pickText : CurrentText, out int lines);
            ems = Mathf.Max(m_pick ? 0f : m_cardEms, ems, 1f);
            float metresPerEm = m_points * TextFit.MetresPerPoint;
            float mainHeight = lines * metresPerEm * LineHeight + 2f * PadY;
            float width = ems * metresPerEm;

            // The meaning sits under the word, inside the same card.
            m_glossHeight = 0f;
            if (m_gloss != null && !string.IsNullOrEmpty(glossText))
            {
                float glossEm = metresPerEm * GlossScale;
                float glossWidth = TextFit.WidestLineEms(glossText, out int glossLines) * glossEm;
                m_glossHeight = glossLines * glossEm * LineHeight + PadY;
                width = Mathf.Max(width, glossWidth);
            }
            m_card = new Vector2(width + 2f * PadX, mainHeight + m_glossHeight);
            // DESIGN.md section 10: a button is at least 3 x 3 cm, even on a far or short label.
            if (m_pick)
            {
                mainHeight = Mathf.Max(mainHeight, 0.03f);
                m_card = new Vector2(Mathf.Max(m_card.x, 0.04f), mainHeight);
            }
            // The word and meaning backgrounds are full width and flush, so together they read as
            // one solid card; the backing behind them only adds the rounded outline.
            m_text.BackgroundSize = new Vector2(m_card.x, mainHeight);
            if (m_glossHeight > 0f)
            {
                // Flush under the word, not overlapping it: the meaning used to reach up one corner
                // radius in front of the word to hide its rounded lower corners, and that strip
                // covered the word's descenders ("p" in "por favor", Nov 19 review). The backing
                // behind both, in the same colour, already fills the notches at the seam. Kept 1 mm in
                // front of the backing, so the hover ring behind it only shows around the edge.
                m_gloss.BackgroundSize = new Vector2(m_card.x, m_glossHeight);
                m_gloss.transform.localPosition = new Vector3(0f, -(mainHeight + m_glossHeight) * 0.5f, -0.001f);
            }
            float centreY = -m_glossHeight * 0.5f;
            if (m_backing != null)
            {
                // One panel behind the word and the meaning.
                m_backing.BackgroundSize = m_card;
                m_backing.transform.localPosition = new Vector3(0f, centreY, 0.001f);
            }
            if (m_ring != null)
            {
                // Just behind the backing and a little larger, so only its edge shows.
                m_ring.BackgroundSize = m_card + new Vector2(2f * HoverRing, 2f * HoverRing);
                m_ring.transform.localPosition = new Vector3(0f, centreY, 0.002f);
            }
            if (m_collider != null)
            {
                m_collider.size = new Vector3(m_card.x, m_card.y, 0.01f);
                m_collider.center = new Vector3(0f, centreY, 0f);
            }

            m_rubyHeight = 0f;
            if (m_ruby != null && !string.IsNullOrEmpty(rubyText))
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
