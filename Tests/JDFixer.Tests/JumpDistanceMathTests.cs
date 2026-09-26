using JDFixer.Core;
using Xunit;

namespace JDFixer.Tests
{
    /// <summary>
    /// Tests for the jump-distance math. The expected values are derived by hand from the game's own
    /// algorithm, documented in each test, so a change in the game can be checked against them.
    /// </summary>
    public class JumpDistanceMathTests
    {
        /// <summary>
        /// 120 BPM => 0.5 s per beat. 16 NJS, offset 0.
        /// halfjump = 4; 16 * 0.5 * 4 = 32 > 17.999 so halve to 2: 16 * 0.5 * 2 = 16, stop.
        /// jd = 16 * 0.5 * (2 + 0) * 2 = 32.
        /// </summary>
        [Fact]
        public void CalculateJumpDistance_Bpm120_Njs16_OffsetZero_Is32()
        {
            Assert.Equal(32f, JumpDistanceMath.CalculateJumpDistance(120f, 16f, 0f), 4);
        }

        /// <summary>
        /// 120 BPM, 10 NJS: 10 * 0.5 * 4 = 20 > 17.999 so halve to 2: 10 * 0.5 * 2 = 10, stop.
        /// jd = 10 * 0.5 * 2 * 2 = 20.
        /// </summary>
        [Fact]
        public void CalculateJumpDistance_Bpm120_Njs10_OffsetZero_Is20()
        {
            Assert.Equal(20f, JumpDistanceMath.CalculateJumpDistance(120f, 10f, 0f), 4);
        }

        /// <summary>
        /// 100 BPM => 0.6 s/beat, 10 NJS. 10 * 0.6 * 4 = 24 > 17.999, so halve once to 2, where
        /// 10 * 0.6 * 2 = 12 is under the limit, so the loop stops. jd = 10 * 0.6 * 2 * 2 = 24.
        /// </summary>
        [Fact]
        public void CalculateJumpDistance_HalvesOnce_Is24()
        {
            Assert.Equal(24f, JumpDistanceMath.CalculateJumpDistance(100f, 10f, 0f), 4);
        }

        /// <summary>
        /// A case that halves twice: 120 BPM => 0.5 s/beat, 20 NJS (Fast Notes).
        /// 20 * 0.5 * 4 = 40 > 17.999 -> 2 (20, still over) -> 1 (10, stop). jd = 20 * 0.5 * 1 * 2 = 20.
        /// </summary>
        [Fact]
        public void CalculateJumpDistance_HalvesTwice_Is20()
        {
            Assert.Equal(20f, JumpDistanceMath.CalculateJumpDistance(120f, 20f, 0f), 4);
        }

        /// <summary>
        /// A positive offset adds directly to the half jump: 120 BPM, 16 NJS, offset 1.
        /// halfjump = 2 + 1 = 3. jd = 16 * 0.5 * 3 * 2 = 48.
        /// </summary>
        [Fact]
        public void CalculateJumpDistance_PositiveOffset_AddsToHalfJump()
        {
            Assert.Equal(48f, JumpDistanceMath.CalculateJumpDistance(120f, 16f, 1f), 4);
        }

        /// <summary>
        /// The 0.25 beat floor. 120 BPM, 16 NJS: base halfjump is 2, so an offset of -3 puts it at -1,
        /// which clamps to 0.25. jd = 16 * 0.5 * 0.25 * 2 = 4.
        /// </summary>
        [Fact]
        public void CalculateJumpDistance_ClampsToQuarterBeatFloor()
        {
            Assert.Equal(4f, JumpDistanceMath.CalculateJumpDistance(120f, 16f, -3f), 4);
        }

        /// <summary>
        /// A map with a very negative offset gets the same JD as one at exactly the floor, which is what
        /// makes the offset snap step zero and used to hang the snap-point generator.
        /// </summary>
        [Fact]
        public void CalculateJumpDistance_FarNegativeOffset_SameAsFloor()
        {
            Assert.Equal(
                JumpDistanceMath.CalculateJumpDistance(120f, 16f, -3f),
                JumpDistanceMath.CalculateJumpDistance(120f, 16f, -10f),
                4);
        }

        /// <summary>
        /// A non-positive BPM previously produced Infinity: the halving loop could not terminate because
        /// 0 * Infinity is NaN, and the loop condition was never false.
        /// </summary>
        [Theory]
        [InlineData(0f)]
        [InlineData(-120f)]
        [InlineData(float.NaN)]
        public void CalculateJumpDistance_NonPositiveBpm_ReturnsZeroAndTerminates(float bpm)
        {
            Assert.Equal(0f, JumpDistanceMath.CalculateJumpDistance(bpm, 16f, 0f));
        }

        [Theory]
        [InlineData(0f)]
        [InlineData(0.005f)]
        [InlineData(-1f)]
        [InlineData(float.NaN)]
        public void SanitizeNjs_ClampsToFallback(float njs)
        {
            Assert.Equal(JumpDistanceMath.FallbackNoteJumpMovementSpeed, JumpDistanceMath.SanitizeNjs(njs));
        }

        [Fact]
        public void SanitizeNjs_LeavesValidValueAlone()
        {
            Assert.Equal(16f, JumpDistanceMath.SanitizeNjs(16f));
        }

        /// <summary>
        /// The defining relationship the whole mod is built on. Verified across a realistic grid of
        /// BPM / NJS / JD values.
        /// </summary>
        [Theory]
        [InlineData(120f, 16f, 24f)]
        [InlineData(120f, 16f, 32f)]
        [InlineData(100f, 10f, 20f)]
        [InlineData(180f, 20f, 12f)]
        [InlineData(90f, 12f, 40f)]
        [InlineData(140f, 17.3f, 22.5f)]
        public void ReactionTime_AndJumpDistance_AreInverses(float bpm, float njs, float jd)
        {
            float rt = JumpDistanceMath.CalculateReactionTime(jd, njs);
            Assert.Equal(jd, JumpDistanceMath.CalculateJumpDistanceFromReactionTime(rt, njs), 3);
        }

        [Fact]
        public void CalculateReactionTime_ZeroNjs_IsZero()
        {
            Assert.Equal(0f, JumpDistanceMath.CalculateReactionTime(24f, 0f));
        }

        /// <summary>
        /// The solver is the analytic inverse of the calculator, so feeding its output back in must
        /// reproduce the requested jump distance. This is the property the gameplay patch relies on: it
        /// writes the solved beat offset back to the game and expects the game to land on the JD the
        /// user asked for.
        /// </summary>
        [Theory]
        [InlineData(120f, 16f, 24f)]
        [InlineData(120f, 16f, 32f)]
        [InlineData(100f, 10f, 18f)]
        [InlineData(180f, 20f, 15f)]
        [InlineData(90f, 12f, 35f)]
        [InlineData(140f, 17.3f, 22.5f)]
        [InlineData(200f, 8f, 20f)]
        public void SolveBeatOffset_RoundTripsThroughCalculateJumpDistance(float bpm, float njs, float jd)
        {
            float offset = JumpDistanceMath.SolveBeatOffsetForJumpDistance(bpm, njs, jd);
            float actual = JumpDistanceMath.CalculateJumpDistance(bpm, njs, offset);
            Assert.Equal(jd, actual, 2);
        }

        /// <summary>
        /// Solving for a jump distance below what the 0.25 beat floor allows cannot round-trip, because
        /// the game clamps. The solver must still return something finite and non-positive-garbage.
        /// </summary>
        [Fact]
        public void SolveBeatOffset_BelowFloor_ReturnsFiniteValue()
        {
            float offset = JumpDistanceMath.SolveBeatOffsetForJumpDistance(120f, 16f, 0.5f);
            Assert.False(float.IsNaN(offset));
            Assert.False(float.IsInfinity(offset));
        }

        [Theory]
        [InlineData(0f)]
        [InlineData(-1f)]
        [InlineData(float.NaN)]
        public void SolveBeatOffset_InvalidBpm_ReturnsZero(float bpm)
        {
            Assert.Equal(0f, JumpDistanceMath.SolveBeatOffsetForJumpDistance(bpm, 16f, 24f));
        }

        [Theory]
        [InlineData(0f)]
        [InlineData(-1f)]
        [InlineData(float.NaN)]
        public void SolveBeatOffset_NonPositiveJumpDistance_ReturnsZero(float jumpDistance)
        {
            Assert.Equal(0f, JumpDistanceMath.SolveBeatOffsetForJumpDistance(120f, 16f, jumpDistance));
        }

        /// <summary>
        /// NJS is sanitized rather than rejected, because that is what the game itself does: a level
        /// reporting a nonsensical NJS plays at 10 rather than not at all. The solver must agree with the
        /// game, so it clamps the same way.
        /// </summary>
        [Theory]
        [InlineData(0f)]
        [InlineData(-1f)]
        [InlineData(0.005f)]
        public void SolveBeatOffset_ClampsNjsLikeTheGameDoes(float njs)
        {
            float offset = JumpDistanceMath.SolveBeatOffsetForJumpDistance(120f, njs, 24f);
            float actual = JumpDistanceMath.CalculateJumpDistance(120f, njs, offset);
            Assert.Equal(24f, actual, 2);
        }
    }
}
