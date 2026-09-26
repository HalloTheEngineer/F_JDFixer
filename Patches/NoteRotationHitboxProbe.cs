using System;
using HarmonyLib;
using UnityEngine;

namespace JDFixer.Patches
{
    /// <summary>
    /// One-shot diagnostic that settles whether note rotation moves the cut hitbox.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>NoteBasicCutInfoHelper.GetBasicCutInfo</c> transforms the saber's cut vector into note-local
    /// space with <c>noteTransform.InverseTransformVector(cutDirVec)</c> and then requires the cut to be
    /// within tolerance of -90 degrees <em>in that frame</em>. There is no separate "logical" rotation, so
    /// if the transform being rotated is the same one the cut test reads, changing the rotation changes
    /// the cut window.
    /// </para>
    /// <para>
    /// Both are <c>[SerializeField]</c> assigned in the note prefab, so this cannot be answered from the
    /// decompiled game code. This logs the answer once, on the first cube note spawned, so the README can
    /// state a fact for the target game version instead of a guess.
    /// </para>
    /// </remarks>
    [HarmonyPatch(typeof(GameNoteController), nameof(GameNoteController.Init))]
    internal static class NoteRotationHitboxProbe
    {
        private static bool _reported;

        private static readonly AccessTools.FieldRef<NoteJump, Transform> RotatedObjectField =
            AccessTools.FieldRefAccess<NoteJump, Transform>("_rotatedObject");

        private static void Postfix(GameNoteController __instance)
        {
            if (_reported)
            {
                return;
            }

            _reported = true;

            try
            {
                Transform noteTransform = __instance.noteTransform;
                if (noteTransform == null)
                {
                    Plugin.Log.Warn("[hitbox probe] Could not resolve NoteController.noteTransform");
                    return;
                }

                var noteJumps = noteTransform.GetComponentsInChildren<NoteJump>(true);
                if (noteJumps.Length == 0)
                {
                    Plugin.Log.Warn("[hitbox probe] No NoteJump found under the note transform");
                    return;
                }

                Transform rotated = RotatedObjectField(noteJumps[0]);
                bool sameObject = ReferenceEquals(rotated, noteTransform);

                Plugin.Log.Info(
                    "[hitbox probe] NoteController._noteTransform is " +
                    (sameObject ? "THE SAME OBJECT AS" : "a different object from") +
                    " NoteJump._rotatedObject - note rotation " +
                    (sameObject ? "MOVES the cut frame used by NoteBasicCutInfoHelper" : "does not affect the cut frame"));
            }
            catch (Exception e)
            {
                Plugin.Log.Warn($"[hitbox probe] Failed: {e.Message}");
            }
        }
    }
}
