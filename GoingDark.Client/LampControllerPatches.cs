using EFT.Interactive;
using HarmonyLib;

namespace GoingDark.Client
{
    [HarmonyPatch(typeof(LampController), nameof(LampController.TurnLights))]
    internal static class LampControllerTurnLightsPatch
    {
        private static void Prefix(LampController __instance, ref bool on)
        {
            if (on && BlackoutController.Instance?.ShouldBlock(__instance) == true)
            {
                on = false;
            }
        }
    }

    [HarmonyPatch(typeof(ControlledLampGroup), nameof(ControlledLampGroup.SetLightState))]
    internal static class ControlledLampGroupSetLightStatePatch
    {
        private static void Prefix(ControlledLampGroup __instance, ref bool state)
        {
            if (state && BlackoutController.Instance?.ShouldBlock(__instance) == true)
            {
                state = false;
            }
        }
    }
}
