using HarmonyLib;
using IPA.Config.Stores;
using IPA.Config;
using IPA.Loader;
using IPA;
using IPALogger = IPA.Logging.Logger;
using JDFixer.Configuration;
using JDFixer.Installers;
using SiraUtil.Zenject;

namespace JDFixer
{
    [Plugin(RuntimeOptions.DynamicInit)]
    public sealed class Plugin
    {
        internal static Harmony harmony;

        internal static IPALogger Log { get; private set; }

        /// <summary>
        /// The mod version as <c>major.minor.patch</c>, read from the assembly so it cannot drift from
        /// <c>Properties/AssemblyInfo.cs</c> or the manifest. Two .bsml files and the mod settings tab used
        /// to carry their own hardcoded copies.
        /// </summary>
        internal static string VersionString => typeof(Plugin).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

        [Init]
        public Plugin(IPALogger logger, Config conf, Zenjector zenjector)
        {
            Log = logger;
            PluginConfig.Instance = conf.Generated<PluginConfig>();
            PluginConfig.Instance.OnLoad();

            zenjector.Install<JDFixerMenuInstaller>(Location.Menu);
        }


        [OnEnable]
        public void OnApplicationStart()
        {
            harmony = new Harmony("com.zephyr.BeatSaber.JDFixer");
            harmony.PatchAll(System.Reflection.Assembly.GetExecutingAssembly());
            CheckForCustomCampaigns();
            UI.Donate.Refresh();
        }


        [OnDisable]
        public void OnApplicationQuit()
        {
            // Both are set during [Init]; guard anyway, because an exception thrown here during shutdown
            // would leave the mod patched with no way to report it.
            PluginConfig.Instance?.Changed();
            harmony?.UnpatchSelf();
        }


        /// <summary>Whether CustomCampaigns is installed. Drives which mission-selection handler is used.</summary>
        internal static bool CheckForCustomCampaigns()
        {
            var cc_installed = PluginManager.GetPluginFromId("CustomCampaigns");
            Log.Debug("CC installed: " + (cc_installed != null));

            return cc_installed != null;
        }
    }
}
