using System;

namespace JDFixer.Core
{
    /// <summary>
    /// The half-jump duration in beats, and the offset that produces a given one.
    /// </summary>
    /// <remarks>
    /// A transcription of <c>CoreMathUtils.CalculateHalfJumpDurationInBeats</c> (verified identical
    /// against 1.44.1), which is how the game turns a beat offset into a half-jump duration in beats.
    /// Kept separate from <see cref="JumpDistanceMath"/> because this is the half-jump side of the
    /// calculation, and because the NJS-event compensation needs it: the provider stores the offset's
    /// result in <c>_halfJumpDurationInBeats</c> and discards the offset, so the only way to recover the
    /// duration a setpoint asked for is to recompute it from the offset.
    /// </remarks>
    internal static class HalfJumpDuration
    {
        /// <summary>
        /// The half jump in beats before the map's offset is added: four beats, halved until the resulting
        /// distance is within the cap.
        /// </summary>
        /// <remarks>
        /// This is not clamped to <see cref="JumpDistanceMath.MinHalfJumpDurationInBeats"/>; the game
        /// only clamps after adding the offset, and the difference matters for very low NJS.
        /// </remarks>
        internal static float ComputeBaseBeats(float bpm, float njs)
        {
            if (bpm <= 0f || float.IsNaN(bpm) || float.IsInfinity(bpm))
            {
                return 0f;
            }

            njs = JumpDistanceMath.SanitizeNjs(njs);

            float oneBeatDuration = 60f / bpm;
            float beats = JumpDistanceMath.StartHalfJumpDurationInBeats;
            float distance = njs * oneBeatDuration * beats;

            while (distance > JumpDistanceMath.MaxHalfJumpDistance)
            {
                beats /= 2f;
                distance = njs * oneBeatDuration * beats;
            }

            return beats;
        }

        /// <summary>
        /// The half jump in beats the game will actually use for a given offset, including its
        /// 0.25 beat floor.
        /// </summary>
        internal static float ComputeBeats(float bpm, float njs, float offset)
        {
            float beats = ComputeBaseBeats(bpm, njs) + offset;
            return beats < JumpDistanceMath.MinHalfJumpDurationInBeats
                ? JumpDistanceMath.MinHalfJumpDurationInBeats
                : beats;
        }
    }
}
