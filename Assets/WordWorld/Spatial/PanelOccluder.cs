using System.Collections.Generic;
using UnityEngine;

namespace WordWorld.Spatial
{
    /// <summary>
    /// Marks a window or card as a flat panel that word labels should not show through. While it is
    /// active, a label that lines up with it from the learner's eyes (in front or behind) fades out,
    /// unless it is part of the current exercise; it comes back as soon as the panel moves or closes.
    /// The scene builder adds one to every window, card and the dock.
    /// </summary>
    [AddComponentMenu("Word World/Panel Occluder")]
    public sealed class PanelOccluder : MonoBehaviour
    {
        [Tooltip("Panel size in metres, in its own plane.")]
        [SerializeField] private Vector2 m_size = new Vector2(0.4f, 0.3f);
        [Tooltip("Centre of the panel in local space, for panels whose pivot is not their middle.")]
        [SerializeField] private Vector2 m_centre;

        private static readonly List<PanelOccluder> s_active = new List<PanelOccluder>();

        public static IReadOnlyList<PanelOccluder> Active => s_active;

        public void Configure(Vector2 size, Vector2 centre)
        {
            m_size = size;
            m_centre = centre;
        }

        private void OnEnable() => s_active.Add(this);
        private void OnDisable() => s_active.Remove(this);

        /// <summary>
        /// True if the point, seen from the eye, falls within this panel grown by
        /// <paramref name="margin"/> metres on each side.
        /// </summary>
        public bool Covers(Vector3 eye, Vector3 point, float margin)
        {
            Transform t = transform;
            Vector3 normal = t.forward;
            Vector3 ray = point - eye;
            float facing = Vector3.Dot(normal, ray);
            if (Mathf.Abs(facing) < 1e-4f) return false; // seen edge on
            Vector3 centre = t.TransformPoint(m_centre);
            float along = Vector3.Dot(normal, centre - eye) / facing;
            if (along <= 0f) return false; // behind the eye
            Vector3 local = t.InverseTransformPoint(eye + ray * along) - (Vector3)m_centre;
            Vector3 scale = t.lossyScale;
            return Mathf.Abs(local.x * scale.x) <= m_size.x * 0.5f + margin &&
                   Mathf.Abs(local.y * scale.y) <= m_size.y * 0.5f + margin;
        }

        // Corners, edge midpoints and centre: enough to catch any overlap of two rectangles that are
        // not wildly different in size, which is all our panels.
        private static readonly Vector2[] s_samples =
        {
            new Vector2(-0.5f, -0.5f), new Vector2(0.5f, -0.5f), new Vector2(-0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, -0.5f), new Vector2(0f, 0.5f), new Vector2(-0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero,
        };

        private Vector3 SamplePoint(Vector2 sample)
        {
            Vector3 scale = transform.lossyScale;
            var local = new Vector3(m_centre.x + sample.x * m_size.x / Mathf.Max(scale.x, 1e-4f),
                m_centre.y + sample.y * m_size.y / Mathf.Max(scale.y, 1e-4f), 0f);
            return transform.TransformPoint(local);
        }

        /// <summary>True if this panel and <paramref name="other"/> overlap as seen from the eye.</summary>
        public bool Overlaps(PanelOccluder other, Vector3 eye, float margin)
        {
            foreach (Vector2 sample in s_samples)
                if (other.Covers(eye, SamplePoint(sample), margin) || Covers(eye, other.SamplePoint(sample), margin))
                    return true;
            return false;
        }

        /// <summary>The first other open panel overlapping this one from the eye, or null.</summary>
        public PanelOccluder FirstOverlap(Vector3 eye, float margin, Transform ignore = null)
        {
            for (int i = 0; i < s_active.Count; i++)
            {
                PanelOccluder other = s_active[i];
                if (other == this || (ignore != null && other.transform == ignore)) continue;
                if (Overlaps(other, eye, margin)) return other;
            }
            return null;
        }

        /// <summary>True if any open panel covers the point as seen from the eye.</summary>
        public static bool AnyCovers(Vector3 eye, Vector3 point, float margin)
        {
            for (int i = 0; i < s_active.Count; i++)
                if (s_active[i].Covers(eye, point, margin)) return true;
            return false;
        }
    }
}
