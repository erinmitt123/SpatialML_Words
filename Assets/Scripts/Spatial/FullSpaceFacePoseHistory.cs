// Copyright (2026) Bytedance Ltd. and/or its affiliates
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.XR;

namespace PicoXR.SpatialML.Demo.Spatial
{
    /// <summary>Interpolates the tracked Unity head pose at the VST capture timestamp.</summary>
    public sealed class FullSpaceFacePoseHistory
    {
        private const int Capacity = 512;
        private readonly Sample[] m_samples = new Sample[Capacity];
        private int m_next;
        private int m_count;

        private struct Sample
        {
            public double monotonicTime;
            public double realtime;
            public Vector3 position;
            public Quaternion rotation;
        }

        public void RecordTrackedHeadPose()
        {
            InputDevice head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            if (!head.isValid || !head.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked) || !tracked ||
                !head.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 position) ||
                !head.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rotation) ||
                (position.sqrMagnitude <= 0.0001f && Quaternion.Angle(rotation, Quaternion.identity) <= 0.1f))
                return;
            Add(ReadClock(1), ReadClock(0), position, rotation);
        }

        public void Add(double monotonicTime, double realtime, Vector3 position, Quaternion rotation)
        {
            m_samples[m_next] = new Sample { monotonicTime = monotonicTime, realtime = realtime,
                position = position, rotation = rotation };
            m_next = (m_next + 1) % Capacity;
            m_count = Mathf.Min(m_count + 1, Capacity);
        }

        public bool TryGetAtCapture(double captureTime, out Matrix4x4 pose)
        {
            pose = Matrix4x4.identity;
            bool wallClock = IsWallClock(captureTime);
            for (int i = 0; i < m_count; i++)
            {
                int index = (m_next - 1 - i + Capacity) % Capacity;
                Sample older = m_samples[index];
                double olderTime = wallClock ? older.realtime : older.monotonicTime;
                if (olderTime > captureTime) continue;
                if (i == 0)
                {
                    if (captureTime - olderTime > 0.05) return false;
                    pose = Matrix4x4.TRS(older.position, older.rotation, Vector3.one);
                    return true;
                }
                Sample newer = m_samples[(index + 1) % Capacity];
                double newerTime = wallClock ? newer.realtime : newer.monotonicTime;
                if (newerTime - olderTime > 0.1 || newerTime <= olderTime) return false;
                float blend = (float)((captureTime - olderTime) / (newerTime - olderTime));
                pose = Matrix4x4.TRS(Vector3.Lerp(older.position, newer.position, blend),
                    Quaternion.Slerp(older.rotation, newer.rotation, blend), Vector3.one);
                return true;
            }
            return false;
        }

        public static bool IsWallClock(double timestamp) => timestamp > 1000000000.0;
        public static double Now(double captureTime) => ReadClock(IsWallClock(captureTime) ? 0 : 1);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeTimespec { public long seconds; public long nanoseconds; }

        [DllImport("libc", EntryPoint = "clock_gettime")]
        private static extern int ClockGetTime(int clockId, out NativeTimespec time);

        private static double ReadClock(int clockId)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (ClockGetTime(clockId, out NativeTimespec time) != 0)
                throw new InvalidOperationException("Cannot read the device capture clock.");
            return time.seconds + time.nanoseconds * 1e-9;
#else
            return clockId == 0 ? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 0.001
                : Time.realtimeSinceStartupAsDouble;
#endif
        }
    }
}
