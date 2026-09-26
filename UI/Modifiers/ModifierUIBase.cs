using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components.Settings;
using BeatSaberMarkupLanguage.GameplaySetup;
using HMUI;
using JDFixer.Configuration;
using JDFixer.Core;
using JDFixer.Interfaces;
using Zenject;

namespace JDFixer.UI
{
    /// <summary>
    /// Shared implementation of the two JDFixer gameplay-setup tabs (Solo/Campaign and TA/MP).
    /// </summary>
    /// <remarks>
    /// <para>
    /// These were three near-copies: <c>ModifierUI</c> and the now-merged <c>LegacyModifierUI</c> shared
    /// 369 identical lines, and <c>Set_Preference_Mode</c> alone was 27 verbatim lines in each. They had
    /// already drifted - the threshold caption had gained a space in one copy, and <c>Refresh</c> had
    /// three different bodies. Both layouts now live in the single <c>ModifierUI</c> tab.
    /// </para>
    /// <para>
    /// BSML members bound here are <c>public</c> rather than <c>private</c>. BSML resolves
    /// <c>[UIValue]</c> and <c>[UIAction]</c> by reflection on the <em>concrete</em> type, and
    /// <c>BindingFlags.NonPublic</c> does not surface private members inherited from a base class. The
    /// containing classes stay <c>internal sealed</c>, so the wider accessibility is not part of any
    /// public surface.
    /// </para>
    /// </remarks>
    internal abstract class ModifierUIBase : IInitializable, IDisposable, INotifyPropertyChanged, IRefreshable
    {
        private readonly MainFlowCoordinator _mainFlow;
        private readonly PreferencesFlowCoordinator _prefFlow;

        /// <summary>Cached per-name, because every raise previously allocated a fresh instance.</summary>
        private readonly Dictionary<string, PropertyChangedEventArgs> _argsCache = new Dictionary<string, PropertyChangedEventArgs>();

        /// <summary>Shorthand for the config singleton, which every bound value reads.</summary>
        protected static PluginConfig Config => PluginConfig.Instance;

        // --- Provided by the concrete tab.

        /// <summary>Tab title in the gameplay setup menu.</summary>
        protected abstract string TabName { get; }

        /// <summary>Embedded BSML resource for this tab.</summary>
        protected abstract string BsmlResource { get; }

        /// <summary>Which menu types the tab appears under.</summary>
        protected abstract MenuType MenuTypes { get; }

        /// <summary>
        /// Lowest selectable jump distance. Overridden where a map is selected, because the map's own
        /// NJS sets the reachable range.
        /// </summary>
        protected virtual float MinJdSlider => Config.minJumpDistance;

        /// <inheritdoc cref="MinJdSlider"/>
        protected virtual float MaxJdSlider => Config.maxJumpDistance;

        /// <inheritdoc cref="MinJdSlider"/>
        protected virtual float MinRtSlider => Config.minReactionTime;

        /// <inheritdoc cref="MinJdSlider"/>
        protected virtual float MaxRtSlider => Config.maxReactionTime;

        /// <summary>
        /// The NJS used to convert between jump distance and reaction time.
        /// </summary>
        /// <remarks>
        /// There is no map in the TA/MP tab, so that tab overrides the accessors below to work in raw
        /// configured units and never needs this. It defaults to the game's own fallback NJS purely so
        /// the value is always finite.
        /// </remarks>
        protected virtual float DisplayNjs => JumpDistanceMath.FallbackNoteJumpMovementSpeed;

        /// <summary>Value shown on the jump distance slider.</summary>
        protected virtual float GetJdDisplayValue() =>
            Config.slider_setting == (int)SliderUnit.ReactionTime
                ? JumpDistanceMath.CalculateJumpDistanceFromReactionTime(Config.reactionTime, DisplayNjs)
                : Config.jumpDistance;

        /// <summary>Applies a jump distance dragged on the slider to whichever unit is active.</summary>
        protected virtual void ApplyJdDisplayValue(float value)
        {
            if (Config.slider_setting == (int)SliderUnit.JumpDistance)
            {
                Config.jumpDistance = value;
            }
            else
            {
                Config.reactionTime = JumpDistanceMath.CalculateReactionTime(value, DisplayNjs);
            }
        }

        /// <summary>Value shown on the reaction time slider.</summary>
        protected virtual float GetRtDisplayValue() =>
            Config.slider_setting == (int)SliderUnit.JumpDistance
                ? JumpDistanceMath.CalculateReactionTime(Config.jumpDistance, DisplayNjs)
                : Config.reactionTime;

        /// <summary>Applies a reaction time dragged on the slider to whichever unit is active.</summary>
        protected virtual void ApplyRtDisplayValue(float value)
        {
            if (Config.slider_setting == (int)SliderUnit.JumpDistance)
            {
                Config.jumpDistance = JumpDistanceMath.CalculateJumpDistanceFromReactionTime(value, DisplayNjs);
            }
            else
            {
                Config.reactionTime = value;
            }
        }

        // --- Hooks for the concrete tab.

        /// <summary>Runs after the standard refresh raises.</summary>
        protected virtual void OnRefresh()
        {
        }

        /// <summary>Runs when the user switches between the JD and RT sliders.</summary>
        protected virtual void OnSliderSettingChanged()
        {
        }

        /// <summary>Runs when the heuristic toggle changes.</summary>
        protected virtual void OnHeuristicChanged()
        {
        }

        /// <summary>Runs after a new automated preference is selected.</summary>
        protected virtual void OnPreferenceSelected()
        {
        }

        /// <summary>
        /// Runs on BSML <c>#post-parse</c> and whenever the slider unit changes. Concrete tabs override
        /// to restyle the slider captions and re-apply their bounds.
        /// </summary>
        protected virtual void OnPostParse()
        {
        }

        protected ModifierUIBase(MainFlowCoordinator mainFlowCoordinator, PreferencesFlowCoordinator preferencesFlowCoordinator)
        {
            _mainFlow = mainFlowCoordinator;
            _prefFlow = preferencesFlowCoordinator;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Raises <see cref="PropertyChanged"/> for several bound names, reusing cached argument
        /// objects. The previous code raised them one at a time, allocating a
        /// <see cref="PropertyChangedEventArgs"/> each time - around forty per map selection.
        /// </summary>
        protected void RaiseAll(params string[] names)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler == null)
            {
                return;
            }

            foreach (string name in names)
            {
                if (!_argsCache.TryGetValue(name, out PropertyChangedEventArgs args))
                {
                    args = new PropertyChangedEventArgs(name);
                    _argsCache.Add(name, args);
                }

                handler(this, args);
            }
        }

        public void Initialize()
        {
            GameplaySetup.Instance?.AddTab(TabName, BsmlResource, this, MenuTypes);
        }

        public void Dispose()
        {
            if (GameplaySetup.Instance == null)
            {
                return;
            }

            Config?.Changed();
            GameplaySetup.Instance.RemoveTab(TabName);
        }

        void IRefreshable.Refresh() => Refresh();

        internal void Refresh()
        {
            RaiseAll(
                nameof(SliderSettingValue),
                nameof(IncrementValue),
                nameof(PrefButton),
                nameof(HeuristicIncrementValue));

            OnRefresh();
        }

        // =====================================================================================
        // BSML contract shared by both tabs.
        // =====================================================================================

        [UIValue("enabled")]
        public bool Enabled
        {
            get => Config.enabled;
            set => Config.enabled = value;
        }

        [UIAction("set_enabled")]
        public void SetEnabled(bool value) => Enabled = value;

        [UIValue("slider_setting_value")]
        public int SliderSettingValue
        {
            get => Config.slider_setting;
            set
            {
                Config.slider_setting = value;
                RaiseAll(nameof(SliderSettingValue), nameof(Thresholds), nameof(IncrementValue));

                OnSliderSettingChanged();
                OnPostParse();
            }
        }

        [UIAction("slider_setting_increment_formatter")]
        public string SliderSettingIncrementFormatter(int value) => EnumDisplay.Name(value, typeof(SliderUnitEnum));

        [UIValue("min_jd_slider")]
        public float MinJdSliderValue => MinJdSlider;

        [UIValue("max_jd_slider")]
        public float MaxJdSliderValue => MaxJdSlider;

        [UIComponent("jd_slider")]
        protected SliderSetting JdSlider { get; set; }

        [UIValue("jd_value")]
        public float JdValue
        {
            get => GetJdDisplayValue();
            set
            {
                ApplyJdDisplayValue(value);
                RaiseAll(nameof(JdValue), nameof(RtValue));
            }
        }

        [UIAction("set_jd_value")]
        public void SetJdValue(float value) => JdValue = value;

        [UIAction("jd_slider_formatter")]
        public string JdSliderFormatter(float value) => JumpDistanceMath.FormatJumpDistance(value);

        [UIValue("min_rt_slider")]
        public float MinRtSliderValue => MinRtSlider;

        [UIValue("max_rt_slider")]
        public float MaxRtSliderValue => MaxRtSlider;

        [UIComponent("rt_slider")]
        protected SliderSetting RtSlider { get; set; }

        [UIValue("rt_value")]
        public float RtValue
        {
            get => GetRtDisplayValue();
            set
            {
                ApplyRtDisplayValue(value);
                RaiseAll(nameof(JdValue), nameof(RtValue));
            }
        }

        [UIAction("set_rt_value")]
        public void SetRtValue(float value) => RtValue = value;

        [UIAction("rt_slider_formatter")]
        public string RtSliderFormatter(float value) => JumpDistanceMath.FormatReactionTime(value);

        [UIValue("increment_value")]
        public int IncrementValue
        {
            get => Config.pref_selected;
            set
            {
                Config.pref_selected = value;
                RaiseAll(nameof(IncrementValue), nameof(PrefButton));

                ApplyPreferenceMode();
                OnPreferenceSelected();
                OnPostParse();
            }
        }

        [UIAction("increment_formatter")]
        public string IncrementFormatter(int value) => FormatPreferenceLabel(Config.pref_selected);

        [UIValue("prefs_max")]
        public float PrefsMax => PreferenceCountBound();

        [UIValue("pref_button")]
        public string PrefButton => FormatPreferenceButton(Config.pref_selected);

        [UIAction("pref_button_clicked")]
        public void PrefButtonClicked()
        {
            // Beat Games provides YoungestChildFlowCoordinatorOrSelf on FlowCoordinator, so the current
            // flow can hand itself to the preferences flow and be dismissed back into.
            var currentFlow = _mainFlow.YoungestChildFlowCoordinatorOrSelf();
            _prefFlow._parentFlow = currentFlow;
            currentFlow.PresentFlowCoordinator(_prefFlow);
        }

        [UIValue("heuristic_increment_value")]
        public int HeuristicIncrementValue
        {
            get => Config.use_heuristic;
            set
            {
                Config.use_heuristic = value;
                RaiseAll(nameof(HeuristicIncrementValue));

                OnHeuristicChanged();
                OnPostParse();
            }
        }

        [UIAction("heuristic_increment_formatter")]
        public string HeuristicIncrementFormatter(int value) => EnumDisplay.Name(value, typeof(HeuristicEnum));

        [UIValue("thresholds")]
        public string Thresholds => string.Format(
            CultureInfo.InvariantCulture,
            "≤ {0} or ≥ {1}",
            JumpDistanceMath.FormatPlainFloat(Config.lower_threshold),
            JumpDistanceMath.FormatPlainFloat(Config.upper_threshold));

        // The footer this tab used to carry is gone: the version line, the changelog line, and the pink
        // donate link. All of it moved to the settings panel (MainMenuUI), which is the calmer place for
        // it. Nothing in the gameplay tab needs the fetched donate text any more either.

        [UIAction("#post-parse")]
        public void PostParse() => OnPostParse();

        // =====================================================================================
        // Preference selection, shared by both tabs.
        // =====================================================================================

        /// <summary>
        /// Maps the single "which preference is active" selector onto the JD and RT preference indices.
        /// The selector is 1-based over <c>jdCount + rtCount</c> entries, with 0 meaning "none".
        /// </summary>
        /// <remarks>
        /// Indices are clamped rather than trusted. <c>pref_selected</c> comes from the config file, and
        /// an out-of-range value used to produce <c>use_jd_pref = -2</c>, which then threw from inside a
        /// Harmony patch as the map started.
        /// </remarks>
        private void ApplyPreferenceMode()
        {
            int jdCount = Config.preferredValues_jd?.Count ?? 0;
            int rtCount = Config.preferredValues_rt?.Count ?? 0;
            int total = jdCount + rtCount;

            int selected = Config.pref_selected;
            if (selected < 0 || selected > total)
            {
                selected = 0;
                Config.pref_selected = 0;
            }

            if (selected == 0)
            {
                Config.use_jd_pref = PluginConfig.NoPreference;
                Config.use_rt_pref = PluginConfig.NoPreference;
            }
            else if (selected <= jdCount)
            {
                Config.use_jd_pref = selected - 1;
                Config.use_rt_pref = PluginConfig.NoPreference;
            }
            else
            {
                Config.use_jd_pref = PluginConfig.NoPreference;
                Config.use_rt_pref = selected - jdCount - 1;
            }
        }

        /// <summary>
        /// Upper bound for the preference selector. Never returns less than one, so the increment control
        /// is always usable even with no preferences configured.
        /// </summary>
        private static float PreferenceCountBound()
        {
            int jdCount = Config.preferredValues_jd?.Count ?? 0;
            int rtCount = Config.preferredValues_rt?.Count ?? 0;
            return Math.Max(1, jdCount + rtCount);
        }

        private static string FormatPreferenceLabel(int selectedEntry)
        {
            int jdCount = Config.preferredValues_jd?.Count ?? 0;
            if (selectedEntry == 0)
            {
                return "None";
            }

            if ((selectedEntry == 1 && jdCount == 0) || selectedEntry <= jdCount)
            {
                return "<#ffff00>[JD] " + selectedEntry.ToString(CultureInfo.InvariantCulture);
            }

            return "<#cc99ff>[RT] " + (selectedEntry - jdCount).ToString(CultureInfo.InvariantCulture);
        }

        private static string FormatPreferenceButton(int selectedEntry)
        {
            int jdCount = Config.preferredValues_jd?.Count ?? 0;

            if (selectedEntry == 0)
            {
                return "Configure  JD  and  RT  Preferences";
            }

            if ((selectedEntry == 1 && jdCount == 0) || selectedEntry <= jdCount)
            {
                return "<#00000000>----<#ffff00>Configure  JD  Preferences<#00000000>----";
            }

            return "<#00000000>----<#cc99ff>Configure  RT  Preferences<#00000000>----";
        }
    }
}
