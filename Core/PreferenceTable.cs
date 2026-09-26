using System;
using System.Collections.Generic;

namespace JDFixer.Core
{
    /// <summary>
    /// A sorted, immutable NJS-keyed lookup table of the values a user configured as an automated
    /// preference.
    /// </summary>
    /// <remarks>
    /// The table is sorted on construction, descending by NJS, so a lookup for a map's NJS always
    /// returns the entry with the largest NJS that does not exceed it.
    /// <para>
    /// This matters: the previous implementation used
    /// <c>FirstOrDefault(x =&gt; x.njs &lt;= njs)</c> over a list that was only ever sorted as a side
    /// effect of the user opening the Preferences screen. A hand-edited or freshly written config could
    /// be in any order, and an ascending list silently returned the wrong entry.
    /// </para>
    /// </remarks>
    internal sealed class PreferenceTable
    {
        /// <summary>A table with no entries, so a lookup always misses.</summary>
        internal static PreferenceTable Empty { get; } = new PreferenceTable(new float[0], new float[0]);

        private readonly float[] _njsDescending;
        private readonly float[] _values;

        private PreferenceTable(float[] njsDescending, float[] values)
        {
            _njsDescending = njsDescending;
            _values = values;
        }

        internal int Count => _njsDescending.Length;

        /// <summary>
        /// Builds a table from raw (NJS, value) pairs. The input order does not matter.
        /// </summary>
        internal static PreferenceTable Create(IEnumerable<KeyValuePair<float, float>> entries)
        {
            if (entries == null)
            {
                return Empty;
            }

            var collected = new List<Entry>();
            int index = 0;
            foreach (var entry in entries)
            {
                if (IsUsable(entry.Key) && IsUsable(entry.Value))
                {
                    collected.Add(new Entry(entry.Key, entry.Value, index++));
                }
            }

            if (collected.Count == 0)
            {
                return Empty;
            }

            // Descending by NJS so the first entry not exceeding the map's NJS is the match. The index
            // tiebreaker makes duplicate NJS deterministic: List.Sort is not stable, so without it the
            // winner would depend on the sort implementation.
            collected.Sort((a, b) =>
            {
                int byNjs = b.Njs.CompareTo(a.Njs);
                return byNjs != 0 ? byNjs : b.Index.CompareTo(a.Index);
            });

            var njs = new float[collected.Count];
            var values = new float[collected.Count];
            for (int i = 0; i < collected.Count; i++)
            {
                njs[i] = collected[i].Njs;
                values[i] = collected[i].Value;
            }

            return new PreferenceTable(njs, values);
        }

        private readonly struct Entry
        {
            internal Entry(float njs, float value, int index)
            {
                Njs = njs;
                Value = value;
                Index = index;
            }

            internal float Njs { get; }
            internal float Value { get; }
            internal int Index { get; }
        }

        /// <summary>
        /// Finds the configured value for <paramref name="njs"/>: the value belonging to the largest
        /// configured NJS that does not exceed it.
        /// </summary>
        /// <param name="hasMatch">
        /// <c>true</c> when a match was found. <c>false</c> means the map's NJS is below every
        /// configured entry, in which case <paramref name="value"/> is undefined and the caller should
        /// keep its own fallback.
        /// </param>
        internal bool TryLookup(float njs, out float value, out bool hasMatch)
        {
            // _njsDescending is sorted descending, so we want the *first* index whose NJS does not
            // exceed the map's. On a descending array that means searching left after a hit.
            int low = 0;
            int high = _njsDescending.Length - 1;
            int found = -1;

            while (low <= high)
            {
                int mid = low + ((high - low) / 2);
                if (_njsDescending[mid] <= njs)
                {
                    found = mid;
                    high = mid - 1;
                }
                else
                {
                    low = mid + 1;
                }
            }

            hasMatch = found >= 0;
            value = hasMatch ? _values[found] : 0f;
            return hasMatch;
        }

        private static bool IsUsable(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
