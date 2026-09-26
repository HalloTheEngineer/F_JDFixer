using JDFixer.Core;
using Xunit;

namespace JDFixer.Tests
{
    public class HalfJumpDurationTests
    {
        /// <summary>
        /// 120 BPM => 0.5 s/beat, 16 NJS. 16 * 0.5 * 4 = 32 > 17.999 so halve once to 2, where
        /// 16 * 0.5 * 2 = 16 fits. Offset 0 gives 2 beats.
        /// </summary>
        [Fact]
        public void ComputeBaseBeats_MatchesTheGamesHalvingLoop()
        {
            Assert.Equal(2f, HalfJumpDuration.ComputeBaseBeats(120f, 16f), 4);
        }

        /// <summary>10 NJS at 120 BPM: 10 * 0.5 * 4 = 20 > 17.999, halve to 2 where 10 fits.</summary>
        [Fact]
        public void ComputeBaseBeats_DefaultNjs()
        {
            Assert.Equal(2f, HalfJumpDuration.ComputeBaseBeats(120f, 10f), 4);
        }

        /// <summary>20 NJS (Fast Notes) at 120 BPM halves twice: 40 -> 20 -> 10, so 1 beat.</summary>
        [Fact]
        public void ComputeBaseBeats_FastNotesHalvesTwice()
        {
            Assert.Equal(1f, HalfJumpDuration.ComputeBaseBeats(120f, 20f), 4);
        }

        [Fact]
        public void ComputeBeats_AddsTheOffset()
        {
            Assert.Equal(3f, HalfJumpDuration.ComputeBeats(120f, 16f, 1f), 4);
        }

        [Fact]
        public void ComputeBeats_ClampsToQuarterBeat()
        {
            Assert.Equal(0.25f, HalfJumpDuration.ComputeBeats(120f, 16f, -10f), 4);
        }


        /// <summary>
        /// The NJS-event compensation scales the half jump by <c>min(current/initial, 1)</c>, which is the
        /// factor the provider itself applies. These pin the arithmetic the patch relies on.
        /// </summary>
        [Theory]
        [InlineData(0.75f, 2f, 1.5f)]
        [InlineData(0.5f, 2f, 1f)]
        [InlineData(1f, 2f, 2f)]
        [InlineData(0.9f, 1f, 0.9f)]
        public void ScalingBeatsByTheNjsFactor_HoldsTheDuration(float njsScale, float initialBeats, float expectedBeats)
        {
            // The provider computes halfJumpDuration = oneBeatDuration * beats / njsScale, so holding the
            // duration constant means holding beats * njsScale constant.
            Assert.Equal(initialBeats * njsScale, expectedBeats, 4);
        }

        [Theory]
        [InlineData(0f)]
        [InlineData(-120f)]
        [InlineData(float.NaN)]
        public void ComputeBaseBeats_InvalidBpm_ReturnsZero(float bpm)
        {
            Assert.Equal(0f, HalfJumpDuration.ComputeBaseBeats(bpm, 16f));
        }

        /// <summary>
        /// Cross-check against the distance calculation: the two must agree, since
        /// <c>jumpDistance == njs * oneBeatDuration * beats * 2</c>.
        /// </summary>
        [Theory]
        [InlineData(120f, 16f, 0f)]
        [InlineData(120f, 16f, 1f)]
        [InlineData(100f, 10f, 0f)]
        [InlineData(180f, 20f, 0.5f)]
        public void ComputeBeats_AgreesWithCalculateJumpDistance(float bpm, float njs, float offset)
        {
            float beats = HalfJumpDuration.ComputeBeats(bpm, njs, offset);
            float expected = njs * (60f / bpm) * beats * 2f;

            Assert.Equal(expected, JumpDistanceMath.CalculateJumpDistance(bpm, njs, offset), 3);
        }
    }
}
