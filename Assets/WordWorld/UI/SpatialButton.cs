using System;
using ByteDance.PICO.SpatialAdapter.Component;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR;

namespace WordWorld.UI
{
    /// <summary>
    /// A native-text button. Spatial Adapter hit-tests its BoxCollider for poke, pinch-ray and
    /// controller rays alike and sends uGUI pointer events (it needs an EventSystem in the scene).
    /// </summary>
    [AddComponentMenu("Word World/Spatial Button")]
    public sealed class SpatialButton : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [Tooltip("What this button stands for, e.g. a language code.")]
        [SerializeField] private string m_value;
        [SerializeField] private SpatialAdapterNativeText m_text;
        [Tooltip("Authored font size in points; labels shrink below it to fit, never grow past it.")]
        [SerializeField] private float m_points;
        [Tooltip("Authored background size in metres. The label always stays inside it.")]
        [SerializeField] private Vector2 m_size;

        [Header("Hover highlight")]
        [Tooltip("Resting background. Set it through Background so the hover tint can restore it.")]
        [SerializeField] private Color m_background = Color.white;
        [Tooltip("Colour the background leans toward while the button is looked at or pointed at.")]
        [SerializeField] private Color m_hoverTint = new Color32(0x1E, 0x6E, 0x58, 0xFF);
        [Range(0f, 1f)] [SerializeField] private float m_hoverAmount = 0.28f;
        [SerializeField] private float m_hoverScale = 1.06f;

        private bool m_warnedSmall;
        private bool m_hovered;
        private Vector3 m_restScale = Vector3.one;
        private static bool s_loggedHover;
        private static float s_lastHoverLog = -1f;

        public event Action<SpatialButton> Clicked;

        public string Value => m_value;

        private bool m_interactable = true;

        /// <summary>
        /// False shows the button but makes it inert: no hover, no sound, no Clicked. For things that
        /// look like buttons but cannot be used yet, such as a breadcrumb step still to come.
        /// </summary>
        public bool Interactable
        {
            get => m_interactable;
            set
            {
                m_interactable = value;
                if (!value) SetHovered(false);
            }
        }
        public SpatialAdapterNativeText Text => m_text;

        /// <summary>
        /// Resting background colour. Always set it here, not on Text: native text has setters only,
        /// so this is the one place that remembers the colour to return to after a hover.
        /// </summary>
        public Color Background
        {
            get => m_background;
            set
            {
                m_background = value;
                if (m_text != null) m_text.BackgroundColor = m_hovered ? HoverColor() : value;
            }
        }

        public void Bind(string value, SpatialAdapterNativeText text, float points, Vector2 size,
            Color background, Color hoverTint)
        {
            m_value = value;
            m_text = text;
            m_points = points;
            m_size = size;
            m_background = background;
            m_hoverTint = hoverTint;
        }

        private void Awake() => m_restScale = transform.localScale;

        // A window closing under the gaze sends no exit event, so it must not reopen still lit.
        private void OnDisable() => SetHovered(false);

        // Spatial Adapter forwards hover from eye gaze, hand rays and controller rays as uGUI
        // enter/exit. Its own SpatialAdapterHoverEffect draws nothing here because native text has
        // no mesh to light up (seen in the Nov 7 recording: buttons gave no feedback before a tap).
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!s_loggedHover)
            {
                s_loggedHover = true;
                Debug.Log($"[WordWorld.Button] hover events arrive (first: {name})");
            }
            // Which input hovers (eye gaze, hand ray, controller) decides whether gaze highlighting
            // can work at all in spatial mode; one line per hover answers it from a test session.
            if (Time.unscaledTime - s_lastHoverLog > 0.25f)
            {
                s_lastHoverLog = Time.unscaledTime;
                Debug.Log($"[WordWorld.Hover] {name} pointer {eventData?.pointerId} " +
                          $"{eventData?.currentInputModule?.GetType().Name} {eventData?.pointerCurrentRaycast.module?.GetType().Name}");
            }
            if (!m_interactable) return;
            if (!m_hovered) WordWorld.Audio.Sfx.Hover(transform.position);
            SetHovered(true);
        }

        public void OnPointerExit(PointerEventData eventData) => SetHovered(false);

        private void SetHovered(bool hovered)
        {
            if (m_hovered == hovered) return;
            m_hovered = hovered;
            if (m_text != null) m_text.BackgroundColor = hovered ? HoverColor() : m_background;
            transform.localScale = hovered ? m_restScale * m_hoverScale : m_restScale;
        }

        private Color HoverColor()
        {
            Color c = Color.Lerp(m_background, m_hoverTint, m_hoverAmount);
            c.a = Mathf.Max(m_background.a, 0.9f);
            return c;
        }

        /// <summary>
        /// Sets the label and shrinks its font until it fits the button. The button keeps its size so
        /// it never grows into its neighbours (seen Nov 2026: "Listening… tap when done" ran past Speak).
        /// </summary>
        public void SetLabel(string value)
        {
            if (m_text == null) return;
            // Native text has setters only, so fit from the string we were given.
            value ??= "";
            if (m_points <= 0f || m_size.x <= 0f)
            {
                m_text.Text = value;
                return;
            }
            // Wraps onto a second line when the button is tall enough, otherwise shrinks.
            m_text.Text = TextFit.Wrap(value, m_points, m_size, out float points);
            m_text.TextSize = TextFit.ToNative(points);
            m_text.BackgroundSize = m_size;
            if (points < m_points * TextFit.MinShrink && !m_warnedSmall)
            {
                m_warnedSmall = true;
                Debug.LogWarning($"[WordWorld.Button] '{value}' needs a wider {name} button ({points:F0} of {m_points:F0} pt)");
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!m_interactable) return;
            // One pinch is one tap, and a gaze-pinch can't tap a button the finger ray isn't on.
            if (!WordWorld.Spatial.ClickGate.Allow(gameObject, eventData)) return;
            // DESIGN.md section 10: light haptic on controllers for press. Harmless when hands are used.
            Pulse(XRNode.LeftHand);
            Pulse(XRNode.RightHand);
            // One line per tap: tells "the tap never arrived" apart from "it arrived and did nothing".
            Debug.Log($"[WordWorld.Button] {name} ({m_value})");
            // Before the handler, so a Speak tap's ripple is cut cleanly when the microphone opens.
            WordWorld.Audio.Sfx.Tap(transform.position);
            Clicked?.Invoke(this);
        }

        private static void Pulse(XRNode node)
        {
            InputDevice device = InputDevices.GetDeviceAtXRNode(node);
            if (device.isValid && device.TryGetHapticCapabilities(out HapticCapabilities caps) && caps.supportsImpulse)
                device.SendHapticImpulse(0, 0.25f, 0.04f);
        }
    }
}
