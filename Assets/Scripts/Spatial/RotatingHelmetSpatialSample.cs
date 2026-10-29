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
using ByteDance.PICO.SecureMR;
using ByteDance.PICO.XR;
using UnityEngine;

namespace PicoXR.SpatialML.Demo.Spatial
{
    /// <summary>
    /// Renders a glTF scene graph in a Spatial container and rotates one entity in place.
    /// </summary>
    public sealed class RotatingHelmetSpatialSample : MonoBehaviour, ISpatialMLServiceCleanup
    {
        private static readonly TensorShape PositionShape = new TensorShape(3, 1);
        private static readonly TensorShape RotationShape = new TensorShape(4, 1);
        private static readonly TensorShape ScaleShape = new TensorShape(3, 1);

        [Header("Scene graph")]
        [SerializeField] private TextAsset m_helmetGltfAsset;
        [SerializeField] private string m_helmetEntityPath = "/";

        [Header("Spatial container")]
        [SerializeField] private int m_vstWidth = 1024;
        [SerializeField] private int m_vstHeight = 1024;
        [SerializeField] private Vector3Int m_containerSize = new Vector3Int(500, 500, 100);

        [Header("Helmet transform")]
        [SerializeField] private Vector3 m_position = new Vector3(0.0f, 0.0f, 0.2f);
        [SerializeField] private Vector3 m_scale = Vector3.one * 0.15f;
        [SerializeField] private float m_degreesPerSecond = 30.0f;

        private Provider m_provider;
        private Pipeline m_updatePipeline;
        private Tensor m_sceneGraph;
        private Tensor m_sceneGraphPlaceholder;
        private Tensor m_rotation;
        private Tensor m_rotationPlaceholder;
        private ulong m_previousRunId;
        private float m_rotationDegrees;
        private bool m_isShuttingDown;

        private void Awake()
        {
            if (!IsSpatialModeEnabled())
            {
                Debug.LogError(
                    $"{nameof(RotatingHelmetSpatialSample)} requires the Spatial build variant. " +
                    "Run PICO > SpatialML > Build Variants > Configure Spatial.");
                enabled = false;
            }
        }

        private void Start()
        {
            if (!enabled)
            {
                return;
            }

            if (m_helmetGltfAsset == null)
            {
                Debug.LogError(
                    $"{nameof(RotatingHelmetSpatialSample)} requires a helmet glTF asset.");
                enabled = false;
                return;
            }

            try
            {
                CreateSpatialPipeline();
                ExecutePipeline();
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"{nameof(RotatingHelmetSpatialSample)} failed to initialize: {exception}");
                enabled = false;
                ShutdownSpatialML();
            }
        }

        private void Update()
        {
            if (m_isShuttingDown || m_updatePipeline == null)
            {
                return;
            }

            m_rotationDegrees = Mathf.Repeat(
                m_rotationDegrees + m_degreesPerSecond * Time.deltaTime,
                360.0f);
            m_rotation.Reset(BuildRotationData(m_rotationDegrees));
            ExecutePipeline();
        }

        private void OnDestroy()
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
            Provider providerToDestroy = m_provider;
            m_provider = null;
            m_updatePipeline = null;
            providerToDestroy.Destroy();
        }

        private void CreateSpatialPipeline()
        {
            m_provider = new Provider(
                m_vstWidth,
                m_vstHeight,
                m_containerSize.x,
                m_containerSize.y,
                m_containerSize.z);
            m_sceneGraph = m_provider.CreateTensor<Gltf>(m_helmetGltfAsset.bytes);
            CreateInitializationPipeline();

            m_updatePipeline = m_provider.CreatePipeline();
            m_sceneGraphPlaceholder = m_updatePipeline.CreateTensorReference<Gltf>();
            m_rotation = m_provider.CreateTensor<float, Matrix>(
                1,
                RotationShape,
                BuildRotationData(m_rotationDegrees));
            m_rotationPlaceholder = m_updatePipeline.CreateTensorReference<float, Matrix>(
                1,
                RotationShape);
            var updateRotation = m_updatePipeline.CreateOperator<UpdateComponentOperator>(
                new UpdateComponentOperatorConfiguration(
                    m_helmetEntityPath,
                    SceneGraphProperty.Transform.Rotation.Path));
            updateRotation.SetOperand("scenegraph", m_sceneGraphPlaceholder);
            updateRotation.SetOperand("data", m_rotationPlaceholder);
        }

        private void CreateInitializationPipeline()
        {
            Pipeline initializationPipeline = m_provider.CreatePipeline();
            Tensor initialSceneGraphPlaceholder =
                initializationPipeline.CreateTensorReference<Gltf>();
            Tensor visibility = initializationPipeline.CreateTensor<byte, Scalar>(
                1,
                new TensorShape(1),
                new byte[] { 1 });
            var showSceneGraph =
                initializationPipeline.CreateOperator<ScenegraphVisibilityOperator>();
            showSceneGraph.SetOperand("scenegraph", initialSceneGraphPlaceholder);
            showSceneGraph.SetOperand("visible", visibility);

            Tensor initialPosition = initializationPipeline.CreateTensor<float, Matrix>(
                1,
                PositionShape,
                BuildPositionData());
            var setInitialPosition = initializationPipeline.CreateOperator<UpdateComponentOperator>(
                new UpdateComponentOperatorConfiguration(
                    m_helmetEntityPath,
                    SceneGraphProperty.Transform.Position.Path));
            setInitialPosition.SetOperand("scenegraph", initialSceneGraphPlaceholder);
            setInitialPosition.SetOperand("data", initialPosition);

            Tensor initialRotation = initializationPipeline.CreateTensor<float, Matrix>(
                1,
                RotationShape,
                BuildRotationData(m_rotationDegrees));
            var setInitialRotation = initializationPipeline.CreateOperator<UpdateComponentOperator>(
                new UpdateComponentOperatorConfiguration(
                    m_helmetEntityPath,
                    SceneGraphProperty.Transform.Rotation.Path));
            setInitialRotation.SetOperand("scenegraph", initialSceneGraphPlaceholder);
            setInitialRotation.SetOperand("data", initialRotation);

            Tensor initialScale = initializationPipeline.CreateTensor<float, Matrix>(
                1,
                ScaleShape,
                BuildScaleData());
            var setInitialScale = initializationPipeline.CreateOperator<UpdateComponentOperator>(
                new UpdateComponentOperatorConfiguration(
                    m_helmetEntityPath,
                    SceneGraphProperty.Transform.Scale.Path));
            setInitialScale.SetOperand("scenegraph", initialSceneGraphPlaceholder);
            setInitialScale.SetOperand("data", initialScale);

            var mapping = new TensorMapping();
            mapping.Set(initialSceneGraphPlaceholder, m_sceneGraph);
            m_previousRunId = initializationPipeline.Execute(mapping);
        }

        private void ExecutePipeline()
        {
            var mapping = new TensorMapping();
            mapping.Set(m_sceneGraphPlaceholder, m_sceneGraph);
            mapping.Set(m_rotationPlaceholder, m_rotation);
            m_previousRunId = m_previousRunId == 0
                ? m_updatePipeline.Execute(mapping)
                : m_updatePipeline.ExecuteAfter(m_previousRunId, mapping);
        }

        private float[] BuildPositionData()
        {
            // Transform.Position accepts Unity-authored local position components directly.
            return new[] { m_position.x, m_position.y, m_position.z };
        }

        private float[] BuildRotationData(float rotationDegrees)
        {
            // TransformComponent:Rotation is a local-transform rotation. In SDK 6.1.0 the
            // upright reorientation moved out of UnityToSpatialEngineRotationQuaternion (now a
            // pure basis change that maps identity to identity) and into the dedicated
            // UnityToSpatialEngineLocalTransformRotation helper. Authoring a turntable spin
            // about Unity's up axis keeps the helmet upright and facing the viewer while it
            // rotates in place, matching the pre-6.1.0 behavior.
            Quaternion rotation =
                SpatialMLSceneGraphTransform.UnityToSpatialEngineLocalTransformRotation(
                    Quaternion.Euler(0.0f, -rotationDegrees, 0.0f));
            return new[] { rotation.w, rotation.x, rotation.y, rotation.z };
        }

        private float[] BuildScaleData()
        {
            return new[] { m_scale.x, m_scale.y, m_scale.z };
        }

        private static bool IsSpatialModeEnabled()
        {
            PXR_ProjectSetting projectSettings = PXR_ProjectSetting.GetProjectConfig();
            return projectSettings != null && projectSettings.isSpatialAdapter;
        }
    }
}
