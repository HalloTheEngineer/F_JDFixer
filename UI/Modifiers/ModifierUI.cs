using System;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components.Settings;
using BeatSaberMarkupLanguage.GameplaySetup;
using HMUI;
using JDFixer.Core;
using UnityEngine;

namespace JDFixer.UI
{
    /// <summary>
    /// The Solo / Campaign tab: the selected map's jump distance, the JD and RT sliders, and automated
    /// preferences.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This absorbs the former <c>LegacyModifierUI</c>. The two were 369 identical lines of markup
    /// between them and were selected by a config check at Zenject-install time, which meant the choice
    /// of layout was frozen for the whole session and a settings change could not take effect without a
    /// restart. They are now one tab that adapts: <see cref="SeparateSliders"/> decides at render time
    /// whether both sliders or one is shown, and whether the beat-offset readouts appear.
    /// </para>
    /// <para>
    /// This tab owns the per-map slider bounds, which is why it stays separate from
    /// <see cref="CustomOnlineUI"/>: in TA and MP there is no selected map, so the bounds there are just
    /// the configured ones.
    /// </para>
    /// </remarks>
    internal sealed class ModifierUI : BeatmapInfoDisplayBase
    {
        internal static readonly Color JdCaption = new Color(1f, 1f, 0f);
        internal static readonly Color RtCaption = new Color(204f / 255f, 153f / 255f, 1f);

        private CurvedTextMeshPro _jdSliderText;
        private CurvedTextMeshPro _rtSliderText;

        private ModifierUI(
            MainFlowCoordinator mainFlowCoordinator,
            PreferencesFlowCoordinator preferencesFlowController)
            : base(mainFlowCoordinator, preferencesFlowController)
        {
        }

        protected override string TabName => "JDFixer";

        protected override string BsmlResource => "JDFixer.UI.BSML.modifierUI.bsml";

        protected override MenuType MenuTypes => MenuType.Solo | MenuType.Campaign;

        // =====================================================================================
        // Display mode.
        //
        // "Separate JD and RT sliders" (formerly the legacy layout): one slider at a time, chosen by
        // the unit selector, plus the cross-unit and beat-offset readouts. Otherwise both sliders are
        // shown at once and the readouts would only restate what is already on screen.
        // =====================================================================================

        /// <summary>Offset snapping is only reachable in separate-slider mode, and only offered then.</summary>
        private static bool UseOffset => Config.use_offset;

        private static bool SeparateSliders => Config.legacy_display_enabled;

        /// <summary>Which unit the unit selector is currently pointing at.</summary>
        private static bool JdSliderActive => Config.slider_setting == (int)SliderUnit.JumpDistance;

        [UIValue("show_jd_slider")]
        public bool ShowJdSlider => !SeparateSliders || JdSliderActive;

        [UIValue("show_rt_slider")]
        public bool ShowRtSlider => !SeparateSliders || !JdSliderActive;

        // --- Beat-offset snapping readout. SnapPointSet.Nearest is a pure function of the map and the
        // --- current slider value, so these getters do not mutate shared state as a side effect of
        // --- being read. Every one of them is reached from BeatmapInfo, which is SnapPointSet.Empty
        // --- before a map is selected, so there is no null path here.

        [UIValue("show_snapped_jd")]
        public bool ShowSnappedJd => SeparateSliders && UseOffset && JdSliderActive;

        [UIValue("snapped_jd")]
        public string SnappedJd
        {
            get
            {
                SnapPoint snapped = SelectedBeatmap.JdSnapPoints.Nearest(JdValue);
                return "<#8c8c8c>" + snapped.OffsetLabel
                     + "     <#ffff00>" + JumpDistanceMath.FormatJumpDistance(snapped.Value)
                     + "     " + JumpDistanceMath.FormatJumpDistanceWithReactionTime(snapped.Value, SelectedBeatmap.NJS);
            }
        }

        [UIValue("show_snapped_rt")]
        public bool ShowSnappedRt => SeparateSliders && UseOffset && !JdSliderActive;

        [UIValue("snapped_rt")]
        public string SnappedRt
        {
            get
            {
                SnapPoint snapped = SelectedBeatmap.RtSnapPoints.Nearest(RtValue);
                return "<#8c8c8c>" + snapped.OffsetLabel
                     + "     " + JumpDistanceMath.FormatJumpDistanceForReactionTime(snapped.Value, SelectedBeatmap.NJS)
                     + "     <#cc99ff>" + JumpDistanceMath.FormatReactionTime(snapped.Value);
            }
        }

        // --- Cross-unit readouts. Only one slider is visible in separate-slider mode, so the other
        // --- unit's value is shown as text derived from the visible one.

        [UIValue("show_jd_display")]
        public bool ShowJdDisplay => SeparateSliders && !UseOffset && !JdSliderActive;

        [UIValue("jd_display")]
        public string JdDisplay => JumpDistanceMath.FormatJumpDistanceForReactionTime(RtValue, SelectedBeatmap.NJS);

        [UIValue("show_rt_display")]
        public bool ShowRtDisplay => SeparateSliders && !UseOffset && JdSliderActive;

        [UIValue("rt_display")]
        public string RtDisplay => JumpDistanceMath.FormatJumpDistanceWithReactionTime(JdValue, SelectedBeatmap.NJS);

        // --- Captions that differ between the two modes. Bound rather than switched with a separate
        // --- markup branch, so that toggling the mode never has to re-parse the tab.

        [UIValue("rt_slider_text")]
        public string RtSliderText => SeparateSliders ? "Desired Reaction Time" : "Reaction Time";

        /// <remarks>
        /// Only the caption is bound. There is deliberately no matching bound hover hint: BSML rebuilds a
        /// <c>~</c>-bound hover hint through Zenject each time the value raises, and these are raised in
        /// bulk - including while the main menu is being destroyed - which faulted
        /// <c>RectTransformHandler.AddHoverHint</c>. The markup carries one static hint instead.
        /// </remarks>
        [UIValue("slider_setting_text")]
        public string SliderSettingText =>
            SeparateSliders ? "Set slider to adjust using..." : "Remember last setting for...";

        // =====================================================================================
        // Options ported from the settings menu.
        //
        // The settings panel is the secondary place to configure JDFixer; this tab is the primary one,
        // because it is already open while choosing a map. Every control here is applied immediately and
        // writes straight to the config, so nothing needs an OK press.
        //
        // These deliberately live on this class rather than on ModifierUIBase: the map readout and the
        // display-mode switches they have to re-raise belong to BeatmapInfoDisplayBase and to this class
        // respectively, and the TA/MP tab has neither. That tab stays minimal on purpose - it exists for
        // setting a value during a tournament, not for reconfiguring the mod.
        // =====================================================================================

        [UIValue("rt_display_value")]
        public bool RtDisplayValue
        {
            get => Config.rt_display_enabled;
            set
            {
                if (Config.rt_display_enabled == value)
                {
                    return;
                }

                Config.rt_display_enabled = value;

                // The map readout switches between a JD-only and a JD+RT form.
                RaiseAll(
                    nameof(RtDisplayValue),
                    nameof(MapJumpDistanceLabel), nameof(MapJumpDistance), nameof(MapMinJumpDistance));
            }
        }

        [UIAction("set_rt_display")]
        public void SetRtDisplay(bool value) => RtDisplayValue = value;

        /// <summary>
        /// Flips between showing both sliders and showing one at a time. This is the same setting the
        /// settings menu calls "Separate JD and RT sliders", and it now drives the whole tab layout.
        /// </summary>
        [UIValue("separate_sliders_value")]
        public bool SeparateSlidersValue
        {
            get => SeparateSliders;
            set
            {
                if (Config.legacy_display_enabled == value)
                {
                    return;
                }

                Config.legacy_display_enabled = value;

                // Which unit is active decides the reachable range of both sliders, so the bounds move
                // with the layout.
                ApplySliderBounds();
                RaiseAll(
                    nameof(MinJdSliderValue), nameof(MaxJdSliderValue), nameof(JdValue),
                    nameof(MinRtSliderValue), nameof(MaxRtSliderValue), nameof(RtValue));

                RaiseDisplayModeValues();
            }
        }

        [UIAction("set_separate_sliders")]
        public void SetSeparateSliders(bool value) => SeparateSlidersValue = value;

        /// <summary>Offset snapping only does anything in separate-slider mode, so it is gated on it.</summary>
        [UIValue("show_offset_options")]
        public bool ShowOffsetOptions => SeparateSliders;

        [UIValue("use_offset_value")]
        public bool UseOffsetValue
        {
            get => Config.use_offset;
            set
            {
                if (Config.use_offset == value)
                {
                    return;
                }

                Config.use_offset = value;
                RaiseAll(nameof(UseOffsetValue));
                RaiseDisplayModeValues();
            }
        }

        [UIAction("set_use_offset")]
        public void SetUseOffset(bool value) => UseOffsetValue = value;

        [UIValue("show_offset_fraction")]
        public bool ShowOffsetFraction => Config.use_offset;

        [UIValue("offset_fraction_value")]
        public float OffsetFractionValue
        {
            get => Config.offset_fraction;
            set
            {
                float clamped = Math.Max(Configuration.PluginConfig.MinOffsetFraction, value);
                if (Config.offset_fraction.Equals(clamped))
                {
                    return;
                }

                Config.offset_fraction = clamped;

                // The snap step feeds the readout, which is only on screen while snapping is enabled.
                RaiseAll(nameof(OffsetFractionValue), nameof(SnappedJd), nameof(SnappedRt));
            }
        }

        [UIValue("song_speed_increment_value")]
        public int SongSpeedIncrementValue
        {
            get => Config.song_speed_setting;
            set
            {
                if (Config.song_speed_setting == value)
                {
                    return;
                }

                Config.song_speed_setting = value;
                RaiseAll(nameof(SongSpeedIncrementValue));
            }
        }

        [UIAction("song_speed_increment_formatter")]
        public string SongSpeedIncrementFormatter(int value) => EnumDisplay.Name(value, typeof(SongSpeedEnum));

        [UIValue("lower_threshold_value")]
        public float LowerThresholdValue
        {
            get => Config.lower_threshold;
            set
            {
                if (Config.lower_threshold.Equals(value))
                {
                    return;
                }

                Config.lower_threshold = value;
                OnThresholdChanged();
            }
        }

        [UIValue("upper_threshold_value")]
        public float UpperThresholdValue
        {
            get => Config.upper_threshold;
            set
            {
                if (Config.upper_threshold.Equals(value))
                {
                    return;
                }

                Config.upper_threshold = value;
                OnThresholdChanged();
            }
        }

        /// <summary>
        /// Re-reads the threshold summary and persists the change.
        /// </summary>
        /// <remarks>
        /// The summary renders both bounds together, so it cannot be raised per-field. The config is also
        /// flushed here rather than left to shutdown, because these values are read by the gameplay patch
        /// when a map starts, not only displayed.
        /// </remarks>
        private void OnThresholdChanged()
        {
            Config?.Changed();
            RaiseAll(nameof(Thresholds));
        }

        // --- Note presentation. Each changes how notes look or how the game behaves, and none of them
        // --- touch jump distance, so they are grouped together and applied live.

        [UIValue("instant_note_rotation_value")]
        public bool InstantNoteRotationValue
        {
            get => Config.instant_note_rotation;
            set => Config.instant_note_rotation = value;
        }

        [UIAction("set_instant_note_rotation")]
        public void SetInstantNoteRotation(bool value) => InstantNoteRotationValue = value;

        [UIValue("disable_look_ahead_value")]
        public bool DisableLookAheadValue
        {
            get => Config.disable_look_ahead;
            set => Config.disable_look_ahead = value;
        }

        [UIAction("set_disable_look_ahead")]
        public void SetDisableLookAhead(bool value) => DisableLookAheadValue = value;

        [UIValue("njs_event_compensation_value")]
        public bool NjsEventCompensationValue
        {
            get => Config.njs_event_compensation;
            set => Config.njs_event_compensation = value;
        }

        [UIAction("set_njs_event_compensation")]
        public void SetNjsEventCompensation(bool value) => NjsEventCompensationValue = value;

        [UIValue("remove_pause_value")]
        public bool RemovePauseValue
        {
            get => Config.remove_pause;
            set => Config.remove_pause = value;
        }

        [UIAction("set_remove_pause")]
        public void SetRemovePause(bool value) => RemovePauseValue = value;

        // =====================================================================================
        // Lifecycle
        // =====================================================================================

        /// <summary>
        /// Resolves the slider captions and applies the selected map's bounds.
        /// </summary>
        /// <remarks>
        /// The caption has to be found by searching the slider's children, because TextMeshPro exposes no
        /// public accessor for its own label, so it is fetched once here and cached.
        /// <para>
        /// The <em>bounds</em> need no such digging. BSML's own <c>SliderSettingHandler</c> writes them
        /// straight onto <c>SliderSetting.Slider.minValue</c> / <c>maxValue</c>, so this does the same to
        /// the same public property. An earlier version hunted for a <c>CustomFormatRangeValuesSlider</c>
        /// child instead - unnecessary, since that type only adds a <c>string.Format</c> to the displayed
        /// text - and carried two cached fields for it.
        /// </para>
        /// </remarks>
        protected override void OnPostParse()
        {
            if (JdSlider == null || RtSlider == null)
            {
                return;
            }

            SetCaption(JdSlider, out _jdSliderText, JdCaption);
            SetCaption(RtSlider, out _rtSliderText, RtCaption);

            ApplySliderBounds();

            // The bounds also feed the [UIValue]s the markup binds min/max to, so BSML has to be told to
            // re-read them even though it only consults them at parse time.
            RaiseAll(nameof(MinRtSliderValue), nameof(MaxRtSliderValue), nameof(RtValue));
            RaiseAll(nameof(MinJdSliderValue), nameof(MaxJdSliderValue), nameof(JdValue));
        }

        /// <inheritdoc/>
        protected override void OnSliderSettingChanged()
        {
            ApplySliderBounds();

            // Every value below is a function of which unit is active.
            RaiseDisplayModeValues();
        }

        /// <summary>
        /// Re-evaluates everything that depends on the display mode.
        /// </summary>
        /// <remarks>
        /// This runs on <see cref="IRefreshable.Refresh"/>, which the UI manager calls when the main menu
        /// deactivates. That is the point at which a change to "Separate JD and RT sliders" in Mod Settings
        /// becomes visible, and it is why the mode no longer has to be decided at install time.
        /// </remarks>
        protected override void OnRefresh()
        {
            ApplySliderBounds();
            RaiseDisplayModeValues();
        }

        /// <inheritdoc/>
        /// <remarks>
        /// The cross-unit readouts are derived from the map's NJS, so they change with the map just as the
        /// snapping readouts do. The former legacy tab re-raised the two <c>show_</c> flags here but not the
        /// values themselves, which left the readouts showing the previous map's numbers until something
        /// else happened to raise them.
        /// </remarks>
        protected override void OnBeatmapInfoUpdated()
        {
            RaiseAll(
                nameof(JdDisplay), nameof(RtDisplay),
                nameof(SnappedJd), nameof(SnappedRt));
        }

        private void RaiseDisplayModeValues() => RaiseAll(
            nameof(ShowOffsetOptions), nameof(ShowOffsetFraction),
            nameof(ShowJdSlider), nameof(ShowRtSlider),
            nameof(RtSliderText), nameof(SliderSettingText),
            nameof(ShowJdDisplay), nameof(JdDisplay),
            nameof(ShowRtDisplay), nameof(RtDisplay),
            nameof(ShowSnappedJd), nameof(SnappedJd),
            nameof(ShowSnappedRt), nameof(SnappedRt));

        /// <summary>
        /// Constrains each slider to the range the selected map can actually perform, so the player cannot
        /// pick a jump distance the game would silently clamp.
        /// </summary>
        /// <remarks>
        /// This has to be done in code rather than in the markup. BSML resolves a <c>~</c>-bound
        /// <c>min</c>/<c>max</c> once, when the tab is parsed, because <c>SliderSettingHandler</c> is a
        /// plain <c>TypeHandler</c> and so never registers a <c>NotifyUpdater</c> for them - unlike
        /// <c>value</c> and <c>text</c>, which are handled by a generic handler and do stay live. The
        /// bounds depend on the map's NJS, so they change whenever a different difficulty is selected, and
        /// also when the player flips between the JD and RT sliders since the two units have different
        /// reachable ranges.
        /// </remarks>
        private void ApplySliderBounds()
        {
            SetBounds(JdSlider, MinJdSlider, MaxJdSlider);
            SetBounds(RtSlider, MinRtSlider, MaxRtSlider);
        }

        private static void SetBounds(SliderSetting slider, float min, float max)
        {
            if (slider?.Slider == null)
            {
                return;
            }

            slider.Slider.minValue = min;
            slider.Slider.maxValue = max;
        }

        private static void SetCaption(SliderSetting slider, out CurvedTextMeshPro caption, Color colour)
        {
            caption = slider.Slider?.GetComponentInChildren<CurvedTextMeshPro>();
            if (caption != null)
            {
                caption.color = colour;
            }
        }
    }
}
