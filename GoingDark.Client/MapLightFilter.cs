using System;
using EFT;
using EFT.Interactive;
using UnityEngine;

namespace GoingDark.Client
{
    internal static class MapLightFilter
    {
        internal static bool ShouldPreserve(Component component)
        {
            if (component == null)
            {
                return true;
            }

            if (component.GetComponentInParent<Player>() != null)
            {
                return true;
            }

            if (component.GetComponentInParent<CandleSwitcher>() != null
                || component.GetComponentInParent<GasLamp>() != null
                || HasNonElectricalLightName(component.transform))
            {
                return true;
            }

            if (!Settings.PreserveExtractionLights.Value)
            {
                return false;
            }

            if (component.GetComponentInParent<ExfiltrationPoint>() != null)
            {
                return true;
            }

            return HasExtractionName(component.transform);
        }

        internal static bool IsSceneMapComponent(Component component)
        {
            if (component == null || !component.gameObject.scene.IsValid())
            {
                return false;
            }

            return !ShouldPreserve(component);
        }

        private static bool HasExtractionName(Transform transform)
        {
            var current = transform;
            while (current != null)
            {
                var name = current.name;
                if (name.IndexOf("exfil", StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("extract", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private static bool HasNonElectricalLightName(Transform transform)
        {
            var current = transform;
            while (current != null)
            {
                var name = current.name;
                if (name.IndexOf("bonfire", StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("campfire", StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("candle", StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("fireplace", StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("flame", StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("gaslamp", StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("chemlight", StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("chem_light", StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("chem light", StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("glowstick", StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("glow_stick", StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("glow stick", StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("lightstick", StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("light_stick", StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("light stick", StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("cyalume", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }
    }
}
