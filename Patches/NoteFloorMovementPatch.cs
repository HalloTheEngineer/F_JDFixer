using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace JDFixer.Patches
{
    /// <summary>
    /// Optional: hides notes during the look-ahead "floor" phase and reveals them when the jump starts.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Note movement is a strict three-phase cascade in <c>NoteMovement.ManualUpdate</c>:
    /// <c>NoteWaiting</c> (held out of sight), then <c>NoteFloorMovement</c> (a linear
    /// <c>LerpUnclamped</c> over a fixed 0.5 s covering the whole half-jump distance), then
    /// <c>NoteJump</c>.
    /// </para>
    /// <para>
    /// Simply skipping <c>NoteFloorMovement.ManualUpdate</c> - what the upstream NoteMovementFix mod
    /// does - leaves the note parked at <c>moveStartPosition</c>, a hundred units further back, so it
    /// appears to hang in the distance and then lurch forward. Instead the note is placed at the start of
    /// the jump immediately and hidden, then revealed on the first jump frame.
    /// </para>
    /// <para>
    /// This pairs well with high jump distances, which push the floor phase's start position further out
    /// and make notes visibly pop.
    /// </para>
    /// </remarks>
    internal static class LookAheadOption
    {
        internal static bool Enabled => Configuration.PluginConfig.Instance?.disable_look_ahead == true;

        /// <summary>
        /// Notes this mod has hidden and not yet revealed.
        /// </summary>
        /// <remarks>
        /// Tracked here rather than read back off the controller, because
        /// <c>NoteController.hidden</c> is an auto-property whose backing field name is a compiler
        /// implementation detail. <see cref="NoteController"/> does not override
        /// <c>GetHashCode</c>/<c>Equals</c> beyond Unity's instance-id identity, so the default
        /// <see cref="HashSet{T}"/> comparer gives reference semantics. Entries are removed when the note
        /// is revealed, and re-added on the next spawn, so the set stays bounded by the note pool.
        /// </remarks>
        private static readonly HashSet<NoteController> Hidden = new HashSet<NoteController>();

        internal static void Hide(NoteController controller)
        {
            if (Hidden.Add(controller))
            {
                // NoteController.Hide also raises HiddenStateDidChange, which toggles the wrapper's
                // active state - cheaper and less invasive than disabling the GameObject.
                controller.Hide(true);
            }
        }

        internal static void Reveal(NoteController controller)
        {
            if (Hidden.Remove(controller))
            {
                controller.Hide(false);
            }
        }
    }

    /// <summary>Collapses the floor phase: the note sits at the start of the jump from the outset.</summary>
    [HarmonyPatch(typeof(NoteFloorMovement), nameof(NoteFloorMovement.ManualUpdate))]
    internal static class NoteFloorMovementPatch
    {
        private static readonly AccessTools.FieldRef<NoteFloorMovement, Vector3> LocalPositionField =
            AccessTools.FieldRefAccess<NoteFloorMovement, Vector3>("_localPosition");

        private static readonly AccessTools.FieldRef<NoteFloorMovement, Quaternion> WorldRotationField =
            AccessTools.FieldRefAccess<NoteFloorMovement, Quaternion>("_worldRotation");

        /// <returns><c>false</c> to skip the original, having produced the same result ourselves.</returns>
        private static bool Prefix(NoteFloorMovement __instance, ref Vector3 __result)
        {
            if (!LookAheadOption.Enabled)
            {
                return true;
            }

            // NoteFloorMovement.endPos is moveEndPosition + _moveEndOffset: where the jump begins.
            Vector3 localPosition = __instance.endPos;
            LocalPositionField(__instance) = localPosition;

            Vector3 result = WorldRotationField(__instance) * localPosition;
            __instance.transform.localPosition = result;
            __result = result;

            return false;
        }
    }

    /// <summary>
    /// Hides each note as it spawns.
    /// </summary>
    /// <remarks>
    /// <c>NoteController.Init</c> is the single funnel for every note type, and runs on every spawn
    /// including pooled reuses, so it is where the reveal has to be armed.
    /// <para>
    /// The signature cannot be matched by parameter type: the second parameter is <c>in NoteSpawnData</c>,
    /// which is a by-reference parameter in IL and so needs <c>MakeByRefType()</c> to match exactly.
    /// A declared-only predicate avoids that entirely.
    /// </para>
    /// </remarks>
    [HarmonyPatch(typeof(NoteController))]
    internal static class NoteControllerInitPatch
    {
        private static MethodBase TargetMethod() => AccessTools.FirstMethod(
            typeof(NoteController),
            m => m.Name == "Init");

        private static void Postfix(NoteController __instance)
        {
            if (LookAheadOption.Enabled)
            {
                LookAheadOption.Hide(__instance);
            }
        }
    }

    /// <summary>Reveals the note on the first frame of its jump.</summary>
    [HarmonyPatch(typeof(NoteJump), nameof(NoteJump.ManualUpdate))]
    internal static class NoteJumpRevealPatch
    {
        private static void Postfix(NoteJump __instance)
        {
            if (!LookAheadOption.Enabled)
            {
                return;
            }

            // ManualUpdate is only reached once the note is in the jump phase, so getting here means the
            // floor phase is over.
            NoteController controller = __instance.GetComponentInParent<NoteController>();
            if (controller != null)
            {
                LookAheadOption.Reveal(controller);
            }
        }
    }
}
