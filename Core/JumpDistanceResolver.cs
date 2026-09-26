using System;

namespace JDFixer.Core
{
    /// <summary>
    /// Which quantity the user is currently adjusting, and therefore which is authoritative.
    /// </summary>
    /// <remarks>
    /// Named <c>SliderUnit</c> rather than <c>SliderSetting</c> to avoid colliding with BSML's own
    /// <c>BeatSaberMarkupLanguage.Components.Settings.SliderSetting</c> component type.
    /// </remarks>
    internal enum SliderUnit
    {
        JumpDistance = 0,
        ReactionTime = 1,
    }

    /// <summary>What determines the authoritative quantity for a given map.</summary>
    internal enum SongSpeedSetting
    {
        /// <summary>The JD setpoint is authoritative; song speed does not change it.</summary>
        JumpDistance = 0,

        /// <summary>The RT setpoint is authoritative and is preserved across song speed changes.</summary>
        ReactionTime = 1,

        /// <summary>Whichever of the two the user is currently working in is authoritative.</summary>
        Respectively = 2,
    }

    /// <summary>Why a given setpoint was chosen. Surfaced in the log and used by the UI.</summary>
    internal enum SetpointSource
    {
        /// <summary>The user's slider / preference-less setpoint.</summary>
        Slider = 0,

        /// <summary>Snapped to a reachable beat-offset fraction.</summary>
        OffsetSnap = 1,

        /// <summary>Matched an automated NJS-keyed preference.</summary>
        Preference = 2,

        /// <summary>The map's own value was kept, because of a threshold or the heuristic.</summary>
        MapOriginal = 3,
    }

    /// <summary>Everything the resolver needs. Deliberately a value type with no behaviour.</summary>
    internal readonly struct SetpointRequest
    {
        internal float Bpm { get; init; }
        internal float MapNjs { get; init; }
        internal float MapOffset { get; init; }

        internal SliderUnit SliderUnit { get; init; }
        internal float JumpDistance { get; init; }
        internal float ReactionTime { get; init; }

        internal bool UseJdPreferences { get; init; }
        internal bool UseRtPreferences { get; init; }
        internal PreferenceTable JdPreferences { get; init; }
        internal PreferenceTable RtPreferences { get; init; }

        internal float LowerThreshold { get; init; }
        internal float UpperThreshold { get; init; }
        internal bool UseHeuristic { get; init; }

        internal bool SnapToOffset { get; init; }
        internal float OffsetFraction { get; init; }
        internal float MinSliderValue { get; init; }
        internal float MaxSliderValue { get; init; }

        internal SongSpeedSetting SongSpeedSetting { get; init; }
        internal float SongSpeedMultiplier { get; init; }

        /// <summary>
        /// Whether <paramref name="njs"/> falls outside the configured range, in which case automated
        /// preferences are bypassed and the map plays at its authored jump distance. Both bounds are
        /// inclusive, matching the original behaviour.
        /// </summary>
        internal bool IsOutsideThresholds(float njs) => njs <= LowerThreshold || njs >= UpperThreshold;
    }

    /// <summary>The outcome of resolving a setpoint for one map.</summary>
    internal readonly struct ResolvedSetpoint
    {
        internal float JumpDistance { get; init; }

        /// <summary>The beat offset to hand back to the game as <c>noteJumpValue</c>.</summary>
        internal float BeatOffset { get; init; }

        /// <summary>The jump distance the map would have had without JDFixer.</summary>
        internal float OriginalJumpDistance { get; init; }

        internal float MapNjs { get; init; }
        internal SetpointSource Source { get; init; }
        internal bool UsedThreshold { get; init; }
        internal bool UsedHeuristic { get; init; }

        /// <summary>True when the setpoint came from the slider rather than a preference or the map.</summary>
        internal bool IsFromSlider => Source == SetpointSource.Slider;
    }

    /// <summary>
    /// Decides the jump distance a map should play at.
    /// </summary>
    /// <remarks>
    /// This was previously inlined in a Harmony prefix using <c>goto</c> to share a single exit. It is
    /// pure and side-effect free so that it can be unit tested; see <c>Tests/JDFixer.Tests</c>.
    /// </remarks>
    internal static class JumpDistanceResolver
    {
        /// <summary>Resolves the setpoint and converts it back into a beat offset for the game.</summary>
        internal static ResolvedSetpoint Resolve(in SetpointRequest request)
        {
            float bpm = request.Bpm;
            float njs = JumpDistanceMath.SanitizeNjs(request.MapNjs);
            float originalJumpDistance = JumpDistanceMath.CalculateJumpDistance(bpm, njs, request.MapOffset);

            var result = new ResolvedSetpoint
            {
                OriginalJumpDistance = originalJumpDistance,
                MapNjs = njs,
                Source = SetpointSource.Slider,
                JumpDistance = FromSlider(request, njs),
            };

            // Offset snapping only applies when no automated preference is active; a preference is a
            // more specific instruction than a snap.
            if (request.SnapToOffset && !request.UseRtPreferences && !request.UseJdPreferences)
            {
                result = result with
                {
                    JumpDistance = FromOffsetSnap(request, njs),
                    Source = SetpointSource.OffsetSnap,
                };
            }

            if (request.UseRtPreferences)
            {
                result = ApplyPreference(request, njs, originalJumpDistance, request.RtPreferences, isReactionTime: true, result);
            }
            else if (request.UseJdPreferences)
            {
                result = ApplyPreference(request, njs, originalJumpDistance, request.JdPreferences, isReactionTime: false, result);
            }

            float compensated = ApplySongSpeedCompensation(result.JumpDistance, njs, request);
            float beatOffset = JumpDistanceMath.SolveBeatOffsetForJumpDistance(bpm, njs, compensated);

            return result with { JumpDistance = compensated, BeatOffset = beatOffset };
        }

        private static float FromSlider(in SetpointRequest request, float njs)
        {
            return request.SliderUnit == SliderUnit.ReactionTime
                ? JumpDistanceMath.CalculateJumpDistanceFromReactionTime(request.ReactionTime, njs)
                : request.JumpDistance;
        }

        private static float FromOffsetSnap(in SetpointRequest request, float njs)
        {
            var snapPoints = BuildSnapPoints(request, njs);
            var snapped = snapPoints.Nearest(request.SliderUnit == SliderUnit.ReactionTime
                ? request.ReactionTime
                : request.JumpDistance);

            return request.SliderUnit == SliderUnit.ReactionTime
                ? JumpDistanceMath.CalculateJumpDistanceFromReactionTime(snapped.Value, njs)
                : snapped.Value;
        }

        private static SnapPointSet BuildSnapPoints(in SetpointRequest request, float njs)
        {
            // The set is generated around the *map's* value, not the user's. The reachable values are a
            // property of the map's beat offset; the user's slider value is merely the input being
            // snapped. Generating around the slider would make every slider position reachable, which
            // defeats the purpose.
            float mapJumpDistance = JumpDistanceMath.CalculateJumpDistance(request.Bpm, njs, request.MapOffset);
            float jdStep = SnapPointCalculator.CalculateUnitStep(request.Bpm, njs, request.MapOffset, request.OffsetFraction);

            if (request.SliderUnit == SliderUnit.ReactionTime)
            {
                float mapReactionTime = JumpDistanceMath.CalculateReactionTime(mapJumpDistance, njs);
                float rtStep = jdStep * JumpDistanceMath.ReactionTimeDivisor / njs;

                return SnapPointCalculator.Create(
                    mapReactionTime,
                    request.MapOffset,
                    rtStep,
                    request.MinSliderValue,
                    request.MaxSliderValue,
                    request.OffsetFraction);
            }

            return SnapPointCalculator.Create(
                mapJumpDistance,
                request.MapOffset,
                jdStep,
                request.MinSliderValue,
                request.MaxSliderValue,
                request.OffsetFraction);
        }

        private static ResolvedSetpoint ApplyPreference(
            in SetpointRequest request,
            float njs,
            float originalJumpDistance,
            PreferenceTable table,
            bool isReactionTime,
            ResolvedSetpoint result)
        {
            if (request.IsOutsideThresholds(njs))
            {
                // Outside the configured NJS range the preference does not apply, and the heuristic
                // deliberately does not run either: the user asked for the map's own value here.
                return result with
                {
                    JumpDistance = originalJumpDistance,
                    Source = SetpointSource.MapOriginal,
                    UsedThreshold = true,
                };
            }

            if (table != null && table.TryLookup(njs, out float configured, out bool hasMatch) && hasMatch)
            {
                result = result with
                {
                    JumpDistance = isReactionTime
                        ? JumpDistanceMath.CalculateJumpDistanceFromReactionTime(configured, njs)
                        : configured,
                    Source = SetpointSource.Preference,
                };
            }
            // No matching entry: the map's NJS is below every configured entry. Keep the slider
            // setpoint, which is the previous behaviour.

            // Heuristic: if the map's authored jump distance is lower than the setpoint, play it at the
            // map's value. The mod exists to fix floaty maps, and a mapper who chose something lower is
            // usually right.
            if (request.UseHeuristic && originalJumpDistance <= result.JumpDistance)
            {
                result = result with
                {
                    JumpDistance = originalJumpDistance,
                    Source = SetpointSource.MapOriginal,
                    UsedHeuristic = true,
                };
            }

            return result;
        }

        private static float ApplySongSpeedCompensation(float jumpDistance, float njs, in SetpointRequest request)
        {
            if (request.SongSpeedSetting == SongSpeedSetting.JumpDistance)
            {
                return jumpDistance;
            }

            bool convert = request.SongSpeedSetting == SongSpeedSetting.ReactionTime
                || (request.SongSpeedSetting == SongSpeedSetting.Respectively
                    && (request.UseRtPreferences
                        || (request.SliderUnit == SliderUnit.ReactionTime && !request.UseJdPreferences)));

            if (!convert)
            {
                return jumpDistance;
            }

            float reactionTime = JumpDistanceMath.CalculateReactionTime(jumpDistance, njs) * request.SongSpeedMultiplier;
            return JumpDistanceMath.CalculateJumpDistanceFromReactionTime(reactionTime, njs);
        }
    }
}
