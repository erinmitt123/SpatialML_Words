using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WordWorld.Labels
{
    /// <summary>
    /// One authored world-space label canvas. Spatial Adapter only exports objects that exist in the
    /// scene at build time, so labels are pooled scene objects that get re-texted and moved.
    /// </summary>
    [AddComponentMenu("Word World/Label View")]
    public sealed class LabelView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI m_text;
        [SerializeField] private Image m_background;

        public TextMeshProUGUI Text => m_text;
        public Image Background => m_background;

        public void Bind(TextMeshProUGUI text, Image background)
        {
            m_text = text;
            m_background = background;
        }

        public void SetText(string value)
        {
            if (m_text != null && m_text.text != value) m_text.text = value;
        }
    }
}
