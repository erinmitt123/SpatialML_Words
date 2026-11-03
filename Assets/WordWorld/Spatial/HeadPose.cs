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
        public static bool TryGet(out Pose pose)
        {
            InputDevice head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            if (head.isValid &&
                head.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 position) &&
                head.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rotation) &&
                // An untracked head reports exactly the origin; placing windows from it put the
                // menu metres away (seen when the app started while the headset was off the head).
                (position.sqrMagnitude > 1e-8f || rotation != Quaternion.identity))
            {
                pose = new Pose(position, rotation);
                return true;
            }
            pose = default;
            return false;
        }
    }
}
