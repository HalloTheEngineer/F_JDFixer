using JDFixer.Configuration;
using JDFixer.Core;

namespace JDFixer
{
    /// <summary>
    /// Everything JDFixer needs to know about the currently selected map, plus the derived values the
    /// UI displays and the gameplay patch needs.
    /// </summary>
    internal sealed class BeatmapInfo
    {
        /// <summary>
        /// A stand-in used before any map is selected, and for maps that are not downloaded. Carries
        /// safe, finite values so the UI can render without dividing by zero.
        /// </summary>
        internal static BeatmapInfo Empty { get; } = new BeatmapInfo();

        /// <summary>
        /// The song speed multiplier captured when the level transition was set up. Used to preserve a
        /// reaction-time setpoint across song speed changes.
        /// </summary>
        internal static float SongSpeedMultiplier { get; set; } = 1f;

        private BeatmapInfo()
        {
            // Zeroed so that Campaigns, Tournament Assistant and undownloaded maps show 0 rather than
            // the values of the last map selected in Solo.

            JumpDistance = 0f;
            MinJumpDistance = 0f;
            ReactionTime = 0f;
            MinReactionTime = 0f;
            Offset = 0f;

            // Must be non-zero: the UI divides by this to convert between JD and RT. Kept small so a
            // degenerate map cannot produce a meaningful-looking but wrong reaction time.
            NJS = 0.001f;

            MinRTSlider = 0f;
            MaxRTSlider = 3000f;
            MinJDSlider = 0f;
            MaxJDSlider = 50f;

            JdSnapPoints = SnapPointSet.Empty;
            RtSnapPoints = SnapPointSet.Empty;
        }

        /// <param name="level">
        /// The selected level, or <c>null</c> when the map is not downloaded. A null level yields the
        /// same safe values as <see cref="Empty"/>.
        /// </param>
        internal BeatmapInfo(BeatmapKey key, BeatmapLevel level)
            : this()
        {
            if (level == null)
            {
                return;
            }

            float bpm = level.beatsPerMinute;

            float njs = JumpDistanceMath.FallbackNoteJumpMovementSpeed;
            float offset = 0f;
            if (level.beatmapBasicData != null
                && level.beatmapBasicData.TryGetValue((key.beatmapCharacteristic, key.difficulty), out BeatmapBasicData beatmapBasicData))
            {
                njs = beatmapBasicData.noteJumpMovementSpeed;
                offset = beatmapBasicData.noteJumpStartBeatOffset;
            }

            njs = JumpDistanceMath.SanitizeNjs(njs);

            NJS = njs;
            Offset = offset;

            JumpDistance = JumpDistanceMath.CalculateJumpDistance(bpm, njs, offset);
            MinJumpDistance = JumpDistanceMath.CalculateJumpDistance(bpm, njs, -50f);
            ReactionTime = JumpDistanceMath.CalculateReactionTime(JumpDistance, njs);
            MinReactionTime = JumpDistanceMath.CalculateReactionTime(MinJumpDistance, njs);

            SetSliderBounds();
            BuildSnapPoints(bpm, njs, offset);
        }

        /// <summary>
        /// Slider ranges, in whichever unit the user is currently working in. The bounds are the
        /// configured ones, widened by the minimum the game itself allows for this map, so a value can
        /// never be set that the game will refuse.
        /// </summary>
        private void SetSliderBounds()
        {
            var config = Configuration.PluginConfig.Instance;

            if (config.slider_setting == (int)SliderUnit.JumpDistance)
            {
                MinJDSlider = config.minJumpDistance;
                MaxJDSlider = config.maxJumpDistance;
                MinRTSlider = JumpDistanceMath.CalculateReactionTime(config.minJumpDistance, NJS);
                MaxRTSlider = JumpDistanceMath.CalculateReactionTime(config.maxJumpDistance, NJS);
            }
            else
            {
                MinRTSlider = config.minReactionTime;
                MaxRTSlider = config.maxReactionTime;
                MinJDSlider = JumpDistanceMath.CalculateJumpDistanceFromReactionTime(config.minReactionTime, NJS);
                MaxJDSlider = JumpDistanceMath.CalculateJumpDistanceFromReactionTime(config.maxReactionTime, NJS);
            }
        }

        /// <summary>
        /// Precomputes the beat-offset snap positions for this map.
        /// </summary>
        /// <remarks>
        /// These are instance state rather than the mutable statics this used to use. The gameplay patch
        /// previously read <c>BeatmapOffsets.jd_snap_value</c>, which is <c>0f</c> until the legacy
        /// settings tab has been rendered at least once in the session; enabling offset snapping and then
        /// launching straight into a map played it at roughly zero jump distance.
        /// <para>
        /// The gameplay patch no longer reads them at all - it derives its own set from the map's BPM,
        /// NJS and offset - but the UI needs the labels, and building them here keeps one code path.
        /// </para>
        /// </remarks>
        private void BuildSnapPoints(float bpm, float njs, float offset)
        {
            var config = Configuration.PluginConfig.Instance;
            float fraction = config.offset_fraction;

            float jdStep = SnapPointCalculator.CalculateUnitStep(bpm, njs, offset, fraction);
            float rtStep = jdStep * JumpDistanceMath.ReactionTimeDivisor / njs;

            JdSnapPoints = SnapPointCalculator.Create(JumpDistance, offset, jdStep, MinJDSlider, MaxJDSlider, fraction);
            RtSnapPoints = SnapPointCalculator.Create(ReactionTime, offset, rtStep, MinRTSlider, MaxRTSlider, fraction);
        }

        public float JumpDistance { get; private set; }
        public float MinJumpDistance { get; private set; }
        public float NJS { get; private set; }
        public float ReactionTime { get; private set; }
        public float MinReactionTime { get; private set; }

        public float Offset { get; private set; }

        internal float MinRTSlider { get; private set; }
        internal float MaxRTSlider { get; private set; }
        internal float MinJDSlider { get; private set; }
        internal float MaxJDSlider { get; private set; }

        internal SnapPointSet JdSnapPoints { get; private set; }
        internal SnapPointSet RtSnapPoints { get; private set; }
    }
}
