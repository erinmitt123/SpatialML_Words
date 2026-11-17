using ByteDance.PICO.SpatialAdapter.Component;
using UnityEngine;

namespace WordWorld.UI
{
    /// <summary>
    /// Remembers a native text's authored size and box, which native text can't report back (it has
    /// setters only). The scene builder adds one to every text it creates, so SetFitted can wrap
    /// and shrink any string into the space the layout gave it.
    /// </summary>
    [AddComponentMenu("Word World/Text Box")]
    public sealed class TextBox : MonoBehaviour
    {
        [Tooltip("Authored size in points (after the builder's window text scale). Text never grows past it.")]
        [SerializeField] private float m_points;
        [Tooltip("The space the text may use, in metres: its background size when it was built.")]
        [SerializeField] private Vector2 m_box;

        public float Points => m_points;
        public Vector2 Box
        {
            get => m_box;
            set => m_box = value;
        }

        public void Bind(float points, Vector2 box)
        {
            m_points = points;
            m_box = box;
        }
    }

    public static class TextBoxExtensions
    {
        /// <summary>
        /// Sets the text, wrapped onto more lines and shrunk if needed so it stays inside its box.
        /// Falls back to a plain set for texts without a TextBox (labels size their own background).
        /// </summary>
        public static void SetFitted(this SpatialAdapterNativeText text, string value)
        {
            if (text == null) return;
            value ??= "";
            var box = text.GetComponent<TextBox>();
            if (box == null || box.Points <= 0f || box.Box.x <= 0f)
            {
                text.Text = value;
                return;
            }
            string wrapped = TextFit.Wrap(value, box.Points, box.Box, out float points);
            text.Text = wrapped;
            text.TextSize = TextFit.ToNative(points);
            if (points < box.Points * TextFit.MinShrink)
                Debug.LogWarning($"[WordWorld.Text] '{value}' needs a bigger {text.name} box ({points:F0} of {box.Points:F0} pt)");
        }
    }
}
