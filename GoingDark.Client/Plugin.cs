using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine.SceneManagement;

namespace GoingDark.Client
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("com.SPT.core", "4.1.0")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.senze.goingdark";
        public const string PluginName = "Going Dark";
        public const string PluginVersion = "0.1.8";

        internal static ManualLogSource Log { get; private set; }

        private BlackoutController _blackoutController;
        private DiagnosticController _diagnosticController;
        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;
            Settings.Bind(Config);

            _blackoutController = new BlackoutController(Logger);
            BlackoutController.Instance = _blackoutController;
            _diagnosticController = new DiagnosticController(Logger);

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll();
            SceneManager.sceneLoaded += OnSceneLoaded;

            Logger.LogInfo($"{PluginName} {PluginVersion} loaded. Permanent blackout mode is enabled.");
        }

        private void Update()
        {
            _blackoutController.Tick();
            _diagnosticController.Tick();
        }

        private void OnGUI()
        {
            _diagnosticController.DrawOverlay();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            _blackoutController.Reset();
            _diagnosticController.Reset();
            BlackoutController.Instance = null;
            _harmony?.UnpatchSelf();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _blackoutController.SceneLoaded(scene);
            _diagnosticController.SceneLoaded();
        }
    }
}
