// Adapted from the SpatialML Unity samples' FullSpaceFacePoseHistory
// (Copyright 2026 Bytedance Ltd. and/or its affiliates, Apache License 2.0).

using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.XR;

namespace WordWorld.Detection
{
    /// <summary>
    /// Ring buffer of tracked head poses so detections use the pose from when the camera frame was
    /// captured, not the current one; using the current pose makes labels drift as the head moves.
    /// </summary>
    public sealed class CapturePoseHistory
    {
        private const int Capacity = 512;
        private readonly Sample[] m_samples = new Sample[Capacity];
        private int m_next;
        private int m_count;

        private struct Sample
        {
            public double MonotonicTime;
            public double Realtime;
            public Vector3 Position;
            public Quaternion Rotation;
        }

        public void RecordTrackedHeadPose()
        {
            InputDevice head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            if (!head.isValid || !head.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked) || !tracked ||
                !head.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 position) ||
                !head.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rotation) ||
                (position.sqrMagnitude <= 0.0001f && Quaternion.Angle(rotation, Quaternion.identity) <= 0.1f))
                return;
            m_samples[m_next] = new Sample
            {
                MonotonicTime = ReadClock(1), Realtime = ReadClock(0), Position = position, Rotation = rotation
            };
            m_next = (m_next + 1) % Capacity;
            m_count = Mathf.Min(m_count + 1, Capacity);
        }

        public bool TryGetLatest(out Pose pose)
        {
            pose = Pose.identity;
            if (m_count == 0) return false;
            Sample latest = m_samples[(m_next - 1 + Capacity) % Capacity];
            pose = new Pose(latest.Position, latest.Rotation);
            return true;
        }

        public bool TryGetAtCapture(double captureTime, out Matrix4x4 pose)
        {
            pose = Matrix4x4.identity;
            bool wallClock = IsWallClock(captureTime);
            for (int i = 0; i < m_count; i++)
            {
                int index = (m_next - 1 - i + Capacity) % Capacity;
                Sample older = m_samples[index];
                double olderTime = wallClock ? older.Realtime : older.MonotonicTime;
                if (olderTime > captureTime) continue;
                if (i == 0)
                {
                    if (captureTime - olderTime > 0.05) return false;
                    pose = Matrix4x4.TRS(older.Position, older.Rotation, Vector3.one);
                    return true;
                }
                Sample newer = m_samples[(index + 1) % Capacity];
                double newerTime = wallClock ? newer.Realtime : newer.MonotonicTime;
                if (newerTime - olderTime > 0.1 || newerTime <= olderTime) return false;
                float blend = (float)((captureTime - olderTime) / (newerTime - olderTime));
                pose = Matrix4x4.TRS(Vector3.Lerp(older.Position, newer.Position, blend),
                    Quaternion.Slerp(older.Rotation, newer.Rotation, blend), Vector3.one);
                return true;
            }
            return false;
        }

        public static bool IsWallClock(double timestamp) => timestamp > 1000000000.0;
        public static double Now(double captureTime) => ReadClock(IsWallClock(captureTime) ? 0 : 1);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeTimespec { public long Seconds; public long Nanoseconds; }

        [DllImport("libc", EntryPoint = "clock_gettime")]
        private static extern int ClockGetTime(int clockId, out NativeTimespec time);

        private static double ReadClock(int clockId)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (ClockGetTime(clockId, out NativeTimespec time) != 0)
                throw new InvalidOperationException("Cannot read the device capture clock.");
            return time.Seconds + time.Nanoseconds * 1e-9;
#else
            return clockId == 0 ? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 0.001
                : Time.realtimeSinceStartupAsDouble;
#endif
        }
    }
}
