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
using ByteDance.PICO.SecureMR;
using ByteDance.PICO.XR;
using UnityEngine;

namespace PicoXR.SpatialML.Demo.Spatial
{
    /// <summary>
    /// Displays the device depth sensor as a distance-colorized dynamic texture on a rectangular
    /// framed panel inside a portal Spatial container. This sample contains no ML model or
    /// inference operator.
    /// </summary>
    public sealed class DepthPortalSpatialSample : MonoBehaviour, ISpatialMLServiceCleanup
    {
        private const string LogTag = "[DepthPortal]";
        private const int BgrToRgbConversion = 4;
        private const float StatsLogIntervalSeconds = 5.0f;

        [Header("Assets")]
        [SerializeField] private TextAsset m_portalGltfAsset;

        [Header("Spatial container")]
        [SerializeField] private int m_vstWidth = 800;
        [SerializeField] private int m_vstHeight = 606;
        [Tooltip("Container dimensions in millimetres. Scene-graph positions use metres. " +
                 "Keep the width:height ratio equal to the 0.9 x 0.72 m frame (5:4).")]
        [SerializeField] private Vector3Int m_containerSize = new Vector3Int(1500, 1200, 1000);

        [Header("Framed depth panel")]
        [SerializeField] private string m_frameRootEntityPath = "/";
        [SerializeField] private string m_panelEntityPath = "/Panel";
        [SerializeField] private int m_panelMaterialIndex;
        [SerializeField] private Vector3 m_framePosition = new Vector3(0.0f, 0.0f, -0.2f);

        [Header("Depth visualization")]
        [Tooltip("Raw depth is 128x160. A 256x320 target preserves that aspect ratio.")]
        [SerializeField] private int m_depthTextureWidth = 320;
        [SerializeField] private int m_depthTextureHeight = 256;
        [Tooltip("Distances at or below this value are shown in red.")]
        [SerializeField] private float m_nearDepthMetres = 0.25f;
        [Tooltip("Distances at or above this value are shown in blue.")]
        [SerializeField] private float m_farDepthMetres = 4.0f;
        [SerializeField] private float m_submitInterval = 0.1f;

        private Provider m_provider;
        private Pipeline m_initializationPipeline;
        private Pipeline m_depthPipeline;
        private Pipeline m_renderPipeline;

        private Tensor m_portalGltfGlobal;
        private readonly Tensor[] m_depthTextureGlobals = new Tensor[2];

        private Tensor m_initializationGltfPlaceholder;
        private Tensor m_depthTextureWrite;
        private Tensor m_renderGltfPlaceholder;
        private Tensor m_depthTexturePlaceholder;

        private ulong m_lastRunId;
        private float m_nextSubmitTime;
        private float m_nextStatsTime;
        private int m_nextTextureIndex;
        private int m_frameCount;
        private bool m_isShuttingDown;

        private void Awake()
        {
            if (!IsSpatialModeEnabled())
            {
                Debug.LogError(
                    $"{nameof(DepthPortalSpatialSample)} requires the Spatial build variant. " +
                    "Run PICO > SpatialML > Build Variants > Configure Spatial.");
                enabled = false;
            }
        }

        private IEnumerator Start()
        {
            if (!enabled)
            {
                yield break;
            }

            if (m_portalGltfAsset == null)
            {
                Debug.LogError(
                    $"{nameof(DepthPortalSpatialSample)} requires a rectangular depth-frame glTF asset.");
                enabled = false;
                yield break;
            }

            Exception providerException = null;
            const int providerStartAttempts = 10;
            for (int attempt = 1; attempt <= providerStartAttempts; attempt++)
            {
                try
                {
                    Debug.Log($"{LogTag} step: CreateProvider attempt {attempt}");
                    CreateProvider();
                    providerException = null;
                }
                catch (InvalidOperationException exception)
                {
                    providerException = exception;
                }

                if (m_provider != null)
                {
                    break;
                }

                if (providerException == null ||
                    !providerException.Message.Contains("service is not connected", StringComparison.OrdinalIgnoreCase) ||
                    attempt == providerStartAttempts)
                {
                    Debug.LogError(
                        $"{nameof(DepthPortalSpatialSample)} failed to create its provider: {providerException}");
                    enabled = false;
                    yield break;
                }

                Debug.LogWarning(
                    $"{LogTag} SpatialML service is still starting; retrying provider creation " +
                    $"({attempt}/{providerStartAttempts}).");
                yield return new WaitForSecondsRealtime(1.0f);
            }

            try
            {
                CreateGlobals();
                CreatePipelines();
                m_lastRunId = RunInitialization(0);
                Debug.Log($"{LogTag} step: initialized model-free depth portal");
            }
            catch (Exception exception)
            {
                Debug.LogError($"{nameof(DepthPortalSpatialSample)} failed to initialize: {exception}");
                enabled = false;
                ShutdownSpatialML();
            }
        }

        private void Update()
        {
            if (m_isShuttingDown || m_provider == null || Time.unscaledTime < m_nextSubmitTime)
            {
                return;
            }

            m_nextSubmitTime = Time.unscaledTime + m_submitInterval;
            int writeTextureIndex = m_nextTextureIndex;
            ulong depthRun = RunDepth(m_lastRunId, writeTextureIndex);
            m_lastRunId = RunRender(depthRun, writeTextureIndex);
            m_nextTextureIndex = (writeTextureIndex + 1) % m_depthTextureGlobals.Length;
            m_frameCount++;

            if (Time.unscaledTime >= m_nextStatsTime)
            {
                m_nextStatsTime = Time.unscaledTime + StatsLogIntervalSeconds;
                Debug.Log($"{LogTag} stats: depth frames={m_frameCount}, lastRun={m_lastRunId}");
            }
        }

        private void OnDestroy()
        {
            ShutdownSpatialML();
        }

        private void OnApplicationQuit()
        {
            ShutdownSpatialML();
        }

        public void ShutdownSpatialML()
        {
            if (m_isShuttingDown || m_provider == null)
            {
                return;
            }

            m_isShuttingDown = true;
            enabled = false;

            Provider providerToDestroy = m_provider;
            m_provider = null;
            providerToDestroy.Destroy();
        }

        private void CreateProvider()
        {
            Debug.Log($"{LogTag} portal=true, containerMillimetres={m_containerSize}, " +
                      $"framePositionMetres={m_framePosition}");
            m_provider = new Provider(
                m_vstWidth,
                m_vstHeight,
                new SpatialContainerConfiguration(
                    m_containerSize.x,
                    m_containerSize.y,
                    m_containerSize.z,
                    isPortal: true));
        }

        private void CreateGlobals()
        {
            m_portalGltfGlobal = m_provider.CreateTensor<Gltf>(m_portalGltfAsset.bytes);
            for (int index = 0; index < m_depthTextureGlobals.Length; index++)
            {
                m_depthTextureGlobals[index] = m_provider.CreateTensor<byte, DynamicTexture>(
                    3,
                    new TensorShape(m_depthTextureHeight, m_depthTextureWidth));
            }
        }

        private void CreatePipelines()
        {
            CreateInitializationPipeline();
            CreateDepthPipeline();
            CreateRenderPipeline();
        }

        private void CreateInitializationPipeline()
        {
            m_initializationPipeline = m_provider.CreatePipeline();
            m_initializationGltfPlaceholder = m_initializationPipeline.CreateTensorReference<Gltf>();

            Tensor visible = m_initializationPipeline.CreateTensor<byte, Scalar>(
                1,
                new TensorShape(1),
                new byte[] { 1 });
            var showPortal = m_initializationPipeline.CreateOperator<ScenegraphVisibilityOperator>();
            showPortal.SetOperand("scenegraph", m_initializationGltfPlaceholder);
            showPortal.SetOperand("visible", visible);

            Tensor position = m_initializationPipeline.CreateTensor<float, Matrix>(
                1,
                new TensorShape(3, 1),
                new[] { m_framePosition.x, m_framePosition.y, m_framePosition.z });
            var setFramePosition = m_initializationPipeline.CreateOperator<UpdateComponentOperator>(
                new UpdateComponentOperatorConfiguration(
                    m_frameRootEntityPath,
                    SceneGraphProperty.Transform.Position.Path));
            setFramePosition.SetOperand("scenegraph", m_initializationGltfPlaceholder);
            setFramePosition.SetOperand("data", position);
        }

        private void CreateDepthPipeline()
        {
            m_depthPipeline = m_provider.CreatePipeline();

            Tensor depthMetres = m_depthPipeline.CreateTensor<float, Matrix>(
                1,
                new TensorShape(m_depthTextureHeight, m_depthTextureWidth));
            Tensor distanceIndex = m_depthPipeline.CreateTensor<float, Matrix>(
                1,
                new TensorShape(m_depthTextureHeight, m_depthTextureWidth));
            Tensor inverseDistanceIndex = m_depthPipeline.CreateTensor<float, Matrix>(
                1,
                new TensorShape(m_depthTextureHeight, m_depthTextureWidth));
            Tensor colorizedDepth = m_depthPipeline.CreateTensor<float, Matrix>(
                3,
                new TensorShape(m_depthTextureHeight, m_depthTextureWidth),
                new float[m_depthTextureWidth * m_depthTextureHeight * 3]);
            Tensor colorizedDepthBytes = m_depthPipeline.CreateTensor<byte, Matrix>(
                3,
                new TensorShape(m_depthTextureHeight, m_depthTextureWidth));
            m_depthTextureWrite = m_depthPipeline.CreateTensorReference<byte, DynamicTexture>(
                3,
                new TensorShape(m_depthTextureHeight, m_depthTextureWidth));

            var depthOperator = m_depthPipeline.CreateOperator<DepthOperator>();
            depthOperator.SetResult("depth map", depthMetres);

            float depthRange = Mathf.Max(0.001f, m_farDepthMetres - m_nearDepthMetres);
            float depthToByteScale = 255.0f / depthRange;
            var scaleDistance = m_depthPipeline.CreateOperator<ArithmeticComposeOperator>(
                new ArithmeticComposeOperatorConfiguration(
                    $"({{0}} - {m_nearDepthMetres:R}) * {depthToByteScale:R}"));
            scaleDistance.SetOperand("{0}", depthMetres);
            scaleDistance.SetResult("result", distanceIndex);

            // Build a BGR image without multiplying two matrices. ArithmeticCompose's Mat * Mat
            // syntax is OpenCV matrix multiplication, not elementwise multiplication. Assignment's
            // documented channel slices let the graph write B = distance, G = 0, R = 255-distance.
            // The final byte assignment saturates distances outside the configured range.
            var invertDistance = m_depthPipeline.CreateOperator<ArithmeticComposeOperator>(
                new ArithmeticComposeOperatorConfiguration("255.0 - {0}"));
            invertDistance.SetOperand("{0}", distanceIndex);
            invertDistance.SetResult("result", inverseDistanceIndex);

            // Slices are [start, end), so channel 2 alone is { 2, 3 }.
            Tensor blueChannel = m_depthPipeline.CreateTensor<int, Slice>(
                2,
                new TensorShape(1),
                new[] { 0, 1 });
            Tensor redChannel = m_depthPipeline.CreateTensor<int, Slice>(
                2,
                new TensorShape(1),
                new[] { 2, 3 });

            var assignBlue = m_depthPipeline.CreateOperator<AssignmentOperator>();
            assignBlue.SetOperand("src", distanceIndex);
            assignBlue.SetOperand("dst channel slice", blueChannel);
            assignBlue.SetResult("dst", colorizedDepth);

            var assignRed = m_depthPipeline.CreateOperator<AssignmentOperator>();
            assignRed.SetOperand("src", inverseDistanceIndex);
            assignRed.SetOperand("dst channel slice", redChannel);
            assignRed.SetResult("dst", colorizedDepth);

            var floatToByte = m_depthPipeline.CreateOperator<AssignmentOperator>();
            floatToByte.SetOperand("src", colorizedDepth);
            floatToByte.SetResult("dst", colorizedDepthBytes);

            var bgrToRgb = m_depthPipeline.CreateOperator<ConvertColorOperator>(
                new ColorConvertOperatorConfiguration(BgrToRgbConversion));
            bgrToRgb.SetOperand("src", colorizedDepthBytes);
            bgrToRgb.SetResult("dst", m_depthTextureWrite);
        }

        private void CreateRenderPipeline()
        {
            m_renderPipeline = m_provider.CreatePipeline();
            m_renderGltfPlaceholder = m_renderPipeline.CreateTensorReference<Gltf>();
            m_depthTexturePlaceholder = m_renderPipeline.CreateTensorReference<byte, DynamicTexture>(
                3,
                new TensorShape(m_depthTextureHeight, m_depthTextureWidth));

            var updateTexture = m_renderPipeline.CreateOperator<UpdateComponentOperator>(
                new UpdateComponentOperatorConfiguration(
                    m_panelEntityPath,
                    SceneGraphProperty.PBRMaterials[m_panelMaterialIndex].BaseColorTexture.Path));
            updateTexture.SetOperand("scenegraph", m_renderGltfPlaceholder);
            updateTexture.SetOperand("data", m_depthTexturePlaceholder);
        }

        private ulong RunInitialization(ulong prerequisiteRun)
        {
            var mapping = new TensorMapping();
            mapping.Set(m_initializationGltfPlaceholder, m_portalGltfGlobal);
            return prerequisiteRun == 0
                ? m_initializationPipeline.Execute(mapping)
                : m_initializationPipeline.ExecuteAfter(prerequisiteRun, mapping);
        }

        private ulong RunDepth(ulong prerequisiteRun, int textureIndex)
        {
            var mapping = new TensorMapping();
            mapping.Set(m_depthTextureWrite, m_depthTextureGlobals[textureIndex]);
            return prerequisiteRun == 0
                ? m_depthPipeline.Execute(mapping)
                : m_depthPipeline.ExecuteAfter(prerequisiteRun, mapping);
        }

        private ulong RunRender(ulong prerequisiteRun, int textureIndex)
        {
            var mapping = new TensorMapping();
            mapping.Set(m_renderGltfPlaceholder, m_portalGltfGlobal);
            mapping.Set(m_depthTexturePlaceholder, m_depthTextureGlobals[textureIndex]);
            return prerequisiteRun == 0
                ? m_renderPipeline.Execute(mapping)
                : m_renderPipeline.ExecuteAfter(prerequisiteRun, mapping);
        }

        private static bool IsSpatialModeEnabled()
        {
            PXR_ProjectSetting projectSettings = PXR_ProjectSetting.GetProjectConfig();
            return projectSettings != null && projectSettings.isSpatialAdapter;
        }
    }
}
