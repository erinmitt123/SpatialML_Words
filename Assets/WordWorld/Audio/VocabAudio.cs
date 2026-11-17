using UnityEngine;
using WordWorld.App;

namespace WordWorld.Audio
{
    /// <summary>
    /// Plays a word's pronunciation from its object (SPATIALML_SETUP.md section 9). Only ever called
    /// from a tap. Words need Sound on All; the Say It ping is an alert, so it plays on Alerts too.
    /// </summary>
    [AddComponentMenu("Word World/Vocab Audio")]
    public sealed class VocabAudio : MonoBehaviour
    {
        [SerializeField] private VocabClips m_clips;
        [SerializeField] private AudioTurn m_turn;
        [SerializeField] private LearnerSettings m_settings;

        public bool Enabled => m_settings.SpokenWords && m_clips != null && m_clips.Count > 0;

        public bool Has(int classIndex, ClipKind kind) =>
            Enabled && m_clips.Get(m_settings.TargetLanguage, classIndex, kind) != null;

        public bool Play(int classIndex, ClipKind kind, Vector3 position) =>
            Enabled && m_turn.Speak(m_clips.Get(m_settings.TargetLanguage, classIndex, kind), position);

        /// <summary>
        /// Like <see cref="Play"/>, for sounds the learner didn't ask for directly (a label tap): never
        /// cuts off the microphone while Say It is listening or checking.
        /// </summary>
        public bool PlayIfFree(int classIndex, ClipKind kind, Vector3 position) =>
            m_turn.State != TurnState.Listening && m_turn.State != TurnState.Checking && Play(classIndex, kind, position);

        /// <summary>A spatial ping from an object, to draw the eye to it. Plays unless Sound is Off.</summary>
        public bool Ping(Vector3 position) => m_settings.Sound != SoundMode.Off && m_turn.Cue(AudioCues.Ping, position);

        public void Stop() => m_turn.Stop();
    }
}
