// Copyright (2026) Bytedance Ltd. and/or its affiliates
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections;
using System.Threading.Tasks;
using ByteDance.PICO.SecureMR;
using ByteDance.PICO.XR;
using UnityEngine;
using Unity.Collections;

namespace PicoXR.SpatialML.Demo.Spatial
{
    /// <summary>
    /// Runs the Pipeline Zoo MediaPipe face detector in a zero-sized Full Space session. SpatialML
    /// projects the face center into camera space. Unity places an exported four-bar frame using
    /// the capture-time head pose. The detection graph uses GPU preprocessing, NPU face inference,
    /// CPU decoding, native projection/publication, and one acknowledged 256-byte readback.
    /// </summary>
    [AddComponentMenu("SpatialML/Full Space/Face Tracking")]
    public sealed class FaceDetectionFullSpaceSpatialSample : MonoBehaviour, ISpatialMLServiceCleanup
    {
        private const string LogTag = "[FaceFullSpace]";
        private const float StatsLogIntervalSeconds = 5.0f;
        private const float MaxCaptureAgeSeconds = 0.5f;
        private NativeArray<double> m_frameBuffer;
        private long m_processingTicks;
        private int m_processingSamples;
        private readonly FullSpaceFacePoseHistory m_poseHistory = new();

        [Header("Pipeline Zoo package")]
        [SerializeField] private SpatialMLPipelineZooAsset m_facePackage;

        [Header("SpatialML session")]
        [SerializeField] private int m_vstWidth = 338;
        [SerializeField] private int m_vstHeight = 256;
        [Tooltip("Zero dimensions keep the SpatialML session in Full Space without a panel.")]
        [SerializeField] private Vector3Int m_containerSize = Vector3Int.zero;
        [SerializeField, Min(0.02f)] private float m_submitInterval = 0.05f;

        [Header("Unity face frame")]
        [SerializeField] private Transform m_faceBoxRoot;
        [SerializeField, Range(0.0f, 1.0f)] private float m_scoreThreshold = 0.3f;
        [SerializeField, Min(0.01f)] private float m_boxMargin = 1.05f;
        [SerializeField, Min(0.001f)] private float m_boxBarThickness = 0.008f;
        [SerializeField, Min(0.05f)] private float m_staleResultSeconds = 2.0f;
        [Tooltip("Camera-local correction for the left VST camera relative to the headset center.")]
        [SerializeField] private Vector3 m_cameraLocalOffset = new(-0.03f, 0.0f, 0.0f);
        [SerializeField, Min(0.01f)] private float m_positionSmoothingSeconds = 0.12f;
        [SerializeField, Min(0.01f)] private float m_rotationSmoothingSeconds = 0.18f;
        [SerializeField, Min(0.01f)] private float m_sizeSmoothingSeconds = 0.15f;

        [Header("Full Space layout")]
        [SerializeField] private Vector3 m_navigationPanelPosition = new Vector3(0.0f, 1.4f, 1.4f);

        private Provider m_provider;
        private FullSpaceFacePipeline m_tracking;
        private bool m_initializing;
        private int m_sequence;
        private ulong m_lastTrackingRun;
        private double m_lastCaptureTimestamp;
        private bool m_lastCaptureWasWallClock;
        private float m_recoveryNotBefore;
        private float m_lastFreshCaptureTime;
        private int m_staleCaptureCount;
        private int m_recoveryCount;
        private bool m_inFlight;
        private bool m_isShuttingDown;
        private float m_nextSubmitTime;
        private float m_nextStatsTime;
        private float m_lastVisibleTime = float.NegativeInfinity;
        private int m_frameCount;
        private int m_detectionCount;
        private bool m_hasFaceTarget;
        private bool m_hasSmoothedFacePose;
        private Vector3 m_targetFacePosition;
        private Quaternion m_targetFaceRotation = Quaternion.identity;
        private float m_targetFaceSize;
        private Vector3 m_smoothedFacePosition;
        private Quaternion m_smoothedFaceRotation = Quaternion.identity;
        private float m_smoothedFaceSize;
        private Transform m_navigationRoot;
        private Vector3 m_navigationOriginalPosition;
        private Quaternion m_navigationOriginalRotation;

        private void Awake()
        {
            if (!IsSpatialModeEnabled())
            {
                Debug.LogError($"{nameof(FaceDetectionFullSpaceSpatialSample)} requires a Spatial build.");
                enabled = false;
                return;
            }

            if (m_faceBoxRoot == null)
            {
                m_faceBoxRoot = transform.Find("Face Box");
            }
            SetFaceBoxVisible(false);
        }

        private IEnumerator Start()
        {
            if (!enabled) yield break;
            MoveNavigationPanelIntoFullSpace();
            if (m_facePackage == null || !ValidateFaceBoxes())
            {
                Debug.LogError($"{nameof(FaceDetectionFullSpaceSpatialSample)} requires the face " +
                               "package and an exported four-bar Face Box.");
                enabled = false;
                yield break;
            }

            yield return InitializeProvider();
        }

        private IEnumerator InitializeProvider()
        {
            if (m_isShuttingDown) yield break;
            m_initializing = true;
            Exception providerException = null;
            const int providerStartAttempts = 10;
            for (int attempt = 1; attempt <= providerStartAttempts; attempt++)
            {
                if (m_isShuttingDown) { m_initializing = false; yield break; }
                try
                {
                    Debug.Log($"{LogTag} CreateProvider attempt={attempt}, vst={m_vstWidth}x{m_vstHeight}, " +
                              $"containerMillimetres={m_containerSize}");
                    m_provider = new Provider(m_vstWidth, m_vstHeight,
                        new SpatialContainerConfiguration(
                            m_containerSize.x, m_containerSize.y, m_containerSize.z));
                    providerException = null;
                }
                catch (InvalidOperationException exception)
                {
                    providerException = exception;
                }

                if (m_provider != null) break;
                if (providerException == null ||
                    !providerException.Message.Contains("service is not connected",
                        StringComparison.OrdinalIgnoreCase) || attempt == providerStartAttempts)
                {
                    Debug.LogError($"{nameof(FaceDetectionFullSpaceSpatialSample)} provider failed: " +
                                   providerException);
                    m_initializing = false;
                    enabled = false;
                    yield break;
                }
                yield return new WaitForSecondsRealtime(1.0f);
            }

            try
            {
                if (m_vstWidth != FullSpaceFaceFrame.CaptureWidth || m_vstHeight != FullSpaceFaceFrame.CaptureHeight)
                    throw new InvalidOperationException("Full Space face tracking requires 338x256 VST images.");
                if (m_containerSize != Vector3Int.zero)
                    throw new InvalidOperationException("Full Space face tracking requires a zero-sized SpatialML container.");
                m_frameBuffer = new NativeArray<double>(FullSpaceFaceFrame.ValueCount, Allocator.Persistent);
                m_tracking = new FullSpaceFacePipeline(m_provider, m_facePackage);
                m_lastTrackingRun = 0;
                m_lastFreshCaptureTime = Time.unscaledTime;
                m_lastCaptureTimestamp = 0;
                m_recoveryNotBefore = Time.unscaledTime + 8.0f;
                m_nextSubmitTime = Time.unscaledTime;
                m_initializing = false;
                Debug.Log($"{LogTag} ready: packaged tracking -> acknowledged readback -> " +
                          "Unity MeshRenderer face frame");
            }
            catch (Exception exception)
            {
                Debug.LogError($"{nameof(FaceDetectionFullSpaceSpatialSample)} initialization failed: " +
                               exception);
                ShutdownSpatialML();
            }
        }

        private void Update()
        {
            m_poseHistory.RecordTrackedHeadPose();
            if (m_isShuttingDown || m_initializing || m_tracking == null) return;
            if (!m_inFlight && Time.unscaledTime >= m_nextSubmitTime)
            {
                _ = RunFrameAsync();
            }
            if (m_hasFaceTarget && Time.unscaledTime - m_lastVisibleTime <= m_staleResultSeconds)
            {
                UpdateSmoothedFaceBox();
            }
            else if (Time.unscaledTime - m_lastVisibleTime > m_staleResultSeconds)
            {
                SetFaceBoxVisible(false);
                m_hasSmoothedFacePose = false;
            }
        }

        private async Task RunFrameAsync()
        {
            m_inFlight = true;
            bool completed = false;
            float submittedAt = Time.unscaledTime;
            try
            {
                // The package clears capture metadata before VST access and publishes the
                // sequence last. Keep one actual frame in flight until that acknowledgment.
                int sequence = ++m_sequence;
                m_lastTrackingRun = m_tracking.Execute(sequence, m_lastTrackingRun);
                await WaitForCompletionAsync(sequence, submittedAt);
                completed = true;
                if (m_isShuttingDown || this == null) return;

                ProcessFrame(sequence, submittedAt);
            }
            catch (Exception exception)
            {
                Debug.LogError($"{LogTag} frame failed: {exception}");
                // If submission/readback failed, completion is unknown. Keep the owner alive and
                // stop new submissions; never destroy buffers underneath native work on a timer.
                if (!completed)
                {
                    enabled = false;
                    SetFaceBoxVisible(false);
                }
            }
            finally
            {
                m_inFlight = !completed;
                if (completed)
                {
                    m_nextSubmitTime = Time.unscaledTime + m_submitInterval;
                    if (m_isShuttingDown) DestroyProvider();
                    else if (Time.unscaledTime >= m_recoveryNotBefore &&
                             Time.unscaledTime - m_lastFreshCaptureTime > 5.0f)
                    {
                        SetFaceBoxVisible(false);
                        m_hasFaceTarget = false;
                        m_hasSmoothedFacePose = false;
                        m_recoveryCount++;
                        Debug.LogWarning($"{LogTag} camera capture stalled; restarting completed provider, recovery={m_recoveryCount}");
                        DestroyProvider();
                        StartCoroutine(RestartProvider());
                    }
                }
            }
        }

        private IEnumerator RestartProvider()
        {
            // Session destruction returns before capture teardown finishes in this SDK. Yield
            // before recreating the provider; completed frames ensure no old backlog survives.
            m_initializing = true;
            yield return new WaitForSecondsRealtime(1.0f);
            if (!m_isShuttingDown) yield return InitializeProvider();
        }

        private async Task WaitForCompletionAsync(int sequence, float submittedAt)
        {
            bool warned = false;
            while (true)
            {
                // Readback is a snapshot. Only the terminal sequence proves this frame completed.
                // A timeout never cancels native work or permits its buffers to be destroyed.
                int bytes = await m_tracking.Result.ReadbackIntoAsync(m_frameBuffer);
                if (FullSpaceFaceFrame.HasCompletion(m_frameBuffer, bytes, sequence)) return;
                if (!warned && Time.unscaledTime - submittedAt > 5.0f)
                {
                    warned = true;
                    Debug.LogWarning($"{LogTag} waiting for sequence={sequence}, run={m_lastTrackingRun}; submissions paused");
                }
                await Task.Delay(15);
            }
        }

        private void ProcessFrame(int sequence, float submittedAt)
        {
            m_frameCount++;
            long processingStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            FullSpaceFaceFrame frame = FullSpaceFaceFrame.Read(m_frameBuffer);
            double captureTime = frame.CaptureTime;
            double age = FullSpaceFacePoseHistory.Now(captureTime) - captureTime;
            bool wallClock = FullSpaceFacePoseHistory.IsWallClock(captureTime);
            bool fresh = (captureTime > m_lastCaptureTimestamp || wallClock != m_lastCaptureWasWallClock) &&
                         age >= -0.05 && age <= MaxCaptureAgeSeconds;
            bool hasHeadPose = m_poseHistory.TryGetAtCapture(captureTime, out Matrix4x4 headPose);
            bool placed = false;
            if (fresh)
            {
                m_lastCaptureTimestamp = captureTime;
                m_lastCaptureWasWallClock = wallClock;
                m_lastFreshCaptureTime = Time.unscaledTime;
                if (FullSpaceFaceFrame.IsFinite(frame.Confidence) && frame.Confidence >= m_scoreThreshold &&
                    frame.Confidence <= 1 && hasHeadPose &&
                    TryApplyFaceBox(frame, headPose))
                {
                    placed = true;
                    m_detectionCount++;
                    m_lastVisibleTime = Time.unscaledTime - Mathf.Max(0, (float)age);
                }
            }
            else m_staleCaptureCount++;
            m_processingTicks += System.Diagnostics.Stopwatch.GetTimestamp() - processingStarted;
            m_processingSamples++;
            if (Time.unscaledTime < m_nextStatsTime) return;
            m_nextStatsTime = Time.unscaledTime + StatsLogIntervalSeconds;
            Debug.Log($"{LogTag} result frames={m_frameCount}, detections={m_detectionCount}, " +
                      $"score={frame.Confidence:F3}, capture={captureTime:F6}, ageMs={age * 1000:F1}, " +
                      $"fresh={fresh}, placed={placed}, stale={m_staleCaptureCount}, " +
                      $"headPose={hasHeadPose}, cameraXYZ={frame.CameraPoint}, " +
                      $"latencyMs={(Time.unscaledTime - submittedAt) * 1000:F1}, " +
                      $"boxVisible={AnyFaceBoxVisible()}, sequence={sequence}, run={m_lastTrackingRun}, " +
                      $"hostUs={(m_processingTicks * 1000000.0 / System.Diagnostics.Stopwatch.Frequency / m_processingSamples):F1}");
        }

        private bool TryApplyFaceBox(FullSpaceFaceFrame frame, Matrix4x4 captureHeadPose)
        {
            if (!frame.TryGetPlacement(captureHeadPose, m_cameraLocalOffset, m_boxMargin,
                out Vector3 position, out Quaternion rotation, out float size)) return false;
            m_targetFacePosition = position;
            m_targetFaceRotation = rotation;
            m_targetFaceSize = size;
            m_hasFaceTarget = true;
            if (!m_hasSmoothedFacePose)
            {
                m_smoothedFacePosition = position;
                m_smoothedFaceRotation = rotation;
                m_smoothedFaceSize = size;
                m_hasSmoothedFacePose = true;
                ApplyFaceBox(position, rotation, size);
            }
            return true;
        }

        private void UpdateSmoothedFaceBox()
        {
            if (!m_hasFaceTarget || !m_hasSmoothedFacePose) return;
            float deltaTime = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
            float positionBlend = 1.0f - Mathf.Exp(-deltaTime / m_positionSmoothingSeconds);
            float rotationBlend = 1.0f - Mathf.Exp(-deltaTime / m_rotationSmoothingSeconds);
            float sizeBlend = 1.0f - Mathf.Exp(-deltaTime / m_sizeSmoothingSeconds);
            m_smoothedFacePosition = Vector3.Lerp(
                m_smoothedFacePosition, m_targetFacePosition, positionBlend);
            m_smoothedFaceRotation = Quaternion.Slerp(
                m_smoothedFaceRotation, m_targetFaceRotation, rotationBlend);
            m_smoothedFaceSize = Mathf.Lerp(m_smoothedFaceSize, m_targetFaceSize, sizeBlend);
            ApplyFaceBox(m_smoothedFacePosition, m_smoothedFaceRotation, m_smoothedFaceSize);
        }

        private void ApplyFaceBox(Vector3 position, Quaternion rotation, float size)
        {
            Transform root = m_faceBoxRoot;
            if (root == null) return;
            root.SetPositionAndRotation(position, rotation);
            root.localScale = Vector3.one;
            ConfigureBar(root.Find("Top"), new Vector3(0, size * 0.5f, 0),
                new Vector3(size, m_boxBarThickness, m_boxBarThickness));
            ConfigureBar(root.Find("Bottom"), new Vector3(0, -size * 0.5f, 0),
                new Vector3(size, m_boxBarThickness, m_boxBarThickness));
            ConfigureBar(root.Find("Left"), new Vector3(-size * 0.5f, 0, 0),
                new Vector3(m_boxBarThickness, size, m_boxBarThickness));
            ConfigureBar(root.Find("Right"), new Vector3(size * 0.5f, 0, 0),
                new Vector3(m_boxBarThickness, size, m_boxBarThickness));
        }

        private static void ConfigureBar(Transform bar, Vector3 position, Vector3 scale)
        {
            bar.localPosition = position;
            bar.localRotation = Quaternion.identity;
            bar.localScale = scale;
        }

        private bool ValidateFaceBoxes()
        {
            Transform root = m_faceBoxRoot;
            return root != null && root.Find("Top") != null && root.Find("Bottom") != null &&
                   root.Find("Left") != null && root.Find("Right") != null;
        }

        private bool AnyFaceBoxVisible()
        {
            return m_faceBoxRoot != null && m_faceBoxRoot.localScale != Vector3.zero;
        }

        private void SetFaceBoxVisible(bool visible)
        {
            Transform root = m_faceBoxRoot;
            if (root == null) return;
            if (!visible) root.localScale = Vector3.zero;
            else if (root.localScale == Vector3.zero) root.localScale = Vector3.one;
        }

        public void ShutdownSpatialML()
        {
            if (m_isShuttingDown) return;
            m_isShuttingDown = true;
            enabled = false;
            SetFaceBoxVisible(false);
            if (!m_inFlight) DestroyProvider();
        }

        private void DestroyProvider()
        {
            Provider provider = m_provider;
            m_provider = null;
            m_tracking = null;
            provider?.Destroy();
            if (m_frameBuffer.IsCreated) m_frameBuffer.Dispose();
        }

        private void OnDestroy()
        {
            ShutdownSpatialML();
            RestoreNavigationPanel();
        }

        private void OnApplicationQuit() => ShutdownSpatialML();

        private void MoveNavigationPanelIntoFullSpace()
        {
            SceneNavigationController navigation = FindFirstObjectByType<SceneNavigationController>();
            if (navigation == null) return;
            m_navigationRoot = navigation.transform;
            m_navigationOriginalPosition = m_navigationRoot.position;
            m_navigationOriginalRotation = m_navigationRoot.rotation;
            m_navigationRoot.SetPositionAndRotation(m_navigationPanelPosition, Quaternion.identity);
        }

        private void RestoreNavigationPanel()
        {
            if (m_navigationRoot == null) return;
            m_navigationRoot.SetPositionAndRotation(m_navigationOriginalPosition,
                m_navigationOriginalRotation);
            m_navigationRoot = null;
        }

        private static bool IsSpatialModeEnabled()
        {
            PXR_ProjectSetting settings = PXR_ProjectSetting.GetProjectConfig();
            return settings != null && settings.isSpatialAdapter;
        }
    }
}
