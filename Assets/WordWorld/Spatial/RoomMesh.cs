using System;
using System.Collections;
using System.Collections.Generic;
using ByteDance.PICO.XR;
using UnityEngine;
using UnityEngine.XR;

namespace WordWorld.Spatial
{
    /// <summary>
    /// Keeps the system room mesh as invisible MeshColliders. Detections are placed where their
    /// camera ray hits this mesh (SPATIALML_SETUP.md sections 2 and 5). Requires
    /// PXR_ProjectSetting.spatialMesh, which makes the loader create the "PICO Mesh" subsystem.
    /// </summary>
    [AddComponentMenu("Word World/Room Mesh")]
    public sealed class RoomMesh : MonoBehaviour
    {
        public const string LayerName = "RoomMesh";
        private const string LogTag = "[WordWorld.RoomMesh]";

        [Tooltip("Debug only: Spatial mode may not render runtime meshes, so this is best-effort.")]
        [SerializeField] private bool m_showMesh;
        [SerializeField] private Material m_debugMaterial;

        public static int LayerMask => 1 << Layer;
        public static int Layer => Math.Max(0, UnityEngine.LayerMask.NameToLayer(LayerName));

        public int ChunkCount => m_chunks.Count;
        public int TriangleCount { get; private set; }
        public string Status { get; private set; } = "Starting";

        // Mesh callbacks may arrive off the main thread; Unity objects are touched only in Update.
        private readonly object m_lock = new object();
        private readonly Dictionary<Guid, PxrSpatialMeshInfo> m_pending = new Dictionary<Guid, PxrSpatialMeshInfo>();
        private readonly Dictionary<Guid, Chunk> m_chunks = new Dictionary<Guid, Chunk>();
        private readonly List<int> m_triangleScratch = new List<int>(4096);
        private readonly List<XRMeshSubsystem> m_subsystems = new List<XRMeshSubsystem>();
        private XRMeshSubsystem m_subsystem;
        private bool m_subscribed;
        private bool m_ownsSubsystem;

        private sealed class Chunk
        {
            public GameObject GameObject;
            public Mesh Mesh;
            public MeshCollider Collider;
            public MeshRenderer Renderer;
            public int Triangles;
        }

        private IEnumerator Start()
        {
            if (UnityEngine.LayerMask.NameToLayer(LayerName) < 0)
                Debug.LogWarning($"{LogTag} layer '{LayerName}' is missing; raycasts will use the Default layer");

            // Same start-up order as the SDK's PXR_SpatialMeshManager in Spatial mode.
            yield return new WaitForSeconds(1f);
            SubsystemManager.GetSubsystems(m_subsystems);
            m_ownsSubsystem = m_subsystems.Count == 0;
            m_subsystem = m_ownsSubsystem ? CreatePicoMeshSubsystem() : m_subsystems[0];
            if (m_subsystem == null)
            {
                Status = "No mesh subsystem (is spatialMesh enabled?)";
                Debug.LogError($"{LogTag} {Status}");
                yield break;
            }
            m_subsystem.Start();
            PXR_Manager.SpatialMeshDataUpdated += OnSpatialMeshDataUpdated;
            m_subscribed = true;
            Status = "Scanning";
            Debug.Log($"{LogTag} mesh subsystem started");
        }

        // In Spatial mode (PICO_MS_SDK) PXR_Loader only creates the mesh subsystem when AR Foundation 6
        // is installed. The subsystem itself is core Unity XR, so create it the way the XR path does.
        private XRMeshSubsystem CreatePicoMeshSubsystem()
        {
            var descriptors = new List<XRMeshSubsystemDescriptor>();
            SubsystemManager.GetSubsystemDescriptors(descriptors);
            foreach (XRMeshSubsystemDescriptor descriptor in descriptors)
            {
                if (descriptor.id != "PICO Mesh") continue;
                Debug.Log($"{LogTag} loader did not create the mesh subsystem; creating '{descriptor.id}'");
                return descriptor.Create();
            }
            Debug.LogError($"{LogTag} no 'PICO Mesh' descriptor ({descriptors.Count} mesh descriptors registered)");
            return null;
        }

        private void OnSpatialMeshDataUpdated(List<PxrSpatialMeshInfo> meshInfos)
        {
            if (meshInfos == null) return;
            lock (m_lock)
            {
                foreach (PxrSpatialMeshInfo info in meshInfos)
                {
                    if (info.state == MeshChangeState.Unchanged) continue;
                    m_pending[info.uuid] = info;
                }
            }
        }

        private void Update()
        {
            lock (m_lock)
            {
                if (m_pending.Count == 0) return;
                foreach (PxrSpatialMeshInfo info in m_pending.Values) Apply(info);
                m_pending.Clear();
            }
            TriangleCount = 0;
            foreach (Chunk chunk in m_chunks.Values) TriangleCount += chunk.Triangles;
            Status = $"{m_chunks.Count} chunks, {TriangleCount} triangles";
        }

        private void Apply(PxrSpatialMeshInfo info)
        {
            if (info.state == MeshChangeState.Removed ||
                info.vertices == null || info.indices == null || info.indices.Length < 3)
            {
                Remove(info.uuid);
                return;
            }
            if (!m_chunks.TryGetValue(info.uuid, out Chunk chunk))
            {
                chunk = CreateChunk(info.uuid);
                m_chunks.Add(info.uuid, chunk);
            }
            m_triangleScratch.Clear();
            for (int i = 0; i + 2 < info.indices.Length; i += 3)
            {
                m_triangleScratch.Add(info.indices[i]);
                m_triangleScratch.Add(info.indices[i + 1]);
                m_triangleScratch.Add(info.indices[i + 2]);
            }
            chunk.Mesh.Clear();
            chunk.Mesh.indexFormat = info.vertices.Length > 65535
                ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            chunk.Mesh.SetVertices(info.vertices);
            chunk.Mesh.SetTriangles(m_triangleScratch, 0);
            chunk.Mesh.RecalculateBounds();
            chunk.Triangles = m_triangleScratch.Count / 3;
            chunk.GameObject.transform.SetPositionAndRotation(info.position, info.rotation);
            // Reassigning sharedMesh is what makes PhysX rebuild the collider.
            chunk.Collider.sharedMesh = null;
            chunk.Collider.sharedMesh = chunk.Mesh;
        }

        private Chunk CreateChunk(Guid id)
        {
            var go = new GameObject($"Room Mesh {id:N}") { layer = Layer };
            go.transform.SetParent(transform, false);
            var chunk = new Chunk
            {
                GameObject = go,
                Mesh = new Mesh { name = "Room Mesh" },
                Collider = go.AddComponent<MeshCollider>(),
            };
            chunk.Mesh.MarkDynamic();
            if (m_showMesh && m_debugMaterial != null)
            {
                go.AddComponent<MeshFilter>().sharedMesh = chunk.Mesh;
                chunk.Renderer = go.AddComponent<MeshRenderer>();
                chunk.Renderer.sharedMaterial = m_debugMaterial;
            }
            return chunk;
        }

        private void Remove(Guid id)
        {
            if (!m_chunks.TryGetValue(id, out Chunk chunk)) return;
            Destroy(chunk.Mesh);
            Destroy(chunk.GameObject);
            m_chunks.Remove(id);
        }

        private void OnDestroy()
        {
            if (m_subscribed) PXR_Manager.SpatialMeshDataUpdated -= OnSpatialMeshDataUpdated;
            if (m_subsystem != null && m_subsystem.running) m_subsystem.Stop();
            if (m_ownsSubsystem && m_subsystem != null) m_subsystem.Destroy();
            foreach (Chunk chunk in m_chunks.Values) Destroy(chunk.Mesh);
            m_chunks.Clear();
        }
    }
}
