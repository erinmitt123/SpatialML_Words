using System.Collections.Generic;
using UnityEngine;
using WordWorld.Spatial;
using WordWorld.Tracking;

namespace WordWorld.Labels
{
    /// <summary>
    /// P0 labels: plain English class names above confirmed objects. Billboarded toward the user,
    /// scaled with distance within limits, and pushed apart vertically when crowded.
    /// </summary>
    [AddComponentMenu("Word World/Debug Labels")]
    public sealed class DebugLabels : MonoBehaviour
    {
        [SerializeField] private TrackingManager m_tracking;
        [SerializeField] private LabelView[] m_pool = System.Array.Empty<LabelView>();

        [Header("Placement")]
        [SerializeField] private float m_gapAboveObject = 0.06f;
        [Tooltip("Canvas scale at 1 m. Canvases are authored at 0.001 (1 px = 1 mm).")]
        [SerializeField] private float m_scaleAtOneMetre = 0.001f;
        [SerializeField] private float m_minScale = 0.0008f;
        [SerializeField] private float m_maxScale = 0.0025f;
        [Tooltip("Label height in canvas pixels, used for de-overlap.")]
        [SerializeField] private float m_labelHeightPixels = 72f;

        private readonly Dictionary<TrackedObject, LabelView> m_active = new Dictionary<TrackedObject, LabelView>();
        private readonly Stack<LabelView> m_free = new Stack<LabelView>();
        private readonly List<Placement> m_placements = new List<Placement>(32);

        private struct Placement
        {
            public LabelView View;
            public Vector3 Position;
            public float Scale;
            public float Distance;
        }

        private void Awake()
        {
            for (int i = m_pool.Length - 1; i >= 0; i--)
            {
                if (m_pool[i] == null) continue;
                m_pool[i].gameObject.SetActive(false);
                m_free.Push(m_pool[i]);
            }
        }

        private void OnEnable()
        {
            if (m_tracking == null) return;
            m_tracking.ObjectConfirmed += Show;
            m_tracking.ObjectRemoved += Hide;
        }

        private void OnDisable()
        {
            if (m_tracking == null) return;
            m_tracking.ObjectConfirmed -= Show;
            m_tracking.ObjectRemoved -= Hide;
        }

        private void Show(TrackedObject track)
        {
            if (m_active.ContainsKey(track)) return;
            if (m_free.Count == 0)
            {
                Debug.LogWarning($"[WordWorld.Labels] label pool exhausted ({m_pool.Length}); '{track.ClassName}' not shown");
                return;
            }
            LabelView view = m_free.Pop();
            view.SetText(track.ClassName);
            m_active.Add(track, view);
            view.gameObject.SetActive(true);
        }

        private void Hide(TrackedObject track)
        {
            if (!m_active.TryGetValue(track, out LabelView view)) return;
            view.gameObject.SetActive(false);
            m_active.Remove(track);
            m_free.Push(view);
        }

        private void LateUpdate()
        {
            if (m_active.Count == 0 || !HeadPose.TryGet(out Pose head)) return;

            m_placements.Clear();
            foreach (KeyValuePair<TrackedObject, LabelView> pair in m_active)
            {
                TrackedObject track = pair.Key;
                Vector3 anchor = track.Position + Vector3.up * (track.Height * 0.5f + m_gapAboveObject);
                float distance = Vector3.Distance(head.position, anchor);
                // Constant angular size keeps far labels readable (3 m target) without near ones looming.
                float scale = Mathf.Clamp(m_scaleAtOneMetre * distance, m_minScale, m_maxScale);
                m_placements.Add(new Placement { View = pair.Value, Position = anchor, Scale = scale, Distance = distance });
            }

            // Nearest first so the closest label keeps its true spot and farther ones step up.
            m_placements.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            for (int i = 0; i < m_placements.Count; i++)
            {
                Placement p = m_placements[i];
                for (int j = 0; j < i; j++)
                {
                    Placement q = m_placements[j];
                    float minGap = m_labelHeightPixels * Mathf.Max(p.Scale, q.Scale);
                    if (Overlaps(head.position, p.Position, q.Position, minGap) && p.Position.y < q.Position.y + minGap)
                        p.Position.y = q.Position.y + minGap;
                }
                m_placements[i] = p;

                Vector3 toUser = head.position - p.Position;
                toUser.y = 0f; // stay upright; tilting text is harder to read
                if (toUser.sqrMagnitude < 1e-4f) toUser = -(head.rotation * Vector3.forward);
                // Canvas text faces -Z, so look away from the user.
                Quaternion rotation = Quaternion.LookRotation(-toUser.normalized, Vector3.up);
                Transform t = p.View.transform;
                t.SetPositionAndRotation(p.Position, rotation);
                t.localScale = new Vector3(p.Scale, p.Scale, p.Scale);
            }
        }

        // Two labels collide when they are roughly in the same viewing direction from the user.
        private static bool Overlaps(Vector3 eye, Vector3 a, Vector3 b, float minGap)
        {
            Vector3 da = a - eye;
            Vector3 db = b - eye;
            da.y = 0f;
            db.y = 0f;
            float distance = Mathf.Max(0.3f, Mathf.Min(da.magnitude, db.magnitude));
            float lateral = Vector3.Angle(da, db) * Mathf.Deg2Rad * distance;
            return lateral < minGap * 4f && Mathf.Abs(a.y - b.y) < minGap;
        }
    }
}
