// Copyright (2025) Bytedance Ltd. and/or its affiliates
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
using UnityEngine.InputSystem;

namespace PicoXR.SpatialML.Demo.Spatial
{
    /// <summary>
    /// Spatial-mode counterpart of <c>StylizeDemo</c>. It runs the same LiteRT style
    /// prediction and style transfer pipeline on the rectified VST feed, then renders the
    /// stylized image onto a <c>portal.gltf</c> panel inside a portal Spatial container.
    ///
    /// The XR sample paints the stylized texture with the XR-only render operators
    /// (LoadTexture / UpdateGltf / SwitchGltfRenderStatus). Those operators are rejected by
    /// the Spatial backend, so this variant renders through the Spatial scene graph instead:
    /// it makes the glTF visible and places it with <see cref="UpdateComponentOperator"/>
    /// transform properties, then pushes each stylized frame into the panel material's base
    /// color texture (<c>ModelComponent:BaseColorTexture</c>).
    /// </summary>
    public sealed class StylizePortalSpatialSample : MonoBehaviour, ISpatialMLServiceCleanup
    {
        public event Action<int> StyleSelected;

        [Header("Assets")]
        [SerializeField] private TextAsset m_portalGltfAsset;
        [Tooltip("Style prediction model (.tflite / LiteRT format).")]
        [SerializeField] private TextAsset m_stylePredictorModel;
        [Tooltip("Style transfer model (.tflite / LiteRT format).")]
        [SerializeField] private TextAsset m_styleTransferModel;
        [SerializeField] private Texture2D[] m_styleTextures;

        [Header("Spatial container")]
        [SerializeField] private int m_vstWidth = 800;
        [SerializeField] private int m_vstHeight = 606;
        [Tooltip("Container dimensions in millimetres. Scene-graph positions below use metres.")]
        [SerializeField] private Vector3Int m_containerSize = new Vector3Int(1500, 1500, 1000);

        [Header("Disc placement")]
        [Tooltip("Common scene-graph root that positions the complete disc at the container origin.")]
        [SerializeField] private string m_discRootEntityPath = "/";
        [Tooltip("Scene-graph entity that owns the textured center of portal.gltf.")]
        [SerializeField] private string m_discEntityPath = "/Panel";
        [Tooltip("Material slot on the Panel entity that receives the stylized image.")]
        [SerializeField] private int m_discMaterialIndex;
        [Tooltip("Disc position in metres relative to the portal. Keep it inside the container depth.")]
        [SerializeField] private Vector3 m_discPosition = new Vector3(0.0f, 0.0f, -0.2f);

        [Header("Runtime")]
        [Tooltip("LiteRT backend for both stylization models.")]
        [SerializeField] private SecureMRModelTarget m_modelTarget = SecureMRModelTarget.Npu;
        [SerializeField] private float m_intervalBetweenPipelineRuns = 0.1f;
        [SerializeField] private int m_styleImageDim = 256;
        [SerializeField] private int m_contentImageDim = 384;
        [Tooltip("Side length, in VST pixels, of the centered square cropped from the VST feed " +
                 "before stylization.")]
        [SerializeField] private int m_vstCropSize = 384;

        [Header("Model IO")]
        [SerializeField] private string m_stylePredictorInputOperatorIOName = "style_image";
        [SerializeField] private string m_stylePredictorOutputOperatorIOName = "mobilenet_conv/Conv/BiasAdd";
        [SerializeField] private string m_styleTransferContentInputOperatorIOName = "content_image";
        [SerializeField] private string m_styleTransferStyleInputOperatorIOName = "mobilenet_conv/Conv/BiasAdd";
        [SerializeField] private string m_styleTransferOutputOperatorIOName = "transformer/expand/conv3/conv/Sigmoid";

        private Provider m_provider;

        private Pipeline m_initializationPipeline;
        private Tensor m_initializationGltfPlaceholder;
        private Pipeline m_vstPipeline;
        private Pipeline m_stylePredictionPipeline;
        private Pipeline m_styleTransferPipeline;
        private Pipeline m_renderPipeline;

        private Tensor m_portalGltf;
        private Tensor m_vstImageGlobal;
        private Tensor m_styleTextureGlobal;
        private Tensor m_predictedStyleGlobal;
        private Tensor m_srcPointsGlobal;
        private readonly Tensor[] m_stylizedRgbaGlobals = new Tensor[2];

        private Tensor m_vstImageWrite;

        private Tensor m_styleTexturePlaceholder;
        private Tensor m_predictedStyleWrite;

        private Tensor m_vstImagePlaceholder;
        private Tensor m_predictedStylePlaceholder;
        private Tensor m_srcPointsPlaceholder;
        private Tensor m_stylizedRgbaWrite;

        private Tensor m_renderGltfPlaceholder;
        private Tensor m_stylizedRgbaPlaceholder;

        private int m_styleTextureIndex;
        private bool m_styleNeedsPrediction;
        private int m_nextTextureIndex;
        private float m_elapsedTime;
        private ulong m_lastPipelineRunId;
        private bool m_leftTriggerPressed;
        private bool m_rightTriggerPressed;
        private bool m_isShuttingDown;
        private InputAction m_leftTriggerAction;
        private InputAction m_rightTriggerAction;

        private void Awake()
        {
            if (!IsSpatialModeEnabled())
            {
                Debug.LogError(
                    $"{nameof(StylizePortalSpatialSample)} requires the Spatial build variant. " +
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

            if (m_portalGltfAsset == null || m_stylePredictorModel == null || m_styleTransferModel == null)
            {
                Debug.LogError($"{nameof(StylizePortalSpatialSample)} is missing required assets.");
                enabled = false;
                yield break;
            }

            if (m_styleTextures == null || m_styleTextures.Length == 0)
            {
                Debug.LogError($"{nameof(StylizePortalSpatialSample)} requires at least one style texture.");
                enabled = false;
                yield break;
            }

            Exception providerException = null;
            const int providerStartAttempts = 10;
            for (int attempt = 1; attempt <= providerStartAttempts; attempt++)
            {
                try
                {
                    Debug.Log($"[StylizePortal] step: CreateProvider attempt {attempt}");
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
                        $"{nameof(StylizePortalSpatialSample)} failed to create its provider: {providerException}");
                    enabled = false;
                    yield break;
                }

                Debug.LogWarning(
                    $"[StylizePortal] SpatialML service is still starting; retrying provider creation " +
                    $"({attempt}/{providerStartAttempts}).");
                yield return new WaitForSecondsRealtime(1.0f);
            }

            try
            {
                Debug.Log("[StylizePortal] step: CreateGlobals");
                CreateGlobals();
                Debug.Log($"[StylizePortal] step: CreatePipelines, model target={m_modelTarget}");
                CreatePipelines();

                m_styleTextureIndex = 0;
                m_styleNeedsPrediction = true;
                Debug.Log("[StylizePortal] step: UploadStyleTexture");
                UploadStyleTexture();

                Debug.Log("[StylizePortal] step: RunInitialization");
                m_lastPipelineRunId = RunInitialization(0);
                Debug.Log("[StylizePortal] step: init complete");
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"{nameof(StylizePortalSpatialSample)} failed to initialize: {exception}");
                enabled = false;
                ShutdownSpatialML();
            }
        }

        private void Update()
        {
            if (m_isShuttingDown || m_provider == null)
            {
                return;
            }

            UpdateControllerInput();

            m_elapsedTime += Time.deltaTime;
            if (m_elapsedTime < m_intervalBetweenPipelineRuns)
            {
                return;
            }

            m_elapsedTime = 0.0f;
            RunFrame();
        }

        private void OnEnable()
        {
            EnableInputActions();
        }

        private void OnDisable()
        {
            DisableInputActions();
        }

        private void OnDestroy()
        {
            DisableInputActions();
            ShutdownSpatialML();
        }

        public void AdvanceStyle()
        {
            if (m_styleTextures == null || m_styleTextures.Length == 0)
            {
                return;
            }

            SelectStyle((m_styleTextureIndex + 1) % m_styleTextures.Length);
        }

        public int StyleCount => m_styleTextures?.Length ?? 0;

        public int CurrentStyleIndex => m_styleTextureIndex;

        public Texture2D GetStyleTexture(int styleIndex)
        {
            return styleIndex >= 0 && styleIndex < StyleCount
                ? m_styleTextures[styleIndex]
                : null;
        }

        public void SelectStyle(int styleIndex)
        {
            if (styleIndex < 0 || styleIndex >= StyleCount)
            {
                return;
            }

            m_styleTextureIndex = styleIndex;
            if (m_styleTextureGlobal != null)
            {
                UploadStyleTexture();
                m_styleNeedsPrediction = true;
            }

            StyleSelected?.Invoke(m_styleTextureIndex);
        }

        public void ShutdownSpatialML()
        {
            if (m_isShuttingDown || m_provider == null)
            {
                return;
            }

            m_isShuttingDown = true;
            Provider providerToDestroy = m_provider;
            m_provider = null;
            providerToDestroy.Destroy();
        }

        private void CreateProvider()
        {
            // A portal requires a volumetric container with positive dimensions; the SDK negates
            // the depth internally to switch the container into portal mode.
            Debug.Log($"[StylizePortal] portal=true, containerMillimetres={m_containerSize}, " +
                      $"discPositionMetres={m_discPosition}");
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
            m_portalGltf = m_provider.CreateTensor<Gltf>(m_portalGltfAsset.bytes);
            // The VST frame stays in the protected SpatialML graph. Matrix is the image-processing
            // tensor contract used by RectifiedVstAccess and ApplyAffine; it is never read back or
            // copied into a Unity Texture2D.
            m_vstImageGlobal = m_provider.CreateTensor<byte, Matrix>(3, new TensorShape(m_vstHeight, m_vstWidth));
            m_styleTextureGlobal = m_provider.CreateTensor<byte, Matrix>(3, new TensorShape(m_styleImageDim, m_styleImageDim));
            m_predictedStyleGlobal = m_provider.CreateTensor<float, Matrix>(100, new TensorShape(1, 1));
            m_srcPointsGlobal = m_provider.CreateTensor<float, Point>(2, new TensorShape(3), BuildCropSrcPoints());
            for (int i = 0; i < m_stylizedRgbaGlobals.Length; i++)
            {
                m_stylizedRgbaGlobals[i] = m_provider.CreateTensor<byte, DynamicTexture>(
                    4,
                    new TensorShape(m_contentImageDim, m_contentImageDim));
            }
        }

        private void CreatePipelines()
        {
            Debug.Log("[StylizePortal] CreateInitializationPipeline");
            CreateInitializationPipeline();
            Debug.Log("[StylizePortal] CreateVstPipeline");
            CreateVstPipeline();
            Debug.Log("[StylizePortal] CreateStylePredictionPipeline");
            CreateStylePredictionPipeline();
            Debug.Log("[StylizePortal] CreateStyleTransferPipeline");
            CreateStyleTransferPipeline();
            Debug.Log("[StylizePortal] CreateRenderPipeline");
            CreateRenderPipeline();
        }

        private void CreateInitializationPipeline()
        {
            // Place the common disc root inside the portal while preserving the authored
            // transforms on its Rim and Panel children.
            m_initializationPipeline = m_provider.CreatePipeline();
            m_initializationGltfPlaceholder = m_initializationPipeline.CreateTensorReference<Gltf>();

            Tensor visibility = m_initializationPipeline.CreateTensor<byte, Scalar>(
                1,
                new TensorShape(1),
                new byte[] { 1 });
            var showPortal = m_initializationPipeline.CreateOperator<ScenegraphVisibilityOperator>();
            showPortal.SetOperand("scenegraph", m_initializationGltfPlaceholder);
            showPortal.SetOperand("visible", visibility);

            Tensor position = m_initializationPipeline.CreateTensor<float, Matrix>(
                1,
                new TensorShape(3, 1),
                BuildPositionData());
            var setDiscPosition = m_initializationPipeline.CreateOperator<UpdateComponentOperator>(
                new UpdateComponentOperatorConfiguration(
                    m_discRootEntityPath,
                    SceneGraphProperty.Transform.Position.Path));
            setDiscPosition.SetOperand("scenegraph", m_initializationGltfPlaceholder);
            setDiscPosition.SetOperand("data", position);
        }

        private void CreateVstPipeline()
        {
            m_vstPipeline = m_provider.CreatePipeline();

            var vstOp = m_vstPipeline.CreateOperator<RectifiedVstAccessOperator>();
            m_vstImageWrite = m_vstPipeline.CreateTensorReference<byte, Matrix>(3, new TensorShape(m_vstHeight, m_vstWidth));
            vstOp.SetResult("left image", m_vstImageWrite);
        }

        private void CreateStylePredictionPipeline()
        {
            m_stylePredictionPipeline = m_provider.CreatePipeline();

            m_styleTexturePlaceholder =
                m_stylePredictionPipeline.CreateTensorReference<byte, Matrix>(3, new TensorShape(m_styleImageDim, m_styleImageDim));
            m_predictedStyleWrite = m_stylePredictionPipeline.CreateTensorReference<float, Matrix>(100, new TensorShape(1, 1));

            var styleRgbFloat = m_stylePredictionPipeline.CreateTensor<float, Matrix>(3, new TensorShape(m_styleImageDim, m_styleImageDim));
            var styleRgbNormalized = m_stylePredictionPipeline.CreateTensor<float, Matrix>(3, new TensorShape(m_styleImageDim, m_styleImageDim));

            var byteToFloatOp = m_stylePredictionPipeline.CreateOperator<AssignmentOperator>();
            byteToFloatOp.SetOperand("src", m_styleTexturePlaceholder);
            byteToFloatOp.SetResult("dst", styleRgbFloat);

            var normalizeOp = m_stylePredictionPipeline.CreateOperator<ArithmeticComposeOperator>(
                new ArithmeticComposeOperatorConfiguration("{0} / 255.0"));
            normalizeOp.SetOperand("{0}", styleRgbFloat);
            normalizeOp.SetResult("result", styleRgbNormalized);

            var modelConfig = new ModelOperatorConfiguration(
                m_stylePredictorModel.bytes,
                SecureMRModelType.LiteRtModel,
                "style_predictor",
                new LiteRtModelConfiguration(m_modelTarget));

            var modelOp = m_stylePredictionPipeline.CreateOperator<RunModelInferenceOperator>(modelConfig);
            modelOp.SetOperand(m_stylePredictorInputOperatorIOName, styleRgbNormalized);
            modelOp.SetResult(m_stylePredictorOutputOperatorIOName, m_predictedStyleWrite);
        }

        private void CreateStyleTransferPipeline()
        {
            m_styleTransferPipeline = m_provider.CreatePipeline();

            m_vstImagePlaceholder = m_styleTransferPipeline.CreateTensorReference<byte, Matrix>(3, new TensorShape(m_vstHeight, m_vstWidth));
            m_predictedStylePlaceholder = m_styleTransferPipeline.CreateTensorReference<float, Matrix>(100, new TensorShape(1, 1));
            m_srcPointsPlaceholder = m_styleTransferPipeline.CreateTensorReference<float, Point>(2, new TensorShape(3));
            m_stylizedRgbaWrite = m_styleTransferPipeline.CreateTensorReference<byte, DynamicTexture>(4, new TensorShape(m_contentImageDim, m_contentImageDim));

            var dstPoints = m_styleTransferPipeline.CreateTensor<float, Point>(2, new TensorShape(3), new[]
            {
                0.0f, 0.0f,
                (float)m_contentImageDim, 0.0f,
                0.0f, (float)m_contentImageDim,
            });
            var affineMat = m_styleTransferPipeline.CreateTensor<float, Matrix>(1, new TensorShape(2, 3));
            var slicedRgb = m_styleTransferPipeline.CreateTensor<byte, Matrix>(3, new TensorShape(m_contentImageDim, m_contentImageDim));
            var slicedRgbFloat = m_styleTransferPipeline.CreateTensor<float, Matrix>(3, new TensorShape(m_contentImageDim, m_contentImageDim));
            var normalizedRgb = m_styleTransferPipeline.CreateTensor<float, Matrix>(3, new TensorShape(m_contentImageDim, m_contentImageDim));
            var stylizedFloat = m_styleTransferPipeline.CreateTensor<float, Matrix>(3, new TensorShape(m_contentImageDim, m_contentImageDim));
            var stylizedScaled = m_styleTransferPipeline.CreateTensor<float, Matrix>(3, new TensorShape(m_contentImageDim, m_contentImageDim));
            var stylizedRgb = m_styleTransferPipeline.CreateTensor<byte, Matrix>(3, new TensorShape(m_contentImageDim, m_contentImageDim));

            var getAffineOp = m_styleTransferPipeline.CreateOperator<GetAffineOperator>();
            getAffineOp.SetOperand("src", m_srcPointsPlaceholder);
            getAffineOp.SetOperand("dst", dstPoints);
            getAffineOp.SetResult("result", affineMat);

            var applyAffineOp = m_styleTransferPipeline.CreateOperator<ApplyAffineOperator>();
            applyAffineOp.SetOperand("affine", affineMat);
            applyAffineOp.SetOperand("src image", m_vstImagePlaceholder);
            applyAffineOp.SetResult("dst image", slicedRgb);

            var uint8ToFloatOp = m_styleTransferPipeline.CreateOperator<AssignmentOperator>();
            uint8ToFloatOp.SetOperand("src", slicedRgb);
            uint8ToFloatOp.SetResult("dst", slicedRgbFloat);

            var normalizeOp = m_styleTransferPipeline.CreateOperator<ArithmeticComposeOperator>(
                new ArithmeticComposeOperatorConfiguration("{0} / 255.0"));
            normalizeOp.SetOperand("{0}", slicedRgbFloat);
            normalizeOp.SetResult("result", normalizedRgb);

            var modelConfig = new ModelOperatorConfiguration(
                m_styleTransferModel.bytes,
                SecureMRModelType.LiteRtModel,
                "style_transfer",
                new LiteRtModelConfiguration(m_modelTarget));

            var modelOp = m_styleTransferPipeline.CreateOperator<RunModelInferenceOperator>(modelConfig);
            modelOp.SetOperand(m_styleTransferContentInputOperatorIOName, normalizedRgb);
            modelOp.SetOperand(m_styleTransferStyleInputOperatorIOName, m_predictedStylePlaceholder);
            modelOp.SetResult(m_styleTransferOutputOperatorIOName, stylizedFloat);

            var scaleOp = m_styleTransferPipeline.CreateOperator<ArithmeticComposeOperator>(
                new ArithmeticComposeOperatorConfiguration("{0} * 255.0"));
            scaleOp.SetOperand("{0}", stylizedFloat);
            scaleOp.SetResult("result", stylizedScaled);

            var floatToUint8Op = m_styleTransferPipeline.CreateOperator<AssignmentOperator>();
            floatToUint8Op.SetOperand("src", stylizedScaled);
            floatToUint8Op.SetResult("dst", stylizedRgb);

            var rgbToRgbaOp = m_styleTransferPipeline.CreateOperator<ConvertColorOperator>(
                new ColorConvertOperatorConfiguration(0));
            rgbToRgbaOp.SetOperand("src", stylizedRgb);
            rgbToRgbaOp.SetResult("dst", m_stylizedRgbaWrite);
        }

        private void CreateRenderPipeline()
        {
            // Paint the stylized frame onto the panel material's base color texture. This is the
            // Spatial-mode replacement for the XR LoadTexture / UpdateGltf render path.
            m_renderPipeline = m_provider.CreatePipeline();

            m_renderGltfPlaceholder = m_renderPipeline.CreateTensorReference<Gltf>();
            m_stylizedRgbaPlaceholder = m_renderPipeline.CreateTensorReference<byte, DynamicTexture>(4, new TensorShape(m_contentImageDim, m_contentImageDim));

            var updateTextureOp = m_renderPipeline.CreateOperator<UpdateComponentOperator>(
                new UpdateComponentOperatorConfiguration(
                    m_discEntityPath,
                    SceneGraphProperty.PBRMaterials[m_discMaterialIndex].BaseColorTexture.Path));
            updateTextureOp.SetOperand("scenegraph", m_renderGltfPlaceholder);
            updateTextureOp.SetOperand("data", m_stylizedRgbaPlaceholder);
        }

        private void RunFrame()
        {
            m_lastPipelineRunId = RunVst(m_lastPipelineRunId);

            if (m_styleNeedsPrediction)
            {
                m_lastPipelineRunId = RunStylePrediction(m_lastPipelineRunId);
                m_styleNeedsPrediction = false;
            }

            int writeTextureIndex = m_nextTextureIndex;
            ulong stylizeRunId = RunStyleTransfer(m_lastPipelineRunId, writeTextureIndex);

            // Keep the texture currently sampled by SpatialEngine unchanged while inference writes
            // the other buffer. RunRender uses ExecuteAfter(stylizeRunId), so SpatialML performs the
            // material swap only after that buffer is complete. The next frame is chained behind the
            // swap through m_lastPipelineRunId; Unity only selects the next queued write target here.
            m_lastPipelineRunId = RunRender(stylizeRunId, writeTextureIndex);
            m_nextTextureIndex = (writeTextureIndex + 1) % m_stylizedRgbaGlobals.Length;
        }

        private ulong RunInitialization(ulong pre)
        {
            var mapping = new TensorMapping();
            mapping.Set(m_initializationGltfPlaceholder, m_portalGltf);
            return pre == 0 ? m_initializationPipeline.Execute(mapping) : m_initializationPipeline.ExecuteAfter(pre, mapping);
        }

        private ulong RunVst(ulong pre)
        {
            var mapping = new TensorMapping();
            mapping.Set(m_vstImageWrite, m_vstImageGlobal);
            return pre == 0 ? m_vstPipeline.Execute(mapping) : m_vstPipeline.ExecuteAfter(pre, mapping);
        }

        private ulong RunStylePrediction(ulong pre)
        {
            var mapping = new TensorMapping();
            mapping.Set(m_styleTexturePlaceholder, m_styleTextureGlobal);
            mapping.Set(m_predictedStyleWrite, m_predictedStyleGlobal);
            return pre == 0 ? m_stylePredictionPipeline.Execute(mapping) : m_stylePredictionPipeline.ExecuteAfter(pre, mapping);
        }

        private ulong RunStyleTransfer(ulong pre, int textureIndex)
        {
            var mapping = new TensorMapping();
            mapping.Set(m_vstImagePlaceholder, m_vstImageGlobal);
            mapping.Set(m_predictedStylePlaceholder, m_predictedStyleGlobal);
            mapping.Set(m_srcPointsPlaceholder, m_srcPointsGlobal);
            mapping.Set(m_stylizedRgbaWrite, m_stylizedRgbaGlobals[textureIndex]);
            return pre == 0 ? m_styleTransferPipeline.Execute(mapping) : m_styleTransferPipeline.ExecuteAfter(pre, mapping);
        }

        private ulong RunRender(ulong pre, int textureIndex)
        {
            var mapping = new TensorMapping();
            mapping.Set(m_renderGltfPlaceholder, m_portalGltf);
            mapping.Set(m_stylizedRgbaPlaceholder, m_stylizedRgbaGlobals[textureIndex]);
            return pre == 0 ? m_renderPipeline.Execute(mapping) : m_renderPipeline.ExecuteAfter(pre, mapping);
        }

        private void UploadStyleTexture()
        {
            Texture2D texture = m_styleTextures[Mathf.Clamp(m_styleTextureIndex, 0, m_styleTextures.Length - 1)];
            if (!TryGetRgb24(texture, m_styleImageDim, m_styleImageDim, out byte[] rgb))
            {
                rgb = CreateFallbackStyleRgb(m_styleImageDim, m_styleImageDim);
            }

            m_styleTextureGlobal.Reset(rgb);
        }

        private void UpdateControllerInput()
        {
            float leftValue = m_leftTriggerAction != null ? m_leftTriggerAction.ReadValue<float>() : 0.0f;
            float rightValue = m_rightTriggerAction != null ? m_rightTriggerAction.ReadValue<float>() : 0.0f;

            bool leftPressed = leftValue >= 0.75f;
            bool rightPressed = rightValue >= 0.75f;

            if ((leftPressed && !m_leftTriggerPressed) || (rightPressed && !m_rightTriggerPressed))
            {
                AdvanceStyle();
            }

            m_leftTriggerPressed = leftPressed;
            m_rightTriggerPressed = rightPressed;
        }

        private void EnableInputActions()
        {
            if (m_leftTriggerAction == null)
            {
                m_leftTriggerAction = new InputAction(type: InputActionType.Value, binding: "<XRController>{LeftHand}/trigger");
            }

            if (m_rightTriggerAction == null)
            {
                m_rightTriggerAction = new InputAction(type: InputActionType.Value, binding: "<XRController>{RightHand}/trigger");
            }

            m_leftTriggerAction.Enable();
            m_rightTriggerAction.Enable();
        }

        private void DisableInputActions()
        {
            m_leftTriggerAction?.Disable();
            m_rightTriggerAction?.Disable();
        }

        private float[] BuildCropSrcPoints()
        {
            // The style-transfer model has a fixed 384x384 input. Select a matching centered
            // region from the 800x606 VST frame, avoiding any resize before inference.
            int size = Mathf.Clamp(m_vstCropSize, 1, Mathf.Min(m_vstWidth, m_vstHeight));
            float x1 = (m_vstWidth - size) * 0.5f;
            float y1 = (m_vstHeight - size) * 0.5f;
            float x2 = x1 + size;
            float y2 = y1 + size;
            return new[] { x1, y1, x2, y1, x1, y2 };
        }

        private float[] BuildPositionData()
        {
            // Transform.Position takes metres directly; only the container dimensions use millimetres.
            return new[] { m_discPosition.x, m_discPosition.y, m_discPosition.z };
        }

        private static bool IsSpatialModeEnabled()
        {
            PXR_ProjectSetting projectSettings = PXR_ProjectSetting.GetProjectConfig();
            return projectSettings != null && projectSettings.isSpatialAdapter;
        }

        private static bool TryGetRgb24(Texture2D texture, int targetWidth, int targetHeight, out byte[] rgb)
        {
            rgb = null;
            if (texture == null)
            {
                return false;
            }

            Color32[] pixels;
            try
            {
                pixels = texture.GetPixels32();
            }
            catch (Exception)
            {
                return false;
            }

            if (pixels == null || pixels.Length == 0)
            {
                return false;
            }

            int srcWidth = texture.width;
            int srcHeight = texture.height;

            Color32[] resized;
            if (srcWidth == targetWidth && srcHeight == targetHeight)
            {
                resized = pixels;
            }
            else
            {
                resized = new Color32[targetWidth * targetHeight];
                for (int y = 0; y < targetHeight; y++)
                {
                    int srcY = (y * srcHeight) / targetHeight;
                    for (int x = 0; x < targetWidth; x++)
                    {
                        int srcX = (x * srcWidth) / targetWidth;
                        resized[y * targetWidth + x] = pixels[srcY * srcWidth + srcX];
                    }
                }
            }

            rgb = new byte[targetWidth * targetHeight * 3];
            for (int i = 0; i < resized.Length; i++)
            {
                int baseIndex = i * 3;
                rgb[baseIndex + 0] = resized[i].r;
                rgb[baseIndex + 1] = resized[i].g;
                rgb[baseIndex + 2] = resized[i].b;
            }

            return true;
        }

        private static byte[] CreateFallbackStyleRgb(int width, int height)
        {
            byte[] rgb = new byte[width * height * 3];
            for (int i = 0; i < rgb.Length; i += 3)
            {
                rgb[i + 0] = 128;
                rgb[i + 1] = 64;
                rgb[i + 2] = 192;
            }

            return rgb;
        }
    }
}
