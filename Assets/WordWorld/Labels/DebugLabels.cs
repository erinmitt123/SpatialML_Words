using System.Collections.Generic;
using UnityEngine;
using WordWorld.Spatial;
using WordWorld.Tracking;

namespace WordWorld.Labels
{
    /// <summary>
    /// P0 labels: plain English class names and the detector's confidence above confirmed objects.
    /// Billboarded toward the user, sized with distance within limits, and pushed apart vertically
    /// when crowded.
    /// </summary>
    [AddComponentMenu("Word World/Debug Labels")]
    public sealed class DebugLabels : MonoBehaviour
    {
        [SerializeField] private TrackingManager m_tracking;
        [SerializeField] private LabelView[] m_pool = System.Array.Empty<LabelView>();

        [Header("Placement")]
        [SerializeField] private float m_gapAboveObject = 0.06f;
        [Tooltip("Font size in points for a label 1 m away (125 pt is about 44 mm tall).")]
        [SerializeField] private float m_pointsAtOneMetre = 125f;
        [Tooltip("Labels keep a constant angular size between these distances (readable at 3 m).")]
        [SerializeField] private float m_minSizeDistance = 0.8f;
        [SerializeField] private float m_maxSizeDistance = 2.5f;

        private readonly Dictionary<TrackedObject, LabelView> m_active = new Dictionary<TrackedObject, LabelView>();
        private readonly Stack<LabelView> m_free = new Stack<LabelView>();
        private readonly List<Placement> m_placements = new List<Placement>(32);

        private struct Placement
        {
            public LabelView View;
            public Vector3 Position;
            public float Height;
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
            view.SetWord(track.ClassName, track.Confidence);
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
                // Rounded to 0.1 m steps: every size change is a native text update.
                float sizeDistance = Mathf.Round(Mathf.Clamp(distance, m_minSizeDistance, m_maxSizeDistance) * 10f) / 10f;
                LabelView view = pair.Value;
                view.SetPoints(Mathf.RoundToInt(m_pointsAtOneMetre * sizeDistance));
                view.SetWord(track.ClassName, track.Confidence);
                m_placements.Add(new Placement { View = view, Position = anchor, Height = view.HeightMetres, Distance = distance });
            }

            // Nearest first so the closest label keeps its true spot and farther ones step up.
            m_placements.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            for (int i = 0; i < m_placements.Count; i++)
            {
                Placement p = m_placements[i];
                for (int j = 0; j < i; j++)
                {
                    Placement q = m_placements[j];
                    float minGap = Mathf.Max(p.Height, q.Height);
                    if (Overlaps(head.position, p.Position, q.Position, minGap) && p.Position.y < q.Position.y + minGap)
                        p.Position.y = q.Position.y + minGap;
                }
                m_placements[i] = p;

                Vector3 toUser = head.position - p.Position;
                toUser.y = 0f; // stay upright; tilting text is harder to read
                if (toUser.sqrMagnitude < 1e-4f) toUser = -(head.rotation * Vector3.forward);
                // Like Unity UI, the text faces -Z, so look away from the user.
                Quaternion rotation = Quaternion.LookRotation(-toUser.normalized, Vector3.up);
                p.View.transform.SetPositionAndRotation(p.Position, rotation);
            }
            LogPlacements(head);
        }

        // Spatial mode cannot be screen-captured, so placement is checked from logcat.
        private float m_nextLogTime;

        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void LogPlacements(Pose head)
        {
            if (Time.unscaledTime < m_nextLogTime) return;
            m_nextLogTime = Time.unscaledTime + 5f;
            var b = new System.Text.StringBuilder("[WordWorld.Labels] head ");
            b.Append(head.position.ToString("F2")).Append(" fwd ").Append((head.rotation * Vector3.forward).ToString("F2"));
            Transform root = transform;
            b.Append(" root ").Append(root.position.ToString("F2")).Append(root.lossyScale.ToString("F2"));
            foreach (KeyValuePair<TrackedObject, LabelView> pair in m_active)
                b.Append(" | ").Append(pair.Key.ClassName).Append(" obj ").Append(pair.Key.Position.ToString("F2"))
                    .Append(" label ").Append(pair.Value.transform.position.ToString("F2"))
                    .Append(" local ").Append(pair.Value.transform.localPosition.ToString("F2"));
            Debug.Log(b.ToString());
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
