// Copyright (2026) Bytedance Ltd. and/or its affiliates
// Licensed under the Apache License, Version 2.0.

using System;
using ByteDance.PICO.SecureMR;

namespace PicoXR.SpatialML.Demo.Spatial
{
    /// <summary>Loads and submits the packaged tracking graph; creates no operators in app code.</summary>
    internal sealed class FullSpaceFacePipeline
    {
        public const string PackageId = "face-full-space";
        public const string PipelineId = "tracking";
        public readonly SpatialMLPipelineZooPipeline Pipeline;
        public readonly Tensor Result;
        private readonly Tensor m_sequence;

        public FullSpaceFacePipeline(Provider provider, SpatialMLPipelineZooAsset package)
        {
            var bundle = SpatialMLPipelineZoo.LoadPackage(provider, package, PipelineId);
            Pipeline = bundle[PipelineId];
            Result = RequireGlobal(bundle, "frame_result");
            m_sequence = RequireGlobal(bundle, "frame_sequence");
            ValidateShape(Result, 1, FullSpaceFaceFrame.ValueCount);
            ValidateShape(m_sequence, 1, 1);
            if (Result.DataType != SecureMRTensorDataType.Double || Result.Channels != 1 ||
                m_sequence.DataType != SecureMRTensorDataType.Int || m_sequence.Channels != 1)
                throw new InvalidOperationException("Tracking globals must be FLOAT64 result and INT32 sequence matrices.");
            Result.Reset(new double[FullSpaceFaceFrame.ValueCount]);
            foreach (string name in new[] { "vst_left_image", "vst_right_image" })
            {
                if (!Pipeline.Tensors.TryGetValue(name, out Tensor image))
                    throw new InvalidOperationException($"The tracking package has no '{name}' tensor.");
                ValidateShape(image, FullSpaceFaceFrame.CaptureHeight, FullSpaceFaceFrame.CaptureWidth);
                if (image.DataType != SecureMRTensorDataType.Byte || image.Channels != 3)
                    throw new InvalidOperationException("Tracking camera images must have three UINT8 channels.");
            }
        }

        public ulong Execute(int sequence, ulong previousRun)
        {
            m_sequence.Reset(new[] { sequence });
            return previousRun == 0 ? Pipeline.Execute() : Pipeline.ExecuteAfter(previousRun);
        }

        private static Tensor RequireGlobal(SpatialMLPipelineZooBundle bundle, string name)
        {
            if (!bundle.GlobalTensors.TryGetValue(name, out Tensor tensor))
                throw new InvalidOperationException($"The tracking package has no global '{name}'.");
            return tensor;
        }

        private static void ValidateShape(Tensor tensor, int rows, int columns)
        {
            if (tensor.Dimensions.Length != 2 || tensor.Dimensions[0] != rows || tensor.Dimensions[1] != columns)
                throw new InvalidOperationException($"Tracking tensor dimensions must be {rows}x{columns}.");
        }
    }
}
