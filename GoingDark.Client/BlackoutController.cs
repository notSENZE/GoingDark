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
        private const int PreparationHierarchyNodesPerFrame = 500;
        private const int LightPreparationStageCount = 8;
        private const double LightBlackoutMillisecondsPerFrame = 4d;
        private const string CardinalEmissionMaterialName = "City_emissive_Cardinal";
        private const string TerminalEmissionMaterialName = "terminal_emissive_bsod";
        private const string GroundZeroBillboardMaterialName = "InterchangeGlitch_1";
        private const string GroundZeroOasisScreenMaterialName = "Sandbox_Oasis_Glitch";
        private const string GroundZeroNeonWomenRendererName = "lab_sign_neon_women_LOD0";
        private const string ColorPaletteEmissionMaterialName = "Color_palette_emissive";
        private const string ColorPaletteMaterialName = "Color_palette";
        private const string InterchangePowerSwitchId = "Shopping_Mall_DesignStuff_00055";
        private const string InterchangeAlarmSwitchId = "Shopping_Mall_DesignStuff_00056";
        private const string InterchangeSaferoomSwitchId = "Shopping_Mall_DesignStuff_00062";

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
        private readonly Action _playEventSound;
        private readonly HashSet<int> _knownLampControllers = new HashSet<int>();
        private readonly HashSet<int> _knownLampGroups = new HashSet<int>();
        private readonly HashSet<int> _knownSceneLights = new HashSet<int>();
        private readonly HashSet<int> _knownCustomLights = new HashSet<int>();
        private readonly HashSet<int> _seenEmissionRenderers = new HashSet<int>();
        private readonly HashSet<int> _preparedLampControllerIds = new HashSet<int>();
        private readonly HashSet<int> _preparedLampGroupIds = new HashSet<int>();
        private readonly HashSet<int> _preparedSceneLightIds = new HashSet<int>();
        private readonly HashSet<int> _preparedCustomLightIds = new HashSet<int>();
        private readonly Queue<LampController> _preparedLampControllers =
            new Queue<LampController>();
        private readonly Queue<ControlledLampGroup> _preparedLampGroups =
            new Queue<ControlledLampGroup>();
        private readonly Queue<Light> _preparedSceneLights = new Queue<Light>();
        private readonly Queue<Behaviour> _preparedCustomLights = new Queue<Behaviour>();
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
        private bool _raidBlackoutSelected;
        private bool _blackoutReady;
        private bool _midRaidEvent;
        private bool _playSoundOnActivation;
        private bool _emissionPreparationStarted;
        private bool _emissionPreparationComplete;
        private bool _lightPreparationStarted;
        private bool _lightPreparationComplete;
        private bool _batchedLightBlackoutActive;
        private int _lightPreparationStage;
        private int _lightBlackoutFrames;
        private double _lightBlackoutWorkMilliseconds;
        private float _midRaidTriggerSeconds;
        private float _raidReadyRealtime;
        private BlackoutMode _raidMode;
        private Switch _interchangePowerSwitch;
        private Switch _interchangeAlarmSwitch;
        private Switch _interchangeSaferoomSwitch;
        private bool _interchangePowerShutdownPending;
        private bool _interchangePowerCloseLogged;
        private bool _interchangePowerLampsDisabled;

        internal BlackoutController(ManualLogSource log, Action playEventSound)
        {
            _log = log;
            _playEventSound = playEventSound;
        }

        internal static BlackoutController Instance { get; set; }

        internal bool IsBlackoutActive => _blackoutActive;

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

            UpdateMidRaidEvent();
            ProcessEmissionPreparationBatch();
            ProcessLightPreparationStep();

            var blackoutAllowed = Settings.Enabled.Value && IsEnabledMap(_locationId);
            if (!blackoutAllowed || !_raidBlackoutSelected || !_blackoutReady)
            {
                DisableBlackout();
                return;
            }

            if (!_blackoutActive)
            {
                ActivateBlackout();
            }

            if (_sweepRequired)
            {
                if (_raidMode == BlackoutMode.MidRaidEvent)
                {
                    BeginBatchedBlackout();
                }
                else
                {
                    ApplyBlackout();
                }

                _sweepRequired = false;
                _blackoutApplied = true;
            }

            ProcessBatchedLightBlackout();
            ProcessInterchangePowerShutdown();
            ProcessActiveRendererDiscoveryBatch();
            ProcessRendererDiscoveryBatch(RendererHierarchyNodesPerFrame);
            ProcessEmissionBatch();
            FinishEmissionSweepIfReady();
        }

        internal bool ShouldBlock(Component component)
        {
            return _blackoutActive && !MapLightFilter.ShouldPreserve(component);
        }

        internal bool ShouldBlockPowerSwitch(Switch powerSwitch)
        {
            return _blackoutActive
                && _raidMode == BlackoutMode.MidRaidEvent
                && string.Equals(_locationId, "Interchange", StringComparison.OrdinalIgnoreCase)
                && IsInterchangePowerSwitch(powerSwitch);
        }

        internal void SceneLoaded(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            if (Settings.DisableEmissiveMapMaterials.Value
                && (_blackoutActive || _emissionPreparationStarted))
            {
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
                    _emissionPreparationComplete = false;
                    _log.LogInfo(
                        $"Queued {queuedRoots} roots from late-loaded scene {scene.name} "
                        + "for incremental renderer discovery.");
                }
            }

            if (_raidMode == BlackoutMode.MidRaidEvent
                && _raidBlackoutSelected
                && Settings.Enabled.Value
                && IsEnabledMap(_locationId)
                && (!_blackoutReady || _blackoutActive))
            {
                RequestLightPreparation();
                if (_blackoutActive)
                {
                    _batchedLightBlackoutActive = true;
                }
            }
        }

        internal void Reset()
        {
            _worldInstanceId = 0;
            _locationId = null;
            _blackoutActive = false;
            _sweepRequired = false;
            _blackoutApplied = false;
            _raidBlackoutSelected = false;
            _blackoutReady = false;
            _midRaidEvent = false;
            _playSoundOnActivation = false;
            _emissionPreparationStarted = false;
            _emissionPreparationComplete = false;
            _lightPreparationStarted = false;
            _lightPreparationComplete = false;
            _batchedLightBlackoutActive = false;
            _lightPreparationStage = 0;
            _lightBlackoutFrames = 0;
            _lightBlackoutWorkMilliseconds = 0d;
            _midRaidTriggerSeconds = 0f;
            _raidReadyRealtime = 0f;
            _raidMode = BlackoutMode.Permanent;
            _interchangePowerSwitch = null;
            _interchangeAlarmSwitch = null;
            _interchangeSaferoomSwitch = null;
            _interchangePowerShutdownPending = false;
            _interchangePowerCloseLogged = false;
            _interchangePowerLampsDisabled = false;
            _knownLampControllers.Clear();
            _knownLampGroups.Clear();
            _knownSceneLights.Clear();
            _knownCustomLights.Clear();
            _seenEmissionRenderers.Clear();
            _preparedLampControllerIds.Clear();
            _preparedLampGroupIds.Clear();
            _preparedSceneLightIds.Clear();
            _preparedCustomLightIds.Clear();
            _preparedLampControllers.Clear();
            _preparedLampGroups.Clear();
            _preparedSceneLights.Clear();
            _preparedCustomLights.Clear();
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
            _raidReadyRealtime = Time.realtimeSinceStartup;
            PlanRaidBlackout(world);

            if (_raidBlackoutSelected
                && _midRaidEvent
                && !_blackoutReady
                && Settings.Enabled.Value)
            {
                RequestLightPreparation();
                if (Settings.DisableEmissiveMapMaterials.Value)
                {
                    BeginEmissionPreparation();
                }
            }
        }

        private void PlanRaidBlackout(GameWorld world)
        {
            if (!IsEnabledMap(_locationId))
            {
                return;
            }

            _raidMode = Settings.Mode.Value;
            if (_raidMode == BlackoutMode.Permanent)
            {
                _raidBlackoutSelected = true;
                _blackoutReady = true;
                return;
            }

            var random = new System.Random(CreateRaidSeed(world));
            var chance = Settings.EventChance.Value;
            var roll = random.NextDouble() * 100d;
            _raidBlackoutSelected = roll < chance;
            if (!_raidBlackoutSelected)
            {
                _log.LogInfo(
                    $"Blackout event skipped on {_locationId}: "
                    + $"roll={roll:F1}, chance={chance}%.");
                return;
            }

            if (_raidMode == BlackoutMode.RaidStartEvent)
            {
                _blackoutReady = true;
                _log.LogInfo(
                    $"Raid-start blackout event selected on {_locationId}: "
                    + $"roll={roll:F1}, chance={chance}%.");
                return;
            }

            _midRaidEvent = true;
            var earliestMinute = Math.Min(
                Settings.EarliestEventMinute.Value,
                Settings.LatestEventMinute.Value);
            var latestMinute = Math.Max(
                Settings.EarliestEventMinute.Value,
                Settings.LatestEventMinute.Value);
            var triggerMinute = earliestMinute;
            if (latestMinute > earliestMinute)
            {
                triggerMinute += (float)random.NextDouble() * (latestMinute - earliestMinute);
            }

            _midRaidTriggerSeconds = triggerMinute * 60f;
            _blackoutReady = GetRaidElapsedSeconds() >= _midRaidTriggerSeconds;

            _log.LogInfo(
                $"Mid-raid blackout event selected on {_locationId}: "
                + $"trigger={triggerMinute:F1} minutes, roll={roll:F1}, chance={chance}%.");
        }

        private void UpdateMidRaidEvent()
        {
            if (!_raidBlackoutSelected || !_midRaidEvent || _blackoutReady)
            {
                return;
            }

            if (GetRaidElapsedSeconds() < _midRaidTriggerSeconds)
            {
                return;
            }

            _blackoutReady = true;
            _playSoundOnActivation = Settings.Enabled.Value && IsEnabledMap(_locationId);
        }

        private void ActivateBlackout()
        {
            _blackoutActive = true;
            _sweepRequired = true;

            if (_raidMode == BlackoutMode.MidRaidEvent)
            {
                _log.LogInfo($"Mid-raid blackout event triggered on {_locationId}.");

                if (string.Equals(
                    _locationId,
                    "Interchange",
                    StringComparison.OrdinalIgnoreCase))
                {
                    _interchangePowerShutdownPending = true;
                }
            }
            else if (_raidMode == BlackoutMode.RaidStartEvent)
            {
                _log.LogInfo($"Raid-start blackout event activated on {_locationId}.");
            }
            else
            {
                _log.LogInfo($"Permanent blackout activated on {_locationId}.");
            }

            if (_playSoundOnActivation)
            {
                _playSoundOnActivation = false;
                _playEventSound?.Invoke();
            }
        }

        private void ProcessInterchangePowerShutdown()
        {
            if (!_interchangePowerShutdownPending)
            {
                return;
            }

            if (_interchangePowerSwitch == null
                || _interchangeAlarmSwitch == null
                || _interchangeSaferoomSwitch == null)
            {
                var switches = UnityEngine.Object.FindObjectsOfType<Switch>();
                foreach (var powerSwitch in switches)
                {
                    if (IsInterchangePowerSwitch(powerSwitch))
                    {
                        _interchangePowerSwitch = powerSwitch;
                    }
                    else if (HasSwitchId(powerSwitch, InterchangeAlarmSwitchId))
                    {
                        _interchangeAlarmSwitch = powerSwitch;
                    }
                    else if (HasSwitchId(powerSwitch, InterchangeSaferoomSwitchId))
                    {
                        _interchangeSaferoomSwitch = powerSwitch;
                    }
                }

                if (_interchangePowerSwitch == null
                    || _interchangeAlarmSwitch == null
                    || _interchangeSaferoomSwitch == null)
                {
                    _interchangePowerShutdownPending = false;
                    _log.LogWarning(
                        "Could not shut down the complete Interchange grid because one or more "
                        + "power switches were not found.");
                    return;
                }
            }

            if (!_interchangePowerLampsDisabled)
            {
                _interchangePowerSwitch.SetLampsState(Turnable.EState.Off);
                _interchangePowerLampsDisabled = true;
            }

            var powerSwitchShut = EnsureSwitchIsShut(_interchangePowerSwitch);
            var alarmSwitchShut = EnsureSwitchIsShut(_interchangeAlarmSwitch);
            var saferoomSwitchShut = EnsureSwitchIsShut(_interchangeSaferoomSwitch);
            if (!_interchangePowerCloseLogged)
            {
                _interchangePowerCloseLogged = true;
                _log.LogInfo(
                    "Interchange power, alarm and saferoom switches received the grid "
                    + "shutdown command.");
            }

            if (!powerSwitchShut || !alarmSwitchShut || !saferoomSwitchShut)
            {
                return;
            }

            _interchangePowerSwitch.NoInteractionsAllowed = true;
            _interchangePowerSwitch.Operatable = false;
            _interchangePowerShutdownPending = false;
            _log.LogInfo(
                "Interchange power, alarm and saferoom switches are shut down; the main "
                + "switch is locked for the rest of the raid.");
        }

        private static bool EnsureSwitchIsShut(Switch powerSwitch)
        {
            if (powerSwitch.DoorState == EDoorState.Shut)
            {
                return true;
            }

            if (powerSwitch.DoorState != EDoorState.Interacting)
            {
                powerSwitch.Close();
            }

            return false;
        }

        private static bool IsInterchangePowerSwitch(Switch powerSwitch)
        {
            return HasSwitchId(powerSwitch, InterchangePowerSwitchId);
        }

        private static bool HasSwitchId(Switch powerSwitch, string switchId)
        {
            return powerSwitch != null
                && string.Equals(powerSwitch.Id, switchId, StringComparison.OrdinalIgnoreCase);
        }

        private void DisableBlackout()
        {
            if (!_blackoutActive)
            {
                return;
            }

            _blackoutActive = false;
            _activeRendererDiscovery = null;
            _emissionRenderers = null;
            _rendererDiscoveryNodes = null;
            _interchangePowerShutdownPending = false;
            if (_blackoutApplied)
            {
                _log.LogInfo("Blackout disabled. Previously changed scene lights reset when the raid ends.");
            }
        }

        private float GetRaidElapsedSeconds()
        {
            if (Singleton<AbstractGame>.Instantiated)
            {
                var game = Singleton<AbstractGame>.Instance;
                if (game != null)
                {
                    return Math.Max(0f, game.PastTime);
                }
            }

            return Math.Max(0f, Time.realtimeSinceStartup - _raidReadyRealtime);
        }

        private static int CreateRaidSeed(GameWorld world)
        {
            unchecked
            {
                var seed = 17;
                foreach (var character in world.LocationId ?? string.Empty)
                {
                    seed = seed * 31 + character;
                }

                var gameDateTime = world.GameDateTime;
                if (gameDateTime == null)
                {
                    return seed * 31 + world.GetInstanceID();
                }

                seed = AddToSeed(seed, gameDateTime.StatedRealDateTime.ToBinary());
                return AddToSeed(seed, gameDateTime.StatedGameDateTime.ToBinary());
            }
        }

        private static int AddToSeed(int seed, long value)
        {
            unchecked
            {
                seed = seed * 31 + (int)value;
                return seed * 31 + (int)(value >> 32);
            }
        }

        private void RequestLightPreparation()
        {
            _lightPreparationStage = 0;
            _lightPreparationStarted = true;
            _lightPreparationComplete = false;
        }

        private void ProcessLightPreparationStep()
        {
            if (!_lightPreparationStarted || _lightPreparationComplete)
            {
                return;
            }

            switch (_lightPreparationStage)
            {
                case 0:
                    QueuePreparedLampControllers();
                    break;
                case 1:
                    QueuePreparedLampGroups();
                    break;
                case 2:
                    QueuePreparedSceneLights();
                    break;
                case 3:
                    QueuePreparedBehaviours(
                        UnityEngine.Object.FindObjectsOfType<AdvancedLight>());
                    break;
                case 4:
                    QueuePreparedBehaviours(
                        UnityEngine.Object.FindObjectsOfType<BaseLight>());
                    break;
                case 5:
                    QueuePreparedVolumetricLights();
                    break;
                case 6:
                    QueuePreparedBehaviours(
                        UnityEngine.Object.FindObjectsOfType<SpotLightFakeGI>());
                    break;
                case 7:
                    QueuePreparedBehaviours(
                        UnityEngine.Object.FindObjectsOfType<CustomLight>());
                    break;
            }

            _lightPreparationStage++;
            if (_lightPreparationStage < LightPreparationStageCount)
            {
                return;
            }

            _lightPreparationStarted = false;
            _lightPreparationComplete = true;
            _log.LogInfo(
                $"Mid-raid light blackout prepared on {_locationId}: "
                + $"lamp controllers={_preparedLampControllerIds.Count}, "
                + $"lamp groups={_preparedLampGroupIds.Count}, "
                + $"scene lights={_preparedSceneLightIds.Count}, "
                + $"custom lights={_preparedCustomLightIds.Count}.");
        }

        private void QueuePreparedLampControllers()
        {
            var lampControllers = UnityEngine.Object.FindObjectsOfType<LampController>();
            foreach (var lampController in lampControllers)
            {
                if (lampController == null
                    || MapLightFilter.ShouldPreserve(lampController)
                    || !_preparedLampControllerIds.Add(lampController.GetInstanceID()))
                {
                    continue;
                }

                _preparedLampControllers.Enqueue(lampController);
            }
        }

        private void QueuePreparedLampGroups()
        {
            var lampGroups = UnityEngine.Object.FindObjectsOfType<ControlledLampGroup>();
            foreach (var lampGroup in lampGroups)
            {
                if (lampGroup == null
                    || MapLightFilter.ShouldPreserve(lampGroup)
                    || !_preparedLampGroupIds.Add(lampGroup.GetInstanceID()))
                {
                    continue;
                }

                _preparedLampGroups.Enqueue(lampGroup);
            }
        }

        private void QueuePreparedSceneLights()
        {
            var lights = UnityEngine.Object.FindObjectsOfType<Light>();
            foreach (var light in lights)
            {
                if (light == null
                    || light.type == LightType.Directional
                    || !MapLightFilter.IsSceneMapComponent(light)
                    || !_preparedSceneLightIds.Add(light.GetInstanceID()))
                {
                    continue;
                }

                _preparedSceneLights.Enqueue(light);
            }
        }

        private void QueuePreparedVolumetricLights()
        {
            var volumetricLights = UnityEngine.Object.FindObjectsOfType<VolumetricLight>();
            foreach (var volumetricLight in volumetricLights)
            {
                if (volumetricLight == null)
                {
                    continue;
                }

                var sourceLight = volumetricLight.GetComponent<Light>();
                if ((sourceLight != null && sourceLight.type == LightType.Directional)
                    || !MapLightFilter.IsSceneMapComponent(volumetricLight)
                    || !_preparedCustomLightIds.Add(volumetricLight.GetInstanceID()))
                {
                    continue;
                }

                _preparedCustomLights.Enqueue(volumetricLight);
            }
        }

        private void QueuePreparedBehaviours<T>(T[] behaviours) where T : Behaviour
        {
            foreach (var behaviour in behaviours)
            {
                if (behaviour == null
                    || !MapLightFilter.IsSceneMapComponent(behaviour)
                    || !_preparedCustomLightIds.Add(behaviour.GetInstanceID()))
                {
                    continue;
                }

                _preparedCustomLights.Enqueue(behaviour);
            }
        }

        private void BeginBatchedBlackout()
        {
            if (!_lightPreparationStarted && !_lightPreparationComplete)
            {
                RequestLightPreparation();
            }

            _batchedLightBlackoutActive = true;
            _lightBlackoutFrames = 0;
            _lightBlackoutWorkMilliseconds = 0d;
            BeginEmissionSweep();

            _log.LogInfo(
                $"Batched mid-raid light blackout started on {_locationId}.");
        }

        private void ProcessBatchedLightBlackout()
        {
            if (!_batchedLightBlackoutActive)
            {
                return;
            }

            var frameTimer = System.Diagnostics.Stopwatch.StartNew();
            var processedComponents = 0;
            do
            {
                if (!TryDisablePreparedLight())
                {
                    break;
                }

                processedComponents++;
            }
            while (frameTimer.Elapsed.TotalMilliseconds < LightBlackoutMillisecondsPerFrame);

            frameTimer.Stop();
            if (processedComponents > 0)
            {
                _lightBlackoutFrames++;
                _lightBlackoutWorkMilliseconds += frameTimer.Elapsed.TotalMilliseconds;
            }

            if (!_lightPreparationComplete
                || _preparedLampControllers.Count > 0
                || _preparedLampGroups.Count > 0
                || _preparedSceneLights.Count > 0
                || _preparedCustomLights.Count > 0)
            {
                return;
            }

            _batchedLightBlackoutActive = false;
            _log.LogInfo(
                $"Batched light blackout applied on {_locationId}: "
                + $"lamp controllers={_knownLampControllers.Count}, "
                + $"lamp groups={_knownLampGroups.Count}, "
                + $"scene lights={_knownSceneLights.Count}, "
                + $"custom lights={_knownCustomLights.Count}, "
                + $"frames={_lightBlackoutFrames}, "
                + $"work={_lightBlackoutWorkMilliseconds:F1}ms.");
        }

        private bool TryDisablePreparedLight()
        {
            while (_preparedLampControllers.Count > 0)
            {
                var lampController = _preparedLampControllers.Dequeue();
                if (lampController == null)
                {
                    continue;
                }

                lampController.TurnLights(false, true);
                _knownLampControllers.Add(lampController.GetInstanceID());
                return true;
            }

            while (_preparedLampGroups.Count > 0)
            {
                var lampGroup = _preparedLampGroups.Dequeue();
                if (lampGroup == null)
                {
                    continue;
                }

                lampGroup.SetLightStateExternal(false);
                _knownLampGroups.Add(lampGroup.GetInstanceID());
                return true;
            }

            while (_preparedSceneLights.Count > 0)
            {
                var light = _preparedSceneLights.Dequeue();
                if (light == null)
                {
                    continue;
                }

                light.enabled = false;
                _knownSceneLights.Add(light.GetInstanceID());
                return true;
            }

            while (_preparedCustomLights.Count > 0)
            {
                var customLight = _preparedCustomLights.Dequeue();
                if (customLight == null)
                {
                    continue;
                }

                customLight.enabled = false;
                _knownCustomLights.Add(customLight.GetInstanceID());
                return true;
            }

            return false;
        }

        private void ApplyBlackout()
        {
            var activationTimer = System.Diagnostics.Stopwatch.StartNew();
            var newLampControllers = DisableLampControllers();
            var lampControllerMilliseconds = activationTimer.ElapsedMilliseconds;
            var newLampGroups = DisableLampGroups();
            var lampGroupMilliseconds = activationTimer.ElapsedMilliseconds;
            var newSceneLights = 0;
            var newCustomLights = 0;

            if (Settings.DisableUnmanagedSceneLights.Value)
            {
                newSceneLights = DisableUnmanagedSceneLights();
                newCustomLights = DisableUnmanagedCustomLights();
            }

            var unmanagedLightMilliseconds = activationTimer.ElapsedMilliseconds;
            BeginEmissionSweep();
            activationTimer.Stop();

            _log.LogInfo(
                $"Blackout applied on {_locationId}: "
                + $"lamp controllers={_knownLampControllers.Count} (+{newLampControllers}), "
                + $"lamp groups={_knownLampGroups.Count} (+{newLampGroups}), "
                + $"scene lights={_knownSceneLights.Count} (+{newSceneLights}), "
                + $"custom lights={_knownCustomLights.Count} (+{newCustomLights}).");
            _log.LogInfo(
                $"Blackout activation timings: "
                + $"lamp controllers={lampControllerMilliseconds}ms, "
                + $"lamp groups={lampGroupMilliseconds - lampControllerMilliseconds}ms, "
                + $"unmanaged lights={unmanagedLightMilliseconds - lampGroupMilliseconds}ms, "
                + $"emission setup={activationTimer.ElapsedMilliseconds - unmanagedLightMilliseconds}ms, "
                + $"total={activationTimer.ElapsedMilliseconds}ms.");
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
                _emissionPreparationStarted = false;
                _emissionPreparationComplete = false;
                return;
            }

            if (_raidMode == BlackoutMode.MidRaidEvent)
            {
                if (!_emissionPreparationStarted)
                {
                    _seenEmissionRenderers.Clear();
                    StartEmissionSweep();
                    QueueLoadedSceneHierarchies();
                }

                _emissionPreparationStarted = false;
                _emissionPreparationComplete = false;
                return;
            }

            _seenEmissionRenderers.Clear();
            StartEmissionSweep();
            _activeRendererDiscovery = UnityEngine.Object.FindObjectsOfType<MeshRenderer>();
            _nextActiveRendererDiscovery = 0;
            _activeRendererDiscoveryCount = _activeRendererDiscovery.Length;
            QueueInactiveLightHierarchies();
        }

        private void BeginEmissionPreparation()
        {
            _seenEmissionRenderers.Clear();
            StartEmissionSweep();
            QueueLoadedSceneHierarchies();
            _emissionPreparationStarted = true;
            _emissionPreparationComplete = false;
        }

        private void ProcessEmissionPreparationBatch()
        {
            if (!_emissionPreparationStarted
                || _emissionPreparationComplete
                || _blackoutReady
                || _blackoutActive)
            {
                return;
            }

            ProcessRendererDiscoveryBatch(PreparationHierarchyNodesPerFrame);
            if (_rendererDiscoveryNodes == null || _rendererDiscoveryNodes.Count > 0)
            {
                return;
            }

            _emissionPreparationComplete = true;
            _log.LogInfo(
                $"Mid-raid emission blackout prepared on {_locationId}: "
                + $"discovery roots={_emissionDiscoveryRootCount}, "
                + $"discovery nodes={_emissionDiscoveryNodeCount}, "
                + $"candidates={_emissionCandidateCount}.");
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

        private void QueueLoadedSceneHierarchies()
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
                    QueueRendererHierarchy(
                        rootObject.transform,
                        IsLightHierarchyRoot(rootObject.name));
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

        private void ProcessRendererDiscoveryBatch(int maximumNodes)
        {
            if (_rendererDiscoveryNodes == null)
            {
                return;
            }

            var processedNodes = 0;
            while (_rendererDiscoveryNodes.Count > 0
                && processedNodes < maximumNodes)
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
                if (renderer != null && !node.IncludeInactive)
                {
                    _activeRendererDiscoveryCount++;
                }

                if (renderer != null && HasEmissionProperties(renderer))
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

            if (string.Equals(locationId, "Interchange", StringComparison.OrdinalIgnoreCase))
            {
                return Settings.InterchangeEnabled.Value;
            }

            if (string.Equals(locationId, "RezervBase", StringComparison.OrdinalIgnoreCase))
            {
                return Settings.ReserveEnabled.Value;
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
