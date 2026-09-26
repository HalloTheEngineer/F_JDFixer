using JDFixer.Core;
using Xunit;

namespace JDFixer.Tests
{
    public class SliderRangeTests
    {
        private const int AbsMin = 2;
        private const int AbsMax = 60;

        /// <summary>
        /// The property both sliders and the gameplay patch depend on. If this ever fails, the mod tab
        /// can offer a range the config says is invalid.
        /// </summary>
        [Theory]
        [InlineData(AbsMin, 0)]
        [InlineData(0, 0)]
        [InlineData(-5, 7)]
        [InlineData(30, 29)]
        [InlineData(30, 30)]
        [InlineData(59, 58)]
        [InlineData(AbsMax, 0)]
        [InlineData(20, 20)]
        [InlineData(3, 4)]
        [InlineData(int.MaxValue, 0)]
        [InlineData(int.MinValue, 0)]
        [InlineData(100, 12)]
        public void Repaired_always_yields_an_ordered_range_inside_the_limits(int min, int max)
        {
            SliderRange range = SliderRange.Repaired(min, max, AbsMin, AbsMax);

            Assert.True(range.Min < range.Max, $"{range.Min} should be below {range.Max}");
            Assert.InRange(range.Min, AbsMin, AbsMax);
            Assert.InRange(range.Max, AbsMin, AbsMax);
        }

        [Fact]
        public void Repaired_leaves_a_valid_range_untouched()
        {
            SliderRange range = SliderRange.Repaired(12, 35, AbsMin, AbsMax);

            Assert.Equal(12, range.Min);
            Assert.Equal(35, range.Max);
        }

        [Fact]
        public void Repaired_swaps_rather_than_resets_when_the_ends_are_crossed()
        {
            // The case that used to reset both ends to 12/35 and lose the user's numbers.
            SliderRange range = SliderRange.Repaired(35, 12, AbsMin, AbsMax);

            Assert.Equal(12, range.Min);
            Assert.Equal(35, range.Max);
        }

        [Theory]
        [InlineData(20)]
        [InlineData(-100)]
        [InlineData(500)]
        [InlineData(int.MaxValue)]
        public void WithMin_keeps_the_range_ordered_whatever_is_requested(int requested)
        {
            SliderRange range = new SliderRange(12, 35).WithMin(requested, AbsMin, AbsMax);

            Assert.True(range.Min < range.Max, $"{range.Min} should be below {range.Max}");
            Assert.InRange(range.Min, AbsMin, AbsMax);
            Assert.InRange(range.Max, AbsMin, AbsMax);
        }

        [Theory]
        [InlineData(20)]
        [InlineData(-100)]
        [InlineData(500)]
        [InlineData(int.MinValue)]
        public void WithMax_keeps_the_range_ordered_whatever_is_requested(int requested)
        {
            SliderRange range = new SliderRange(12, 35).WithMax(requested, AbsMin, AbsMax);

            Assert.True(range.Min < range.Max, $"{range.Min} should be below {range.Max}");
            Assert.InRange(range.Min, AbsMin, AbsMax);
            Assert.InRange(range.Max, AbsMin, AbsMax);
        }

        [Fact]
        public void WithMin_within_the_current_range_moves_only_the_minimum()
        {
            SliderRange range = new SliderRange(12, 35).WithMin(20, AbsMin, AbsMax);

            Assert.Equal(20, range.Min);
            Assert.Equal(35, range.Max);
        }

        [Fact]
        public void WithMax_within_the_current_range_moves_only_the_maximum()
        {
            SliderRange range = new SliderRange(12, 35).WithMax(50, AbsMin, AbsMax);

            Assert.Equal(12, range.Min);
            Assert.Equal(50, range.Max);
        }

        [Fact]
        public void WithMin_past_the_maximum_lifts_the_maximum_rather_than_refusing()
        {
            SliderRange range = new SliderRange(12, 35).WithMin(40, AbsMin, AbsMax);

            Assert.Equal(40, range.Min);
            Assert.Equal(41, range.Max);
        }

        [Fact]
        public void WithMax_past_the_minimum_drops_the_minimum_rather_than_refusing()
        {
            SliderRange range = new SliderRange(12, 35).WithMax(8, AbsMin, AbsMax);

            Assert.Equal(7, range.Min);
            Assert.Equal(8, range.Max);
        }

        [Fact]
        public void WithMin_cannot_reach_the_absolute_maximum_because_the_maximum_needs_a_value_above_it()
        {
            SliderRange range = new SliderRange(12, 35).WithMin(AbsMax, AbsMin, AbsMax);

            Assert.Equal(AbsMax - 1, range.Min);
            Assert.Equal(AbsMax, range.Max);
        }

        [Fact]
        public void WithMax_cannot_reach_the_absolute_minimum_because_the_minimum_needs_a_value_below_it()
        {
            SliderRange range = new SliderRange(12, 35).WithMax(AbsMin, AbsMin, AbsMax);

            Assert.Equal(AbsMin, range.Min);
            Assert.Equal(AbsMin + 1, range.Max);
        }

        [Fact]
        public void The_reaction_time_limits_behave_the_same_way()
        {
            SliderRange range = SliderRange.Repaired(1600, 300, 50, 4000);

            Assert.Equal(300, range.Min);
            Assert.Equal(1600, range.Max);
        }

        /// <summary>
        /// Dragging an end repeatedly must never walk the other end somewhere the user did not ask for.
        /// </summary>
        [Fact]
        public void Repeated_drags_converge_instead_of_drifting()
        {
            var range = new SliderRange(12, 35);

            for (int i = 0; i < 200; i++)
            {
                range = range.WithMin(i, AbsMin, AbsMax);
                range = range.WithMax(AbsMax - i, AbsMin, AbsMax);

                Assert.InRange(range.Min, AbsMin, AbsMax);
                Assert.InRange(range.Max, AbsMin, AbsMax);
            }

            Assert.True(range.Min < range.Max);
        }
    }
}
