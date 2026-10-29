using System;
using System.Collections.Generic;
using UnityEngine;

namespace WordWorld.Detection
{
    /// <summary>
    /// One detection as a world-space camera ray. The model never measures distance; the room mesh
    /// decides where along the ray the object is (SPATIALML_SETUP.md section 5).
    /// </summary>
    public struct DetectionRay
    {
        public int ClassId;
        public float Confidence;
        /// <summary>Camera centre at capture time, in Unity world space.</summary>
        public Vector3 Origin;
        /// <summary>Box centre on the camera's one-metre plane, minus Origin (not normalised).</summary>
        public Vector3 PlaneDirection;
        /// <summary>Camera axes at capture time, used to sample the box footprint on the mesh.</summary>
        public Vector3 CameraRight;
        public Vector3 CameraUp;
        /// <summary>Box width and height on the one-metre plane (metres per metre of depth).</summary>
        public float PlaneWidth;
        public float PlaneHeight;
    }

    /// <summary>A detection resolved onto the room mesh.</summary>
    public struct PlacedDetection
    {
        public int ClassId;
        public float Confidence;
        public Vector3 Position;
        public float Width;
        public float Height;
    }

    /// <summary>Everything one completed inference frame produced.</summary>
    public sealed class DetectionFrame
    {
        public readonly List<DetectionRay> Rays = new List<DetectionRay>(32);
        public double CaptureTime;
        public Pose Camera;
        /// <summary>False while the head moves too fast for boxes to line up with the mesh.</summary>
        public bool MotionReliable;

        public void Clear()
        {
            Rays.Clear();
            CaptureTime = 0;
            Camera = Pose.identity;
            MotionReliable = false;
        }
    }

    /// <summary>
    /// Model-agnostic detector. YOLOX today; an Apache-2.0 EfficientDet swap only replaces the
    /// implementation (CLAUDE.md section 1).
    /// </summary>
    public interface IObjectDetector
    {
        /// <summary>Raised on the main thread; the frame object is reused, so copy what you keep.</summary>
        event Action<DetectionFrame> FrameReady;
        bool IsRunning { get; }
        string Status { get; }
        /// <summary>Completed inference frames per second, averaged over the last few seconds.</summary>
        float InferenceRate { get; }
    }
}
