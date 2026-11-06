using UnityEngine;
using WordWorld.App;

namespace WordWorld.Audio
{
    /// <summary>
    /// Plays a word's pronunciation from its object (SPATIALML_SETUP.md section 9). Only ever called
    /// from a tap; respects the Audio on/off setting.
    /// </summary>
    [AddComponentMenu("Word World/Vocab Audio")]
    public sealed class VocabAudio : MonoBehaviour
    {
        [SerializeField] private VocabClips m_clips;
        [SerializeField] private AudioTurn m_turn;
        [SerializeField] private LearnerSettings m_settings;

        public bool Enabled => m_settings.AudioOn && m_clips != null && m_clips.Count > 0;

        public bool Has(int classIndex, ClipKind kind) =>
            Enabled && m_clips.Get(m_settings.TargetLanguage, classIndex, kind) != null;

        public bool Play(int classIndex, ClipKind kind, Vector3 position) =>
            Enabled && m_turn.Speak(m_clips.Get(m_settings.TargetLanguage, classIndex, kind), position);

        /// <summary>A spatial ping from an object, to draw the eye to it. Needs Audio on, not vocab clips.</summary>
        public bool Ping(Vector3 position) => m_settings.AudioOn && m_turn.Cue(AudioCues.Ping, position);

        public void Stop() => m_turn.Stop();
    }
}
