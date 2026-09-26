using System;
using System.Collections.Generic;
using System.Globalization;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.Components.Settings;
using BeatSaberMarkupLanguage.ViewControllers;
using HMUI;
using JDFixer.Configuration;

namespace JDFixer.UI
{
    /// <summary>
    /// Shared implementation of the NJS-keyed preference editor used for both jump distance and reaction
    /// time. The two concrete controllers differ only in which config table they edit, how they label
    /// the value column, and how a cell is formatted.
    /// </summary>
    /// <remarks>
    /// The two previous implementations were 71% identical line for line and had already drifted: the
    /// cell label was culture-sensitive in both, and the RT one had a formatter the JD one lacked.
    /// <para>
    /// All indexing goes through <see cref="ActiveTable"/>, which returns an empty list rather than
    /// throwing. The old code indexed <c>preferredValues_jd[use_jd_pref]</c> directly, and
    /// <c>use_jd_pref</c> is only guaranteed to be -1 or in range by config validation - a stale -1
    /// reaching this screen produced an out-of-range exception.
    /// </para>
    /// </remarks>
    /// <remarks>
    /// BSML members bound here are <c>public</c>/<c>protected</c>, never <c>private</c>: BSML resolves
    /// bindings by reflection on the <em>concrete</em> type it is handed, and reflection does not
    /// surface private members inherited from a base class. Declaring them private here made every one of
    /// them invisible to the two derived controllers, which BSML reports only as a parse failure.
    /// </remarks>
    internal abstract class PreferencesListViewController<TPref> : BSMLResourceViewController
        where TPref : class
    {
        public override string ResourceName => "JDFixer.UI.BSML.preferencesList.bsml";

        // --- Provided by the concrete controller.

        /// <summary>All preference tables, or an empty list when none are configured.</summary>
        protected abstract List<List<TPref>> Tables { get; }

        /// <summary>Index of the table being edited, or -1 when none is active.</summary>
        protected abstract int ActiveIndex { get; }

        protected abstract string ValueLabel { get; }
        protected abstract string HeaderText { get; }
        protected abstract string HeaderColor { get; }
        protected abstract float ValueIncrement { get; }
        protected abstract float MinSliderValue { get; }
        protected abstract float MaxSliderValue { get; }
        protected abstract float DefaultNjs { get; }
        protected abstract float DefaultValue { get; }

        protected abstract TPref CreateEntry(float njs, float value);
        protected abstract float GetNjs(TPref entry);
        protected abstract float GetValue(TPref entry);
        protected abstract string FormatCell(TPref entry);

        /// <summary>
        /// The active table, or a detached empty list when the index is out of range. Callers may mutate
        /// the result of <see cref="ActiveTable"/> only when <see cref="HasActiveTable"/> is true.
        /// </summary>
        protected List<TPref> ActiveTable
        {
            get
            {
                var tables = Tables;
                int index = ActiveIndex;

                if (tables == null || index < 0 || index >= tables.Count)
                {
                    return new List<TPref>();
                }

                return tables[index] ?? (tables[index] = new List<TPref>());
            }
        }

        protected bool HasActiveTable
        {
            get
            {
                var tables = Tables;
                int index = ActiveIndex;
                return tables != null && index >= 0 && index < tables.Count;
            }
        }

        // --- State.

        [UIComponent("njs_slider")]
        protected SliderSetting _njsSlider;

        [UIComponent("value_slider")]
        protected SliderSetting _valueSlider;

        [UIComponent("pref_list")]
        protected CustomListTableData _prefList;

        private float _newNjs = 16f;
        private float _newValue;
        private TPref _selected;

        // --- BSML bindings.

        [UIValue("header_text")]
        public string HeaderTextValue => HeaderText;

        [UIValue("header_color")]
        public string HeaderColorValue => HeaderColor;

        [UIValue("value_label")]
        public string ValueLabelValue => ValueLabel;

        [UIValue("value_increment")]
        public float ValueIncrementValue => ValueIncrement;

        [UIValue("min_slider")]
        public float MinSlider => MinSliderValue;

        [UIValue("max_slider")]
        public float MaxSlider => MaxSliderValue;

        [UIValue("njs_value")]
        public float NjsValue
        {
            get => _newNjs;
            set => _newNjs = value;
        }

        [UIAction("set_njs_value")]
        public void SetNjsValue(float value) => _newNjs = value;

        [UIValue("value")]
        public float Value
        {
            get => _newValue;
            set => _newValue = value;
        }

        [UIAction("set_value")]
        public void SetValue(float value) => _newValue = value;

        [UIAction("value_formatter")]
        public string ValueFormatter(float value) => FormatValue(value);

        [UIAction("select_pref")]
        public void SelectPref(TableView tableView, int row)
        {
            var table = ActiveTable;
            _selected = row >= 0 && row < table.Count ? table[row] : null;
        }

        [UIAction("add_pressed")]
        public void AddPressed()
        {
            if (!HasActiveTable)
            {
                Plugin.Log.Warn("Cannot add a preference: no preference list is selected");
                return;
            }

            var table = ActiveTable;

            // Adding an NJS that already exists replaces it, so the list stays a function of NJS.
            table.RemoveAll(x => GetNjs(x) == _newNjs);
            table.Add(CreateEntry(_newNjs, _newValue));

            SortAndReload();
            Flush();
        }

        [UIAction("remove_pressed")]
        public void RemovePressed()
        {
            if (_selected == null)
            {
                return;
            }

            if (HasActiveTable)
            {
                ActiveTable.RemoveAll(x => ReferenceEquals(x, _selected));
            }

            SortAndReload();
            Flush();
        }

        [UIAction("#post-parse")]
        public void PostParse()
        {
            // Seed the "add" inputs from the concrete controller's defaults, so they cannot drift from
            // what a freshly created entry will look like.
            _newNjs = DefaultNjs;
            _newValue = DefaultValue;

            ApplySliderBounds();
            Reload();
        }

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);

            // Re-applied on every activation, not just on the first parse. See ApplySliderBounds.
            ApplySliderBounds();
            Reload();
        }

        /// <summary>
        /// Re-applies the value slider's bounds from the current configuration.
        /// </summary>
        /// <remarks>
        /// BSML resolves a <c>~</c>-bound <c>min</c>/<c>max</c> once, when the resource is parsed, because
        /// <c>SliderSettingHandler</c> is a plain <c>TypeHandler</c> and never registers a notify
        /// updater for them. The <c>~min_slider</c>/<c>~max_slider</c> values themselves do stay live, so
        /// without this the bound text would show the new range while the slider still stopped at the old
        /// one.
        /// <para>
        /// That matters because these bounds come from the slider range settings in Mod Settings, and this
        /// controller is bound as a singleton whose resource is parsed only on its first activation. Left
        /// alone, the editor would keep offering whatever range happened to be configured the very first
        /// time it was opened in the session.
        /// </para>
        /// </remarks>
        private void ApplySliderBounds()
        {
            if (_valueSlider?.Slider == null)
            {
                return;
            }

            _valueSlider.Slider.minValue = MinSliderValue;
            _valueSlider.Slider.maxValue = MaxSliderValue;

            // A range narrowed in Mod Settings can leave the seeded value outside it, and the slider would
            // then clamp the displayed value without telling us, so the pending value is moved inside and
            // the binding re-read.
            float clamped = Math.Min(Math.Max(_newValue, MinSliderValue), MaxSliderValue);
            if (clamped != _newValue)
            {
                _newValue = clamped;
                NotifyPropertyChanged(nameof(Value));
            }
        }

        protected override void DidDeactivate(bool removedFromHierarchy, bool screenSystemDisabling)
        {
            base.DidDeactivate(removedFromHierarchy, screenSystemDisabling);
            Flush();
        }

        private void SortAndReload()
        {
            if (HasActiveTable)
            {
                // Descending by NJS so the list reads naturally, and so the "equal or lower" wording in
                // the description is visually obvious.
                ActiveTable.Sort((a, b) => GetNjs(b).CompareTo(GetNjs(a)));
            }

            Reload();
        }

        private void Reload()
        {
            if (_prefList == null)
            {
                return;
            }

            _prefList.Data.Clear();

            foreach (var entry in ActiveTable)
            {
                _prefList.Data.Add(new CustomListTableData.CustomCellInfo(FormatCell(entry)));
            }

            _prefList.TableView.ReloadData();
            _prefList.TableView.ClearSelection();
            _selected = null;
        }

        /// <summary>
        /// Push the config to disk. Without this, adding or removing a preference is only persisted when
        /// the app quits, so a crash loses the edit.
        /// </summary>
        private static void Flush() => PluginConfig.Instance?.Changed();

        /// <summary>Formats a value for the slider's inline label.</summary>
        protected virtual string FormatValue(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
