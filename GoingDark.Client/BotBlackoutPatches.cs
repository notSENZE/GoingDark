using System;
using EFT;
using HarmonyLib;

namespace GoingDark.Client
{
    internal static class LookSensorBlackoutPatchState
    {
        internal static readonly AccessTools.FieldRef<LookSensor, float> NextVisibleDistanceUpdate =
            AccessTools.FieldRefAccess<LookSensor, float>("_nextUpdateVisibleDist");

        [ThreadStatic]
        internal static bool CalculatingVisibleDistance;

        [ThreadStatic]
        internal static bool ApplyingFinalMultiplier;
    }

    [HarmonyPatch(typeof(LookSensor), nameof(LookSensor.CalcVisibleDistance))]
    internal static class LookSensorVisibleDistanceCalculationPatch
    {
        [HarmonyPrefix]
        private static void Prefix(LookSensor __instance, out float __state)
        {
            __state = LookSensorBlackoutPatchState.NextVisibleDistanceUpdate(__instance);
            LookSensorBlackoutPatchState.CalculatingVisibleDistance = true;
        }

        [HarmonyPostfix]
        private static void Postfix(LookSensor __instance, float __state)
        {
            LookSensorBlackoutPatchState.CalculatingVisibleDistance = false;
            if (LookSensorBlackoutPatchState.NextVisibleDistanceUpdate(__instance) == __state)
            {
                return;
            }

            var bot = Traverse.Create(__instance)
                .Field("_botOwner")
                .GetValue<BotOwner>();
            var multiplier = BotBlackoutEffects.VisionDistanceMultiplier(bot);
            if (multiplier >= 1f)
            {
                return;
            }

            LookSensorBlackoutPatchState.ApplyingFinalMultiplier = true;
            try
            {
                __instance.VisibleDist *= multiplier;
            }
            finally
            {
                LookSensorBlackoutPatchState.ApplyingFinalMultiplier = false;
            }
        }

        [HarmonyFinalizer]
        private static Exception Finalizer(Exception __exception)
        {
            LookSensorBlackoutPatchState.CalculatingVisibleDistance = false;
            LookSensorBlackoutPatchState.ApplyingFinalMultiplier = false;
            return __exception;
        }
    }

    [HarmonyPatch(typeof(LookSensor), "set_VisibleDist")]
    internal static class LookSensorVisibleDistancePatch
    {
        [HarmonyPrefix]
        private static void Prefix(LookSensor __instance, ref float value)
        {
            if (LookSensorBlackoutPatchState.CalculatingVisibleDistance
                || LookSensorBlackoutPatchState.ApplyingFinalMultiplier)
            {
                return;
            }

            var bot = Traverse.Create(__instance)
                .Field("_botOwner")
                .GetValue<BotOwner>();
            value *= BotBlackoutEffects.VisionDistanceMultiplier(bot);
        }
    }

    [HarmonyPatch(typeof(EnemyInfo), "GetVisibilityChangeSpeedK")]
    internal static class EnemyVisibilityChangeSpeedPatch
    {
        [HarmonyPostfix]
        private static void Postfix(EnemyInfo __instance, ref float __result)
        {
            __result *= BotBlackoutEffects.RecognitionMultiplier(__instance);
        }
    }
}
