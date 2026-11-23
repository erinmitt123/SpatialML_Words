using ByteDance.PICO.SpatialAdapter.Component;
using ByteDance.PICO.XR;
using UnityEngine;
using UnityEngine.EventSystems;

namespace WordWorld.Spatial
{
    /// <summary>
    /// A ray from each tracked index fingertip, always shown while the hand is tracked (Nov 16
    /// review: "there should be a ray coming from my finger at all times to tap on it"). In spatial
    /// mode the system targets with eye gaze and draws no hand ray, so the app draws its own: it
    /// follows the hand's aim pose, lights up whatever it points at (the same hover a gaze or
    /// controller gives) and taps it on an index pinch. Poke and gaze-pinch keep working; ClickGate
    /// stops one pinch counting twice and keeps a gaze-pinch from tapping something the ray is not on.
    /// </summary>
    [AddComponentMenu("Word World/Finger Ray")]
    public sealed class FingerRay : MonoBehaviour
    {
        [Tooltip("Beam plate per hand, left then right (native text background).")]
        [SerializeField] private SpatialAdapterNativeText[] m_beams = new SpatialAdapterNativeText[2];
        [Tooltip("Dot plate per hand where the ray lands.")]
        [SerializeField] private SpatialAdapterNativeText[] m_dots = new SpatialAdapterNativeText[2];
        [SerializeField] private float m_beamWidth = 0.0035f;
        [Tooltip("Length when the ray points at nothing.")]
        [SerializeField] private float m_idleLength = 0.6f;
        [SerializeField] private float m_maxDistance = 4f;
        [SerializeField] private Color m_idleColor = new Color(1f, 1f, 1f, 0.55f);
        [SerializeField] private Color m_targetColor = new Color32(0x1E, 0x6E, 0x58, 0xE6);

        private readonly HandState[] m_hands = { new HandState(HandType.HandLeft), new HandState(HandType.HandRight) };
        private readonly RaycastHit[] m_hits = new RaycastHit[16];
        private bool m_loggedState;
        private float m_firstTry = -1f;

        /// <summary>Clickable objects a ray is on right now (null when none).</summary>
        public static GameObject LeftTarget { get; private set; }
        public static GameObject RightTarget { get; private set; }

        /// <summary>True when any ray is pointing at something clickable.</summary>
        public static bool AnyTarget => LeftTarget != null || RightTarget != null;

        public static bool IsTarget(GameObject go) => go != null && (go == LeftTarget || go == RightTarget);

        private const int LeftPointerId = -51;
        private const int RightPointerId = -52;

        public static bool IsRayPointer(int pointerId) => pointerId == LeftPointerId || pointerId == RightPointerId;

        private sealed class HandState
        {
            public readonly HandType Hand;
            public HandAimState Aim;
            public HandJointLocations Joints;
            public GameObject Target;
            public bool Pinching;
            public PointerEventData Pointer;

            public HandState(HandType hand)
            {
                Hand = hand;
                Joints = new HandJointLocations { jointLocations = new HandJointLocation[(int)HandJoint.JointMax] };
            }
        }

        private void Update()
        {
            if (m_firstTry < 0f) m_firstTry = Time.unscaledTime;
            for (int i = 0; i < m_hands.Length; i++) UpdateHand(i, m_hands[i]);
            LeftTarget = m_hands[0].Target;
            RightTarget = m_hands[1].Target;
        }

        private void OnDisable()
        {
            for (int i = 0; i < m_hands.Length; i++)
            {
                SetTarget(m_hands[i], null);
                Show(i, false);
            }
            LeftTarget = RightTarget = null;
        }

        private void UpdateHand(int index, HandState hand)
        {
            bool aimOk = PXR_Plugin.HandTracking.UPxr_GetHandTrackerAimState(hand.Hand, ref hand.Aim);
            bool tracked = aimOk && (hand.Aim.aimStatus & HandAimStatus.AimComputed) != 0;
            // One line once a session: tells "no hand data in spatial mode" apart from "hidden".
            if (!m_loggedState && (tracked || Time.unscaledTime - m_firstTry > 15f))
            {
                m_loggedState = true;
                Debug.Log($"[WordWorld.FingerRay] hand aim {(tracked ? "available" : "not available")} ({hand.Hand}, call {(aimOk ? "ok" : "failed")}, status {hand.Aim.aimStatus})");
            }
            if (!tracked || !HeadPose.TryGet(out Pose head))
            {
                SetTarget(hand, null);
                hand.Pinching = false;
                Show(index, false);
                return;
            }

            Vector3 direction = hand.Aim.aimRayPose.Orientation.ToQuat() * Vector3.forward;
            Vector3 origin = hand.Aim.aimRayPose.Position.ToVector3();
            // Start at the index fingertip when joints are known, so the ray comes from the finger.
            if (PXR_Plugin.HandTracking.UPxr_GetHandTrackerJointLocations(hand.Hand, ref hand.Joints) &&
                hand.Joints.jointLocations != null && hand.Joints.jointLocations.Length > (int)HandJoint.JointIndexTip)
            {
                HandJointLocation tip = hand.Joints.jointLocations[(int)HandJoint.JointIndexTip];
                if ((tip.locationStatus & HandLocationStatus.PositionValid) != 0) origin = tip.pose.Position.ToVector3();
            }

            GameObject target = Cast(origin, direction, out float distance);
            SetTarget(hand, target, origin, direction, distance);

            bool pinching = (hand.Aim.aimStatus & HandAimStatus.AimIndexPinching) != 0;
            if (pinching && !hand.Pinching && target != null) Click(hand, target, origin + direction * distance);
            hand.Pinching = pinching;

            Draw(index, head, origin, direction, target != null ? distance : m_idleLength, target != null);
        }

        // The nearest thing a pointer could tap. Room mesh and the rays' own plates are skipped.
        private GameObject Cast(Vector3 origin, Vector3 direction, out float distance)
        {
            distance = m_idleLength;
            int count = Physics.RaycastNonAlloc(origin, direction, m_hits, m_maxDistance, ~0, QueryTriggerInteraction.Collide);
            float best = float.MaxValue;
            GameObject found = null;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = m_hits[i];
                if (hit.distance >= best || hit.collider.transform.IsChildOf(transform)) continue;
                GameObject handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit.collider.gameObject);
                if (handler == null) continue;
                best = hit.distance;
                found = handler;
            }
            if (found != null) distance = best;
            return found;
        }

        private void SetTarget(HandState hand, GameObject target, Vector3 origin = default, Vector3 direction = default, float distance = 0f)
        {
            if (hand.Target == target) return;
            PointerEventData data = Pointer(hand, origin + direction * distance);
            if (hand.Target != null) ExecuteEvents.Execute(hand.Target, data, ExecuteEvents.pointerExitHandler);
            hand.Target = target;
            if (target != null) ExecuteEvents.Execute(target, data, ExecuteEvents.pointerEnterHandler);
        }

        private void Click(HandState hand, GameObject target, Vector3 point)
        {
            if (!ClickGate.Allow(target, fromRay: true)) return;
            Debug.Log($"[WordWorld.FingerRay] {hand.Hand} pinch on {target.name}");
            ExecuteEvents.Execute(target, Pointer(hand, point), ExecuteEvents.pointerClickHandler);
        }

        private PointerEventData Pointer(HandState hand, Vector3 point)
        {
            hand.Pointer ??= new PointerEventData(EventSystem.current) { pointerId = hand.Hand == HandType.HandLeft ? LeftPointerId : RightPointerId };
            hand.Pointer.pointerCurrentRaycast = new RaycastResult { gameObject = hand.Target, worldPosition = point };
            return hand.Pointer;
        }

        // The beam is a thin plate along the ray, turned about its own axis to face the eyes.
        private void Draw(int index, Pose head, Vector3 origin, Vector3 direction, float length, bool onTarget)
        {
            SpatialAdapterNativeText beam = index < m_beams.Length ? m_beams[index] : null;
            SpatialAdapterNativeText dot = index < m_dots.Length ? m_dots[index] : null;
            Color color = onTarget ? m_targetColor : m_idleColor;
            if (beam != null)
            {
                Vector3 middle = origin + direction * (length * 0.5f);
                Vector3 toEye = Vector3.ProjectOnPlane(middle - head.position, direction);
                if (toEye.sqrMagnitude < 1e-6f) toEye = Vector3.ProjectOnPlane(head.rotation * Vector3.forward, direction);
                beam.transform.SetPositionAndRotation(middle, Quaternion.LookRotation(toEye, direction));
                beam.BackgroundSize = new Vector2(m_beamWidth, length);
                beam.BackgroundColor = color;
                if (!beam.gameObject.activeSelf) beam.gameObject.SetActive(true);
            }
            if (dot != null)
            {
                Vector3 end = origin + direction * length;
                dot.transform.SetPositionAndRotation(end - direction * 0.004f, Quaternion.LookRotation(end - head.position));
                dot.BackgroundColor = color;
                if (dot.gameObject.activeSelf != onTarget) dot.gameObject.SetActive(onTarget);
            }
        }

        private void Show(int index, bool visible)
        {
            if (index < m_beams.Length && m_beams[index] != null && m_beams[index].gameObject.activeSelf != visible)
                m_beams[index].gameObject.SetActive(visible);
            if (!visible && index < m_dots.Length && m_dots[index] != null && m_dots[index].gameObject.activeSelf)
                m_dots[index].gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// One tap per pinch, whichever path delivers it first: the system's gaze-pinch or poke, or a
    /// finger ray. While a ray is on something, a system tap elsewhere is ignored, so what the ray
    /// shows is what gets tapped.
    /// </summary>
    public static class ClickGate
    {
        private const float RepeatSeconds = 0.35f;
        private static GameObject s_last;
        private static float s_lastTime = -1f;

        /// <summary>For pointer click handlers: a finger ray's click already passed the gate.</summary>
        public static bool Allow(GameObject target, PointerEventData eventData) =>
            (eventData != null && FingerRay.IsRayPointer(eventData.pointerId)) || Allow(target);

        public static bool Allow(GameObject target, bool fromRay = false)
        {
            if (target == null) return true;
            if (!fromRay && FingerRay.AnyTarget && !FingerRay.IsTarget(target)) return false;
            float now = Time.unscaledTime;
            if (target == s_last && now - s_lastTime < RepeatSeconds) return false;
            s_last = target;
            s_lastTime = now;
            return true;
        }
    }
}
