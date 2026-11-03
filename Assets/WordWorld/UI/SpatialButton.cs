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

        public event Action<SpatialButton> Clicked;

        public string Value => m_value;
        public SpatialAdapterNativeText Text => m_text;

        public void Bind(string value, SpatialAdapterNativeText text)
        {
            m_value = value;
            m_text = text;
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
