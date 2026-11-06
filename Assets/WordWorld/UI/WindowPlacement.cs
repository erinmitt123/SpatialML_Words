using UnityEngine;
using WordWorld.Spatial;

namespace WordWorld.UI
{
    /// <summary>
    /// Where windows and status lines appear. Windows are placed once, upright and facing the user;
    /// they never follow the head (DESIGN.md section 10). Status lines re-centre in one step.
    /// </summary>
    public static class WindowPlacement
    {
        /// <summary>The user's head yaw only, so windows stay upright.</summary>
        public static bool TryGetYaw(out Pose head, out Quaternion yaw)
        {
            yaw = Quaternion.identity;
            if (!HeadPose.TryGet(out head)) return false;
            Vector3 forward = head.rotation * Vector3.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-4f) forward = Vector3.forward;
            yaw = Quaternion.LookRotation(forward.normalized, Vector3.up);
            return true;
        }

        public static bool PlaceInFront(Transform target, Vector3 offset)
        {
            if (target == null || !TryGetYaw(out Pose head, out Quaternion yaw)) return false;
            target.SetPositionAndRotation(head.position + yaw * offset, yaw);
            return true;
        }

        /// <summary>
        /// One window of a curved row around the user: <paramref name="degrees"/> to the right of
        /// where they look, at <paramref name="radius"/>, facing them.
        /// </summary>
        public static bool PlaceOnArc(Transform target, float degrees, float radius, float height)
        {
            if (target == null || !TryGetYaw(out Pose head, out Quaternion yaw)) return false;
            Quaternion facing = yaw * Quaternion.Euler(0f, degrees, 0f);
            target.SetPositionAndRotation(head.position + facing * new Vector3(0f, height, radius), facing);
            return true;
        }

        /// <summary>
        /// Beside an object, on the user's side of it: along the line to <paramref name="point"/>, at its
        /// distance but pulled in to between <paramref name="minDistance"/> and
        /// <paramref name="maxDistance"/> so text stays readable. Shifted <paramref name="side"/> metres
        /// toward where the user is looking so the card doesn't cover the object, and kept between
        /// chest and eye height. Upright and facing the user like every other window.
        /// </summary>
        public static bool PlaceBeside(Transform target, Vector3 point, float side = 0.3f,
            float minDistance = 0.7f, float maxDistance = 1.3f)
        {
            if (target == null || !HeadPose.TryGet(out Pose head)) return false;
            Vector3 toPoint = point - head.position;
            toPoint.y = 0f;
            if (toPoint.sqrMagnitude < 0.01f) return false;
            Quaternion facing = Quaternion.LookRotation(toPoint.normalized, Vector3.up);
            Vector3 gaze = head.rotation * Vector3.forward;
            float towardGaze = Vector3.Dot(gaze, facing * Vector3.right) >= 0f ? 1f : -1f;
            float distance = Mathf.Clamp(toPoint.magnitude, minDistance, maxDistance);
            Vector3 position = head.position + facing * new Vector3(side * towardGaze, 0f, distance);
            position.y = Mathf.Clamp(point.y, head.position.y - 0.45f, head.position.y + 0.05f);
            target.SetPositionAndRotation(position, facing);
            return true;
        }

        /// <summary>Re-centres a status line once the user has looked more than this far away from it.</summary>
        public static void KeepInView(Transform target, Vector3 offset, float maxDegrees)
        {
            if (target == null || !HeadPose.TryGet(out Pose head)) return;
            Vector3 toTarget = target.position - head.position;
            Vector3 forward = head.rotation * Vector3.forward;
            toTarget.y = 0f;
            forward.y = 0f;
            if (Vector3.Angle(toTarget, forward) > maxDegrees) PlaceInFront(target, offset);
        }
    }
}
