using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace GoingDark.Client
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("com.SPT.core", "4.1.0")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.senze.goingdark";
        public const string PluginName = "Going Dark";
        public const string PluginVersion = "0.1.2";

        internal static ManualLogSource Log { get; private set; }

        private BlackoutController _blackoutController;
        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;
            Settings.Bind(Config);

            _blackoutController = new BlackoutController(Logger);
            BlackoutController.Instance = _blackoutController;

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll();

            Logger.LogInfo($"{PluginName} {PluginVersion} loaded. Permanent blackout mode is enabled.");
        }

        private void Update()
        {
            _blackoutController.Tick();
        }

        private void OnDestroy()
        {
            _blackoutController.Reset();
            BlackoutController.Instance = null;
            _harmony?.UnpatchSelf();
        }
    }
}
