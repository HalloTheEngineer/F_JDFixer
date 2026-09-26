using System.Collections.Generic;
using System.Runtime.CompilerServices;
using IPA.Config.Stores;
using IPA.Config.Stores.Attributes;
using IPA.Config.Stores.Converters;


[assembly: InternalsVisibleTo(GeneratedStore.AssemblyVisibilityTarget)]
[assembly: InternalsVisibleTo("JDFixer.Tests")]

namespace JDFixer.Configuration
{
    /// <summary>
    /// The BSIPA-backed configuration. Property names and accessibility are part of the on-disk
    /// <c>JDFixer.json</c> format: renaming or un-virtualing a property silently drops users' settings.
    /// </summary>
    internal class PluginConfig
    {
        /// <summary>Lowest accepted value for the offset fraction denominator.</summary>
        internal const float MinOffsetFraction = 2f;

        /// <summary>
        /// Reconciles the stored config: migrates legacy shapes first, then repairs out-of-range values.
        /// </summary>
        /// <remarks>
        /// Order matters. The legacy migration moves data into <see cref="preferredValues_jd"/> and
        /// <see cref="preferredValues_rt"/>, which start out empty. Validating first therefore saw an
        /// empty table, judged every stored preference index out of range, and reset the user's active
        /// preference to "None" on the very first launch after upgrading.
        /// </remarks>
        public void OnLoad()
        {
            bool converted = false;
            converted |= TryConvertJD();
            converted |= TryConvertRT();

            CheckConflicts();

            if (converted)
            {
                Plugin.Log.Info("Converted legacy preferred values");
            }
        }

        private bool TryConvertJD()
        {
            if (preferredValues == null)
            {
                return false;
            }

            // Insert at the front: existing indices must keep pointing at the same data, otherwise a
            // partial migration would silently re-point the user's selection.
            preferredValues_jd.Insert(0, preferredValues);
            preferredValues = null;
            return true;
        }

        private bool TryConvertRT()
        {
            if (rt_preferredValues == null)
            {
                return false;
            }

            preferredValues_rt.Insert(0, rt_preferredValues);
            rt_preferredValues = null;
            return true;
        }

        /// <summary>
        /// Clamps every stored index and numeric setting into a range the rest of the mod can index and
        /// divide by safely. A Harmony patch indexing <c>preferredValues_jd[-2]</c> throws on the main
        /// thread at gameplay start, so this is a correctness boundary, not a nicety.
        /// </summary>
        private void CheckConflicts()
        {
            preferredValues_jd ??= new List<List<JDPref>>();
            preferredValues_rt ??= new List<List<RTPref>>();

            int jdCount = preferredValues_jd.Count;
            int rtCount = preferredValues_rt.Count;
            int total = jdCount + rtCount;

            if (pref_selected < 0 || pref_selected > total)
            {
                Plugin.Log.Info($"Invalid pref_selected value was reset ({pref_selected} not in 0..{total})");
                pref_selected = 0;
            }

            if (!IsValidIndex(use_jd_pref, jdCount))
            {
                Plugin.Log.Info("Invalid use_jd_pref value was reset");
                use_jd_pref = NoPreference;
            }

            if (!IsValidIndex(use_rt_pref, rtCount))
            {
                Plugin.Log.Info("Invalid use_rt_pref value was reset");
                use_rt_pref = NoPreference;
            }

            if (offset_fraction < MinOffsetFraction)
            {
                Plugin.Log.Info($"Invalid offset_fraction value was reset ({offset_fraction} < {MinOffsetFraction})");
                offset_fraction = 8f;
            }

            if (minJumpDistance >= maxJumpDistance)
            {
                // Swapped rather than reset. A user who typed the two ends the wrong way round - the
                // only realistic way to reach this - keeps both numbers instead of losing them to the
                // defaults, which is what the previous reset did.
                Plugin.Log.Info($"minJumpDistance ({minJumpDistance}) was not below maxJumpDistance ({maxJumpDistance}); swapping");

                var jd = Core.SliderRange.Repaired(minJumpDistance, maxJumpDistance, 2, 60);
                minJumpDistance = jd.Min;
                maxJumpDistance = jd.Max;
            }

            if (minReactionTime >= maxReactionTime)
            {
                Plugin.Log.Info($"minReactionTime ({minReactionTime}) was not below maxReactionTime ({maxReactionTime}); swapping");

                var rt = Core.SliderRange.Repaired(minReactionTime, maxReactionTime, 50, 4000);
                minReactionTime = rt.Min;
                maxReactionTime = rt.Max;
            }
        }

        /// <summary>Preference sentinel meaning "no automated preference is active".</summary>
        internal const int NoPreference = -1;

        /// <summary>
        /// A preference index is either the <see cref="NoPreference"/> sentinel or a valid position.
        /// The previous check only tested the upper bound, so a hand-edited <c>-1</c> selection became
        /// <c>use_jd_pref = -2</c> and then an out-of-range list index.
        /// </summary>
        private static bool IsValidIndex(int index, int count) => index == NoPreference || (index >= 0 && index < count);

        internal static PluginConfig Instance { get; set; }

        internal virtual bool enabled { get; set; } = false;

        internal virtual float jumpDistance { get; set; } = 24f;
        internal virtual int minJumpDistance { get; set; } = 12;
        internal virtual int maxJumpDistance { get; set; } = 35;
        internal virtual int use_jd_pref { get; set; } = NoPreference;

        [UseConverter(typeof(ListConverter<List<JDPref>>))]
        [NonNullable]
        internal virtual List<List<JDPref>> preferredValues_jd { get; set; } = new List<List<JDPref>>();

        /// <summary>
        /// Legacy config value for JumpDistance preferred-values. Retired by
        /// <see cref="TryConvertJD"/> but must remain, and must remain <c>protected virtual</c>: it is
        /// how an upgrading user's data is read, and the generated store relies on the virtual chain.
        /// </summary>
        [UseConverter(typeof(ListConverter<JDPref>))]
        protected virtual List<JDPref> preferredValues { get; set; }

        internal virtual float reactionTime { get; set; } = 500f;
        internal virtual int minReactionTime { get; set; } = 300;
        internal virtual int maxReactionTime { get; set; } = 1600;
        internal virtual int use_rt_pref { get; set; } = NoPreference;

        [UseConverter(typeof(ListConverter<List<RTPref>>))]
        [NonNullable]
        internal virtual List<List<RTPref>> preferredValues_rt { get; set; } = new List<List<RTPref>>();

        /// <summary>
        /// Legacy config value for ReactionTime preferred-values. Retired by <see cref="TryConvertRT"/>;
        /// see <see cref="preferredValues"/> for why it must stay.
        /// </summary>
        [UseConverter(typeof(ListConverter<RTPref>))]
        protected virtual List<RTPref> rt_preferredValues { get; set; } = null;

        internal virtual int slider_setting { get; set; } = (int)Core.SliderUnit.JumpDistance;
        internal virtual int pref_selected { get; set; } = 0;

        internal virtual int use_heuristic { get; set; } = 0;
        internal virtual float lower_threshold { get; set; } = 1f;
        internal virtual float upper_threshold { get; set; } = 100f;

        internal virtual bool rt_display_enabled { get; set; } = true;
        internal virtual bool legacy_display_enabled { get; set; } = false;

        internal virtual bool use_offset { get; set; } = false;

        /// <summary>
        /// Denominator of the beat-offset snap step. Kept as <c>float</c> because that is the type the
        /// value has always had on disk; changing it would fail to deserialise existing configs.
        /// </summary>
        internal virtual float offset_fraction { get; set; } = 8f;

        internal virtual int song_speed_setting { get; set; } = (int)Core.SongSpeedSetting.JumpDistance;

        // --- Note presentation. All default off: each changes how notes look or how the game
        // --- behaves outside of jump distance, so none of them should surprise an existing user.

        /// <summary>
        /// Notes appear at their final cut angle immediately instead of swinging into place over the
        /// first half of the jump.
        /// </summary>
        internal virtual bool instant_note_rotation { get; set; } = false;

        /// <summary>
        /// Notes are hidden during the look-ahead floor phase and appear when the jump begins, instead
        /// of travelling the half-jump distance during the preceding 0.5 seconds.
        /// </summary>
        internal virtual bool disable_look_ahead { get; set; } = false;

        /// <summary>
        /// Keep the game from pausing when the HMD is unmounted or loses focus, and ignore the pause
        /// button. Can affect leaderboard eligibility.
        /// </summary>
        internal virtual bool remove_pause { get; set; } = false;

        /// <summary>
        /// Recompute the reaction time compensation while a map's NJS events are changing speed, rather
        /// than only at map start.
        /// </summary>
        internal virtual bool njs_event_compensation { get; set; } = false;

        /// <summary>
        /// Called by BSIPA when the config is written, and by the mod when it wants the change flushed
        /// to disk immediately.
        /// </summary>
        internal virtual void Changed()
        {
            // Re-clamp on every write. The UI can transiently produce out-of-range indices (removing
            // the last preference while a later one is selected), and an out-of-range index becomes an
            // exception thrown from a Harmony patch on the main thread at gameplay start.
            CheckConflicts();
        }
    }
}
