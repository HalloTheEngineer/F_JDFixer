using BeatSaberMarkupLanguage.Attributes;
using JDFixer.Core;
using JDFixer.Interfaces;

namespace JDFixer.UI
{
    /// <summary>
    /// Adds the "selected map" readout to <see cref="ModifierUIBase"/>. Shared by the two tabs that show
    /// it, which is everything except the TA/MP tab (there is no map selected there to describe).
    /// </summary>
    internal abstract class BeatmapInfoDisplayBase : ModifierUIBase, IBeatmapInfoUpdater
    {
        /// <summary>The selected map, or <see cref="BeatmapInfo.Empty"/> before one is chosen.</summary>
        protected BeatmapInfo SelectedBeatmap { get; private set; } = BeatmapInfo.Empty;

        protected BeatmapInfoDisplayBase(
            MainFlowCoordinator mainFlowCoordinator,
            PreferencesFlowCoordinator preferencesFlowCoordinator)
            : base(mainFlowCoordinator, preferencesFlowCoordinator)
        {
        }

        /// <summary>
        /// The map's NJS, which sets the reachable slider range and the JD/RT conversion. Falls back to
        /// the game's default NJS before a map is selected so the conversion stays finite.
        /// </summary>
        protected override float DisplayNjs =>
            SelectedBeatmap.NJS > JumpDistanceMath.MinNoteJumpMovementSpeed
                ? SelectedBeatmap.NJS
                : JumpDistanceMath.FallbackNoteJumpMovementSpeed;

        protected override float MinJdSlider => SelectedBeatmap.MinJDSlider;

        protected override float MaxJdSlider => SelectedBeatmap.MaxJDSlider;

        protected override float MinRtSlider => SelectedBeatmap.MinRTSlider;

        protected override float MaxRtSlider => SelectedBeatmap.MaxRTSlider;

        public virtual void BeatmapInfoUpdated(BeatmapInfo beatmapInfo)
        {
            SelectedBeatmap = beatmapInfo ?? BeatmapInfo.Empty;

            RaiseAll(nameof(MapJumpDistanceLabel), nameof(MapJumpDistance), nameof(MapMinJumpDistance));

            OnBeatmapInfoUpdated();
        }

        /// <summary>Runs after the standard map readout raises.</summary>
        protected virtual void OnBeatmapInfoUpdated()
        {
        }

        [UIValue("map_jd_rt")]
        public string MapJumpDistanceLabel => Config.rt_display_enabled ? "Map JD and RT" : "Map JD";

        [UIValue("map_default_jd")]
        public string MapJumpDistance
        {
            get
            {
                if (Config.rt_display_enabled)
                {
                    return JumpDistanceMath.FormatJumpDistanceWithReactionTime(SelectedBeatmap.JumpDistance, SelectedBeatmap.NJS);
                }

                return "<#ffff00>" + JumpDistanceMath.FormatJumpDistance(SelectedBeatmap.JumpDistance);
            }
        }

        [UIValue("map_min_jd")]
        public string MapMinJumpDistance
        {
            get
            {
                if (Config.rt_display_enabled)
                {
                    return "<#8c8c8c>" + JumpDistanceMath.FormatJumpDistance(SelectedBeatmap.MinJumpDistance)
                         + "     <#8c8c8c>" + JumpDistanceMath.FormatReactionTime(SelectedBeatmap.MinReactionTime);
                }

                return "<#8c8c8c>" + JumpDistanceMath.FormatJumpDistance(SelectedBeatmap.MinJumpDistance);
            }
        }
    }
}
