using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.XR;
using WordWorld.Detection;
using WordWorld.Spatial;
using WordWorld.Tracking;

namespace WordWorld.DebugTools
{
    /// <summary>
    /// P0 debug panel: FPS, detector state, raw detections and confirmed objects. Development
    /// builds only (CLAUDE.md "no debug UI in release"). Left controller Y toggles it.
    /// </summary>
    [AddComponentMenu("Word World/Detection Debug Panel")]
    public sealed class DetectionDebugPanel : MonoBehaviour
    {
        [SerializeField] private GameObject m_panelRoot;
        [SerializeField] private TextMeshProUGUI m_text;
        [SerializeField] private MonoBehaviour m_detector;
        [SerializeField] private TrackingManager m_tracking;
        [SerializeField] private RoomMesh m_roomMesh;
        [SerializeField] private float m_refreshInterval = 0.25f;
        [Tooltip("Where the panel appears relative to the user's first head pose.")]
        [SerializeField] private Vector3 m_offsetFromHead = new Vector3(-0.45f, -0.15f, 1.1f);
        [SerializeField] private int m_maxRawRows = 8;

        private readonly StringBuilder m_builder = new StringBuilder(1024);
        private IObjectDetector m_source;
        private float m_nextRefresh;
        private int m_frames;
        private float m_fpsWindowStart;
        private float m_fps;
        private bool m_placed;
        private bool m_toggleWasDown;

        private void Awake()
        {
            if (!Debug.isDebugBuild && !Application.isEditor)
            {
                if (m_panelRoot != null) m_panelRoot.SetActive(false);
                enabled = false;
                return;
            }
            m_source = m_detector as IObjectDetector;
            m_fpsWindowStart = Time.unscaledTime;
        }

        private void Update()
        {
            m_frames++;
            float now = Time.unscaledTime;
            if (now - m_fpsWindowStart >= 1f)
            {
                m_fps = m_frames / (now - m_fpsWindowStart);
                m_frames = 0;
                m_fpsWindowStart = now;
            }

            if (!m_placed) TryPlaceInFrontOfUser();
            PollToggle();
            if (now < m_nextRefresh || m_panelRoot == null || !m_panelRoot.activeSelf) return;
            m_nextRefresh = now + m_refreshInterval;
            Refresh();
        }

        private void TryPlaceInFrontOfUser()
        {
            if (m_panelRoot == null || !HeadPose.TryGet(out Pose head)) return;
            Vector3 forward = head.rotation * Vector3.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-4f) return;
            Quaternion yaw = Quaternion.LookRotation(forward.normalized, Vector3.up);
            m_panelRoot.transform.SetPositionAndRotation(head.position + yaw * m_offsetFromHead, yaw);
            m_placed = true;
        }

        private void PollToggle()
        {
            InputDevice left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            bool down = left.isValid && left.TryGetFeatureValue(CommonUsages.secondaryButton, out bool pressed) && pressed;
            if (down && !m_toggleWasDown && m_panelRoot != null) m_panelRoot.SetActive(!m_panelRoot.activeSelf);
            m_toggleWasDown = down;
        }

        private void Refresh()
        {
            if (m_text == null) return;
            StringBuilder b = m_builder;
            b.Clear();
            b.Append("<b>Word World debug</b>   FPS ").Append(m_fps.ToString("0")).Append('\n');
            if (m_source != null)
                b.Append("Detector: ").Append(m_source.Status)
                    .Append("  (").Append(m_source.InferenceRate.ToString("0.0")).Append(" Hz)\n");
            if (m_roomMesh != null) b.Append("Room mesh: ").Append(m_roomMesh.Status).Append('\n');
            if (m_tracking != null)
            {
                b.Append("Rays ").Append(m_tracking.LastRayCount)
                    .Append("  placed ").Append(m_tracking.LastPlacedCount)
                    .Append(" (stereo ").Append(m_tracking.LastStereoCount).Append(')')
                    .Append("  candidates ").Append(m_tracking.CandidateCount)
                    .Append("  confirmed ").Append(m_tracking.Confirmed.Count)
                    .Append(m_tracking.LastFrameReliable ? "" : "  <color=#ffb000>(moving)</color>").Append('\n');

                b.Append("<size=80%>Raw:");
                int rows = Mathf.Min(m_maxRawRows, m_tracking.LastRays.Count);
                for (int i = 0; i < rows; i++)
                {
                    DetectionRay ray = m_tracking.LastRays[i];
                    bool usable = ray.Confidence >= m_tracking.ConfidenceThreshold && CocoClasses.IsHousehold(ray.ClassId);
                    b.Append(i % 2 == 0 ? "\n  " : "   ")
                        .Append(usable ? "" : "<color=#888888>")
                        .Append(CocoClasses.NameOf(ray.ClassId)).Append(' ').Append(ray.Confidence.ToString("0.00"))
                        .Append(usable ? "" : "</color>");
                }
                b.Append("\nConfirmed:");
                foreach (TrackedObject track in m_tracking.Confirmed)
                    b.Append("\n  ").Append(track.ClassName).Append(" #").Append(track.Id)
                        .Append(' ').Append(track.Confidence.ToString("0.00"));
                b.Append("</size>");
            }
            m_text.SetText(b);
        }
    }
}
