using System;
using System.Collections.Generic;
using UnityEngine;
using WordWorld.Detection;
using WordWorld.Spatial;

namespace WordWorld.Tracking
{
    /// <summary>
    /// Turns noisy per-frame detections into stable TrackedObjects (CLAUDE.md section 2): confidence
    /// threshold, 5-of-8 frame confirmation within 0.25 m, smoothing, same-class merging, and removal
    /// after 10 s unseen while in view. Past projects flickered; nothing here labels a single frame.
    /// </summary>
    [AddComponentMenu("Word World/Tracking Manager")]
    public sealed class TrackingManager : MonoBehaviour
    {
        [Tooltip("Component implementing IObjectDetector.")]
        [SerializeField] private MonoBehaviour m_detector;

        [Header("CLAUDE.md section 2")]
        [SerializeField, Range(0f, 1f)] private float m_confidenceThreshold = 0.55f;
        [SerializeField, Range(1, 32)] private int m_windowFrames = 8;
        [SerializeField, Range(1, 32)] private int m_requiredHits = 5;
        [SerializeField] private float m_matchRadius = 0.25f;
        [SerializeField] private float m_dropAfterUnseenSeconds = 10f;
        [Tooltip("Exponential smoothing weight for each new observation.")]
        [SerializeField, Range(0.05f, 1f)] private float m_smoothing = 0.3f;

        [Header("In-view test for removal")]
        [SerializeField] private float m_viewHalfAngleDegrees = 35f;
        [SerializeField] private float m_viewMaxDistance = 5f;

        public event Action<TrackedObject> ObjectConfirmed;
        public event Action<TrackedObject> ObjectRemoved;

        public float ConfidenceThreshold
        {
            get => m_confidenceThreshold;
            set => m_confidenceThreshold = Mathf.Clamp01(value);
        }

        public IReadOnlyList<TrackedObject> Confirmed => m_confirmed;
        public int CandidateCount => m_tracks.Count - m_confirmed.Count;

        // Last-frame diagnostics for the debug panel.
        public int LastRayCount { get; private set; }
        public int LastPlacedCount { get; private set; }
        public bool LastFrameReliable { get; private set; }
        public IReadOnlyList<DetectionRay> LastRays => m_lastRays;

        private IObjectDetector m_source;
        private readonly List<TrackedObject> m_tracks = new List<TrackedObject>();
        private readonly List<TrackedObject> m_confirmed = new List<TrackedObject>();
        private readonly List<PlacedDetection> m_placed = new List<PlacedDetection>(32);
        private readonly List<DetectionRay> m_lastRays = new List<DetectionRay>(32);
        private double m_lastCaptureTime;
        private int m_nextId = 1;

        private void OnEnable()
        {
            m_source = m_detector as IObjectDetector;
            if (m_source == null)
            {
                Debug.LogError("[WordWorld.Tracking] Detector reference does not implement IObjectDetector.");
                enabled = false;
                return;
            }
            m_source.FrameReady += OnFrame;
        }

        private void OnDisable()
        {
            if (m_source != null) m_source.FrameReady -= OnFrame;
        }

        private void OnFrame(DetectionFrame frame)
        {
            m_lastRays.Clear();
            m_lastRays.AddRange(frame.Rays);
            LastRayCount = frame.Rays.Count;
            LastFrameReliable = frame.MotionReliable;
            float dt = m_lastCaptureTime > 0 ? Mathf.Clamp((float)(frame.CaptureTime - m_lastCaptureTime), 0f, 0.5f) : 0f;
            m_lastCaptureTime = frame.CaptureTime;

            // Fast head motion smears boxes off their objects; such frames are not evidence either way.
            if (!frame.MotionReliable)
            {
                LastPlacedCount = 0;
                return;
            }

            m_placed.Clear();
            int mask = RoomMesh.LayerMask;
            foreach (DetectionRay ray in frame.Rays)
            {
                if (ray.Confidence < m_confidenceThreshold || !CocoClasses.IsHousehold(ray.ClassId)) continue;
                if (MeshRayPlacer.TryPlace(ray, mask, out PlacedDetection placed)) m_placed.Add(placed);
            }
            LastPlacedCount = m_placed.Count;

            foreach (TrackedObject track in m_tracks) track.HitThisFrame = false;
            foreach (PlacedDetection detection in m_placed) Associate(detection);
            AgeTracks(frame.Camera, dt);
            MergeDuplicates();
        }

        private void Associate(in PlacedDetection detection)
        {
            TrackedObject best = null;
            float bestDistance = m_matchRadius;
            foreach (TrackedObject track in m_tracks)
            {
                if (track.ClassId != detection.ClassId || track.HitThisFrame) continue;
                float distance = Vector3.Distance(track.Position, detection.Position);
                // Prefer confirmed tracks on ties so a new candidate cannot steal their evidence.
                if (distance < bestDistance || (best != null && !best.Confirmed && track.Confirmed &&
                                                Mathf.Approximately(distance, bestDistance)))
                {
                    best = track;
                    bestDistance = distance;
                }
            }

            float now = Time.unscaledTime;
            if (best == null)
            {
                m_tracks.Add(new TrackedObject
                {
                    Id = m_nextId++,
                    ClassId = detection.ClassId,
                    ClassName = CocoClasses.NameOf(detection.ClassId),
                    Position = detection.Position,
                    Width = detection.Width,
                    Height = detection.Height,
                    Confidence = detection.Confidence,
                    FirstSeenTime = now,
                    LastSeenTime = now,
                    HitThisFrame = true,
                });
                return;
            }

            best.Position = Vector3.Lerp(best.Position, detection.Position, m_smoothing);
            best.Width = Mathf.Lerp(best.Width, detection.Width, m_smoothing);
            best.Height = Mathf.Lerp(best.Height, detection.Height, m_smoothing);
            best.Confidence = Mathf.Lerp(best.Confidence, detection.Confidence, m_smoothing);
            best.LastSeenTime = now;
            best.HitThisFrame = true;
        }

        private void AgeTracks(Pose camera, float dt)
        {
            uint windowMask = m_windowFrames >= 32 ? uint.MaxValue : (1u << m_windowFrames) - 1u;
            for (int i = m_tracks.Count - 1; i >= 0; i--)
            {
                TrackedObject track = m_tracks[i];
                track.HitHistory = ((track.HitHistory << 1) | (track.HitThisFrame ? 1u : 0u)) & windowMask;

                if (!track.Confirmed)
                {
                    if (CountBits(track.HitHistory) >= m_requiredHits)
                    {
                        track.Confirmed = true;
                        m_confirmed.Add(track);
                        ObjectConfirmed?.Invoke(track);
                    }
                    else if (track.HitHistory == 0)
                    {
                        m_tracks.RemoveAt(i);
                    }
                    continue;
                }

                // Only time spent looking at the spot counts; objects behind the user persist.
                if (track.HitThisFrame) track.UnseenInViewSeconds = 0f;
                else if (IsInView(track.Position, camera)) track.UnseenInViewSeconds += dt;
                if (track.UnseenInViewSeconds >= m_dropAfterUnseenSeconds) RemoveAt(i);
            }
        }

        private void MergeDuplicates()
        {
            for (int i = m_tracks.Count - 1; i >= 0; i--)
            {
                TrackedObject a = m_tracks[i];
                for (int j = 0; j < i; j++)
                {
                    TrackedObject b = m_tracks[j];
                    if (a.ClassId != b.ClassId || Vector3.Distance(a.Position, b.Position) > m_matchRadius) continue;
                    // Keep the confirmed (or older) one; fold the other's evidence into it.
                    TrackedObject keep = b.Confirmed || !a.Confirmed ? b : a;
                    TrackedObject drop = keep == b ? a : b;
                    keep.Position = Vector3.Lerp(keep.Position, drop.Position, 0.5f);
                    keep.HitHistory |= drop.HitHistory;
                    keep.LastSeenTime = Mathf.Max(keep.LastSeenTime, drop.LastSeenTime);
                    RemoveAt(m_tracks.IndexOf(drop));
                    break;
                }
            }
        }

        private void RemoveAt(int index)
        {
            TrackedObject track = m_tracks[index];
            m_tracks.RemoveAt(index);
            if (track.Confirmed && m_confirmed.Remove(track)) ObjectRemoved?.Invoke(track);
        }

        private bool IsInView(Vector3 position, Pose camera)
        {
            Vector3 toObject = position - camera.position;
            float distance = toObject.magnitude;
            if (distance < 0.1f || distance > m_viewMaxDistance) return false;
            return Vector3.Angle(camera.forward, toObject) <= m_viewHalfAngleDegrees;
        }

        private static int CountBits(uint value)
        {
            int count = 0;
            while (value != 0)
            {
                value &= value - 1;
                count++;
            }
            return count;
        }

        /// <summary>Hides a label the user marked "Not right?" (CLAUDE.md section 6).</summary>
        public void Dismiss(TrackedObject track)
        {
            int index = m_tracks.IndexOf(track);
            if (index >= 0) RemoveAt(index);
        }
    }
}
