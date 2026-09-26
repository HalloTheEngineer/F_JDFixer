using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JDFixer.Configuration;
using JDFixer.Core;

namespace JDFixer.Patches
{
    /// <summary>
    /// Applies the resolved jump distance to a map as it starts.
    /// </summary>
    /// <remarks>
    /// All of the decision-making lives in <see cref="JumpDistanceResolver"/>, which is pure and unit
    /// tested. This class only reads the values the game hands us and writes back the beat offset.
    /// <para>
    /// This runs once per gameplay-scene start, from <c>BeatmapObjectSpawnController.Start()</c>, not
    /// once per note.
    /// </para>
    /// </remarks>
    [HarmonyPatch(typeof(VariableMovementDataProvider), nameof(VariableMovementDataProvider.Init))]
    internal static class VariableMovementDataProviderPatch
    {
        private static void Prefix(
            ref float noteJumpMovementSpeed,
            float bpm,
            ref BeatmapObjectSpawnMovementData.NoteJumpValueType noteJumpValueType,
            ref float noteJumpValue)
        {
            var config = Configuration.PluginConfig.Instance;
            if (config == null || !config.enabled)
            {
                return;
            }

            // From 1.19.0 the game interprets noteJumpValue as a beat offset. JDFixer only supports that
            // mode, and the base game default is Dynamic, which is what this maps to.
            noteJumpValueType = BeatmapObjectSpawnMovementData.NoteJumpValueType.BeatOffset;

            // A corrupt level can report a non-positive BPM, in which case the offset we would solve for
            // is meaningless. Leave the map's own offset alone rather than overwriting it.
            if (bpm <= 0f || float.IsNaN(bpm) || float.IsInfinity(bpm))
            {
                Plugin.Log.Warn($"Skipping setpoint: map reports an unusable BPM ({bpm})");
                return;
            }

            float mapNjs = JumpDistanceMath.SanitizeNjs(noteJumpMovementSpeed);
            float mapOffset = noteJumpValue;

            ResolvedSetpoint resolved = JumpDistanceResolver.Resolve(BuildRequest(config, bpm, mapNjs, mapOffset));

            noteJumpValue = resolved.BeatOffset;

            // Arm the NJS-event compensation from here rather than from a second patch on the same
            // method: the offset has to be the one just resolved, and depending on Harmony's relative
            // ordering of two postfixes on one target would be fragile.
            NjsEventCompensation.Arm(config, bpm, mapNjs, resolved.BeatOffset);

            Plugin.Log.Debug(
                $"Setpoint {resolved.JumpDistance:0.##} JD " +
                $"({JumpDistanceMath.CalculateReactionTime(resolved.JumpDistance, mapNjs):0} ms RT) " +
                $"from {resolved.Source}" +
                (resolved.UsedThreshold ? " [threshold]" : string.Empty) +
                (resolved.UsedHeuristic ? " [heuristic]" : string.Empty) +
                $" | map: {resolved.OriginalJumpDistance:0.##} JD at NJS {mapNjs:0.##} offset {mapOffset:0.##}" +
                $" | writing offset {resolved.BeatOffset:0.####}");
        }

        private static SetpointRequest BuildRequest(Configuration.PluginConfig config, float bpm, float mapNjs, float mapOffset)
        {
            bool useJdPreferences = config.use_jd_pref != Configuration.PluginConfig.NoPreference;
            bool useRtPreferences = config.use_rt_pref != Configuration.PluginConfig.NoPreference;

            bool reactionTimeSlider = config.slider_setting == (int)SliderUnit.ReactionTime;

            // Slider bounds for the snap-point generator, in whichever unit the user is working in.
            float minSlider = reactionTimeSlider ? config.minReactionTime : config.minJumpDistance;
            float maxSlider = reactionTimeSlider ? config.maxReactionTime : config.maxJumpDistance;

            return new SetpointRequest
            {
                Bpm = bpm,
                MapNjs = mapNjs,
                MapOffset = mapOffset,

                SliderUnit = reactionTimeSlider ? SliderUnit.ReactionTime : SliderUnit.JumpDistance,
                JumpDistance = config.jumpDistance,
                ReactionTime = config.reactionTime,

                UseJdPreferences = useJdPreferences,
                UseRtPreferences = useRtPreferences,
                JdPreferences = useJdPreferences ? BuildTable(config.preferredValues_jd, config.use_jd_pref) : PreferenceTable.Empty,
                RtPreferences = useRtPreferences ? BuildTable(config.preferredValues_rt, config.use_rt_pref) : PreferenceTable.Empty,

                LowerThreshold = config.lower_threshold,
                UpperThreshold = config.upper_threshold,
                UseHeuristic = config.use_heuristic != 0,

                SnapToOffset = config.use_offset && config.legacy_display_enabled,
                OffsetFraction = config.offset_fraction,
                MinSliderValue = minSlider,
                MaxSliderValue = maxSlider,

                SongSpeedSetting = (SongSpeedSetting)config.song_speed_setting,
                SongSpeedMultiplier = BeatmapInfo.SongSpeedMultiplier,
            };
        }

        /// <summary>
        /// Converts the stored preference list for the active index into a sorted lookup table.
        /// </summary>
        /// <remarks>
        /// Index access is bounds-checked. Previously a hand-edited config could produce
        /// <c>use_jd_pref = -2</c> and this threw <see cref="System.IndexOutOfRangeException"/> from
        /// inside a Harmony prefix, on the main thread, as the map started.
        /// </remarks>
        private static PreferenceTable BuildTable<T>(List<List<T>> tables, int index)
            where T : class
        {
            if (tables == null || index < 0 || index >= tables.Count)
            {
                return PreferenceTable.Empty;
            }

            var entries = tables[index];
            if (entries == null || entries.Count == 0)
            {
                return PreferenceTable.Empty;
            }

            var pairs = new List<KeyValuePair<float, float>>(entries.Count);
            foreach (var entry in entries)
            {
                // The two config shapes are distinguished by type, so no extra flag is needed.
                switch (entry)
                {
                    case Configuration.JDPref jd:
                        pairs.Add(new KeyValuePair<float, float>(jd.njs, jd.jumpDistance));
                        break;
                    case Configuration.RTPref rt:
                        pairs.Add(new KeyValuePair<float, float>(rt.njs, rt.reactionTime));
                        break;
                }
            }

            return PreferenceTable.Create(pairs);
        }
    }

    /// <summary>
    /// Captures the song speed multiplier so reaction-time setpoints can be preserved across it.
    /// </summary>
    /// <remarks>
    /// Note that practice mode is a plain <c>Nullable&lt;PracticeSettings&gt;</c> reference type, not a
    /// value type, so <c>?.</c> applies.
    /// </remarks>
    [HarmonyPatch]
    internal static class StandardLevelScenesTransitionSetupDataSOPatch
    {
        // Beat Saber 1.44.1 added an IBeatmapLevelData parameter to Init, so match on the two types the
        // postfix actually consumes. This stays valid if more optional parameters are appended.
        private static MethodBase TargetMethod() => AccessTools.FirstMethod(
            typeof(StandardLevelScenesTransitionSetupDataSO),
            m => m.Name == "Init"
                 && m.GetParameters().Any(p => p.ParameterType == typeof(GameplayModifiers))
                 && m.GetParameters().Any(p => p.ParameterType == typeof(PracticeSettings)));

        private static void Postfix(GameplayModifiers gameplayModifiers, PracticeSettings practiceSettings)
        {
            BeatmapInfo.SongSpeedMultiplier = practiceSettings?.songSpeedMul ?? gameplayModifiers.songSpeedMul;
        }
    }

    [HarmonyPatch(typeof(MultiplayerLevelScenesTransitionSetupDataSO), nameof(MultiplayerLevelScenesTransitionSetupDataSO.Init))]
    internal static class MultiplayerLevelScenesTransitionSetupDataSOPatch
    {
        private static void Postfix(GameplayModifiers gameplayModifiers)
        {
            BeatmapInfo.SongSpeedMultiplier = gameplayModifiers.songSpeedMul;
        }
    }
}
