using System;
using JDFixer.Core;
using Xunit;

namespace JDFixer.Tests
{
    public class SnapPointCalculatorTests
    {
        /// <summary>
        /// 120 BPM, 16 NJS, offset 0. baseHalfJump is 2 (see JumpDistanceMathTests), so one 1/8 step
        /// gives halfjump 2.125 and jd = 16 * 0.5 * 2.125 * 2 = 34. The step is therefore 2.
        /// </summary>
        private const float StepFor120Bpm16Njs = 2f;

        /// <summary>The slider bounds the mod ships with.</summary>
        private const float MinSlider = 12f;
        private const float MaxSlider = 35f;

        [Fact]
        public void Create_GeneratesPointsBothWaysAroundTheBase()
        {
            var set = SnapPointCalculator.Create(32f, 0f, StepFor120Bpm16Njs, MinSlider, MaxSlider, 8f);

            Assert.Equal(32f, set.BaseValue);

            // 32 +/- 2 clipped to [12, 35]: 12, 14, ... 30, 32, 34
            Assert.Equal(12f, set[0], 3);
            Assert.Equal(34f, set[set.Count - 1], 3);
            Assert.Equal(32f, set.Nearest(32f).Value, 3);
        }

        [Fact]
        public void Create_ProducesStrictlyAscendingPoints()
        {
            var set = SnapPointCalculator.Create(32f, 0f, StepFor120Bpm16Njs, MinSlider, MaxSlider, 8f);

            for (int i = 1; i < set.Count; i++)
            {
                Assert.True(set[i] > set[i - 1], $"points must ascend: index {i}");
            }
        }

        [Fact]
        public void Create_ClipsToTheSliderBounds()
        {
            var set = SnapPointCalculator.Create(32f, 0f, StepFor120Bpm16Njs, MinSlider, MaxSlider, 8f);

            Assert.True(set[0] >= MinSlider, "must not generate a point below the slider minimum");
            Assert.True(set[set.Count - 1] <= MaxSlider, "must not generate a point above the slider maximum");
        }

        /// <summary>
        /// The bug that motivated rewriting this: for any map whose offset is at or below -3.75 beats,
        /// both the un-shifted and the shifted offset clamp to the game's 0.25 beat floor, so the step
        /// is exactly zero. The old implementation looped <c>while (point &lt;= max) { point += 0; }</c>,
        /// growing an unbounded list until the game froze.
        /// </summary>
        [Theory]
        [InlineData(-3.75f)]
        [InlineData(-4f)]
        [InlineData(-10f)]
        [InlineData(-50f)]
        public void Create_ZeroStep_DoesNotHang(float offset)
        {
            float step = SnapPointCalculator.CalculateUnitStep(120f, 16f, offset, 8f);
            Assert.Equal(0f, step, 6);

            var set = SnapPointCalculator.Create(20f, offset, step, 0f, 100f, 8f);

            Assert.Same(SnapPointSet.Empty, set);
        }

        [Theory]
        [InlineData(0f)]
        [InlineData(-1f)]
        [InlineData(float.NaN)]
        [InlineData(float.PositiveInfinity)]
        public void Create_InvalidStep_ReturnsEmpty(float step)
        {
            var set = SnapPointCalculator.Create(32f, 0f, step, 0f, 100f, 8f);
            Assert.Same(SnapPointSet.Empty, set);
        }

        [Theory]
        [InlineData(0f)]
        [InlineData(-4f)]
        [InlineData(float.NaN)]
        public void Create_InvalidOffsetFraction_ReturnsEmpty(float fraction)
        {
            var set = SnapPointCalculator.Create(32f, 0f, StepFor120Bpm16Njs, 0f, 100f, fraction);
            Assert.Same(SnapPointSet.Empty, set);
        }

        /// <summary>
        /// A step wider than the whole slider range cannot produce a second point, so there is nothing
        /// to snap to and we return the empty set rather than a one-element set.
        /// </summary>
        [Fact]
        public void Create_SingleReachableValue_ReturnsEmpty()
        {
            var set = SnapPointCalculator.Create(50f, 0f, 1000f, 0f, 100f, 8f);
            Assert.Same(SnapPointSet.Empty, set);
        }

        /// <summary>
        /// The empty set must return its input unchanged. The old code read mutable static fields that
        /// defaulted to 0f, so a player who enabled offset snapping and launched straight into a map
        /// without opening the settings tab got a jump distance of roughly zero.
        /// </summary>
        [Fact]
        public void EmptySet_Nearest_ReturnsInputUnchanged()
        {
            var snapped = SnapPointSet.Empty.Nearest(24f);

            Assert.Equal(24f, snapped.Value, 4);
            Assert.Equal(string.Empty, snapped.OffsetLabel);
        }

        [Fact]
        public void Nearest_ReturnsFirstPointAtOrAboveInput()
        {
            var set = SnapPointCalculator.Create(32f, 0f, StepFor120Bpm16Njs, MinSlider, MaxSlider, 8f);

            Assert.Equal(32f, set.Nearest(31f).Value, 3);
            Assert.Equal(32f, set.Nearest(32f).Value, 3);
            Assert.Equal(34f, set.Nearest(32.1f).Value, 3);
        }

        [Fact]
        public void Nearest_AboveRange_ClampsToLastPoint()
        {
            var set = SnapPointCalculator.Create(32f, 0f, StepFor120Bpm16Njs, MinSlider, 40f, 8f);

            Assert.Equal(40f, set.Nearest(1000f).Value, 3);
        }

        [Fact]
        public void Nearest_BelowRange_ClampsToFirstPoint()
        {
            var set = SnapPointCalculator.Create(32f, 0f, StepFor120Bpm16Njs, 20f, MaxSlider, 8f);

            Assert.Equal(20f, set.Nearest(-1000f).Value, 3);
        }

        /// <summary>
        /// Every generated point must be a jump distance the game can actually produce, which is what
        /// makes snapping meaningful. Verified by feeding the labelled beat offset back through the
        /// game's own calculation.
        /// </summary>
        [Fact]
        public void GeneratedPoints_AreReachableByTheGame()
        {
            const float bpm = 120f;
            const float njs = 16f;
            const float offset = 0f;
            const int fraction = 8;

            float step = SnapPointCalculator.CalculateUnitStep(bpm, njs, offset, fraction);
            float baseJd = JumpDistanceMath.CalculateJumpDistance(bpm, njs, offset);
            var set = SnapPointCalculator.Create(baseJd, offset, step, 12f, 40f, fraction);

            // Every point in the set must be a jump distance the game can produce from some reachable
            // beat offset. Walk the offsets outward from the base and check the values line up.
            int baseIndex = -1;
            for (int i = 0; i < set.Count; i++)
            {
                if (Math.Abs(set[i] - baseJd) < 0.001f)
                {
                    baseIndex = i;
                    break;
                }
            }

            Assert.True(baseIndex >= 0, "the base value must be present in the set");

            int span = Math.Min(baseIndex, set.Count - 1 - baseIndex);
            for (int k = 0; k <= span; k++)
            {
                float up = JumpDistanceMath.CalculateJumpDistance(bpm, njs, offset + k / (float)fraction);
                Assert.Equal(set[baseIndex + k], up, 3);

                float down = JumpDistanceMath.CalculateJumpDistance(bpm, njs, offset - k / (float)fraction);
                Assert.Equal(set[baseIndex - k], down, 3);
            }
        }

        [Fact]
        public void OffsetLabel_ShowsTheBeatOffsetRequired()
        {
            var set = SnapPointCalculator.Create(32f, 0f, StepFor120Bpm16Njs, MinSlider, MaxSlider, 8f);

            Assert.Equal("( 0, 0 )", set.Nearest(32f).OffsetLabel);
            Assert.Equal("( 0.125, 0.13 )", set.Nearest(34f).OffsetLabel);
            Assert.Contains("-0.125", set.Nearest(30f).OffsetLabel);
        }
    }
}
