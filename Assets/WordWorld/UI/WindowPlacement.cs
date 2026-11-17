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
        /// In front of an object but within arm's reach: on the line from the user to
        /// <paramref name="point"/>, <paramref name="distance"/> from the eyes, so the card is clearly
        /// about that object yet pokeable. Cards opened at the object itself ended up 2.5 m away and
        /// behind other windows (Nov 7 recording, 4:42). Height follows the line to the object but stays
        /// in the comfortable poke band below the eyes. Upright and facing the user.
        /// </summary>
        public static bool PlaceNear(Transform target, Vector3 point, float distance = 0.6f,
            float highest = -0.1f, float lowest = -0.32f)
        {
            if (target == null || !HeadPose.TryGet(out Pose head)) return false;
            Vector3 toPoint = point - head.position;
            Vector3 flat = new Vector3(toPoint.x, 0f, toPoint.z);
            if (flat.sqrMagnitude < 0.01f) return false;
            Quaternion facing = Quaternion.LookRotation(flat.normalized, Vector3.up);
            // Where the sight line to the object passes at this distance, clamped to the poke band.
            float drop = toPoint.y * distance / Mathf.Max(flat.magnitude, distance);
            Vector3 position = head.position + facing * new Vector3(0f, 0f, distance);
            position.y = head.position.y + Mathf.Clamp(drop, lowest, highest);
            target.SetPositionAndRotation(position, facing);
            return true;
        }

        /// <summary>Re-centres a status line once the user has looked more than this far away from it.</summary>
        public static void KeepInView(Transform target, Vector3 offset, float maxDegrees)
        {
            if (target == null || !HeadPose.TryGet(out Pose head)) return;
            Vector3 toTarget = target.position - head.position;
            Vector3 forward = head.rotation * Vector3.forward;
            // Also when the user stands up or sits down, so the dock never ends up at their feet.
            bool wrongHeight = Mathf.Abs(toTarget.y - offset.y) > 0.35f;
            toTarget.y = 0f;
            forward.y = 0f;
            if (wrongHeight || Vector3.Angle(toTarget, forward) > maxDegrees) PlaceInFront(target, offset);
        }
    }
}
