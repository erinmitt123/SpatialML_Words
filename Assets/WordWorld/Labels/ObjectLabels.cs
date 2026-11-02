using System;
using System.Collections.Generic;
using ByteDance.PICO.SpatialAdapter.Component;
using UnityEngine;
using WordWorld.App;
using WordWorld.Data;
using WordWorld.Spatial;
using WordWorld.Tracking;

namespace WordWorld.Labels
{
    /// <summary>
    /// Object labels and their states (DESIGN.md section 3): scanning dots on unconfirmed objects,
    /// fade-in on confirm, 40% when the object is out of sight, dimmed while another is selected,
    /// dots only in quiz mode. At most eight labels within 4 m, nearest first, stacked so they never
    /// overlap. Nothing is shown until a language is chosen.
    ///
    /// Tracked positions and distances jitter by centimetres every frame, so labels ignore small
    /// target changes, glide to new ones, and only resize in coarse steps.
    /// </summary>
    [AddComponentMenu("Word World/Object Labels")]
    public sealed class ObjectLabels : MonoBehaviour
    {
        private const int PersonClassId = 0;

        [SerializeField] private TrackingManager m_tracking;
        [SerializeField] private LearnerSettings m_settings;
        [SerializeField] private Vocabulary m_vocabulary;
        [SerializeField] private LabelView[] m_pool = Array.Empty<LabelView>();
        [Tooltip("Pulsing dots on objects seen but not yet confirmed.")]
        [SerializeField] private SpatialAdapterNativeText[] m_scanDots = Array.Empty<SpatialAdapterNativeText>();

        [Header("Placement (DESIGN.md section 3)")]
        [Tooltip("Card centre height above the hit point; raised further for tall objects so it never covers them.")]
        [SerializeField] private float m_heightAboveHit = 0.15f;
        [Tooltip("Font size in points for a label 1 m away (90 pt is about 2 cm cap height).")]
        [SerializeField] private float m_pointsAtOneMetre = 90f;
        [Tooltip("Labels keep a constant angular size between these distances (2 to 7 cm text).")]
        [SerializeField] private float m_minSizeDistance = 0.8f;
        [SerializeField] private float m_maxSizeDistance = 3.3f;
        [SerializeField] private int m_maxLabels = 8;
        [SerializeField] private float m_maxDistance = 4f;

        [Header("States")]
        [SerializeField] private float m_fadeSeconds = 0.25f;
        [Tooltip("Unseen this long counts as lost (40% opacity).")]
        [SerializeField] private float m_lostAfterSeconds = 1.5f;
        [Tooltip("Lost labels fade out after this long.")]
        [SerializeField] private float m_lostFadeAfterSeconds = 10f;
        [SerializeField, Range(0f, 1f)] private float m_lostOpacity = 0.4f;
        [SerializeField, Range(0f, 1f)] private float m_dimmedOpacity = 0.6f;
        [Tooltip("Frames out of the last eight an unconfirmed object needs before it gets a scanning dot.")]
        [SerializeField, Range(1, 8)] private int m_scanDotMinHits = 2;

        [Header("Steadiness")]
        [Tooltip("Target moves smaller than this (plus 1.5% of distance) are ignored.")]
        [SerializeField] private float m_deadband = 0.03f;
        [SerializeField] private float m_glideSeconds = 0.35f;
        [Tooltip("Resize only when the ideal size differs from the current one by this fraction.")]
        [SerializeField, Range(0f, 1f)] private float m_resizeHysteresis = 0.25f;

        private readonly Dictionary<TrackedObject, ActiveLabel> m_active = new Dictionary<TrackedObject, ActiveLabel>();
        private readonly Dictionary<LabelView, TrackedObject> m_byView = new Dictionary<LabelView, TrackedObject>();
        private readonly Stack<LabelView> m_free = new Stack<LabelView>();
        private readonly List<ActiveLabel> m_ranked = new List<ActiveLabel>(32);
        private readonly List<Placement> m_placements = new List<Placement>(32);
        private readonly HashSet<TrackedObject> m_hiddenForSession = new HashSet<TrackedObject>();
        private TrackedObject m_selected;
        private bool m_quizMode;

        /// <summary>Quiz mode: raised when the learner touches or pinches a tracked object.</summary>
        public event Action<TrackedObject> ObjectPicked;
        /// <summary>Raised when a label is tapped.</summary>
        public event Action<TrackedObject, LabelView> LabelTapped;
        /// <summary>Raised the first time any label becomes visible this session.</summary>
        public event Action<TrackedObject, LabelView> FirstLabelShown;
        private bool m_anyShown;

        private sealed class ActiveLabel
        {
            public TrackedObject Track;
            public LabelView View;
            public Vocabulary.LabelContent Content;
            public bool HasContent;
            public Vector3 Goal;
            public Vector3 Shown;
            public Vector3 Velocity;
            public bool Placed;
            public int Points;
            public float Distance;
            public float Opacity;
            public float ShownSince = -1f;
            public bool Visible;
            public int Percent = -1;
            // Timed effects (unscaled time): word shown during the quiz, shake, attention pulse.
            public float FlashUntil;
            public float ShakeUntil;
            public float PulseUntil;
        }

        private struct Placement
        {
            public ActiveLabel Label;
            public Vector3 Position;
            public float Height;
        }

        public bool QuizMode
        {
            get => m_quizMode;
            set => m_quizMode = value;
        }

        private bool Ready => m_settings == null || m_settings.SessionReady;

        private void Awake()
        {
            for (int i = m_pool.Length - 1; i >= 0; i--)
            {
                if (m_pool[i] == null) continue;
                m_pool[i].gameObject.SetActive(false);
                m_pool[i].Tapped += OnViewTapped;
                m_free.Push(m_pool[i]);
            }
            foreach (SpatialAdapterNativeText dot in m_scanDots) dot.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            if (m_tracking == null) return;
            m_tracking.ObjectConfirmed += Show;
            m_tracking.ObjectRemoved += Hide;
            if (m_settings != null) m_settings.Changed += OnSettingsChanged;
        }

        private void OnDisable()
        {
            if (m_tracking == null) return;
            m_tracking.ObjectConfirmed -= Show;
            m_tracking.ObjectRemoved -= Hide;
            if (m_settings != null) m_settings.Changed -= OnSettingsChanged;
        }

        /// <summary>The selected label stays at full opacity; the others dim (detail card open).</summary>
        public void SetSelected(TrackedObject track) => m_selected = track;

        /// <summary>"Not right?" on the detail card hides that label for the rest of the session.</summary>
        public void HideForSession(TrackedObject track)
        {
            m_hiddenForSession.Add(track);
            Hide(track);
        }

        public bool TryGetLabel(TrackedObject track, out LabelView view)
        {
            view = null;
            if (track == null || !m_active.TryGetValue(track, out ActiveLabel label)) return false;
            view = label.View;
            return true;
        }

        /// <summary>The nearest labelled object of a class, if one is tracked.</summary>
        public bool TryFindByClass(int classId, out TrackedObject track)
        {
            track = null;
            float best = float.MaxValue;
            HeadPose.TryGet(out Pose head);
            foreach (ActiveLabel label in m_active.Values)
            {
                if (label.Track.ClassId != classId) continue;
                float d = Vector3.Distance(head.position, label.Track.Position);
                if (d < best)
                {
                    best = d;
                    track = label.Track;
                }
            }
            return track != null;
        }

        /// <summary>Labelled objects confirmed this session, for quiz prompts.</summary>
        public void GetLabelled(List<TrackedObject> into)
        {
            into.Clear();
            foreach (ActiveLabel label in m_active.Values) into.Add(label.Track);
        }

        /// <summary>Quiz feedback: show the word for a moment even though labels are hidden.</summary>
        public void Flash(TrackedObject track, float seconds)
        {
            if (track != null && m_active.TryGetValue(track, out ActiveLabel label)) label.FlashUntil = Time.unscaledTime + seconds;
        }

        /// <summary>Quiz feedback for a wrong pick: a brief sideways shake.</summary>
        public void Shake(TrackedObject track)
        {
            if (track != null && m_active.TryGetValue(track, out ActiveLabel label)) label.ShakeUntil = Time.unscaledTime + 0.4f;
        }

        /// <summary>Word Bank: the dot pulses and the label shows at full strength.</summary>
        public void Pulse(TrackedObject track, float seconds)
        {
            if (track != null && m_active.TryGetValue(track, out ActiveLabel label)) label.PulseUntil = Time.unscaledTime + seconds;
        }

        public bool TryGetContent(TrackedObject track, out Vocabulary.LabelContent content)
        {
            content = default;
            if (track == null || !m_active.TryGetValue(track, out ActiveLabel label) || !label.HasContent) return false;
            content = label.Content;
            return true;
        }

        private void OnViewTapped(LabelView view)
        {
            if (!m_byView.TryGetValue(view, out TrackedObject track)) return;
            if (m_quizMode) ObjectPicked?.Invoke(track);
            else LabelTapped?.Invoke(track, view);
        }

        // Objects confirmed while the picker was open get their labels now; existing labels re-text.
        private void OnSettingsChanged()
        {
            foreach (ActiveLabel label in m_active.Values) Retext(label);
            for (int i = 0; i < m_tracking.Confirmed.Count; i++) Show(m_tracking.Confirmed[i]);
            // Label people may have been turned off.
            m_ranked.Clear();
            foreach (ActiveLabel label in m_active.Values)
                if (!Labelable(label.Track)) m_ranked.Add(label);
            foreach (ActiveLabel label in m_ranked) Hide(label.Track);
            m_ranked.Clear();
        }

        private bool Labelable(TrackedObject track)
        {
            if (m_hiddenForSession.Contains(track)) return false;
            if (m_vocabulary != null && !m_vocabulary.IsEnabled(track.ClassId)) return false;
            // DESIGN.md section 12: no labels on people unless the learner turns it on.
            return track.ClassId != PersonClassId || (m_settings != null && m_settings.LabelPeople);
        }

        // Looked up only on show and on settings changes; per frame only the confidence changes.
        private void Retext(ActiveLabel label)
        {
            label.HasContent = m_settings != null && m_vocabulary != null &&
                m_vocabulary.TryGetLabel(label.Track.ClassId, m_settings.TargetLanguage, m_settings.ReadingLevel,
                    m_settings.ShowPinyin, out label.Content);
            if (!label.HasContent) label.Content = new Vocabulary.LabelContent { Main = label.Track.ClassName };
            if (m_settings != null && !m_settings.ShowMeaning) label.Content.Meaning = null;
            label.Percent = -1;
            ApplyText(label);
        }

        // DESIGN.md section 12: confidence numbers only in debug builds.
        private static void ApplyText(ActiveLabel label)
        {
            Vocabulary.LabelContent c = label.Content;
            if (Debug.isDebugBuild)
            {
                int percent = Mathf.Clamp(Mathf.RoundToInt(label.Track.Confidence * 20f) * 5, 0, 100);
                if (percent == label.Percent) return;
                label.Percent = percent;
                label.View.SetContent(c.Main, c.Ruby, c.MainIsPinyin, c.Meaning, PercentSuffix(percent), "  100%");
            }
            else label.View.SetContent(c.Main, c.Ruby, c.MainIsPinyin, c.Meaning);
        }

        private static readonly string[] s_percentSuffixes = BuildPercentSuffixes();
        private static string[] BuildPercentSuffixes()
        {
            var suffixes = new string[21];
            for (int i = 0; i <= 20; i++) suffixes[i] = $"  {i * 5}%";
            return suffixes;
        }
        private static string PercentSuffix(int percent) => s_percentSuffixes[percent / 5];

        private void Show(TrackedObject track)
        {
            if (!Ready || m_active.ContainsKey(track) || !Labelable(track)) return;
            if (m_free.Count == 0)
            {
                Debug.LogWarning($"[WordWorld.Labels] label pool exhausted ({m_pool.Length}); '{track.ClassName}' not shown");
                return;
            }
            LabelView view = m_free.Pop();
            var label = new ActiveLabel { Track = track, View = view };
            m_active.Add(track, label);
            m_byView[view] = track;
            Retext(label);
        }

        private void Hide(TrackedObject track)
        {
            if (!m_active.TryGetValue(track, out ActiveLabel label)) return;
            label.View.gameObject.SetActive(false);
            m_active.Remove(track);
            m_byView.Remove(label.View);
            m_free.Push(label.View);
            if (m_selected == track) m_selected = null;
        }

        private void LateUpdate()
        {
            if (!HeadPose.TryGet(out Pose head)) return;
            UpdateScanDots(head);
            if (m_active.Count == 0) return;

            float now = Time.unscaledTime;
            float dt = Time.deltaTime;
            float sizeScale = m_settings != null ? m_settings.LabelScale : 1f;

            // Nearest eight within range are eligible; the rest fade out.
            m_ranked.Clear();
            foreach (ActiveLabel label in m_active.Values)
            {
                label.Distance = Vector3.Distance(head.position, label.Track.Position);
                m_ranked.Add(label);
            }
            m_ranked.Sort((a, b) => a.Distance.CompareTo(b.Distance));

            m_placements.Clear();
            for (int i = 0; i < m_ranked.Count; i++)
            {
                ActiveLabel label = m_ranked[i];
                TrackedObject track = label.Track;
                // In the quiz every nearby dot stays, so any object can be picked.
                bool eligible = m_quizMode ? label.Distance <= m_maxDistance * 1.5f
                    : i < m_maxLabels && label.Distance <= m_maxDistance;
                float unseen = now - track.LastSeenTime;
                float target = !eligible ? 0f
                    : unseen > m_lostAfterSeconds + m_lostFadeAfterSeconds ? 0f
                    : unseen > m_lostAfterSeconds ? m_lostOpacity
                    : 1f;
                if (m_selected != null && m_selected != track) target *= m_dimmedOpacity;
                if (now < label.PulseUntil || now < label.FlashUntil) target = 1f;

                // Fade in (and out) over a quarter second.
                label.Opacity = Mathf.MoveTowards(label.Opacity, target, dt / m_fadeSeconds);
                bool visible = label.Opacity > 0.01f || target > 0f;
                if (visible != label.Visible)
                {
                    label.Visible = visible;
                    label.View.gameObject.SetActive(visible);
                    if (visible)
                    {
                        label.ShownSince = now;
                        if (!m_anyShown)
                        {
                            m_anyShown = true;
                            FirstLabelShown?.Invoke(track, label.View);
                        }
                    }
                }
                if (!visible) continue;

                label.View.SetTextVisible(!m_quizMode || now < label.FlashUntil);
                label.View.SetOpacity(label.Opacity);
                label.View.SetObjectBounds(m_quizMode ? new Vector3(
                    Mathf.Max(0.12f, track.Width), Mathf.Max(0.12f, track.Height), Mathf.Max(0.12f, track.Width)) : Vector3.zero);
                // Confirmed: scales from 90% to 100% as it fades in; pulses while highlighted.
                float appear = Mathf.Clamp01((now - label.ShownSince) / m_fadeSeconds);
                float scale = 0.9f + 0.1f * (1f - (1f - appear) * (1f - appear));
                if (now < label.PulseUntil) scale *= 1f + 0.08f * Mathf.Sin(now * 9f);
                label.View.transform.localScale = new Vector3(scale, scale, scale);

                Vector3 hit = track.Position;
                Vector3 anchor = hit + Vector3.up * Mathf.Max(m_heightAboveHit, track.Height * 0.5f + 0.06f);
                float distance = Vector3.Distance(head.position, anchor);
                // Constant angular size keeps far labels readable without near ones looming, but only
                // in coarse steps: every resize is visible and is a native text update.
                float ideal = m_pointsAtOneMetre * sizeScale * Mathf.Clamp(distance, m_minSizeDistance, m_maxSizeDistance);
                if (label.Points == 0 || Mathf.Abs(ideal - label.Points) > label.Points * m_resizeHysteresis)
                    label.Points = Mathf.RoundToInt(ideal / 10f) * 10;
                label.View.SetPoints(label.Points);
                if (Debug.isDebugBuild) ApplyText(label);
                m_placements.Add(new Placement { Label = label, Position = anchor, Height = label.View.HeightMetres });
            }

            // Nearest first, so the closest object keeps the bottom spot and farther ones stack above.
            for (int i = 0; i < m_placements.Count; i++)
            {
                Placement p = m_placements[i];
                for (int j = 0; j < i; j++)
                {
                    Placement q = m_placements[j];
                    float minGap = (p.Height + q.Height) * 0.5f + 0.01f;
                    if (Overlaps(head.position, p.Position, q.Position, minGap) && p.Position.y < q.Position.y + minGap)
                        p.Position.y = q.Position.y + minGap;
                }
                m_placements[i] = p;

                ActiveLabel label = p.Label;
                if (!label.Placed)
                {
                    label.Goal = label.Shown = p.Position;
                    label.Velocity = Vector3.zero;
                    label.Placed = true;
                }
                else if (Vector3.Distance(label.Goal, p.Position) > m_deadband + 0.015f * label.Distance)
                {
                    label.Goal = p.Position;
                }
                label.Shown = Vector3.SmoothDamp(label.Shown, label.Goal, ref label.Velocity, m_glideSeconds, Mathf.Infinity, dt);
                Quaternion facing = Billboard(head.position, label.Shown);
                Vector3 shake = now < label.ShakeUntil ? facing * Vector3.right * (0.012f * Mathf.Sin(now * 60f)) : Vector3.zero;
                label.View.transform.SetPositionAndRotation(label.Shown + shake, facing);
                // The leader ends on the object even while the card is stacked above others.
                label.View.SetLeaderLength((label.Shown.y - label.Track.Position.y) / label.View.transform.localScale.y);
            }
            LogPlacements(head);
        }

        // DESIGN.md: rotate on Y only so text never tilts. Like Unity UI, the text faces -Z.
        public static Quaternion Billboard(Vector3 eye, Vector3 position)
        {
            Vector3 toUser = eye - position;
            toUser.y = 0f;
            if (toUser.sqrMagnitude < 1e-4f) toUser = Vector3.back;
            return Quaternion.LookRotation(-toUser.normalized, Vector3.up);
        }

        // "Scanning": a small pulsing dot on objects seen but not yet confirmed, so the user knows the
        // app has noticed something.
        private void UpdateScanDots(Pose head)
        {
            if (m_scanDots.Length == 0) return;
            int used = 0;
            bool show = Ready && !m_quizMode && m_tracking != null && !m_tracking.Paused;
            if (show)
            {
                IReadOnlyList<TrackedObject> tracks = m_tracking.Tracks;
                float pulse = 1f + 0.35f * Mathf.Sin(Time.unscaledTime * 5f);
                for (int i = 0; i < tracks.Count && used < m_scanDots.Length; i++)
                {
                    TrackedObject track = tracks[i];
                    if (track.Confirmed || CountBits(track.HitHistory) < m_scanDotMinHits || !Labelable(track)) continue;
                    SpatialAdapterNativeText dot = m_scanDots[used++];
                    if (!dot.gameObject.activeSelf) dot.gameObject.SetActive(true);
                    dot.transform.SetPositionAndRotation(track.Position, Billboard(head.position, track.Position));
                    dot.transform.localScale = new Vector3(pulse, pulse, 1f);
                }
            }
            for (int i = used; i < m_scanDots.Length; i++)
                if (m_scanDots[i].gameObject.activeSelf) m_scanDots[i].gameObject.SetActive(false);
        }

        private static int CountBits(uint value)
        {
            int count = 0;
            while (value != 0)
            {
                value &= value - 1;
                count++;
            }
            return count;
        }

        // Spatial mode cannot be screen-captured, so placement is checked from logcat.
        private float m_nextLogTime;

        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void LogPlacements(Pose head)
        {
            if (Time.unscaledTime < m_nextLogTime) return;
            m_nextLogTime = Time.unscaledTime + 5f;
            var b = new System.Text.StringBuilder("[WordWorld.Labels] head ");
            b.Append(head.position.ToString("F2"));
            foreach (ActiveLabel label in m_active.Values)
                b.Append(" | ").Append(label.Track.ClassName).Append(" '").Append(label.View.CurrentText).Append("' ")
                    .Append(label.Track.Position.ToString("F2")).Append(' ').Append(label.Points).Append("pt a")
                    .Append(label.Opacity.ToString("F1"));
            Debug.Log(b.ToString());
        }

        // Two labels collide when they are roughly in the same viewing direction from the user.
        private static bool Overlaps(Vector3 eye, Vector3 a, Vector3 b, float minGap)
        {
            Vector3 da = a - eye;
            Vector3 db = b - eye;
            da.y = 0f;
            db.y = 0f;
            float distance = Mathf.Max(0.3f, Mathf.Min(da.magnitude, db.magnitude));
            float lateral = Vector3.Angle(da, db) * Mathf.Deg2Rad * distance;
            return lateral < minGap * 4f && Mathf.Abs(a.y - b.y) < minGap;
        }
    }
}
