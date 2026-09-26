using System;

namespace JDFixer.Core
{
    /// <summary>
    /// Extracts a marked block from a remote text document.
    /// </summary>
    /// <remarks>
    /// The remote documents are plain text files with a <c>[JDFIXER] ... ###</c> region, the convention
    /// the author uses to keep per-mod text inside shared files.
    /// <para>
    /// This used to be inlined in the donate loader as <c>IndexOf(marker)</c> followed by
    /// <c>IndexOf(terminator, start)</c>, with the "was the marker found" check written <em>after</em> the
    /// second call. <c>String.IndexOf(string, int)</c> throws when <c>startIndex</c> is negative, so any
    /// response without the marker - a truncated download, a GitHub 404 page, an HTML error page - threw
    /// <see cref="ArgumentOutOfRangeException"/>. Because the fetch was fire-and-forget the exception was
    /// swallowed, and because the "loaded" flag had already been set the banner could never load again for
    /// the rest of the session.
    /// </para>
    /// </remarks>
    internal static class RemoteTextBlock
    {
        internal const string Marker = "[JDFIXER]";
        internal const string Terminator = "###";

        /// <summary>
        /// Extracts the <c>[JDFIXER] ... ###</c> region.
        /// </summary>
        /// <returns>
        /// <c>false</c> when the source is null or empty, the marker is absent, the terminator is absent,
        /// or the region is empty. Never throws.
        /// </returns>
        internal static bool TryExtract(string source, out string block)
        {
            block = string.Empty;

            if (string.IsNullOrEmpty(source))
            {
                return false;
            }

            int start = source.IndexOf(Marker, StringComparison.Ordinal);
            if (start < 0)
            {
                return false;
            }

            start += Marker.Length;

            int end = source.IndexOf(Terminator, start, StringComparison.Ordinal);
            if (end < 0 || end <= start)
            {
                return false;
            }

            block = source.Substring(start, end - start);
            return true;
        }
    }
}
