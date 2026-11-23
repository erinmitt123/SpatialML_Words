using UnityEngine;
using UnityEngine.EventSystems;

namespace WordWorld.Practice
{
    /// <summary>
    /// One practice object: a 3D model standing in for a real object of one COCO class. The root's
    /// origin is the model's bottom centre; its BoxCollider covers the model so a poke, pinch-ray or
    /// controller ray on the model itself works like tapping its label.
    /// </summary>
    [AddComponentMenu("Word World/Practice Model")]
    public sealed class PracticeModel : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private int m_classId;
        [Tooltip("Width, height and depth in metres, measured by the scene builder.")]
        [SerializeField] private Vector3 m_size;
        [Tooltip("Turns the model so its front faces the learner.")]
        [SerializeField] private float m_yawOffset;
        [Tooltip("Placement around the learner: degrees right of where they look, and distance.")]
        [SerializeField] private float m_angle;
        [SerializeField] private float m_distance = 1.8f;
        [Tooltip("Stands on top of this model instead of the floor (the vase on the table).")]
        [SerializeField] private PracticeModel m_standOn;

        public int ClassId => m_classId;
        public Vector3 Size => m_size;
        public float YawOffset => m_yawOffset;
        public float Angle => m_angle;
        public float Distance => m_distance;
        public PracticeModel StandOn => m_standOn;

        public event System.Action<PracticeModel, Vector3> Tapped;

        public void Configure(int classId, Vector3 size, float yawOffset, float angle, float distance, PracticeModel standOn)
        {
            m_classId = classId;
            m_size = size;
            m_yawOffset = yawOffset;
            m_angle = angle;
            m_distance = distance;
            m_standOn = standOn;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!WordWorld.Spatial.ClickGate.Allow(gameObject, eventData)) return;
            Vector3 at = eventData.pointerCurrentRaycast.isValid ? eventData.pointerCurrentRaycast.worldPosition : transform.position;
            Tapped?.Invoke(this, at);
        }
    }
}
