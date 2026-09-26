using JDFixer.Core;
using Xunit;

namespace JDFixer.Tests
{
    public class RemoteTextBlockTests
    {
        [Fact]
        public void TryExtract_ReadsTheMarkedRegion()
        {
            const string source = "intro\n[JDFIXER]\nHello there\n###\ntail";

            Assert.True(RemoteTextBlock.TryExtract(source, out string block));
            Assert.Equal("\nHello there\n", block);
        }

        /// <summary>
        /// The regression: the old code called IndexOf(terminator, markerIndex) before checking whether
        /// the marker was found, and IndexOf(string, -1) throws. This is a guaranteed throw for any
        /// response lacking the marker, which includes a GitHub 404 page or a truncated download.
        /// </summary>
        [Theory]
        [InlineData("")]
        [InlineData("no marker here at all")]
        [InlineData("<html><body>404: Not Found</body></html>")]
        [InlineData("###")]
        [InlineData("[OTHERMOD]\ntext\n###")]
        [InlineData(null)]
        public void TryExtract_MissingMarker_ReturnsFalseAndDoesNotThrow(string source)
        {
            Assert.False(RemoteTextBlock.TryExtract(source, out string block));
            Assert.Equal(string.Empty, block);
        }

        /// <summary>Marker present but no terminator: also used to produce a negative Substring length.</summary>
        [Fact]
        public void TryExtract_MissingTerminator_ReturnsFalse()
        {
            Assert.False(RemoteTextBlock.TryExtract("intro [JDFIXER] dangling", out string block));
            Assert.Equal(string.Empty, block);
        }

        [Fact]
        public void TryExtract_EmptyRegion_ReturnsFalse()
        {
            Assert.False(RemoteTextBlock.TryExtract("[JDFIXER]###", out _));
        }

        [Fact]
        public void TryExtract_UsesTheFirstRegionOnly()
        {
            const string source = "[JDFIXER]first###[JDFIXER]second###";

            Assert.True(RemoteTextBlock.TryExtract(source, out string block));
            Assert.Equal("first", block);
        }

        /// <summary>Ordinal comparison: the marker is ASCII and must not be case-folded or localised.</summary>
        [Fact]
        public void TryExtract_IsCaseSensitive()
        {
            Assert.False(RemoteTextBlock.TryExtract("[jdfixer]text###", out _));
        }
    }
}
