using System.Collections.Generic;
using JDFixer.Core;
using Xunit;

namespace JDFixer.Tests
{
    public class PreferenceTableTests
    {
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
        public void TryLookup_ReturnsLargestConfiguredNjsNotExceedingTheMap()
        {
            var table = Table((10f, 10f), (16f, 16f), (20f, 20f));

            Assert.True(table.TryLookup(17f, out float value, out bool hasMatch));
            Assert.True(hasMatch);
            Assert.Equal(16f, value, 4);
        }

        /// <summary>
        /// Order independence. The old lookup was a linear scan over a list that was only sorted as a
        /// side effect of the user opening the Preferences screen, so an ascending stored list silently
        /// returned the 12 NJS entry for a 17 NJS map.
        /// </summary>
        [Fact]
        public void TryLookup_IsIndependentOfInputOrder()
        {
            var ascending = Table((12f, 12f), (16f, 16f), (20f, 20f));
            var descending = Table((20f, 20f), (16f, 16f), (12f, 12f));

            ascending.TryLookup(17f, out float a, out _);
            descending.TryLookup(17f, out float d, out _);

            Assert.Equal(a, d);
            Assert.Equal(16f, a, 4);
        }

        [Fact]
        public void TryLookup_ExactMatchIsUsed()
        {
            var table = Table((10f, 10f), (16f, 16f));

            Assert.True(table.TryLookup(16f, out float value, out _));
            Assert.Equal(16f, value, 4);
        }

        [Fact]
        public void TryLookup_BelowEveryEntry_ReportsNoMatch()
        {
            var table = Table((16f, 16f), (20f, 20f));

            Assert.False(table.TryLookup(10f, out _, out bool hasMatch));
            Assert.False(hasMatch);
        }

        [Fact]
        public void EmptyTable_NeverMatches()
        {
            Assert.False(PreferenceTable.Empty.TryLookup(16f, out _, out bool hasMatch));
            Assert.False(hasMatch);
        }

        [Fact]
        public void Create_Null_ReturnsEmpty()
        {
            Assert.Same(PreferenceTable.Empty, PreferenceTable.Create(null));
        }

        /// <summary>Non-finite entries are dropped rather than poisoning the search.</summary>
        [Fact]
        public void Create_DropsNonFiniteEntries()
        {
            var table = Table((float.NaN, 10f), (16f, 16f), (20f, float.PositiveInfinity));

            Assert.Equal(1, table.Count);
            Assert.True(table.TryLookup(17f, out float value, out _));
            Assert.Equal(16f, value, 4);
        }

        /// <summary>
        /// Duplicate NJS entries are ambiguous input, so the winner is defined rather than left to the
        /// sort: the last one added wins, because a user re-adding a row is correcting it. List.Sort is
        /// not stable, so without an explicit tiebreaker this was implementation-defined.
        /// </summary>
        [Fact]
        public void Create_DuplicateNjs_LastAddedWins_Deterministically()
        {
            for (int i = 0; i < 20; i++)
            {
                var table = Table((16f, 1f), (16f, 2f), (16f, 3f));
                table.TryLookup(16f, out float value, out _);
                Assert.Equal(3f, value, 4);
            }
        }
    }
}
