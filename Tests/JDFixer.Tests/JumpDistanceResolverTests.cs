using System.Collections.Generic;
using JDFixer.Core;
using Xunit;

namespace JDFixer.Tests
{
    public class JumpDistanceResolverTests
    {
        /// <summary>A 120 BPM, 16 NJS, offset 0 map: 32 JD, 500 ms RT.</summary>
        private const float Bpm = 120f;
        private const float Njs = 16f;
        private const float Offset = 0f;
        private const float OriginalJd = 32f;

        private static SetpointRequest BaseRequest => new SetpointRequest
        {
            Bpm = Bpm,
            MapNjs = Njs,
            MapOffset = Offset,
            SliderUnit = SliderUnit.JumpDistance,
            JumpDistance = 24f,
            ReactionTime = 750f,
            UseJdPreferences = false,
            UseRtPreferences = false,
            JdPreferences = PreferenceTable.Empty,
            RtPreferences = PreferenceTable.Empty,
            LowerThreshold = 1f,
            UpperThreshold = 100f,
            UseHeuristic = false,
            SnapToOffset = false,
            OffsetFraction = 8f,
            MinSliderValue = 12f,
            MaxSliderValue = 35f,
            SongSpeedSetting = SongSpeedSetting.JumpDistance,
            SongSpeedMultiplier = 1f,
        };

        private static PreferenceTable Table(params (float njs, float value)[] entries)
        {
            var pairs = new List<KeyValuePair<float, float>>();
            foreach (var (njs, value) in entries)
            {
                pairs.Add(new KeyValuePair<float, float>(njs, value));
            }

            return PreferenceTable.Create(pairs);
        }

        [Fact]
        public void NoPreferences_UsesTheSliderValue()
        {
            var result = JumpDistanceResolver.Resolve(BaseRequest);

            Assert.Equal(24f, result.JumpDistance, 3);
            Assert.Equal(SetpointSource.Slider, result.Source);
            Assert.Equal(OriginalJd, result.OriginalJumpDistance, 3);
        }

        /// <summary>
        /// The headline property: the beat offset handed back to the game must make the game play at the
        /// resolved jump distance. This is what the gameplay patch writes into <c>noteJumpValue</c>.
        /// </summary>
        [Theory]
        [InlineData(12f)]
        [InlineData(24f)]
        [InlineData(35f)]
        [InlineData(50f)]
        public void BeatOffset_RoundTripsToTheResolvedJumpDistance(float desired)
        {
            var request = BaseRequest with { JumpDistance = desired };
            var result = JumpDistanceResolver.Resolve(request);

            float actual = JumpDistanceMath.CalculateJumpDistance(Bpm, Njs, result.BeatOffset);
            Assert.Equal(desired, actual, 2);
        }

        [Fact]
        public void ReactionTimeSlider_ConvertsToJumpDistance()
        {
            // 750 ms at 16 NJS => 750 * 2 * 16 / 500 = 48 JD.
            var result = JumpDistanceResolver.Resolve(BaseRequest with { SliderUnit = SliderUnit.ReactionTime });

            Assert.Equal(48f, result.JumpDistance, 3);
        }

        [Fact]
        public void JdPreference_OverridesTheSlider()
        {
            var request = BaseRequest with
            {
                UseJdPreferences = true,
                JdPreferences = Table((16f, 28f)),
            };

            var result = JumpDistanceResolver.Resolve(request);

            Assert.Equal(28f, result.JumpDistance, 3);
            Assert.Equal(SetpointSource.Preference, result.Source);
        }

        [Fact]
        public void RtPreference_IsConvertedUsingTheMapsNjs()
        {
            // 500 ms at 16 NJS => 500 * 2 * 16 / 500 = 32 JD.
            var request = BaseRequest with
            {
                UseRtPreferences = true,
                RtPreferences = Table((16f, 500f)),
            };

            var result = JumpDistanceResolver.Resolve(request);

            Assert.Equal(32f, result.JumpDistance, 3);
            Assert.Equal(SetpointSource.Preference, result.Source);
        }

        [Fact]
        public void PreferenceWithNoMatchingEntry_KeepsTheSliderValue()
        {
            var request = BaseRequest with
            {
                UseJdPreferences = true,
                JdPreferences = Table((20f, 28f)),
            };

            var result = JumpDistanceResolver.Resolve(request);

            Assert.Equal(24f, result.JumpDistance, 3);
        }

        [Theory]
        [InlineData(0.5f)]
        [InlineData(1f)]
        [InlineData(100f)]
        [InlineData(150f)]
        public void OutsideThresholds_FallsBackToTheMapsOwnJumpDistance(float mapNjs)
        {
            var request = BaseRequest with
            {
                MapNjs = mapNjs,
                UseJdPreferences = true,
                JdPreferences = Table((16f, 28f)),
                UseHeuristic = true,
            };

            var result = JumpDistanceResolver.Resolve(request);

            float expectedOriginal = JumpDistanceMath.CalculateJumpDistance(Bpm, mapNjs, Offset);
            Assert.Equal(expectedOriginal, result.JumpDistance, 3);
            Assert.True(result.UsedThreshold);
        }

        /// <summary>
        /// The heuristic must not run when the threshold already decided the outcome; the user asked for
        /// the map's own value in that case, not for it to be re-derived.
        /// </summary>
        [Fact]
        public void ThresholdFallback_DoesNotAlsoReportHeuristic()
        {
            var request = BaseRequest with
            {
                MapNjs = 0.5f,
                UseJdPreferences = true,
                JdPreferences = Table((16f, 28f)),
                UseHeuristic = true,
            };

            var result = JumpDistanceResolver.Resolve(request);

            Assert.True(result.UsedThreshold);
            Assert.False(result.UsedHeuristic);
        }

        /// <summary>
        /// The heuristic only ever lowers the setpoint. A map authored at 32 JD with a preference of
        /// 20 is *more* floaty than the user wants, so the preference is applied untouched.
        /// </summary>
        [Fact]
        public void Heuristic_DoesNotFireWhenTheMapIsMoreFloatyThanThePreference()
        {
            var request = BaseRequest with
            {
                UseJdPreferences = true,
                JdPreferences = Table((16f, 20f)),
                UseHeuristic = true,
            };

            var result = JumpDistanceResolver.Resolve(request);

            Assert.Equal(20f, result.JumpDistance, 3);
            Assert.False(result.UsedHeuristic);
        }

        /// <summary>
        /// The case the heuristic exists for: the map is authored *tighter* than the user's setpoint, so
        /// forcing the setpoint would make it worse. This is the normal case for a floaty-map player, who
        /// sets a generous JD and expects easy maps to keep theirs.
        /// </summary>
        [Fact]
        public void Heuristic_FiresWhenTheMapIsTighterThanThePreference()
        {
            var request = BaseRequest with
            {
                UseJdPreferences = true,
                JdPreferences = Table((16f, 40f)),
            };

            var withoutHeuristic = JumpDistanceResolver.Resolve(request);
            Assert.Equal(40f, withoutHeuristic.JumpDistance, 3);
            Assert.False(withoutHeuristic.UsedHeuristic);

            var withHeuristic = JumpDistanceResolver.Resolve(request with { UseHeuristic = true });
            Assert.Equal(32f, withHeuristic.JumpDistance, 3);
            Assert.True(withHeuristic.UsedHeuristic);
            Assert.Equal(SetpointSource.MapOriginal, withHeuristic.Source);
        }

        /// <summary>The map's authored jump distance for the fixture: 120 BPM, 16 NJS, offset 0.</summary>
        [Fact]
        public void FixtureMapJumpDistance_Is32()
        {
            Assert.Equal(32f, JumpDistanceResolver.Resolve(BaseRequest).OriginalJumpDistance, 3);
        }

        /// <summary>
        /// Offset snapping is ignored when an automated preference is active, because a preference is a
        /// more specific instruction.
        /// </summary>
        [Fact]
        public void SnapToOffset_IsIgnoredWhenAPreferenceIsActive()
        {
            var withSnapOnly = JumpDistanceResolver.Resolve(BaseRequest with { SnapToOffset = true });
            Assert.Equal(SetpointSource.OffsetSnap, withSnapOnly.Source);

            var withPreference = JumpDistanceResolver.Resolve(BaseRequest with
            {
                SnapToOffset = true,
                UseJdPreferences = true,
                JdPreferences = Table((16f, 28f)),
            });

            Assert.Equal(SetpointSource.Preference, withPreference.Source);
            Assert.Equal(28f, withPreference.JumpDistance, 3);
        }

        /// <summary>
        /// A map whose offset is deep enough that the game's 0.25 beat floor clamps every reachable
        /// value must fall back to the slider setpoint, not to zero. The old code read
        /// <c>BeatmapOffsets.jd_snap_value</c>, a static that is 0f until the settings tab has been
        /// rendered at least once in the session.
        /// </summary>
        [Fact]
        public void SnapToOffset_UnsnappableMap_KeepsTheSliderValue()
        {
            var request = BaseRequest with
            {
                MapOffset = -10f,
                SnapToOffset = true,
            };

            var result = JumpDistanceResolver.Resolve(request);

            Assert.True(result.JumpDistance > 0f, "must not resolve to a zero jump distance");
            Assert.Equal(24f, result.JumpDistance, 3);
        }

        [Fact]
        public void SnapToOffset_RoundsToAReachableValue()
        {
            // 32 JD base with a step of 2: 33 must snap down to a reachable 32 or up to 34.
            var request = BaseRequest with { JumpDistance = 33f, SnapToOffset = true };

            var result = JumpDistanceResolver.Resolve(request);

            Assert.Equal(SetpointSource.OffsetSnap, result.Source);
            Assert.Contains(result.JumpDistance, new[] { 32f, 34f });
        }

        [Fact]
        public void SongSpeed_JumpDistanceSetting_LeavesTheValueAlone()
        {
            var request = BaseRequest with
            {
                SongSpeedSetting = SongSpeedSetting.JumpDistance,
                SongSpeedMultiplier = 1.2f,
            };

            Assert.Equal(24f, JumpDistanceResolver.Resolve(request).JumpDistance, 3);
        }

        /// <summary>
        /// The point of the song speed setting: the user configures 500 ms, plays at 1.2x speed, and
        /// still gets 500 ms of reaction time - which means a larger jump distance.
        /// </summary>
        [Fact]
        public void SongSpeed_ReactionTimeSetting_PreservesReactionTime()
        {
            var atNormalSpeed = JumpDistanceResolver.Resolve(BaseRequest with { JumpDistance = 32f });
            Assert.Equal(500f, JumpDistanceMath.CalculateReactionTime(atNormalSpeed.JumpDistance, Njs), 2);

            var atFastSpeed = JumpDistanceResolver.Resolve(BaseRequest with
            {
                JumpDistance = 32f,
                SongSpeedSetting = SongSpeedSetting.ReactionTime,
                SongSpeedMultiplier = 1.2f,
            });

            Assert.Equal(600f, JumpDistanceMath.CalculateReactionTime(atFastSpeed.JumpDistance, Njs), 1);
        }

        [Fact]
        public void SongSpeed_Respectively_FollowsTheActiveSlider()
        {
            // JD slider with no JD preference: JD is authoritative, so nothing changes.
            var jdSlider = JumpDistanceResolver.Resolve(BaseRequest with
            {
                SliderUnit = SliderUnit.JumpDistance,
                SongSpeedSetting = SongSpeedSetting.Respectively,
                SongSpeedMultiplier = 1.2f,
            });
            Assert.Equal(24f, jdSlider.JumpDistance, 3);

            // RT slider with no preference: RT is authoritative.
            var rtSlider = JumpDistanceResolver.Resolve(BaseRequest with
            {
                SliderUnit = SliderUnit.ReactionTime,
                SongSpeedSetting = SongSpeedSetting.Respectively,
                SongSpeedMultiplier = 1.2f,
            });
            float rt = JumpDistanceMath.CalculateReactionTime(rtSlider.JumpDistance, Njs);
            Assert.Equal(750f * 1.2f, rt, 0);
        }

        [Fact]
        public void Resolve_HandlesAMapWithNonsensicalNjs()
        {
            var request = BaseRequest with { MapNjs = 0f };

            var result = JumpDistanceResolver.Resolve(request);

            Assert.Equal(JumpDistanceMath.FallbackNoteJumpMovementSpeed, result.MapNjs, 4);
            Assert.False(float.IsNaN(result.JumpDistance));
            Assert.False(float.IsInfinity(result.BeatOffset));
        }

        /// <summary>
        /// A corrupt BPM cannot produce a meaningful beat offset, so the solver returns 0. The user's
        /// setpoint itself does not depend on BPM, so it survives. The gameplay patch additionally
        /// leaves the map's offset untouched for a non-positive BPM rather than overwriting it with 0.
        /// </summary>
        [Fact]
        public void Resolve_HandlesAMapWithNonsensicalBpm()
        {
            var result = JumpDistanceResolver.Resolve(BaseRequest with { Bpm = 0f });

            Assert.Equal(24f, result.JumpDistance, 3);
            Assert.Equal(0f, result.BeatOffset, 4);
        }
    }
}
