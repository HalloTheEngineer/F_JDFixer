using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.GameplaySetup;
using HMUI;
using JDFixer.Core;
using UnityEngine;

namespace JDFixer.UI
{
    /// <summary>
    /// The Tournament Assistant and multiplayer tab.
    /// </summary>
    /// <remarks>
    /// There is no selected map in these modes, so the sliders work in raw configured units rather than
    /// being converted against a map's NJS, and the slider captions grey out when an automated
    /// preference is overriding them.
    /// </remarks>
    internal sealed class CustomOnlineUI : ModifierUIBase
    {
        private CustomOnlineUI(
            MainFlowCoordinator mainFlowCoordinator,
            PreferencesFlowCoordinator preferencesFlowCoordinator)
            : base(mainFlowCoordinator, preferencesFlowCoordinator)
        {
        }

        protected override string TabName => "JDFixer-TA/MP";

        protected override string BsmlResource => "JDFixer.UI.BSML.customOnlineUI.bsml";

        protected override MenuType MenuTypes => MenuType.Custom | MenuType.Online;

        // No map is selected here, so the raw configured value is shown and stored without conversion.

        protected override float GetJdDisplayValue() => Config.jumpDistance;

        protected override void ApplyJdDisplayValue(float value) => Config.jumpDistance = value;

        protected override float GetRtDisplayValue() => Config.reactionTime;

        protected override void ApplyRtDisplayValue(float value) => Config.reactionTime = value;

        private static bool PreferenceActive =>
            Config.use_jd_pref != Configuration.PluginConfig.NoPreference
            || Config.use_rt_pref != Configuration.PluginConfig.NoPreference;

        [UIValue("jd_text")]
        public string JdText => Describe("Desired Jump Distance", (int)SliderUnit.JumpDistance);

        [UIValue("rt_text")]
        public string RtText => Describe("Desired Reaction Time", (int)SliderUnit.ReactionTime);

        /// <summary>
        /// Dims the caption of whichever slider is not currently authoritative: either because a
        /// preference is overriding both, or because the user is working in the other unit.
        /// </summary>
        private static string Describe(string activeLabel, int sliderSetting)
        {
            if (PreferenceActive)
            {
                return "<#555555dd>Inactive " + (sliderSetting == (int)SliderUnit.JumpDistance ? "JD" : "RT");
            }

            string label = sliderSetting == (int)SliderUnit.JumpDistance ? "Desired Jump Distance" : "Desired Reaction Time";
            return Config.slider_setting == sliderSetting ? label : "<#555555dd>" + label;
        }

        private static readonly Color Inactive = new Color(0.3f, 0.3f, 0.3f);
        private static readonly Color JdActive = new Color(1f, 1f, 0f);
        private static readonly Color RtActive = new Color(204f / 255f, 153f / 255f, 1f);

        /// <inheritdoc/>
        protected override void OnPostParse()
        {
            if (JdSlider == null || RtSlider == null)
            {
                return;
            }

            var jdText = JdSlider.Slider.GetComponentInChildren<CurvedTextMeshPro>();
            var rtText = RtSlider.Slider.GetComponentInChildren<CurvedTextMeshPro>();

            if (jdText != null && rtText != null)
            {
                if (PreferenceActive)
                {
                    jdText.color = Inactive;
                    rtText.color = Inactive;
                }
                else if (Config.slider_setting == (int)SliderUnit.JumpDistance)
                {
                    jdText.color = JdActive;
                    rtText.color = Inactive;
                }
                else
                {
                    jdText.color = Inactive;
                    rtText.color = RtActive;
                }
            }

            RaiseAll(nameof(JdText), nameof(RtText), nameof(JdValue), nameof(RtValue));
        }

        /// <inheritdoc/>
        protected override void OnSliderSettingChanged() => RaiseAll(nameof(JdText), nameof(RtText));

        /// <inheritdoc/>
        protected override void OnPreferenceSelected() => RaiseAll(nameof(JdText), nameof(RtText));
    }
}
