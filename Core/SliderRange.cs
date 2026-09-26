using System;

namespace JDFixer.Core
{
    /// <summary>
    /// An inclusive minimum/maximum pair for one of the two slider units, kept strictly ordered.
    /// </summary>
    /// <remarks>
    /// The pair is a value rather than two loose ints because the interesting behaviour is the
    /// interaction between the ends: dragging one past the other has to carry the other out of the way,
    /// and that is far easier to reason about - and to test - as one operation.
    /// </remarks>
    internal readonly struct SliderRange
    {
        internal SliderRange(int min, int max)
        {
            Min = min;
            Max = max;
        }

        internal int Min { get; }

        internal int Max { get; }

        /// <summary>
        /// Returns a range with the ends in order and inside the absolute limits, whatever order and
        /// magnitude they arrived in.
        /// </summary>
        /// <remarks>
        /// Used to repair a hand-edited config. Crossing ends are swapped rather than reset, so a user
        /// who typed the two values the wrong way round keeps both numbers instead of losing them to the
        /// defaults.
        /// </remarks>
        internal static SliderRange Repaired(int min, int max, int absoluteMin, int absoluteMax)
        {
            if (min > max)
            {
                (min, max) = (max, min);
            }

            // Two distinct values are required for Min < Max, so the usable window is one narrower at
            // each end than the absolute limits.
            int lo = Clamp(min, absoluteMin, absoluteMax - 1);
            int hi = Clamp(max, absoluteMin + 1, absoluteMax);

            if (lo >= hi)
            {
                // Both landed on the same value, which the clamps above cannot resolve on their own.
                lo = Clamp(lo, absoluteMin, absoluteMax - 1);
                hi = lo + 1;
            }

            return new SliderRange(lo, hi);
        }

        /// <summary>
        /// Moves the low end, carrying the high end out of the way if the two would otherwise cross.
        /// </summary>
        /// <param name="value">Requested new minimum.</param>
        /// <param name="absoluteMin">Lowest value the unit permits at all.</param>
        /// <param name="absoluteMax">Highest value the unit permits at all.</param>
        /// <remarks>
        /// The high end is lifted rather than the request being refused, so dragging the minimum to the
        /// top of its travel widens the range instead of appearing to do nothing.
        /// </remarks>
        internal SliderRange WithMin(int value, int absoluteMin, int absoluteMax)
        {
            int lo = Clamp(value, absoluteMin, absoluteMax - 1);
            return new SliderRange(lo, Math.Min(Math.Max(Max, lo + 1), absoluteMax));
        }

        /// <summary>
        /// Moves the high end, carrying the low end out of the way if the two would otherwise cross.
        /// </summary>
        /// <param name="value">Requested new maximum.</param>
        /// <param name="absoluteMin">Lowest value the unit permits at all.</param>
        /// <param name="absoluteMax">Highest value the unit permits at all.</param>
        internal SliderRange WithMax(int value, int absoluteMin, int absoluteMax)
        {
            int hi = Clamp(value, absoluteMin + 1, absoluteMax);
            return new SliderRange(Math.Max(Math.Min(Min, hi - 1), absoluteMin), hi);
        }

        private static int Clamp(int value, int min, int max) =>
            max < min ? min : Math.Min(Math.Max(value, min), max);
    }
}
