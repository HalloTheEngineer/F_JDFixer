using System.Collections.Generic;
using System.Globalization;
using JDFixer.Configuration;

namespace JDFixer.UI
{
    /// <summary>Editor for the NJS-keyed jump-distance preferences.</summary>
    internal sealed class JDPreferencesListViewController : PreferencesListViewController<JDPref>
    {
        protected override List<List<JDPref>> Tables => PluginConfig.Instance.preferredValues_jd;

        protected override int ActiveIndex => PluginConfig.Instance.use_jd_pref;

        protected override string ValueLabel => "Desired Jump Distance";

        protected override string HeaderText =>
            "Maps will automatically run using the [NJS | JD] setting that is equal or lower than the " +
            "selected map's NJS. For constant JD across all maps, use [0 NJS | Desired JD]";

        protected override string HeaderColor => "#ffff00ff";

        protected override float ValueIncrement => 0.1f;

        protected override float MinSliderValue => PluginConfig.Instance.minJumpDistance;

        protected override float MaxSliderValue => PluginConfig.Instance.maxJumpDistance;

        protected override float DefaultNjs => 16f;

        protected override float DefaultValue => 18f;

        protected override JDPref CreateEntry(float njs, float value) => new JDPref(njs, value);

        protected override float GetNjs(JDPref entry) => entry.njs;

        protected override float GetValue(JDPref entry) => entry.jumpDistance;

        protected override string FormatCell(JDPref entry) => string.Format(
            CultureInfo.InvariantCulture,
            "{0} NJS | {1} Jump Distance",
            GetNjs(entry).ToString("0.##", CultureInfo.InvariantCulture),
            GetValue(entry).ToString("0.##", CultureInfo.InvariantCulture));
    }
}
