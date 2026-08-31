using BepInEx.Configuration;

namespace GoingDark.Client
{
    internal static class Settings
    {
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<bool> StreetsEnabled;
        internal static ConfigEntry<bool> GroundZeroEnabled;
        internal static ConfigEntry<bool> DisableUnmanagedSceneLights;
        internal static ConfigEntry<bool> DisableEmissiveMapMaterials;
        internal static ConfigEntry<bool> PreserveExtractionLights;

        internal static void Bind(ConfigFile config)
        {
            Enabled = config.Bind(
                "1. General",
                "Enabled",
                true,
                "Apply a permanent infrastructure blackout in supported raids.");

            StreetsEnabled = config.Bind(
                "2. Maps",
                "Streets of Tarkov",
                true,
                "Apply the blackout on Streets of Tarkov.");

            GroundZeroEnabled = config.Bind(
                "2. Maps",
                "Ground Zero",
                true,
                "Apply the blackout on both Ground Zero level variants.");

            DisableUnmanagedSceneLights = config.Bind(
                "3. Blackout",
                "Disable Unmanaged Scene Lights",
                true,
                "Also disable scene lights that are not connected to EFT's normal lamp controllers.");

            DisableEmissiveMapMaterials = config.Bind(
                "3. Blackout",
                "Disable Emissive Map Materials",
                true,
                "Turn off powered emission on map signs, advertisements and other illuminated surfaces.");

            PreserveExtractionLights = config.Bind(
                "3. Blackout",
                "Preserve Extraction Lights",
                true,
                "Keep lights that are attached to extraction points or clearly named extraction signals.");
        }
    }
}
