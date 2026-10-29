using System;
using System.Collections;
using System.Threading.Tasks;
using ByteDance.PICO.SecureMR;
using PicoXR.SpatialML.Demo;
using Unity.Collections;
using UnityEngine;

namespace WordWorld.Detection
{
    /// <summary>
    /// Runs the packaged YOLOX-M SpatialML pipeline (ported from PICO's yolo-mesh sample) in a
    /// zero-size Full Space container and turns its 33x20 FLOAT32 packet into world-space rays.
    /// The app never sees camera pixels: the pipeline publishes only classes, scores and rays.
    /// </summary>
    [AddComponentMenu("Word World/YOLOX SpatialML Detector")]
    public sealed class YoloxSpatialMLDetector : MonoBehaviour, IObjectDetector, ISpatialMLServiceCleanup
    {
        private const string LogTag = "[WordWorld.Detector]";
        private const string PipelineId = "inference";
        private const string OutputTensor = "detections";

        // Packet layout written by tools/spatialml (live-mesh-output.js from source B).
        private const int MaxDetections = 32;
        private const int RowSize = 20;
        private const int CounterIndex = MaxDetections * RowSize;   // 640
        private const int CameraToHeadIndex = CounterIndex + 1;     // 641..656, row-major
        private const int TimestampIndex = CounterIndex + 17;       // 657..659
        private const int ValueCount = (MaxDetections + 1) * RowSize;
        private const float PipelineScoreFloor = 0.5f;              // matches the packing script

        [SerializeField] private SpatialMLPipelineZooAsset m_package;
        [Tooltip("The yolo-detection package expects a 640x640 rectified VST image.")]
        [SerializeField] private int m_vstSize = 640;
        [Tooltip("Inference runs at a fixed rate, never every frame (CLAUDE.md code conventions).")]
        [SerializeField, Range(1f, 15f)] private float m_targetRateHz = 8f;
        [Tooltip("Frames older than this are ignored; their head pose can no longer be matched.")]
        [SerializeField] private float m_maxCaptureAgeSeconds = 0.5f;
        [Header("Motion gate (from source B)")]
        [SerializeField] private float m_maxCameraSpeed = 0.5f;
        [SerializeField] private float m_maxCameraTurnDegrees = 30f;

        public event Action<DetectionFrame> FrameReady;
        public bool IsRunning => m_pipeline != null && !m_failed;
        public string Status { get; private set; } = "Starting";
        public float InferenceRate { get; private set; }

        private readonly CapturePoseHistory m_poses = new CapturePoseHistory();
        private readonly DetectionFrame m_frame = new DetectionFrame();
        private Provider m_provider;
        private SpatialMLPipelineZooPipeline m_pipeline;
        private Tensor m_output;
        private NativeArray<float> m_values;
        private ulong m_lastRun;
        private float m_lastCounter;
        private double m_lastCaptureTime;
        private bool m_inFlight;
        private bool m_failed;
        private bool m_isShuttingDown;
        private bool m_warnedStaticCounter;
        private float m_nextSubmitTime;
        private Pose m_previousCamera;
        private double m_previousCameraTime;
        private bool m_hasPreviousCamera;
        private int m_rateFrames;
        private float m_rateWindowStart;

        private IEnumerator Start()
        {
            if (m_package == null)
            {
                Fail("No yolo-detection package assigned.");
                yield break;
            }

            const int attempts = 10;
            for (int attempt = 1; attempt <= attempts && m_provider == null; attempt++)
            {
                if (m_isShuttingDown) yield break;
                Status = $"Connecting to SpatialML (attempt {attempt})";
                try
                {
                    // Zero size keeps SpatialML invisible: it computes, Unity renders the labels.
                    m_provider = new Provider(m_vstSize, m_vstSize, new SpatialContainerConfiguration(0, 0, 0));
                }
                catch (InvalidOperationException exception) when (
                    exception.Message.IndexOf("service is not connected", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    attempt < attempts)
                {
                    Debug.LogWarning($"{LogTag} SpatialML not ready, retrying: {exception.Message}");
                }
                catch (Exception exception)
                {
                    Fail($"Provider failed: {exception}");
                    yield break;
                }
                if (m_provider == null) yield return new WaitForSecondsRealtime(1f);
            }

            try
            {
                Status = "Loading YOLOX";
                SpatialMLPipelineZooBundle bundle = SpatialMLPipelineZoo.LoadPackage(m_provider, m_package, PipelineId);
                m_pipeline = bundle[PipelineId];
                if (!bundle.GlobalTensors.TryGetValue(OutputTensor, out m_output))
                    throw new InvalidOperationException($"Package has no global '{OutputTensor}'.");
                if (m_output.Dimensions.Length != 2 || m_output.Dimensions[0] != MaxDetections + 1 ||
                    m_output.Dimensions[1] != RowSize)
                    throw new InvalidOperationException("Detection packet must be 33x20.");
                // Globals start uninitialised; clear once so stale bytes cannot pass as a frame.
                m_output.Reset(new float[ValueCount]);
                m_values = new NativeArray<float>(ValueCount, Allocator.Persistent);
                m_rateWindowStart = Time.unscaledTime;
                Status = "Running";
                Debug.Log($"{LogTag} ready: {m_vstSize}x{m_vstSize} VST, {m_targetRateHz:F0} Hz target");
            }
            catch (Exception exception)
            {
                Fail($"Initialization failed: {exception}");
                DestroyProvider();
            }
        }

        private void Update()
        {
            m_poses.RecordTrackedHeadPose();
            if (!IsRunning || m_isShuttingDown || m_inFlight || Time.unscaledTime < m_nextSubmitTime) return;
            _ = RunFrameAsync();
        }

        private async Task RunFrameAsync()
        {
            m_inFlight = true;
            bool completed = false;
            float submittedAt = Time.unscaledTime;
            try
            {
                m_lastRun = m_lastRun == 0 ? m_pipeline.Execute() : m_pipeline.ExecuteAfter(m_lastRun);
                await WaitForPacketAsync(submittedAt);
                completed = true;
                if (m_isShuttingDown || this == null) return;
                ProcessPacket();
            }
            catch (Exception exception)
            {
                // Completion is unknown, so keep native buffers alive and stop submitting.
                Fail($"Frame failed: {exception}");
            }
            finally
            {
                m_inFlight = !completed;
                m_nextSubmitTime = submittedAt + 1f / m_targetRateHz;
                if (completed && m_isShuttingDown) DestroyProvider();
            }
        }

        private async Task WaitForPacketAsync(float submittedAt)
        {
            bool warned = false;
            while (true)
            {
                // Readback is a snapshot and can precede the queued writer; only a new counter
                // proves this submission's packet is complete.
                int bytes = await m_output.ReadbackIntoAsync(m_values);
                if (bytes == ValueCount * sizeof(float))
                {
                    float counter = m_values[CounterIndex];
                    if (float.IsFinite(counter) && counter > 0 && counter != m_lastCounter)
                    {
                        m_lastCounter = counter;
                        return;
                    }
                    if (counter == m_lastCounter && counter > 0 && ReadCaptureTime() != m_lastCaptureTime &&
                        Time.unscaledTime - submittedAt > 1f)
                    {
                        // Defensive: if the backend does not persist frame_counter between runs, a
                        // new capture timestamp is the next-best completion signal.
                        if (!m_warnedStaticCounter)
                        {
                            m_warnedStaticCounter = true;
                            Debug.LogWarning($"{LogTag} frame counter is not advancing; using capture time");
                        }
                        return;
                    }
                }
                if (!warned && Time.unscaledTime - submittedAt > 5f)
                {
                    warned = true;
                    Debug.LogWarning($"{LogTag} waiting for packet, run={m_lastRun}; submissions paused");
                }
                await Task.Delay(15);
            }
        }

        private double ReadCaptureTime() =>
            m_values[TimestampIndex] + m_values[TimestampIndex + 1] * 1e-3 + m_values[TimestampIndex + 2] * 1e-9;

        private void ProcessPacket()
        {
            CountRate();
            double captureTime = ReadCaptureTime();
            double age = CapturePoseHistory.Now(captureTime) - captureTime;
            if (captureTime <= m_lastCaptureTime || age < -0.05 || age > m_maxCaptureAgeSeconds) return;
            m_lastCaptureTime = captureTime;
            if (!m_poses.TryGetAtCapture(captureTime, out Matrix4x4 head)) return;

            // Packet poses are OpenXR head-local (right-handed, -Z forward); Unity is left-handed.
            int c = CameraToHeadIndex;
            Vector3 cameraInHead = FromOpenXr(m_values[c + 3], m_values[c + 7], m_values[c + 11]);
            Vector3 rightInHead = FromOpenXr(m_values[c + 0], m_values[c + 4], m_values[c + 8]);
            Vector3 upInHead = FromOpenXr(m_values[c + 1], m_values[c + 5], m_values[c + 9]);
            Vector3 forwardInHead = -FromOpenXr(m_values[c + 2], m_values[c + 6], m_values[c + 10]);
            if (!IsFinite(cameraInHead) || rightInHead.sqrMagnitude < 0.9f || upInHead.sqrMagnitude < 0.9f) return;

            m_frame.Clear();
            m_frame.CaptureTime = captureTime;
            Vector3 origin = head.MultiplyPoint3x4(cameraInHead);
            Vector3 right = head.MultiplyVector(rightInHead).normalized;
            Vector3 up = head.MultiplyVector(upInHead).normalized;
            Quaternion cameraRotation = Quaternion.LookRotation(head.MultiplyVector(forwardInHead), up);
            m_frame.Camera = new Pose(origin, cameraRotation);
            m_frame.MotionReliable = EvaluateMotion(m_frame.Camera, captureTime);

            for (int slot = 0; slot < MaxDetections; slot++)
            {
                int row = slot * RowSize;
                float score = m_values[row];
                float classValue = m_values[row + 1];
                float width = m_values[row + 2];
                float height = m_values[row + 3];
                if (!(score >= PipelineScoreFloor) || score > 1f || classValue < 0 ||
                    classValue >= CocoClasses.Count || !(width > 0) || !(height > 0)) continue;
                Vector3 pointInHead = FromOpenXr(m_values[row + 7], m_values[row + 11], m_values[row + 15]);
                if (!IsFinite(pointInHead)) continue;
                Vector3 planePoint = head.MultiplyPoint3x4(pointInHead);
                m_frame.Rays.Add(new DetectionRay
                {
                    ClassId = Mathf.RoundToInt(classValue),
                    Confidence = score,
                    Origin = origin,
                    PlaneDirection = planePoint - origin,
                    CameraRight = right,
                    CameraUp = up,
                    PlaneWidth = width,
                    PlaneHeight = height,
                });
            }
            FrameReady?.Invoke(m_frame);
        }

        /// <summary>Source B's YoloMotionGate: boxes smear when the camera moves or turns quickly.</summary>
        private bool EvaluateMotion(Pose camera, double time)
        {
            bool reliable = false;
            if (m_hasPreviousCamera)
            {
                double dt = time - m_previousCameraTime;
                if (dt > 0 && dt <= 0.5)
                {
                    float speed = (float)(Vector3.Distance(camera.position, m_previousCamera.position) / dt);
                    float turn = (float)(Quaternion.Angle(camera.rotation, m_previousCamera.rotation) / dt);
                    reliable = speed <= m_maxCameraSpeed && turn <= m_maxCameraTurnDegrees;
                }
            }
            m_previousCamera = camera;
            m_previousCameraTime = time;
            m_hasPreviousCamera = true;
            return reliable;
        }

        private void CountRate()
        {
            m_rateFrames++;
            float elapsed = Time.unscaledTime - m_rateWindowStart;
            if (elapsed < 2f) return;
            InferenceRate = m_rateFrames / elapsed;
            m_rateFrames = 0;
            m_rateWindowStart = Time.unscaledTime;
        }

        private static Vector3 FromOpenXr(float x, float y, float z) => new Vector3(x, y, -z);

        private static bool IsFinite(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);

        private void Fail(string message)
        {
            m_failed = true;
            Status = message.Split('\n')[0];
            Debug.LogError($"{LogTag} {message}");
        }

        public void ShutdownSpatialML()
        {
            if (m_isShuttingDown) return;
            m_isShuttingDown = true;
            enabled = false;
            if (!m_inFlight) DestroyProvider();
        }

        private void DestroyProvider()
        {
            Provider provider = m_provider;
            m_provider = null;
            m_pipeline = null;
            m_output = null;
            provider?.Destroy();
            if (m_values.IsCreated) m_values.Dispose();
        }

        private void OnDestroy() => ShutdownSpatialML();
        private void OnApplicationQuit() => ShutdownSpatialML();
    }
}
