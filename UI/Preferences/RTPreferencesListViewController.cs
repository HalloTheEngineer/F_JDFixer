using System.Collections.Generic;
using System.Globalization;
using JDFixer.Configuration;

namespace JDFixer.UI
{
    /// <summary>Editor for the NJS-keyed reaction-time preferences.</summary>
    internal sealed class RTPreferencesListViewController : PreferencesListViewController<RTPref>
    {
        protected override List<List<RTPref>> Tables => PluginConfig.Instance.preferredValues_rt;

        protected override int ActiveIndex => PluginConfig.Instance.use_rt_pref;

        protected override string ValueLabel => "Desired Reaction Time";

        protected override string HeaderText =>
            "Maps will automatically run using the [NJS | RT] setting that is equal or lower than the " +
            "selected map's NJS. For constant RT across all maps, use [0 NJS | Desired RT]";

        protected override string HeaderColor => "#cc99ffff";

        protected override float ValueIncrement => 1f;

        protected override float MinSliderValue => PluginConfig.Instance.minReactionTime;

        protected override float MaxSliderValue => PluginConfig.Instance.maxReactionTime;

        protected override float DefaultNjs => 16f;

        protected override float DefaultValue => 800f;

        protected override RTPref CreateEntry(float njs, float value) => new RTPref(njs, value);

        protected override float GetNjs(RTPref entry) => entry.njs;

        protected override float GetValue(RTPref entry) => entry.reactionTime;

        protected override string FormatCell(RTPref entry) => string.Format(
            CultureInfo.InvariantCulture,
            "{0} NJS | {1} ms",
            GetNjs(entry).ToString("0.##", CultureInfo.InvariantCulture),
            GetValue(entry).ToString("0", CultureInfo.InvariantCulture));

        protected override string FormatValue(float value) => value.ToString("0", CultureInfo.InvariantCulture) + " ms";
    }
}
