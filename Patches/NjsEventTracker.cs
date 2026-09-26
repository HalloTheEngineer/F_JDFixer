using System;
using HarmonyLib;
using JDFixer.Core;

namespace JDFixer.Patches
{
    /// <summary>
    /// Optional: keeps the reaction-time setpoint accurate on maps that change note jump speed part way
    /// through.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A map's NJS is not necessarily constant. <c>NoteJumpSpeedEventData</c> entries in the chart ramp it
    /// up and down, and <c>VariableMovementDataProvider.ManualUpdate</c> re-derives the half-jump
    /// duration from the live speed on every change:
    /// <code>
    /// float num = Mathf.Min(_noteJumpMovementSpeed / _initNoteJumpMovementSpeed, 1f);
    /// _halfJumpDuration = _initOneBeatDuration * _halfJumpDurationInBeats / num;
    /// _jumpDistance     = _noteJumpMovementSpeed * _jumpDuration;
    /// </code>
    /// Note the <c>Min</c> with 1: an NJS event can only make notes arrive sooner, never later.
    /// </para>
    /// <para>
    /// Working through the algebra: when NJS <em>rises</em>, <c>num</c> is pinned to 1, the duration is
    /// unchanged and the distance grows. When NJS <em>falls</em>, the duration stretches by exactly
    /// <c>1/num</c> while the distance stays exactly the same - which is the "NJS cheesing" that silently
    /// hands the player extra time.
    /// </para>
    /// <para>
    /// So the offset JDFixer writes already yields a jump distance that is invariant under a downward
    /// ramp: a player who configured a <em>distance</em> gets precisely what they asked for and is left
    /// alone. A player who configured a <em>reaction time</em> does not - the distance holds but the
    /// duration grows, so their reaction time drifts upward through the ramp. This corrects only that
    /// case, by scaling the half-jump duration the provider uses.
    /// </para>
    /// <para>
    /// Note that the provider does <em>not</em> retain the beat offset: <c>Init</c> folds it straight into
    /// <c>_halfJumpDurationInBeats</c> and discards it. The compensation therefore scales that field
    /// rather than trying to write an offset back.
    /// </para>
    /// </remarks>
    internal static class NjsEventCompensation
    {
        private static bool _armed;
        private static float _bpm;
        private static float _initialNjs;

        /// <summary>The half jump in beats the setpoint asked for, captured at map start.</summary>
        private static float _initialBeats;

        /// <summary>
        /// Records the resolved setpoint so later NJS changes can be compensated relative to it.
        /// </summary>
        /// <remarks>
        /// Called from the <c>VariableMovementDataProvider.Init</c> patch rather than from a second patch
        /// on the same method, so the value is unambiguously the one JDFixer just resolved instead of
        /// depending on the relative order of two postfixes on one target.
        /// </remarks>
        internal static void Arm(Configuration.PluginConfig config, float bpm, float njs, float beatOffset)
        {
            _armed = config != null
                && config.enabled
                && config.njs_event_compensation
                && IsReactionTimeAuthoritative(config)
                && bpm > 0f
                && !float.IsNaN(beatOffset)
                && !float.IsInfinity(beatOffset);

            if (!_armed)
            {
                return;
            }

            _bpm = bpm;
            _initialNjs = njs;

            // Derive the beats from the offset we just wrote, rather than reading the provider's field:
            // this is exactly the duration the user asked for, independent of the game's own arithmetic.
            _initialBeats = HalfJumpDuration.ComputeBeats(bpm, njs, beatOffset);

            Plugin.Log.Debug(
                $"NJS event compensation armed: {beatOffset:0.####} offset = {_initialBeats:0.###} beats " +
                $"at BPM {bpm:0.##}, NJS {njs:0.##}");
        }

        /// <summary>Disables the compensation, e.g. on leaving gameplay.</summary>
        internal static void Disarm() => _armed = false;

        /// <summary>
        /// The half-jump duration in beats that keeps the reaction time constant at the current NJS.
        /// </summary>
        /// <param name="initialNjs">Provider's <c>_initNoteJumpMovementSpeed</c>.</param>
        /// <param name="currentNjs">Provider's <c>_noteJumpMovementSpeed</c>.</param>
        /// <param name="wasUpdatedThisFrame">
        /// The provider's own flag for "the speed actually moved", so this does no work on ordinary frames.
        /// </param>
        /// <returns>The beats to apply, or <c>null</c> when no change is warranted.</returns>
        internal static float? ComputeAdjustedBeats(float initialNjs, float currentNjs, bool wasUpdatedThisFrame)
        {
            if (!_armed || !wasUpdatedThisFrame || initialNjs <= 0f || currentNjs <= 0f)
            {
                return null;
            }

            // Exactly the factor the provider itself applies, including its clamp.
            float njsScale = currentNjs / initialNjs;
            if (njsScale > 1f || njsScale <= 0f)
            {
                // Pinned at 1 when NJS rises: the duration is unchanged, so there is nothing to correct.
                return null;
            }

            float target = _initialBeats * njsScale;

            if (target < JumpDistanceMath.MinHalfJumpDurationInBeats)
            {
                // Already as short as the game allows; scaling further is not possible.
                target = JumpDistanceMath.MinHalfJumpDurationInBeats;
            }

            return Math.Abs(target - _initialBeats) < 0.0001f ? (float?)null : target;
        }

        /// <summary>
        /// Whether the user's current selection makes the reaction time the quantity being held.
        /// </summary>
        private static bool IsReactionTimeAuthoritative(Configuration.PluginConfig config)
        {
            switch ((SongSpeedSetting)config.song_speed_setting)
            {
                case SongSpeedSetting.ReactionTime:
                    return true;

                case SongSpeedSetting.Respectively:
                    return config.use_rt_pref != Configuration.PluginConfig.NoPreference
                        || (config.slider_setting == (int)SliderUnit.ReactionTime
                            && config.use_jd_pref == Configuration.PluginConfig.NoPreference);

                default:
                    return false;
            }
        }
    }

    /// <summary>
    /// Applies the compensation as the map's NJS changes.
    /// </summary>
    /// <remarks>
    /// <c>VariableMovementDataProvider.ManualUpdate</c> is private and is where the game recomputes the
    /// half-jump duration from the live speed, so it is the only place a change can be observed in time.
    /// The provider recomputes <c>_halfJumpDuration</c> from <c>_halfJumpDurationInBeats</c> before this
    /// postfix runs, so the new value takes effect on the following speed change - or, if the speed holds
    /// steady, on the next frame the provider recomputes. Writing the field rather than re-deriving the
    /// duration keeps this from re-entering the provider's own update path.
    /// <para>
    /// <c>wasUpdatedThisFrame</c> is read off the instance rather than declared as a postfix parameter.
    /// It is a public property that <c>ManualUpdate</c> assigns, not one of that method's parameters, and
    /// Harmony matches bare postfix parameter names against the original method's parameters only.
    /// Declaring it inline made Harmony throw <c>Parameter "wasUpdatedThisFrame" not found</c>, which
    /// aborted the whole of <c>PatchAll</c> and left the mod running with no patches applied at all.
    /// </para>
    /// </remarks>
    [HarmonyPatch(typeof(VariableMovementDataProvider), "ManualUpdate")]
    internal static class NjsEventUpdatePatch
    {
        private static readonly AccessTools.FieldRef<VariableMovementDataProvider, float> HalfJumpDurationInBeatsField =
            AccessTools.FieldRefAccess<VariableMovementDataProvider, float>("_halfJumpDurationInBeats");

        private static readonly AccessTools.FieldRef<VariableMovementDataProvider, float> InitNoteJumpMovementSpeedField =
            AccessTools.FieldRefAccess<VariableMovementDataProvider, float>("_initNoteJumpMovementSpeed");

        private static readonly AccessTools.FieldRef<VariableMovementDataProvider, float> NoteJumpMovementSpeedField =
            AccessTools.FieldRefAccess<VariableMovementDataProvider, float>("_noteJumpMovementSpeed");

        private static void Postfix(VariableMovementDataProvider __instance)
        {
            float? adjusted = NjsEventCompensation.ComputeAdjustedBeats(
                InitNoteJumpMovementSpeedField(__instance),
                NoteJumpMovementSpeedField(__instance),
                __instance.wasUpdatedThisFrame);

            if (adjusted == null)
            {
                return;
            }

            HalfJumpDurationInBeatsField(__instance) = adjusted.Value;

            Plugin.Log.Debug(
                $"NJS {NoteJumpMovementSpeedField(__instance):0.##}: half jump scaled to {adjusted.Value:0.###} beats");
        }
    }
}
