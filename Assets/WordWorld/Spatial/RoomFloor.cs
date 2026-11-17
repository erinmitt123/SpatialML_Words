using UnityEngine;

namespace WordWorld.Spatial
{
    /// <summary>Floor height under a point, from the room mesh when there is one.</summary>
    public static class RoomFloor
    {
        /// <summary>Typical standing eye height, used when the room mesh has nothing below.</summary>
        public const float FallbackEyeHeight = 1.5f;

        public static float Below(Vector3 point)
        {
            if (Physics.Raycast(point, Vector3.down, out RaycastHit hit, 2.5f, RoomMesh.LayerMask, QueryTriggerInteraction.Ignore))
                return hit.point.y;
            return point.y - FallbackEyeHeight;
        }
    }
}
