using System.Collections.Generic;

namespace JDFixer.Core
{
    /// <summary>Which quantity the sliders are working in.</summary>
    internal enum SliderUnitEnum
    {
        JumpDistance = 0,
        ReactionTime = 1,
    }

    /// <summary>Whether the "play the map at its own value if that is lower" heuristic is on.</summary>
    internal enum HeuristicEnum
    {
        Off = 0,
        On = 1,
    }

    /// <summary>Which setpoint is authoritative when the song speed is changed.</summary>
    internal enum SongSpeedEnum
    {
        JumpDistance = 0,
        ReactionTime = 1,
        Respectively = 2,
    }

    /// <summary>
    /// Display names for the increment controls.
    /// </summary>
    /// <remarks>
    /// These used to come straight from <c>Enum.ToString()</c>, which rendered the raw identifiers into
    /// the UI - including <c>JD_RT_Respectively</c>, with its underscores.
    /// </remarks>
    internal static class EnumDisplay
    {
        private static readonly Dictionary<System.Enum, string> Names = new Dictionary<System.Enum, string>
        {
            { SliderUnitEnum.JumpDistance, "Jump Distance" },
            { SliderUnitEnum.ReactionTime, "Reaction Time" },
            { HeuristicEnum.Off, "Off" },
            { HeuristicEnum.On, "On" },
            { SongSpeedEnum.JumpDistance, "Jump Distance" },
            { SongSpeedEnum.ReactionTime, "Reaction Time" },
            { SongSpeedEnum.Respectively, "JD or RT (Whichever Is Selected)" },
        };

        /// <summary>Human-readable name, falling back to the identifier for an unknown value.</summary>
        internal static string Name(int value, System.Type enumType)
        {
            try
            {
                return Name((System.Enum)System.Enum.ToObject(enumType, value));
            }
            catch (System.ArgumentException)
            {
                // Value is not defined for this enum, e.g. a hand-edited config.
                return string.Empty;
            }
        }

        internal static string Name(System.Enum value)
        {
            if (value != null && Names.TryGetValue(value, out string name))
            {
                return name;
            }

            return value?.ToString() ?? string.Empty;
        }
    }
}
