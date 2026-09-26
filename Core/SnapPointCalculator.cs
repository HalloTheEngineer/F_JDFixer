using System;
using System.Collections.Generic;
using System.Globalization;

namespace JDFixer.Core
{
    /// <summary>A single selectable snap position and the beat offset that produces it.</summary>
    internal readonly struct SnapPoint
    {
        internal SnapPoint(float value, string offsetLabel)
        {
            Value = value;
            OffsetLabel = offsetLabel;
        }

        /// <summary>Jump distance or reaction time, depending on the active slider setting.</summary>
        internal float Value { get; }

        /// <summary>Human readable beat offset, e.g. <c>( -0.13, 0 )</c>. May be empty.</summary>
        internal string OffsetLabel { get; }
    }

    /// <summary>
    /// An immutable, strictly ascending set of snap positions.
    /// </summary>
    internal sealed class SnapPointSet
    {
        /// <summary>
        /// A set that cannot snap. <see cref="Nearest"/> on this returns its input unchanged, which is
        /// what makes it safe to use when the caller has no map context: the previous implementation
        /// used mutable static fields that defaulted to <c>0f</c>, and the gameplay patch would then
        /// drive jump distance to roughly zero for anyone who enabled offset snapping without opening
        /// the legacy settings tab first.
        /// </summary>
        internal static SnapPointSet Empty { get; } = new SnapPointSet(new float[0], new string[0], 0f);

        private readonly float[] _values;
        private readonly string[] _labels;
        private readonly float _baseValue;

        internal SnapPointSet(float[] values, string[] labels, float baseValue)
        {
            _values = values;
            _labels = labels;
            _baseValue = baseValue;
        }

        internal int Count => _values.Length;

        /// <summary>The un-snapped value the set was generated around.</summary>
        internal float BaseValue => _baseValue;

        internal float this[int index] => _values[index];

        /// <summary>
        /// Returns the snap position at or above <paramref name="value"/>, or the last position when
        /// <paramref name="value"/> is above the whole range.
        /// </summary>
        /// <remarks>
        /// Deliberately side-effect free. The previous implementation stored the result in mutable
        /// static fields that were written from BSML property getters and read by the gameplay patch,
        /// so the outcome depended on whether the UI had been rendered.
        /// </remarks>
        internal SnapPoint Nearest(float value)
        {
            if (_values.Length == 0)
            {
                return new SnapPoint(value, string.Empty);
            }

            // _values is strictly ascending, so ~index is the first entry greater than value.
            int index = Array.BinarySearch(_values, value);
            if (index < 0)
            {
                index = ~index;
            }

            if (index >= _values.Length)
            {
                index = _values.Length - 1;
            }

            return new SnapPoint(_values[index], _labels[index]);
        }
    }

    /// <summary>
    /// Generates the set of JD or RT values reachable by shifting a map's beat offset by a fixed
    /// fraction, mirroring Beat Saber's own offset snapper.
    /// </summary>
    internal static class SnapPointCalculator
    {
        /// <summary>
        /// Hard cap on generated points. The previous implementation had no bound and looped forever
        /// when the step was zero, growing an unbounded list until the game froze.
        /// </summary>
        internal const int MaxPointCount = 512;

        /// <summary>
        /// Builds the snap positions around <paramref name="baseValue"/>.
        /// </summary>
        /// <param name="baseValue">The map's current JD (or RT), i.e. its un-snapped value.</param>
        /// <param name="baseOffset">
        /// The map's beat offset, used only to label each generated point with the absolute beat
        /// offset it would require.
        /// </param>
        /// <param name="unitStep">
        /// The change in <paramref name="baseValue"/> produced by one step of the offset fraction.
        /// When this is zero or non-finite the set degrades to the single base point, which is the
        /// correct answer: no offset produces a different value, so there is nothing to snap to.
        /// </param>
        /// <param name="minValue">Lowest selectable slider value.</param>
        /// <param name="maxValue">Highest selectable slider value.</param>
        /// <param name="offsetFraction">Denominator of the offset step, e.g. 8 for 1/8.</param>
        internal static SnapPointSet Create(float baseValue, float baseOffset, float unitStep, float minValue, float maxValue, float offsetFraction)
        {
            if (!IsUsable(baseValue) || !IsUsable(unitStep) || unitStep <= 0f || !IsUsable(offsetFraction) || offsetFraction < 1f)
            {
                return SnapPointSet.Empty;
            }

            var values = new List<float>();
            var labels = new List<string>();

            values.Add(baseValue);
            labels.Add(FormatOffsetLabel(0f, baseOffset));

            // Upwards from the base value.
            int multiple = 1;
            float point = baseValue + unitStep;
            while (point <= maxValue && values.Count < MaxPointCount)
            {
                values.Add(point);
                labels.Add(FormatOffsetLabel(multiple / offsetFraction, baseOffset));

                point += unitStep;
                multiple++;
            }

            // Downwards from the base value, prepending so the result stays ascending.
            multiple = -1;
            point = baseValue - unitStep;
            while (point >= minValue && values.Count < MaxPointCount)
            {
                values.Insert(0, point);
                labels.Insert(0, FormatOffsetLabel(multiple / offsetFraction, baseOffset));

                point -= unitStep;
                multiple--;
            }

            return values.Count == 1
                ? SnapPointSet.Empty
                : new SnapPointSet(values.ToArray(), labels.ToArray(), baseValue);
        }

        /// <summary>
        /// The change in jump distance produced by shifting the beat offset by
        /// <c>1 / offsetFraction</c>. This is zero whenever both the un-shifted and the shifted offsets
        /// fall below the game's 0.25 beat floor, which is the case for any map with an offset at or
        /// below -3.75 beats.
        /// </summary>
        internal static float CalculateUnitStep(float bpm, float njs, float offset, float offsetFraction)
        {
            if (!IsUsable(offsetFraction) || offsetFraction < 1f)
            {
                return 0f;
            }

            float shifted = JumpDistanceMath.CalculateJumpDistance(bpm, njs, offset + 1f / offsetFraction);
            float unshifted = JumpDistanceMath.CalculateJumpDistance(bpm, njs, offset);
            return shifted - unshifted;
        }

        private static bool IsUsable(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static string FormatOffsetLabel(float offsetDelta, float baseOffset)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "( {0}, {1} )",
                offsetDelta.ToString("0.###", CultureInfo.InvariantCulture),
                (baseOffset + offsetDelta).ToString("0.##", CultureInfo.InvariantCulture));
        }
    }
}
