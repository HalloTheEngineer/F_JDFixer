using HarmonyLib;

namespace JDFixer.Patches
{
    /// <summary>
    /// Optional: stops the game pausing when the HMD is unmounted or loses focus, and ignores the pause
    /// button.
    /// </summary>
    /// <remarks>
    /// Each of the three handlers in <c>PauseController</c> does nothing but conditionally call
    /// <c>Pause()</c>, verified against 1.44.1:
    /// <code>
    /// private void HandleMenuButtonTriggered() { if (canChangePauseState) Pause(); }
    /// private void HandleFocusWasCaptured()    { Pause(); }
    /// private void HandleHMDUnmounted()        { if (!ignoreHMDUUnmountEvets) Pause(); }
    /// </code>
    /// so skipping them is sufficient and there is no state to restore.
    /// <para>
    /// Two cautions. ScoreSaber and BeatLeader patch these same methods to implement their own replay
    /// pausing, so with those installed the behaviour is whichever patch wins - JDFixer uses its own
    /// Harmony id (<c>com.zephyr.BeatSaber.JDFixer</c>) and Harmony applies patches in id order.
    /// Separately, a leaderboard may treat an unpaused disconnect differently, so this can affect
    /// eligibility. Off by default for that reason.
    /// </para>
    internal static class PauseOption
    {
        internal static bool Enabled => Configuration.PluginConfig.Instance?.remove_pause == true;
    }

    // Named by string: the handlers are private, so nameof cannot reach them from here.
    [HarmonyPatch(typeof(PauseController), "HandleHMDUnmounted")]
    internal static class PauseControllerHmdUnmountedPatch
    {
        private static bool Prefix() => !PauseOption.Enabled;
    }

    [HarmonyPatch(typeof(PauseController), "HandleFocusWasCaptured")]
    internal static class PauseControllerFocusCapturedPatch
    {
        private static bool Prefix() => !PauseOption.Enabled;
    }

    [HarmonyPatch(typeof(PauseController), "HandleMenuButtonTriggered")]
    internal static class PauseControllerMenuButtonPatch
    {
        private static bool Prefix() => !PauseOption.Enabled;
    }
}
