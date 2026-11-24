using UnityEngine;
using UnityEngine.XR;

namespace WordWorld.Spatial
{
    /// <summary>
    /// Latest tracked head pose. Read from the XR head node rather than Camera.main because in
    /// Spatial mode the scene camera is the window camera, not the user's eyes.
    /// </summary>
    public static class HeadPose
    {
        // Tracking reports a few frames of unsettled pose right after it starts; a window placed
        // from one of them landed at the user's feet (V2 review). Wait this long after tracking begins.
        private const float SettleSeconds = 0.5f;
        private static float s_trackedSince = -1f;
        private static int s_checkedFrame = -1;
        private static bool s_lastOk;
        private static Pose s_lastPose;

        public static bool TryGet(out Pose pose)
        {
            // Cached per frame: many components ask every frame.
            if (s_checkedFrame == Time.frameCount)
            {
                pose = s_lastPose;
                return s_lastOk;
            }
            s_checkedFrame = Time.frameCount;
            s_lastOk = Read(out s_lastPose);
            pose = s_lastPose;
            return s_lastOk;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor only: a head pose for play mode without a headset (tools/verification). Ignored
        /// in builds, and while a real head is tracked.
        /// </summary>
        public static Pose? EditorOverride { get; set; }
#endif

        private static bool Read(out Pose pose)
        {
            pose = default;
            InputDevice head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
#if UNITY_EDITOR
            if (!head.isValid && EditorOverride.HasValue)
            {
                pose = EditorOverride.Value;
                return true;
            }
#endif
            bool tracked = head.isValid &&
                (!head.TryGetFeatureValue(CommonUsages.isTracked, out bool isTracked) || isTracked) &&
                head.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 position) &&
                head.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rotation) &&
                // An untracked head reports exactly the origin; placing windows from it put the
                // menu metres away (seen when the app started while the headset was off the head).
                (position.sqrMagnitude > 1e-8f || rotation != Quaternion.identity);
            if (!tracked)
            {
                s_trackedSince = -1f;
                return false;
            }
            head.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 p);
            head.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion r);
            if (s_trackedSince < 0f) s_trackedSince = Time.unscaledTime;
            if (Time.unscaledTime - s_trackedSince < SettleSeconds) return false;
            pose = new Pose(p, r);
            return true;
        }
    }
}
