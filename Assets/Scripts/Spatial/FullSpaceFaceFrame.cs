// Copyright (2026) Bytedance Ltd. and/or its affiliates
// Licensed under the Apache License, Version 2.0.

using System;
using Unity.Collections;
using UnityEngine;

namespace PicoXR.SpatialML.Demo.Spatial
{
    /// <summary>The acknowledged 256-byte result of one Full Space face frame.</summary>
    public readonly struct FullSpaceFaceFrame
    {
        public const int CaptureWidth = 338;
        public const int CaptureHeight = 256;
        public const int DetectionValueCount = 21;
        public const int ValueCount = 32;
        public const int ByteCount = ValueCount * sizeof(double);
        public const int DetectionOffset = 3;
        public const int FocalLengthOffset = 24;
        public const int TimestampOffset = 25;
        public const int SequenceOffset = 29;
        public const int PublicationMarkerOffset = 30;

        public readonly Vector3 CameraPoint;
        public readonly Rect PixelBounds;
        public readonly float Confidence;
        public readonly float FocalLengthY;
        public readonly double CaptureTime;
        public readonly bool HasFiniteDetection;

        private FullSpaceFaceFrame(NativeArray<double> values)
        {
            CameraPoint = new Vector3((float)values[0], (float)values[1], (float)values[2]);
            PixelBounds = Rect.MinMaxRect((float)values[3], (float)values[4],
                (float)values[5], (float)values[6]);
            Confidence = (float)values[7];
            FocalLengthY = (float)values[FocalLengthOffset];
            CaptureTime = DecodeTimestamp(values[TimestampOffset], values[TimestampOffset + 1],
                values[TimestampOffset + 2], values[TimestampOffset + 3]);
            HasFiniteDetection = true;
            for (int i = 0; i < DetectionValueCount; i++)
                if (!IsFinite((float)values[DetectionOffset + i])) HasFiniteDetection = false;
        }

        public static bool HasCompletion(NativeArray<double> values, int returnedBytes, int sequence) =>
            values.IsCreated && values.Length == ValueCount && returnedBytes == ByteCount &&
            values[SequenceOffset] == sequence && values[PublicationMarkerOffset] == 1;

        public static FullSpaceFaceFrame Read(NativeArray<double> values)
        {
            if (!values.IsCreated || values.Length != ValueCount)
                throw new ArgumentException("A face frame must contain 32 FLOAT64 values.", nameof(values));
            return new FullSpaceFaceFrame(values);
        }

        /// <summary>Apply the existing camera corrections using the head pose at capture time.</summary>
        public bool TryGetPlacement(Matrix4x4 captureHeadPose, Vector3 cameraLocalOffset, float margin,
            out Vector3 position, out Quaternion rotation, out float size)
        {
            position = default;
            rotation = Quaternion.identity;
            size = 0;
            if (!HasFiniteDetection || PixelBounds.width <= 0 || PixelBounds.height <= 0 ||
                !IsFinite(CameraPoint.x) || !IsFinite(CameraPoint.y) || !IsFinite(CameraPoint.z) ||
                !IsFinite(FocalLengthY) || FocalLengthY <= 0) return false;

            // Native camera XYZ -> Unity head-local coordinates. The former camera/world
            // matrices cancel in the stage rebase; only the capture-time head pose is needed.
            Vector3 cameraPoint = new Vector3(CameraPoint.x, -CameraPoint.y, CameraPoint.z + 0.03f);
            float depth = Mathf.Abs(cameraPoint.z);
            if (depth < 0.08f || depth > 5.0f) return false;
            size = Mathf.Max(PixelBounds.height, 1) * depth / FocalLengthY * margin;
            Vector3 local = new Vector3(cameraPoint.x, cameraPoint.y, -cameraPoint.z) + cameraLocalOffset;
            position = captureHeadPose.MultiplyPoint3x4(local);
            rotation = captureHeadPose.rotation;
            return IsFinite(size) && size > 0 && IsFinite(position.x) &&
                   IsFinite(position.y) && IsFinite(position.z);
        }

        private static double DecodeTimestamp(double secondsHigh, double secondsLow,
            double nanosHigh, double nanosLow)
        {
            // Low INT32 words are unsigned halves of the timestamp, even when negative.
            double seconds = secondsHigh * 4294967296.0 + unchecked((uint)(int)secondsLow);
            double nanos = nanosHigh * 4294967296.0 + unchecked((uint)(int)nanosLow);
            return seconds + nanos * 1e-9;
        }

        public static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
