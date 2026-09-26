using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace JDFixer.Patches
{
    /// <summary>
    /// Optional: notes appear at their final cut angle immediately instead of swinging into place over
    /// the first half of the jump.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Stock <c>NoteJump.ManualUpdate</c> animates the note's rotation with a two-stage slerp:
    /// <code>
    /// Quaternion q = (num2 &lt; 0.125f)
    ///     ? Quaternion.Slerp(_startRotation, _middleRotation, ...)
    ///     : Quaternion.Slerp(_middleRotation, _endRotation, ...);
    /// </code>
    /// Both slerps degenerate when their two rotations are equal, and <c>Quaternion.Slerp(q, q, t)</c>
    /// returns <c>q</c>. So setting <c>_startRotation = _middleRotation</c> after <c>Init</c> makes
    /// <c>q</c> equal <c>_endRotation</c> for the whole jump, which is exactly the intended result.
    /// </para>
    /// <para>
    /// This matters because the alternative - copying <c>ManualUpdate</c> and patching it wholesale - is
    /// what the upstream NoteMovementFix mod does, roughly 180 lines of forked game logic that silently
    /// diverges whenever Beat Games touch note movement. Verified against 1.44.1: the two fields are
    /// written only in <c>Init</c> and read at exactly one place, <c>NoteJump.cs:188</c>.
    /// </para>
    /// <para>
    /// The rotate-towards-player blend downstream is untouched, so this produces the same
    /// <c>Quaternion.Lerp(_endRotation, towardsPlayer, num2 * 2f)</c> as the reference implementation.
    /// </para>
    /// </remarks>
    [HarmonyPatch(typeof(NoteJump), nameof(NoteJump.Init))]
    internal static class NoteJumpRotationPatch
    {
        private static readonly AccessTools.FieldRef<NoteJump, Quaternion> StartRotationField =
            AccessTools.FieldRefAccess<NoteJump, Quaternion>("_startRotation");

        private static readonly AccessTools.FieldRef<NoteJump, Quaternion> MiddleRotationField =
            AccessTools.FieldRefAccess<NoteJump, Quaternion>("_middleRotation");

        private static void Postfix(NoteJump __instance)
        {
            if (Configuration.PluginConfig.Instance?.instant_note_rotation != true)
            {
                return;
            }

            // Both branches of the game's ternary now slerp from _endRotation to _endRotation, so the
            // result is _endRotation regardless of the interpolation factor.
            StartRotationField(__instance) = MiddleRotationField(__instance);
        }
    }
}
