using System.Globalization;

namespace JDFixer.Core
{
    /// <summary>
    /// Pure jump-distance / reaction-time math. This is a direct transcription of the way Beat Saber
    /// derives note jump distance from BPM, NJS and the beat offset, so that the numbers JDFixer shows
    /// in the UI match the numbers the game actually plays at.
    /// </summary>
    /// <remarks>
    /// Everything here is deliberately free of Unity, IPA and config dependencies so it can be unit
    /// tested. See <c>Tests/JDFixer.Tests</c>.
    /// </remarks>
    internal static class JumpDistanceMath
    {
        /// <summary>
        /// Beat offset the game starts from before clamping. Matches
        /// <c>BeatmapObjectSpawnMovementData._startHalfJumpDurationInBeats</c>.
        /// </summary>
        internal const float StartHalfJumpDurationInBeats = 4f;

        /// <summary>
        /// Beat offset above which the game halves the jump. Matches
        /// <c>BeatmapObjectSpawnMovementData._maxHalfJumpDistance</c>. The game compares with a strict
        /// inequality against exactly 18, so we use the same epsilon.
        /// </summary>
        internal const float MaxHalfJumpDistance = 17.999f;

        /// <summary>
        /// Floor the game clamps the half jump to, in beats. Matches
        /// <c>CoreMathUtils.CalculateHalfJumpDurationInBeats</c>.
        /// </summary>
        internal const float MinHalfJumpDurationInBeats = 0.25f;

        /// <summary>
        /// NJS at or below which the difficulty's own value is discarded by the game. Matches
        /// <c>VariableMovementDataProvider.kMinNoteJumpMovementSpeed</c>.
        /// </summary>
        internal const float MinNoteJumpMovementSpeed = 0.01f;

        /// <summary>NJS substituted when a map reports a nonsensical value.</summary>
        internal const float FallbackNoteJumpMovementSpeed = 10f;

        /// <summary>NJS the game falls back to for Easy/Normal/Hard.</summary>
        internal const float DefaultNjs = 10f;

        /// <summary>Reaction time divisor: JD and RT relate by <c>rt = jd * 500 / njs</c>.</summary>
        internal const float ReactionTimeDivisor = 500f;

        /// <summary>
        /// Computes the jump distance, in Beat Saber units, produced by a given BPM / NJS / beat offset.
        /// This mirrors the game's own calculation and is the single source of truth for the values the
        /// JDFixer UI displays.
        /// </summary>
        /// <returns>
        /// The jump distance, or <c>0</c> when <paramref name="bpm"/> is not positive. The game never
        /// passes a non-positive BPM, but a hand-edited or corrupt level can, and the unguarded
        /// calculation previously looped for ~1300 iterations before settling on <see cref="float.PositiveInfinity"/>.
        /// </returns>
        internal static float CalculateJumpDistance(float bpm, float njs, float offset)
        {
            if (bpm <= 0f || float.IsNaN(bpm) || float.IsInfinity(bpm))
            {
                return 0f;
            }

            njs = SanitizeNjs(njs);

            float oneBeatDuration = 60f / bpm;
            float halfJump = StartHalfJumpDurationInBeats;

            while (njs * oneBeatDuration * halfJump > MaxHalfJumpDistance)
            {
                halfJump /= 2f;
            }

            halfJump += offset;
            if (halfJump < MinHalfJumpDurationInBeats)
            {
                halfJump = MinHalfJumpDurationInBeats;
            }

            return njs * oneBeatDuration * halfJump * 2f;
        }

        /// <summary>
        /// The analytic inverse of <see cref="CalculateJumpDistance"/>: finds the beat offset that makes
        /// the game produce <paramref name="jumpDistance"/>.
        /// </summary>
        /// <returns>
        /// The beat offset to feed to the game as <c>noteJumpValue</c>, or <c>0</c> when
        /// <paramref name="bpm"/> / <paramref name="njs"/> are not usable.
        /// </returns>
        internal static float SolveBeatOffsetForJumpDistance(float bpm, float njs, float jumpDistance)
        {
            if (bpm <= 0f || float.IsNaN(bpm) || float.IsInfinity(bpm))
            {
                return 0f;
            }

            njs = SanitizeNjs(njs);
            if (jumpDistance <= 0f || float.IsNaN(jumpDistance) || float.IsInfinity(jumpDistance))
            {
                return 0f;
            }

            float oneBeatDuration = 60f / bpm;
            float baseHalfJump = StartHalfJumpDurationInBeats;

            while (njs * oneBeatDuration * baseHalfJump > MaxHalfJumpDistance)
            {
                baseHalfJump /= 2f;
            }

            if (baseHalfJump < MinHalfJumpDurationInBeats)
            {
                baseHalfJump = MinHalfJumpDurationInBeats;
            }

            // jumpDistance == njs * oneBeatDuration * (baseHalfJump + offset) * 2
            return jumpDistance / (njs * oneBeatDuration * 2f) - baseHalfJump;
        }

        /// <summary>Reaction time in milliseconds for a jump distance at a given NJS.</summary>
        internal static float CalculateReactionTime(float jumpDistance, float njs)
        {
            if (njs <= MinNoteJumpMovementSpeed)
            {
                return 0f;
            }

            return jumpDistance / (2f * njs) * ReactionTimeDivisor;
        }

        /// <summary>Jump distance that yields a given reaction time at a given NJS.</summary>
        internal static float CalculateJumpDistanceFromReactionTime(float reactionTime, float njs)
        {
            return reactionTime * (2f * njs) / ReactionTimeDivisor;
        }

        /// <summary>
        /// The methods below are written so that the test project needs no Beat Saber assemblies: a pure
        /// numeric core can be verified on any machine, without a game install.
        /// </summary>

        /// <summary>Clamps a reported NJS into the range the game will accept.</summary>
        internal static float SanitizeNjs(float njs)
        {
            if (float.IsNaN(njs) || njs <= MinNoteJumpMovementSpeed)
            {
                return FallbackNoteJumpMovementSpeed;
            }

            return njs;
        }

        // --- Formatting helpers. Invariant on purpose: these strings are compared and parsed by
        // --- humans reading the HUD, and a comma decimal separator reads as a thousands separator.

        /// <summary>Coloured jump distance plus its reaction time, as shown next to a map's JD.</summary>
        internal static string FormatJumpDistanceWithReactionTime(float jumpDistance, float njs)
        {
            return "<#ffff00>" + FormatJumpDistance(jumpDistance)
                 + "     <#8c1aff>" + FormatReactionTime(CalculateReactionTime(jumpDistance, njs));
        }

        /// <summary>Coloured jump distance derived from a reaction time.</summary>
        internal static string FormatJumpDistanceForReactionTime(float reactionTime, float njs)
        {
            return "<#ffff00>" + FormatJumpDistance(CalculateJumpDistanceFromReactionTime(reactionTime, njs));
        }

        internal static string FormatJumpDistance(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);

        internal static string FormatReactionTime(float value) => value.ToString("0", CultureInfo.InvariantCulture) + " ms";

        internal static string FormatPlainFloat(float value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
