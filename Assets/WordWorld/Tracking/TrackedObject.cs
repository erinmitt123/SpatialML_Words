using UnityEngine;

namespace WordWorld.Tracking
{
    /// <summary>A real object that passed multi-frame confirmation, or a practice object. Only these ever get labels.</summary>
    public sealed class TrackedObject
    {
        public int Id { get; internal set; }
        public int ClassId { get; internal set; }
        public string ClassName { get; internal set; }
        /// <summary>Smoothed object position (room mesh surface, or stereo depth), in Unity world space.</summary>
        public Vector3 Position { get; internal set; }
        public float Width { get; internal set; }
        public float Height { get; internal set; }
        public float Confidence { get; internal set; }
        public bool Confirmed { get; internal set; }
        public float FirstSeenTime { get; internal set; }
        public float LastSeenTime { get; internal set; }
        /// <summary>A practice object (a 3D model placed by the app), not something the camera saw.</summary>
        public bool IsVirtual { get; internal set; }

        // Bit i set = matched i frames ago (bit 0 is the newest frame).
        internal uint HitHistory;
        internal bool HitThisFrame;
        internal float UnseenInViewSeconds;
    }
}
