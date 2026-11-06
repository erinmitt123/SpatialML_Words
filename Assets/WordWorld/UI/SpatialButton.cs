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
    public sealed class SpatialButton : MonoBehaviour, IPointerClickHandler
    {
        [Tooltip("What this button stands for, e.g. a language code.")]
        [SerializeField] private string m_value;
        [SerializeField] private SpatialAdapterNativeText m_text;
        [Tooltip("Authored font size in points; labels shrink below it to fit, never grow past it.")]
        [SerializeField] private float m_points;
        [Tooltip("Authored background size in metres. The label always stays inside it.")]
        [SerializeField] private Vector2 m_size;

        private bool m_warnedSmall;

        public event Action<SpatialButton> Clicked;

        public string Value => m_value;
        public SpatialAdapterNativeText Text => m_text;

        public void Bind(string value, SpatialAdapterNativeText text, float points, Vector2 size)
        {
            m_value = value;
            m_text = text;
            m_points = points;
            m_size = size;
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
            m_text.Text = value;
            if (m_points <= 0f || m_size.x <= 0f) return;
            float points = TextFit.FitPoints(value, m_points, m_size);
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
            // DESIGN.md section 10: light haptic on controllers for press. Harmless when hands are used.
            Pulse(XRNode.LeftHand);
            Pulse(XRNode.RightHand);
            // One line per tap: tells "the tap never arrived" apart from "it arrived and did nothing".
            Debug.Log($"[WordWorld.Button] {name} ({m_value})");
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
