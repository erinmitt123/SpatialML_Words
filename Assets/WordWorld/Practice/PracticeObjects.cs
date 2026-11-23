using System.Collections.Generic;
using UnityEngine;
using WordWorld.App;
using WordWorld.Labels;
using WordWorld.Spatial;
using WordWorld.Tracking;
using WordWorld.UI;

namespace WordWorld.Practice
{
    /// <summary>
    /// Practice objects (Settings): five 3D models (chair, sofa, plant, table and a vase on it) placed
    /// on the floor around the learner. Each becomes a confirmed track, so it gets a label and takes
    /// part in Explore, Find It and Say It exactly like a real object. They keep a sparse room (or a
    /// headset whose object recognition is not working) good for a full session. They last one
    /// session (LearnerSettings.PracticeObjects is not saved).
    ///
    /// Placement uses the room mesh: each model goes into a gap, a patch of open floor at the
    /// learner's floor level with nothing of the mesh (furniture, walls) in the space it would fill
    /// and a clear line of sight to it, away from the other models and from real tracked objects.
    /// Gaps near each model's preferred direction and distance win. A model with no gap is left out
    /// rather than put inside furniture. Without a room mesh they fall back to fixed spots.
    /// The models are in the scene from the start and only moved and shown at runtime: Spatial
    /// Adapter syncs objects registered with the scene, not ones instantiated later.
    /// </summary>
    [AddComponentMenu("Word World/Practice Objects")]
    public sealed class PracticeObjects : MonoBehaviour
    {
        [SerializeField] private LearnerSettings m_settings;
        [SerializeField] private TrackingManager m_tracking;
        [SerializeField] private ObjectLabels m_labels;
        [SerializeField] private PracticeModel[] m_models = System.Array.Empty<PracticeModel>();
        [Tooltip("Keep models at least this far in front of a wall the room mesh knows about.")]
        [SerializeField] private float m_wallClearance = 0.15f;
        [SerializeField] private float m_minDistance = 1.0f;
        [SerializeField] private float m_maxDistance = 3.2f;
        [Tooltip("Free space kept around each model's footprint, from the mesh and from other objects.")]
        [SerializeField] private float m_clearance = 0.12f;
        [Tooltip("Floor patches more than this above or below the learner's floor are furniture or steps.")]
        [SerializeField] private float m_floorTolerance = 0.12f;

        private sealed class Placed
        {
            public PracticeModel Model;
            public TrackedObject Track;
            public Vector3 Base;
            public bool Moving;
        }

        private readonly List<Placed> m_placed = new List<Placed>();
        private readonly Dictionary<PracticeModel, Placed> m_byModel = new Dictionary<PracticeModel, Placed>();

        public bool IsPlaced => m_placed.Count > 0;

        // One placement per switch-on (or Move here): a room with no gaps must not be searched every frame.
        private bool m_tried;

        private void Awake()
        {
            foreach (PracticeModel model in m_models)
            {
                if (model == null) continue;
                model.gameObject.SetActive(false);
                model.Tapped += OnModelTapped;
            }
        }

        private void Update()
        {
            bool want = m_settings != null && m_settings.SessionReady && m_settings.PracticeObjects;
            if (want && !IsPlaced && !m_tried) Place();
            else if (!want)
            {
                m_tried = false;
                if (IsPlaced) Clear();
            }
            if (IsPlaced) Animate();
        }

        /// <summary>Settings "Move here": puts the objects around where the learner now stands.</summary>
        public void PlaceHere()
        {
            Clear();
            m_tried = false;
            Place();
        }

        private void Place()
        {
            if (!WindowPlacement.TryGetYaw(out Pose head, out Quaternion yaw)) return;
            m_tried = true;
            // Is there a room mesh under the learner? Then models go into gaps in it.
            bool mesh = Physics.Raycast(head.position, Vector3.down, 2.5f, RoomMesh.LayerMask, QueryTriggerInteraction.Ignore);
            float floor = RoomFloor.Below(head.position);
            // Floor-standing models first, largest first (they need the biggest gaps), so the vase
            // can stand on the table.
            var floorModels = new List<PracticeModel>();
            foreach (PracticeModel model in m_models)
                if (model != null && model.StandOn == null) floorModels.Add(model);
            floorModels.Sort((a, b) => Footprint(b).CompareTo(Footprint(a)));
            foreach (PracticeModel model in floorModels)
            {
                if (!mesh)
                {
                    PlaceOne(model, head, yaw, floor);
                    continue;
                }
                // Strict first; a lumpy scanned floor or a small room then gets a looser pass, so the
                // learner always has something to practise on (Nov 16: nothing appeared at all).
                if (TryFindGap(model, head, yaw, floor, 1f, out Vector3 spot) ||
                    TryFindGap(model, head, yaw, floor, 0.4f, out spot)) PlaceAt(model, spot, head, yaw);
                else Debug.Log($"[WordWorld.Practice] no free floor for the {model.name}; left out");
            }
            foreach (PracticeModel model in m_models)
                if (model != null && model.StandOn != null && m_byModel.ContainsKey(model.StandOn)) PlaceOne(model, head, yaw, floor);
            Debug.Log($"[WordWorld.Practice] placed {m_placed.Count} practice objects (floor {floor:0.00} m, {(mesh ? "in room mesh gaps" : "no room mesh: fixed spots")})");
            // The room answers: a ripple runs out and lifts each new object as it passes.
            RoomFx.Ripple();
        }

        private void PlaceOne(PracticeModel model, Pose head, Quaternion yaw, float floor)
        {
            Vector3 size = model.Size;
            Vector3 basePos;
            if (model.StandOn != null && m_byModel.TryGetValue(model.StandOn, out Placed under))
            {
                basePos = under.Base + Vector3.up * under.Model.Size.y;
            }
            else
            {
                Vector3 direction = yaw * Quaternion.Euler(0f, model.Angle, 0f) * Vector3.forward;
                float distance = ClearOfWalls(head.position, direction, model.Distance, Mathf.Max(size.x, size.z));
                basePos = head.position + direction * distance;
                basePos.y = floor;
            }
            PlaceAt(model, basePos, head, yaw);
        }

        private static float Footprint(PracticeModel model) => Mathf.Max(model.Size.x, model.Size.z);

        // Searches a fan in front of the learner (every 10 degrees, every 25 cm) for open floor.
        private bool TryFindGap(PracticeModel model, Pose head, Quaternion yaw, float floor, float strictness, out Vector3 spot)
        {
            spot = default;
            Vector3 size = model.Size;
            float radius = Footprint(model) * 0.5f;
            float bestScore = float.MaxValue;
            int mask = RoomMesh.LayerMask;
            for (int angle = -120; angle <= 120; angle += 10)
            {
                Vector3 direction = yaw * Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                for (float distance = Mathf.Max(m_minDistance, radius + 0.6f); distance <= m_maxDistance; distance += 0.25f)
                {
                    float score = Mathf.Abs(angle - model.Angle) / 10f + Mathf.Abs(distance - model.Distance) / 0.5f;
                    if (score >= bestScore) continue;
                    Vector3 p = head.position + direction * distance;
                    // Floor here, at the learner's floor level: not outside the scanned room, not a tabletop.
                    if (!Physics.Raycast(new Vector3(p.x, head.position.y, p.z), Vector3.down, out RaycastHit hit, 2.5f,
                            mask, QueryTriggerInteraction.Ignore)) continue;
                    if (hit.normal.y < 0.85f || Mathf.Abs(hit.point.y - floor) > m_floorTolerance) continue;
                    // Nothing of the mesh in the space the model would fill (from 6 cm above the floor).
                    // The looser pass checks from higher above the floor (scanned floors are bumpy)
                    // with less room around the model.
                    float lift = strictness < 1f ? 0.15f : 0.06f;
                    float clearance = m_clearance * strictness;
                    float height = Mathf.Max(0.1f, size.y - lift);
                    var centre = new Vector3(p.x, hit.point.y + lift + height * 0.5f, p.z);
                    var half = new Vector3(radius + clearance, height * 0.5f, radius + clearance);
                    if (Physics.CheckBox(centre, half, Quaternion.identity, mask, QueryTriggerInteraction.Ignore)) continue;
                    // The learner can see it: no wall or furniture in between.
                    Vector3 aim = new Vector3(p.x, hit.point.y + size.y * 0.5f, p.z);
                    Vector3 toAim = aim - head.position;
                    if (Physics.Raycast(head.position, toAim.normalized, toAim.magnitude - radius, mask, QueryTriggerInteraction.Ignore)) continue;
                    if (Crowded(p, radius)) continue;
                    bestScore = score;
                    spot = new Vector3(p.x, hit.point.y, p.z);
                }
            }
            return bestScore < float.MaxValue;
        }

        // Too near a model already placed, or a real object the learner has (its label would clash).
        private bool Crowded(Vector3 p, float radius)
        {
            foreach (Placed placed in m_placed)
                if (Flat(p - placed.Base) < radius + Footprint(placed.Model) * 0.5f + 2f * m_clearance) return true;
            foreach (TrackedObject track in m_tracking.Confirmed)
                if (!track.IsVirtual && Flat(p - track.Position) < radius + 0.4f) return true;
            return false;
        }

        private static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;

        private void PlaceAt(PracticeModel model, Vector3 basePos, Pose head, Quaternion yaw)
        {
            Vector3 size = model.Size;
            Vector3 toUser = head.position - basePos;
            toUser.y = 0f;
            Quaternion facing = toUser.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(toUser.normalized, Vector3.up) : yaw;
            model.transform.SetPositionAndRotation(basePos, facing * Quaternion.Euler(0f, model.YawOffset, 0f));
            model.gameObject.SetActive(true);

            TrackedObject track = m_tracking.AddVirtual(model.ClassId, basePos + Vector3.up * (size.y * 0.5f),
                Mathf.Max(size.x, size.z), size.y);
            var placed = new Placed { Model = model, Track = track, Base = basePos };
            m_placed.Add(placed);
            m_byModel[model] = placed;
        }

        // Pulls a model in from a wall the room mesh knows about, so it never stands half inside it.
        private float ClearOfWalls(Vector3 head, Vector3 direction, float distance, float footprint)
        {
            Vector3 origin = head + Vector3.down * 0.5f;
            float reach = distance + footprint * 0.5f + m_wallClearance;
            if (Physics.Raycast(origin, direction, out RaycastHit hit, reach, RoomMesh.LayerMask, QueryTriggerInteraction.Ignore))
                distance = hit.distance - footprint * 0.5f - m_wallClearance;
            return Mathf.Max(m_minDistance, distance);
        }

        private void Clear()
        {
            foreach (Placed placed in m_placed)
            {
                m_tracking.RemoveVirtual(placed.Track);
                placed.Model.gameObject.SetActive(false);
            }
            m_placed.Clear();
            m_byModel.Clear();
        }

        // The model moves with its label: hops on a right answer, shakes on a wrong one, bobs as the
        // ripple passes. Only written while moving, since every transform change is synced.
        private void Animate()
        {
            if (!HeadPose.TryGet(out Pose head)) return;
            float now = Time.unscaledTime;
            foreach (Placed placed in m_placed)
            {
                Vector3 centre = placed.Track.Position;
                Vector3 right = Vector3.Cross(Vector3.up, centre - head.position).normalized;
                ObjectEffects.Motion motion = ObjectEffects.Evaluate(placed.Track, centre, right, now);
                bool moving = motion.Offset.sqrMagnitude > 1e-8f;
                if (!moving && !placed.Moving) continue;
                placed.Moving = moving;
                placed.Model.transform.position = placed.Base + motion.Offset;
            }
        }

        private void OnModelTapped(PracticeModel model, Vector3 at)
        {
            if (m_byModel.TryGetValue(model, out Placed placed)) m_labels.TapObject(placed.Track, at);
        }
    }
}
