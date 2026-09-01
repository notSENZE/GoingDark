using System;
using System.Collections.Generic;
using BepInEx.Logging;
using Comfort.Common;
using EFT;
using EFT.Interactive;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GoingDark.Client
{
    internal sealed class BlackoutController
    {
        private const int EmissionRenderersPerFrame = 75;
        private const int ActiveRenderersCheckedPerFrame = 2000;
        private const int RendererHierarchyNodesPerFrame = 150;
        private const string CardinalEmissionMaterialName = "City_emissive_Cardinal";
        private const string TerminalEmissionMaterialName = "terminal_emissive_bsod";
        private const string GroundZeroBillboardMaterialName = "InterchangeGlitch_1";
        private const string GroundZeroOasisScreenMaterialName = "Sandbox_Oasis_Glitch";
        private const string GroundZeroNeonWomenRendererName = "lab_sign_neon_women_LOD0";
        private const string ColorPaletteEmissionMaterialName = "Color_palette_emissive";
        private const string ColorPaletteMaterialName = "Color_palette";

        private static readonly int EmissionVisibilityId = Shader.PropertyToID("_EmissionVisibility");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int EmissiveColorId = Shader.PropertyToID("_EmissiveColor");
        private static readonly int EmissionPowerId = Shader.PropertyToID("_EmissionPower");
        private static readonly int EmissionMapId = Shader.PropertyToID("_EmissionMap");
        private static readonly int SpecMapId = Shader.PropertyToID("_SpecMap");
        private static readonly int SpecColorId = Shader.PropertyToID("_SpecColor");
        private static readonly int GlossnessId = Shader.PropertyToID("_Glossness");
        private static readonly int SpecularnessId = Shader.PropertyToID("_Specularness");
        private static readonly int ReflectColorId = Shader.PropertyToID("_ReflectColor");
        private static readonly int SpecValuesId = Shader.PropertyToID("_SpecVals");

        private readonly ManualLogSource _log;
        private readonly HashSet<int> _knownLampControllers = new HashSet<int>();
        private readonly HashSet<int> _knownLampGroups = new HashSet<int>();
        private readonly HashSet<int> _knownSceneLights = new HashSet<int>();
        private readonly HashSet<int> _knownCustomLights = new HashSet<int>();
        private readonly HashSet<int> _seenEmissionRenderers = new HashSet<int>();
        private readonly MaterialPropertyBlock _materialPropertyBlock = new MaterialPropertyBlock();

        private int _worldInstanceId;
        private string _locationId;
        private MeshRenderer[] _activeRendererDiscovery;
        private int _nextActiveRendererDiscovery;
        private int _activeRendererDiscoveryCount;
        private Queue<MeshRenderer> _emissionRenderers;
        private Queue<RendererDiscoveryNode> _rendererDiscoveryNodes;
        private int _emissionCandidateCount;
        private int _emissionDiscoveryRootCount;
        private int _emissionDiscoveryNodeCount;
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
                    _activeRendererDiscovery = null;
                    _emissionRenderers = null;
                    _rendererDiscoveryNodes = null;
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

            ProcessActiveRendererDiscoveryBatch();
            ProcessRendererDiscoveryBatch();
            ProcessEmissionBatch();
            FinishEmissionSweepIfReady();
        }

        internal bool ShouldBlock(Component component)
        {
            return _blackoutActive && !MapLightFilter.ShouldPreserve(component);
        }

        internal void SceneLoaded(Scene scene)
        {
            if (!_blackoutActive
                || !Settings.DisableEmissiveMapMaterials.Value
                || !scene.IsValid()
                || !scene.isLoaded)
            {
                return;
            }

            EnsureEmissionSweep();
            var queuedRoots = 0;
            var rootObjects = scene.GetRootGameObjects();
            foreach (var rootObject in rootObjects)
            {
                QueueRendererHierarchy(
                    rootObject.transform,
                    IsLightHierarchyRoot(rootObject.name));
                queuedRoots++;
            }

            if (queuedRoots > 0)
            {
                _log.LogInfo(
                    $"Queued {queuedRoots} roots from late-loaded scene {scene.name} "
                    + "for incremental renderer discovery.");
            }
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
            _seenEmissionRenderers.Clear();
            _activeRendererDiscovery = null;
            _nextActiveRendererDiscovery = 0;
            _activeRendererDiscoveryCount = 0;
            _emissionRenderers = null;
            _rendererDiscoveryNodes = null;
            _emissionCandidateCount = 0;
            _emissionDiscoveryRootCount = 0;
            _emissionDiscoveryNodeCount = 0;
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
                _rendererDiscoveryNodes = null;
                _activeRendererDiscovery = null;
                return;
            }

            _seenEmissionRenderers.Clear();
            StartEmissionSweep();
            _activeRendererDiscovery = UnityEngine.Object.FindObjectsOfType<MeshRenderer>();
            _nextActiveRendererDiscovery = 0;
            _activeRendererDiscoveryCount = _activeRendererDiscovery.Length;
            QueueInactiveLightHierarchies();
        }

        private void StartEmissionSweep()
        {
            _emissionRenderers = new Queue<MeshRenderer>();
            _rendererDiscoveryNodes = new Queue<RendererDiscoveryNode>();
            _activeRendererDiscovery = null;
            _nextActiveRendererDiscovery = 0;
            _activeRendererDiscoveryCount = 0;
            _emissionCandidateCount = 0;
            _emissionDiscoveryRootCount = 0;
            _emissionDiscoveryNodeCount = 0;
            _emissionRendererCount = 0;
            _emissionMaterialCount = 0;
        }

        private void EnsureEmissionSweep()
        {
            if (_emissionRenderers == null || _rendererDiscoveryNodes == null)
            {
                StartEmissionSweep();
            }
        }

        private void QueueInactiveLightHierarchies()
        {
            for (var sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                var scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    continue;
                }

                var rootObjects = scene.GetRootGameObjects();
                foreach (var rootObject in rootObjects)
                {
                    if (IsLightHierarchyRoot(rootObject.name))
                    {
                        QueueRendererHierarchy(rootObject.transform, true);
                    }
                }
            }
        }

        private void QueueRendererHierarchy(Transform root, bool includeInactive)
        {
            if (root == null)
            {
                return;
            }

            EnsureEmissionSweep();
            _rendererDiscoveryNodes.Enqueue(new RendererDiscoveryNode(root, includeInactive));
            _emissionDiscoveryRootCount++;
        }

        private void ProcessActiveRendererDiscoveryBatch()
        {
            if (_activeRendererDiscovery == null)
            {
                return;
            }

            var endIndex = Math.Min(
                _nextActiveRendererDiscovery + ActiveRenderersCheckedPerFrame,
                _activeRendererDiscovery.Length);
            while (_nextActiveRendererDiscovery < endIndex)
            {
                var renderer = _activeRendererDiscovery[_nextActiveRendererDiscovery];
                if (renderer != null && HasEmissionProperties(renderer))
                {
                    QueueEmissionRenderer(renderer);
                }

                _nextActiveRendererDiscovery++;
            }

            if (_nextActiveRendererDiscovery >= _activeRendererDiscovery.Length)
            {
                _activeRendererDiscovery = null;
            }
        }

        private bool QueueEmissionRenderer(MeshRenderer renderer)
        {
            if (renderer == null || !_seenEmissionRenderers.Add(renderer.GetInstanceID()))
            {
                return false;
            }

            EnsureEmissionSweep();
            _emissionRenderers.Enqueue(renderer);
            _emissionCandidateCount++;
            return true;
        }

        private void ProcessRendererDiscoveryBatch()
        {
            if (_rendererDiscoveryNodes == null)
            {
                return;
            }

            var processedNodes = 0;
            while (_rendererDiscoveryNodes.Count > 0
                && processedNodes < RendererHierarchyNodesPerFrame)
            {
                var node = _rendererDiscoveryNodes.Dequeue();
                var transform = node.Transform;
                processedNodes++;
                _emissionDiscoveryNodeCount++;

                if (transform == null
                    || (!node.IncludeInactive && !transform.gameObject.activeInHierarchy))
                {
                    continue;
                }

                var renderer = transform.GetComponent<MeshRenderer>();
                if (renderer != null
                    && (renderer.gameObject.activeInHierarchy
                        || HasEmissionProperties(renderer)))
                {
                    QueueEmissionRenderer(renderer);
                }

                for (var childIndex = 0; childIndex < transform.childCount; childIndex++)
                {
                    _rendererDiscoveryNodes.Enqueue(
                        new RendererDiscoveryNode(transform.GetChild(childIndex), node.IncludeInactive));
                }
            }
        }

        private void ProcessEmissionBatch()
        {
            if (_emissionRenderers == null)
            {
                return;
            }

            var processedRenderers = 0;
            while (_emissionRenderers.Count > 0
                && processedRenderers < EmissionRenderersPerFrame)
            {
                DisableRendererEmission(_emissionRenderers.Dequeue());
                processedRenderers++;
            }

        }

        private void FinishEmissionSweepIfReady()
        {
            if (_emissionRenderers == null
                || _rendererDiscoveryNodes == null
                || _activeRendererDiscovery != null
                || _emissionRenderers.Count > 0
                || _rendererDiscoveryNodes.Count > 0)
            {
                return;
            }

            _log.LogInfo(
                $"Emission blackout applied on {_locationId}: "
                + $"active renderers checked={_activeRendererDiscoveryCount}, "
                + $"discovery roots={_emissionDiscoveryRootCount}, "
                + $"discovery nodes={_emissionDiscoveryNodeCount}, "
                + $"candidates={_emissionCandidateCount}, "
                + $"renderers={_emissionRendererCount}, materials={_emissionMaterialCount}.");
            _emissionRenderers = null;
            _rendererDiscoveryNodes = null;
        }

        private static bool HasEmissionProperties(MeshRenderer renderer)
        {
            var materials = renderer.sharedMaterials;
            foreach (var material in materials)
            {
                if (material != null
                    && (ShouldDisableSurfaceReflection(renderer, material)
                        || material.HasProperty(EmissionVisibilityId)
                        || material.HasProperty(EmissionColorId)
                        || material.HasProperty(EmissiveColorId)
                        || material.HasProperty(EmissionPowerId)
                        || material.HasProperty(EmissionMapId)))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsLightHierarchyRoot(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            return name.IndexOf("_light", StringComparison.OrdinalIgnoreCase) >= 0
                || name.StartsWith("light_", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "light", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "lighting", StringComparison.OrdinalIgnoreCase);
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
                var hasEmissiveColor = material.HasProperty(EmissiveColorId);
                var hasEmissionPower = material.HasProperty(EmissionPowerId);
                var hasEmissionMap = material.HasProperty(EmissionMapId);
                var disableSurfaceReflection = ShouldDisableSurfaceReflection(renderer, material);

                if (!hasEmissionVisibility
                    && !hasEmissionColor
                    && !hasEmissiveColor
                    && !hasEmissionPower
                    && !hasEmissionMap
                    && !disableSurfaceReflection)
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
                var emissiveColorActive = hasEmissiveColor
                    && (HasVisibleColor(material.GetColor(EmissiveColorId))
                        || HasVisibleColor(_materialPropertyBlock.GetColor(EmissiveColorId)));
                var powerActive = hasEmissionPower
                    && (material.GetFloat(EmissionPowerId) > 0.001f
                        || _materialPropertyBlock.GetFloat(EmissionPowerId) > 0.001f);
                var emissionMapActive = hasEmissionMap
                    && (material.GetTexture(EmissionMapId) != null
                        || _materialPropertyBlock.GetTexture(EmissionMapId) != null);

                if (!visibilityActive
                    && !colorActive
                    && !emissiveColorActive
                    && !powerActive
                    && !emissionMapActive
                    && !disableSurfaceReflection)
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

                if (hasEmissiveColor)
                {
                    _materialPropertyBlock.SetColor(EmissiveColorId, Color.black);
                }

                if (hasEmissionPower)
                {
                    _materialPropertyBlock.SetFloat(EmissionPowerId, 0f);
                }

                if (hasEmissionMap)
                {
                    _materialPropertyBlock.SetTexture(EmissionMapId, Texture2D.blackTexture);
                }

                if (disableSurfaceReflection)
                {
                    DisableSurfaceReflection(material, _materialPropertyBlock);
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

        private static bool ShouldDisableSurfaceReflection(
            MeshRenderer renderer,
            Material material)
        {
            if (string.Equals(
                    material.name,
                    CardinalEmissionMaterialName,
                    StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    material.name,
                    TerminalEmissionMaterialName,
                    StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    material.name,
                    GroundZeroBillboardMaterialName,
                    StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    material.name,
                    GroundZeroOasisScreenMaterialName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (renderer == null
                || !string.Equals(
                    renderer.name,
                    GroundZeroNeonWomenRendererName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return string.Equals(
                    material.name,
                    ColorPaletteEmissionMaterialName,
                    StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    material.name,
                    ColorPaletteMaterialName,
                    StringComparison.OrdinalIgnoreCase);
        }

        private static void DisableSurfaceReflection(
            Material material,
            MaterialPropertyBlock propertyBlock)
        {
            if (material.HasProperty(SpecMapId))
            {
                propertyBlock.SetTexture(SpecMapId, Texture2D.blackTexture);
            }

            if (material.HasProperty(SpecColorId))
            {
                propertyBlock.SetColor(SpecColorId, Color.black);
            }

            if (material.HasProperty(GlossnessId))
            {
                propertyBlock.SetFloat(GlossnessId, 0f);
            }

            if (material.HasProperty(SpecularnessId))
            {
                propertyBlock.SetFloat(SpecularnessId, 0f);
            }

            if (material.HasProperty(ReflectColorId))
            {
                propertyBlock.SetColor(ReflectColorId, Color.black);
            }

            if (material.HasProperty(SpecValuesId))
            {
                propertyBlock.SetVector(SpecValuesId, Vector4.zero);
            }
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

    internal sealed class RendererDiscoveryNode
    {
        internal RendererDiscoveryNode(Transform transform, bool includeInactive)
        {
            Transform = transform;
            IncludeInactive = includeInactive;
        }

        internal Transform Transform { get; }
        internal bool IncludeInactive { get; }
    }
}
