using BepInEx.Configuration;
using UnityEngine;

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
        internal static ConfigEntry<bool> DiagnosticMode;
        internal static ConfigEntry<KeyboardShortcut> CaptureDiagnosticTarget;
        internal static ConfigEntry<KeyboardShortcut> LiveTestDiagnosticTarget;
        internal static ConfigEntry<KeyboardShortcut> SurfaceTestDiagnosticTarget;

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

            DiagnosticMode = config.Bind(
                "4. Diagnostics",
                "Diagnostic Mode",
                false,
                "Enable targeted renderer capture and temporary live tests during a raid.");

            CaptureDiagnosticTarget = config.Bind(
                "4. Diagnostics",
                "Capture Target",
                new KeyboardShortcut(KeyCode.F7),
                "Capture and briefly blink the map renderer under the crosshair, then add it to the current raid report.");

            LiveTestDiagnosticTarget = config.Bind(
                "4. Diagnostics",
                "Live Test Target",
                new KeyboardShortcut(KeyCode.F8),
                "Turn off emission-like properties on the selected renderer. Press again to blink it briefly.");

            SurfaceTestDiagnosticTarget = config.Bind(
                "4. Diagnostics",
                "Surface Test Target",
                new KeyboardShortcut(KeyCode.F9),
                "Cycle reversible tests for base color, reflections and the main texture on the selected renderer.");
        }
    }
}
