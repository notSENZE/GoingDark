using System;
using System.Collections.Generic;
using BepInEx.Logging;
using Comfort.Common;
using EFT;
using EFT.Interactive;
using UnityEngine;

namespace GoingDark.Client
{
    internal sealed class BlackoutController
    {
        private const int EmissionRenderersPerFrame = 75;

        private static readonly int EmissionVisibilityId = Shader.PropertyToID("_EmissionVisibility");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private readonly ManualLogSource _log;
        private readonly HashSet<int> _knownLampControllers = new HashSet<int>();
        private readonly HashSet<int> _knownLampGroups = new HashSet<int>();
        private readonly HashSet<int> _knownSceneLights = new HashSet<int>();
        private readonly HashSet<int> _knownCustomLights = new HashSet<int>();
        private readonly MaterialPropertyBlock _materialPropertyBlock = new MaterialPropertyBlock();

        private int _worldInstanceId;
        private string _locationId;
        private MeshRenderer[] _emissionRenderers;
        private int _nextEmissionRenderer;
        private int _emissionRendererCount;
        private int _emissionMaterialCount;
        private bool _blackoutActive;
        private bool _sweepRequired;
        private bool _blackoutApplied;

        internal BlackoutController(ManualLogSource log)
        {
            _log = log;
        }

        internal static BlackoutController Instance { get; set; }

        internal void Tick()
        {
            if (!Singleton<GameWorld>.Instantiated)
            {
                Reset();
                return;
            }

            var world = Singleton<GameWorld>.Instance;
            if (world == null || world.MainPlayer == null)
            {
                return;
            }

            var worldInstanceId = world.GetInstanceID();
            if (_worldInstanceId != worldInstanceId)
            {
                BeginRaid(world);
            }

            var shouldBeActive = Settings.Enabled.Value && IsEnabledMap(_locationId);
            if (shouldBeActive != _blackoutActive)
            {
                _blackoutActive = shouldBeActive;
                if (_blackoutActive)
                {
                    _sweepRequired = true;
                    _log.LogInfo($"Permanent blackout activated on {_locationId}.");
                }
                else if (_blackoutApplied)
                {
                    _emissionRenderers = null;
                    _log.LogInfo("Blackout disabled. Previously changed scene lights reset when the raid ends.");
                }
            }

            if (!_blackoutActive)
            {
                return;
            }

            if (_sweepRequired)
            {
                ApplyBlackout();
                _sweepRequired = false;
                _blackoutApplied = true;
            }

            ProcessEmissionBatch();
        }

        internal bool ShouldBlock(Component component)
        {
            return _blackoutActive && !MapLightFilter.ShouldPreserve(component);
        }

        internal void Reset()
        {
            _worldInstanceId = 0;
            _locationId = null;
            _blackoutActive = false;
            _sweepRequired = false;
            _blackoutApplied = false;
            _knownLampControllers.Clear();
            _knownLampGroups.Clear();
            _knownSceneLights.Clear();
            _knownCustomLights.Clear();
            _emissionRenderers = null;
            _nextEmissionRenderer = 0;
            _emissionRendererCount = 0;
            _emissionMaterialCount = 0;
        }

        private void BeginRaid(GameWorld world)
        {
            Reset();
            _worldInstanceId = world.GetInstanceID();
            _locationId = world.LocationId ?? string.Empty;
        }

        private void ApplyBlackout()
        {
            var newLampControllers = DisableLampControllers();
            var newLampGroups = DisableLampGroups();
            var newSceneLights = 0;
            var newCustomLights = 0;

            if (Settings.DisableUnmanagedSceneLights.Value)
            {
                newSceneLights = DisableUnmanagedSceneLights();
                newCustomLights = DisableUnmanagedCustomLights();
            }

            BeginEmissionSweep();

            _log.LogInfo(
                $"Blackout applied on {_locationId}: "
                + $"lamp controllers={_knownLampControllers.Count} (+{newLampControllers}), "
                + $"lamp groups={_knownLampGroups.Count} (+{newLampGroups}), "
                + $"scene lights={_knownSceneLights.Count} (+{newSceneLights}), "
                + $"custom lights={_knownCustomLights.Count} (+{newCustomLights}).");
        }

        private int DisableLampControllers()
        {
            var newCount = 0;
            var lampControllers = UnityEngine.Object.FindObjectsOfType<LampController>();
            foreach (var lampController in lampControllers)
            {
                if (!ShouldBlock(lampController))
                {
                    continue;
                }

                lampController.TurnLights(false, true);
                if (_knownLampControllers.Add(lampController.GetInstanceID()))
                {
                    newCount++;
                }
            }

            return newCount;
        }

        private int DisableLampGroups()
        {
            var newCount = 0;
            var lampGroups = UnityEngine.Object.FindObjectsOfType<ControlledLampGroup>();
            foreach (var lampGroup in lampGroups)
            {
                if (!ShouldBlock(lampGroup))
                {
                    continue;
                }

                lampGroup.SetLightStateExternal(false);
                if (_knownLampGroups.Add(lampGroup.GetInstanceID()))
                {
                    newCount++;
                }
            }

            return newCount;
        }

        private int DisableUnmanagedSceneLights()
        {
            var newCount = 0;
            var lights = UnityEngine.Object.FindObjectsOfType<Light>();
            foreach (var light in lights)
            {
                if (light.type == LightType.Directional
                    || !MapLightFilter.IsSceneMapComponent(light))
                {
                    continue;
                }

                light.enabled = false;
                if (_knownSceneLights.Add(light.GetInstanceID()))
                {
                    newCount++;
                }
            }

            return newCount;
        }

        private int DisableUnmanagedCustomLights()
        {
            var newCount = 0;
            newCount += DisableBehaviours(UnityEngine.Object.FindObjectsOfType<AdvancedLight>());
            newCount += DisableBehaviours(UnityEngine.Object.FindObjectsOfType<BaseLight>());
            newCount += DisableVolumetricLights();
            newCount += DisableBehaviours(UnityEngine.Object.FindObjectsOfType<SpotLightFakeGI>());
            newCount += DisableBehaviours(UnityEngine.Object.FindObjectsOfType<CustomLight>());
            return newCount;
        }

        private int DisableVolumetricLights()
        {
            var newCount = 0;
            var volumetricLights = UnityEngine.Object.FindObjectsOfType<VolumetricLight>();
            foreach (var volumetricLight in volumetricLights)
            {
                var sourceLight = volumetricLight.GetComponent<Light>();
                if (sourceLight != null && sourceLight.type == LightType.Directional)
                {
                    continue;
                }

                if (!MapLightFilter.IsSceneMapComponent(volumetricLight))
                {
                    continue;
                }

                volumetricLight.enabled = false;
                if (_knownCustomLights.Add(volumetricLight.GetInstanceID()))
                {
                    newCount++;
                }
            }

            return newCount;
        }

        private int DisableBehaviours<T>(T[] behaviours) where T : Behaviour
        {
            var newCount = 0;
            foreach (var behaviour in behaviours)
            {
                if (!MapLightFilter.IsSceneMapComponent(behaviour))
                {
                    continue;
                }

                behaviour.enabled = false;
                if (_knownCustomLights.Add(behaviour.GetInstanceID()))
                {
                    newCount++;
                }
            }

            return newCount;
        }

        private void BeginEmissionSweep()
        {
            if (!Settings.DisableEmissiveMapMaterials.Value)
            {
                _emissionRenderers = null;
                return;
            }

            _emissionRenderers = UnityEngine.Object.FindObjectsOfType<MeshRenderer>();
            _nextEmissionRenderer = 0;
            _emissionRendererCount = 0;
            _emissionMaterialCount = 0;
        }

        private void ProcessEmissionBatch()
        {
            if (_emissionRenderers == null)
            {
                return;
            }

            var endIndex = Math.Min(
                _nextEmissionRenderer + EmissionRenderersPerFrame,
                _emissionRenderers.Length);

            while (_nextEmissionRenderer < endIndex)
            {
                DisableRendererEmission(_emissionRenderers[_nextEmissionRenderer]);
                _nextEmissionRenderer++;
            }

            if (_nextEmissionRenderer < _emissionRenderers.Length)
            {
                return;
            }

            _log.LogInfo(
                $"Emission blackout applied on {_locationId}: "
                + $"renderers={_emissionRendererCount}, materials={_emissionMaterialCount}.");
            _emissionRenderers = null;
        }

        private void DisableRendererEmission(MeshRenderer renderer)
        {
            if (!MapLightFilter.IsSceneMapComponent(renderer))
            {
                return;
            }

            var rendererChanged = false;
            var materials = renderer.sharedMaterials;
            for (var materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                var material = materials[materialIndex];
                if (material == null)
                {
                    continue;
                }

                var hasEmissionVisibility = material.HasProperty(EmissionVisibilityId);
                var hasEmissionColor = material.HasProperty(EmissionColorId);
                if (!hasEmissionVisibility && !hasEmissionColor)
                {
                    continue;
                }

                _materialPropertyBlock.Clear();
                renderer.GetPropertyBlock(_materialPropertyBlock, materialIndex);

                var visibilityActive = hasEmissionVisibility
                    && (material.GetFloat(EmissionVisibilityId) > 0.001f
                        || _materialPropertyBlock.GetFloat(EmissionVisibilityId) > 0.001f);
                var colorActive = hasEmissionColor
                    && (HasVisibleColor(material.GetColor(EmissionColorId))
                        || HasVisibleColor(_materialPropertyBlock.GetColor(EmissionColorId)));

                if (!visibilityActive && !colorActive)
                {
                    continue;
                }

                if (hasEmissionVisibility)
                {
                    _materialPropertyBlock.SetFloat(EmissionVisibilityId, 0f);
                }

                if (hasEmissionColor)
                {
                    _materialPropertyBlock.SetColor(EmissionColorId, Color.black);
                }

                renderer.SetPropertyBlock(_materialPropertyBlock, materialIndex);
                rendererChanged = true;
                _emissionMaterialCount++;
            }

            if (rendererChanged)
            {
                _emissionRendererCount++;
            }
        }

        private static bool HasVisibleColor(Color color)
        {
            return color.r > 0.001f || color.g > 0.001f || color.b > 0.001f;
        }

        private static bool IsEnabledMap(string locationId)
        {
            if (string.Equals(locationId, "TarkovStreets", StringComparison.OrdinalIgnoreCase))
            {
                return Settings.StreetsEnabled.Value;
            }

            if (string.Equals(locationId, "Sandbox", StringComparison.OrdinalIgnoreCase)
                || string.Equals(locationId, "Sandbox_high", StringComparison.OrdinalIgnoreCase))
            {
                return Settings.GroundZeroEnabled.Value;
            }

            return false;
        }
    }
}
