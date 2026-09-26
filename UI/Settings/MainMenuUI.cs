using System;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components.Settings;
using HMUI;
using JDFixer.Configuration;
using JDFixer.Core;
using Zenject;

namespace JDFixer.UI
{
    internal sealed class MainMenuUI : IInitializable, IDisposable
    {
        /// <summary>
        /// Absolute limits for the slider range settings, independent of the min/max each one is shown
        /// with. The slider already constrains the value; this is the backstop that stops a hand-edited
        /// config from producing a nonsensical range.
        /// </summary>
        private const int MinJdLimit = 2;
        private const int MaxJdLimit = 60;
        private const int MinRtLimit = 50;
        private const int MaxRtLimit = 4000;

        private readonly MainFlowCoordinator _mainFlow;
        private readonly DonateFlowCoordinator _donateFlow;

        [Inject]
        private MainMenuUI(MainFlowCoordinator mainFlowCoordinator, DonateFlowCoordinator donateFlowCoordinator)
        {
            _mainFlow = mainFlowCoordinator;
            _donateFlow = donateFlowCoordinator;
        }

        public void Initialize()
        {
            BeatSaberMarkupLanguage.Settings.BSMLSettings.Instance.AddSettingsMenu("JDFixer", "JDFixer.UI.BSML.mainMenuUI.bsml", this);
        }

        public void Dispose()
        {
            if (BeatSaberMarkupLanguage.Settings.BSMLSettings.Instance != null)
            {
                BeatSaberMarkupLanguage.Settings.BSMLSettings.Instance.RemoveSettingsMenu(this);
            }
        }

        // =====================================================================================
        // Display.
        // =====================================================================================

        [UIValue("rt_display_value")]
        private bool RT_Display_Value
        {
            get => PluginConfig.Instance.rt_display_enabled;
            set
            {
                PluginConfig.Instance.rt_display_enabled = value;
                Flush();
            }
        }
        [UIAction("set_rt_display")]
        private void Set_RT_Display(bool value)
        {
            RT_Display_Value = value;
        }


        [UIValue("legacy_display_value")]
        private bool Legacy_Display_Value
        {
            get => PluginConfig.Instance.legacy_display_enabled;
            set
            {
                PluginConfig.Instance.legacy_display_enabled = value;
                Flush();
            }
        }
        [UIAction("set_legacy_display")]
        private void Set_Legacy_Display(bool value)
        {
            Legacy_Display_Value = value;
        }


        // --- Beat-offset snapping. The gameplay patch only snaps when both this and
        // --- legacy_display_enabled are set, so the control is only offered when it can actually do
        // --- anything. The stored value is left alone while it is unavailable, so switching separate
        // --- sliders back on restores the previous choice rather than silently clearing it.

        [UIValue("offset_available")]
        private bool Offset_Available => PluginConfig.Instance.legacy_display_enabled;

        [UIValue("use_offset_value")]
        private bool Use_Offset_Value
        {
            get => PluginConfig.Instance.use_offset;
            set
            {
                PluginConfig.Instance.use_offset = value;
                Flush();
            }
        }
        [UIAction("set_use_offset")]
        private void Set_Use_Offset(bool value)
        {
            Use_Offset_Value = value;
        }

        [UIValue("show_offset_fraction")]
        private bool Show_Offset_Fraction => PluginConfig.Instance.use_offset;

        [UIValue("offset_fraction_value")]
        private float Offset_Fraction_Value
        {
            get => PluginConfig.Instance.offset_fraction;
            set
            {
                PluginConfig.Instance.offset_fraction = Math.Max(PluginConfig.MinOffsetFraction, value);
                Flush();
            }
        }
        [UIAction("set_offset_fraction")]
        private void Set_Offset_Fraction(float value)
        {
            Offset_Fraction_Value = value;
        }

        // =====================================================================================
        // Slider range.
        //
        // These bound three separate things at once: the sliders in the mod tab, the sliders in the
        // preference editor, and the range the gameplay patch will accept. They were previously only
        // reachable by hand-editing JDFixer.json.
        // =====================================================================================

        [UIValue("min_jd_value")]
        private float Min_Jd_Value
        {
            get => PluginConfig.Instance.minJumpDistance;
            set
            {
                SliderRange range = new SliderRange(
                        PluginConfig.Instance.minJumpDistance,
                        PluginConfig.Instance.maxJumpDistance)
                    .WithMin((int)Math.Round(value), MinJdLimit, MaxJdLimit);

                PluginConfig.Instance.minJumpDistance = range.Min;
                PluginConfig.Instance.maxJumpDistance = range.Max;

                Flush();
            }
        }
        [UIAction("set_min_jd")]
        private void Set_Min_Jd(float value)
        {
            Min_Jd_Value = value;
        }

        [UIValue("max_jd_value")]
        private float Max_Jd_Value
        {
            get => PluginConfig.Instance.maxJumpDistance;
            set
            {
                SliderRange range = new SliderRange(
                        PluginConfig.Instance.minJumpDistance,
                        PluginConfig.Instance.maxJumpDistance)
                    .WithMax((int)Math.Round(value), MinJdLimit, MaxJdLimit);

                PluginConfig.Instance.minJumpDistance = range.Min;
                PluginConfig.Instance.maxJumpDistance = range.Max;

                Flush();
            }
        }
        [UIAction("set_max_jd")]
        private void Set_Max_Jd(float value)
        {
            Max_Jd_Value = value;
        }

        [UIValue("min_rt_value")]
        private float Min_Rt_Value
        {
            get => PluginConfig.Instance.minReactionTime;
            set
            {
                SliderRange range = new SliderRange(
                        PluginConfig.Instance.minReactionTime,
                        PluginConfig.Instance.maxReactionTime)
                    .WithMin((int)Math.Round(value), MinRtLimit, MaxRtLimit);

                PluginConfig.Instance.minReactionTime = range.Min;
                PluginConfig.Instance.maxReactionTime = range.Max;

                Flush();
            }
        }
        [UIAction("set_min_rt")]
        private void Set_Min_Rt(float value)
        {
            Min_Rt_Value = value;
        }

        [UIValue("max_rt_value")]
        private float Max_Rt_Value
        {
            get => PluginConfig.Instance.maxReactionTime;
            set
            {
                SliderRange range = new SliderRange(
                        PluginConfig.Instance.minReactionTime,
                        PluginConfig.Instance.maxReactionTime)
                    .WithMax((int)Math.Round(value), MinRtLimit, MaxRtLimit);

                PluginConfig.Instance.minReactionTime = range.Min;
                PluginConfig.Instance.maxReactionTime = range.Max;

                Flush();
            }
        }
        [UIAction("set_max_rt")]
        private void Set_Max_Rt(float value)
        {
            Max_Rt_Value = value;
        }

        // =====================================================================================
        // Song speed.
        // =====================================================================================

        [UIValue("song_speed_increment_value")]
        private int Song_Speed_Increment_Value
        {
            get => PluginConfig.Instance.song_speed_setting;
            set
            {
                PluginConfig.Instance.song_speed_setting = value;
                Flush();
            }
        }
        [UIAction("song_speed_increment_formatter")]
        private string Song_Speed_Increment_Formatter(int value) => Core.EnumDisplay.Name(value, typeof(Core.SongSpeedEnum));

        // =====================================================================================
        // Automated preference thresholds.
        // =====================================================================================

        [UIValue("lower_threshold_value")]
        private float Lower_Threshold_Value
        {
            get => PluginConfig.Instance.lower_threshold;
            set
            {
                PluginConfig.Instance.lower_threshold = value;
                Flush();
            }
        }
        [UIAction("set_lower_threshold")]
        private void Set_Lower_Threshold(float value)
        {
            Lower_Threshold_Value = value;
        }


        [UIValue("upper_threshold_value")]
        private float Upper_Threshold_Value
        {
            get => PluginConfig.Instance.upper_threshold;
            set
            {
                PluginConfig.Instance.upper_threshold = value;
                Flush();
            }
        }
        [UIAction("set_upper_threshold")]
        private void Set_Upper_Threshold(float value)
        {
            Upper_Threshold_Value = value;
        }

        // --- Note presentation. Each is applied live, so no OK press is needed.

        [UIValue("instant_note_rotation_value")]
        private bool Instant_Note_Rotation_Value
        {
            get => PluginConfig.Instance.instant_note_rotation;
            set
            {
                PluginConfig.Instance.instant_note_rotation = value;
                Flush();
            }
        }
        [UIAction("set_instant_note_rotation")]
        private void Set_Instant_Note_Rotation(bool value)
        {
            Instant_Note_Rotation_Value = value;
        }


        [UIValue("disable_look_ahead_value")]
        private bool Disable_Look_Ahead_Value
        {
            get => PluginConfig.Instance.disable_look_ahead;
            set
            {
                PluginConfig.Instance.disable_look_ahead = value;
                Flush();
            }
        }
        [UIAction("set_disable_look_ahead")]
        private void Set_Disable_Look_Ahead(bool value)
        {
            Disable_Look_Ahead_Value = value;
        }


        [UIValue("njs_event_compensation_value")]
        private bool Njs_Event_Compensation_Value
        {
            get => PluginConfig.Instance.njs_event_compensation;
            set
            {
                PluginConfig.Instance.njs_event_compensation = value;
                Flush();
            }
        }
        [UIAction("set_njs_event_compensation")]
        private void Set_Njs_Event_Compensation(bool value)
        {
            Njs_Event_Compensation_Value = value;
        }


        [UIValue("remove_pause_value")]
        private bool Remove_Pause_Value
        {
            get => PluginConfig.Instance.remove_pause;
            set
            {
                PluginConfig.Instance.remove_pause = value;
                Flush();
            }
        }
        [UIAction("set_remove_pause")]
        private void Set_Remove_Pause(bool value)
        {
            Remove_Pause_Value = value;
        }

        // =====================================================================================
        // Footer.
        // =====================================================================================

        // --- Donate. The link lived on the gameplay tab until it was moved here: a pink appeal in the
        // --- corner of the mod tab was clutter, and this is a calmer place to ask. The screen itself is
        // --- still the flow built for it, presented from whichever flow is currently showing the menu.

        [UIValue("donate_text")]
        public string DonateText => Donate.DonateClickableText;

        [UIValue("donate_hint")]
        public string DonateHint => Donate.DonateClickableHint;

        [UIAction("open_donate")]
        public void OpenDonate()
        {
            Donate.Refresh();

            var currentFlow = _mainFlow.YoungestChildFlowCoordinatorOrSelf();
            _donateFlow._parentFlow = currentFlow;
            currentFlow.PresentFlowCoordinator(_donateFlow);
        }

        [UIValue("press_ok_text_1")]
        private string Press_Ok_Text_1 => "<#ffffffff>Settings apply as you change them  <#ff0080ff>♡";
        [UIValue("press_ok_text_2")]
        private string Press_Ok_Text_2 => "<size=70%><#ff0080ff>v" + Plugin.VersionString + " by Zephyr9125";
        [UIValue("press_ok_hint_2")]
        private string Press_Ok_Hint_2 => string.Empty;

        /// <summary>
        /// Writes the config to disk and re-runs its validation.
        /// </summary>
        /// <remarks>
        /// Without this the settings tab only reached disk when the game quit, and the range guards above
        /// would not be re-checked until then.
        /// </remarks>
        private static void Flush() => PluginConfig.Instance?.Changed();
    }
}
