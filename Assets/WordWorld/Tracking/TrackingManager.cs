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
    /// Matching is global per frame (ObjectAssignment); confirmed objects use the yolo-mesh sample's
    /// camera-aware re-acquisition gate (ObjectGeometry) so partial views do not spawn duplicates.
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
        [Tooltip("Depth tolerance along the view ray, as a fraction of distance. Mesh and stereo depth " +
                 "disagree by 15-25% up close on Swan, so 0.25 m along the ray split one object into several.")]
        [SerializeField, Range(0.05f, 1f)] private float m_depthTolerance = 0.35f;
        [SerializeField] private float m_dropAfterUnseenSeconds = 10f;
        [Tooltip("Exponential smoothing weight for each new observation.")]
        [SerializeField, Range(0.05f, 1f)] private float m_smoothing = 0.3f;
        [Tooltip("Smoothing once confirmed: the object is not moving, so new frames mostly add noise.")]
        [SerializeField, Range(0.02f, 1f)] private float m_confirmedSmoothing = 0.08f;

        [Header("Stereo-only placement (no room mesh)")]
        [Tooltip("Stereo depth gets unreliable with range; without the mesh it put TVs and books 5-6 m away.")]
        [SerializeField] private float m_maxStereoDepth = 4f;
        [Tooltip("Placements further below the camera than this are under the floor (seen: a cup 3 m down).")]
        [SerializeField] private float m_maxDropBelowCamera = 1.9f;
        [SerializeField] private float m_maxRiseAboveCamera = 1.2f;

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
        /// <summary>Every track, confirmed or not; unconfirmed ones drive the "scanning" dots.</summary>
        public IReadOnlyList<TrackedObject> Tracks => m_tracks;
        /// <summary>While paused, frames are ignored (DESIGN.md section 8 recognition chip).</summary>
        public bool Paused { get; set; }
        /// <summary>Time of the last frame with any detection, for the "having trouble seeing" hint.</summary>
        public float LastDetectionTime { get; private set; }
        public int CandidateCount => m_tracks.Count - m_confirmed.Count;

        // Last-frame diagnostics for the debug panel.
        public int LastRayCount { get; private set; }
        public int LastPlacedCount { get; private set; }
        /// <summary>How many of LastPlacedCount came from stereo depth rather than the room mesh.</summary>
        public int LastStereoCount { get; private set; }
        public bool LastFrameReliable { get; private set; }
        public IReadOnlyList<DetectionRay> LastRays => m_lastRays;

        private IObjectDetector m_source;
        private readonly List<TrackedObject> m_tracks = new List<TrackedObject>();
        private readonly List<TrackedObject> m_confirmed = new List<TrackedObject>();
        private readonly List<PlacedDetection> m_placed = new List<PlacedDetection>(32);
        private readonly List<DetectionRay> m_lastRays = new List<DetectionRay>(32);
        private readonly List<int> m_unassigned = new List<int>(32);
        private readonly List<TrackedObject> m_stageTracks = new List<TrackedObject>();
        private readonly ObjectAssignment m_assignment = new ObjectAssignment();

        // The detector names parts of a laptop as objects of their own: its keys as "keyboard" and its
        // screen as "tv". A part's box centre lies inside the laptop's box in the same frame.
        private static readonly int s_laptop = Array.IndexOf(CocoClasses.Names, "laptop");
        private static readonly int[] s_laptopParts =
        {
            Array.IndexOf(CocoClasses.Names, "keyboard"),
            Array.IndexOf(CocoClasses.Names, "tv"),
        };
        private double m_lastCaptureTime;
        private int m_nextId = 1;
        private float m_nextSummaryTime;
        private int m_summaryRays, m_summaryPlaced, m_summaryStereo, m_summaryWithDepth, m_summaryEligible;
        private string m_summarySample = "";

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
            if (Paused) return;
            if (frame.Rays.Count > 0) LastDetectionTime = Time.unscaledTime;
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
                LastStereoCount = 0;
                return;
            }

            m_placed.Clear();
            int mask = RoomMesh.LayerMask;
            int stereo = 0;
            foreach (DetectionRay ray in frame.Rays)
            {
                if (ray.Confidence < m_confidenceThreshold || !CocoClasses.IsHousehold(ray.ClassId)) continue;
                if (IsPartOfLaptop(ray, frame.Rays)) continue;
                // The mesh fit is more precise; stereo covers surfaces the mesh lacks (or no mesh at all).
                // When both measure and disagree, one of them is on something else: skip, never guess.
                bool onMesh = MeshRayPlacer.TryPlace(ray, mask, out PlacedDetection placed);
                if (onMesh && SourcesDisagree(ray, placed)) continue;
                if (onMesh || TryPlaceStereo(ray, out placed))
                {
                    m_placed.Add(placed);
                    if (placed.FromStereo) stereo++;
                }
            }
            LastPlacedCount = m_placed.Count;
            LastStereoCount = stereo;
            LogSummary(frame.Rays.Count);

            foreach (TrackedObject track in m_tracks) track.HitThisFrame = false;
            AssignDetections(frame.Camera);
            AgeTracks(frame.Camera, dt);
            MergeDuplicates(frame.Camera);
        }

        private bool IsPartOfLaptop(in DetectionRay part, List<DetectionRay> rays)
        {
            if (Array.IndexOf(s_laptopParts, part.ClassId) < 0) return false;
            foreach (DetectionRay laptop in rays)
            {
                if (laptop.ClassId != s_laptop || laptop.Confidence < m_confidenceThreshold) continue;
                // Same frame and camera, so one-metre-plane offsets compare like image coordinates.
                Vector3 offset = part.PlaneDirection - laptop.PlaneDirection;
                if (Mathf.Abs(Vector3.Dot(offset, laptop.CameraRight)) <= laptop.PlaneWidth * 0.5f &&
                    Mathf.Abs(Vector3.Dot(offset, laptop.CameraUp)) <= laptop.PlaneHeight * 0.5f) return true;
            }
            return false;
        }

        private bool SourcesDisagree(in DetectionRay ray, in PlacedDetection onMesh)
        {
            float stereo = ray.StereoDepth;
            if (!(stereo >= MeshRayPlacer.MinDepth && stereo <= MeshRayPlacer.MaxDepth)) return false;
            float mesh = Vector3.Dot(onMesh.Position - ray.Origin, ray.PlaneDirection) / ray.PlaneDirection.sqrMagnitude;
            return Mathf.Abs(mesh - stereo) > m_depthTolerance * Mathf.Min(mesh, stereo);
        }

        /// <summary>
        /// Whether two points can be the same object seen from <paramref name="camera"/>: within the
        /// 0.25 m match radius across the view, but within m_depthTolerance of the distance along it,
        /// because depth (not direction) is what the sensors get wrong. Returns a normalised cost, or
        /// -1 when they cannot match.
        /// </summary>
        private float ViewCost(Vector3 a, Vector3 b, Pose camera)
        {
            Vector3 da = a - camera.position, db = b - camera.position;
            float ra = da.magnitude, rb = db.magnitude;
            if (ra < 1e-3f || rb < 1e-3f) return -1f;
            float near = Mathf.Min(ra, rb);
            float lateral = Vector3.Angle(da, db) * Mathf.Deg2Rad * near;
            float along = Mathf.Abs(ra - rb);
            float depthLimit = Mathf.Max(m_matchRadius, m_depthTolerance * near);
            if (lateral > m_matchRadius || along > depthLimit) return -1f;
            return 0.5f * (lateral * lateral / (m_matchRadius * m_matchRadius) + along * along / (depthLimit * depthLimit));
        }

        // Headset tests often run unattended; logcat is the only way to see the pipeline then.
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD"), System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void LogSummary(int rays)
        {
            m_summaryRays += rays;
            m_summaryPlaced += LastPlacedCount;
            m_summaryStereo += LastStereoCount;
            foreach (DetectionRay ray in m_lastRays)
            {
                if (ray.StereoDepth > 0) m_summaryWithDepth++;
                if (ray.Confidence >= m_confidenceThreshold && CocoClasses.IsHousehold(ray.ClassId)) m_summaryEligible++;
                if (m_summarySample.Length < 400)
                {
                    // Mesh vs stereo depth for the same ray shows whether the two sources agree.
                    float meshDepth = MeshRayPlacer.TryPlace(ray, RoomMesh.LayerMask, out PlacedDetection onMesh)
                        ? Vector3.Dot(onMesh.Position - ray.Origin, ray.PlaneDirection) / ray.PlaneDirection.sqrMagnitude
                        : 0f;
                    m_summarySample += $" {CocoClasses.NameOf(ray.ClassId)}:{ray.Confidence:0.00} st{ray.StereoDepth:0.00}/mesh{meshDepth:0.00}";
                }
            }
            if (Time.unscaledTime < m_nextSummaryTime) return;
            m_nextSummaryTime = Time.unscaledTime + 5f;
            Debug.Log($"[WordWorld.Tracking] last 5 s: rays {m_summaryRays} (eligible {m_summaryEligible}, " +
                      $"with depth {m_summaryWithDepth}), placed {m_summaryPlaced} (stereo {m_summaryStereo}), " +
                      $"candidates {CandidateCount}, confirmed {m_confirmed.Count};{m_summarySample}");
            m_summaryRays = m_summaryPlaced = m_summaryStereo = m_summaryWithDepth = m_summaryEligible = 0;
            m_summarySample = "";
        }

        /// <summary>
        /// Source B's original (pre-mesh) placement: the box centre on the measured depth plane.
        /// PlaneDirection reaches the one-metre plane, so scaling it by axial depth lands on the object.
        /// </summary>
        private bool TryPlaceStereo(in DetectionRay ray, out PlacedDetection placed)
        {
            placed = default;
            float depth = ray.StereoDepth;
            if (!(depth >= MeshRayPlacer.MinDepth && depth <= Mathf.Min(MeshRayPlacer.MaxDepth, m_maxStereoDepth))) return false;
            Vector3 position = ray.Origin + ray.PlaneDirection * depth;
            float rise = position.y - ray.Origin.y;
            if (rise < -m_maxDropBelowCamera || rise > m_maxRiseAboveCamera) return false;
            placed = new PlacedDetection
            {
                ClassId = ray.ClassId,
                Confidence = ray.Confidence,
                Position = position,
                Width = ray.PlaneWidth * depth,
                Height = ray.PlaneHeight * depth,
                FromStereo = true,
            };
            return true;
        }

        /// <summary>
        /// Matches this frame's detections to tracks globally (Hungarian), as PICO's yolo-mesh sample
        /// does: confirmed objects first, with the camera-aware re-acquisition gate, then candidates
        /// with CLAUDE.md's 0.25 m rule. Leftover detections start new candidates.
        /// </summary>
        private void AssignDetections(Pose camera)
        {
            m_unassigned.Clear();
            for (int i = 0; i < m_placed.Count; i++) m_unassigned.Add(i);
            MatchStage(confirmed: true, camera);
            MatchStage(confirmed: false, camera);
            foreach (int index in m_unassigned) StartTrack(m_placed[index]);
        }

        private void MatchStage(bool confirmed, Pose camera)
        {
            m_stageTracks.Clear();
            foreach (TrackedObject track in m_tracks)
                if (track.Confirmed == confirmed && !track.HitThisFrame) m_stageTracks.Add(track);
            int rows = m_unassigned.Count, tracks = m_stageTracks.Count;
            if (rows == 0 || tracks == 0) return;

            // One dummy column per detection lets every detection stay unmatched.
            m_assignment.Begin(rows, tracks + rows);
            for (int r = 0; r < rows; r++)
            {
                PlacedDetection detection = m_placed[m_unassigned[r]];
                for (int c = 0; c < tracks; c++)
                {
                    float cost = confirmed
                        ? ObjectGeometry.ConfirmedCost(m_stageTracks[c], detection, camera)
                        : CandidateCost(m_stageTracks[c], detection, camera);
                    m_assignment.Set(r, c, cost >= 0f ? cost : ObjectAssignment.Invalid);
                }
                for (int c = tracks; c < tracks + rows; c++) m_assignment.Set(r, c, ObjectAssignment.Unmatched);
            }

            int[] columns = m_assignment.Solve();
            for (int r = rows - 1; r >= 0; r--)
            {
                int c = columns[r];
                if (c < 0 || c >= tracks) continue;
                PlacedDetection detection = m_placed[m_unassigned[r]];
                TrackedObject track = m_stageTracks[c];
                // Re-check: the solver only falls back to an invalid pair when nothing else fits.
                float cost = confirmed ? ObjectGeometry.ConfirmedCost(track, detection, camera) : CandidateCost(track, detection, camera);
                if (cost < 0f) continue;
                Observe(track, detection);
                m_unassigned.RemoveAt(r);
            }
        }

        // CLAUDE.md section 2: a candidate only gathers evidence within 0.25 m of where it was seen,
        // measured across the view; along the view the sensors' depth noise needs more room (ViewCost).
        private float CandidateCost(TrackedObject track, in PlacedDetection detection, Pose camera)
        {
            if (track.ClassId != detection.ClassId) return -1f;
            return ViewCost(track.Position, detection.Position, camera);
        }

        private void StartTrack(in PlacedDetection detection)
        {
            float now = Time.unscaledTime;
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
        }

        private void Observe(TrackedObject track, in PlacedDetection detection)
        {
            float weight = track.Confirmed ? m_confirmedSmoothing : m_smoothing;
            track.Position = Vector3.Lerp(track.Position, detection.Position, weight);
            track.Width = Mathf.Lerp(track.Width, detection.Width, weight);
            track.Height = Mathf.Lerp(track.Height, detection.Height, weight);
            track.Confidence = Mathf.Lerp(track.Confidence, detection.Confidence, weight);
            track.LastSeenTime = Time.unscaledTime;
            track.HitThisFrame = true;
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

        private void MergeDuplicates(Pose camera)
        {
            for (int i = m_tracks.Count - 1; i >= 0; i--)
            {
                TrackedObject a = m_tracks[i];
                for (int j = 0; j < i; j++)
                {
                    TrackedObject b = m_tracks[j];
                    if (a.ClassId != b.ClassId) continue;
                    if (Vector3.Distance(a.Position, b.Position) > m_matchRadius && ViewCost(a.Position, b.Position, camera) < 0f) continue;
                    // Keep the confirmed (or older) one; fold the other's evidence into it.
                    TrackedObject keep = b.Confirmed || !a.Confirmed ? b : a;
                    TrackedObject drop = keep == b ? a : b;
                    // A confirmed object is the persistent one; a stray copy must not drag it around.
                    keep.Position = Vector3.Lerp(keep.Position, drop.Position, keep.Confirmed && !drop.Confirmed ? 0.1f : 0.5f);
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
