using UnityEngine;
using WordWorld.Detection;

namespace WordWorld.Tracking
{
    /// <summary>
    /// Re-acquisition test for confirmed objects. Port of YoloObjectGeometry.score from PICO's
    /// yolo-mesh sample, minus its multi-view ray relocalisation and ambiguity bookkeeping.
    ///
    /// A fixed sphere is the wrong shape for a returning object: depth error grows along the
    /// viewing ray with range, and a partial view (one end of a couch) moves the box centre by
    /// a large fraction of the object's size. The gate is therefore an ellipsoid aligned with the
    /// camera ray and scaled by object size, and the image footprints must agree.
    /// </summary>
    public static class ObjectGeometry
    {
        private const float NearbyReacquisition = 0.16f;
        private const float MaxJump = 0.6f;

        /// <summary>
        /// Cost of explaining <paramref name="detection"/> with a confirmed track, or a negative value
        /// when it cannot be the same object. Lower is better; 1 is the edge of the 3D gate.
        /// </summary>
        public static float ConfirmedCost(TrackedObject track, in PlacedDetection detection, Pose camera)
        {
            if (track.ClassId != detection.ClassId) return -1f;
            Vector3 delta = detection.Position - track.Position;
            if (delta.magnitude > MaxJump) return -1f;

            Vector3 ray = ViewRay(track.Position, camera);
            float along = Vector3.Dot(delta, ray);
            float radial = Mathf.Abs(along);
            float lateral = (delta - ray * along).magnitude;

            float side = Mathf.Min(track.Width, track.Height);
            float surface = Mathf.Clamp(side * 0.15f, 0.025f, 0.10f);
            float range = Mathf.Clamp((detection.Position - camera.position).magnitude, 0.5f, 4f);
            float depthNoise = 0.06f + 0.02f * range * range;
            float depthGate = Mathf.Clamp(3f * Mathf.Sqrt(depthNoise * depthNoise + surface * surface), 0.25f, 0.5f);
            float lateralGate = Mathf.Clamp(side * 0.4f + 0.06f, 0.14f, 0.36f);
            float normalized = radial * radial / (depthGate * depthGate) +
                               lateral * lateral / (lateralGate * lateralGate);
            if (normalized > 1f) return -1f;

            bool sizesCompatible = SizeRatio(track.Width, detection.Width) >= 0.35f &&
                                   SizeRatio(track.Height, detection.Height) >= 0.35f;
            bool accepted;
            if (!Footprint(track.Position, track.Width, track.Height, detection, camera, out float iou, out float containment))
            {
                accepted = false;
            }
            else
            {
                // Direct support: boxes overlap and depth agrees (or overlap is near-total).
                bool direct = iou >= 0.40f && (radial <= depthGate || iou >= 0.80f);
                // A fuller (or partial) view of a known object sits inside its old footprint.
                bool contained = containment >= 0.85f && lateral <= 0.12f && sizesCompatible;
                accepted = direct || contained;
            }
            // Small objects jump across their own footprint when the detector switches visible parts.
            if (!accepted) accepted = radial * radial + lateral * lateral <= NearbyReacquisition * NearbyReacquisition &&
                                      sizesCompatible;
            if (!accepted) return -1f;

            // Size is a soft cost: depth error changes both the centre and the metric dimensions.
            float sizeCost = 0.06f * (Mathf.Abs(Mathf.Log(detection.Width / track.Width)) +
                                      Mathf.Abs(Mathf.Log(detection.Height / track.Height)));
            return normalized + sizeCost;
        }

        private static Vector3 ViewRay(Vector3 center, Pose camera)
        {
            Vector3 offset = center - camera.position;
            float length = offset.magnitude;
            return length > 0.1f && float.IsFinite(length) ? offset / length : camera.forward;
        }

        /// <summary>IoU and containment of the two boxes projected onto the camera's image plane.</summary>
        private static bool Footprint(Vector3 trackPosition, float trackWidth, float trackHeight,
            in PlacedDetection detection, Pose camera, out float iou, out float containment)
        {
            iou = containment = 0f;
            if (!Project(trackPosition, trackWidth, trackHeight, camera, out Rect a) ||
                !Project(detection.Position, detection.Width, detection.Height, camera, out Rect b)) return false;
            float x = Mathf.Max(0f, Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin));
            float y = Mathf.Max(0f, Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin));
            float area = x * y;
            float areaA = a.width * a.height, areaB = b.width * b.height;
            if (areaA <= 0f || areaB <= 0f) return false;
            iou = area / (areaA + areaB - area);
            containment = area / Mathf.Min(areaA, areaB);
            return true;
        }

        private static bool Project(Vector3 position, float width, float height, Pose camera, out Rect box)
        {
            Vector3 relative = position - camera.position;
            float depth = Vector3.Dot(relative, camera.forward);
            box = default;
            if (depth <= 0.15f) return false;
            float cx = Vector3.Dot(relative, camera.right) / depth;
            float cy = Vector3.Dot(relative, camera.up) / depth;
            float w = width / depth, h = height / depth;
            box = new Rect(cx - w * 0.5f, cy - h * 0.5f, w, h);
            return true;
        }

        private static float SizeRatio(float a, float b) => a <= 0f || b <= 0f ? 0f : Mathf.Min(a, b) / Mathf.Max(a, b);
    }
}
