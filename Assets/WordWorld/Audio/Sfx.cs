using System;
using UnityEngine;

namespace WordWorld.Audio
{
    /// <summary>The UI sound effects. All are short and play from a point in the room.</summary>
    public enum SfxKind { Appear, Tap, Correct, Hover, Dismiss }

    /// <summary>
    /// Where UI code asks for a sound effect, without needing a reference to the player. Labels,
    /// buttons and windows call these; <see cref="SoundFx"/> decides whether and how to play them.
    /// </summary>
    public static class Sfx
    {
        public static event Action<SfxKind, Vector3> Requested;

        /// <summary>Something popped up here: a label, a window or a card.</summary>
        public static void Appear(Vector3 position) => Requested?.Invoke(SfxKind.Appear, position);

        /// <summary>The learner touched something here.</summary>
        public static void Tap(Vector3 position) => Requested?.Invoke(SfxKind.Tap, position);

        public static void Correct(Vector3 position) => Requested?.Invoke(SfxKind.Correct, position);

        /// <summary>The learner is looking or pointing at a button here.</summary>
        public static void Hover(Vector3 position) => Requested?.Invoke(SfxKind.Hover, position);

        /// <summary>A window or card closed here.</summary>
        public static void Dismiss(Vector3 position) => Requested?.Invoke(SfxKind.Dismiss, position);
    }
}
