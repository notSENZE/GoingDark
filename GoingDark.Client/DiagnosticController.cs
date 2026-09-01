using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using Comfort.Common;
using EFT;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Rendering;

namespace GoingDark.Client
{
    internal sealed class DiagnosticController
    {
        private const float MaxSelectionDistance = 200f;
        private const float RendererBlinkSeconds = 1.25f;
        private const float NearbyEffectRadius = 3f;

        private readonly ManualLogSource _log;
        private readonly HashSet<int> _capturedRendererIds = new HashSet<int>();
        private readonly MaterialPropertyBlock _materialPropertyBlock = new MaterialPropertyBlock();

        private int _worldInstanceId;
        private string _locationId;
        private MeshRenderer[] _rendererCache;
        private DiagnosticReport _report;
        private string _reportPath;
        private Renderer _selectedRenderer;
        private DiagnosticTargetRecord _selectedTarget;
        private bool _selectedTargetTested;
        private SurfaceTestSession _surfaceTest;
        private Renderer _blinkingRenderer;
        private bool _blinkingRendererWasEnabled;
        private float _blinkRestoreTime;
        private string _statusMessage;
        private float _statusUntil;

        internal DiagnosticController(ManualLogSource log)
        {
            _log = log;
        }

        internal void Tick()
        {
            RestoreBlinkWhenReady();

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

            if (!Settings.DiagnosticMode.Value)
            {
                return;
            }

            if (Settings.CaptureDiagnosticTarget.Value.IsDown())
            {
                CaptureTarget();
            }

            if (Settings.LiveTestDiagnosticTarget.Value.IsDown())
            {
                TestSelectedTarget();
            }

            if (Settings.SurfaceTestDiagnosticTarget.Value.IsDown())
            {
                TestSelectedSurface();
            }
        }

        internal void DrawOverlay()
        {
            if (!Settings.DiagnosticMode.Value || _worldInstanceId == 0)
            {
                return;
            }

            DrawCenterReticle();

            var targetCount = _report?.Targets.Count ?? 0;
            var text = $"Going Dark diagnostics | {Settings.CaptureDiagnosticTarget.Value}: capture target"
                + $" | {Settings.LiveTestDiagnosticTarget.Value}: emission test"
                + $" | {Settings.SurfaceTestDiagnosticTarget.Value}: surface test"
                + $" | targets: {targetCount}";
            var overlayHeight = 58f;

            if (_selectedTarget != null)
            {
                text += $"\nSelected #{_selectedTarget.Number}: {_selectedTarget.Name} "
                    + $"({_selectedTarget.SelectionMethod})";
                text += "\nMaterials: " + FormatMaterialNames(_selectedTarget);
                overlayHeight += 38f;

                if (_surfaceTest != null && !string.IsNullOrEmpty(_surfaceTest.CurrentStage))
                {
                    text += "\nSurface test: " + _surfaceTest.CurrentStage;
                    overlayHeight += 20f;
                }

                var centerX = Screen.width * 0.5f;
                var centerY = Screen.height * 0.5f;
                var labelWidth = Math.Min(600f, Screen.width - 40f);
                GUI.Box(
                    new Rect(centerX - labelWidth * 0.5f, centerY + 24f, labelWidth, 26f),
                    $"#{_selectedTarget.Number}  {_selectedTarget.Name}");
            }

            if (!string.IsNullOrEmpty(_statusMessage) && Time.unscaledTime <= _statusUntil)
            {
                text += "\n" + _statusMessage;
                overlayHeight += 20f;
            }

            var width = Math.Min(900f, Screen.width - 40f);
            GUI.Box(new Rect(20f, 20f, width, overlayHeight), text);
        }

        internal void Reset()
        {
            RestoreSurfaceTest();

            if (_worldInstanceId == 0)
            {
                return;
            }

            RestoreBlink();
            SaveReport();
            _worldInstanceId = 0;
            _locationId = null;
            _rendererCache = null;
            _report = null;
            _reportPath = null;
            _selectedRenderer = null;
            _selectedTarget = null;
            _selectedTargetTested = false;
            _capturedRendererIds.Clear();
            _statusMessage = null;
            _statusUntil = 0f;
        }

        internal void SceneLoaded()
        {
            _rendererCache = null;
        }

        private void BeginRaid(GameWorld world)
        {
            Reset();
            _worldInstanceId = world.GetInstanceID();
            _locationId = world.LocationId ?? string.Empty;
        }

        private void CaptureTarget()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                SetStatus("No active game camera was found.");
                return;
            }

            var screenCenter = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
            var ray = camera.ScreenPointToRay(screenCenter);
            Vector3 selectionPoint;
            var renderer = FindPhysicsTarget(ray, out selectionPoint);
            var selectionMethod = "Physics raycast";

            if (renderer == null)
            {
                renderer = FindRendererBoundsTarget(ray, out selectionPoint);
                selectionMethod = "Renderer bounds fallback";
            }

            if (renderer == null)
            {
                SetStatus("No map renderer was found under the crosshair.");
                return;
            }

            RestoreSurfaceTest();
            EnsureReport();
            _selectedRenderer = renderer;
            _selectedTargetTested = false;

            var rendererId = renderer.GetInstanceID();
            if (_capturedRendererIds.Contains(rendererId))
            {
                _selectedTarget = FindCapturedTarget(rendererId);
                BlinkSelectedRenderer();
                SetStatus(
                    $"Selected again: {renderer.name}. The renderer is blinking now and is "
                    + "already in this raid report.");
                return;
            }

            _selectedTarget = CaptureRenderer(renderer, selectionMethod, selectionPoint);
            _report.Targets.Add(_selectedTarget);
            _capturedRendererIds.Add(rendererId);
            BlinkSelectedRenderer();

            SetStatus(
                $"Captured #{_report.Targets.Count}: {renderer.name} "
                + $"({_selectedTarget.Materials.Count} material slots). "
                + $"Nearby effects: {_selectedTarget.NearbyEffects.Count}. "
                + $"It is blinking for {RendererBlinkSeconds:0.##} seconds. Report saved.");
        }

        private static void DrawCenterReticle()
        {
            var centerX = Screen.width * 0.5f;
            var centerY = Screen.height * 0.5f;
            var texture = Texture2D.whiteTexture;
            var previousColor = GUI.color;

            GUI.color = new Color(0f, 0f, 0f, 0.9f);
            DrawReticleSegments(texture, centerX, centerY, 3f, 11f, 4f);

            GUI.color = new Color(0.15f, 0.95f, 1f, 1f);
            DrawReticleSegments(texture, centerX, centerY, 1f, 10f, 5f);
            GUI.DrawTexture(new Rect(centerX - 1f, centerY - 1f, 2f, 2f), texture);

            GUI.color = previousColor;
        }

        private static void DrawReticleSegments(
            Texture texture,
            float centerX,
            float centerY,
            float thickness,
            float length,
            float gap)
        {
            var halfThickness = thickness * 0.5f;
            GUI.DrawTexture(
                new Rect(centerX - halfThickness, centerY - gap - length, thickness, length),
                texture);
            GUI.DrawTexture(
                new Rect(centerX - halfThickness, centerY + gap, thickness, length),
                texture);
            GUI.DrawTexture(
                new Rect(centerX - gap - length, centerY - halfThickness, length, thickness),
                texture);
            GUI.DrawTexture(
                new Rect(centerX + gap, centerY - halfThickness, length, thickness),
                texture);
        }

        private static string FormatMaterialNames(DiagnosticTargetRecord target)
        {
            if (target.Materials.Count == 0)
            {
                return "(none)";
            }

            var materialNames = new List<string>();
            foreach (var material in target.Materials)
            {
                materialNames.Add($"[{material.Index}] {material.Name}");
            }

            return string.Join(" | ", materialNames);
        }

        private Renderer FindPhysicsTarget(Ray ray, out Vector3 selectionPoint)
        {
            selectionPoint = ray.origin;
            var hits = Physics.RaycastAll(
                ray,
                MaxSelectionDistance,
                ~0,
                QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

            foreach (var hit in hits)
            {
                var collider = hit.collider;
                if (collider == null)
                {
                    continue;
                }

                var renderer = collider.GetComponent<Renderer>();
                if (IsDiagnosticTarget(renderer))
                {
                    selectionPoint = hit.point;
                    return renderer;
                }

                renderer = collider.GetComponentInParent<Renderer>();
                if (IsDiagnosticTarget(renderer))
                {
                    selectionPoint = hit.point;
                    return renderer;
                }

                var childRenderers = collider.GetComponentsInChildren<Renderer>();
                float rendererDistance;
                renderer = FindClosestBoundsTarget(childRenderers, ray, out rendererDistance);
                if (renderer != null)
                {
                    selectionPoint = hit.point;
                    return renderer;
                }
            }

            return null;
        }

        private Renderer FindRendererBoundsTarget(Ray ray, out Vector3 selectionPoint)
        {
            if (_rendererCache == null)
            {
                _rendererCache = UnityEngine.Object.FindObjectsOfType<MeshRenderer>();
            }

            float distance;
            var renderer = FindClosestBoundsTarget(_rendererCache, ray, out distance);
            selectionPoint = renderer != null ? ray.GetPoint(distance) : ray.origin;
            return renderer;
        }

        private static Renderer FindClosestBoundsTarget(
            Renderer[] renderers,
            Ray ray,
            out float closestDistance)
        {
            Renderer closestRenderer = null;
            closestDistance = MaxSelectionDistance;

            foreach (var renderer in renderers)
            {
                if (!IsDiagnosticTarget(renderer))
                {
                    continue;
                }

                float distance;
                if (!renderer.bounds.IntersectRay(ray, out distance)
                    || distance < 0f
                    || distance > closestDistance)
                {
                    continue;
                }

                closestRenderer = renderer;
                closestDistance = distance;
            }

            return closestRenderer;
        }

        private static bool IsDiagnosticTarget(Renderer renderer)
        {
            return renderer != null
                && renderer.enabled
                && renderer.gameObject.activeInHierarchy
                && MapLightFilter.IsSceneMapComponent(renderer);
        }

        private DiagnosticTargetRecord CaptureRenderer(
            Renderer renderer,
            string selectionMethod,
            Vector3 selectionPoint)
        {
            var target = new DiagnosticTargetRecord
            {
                Number = _report.Targets.Count + 1,
                CapturedAt = DateTime.Now.ToString("O", CultureInfo.InvariantCulture),
                RendererInstanceId = renderer.GetInstanceID(),
                Name = renderer.name,
                HierarchyPath = GetHierarchyPath(renderer.transform),
                Scene = renderer.gameObject.scene.name,
                RendererType = renderer.GetType().FullName,
                SelectionMethod = selectionMethod,
                SelectionPoint = FormatVector3(selectionPoint),
                Position = FormatVector3(renderer.transform.position),
                BoundsCenter = FormatVector3(renderer.bounds.center),
                BoundsSize = FormatVector3(renderer.bounds.size),
                Components = CaptureComponentNames(renderer.gameObject)
            };

            target.NearbyEffects.AddRange(CaptureNearbyEffects(selectionPoint));

            var materials = renderer.sharedMaterials;
            for (var materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                target.Materials.Add(CaptureMaterial(renderer, materials[materialIndex], materialIndex));
            }

            return target;
        }

        private DiagnosticMaterialRecord CaptureMaterial(
            Renderer renderer,
            Material material,
            int materialIndex)
        {
            var materialRecord = new DiagnosticMaterialRecord
            {
                Index = materialIndex,
                Name = material != null ? material.name : "(null)"
            };

            if (material == null || material.shader == null)
            {
                return materialRecord;
            }

            var shader = material.shader;
            materialRecord.Shader = shader.name;
            materialRecord.RenderQueue = material.renderQueue;
            materialRecord.Keywords.AddRange(material.shaderKeywords ?? Array.Empty<string>());
            materialRecord.Keywords.Sort(StringComparer.OrdinalIgnoreCase);

            _materialPropertyBlock.Clear();
            renderer.GetPropertyBlock(_materialPropertyBlock, materialIndex);

            var propertyCount = shader.GetPropertyCount();
            for (var propertyIndex = 0; propertyIndex < propertyCount; propertyIndex++)
            {
                var propertyName = shader.GetPropertyName(propertyIndex);
                var propertyId = shader.GetPropertyNameId(propertyIndex);
                var propertyType = shader.GetPropertyType(propertyIndex);
                var hasOverride = _materialPropertyBlock.HasProperty(propertyId);
                var description = shader.GetPropertyDescription(propertyIndex);

                materialRecord.Properties.Add(new DiagnosticShaderPropertyRecord
                {
                    Name = propertyName,
                    Description = description,
                    Type = propertyType.ToString(),
                    Flags = shader.GetPropertyFlags(propertyIndex).ToString(),
                    MaterialValue = ReadMaterialValue(material, propertyId, propertyType),
                    HasRendererOverride = hasOverride,
                    RendererOverrideValue = hasOverride
                        ? ReadPropertyBlockValue(_materialPropertyBlock, propertyId, propertyType)
                        : null,
                    EmissionCandidate = IsEmissionProperty(propertyName, description)
                });
            }

            return materialRecord;
        }

        private void TestSelectedTarget()
        {
            if (_selectedRenderer == null || _selectedTarget == null)
            {
                SetStatus($"Capture a target with {Settings.CaptureDiagnosticTarget.Value} first.");
                return;
            }

            RestoreSurfaceTest();
            if (_selectedTargetTested)
            {
                BlinkSelectedRenderer();
                SetStatus(
                    $"Blinking {_selectedRenderer.name} for {RendererBlinkSeconds:0.##} seconds. "
                    + "If the bright surface disappears, the correct renderer is selected.");
                return;
            }

            var changedProperties = ApplyEmissionTest(_selectedRenderer);
            _selectedTargetTested = true;
            _selectedTarget.LiveTestAppliedAt = DateTime.Now.ToString("O", CultureInfo.InvariantCulture);
            _selectedTarget.LiveTestProperties.Clear();
            _selectedTarget.LiveTestProperties.AddRange(changedProperties);

            if (changedProperties.Count == 0)
            {
                BlinkSelectedRenderer();
                SetStatus(
                    $"No emission-like shader property was found on {_selectedRenderer.name}. "
                    + "The renderer is blinking briefly to confirm the target.");
                return;
            }

            SaveReport();
            SetStatus(
                $"Live test applied {changedProperties.Count} emission overrides to {_selectedRenderer.name}. "
                + $"Press {Settings.LiveTestDiagnosticTarget.Value} again to blink the renderer.");
        }

        private List<string> ApplyEmissionTest(Renderer renderer)
        {
            var changedProperties = new List<string>();
            var materials = renderer.sharedMaterials;

            for (var materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                var material = materials[materialIndex];
                if (material == null || material.shader == null)
                {
                    continue;
                }

                _materialPropertyBlock.Clear();
                renderer.GetPropertyBlock(_materialPropertyBlock, materialIndex);
                var materialChanged = false;
                var shader = material.shader;

                for (var propertyIndex = 0; propertyIndex < shader.GetPropertyCount(); propertyIndex++)
                {
                    var propertyName = shader.GetPropertyName(propertyIndex);
                    var description = shader.GetPropertyDescription(propertyIndex);
                    if (!IsEmissionProperty(propertyName, description))
                    {
                        continue;
                    }

                    var propertyId = shader.GetPropertyNameId(propertyIndex);
                    var propertyType = shader.GetPropertyType(propertyIndex);
                    SetBlackoutValue(_materialPropertyBlock, propertyId, propertyType);
                    changedProperties.Add($"material[{materialIndex}].{propertyName} ({propertyType})");
                    materialChanged = true;
                }

                if (materialChanged)
                {
                    renderer.SetPropertyBlock(_materialPropertyBlock, materialIndex);
                }
            }

            return changedProperties;
        }

        private void TestSelectedSurface()
        {
            if (_selectedRenderer == null || _selectedTarget == null)
            {
                SetStatus($"Capture a target with {Settings.CaptureDiagnosticTarget.Value} first.");
                return;
            }

            if (_surfaceTest == null || _surfaceTest.Renderer != _selectedRenderer)
            {
                _surfaceTest = CaptureSurfaceTestState(_selectedRenderer);
            }

            RestoreSurfacePropertyBlocks(_surfaceTest);

            var stage = (SurfaceTestStage)_surfaceTest.NextStage;
            var changedProperties = new List<string>();
            if (stage != SurfaceTestStage.Restored)
            {
                changedProperties = ApplySurfaceTest(_selectedRenderer, stage);
            }

            var stageName = GetSurfaceTestStageName(stage);
            _surfaceTest.CurrentStage = stageName;
            _surfaceTest.NextStage = (_surfaceTest.NextStage + 1) % 5;

            var testRecord = new DiagnosticSurfaceTestRecord
            {
                AppliedAt = DateTime.Now.ToString("O", CultureInfo.InvariantCulture),
                Stage = stageName
            };
            testRecord.Properties.AddRange(changedProperties);
            _selectedTarget.SurfaceTests.Add(testRecord);
            SaveReport();

            if (stage == SurfaceTestStage.Restored)
            {
                SetStatus(
                    $"Surface test restored the original material state on {_selectedRenderer.name}. "
                    + $"Press {Settings.SurfaceTestDiagnosticTarget.Value} to begin again.");
                return;
            }

            if (changedProperties.Count == 0)
            {
                SetStatus(
                    $"Surface test '{stageName}' found no matching properties on "
                    + $"{_selectedRenderer.name}. Press {Settings.SurfaceTestDiagnosticTarget.Value} "
                    + "for the next stage.");
                return;
            }

            SetStatus(
                $"Surface test '{stageName}' applied {changedProperties.Count} overrides to "
                + $"{_selectedRenderer.name}. Press {Settings.SurfaceTestDiagnosticTarget.Value} "
                + "for the next stage; the previous stage will be restored first.");
        }

        private SurfaceTestSession CaptureSurfaceTestState(Renderer renderer)
        {
            var session = new SurfaceTestSession
            {
                Renderer = renderer
            };

            for (var materialIndex = 0; materialIndex < renderer.sharedMaterials.Length; materialIndex++)
            {
                var propertyBlock = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(propertyBlock, materialIndex);
                session.OriginalPropertyBlocks.Add(new MaterialPropertyBlockSnapshot
                {
                    MaterialIndex = materialIndex,
                    PropertyBlock = propertyBlock,
                    WasEmpty = propertyBlock.isEmpty
                });
            }

            return session;
        }

        private List<string> ApplySurfaceTest(Renderer renderer, SurfaceTestStage stage)
        {
            var changedProperties = new List<string>();
            var materials = renderer.sharedMaterials;

            for (var materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                var material = materials[materialIndex];
                if (material == null || material.shader == null)
                {
                    continue;
                }

                _materialPropertyBlock.Clear();
                renderer.GetPropertyBlock(_materialPropertyBlock, materialIndex);
                var materialChanged = false;
                var shader = material.shader;

                for (var propertyIndex = 0; propertyIndex < shader.GetPropertyCount(); propertyIndex++)
                {
                    var propertyName = shader.GetPropertyName(propertyIndex);
                    if (!IsSurfaceTestProperty(stage, propertyName))
                    {
                        continue;
                    }

                    var propertyId = shader.GetPropertyNameId(propertyIndex);
                    var propertyType = shader.GetPropertyType(propertyIndex);
                    SetBlackoutValue(_materialPropertyBlock, propertyId, propertyType);
                    changedProperties.Add($"material[{materialIndex}].{propertyName} ({propertyType})");
                    materialChanged = true;
                }

                if (materialChanged)
                {
                    renderer.SetPropertyBlock(_materialPropertyBlock, materialIndex);
                }
            }

            return changedProperties;
        }

        private void RestoreSurfaceTest()
        {
            if (_surfaceTest == null)
            {
                return;
            }

            RestoreSurfacePropertyBlocks(_surfaceTest);
            _surfaceTest = null;
        }

        private static void RestoreSurfacePropertyBlocks(SurfaceTestSession session)
        {
            if (session.Renderer == null)
            {
                return;
            }

            foreach (var snapshot in session.OriginalPropertyBlocks)
            {
                session.Renderer.SetPropertyBlock(
                    snapshot.WasEmpty ? null : snapshot.PropertyBlock,
                    snapshot.MaterialIndex);
            }
        }

        private static bool IsSurfaceTestProperty(SurfaceTestStage stage, string propertyName)
        {
            var isBaseColor = string.Equals(propertyName, "_Color", StringComparison.Ordinal)
                || string.Equals(propertyName, "_BaseColor", StringComparison.Ordinal)
                || string.Equals(propertyName, "_BaseTintColor", StringComparison.Ordinal)
                || string.Equals(propertyName, "_TintColor", StringComparison.Ordinal)
                || string.Equals(propertyName, "_DefVals", StringComparison.Ordinal);
            var isReflection = string.Equals(propertyName, "_SpecColor", StringComparison.Ordinal)
                || string.Equals(propertyName, "_ReflectColor", StringComparison.Ordinal)
                || string.Equals(propertyName, "_Glossness", StringComparison.Ordinal)
                || string.Equals(propertyName, "_Specularness", StringComparison.Ordinal)
                || string.Equals(propertyName, "_SpecPower", StringComparison.Ordinal)
                || string.Equals(propertyName, "_Shininess", StringComparison.Ordinal)
                || string.Equals(propertyName, "_SpecVals", StringComparison.Ordinal)
                || string.Equals(propertyName, "_SpecMap", StringComparison.Ordinal)
                || string.Equals(propertyName, "_GlossMap", StringComparison.Ordinal);
            var isMainTexture = string.Equals(propertyName, "_MainTex", StringComparison.Ordinal);

            if (stage == SurfaceTestStage.BaseColor)
            {
                return isBaseColor;
            }

            if (stage == SurfaceTestStage.Reflection)
            {
                return isReflection;
            }

            if (stage == SurfaceTestStage.MainTexture)
            {
                return isMainTexture;
            }

            return stage == SurfaceTestStage.Combined
                && (isBaseColor || isReflection || isMainTexture);
        }

        private static string GetSurfaceTestStageName(SurfaceTestStage stage)
        {
            switch (stage)
            {
                case SurfaceTestStage.BaseColor:
                    return "Base color";
                case SurfaceTestStage.Reflection:
                    return "Reflection and specular";
                case SurfaceTestStage.MainTexture:
                    return "Main texture";
                case SurfaceTestStage.Combined:
                    return "Combined surface";
                default:
                    return "Original restored";
            }
        }

        private static void SetBlackoutValue(
            MaterialPropertyBlock propertyBlock,
            int propertyId,
            ShaderPropertyType propertyType)
        {
            switch (propertyType)
            {
                case ShaderPropertyType.Color:
                    propertyBlock.SetColor(propertyId, Color.black);
                    break;
                case ShaderPropertyType.Vector:
                    propertyBlock.SetVector(propertyId, Vector4.zero);
                    break;
                case ShaderPropertyType.Float:
                case ShaderPropertyType.Range:
                    propertyBlock.SetFloat(propertyId, 0f);
                    break;
                case ShaderPropertyType.Texture:
                    propertyBlock.SetTexture(propertyId, Texture2D.blackTexture);
                    break;
                case ShaderPropertyType.Int:
                    propertyBlock.SetInt(propertyId, 0);
                    break;
            }
        }

        private void BlinkSelectedRenderer()
        {
            RestoreBlink();
            _blinkingRenderer = _selectedRenderer;
            _blinkingRendererWasEnabled = _blinkingRenderer.enabled;
            _blinkingRenderer.enabled = false;
            _blinkRestoreTime = Time.unscaledTime + RendererBlinkSeconds;
            _selectedTarget.RendererBlinkUsed = true;
            SaveReport();
        }

        private void RestoreBlinkWhenReady()
        {
            if (_blinkingRenderer != null && Time.unscaledTime >= _blinkRestoreTime)
            {
                RestoreBlink();
            }
        }

        private void RestoreBlink()
        {
            if (_blinkingRenderer != null)
            {
                _blinkingRenderer.enabled = _blinkingRendererWasEnabled;
            }

            _blinkingRenderer = null;
            _blinkRestoreTime = 0f;
        }

        private void EnsureReport()
        {
            if (_report != null)
            {
                return;
            }

            var createdAt = DateTime.Now;
            _report = new DiagnosticReport
            {
                ModVersion = Plugin.PluginVersion,
                LocationId = _locationId,
                CreatedAt = createdAt.ToString("O", CultureInfo.InvariantCulture)
            };

            var diagnosticsDirectory = Path.Combine(
                BepInEx.Paths.PluginPath,
                "GoingDark",
                "diagnostics");
            var fileName = SanitizeFileName(
                $"{_locationId}_{createdAt:yyyy-MM-dd_HHmmss}.json");
            _reportPath = Path.Combine(diagnosticsDirectory, fileName);
        }

        private void SaveReport()
        {
            if (_report == null || string.IsNullOrEmpty(_reportPath))
            {
                return;
            }

            _report.LastUpdatedAt = DateTime.Now.ToString("O", CultureInfo.InvariantCulture);

            try
            {
                var directory = Path.GetDirectoryName(_reportPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonConvert.SerializeObject(_report, Formatting.Indented);
                File.WriteAllText(_reportPath, json);
            }
            catch (IOException exception)
            {
                _log.LogError($"Could not write diagnostic report: {exception.Message}");
            }
            catch (UnauthorizedAccessException exception)
            {
                _log.LogError($"Could not write diagnostic report: {exception.Message}");
            }
        }

        private DiagnosticTargetRecord FindCapturedTarget(int rendererId)
        {
            foreach (var target in _report.Targets)
            {
                if (target.RendererInstanceId == rendererId)
                {
                    return target;
                }
            }

            return null;
        }

        private void SetStatus(string message)
        {
            _statusMessage = message;
            _statusUntil = Time.unscaledTime + 8f;
            _log.LogInfo($"[Diagnostics] {message}");
        }

        private static bool IsEmissionProperty(string name, string description)
        {
            var text = ((name ?? string.Empty) + " " + (description ?? string.Empty))
                .ToLowerInvariant();
            return text.Contains("emiss")
                || text.Contains("emission")
                || text.Contains("glow")
                || text.Contains("illum")
                || text.Contains("neon");
        }

        private static string ReadMaterialValue(
            Material material,
            int propertyId,
            ShaderPropertyType propertyType)
        {
            switch (propertyType)
            {
                case ShaderPropertyType.Color:
                    return FormatColor(material.GetColor(propertyId));
                case ShaderPropertyType.Vector:
                    return FormatVector4(material.GetVector(propertyId));
                case ShaderPropertyType.Float:
                case ShaderPropertyType.Range:
                    return FormatFloat(material.GetFloat(propertyId));
                case ShaderPropertyType.Texture:
                    return FormatTexture(material.GetTexture(propertyId));
                case ShaderPropertyType.Int:
                    return material.GetInt(propertyId).ToString(CultureInfo.InvariantCulture);
                default:
                    return null;
            }
        }

        private static string ReadPropertyBlockValue(
            MaterialPropertyBlock propertyBlock,
            int propertyId,
            ShaderPropertyType propertyType)
        {
            switch (propertyType)
            {
                case ShaderPropertyType.Color:
                    return FormatColor(propertyBlock.GetColor(propertyId));
                case ShaderPropertyType.Vector:
                    return FormatVector4(propertyBlock.GetVector(propertyId));
                case ShaderPropertyType.Float:
                case ShaderPropertyType.Range:
                    return FormatFloat(propertyBlock.GetFloat(propertyId));
                case ShaderPropertyType.Texture:
                    return FormatTexture(propertyBlock.GetTexture(propertyId));
                case ShaderPropertyType.Int:
                    return propertyBlock.GetInt(propertyId).ToString(CultureInfo.InvariantCulture);
                default:
                    return null;
            }
        }

        private static List<string> CaptureComponentNames(GameObject gameObject)
        {
            var names = new List<string>();
            var components = gameObject.GetComponents<Component>();
            foreach (var component in components)
            {
                if (component != null)
                {
                    names.Add(component.GetType().FullName);
                }
            }

            names.Sort(StringComparer.OrdinalIgnoreCase);
            return names;
        }

        private static List<DiagnosticEffectRecord> CaptureNearbyEffects(Vector3 selectionPoint)
        {
            var records = new List<DiagnosticEffectRecord>();
            var capturedIds = new HashSet<int>();

            CaptureNearbyComponents(
                UnityEngine.Object.FindObjectsOfType<Light>(),
                selectionPoint,
                capturedIds,
                records);
            CaptureNearbyComponents(
                UnityEngine.Object.FindObjectsOfType<ParticleSystem>(),
                selectionPoint,
                capturedIds,
                records);
            CaptureNearbyComponents(
                UnityEngine.Object.FindObjectsOfType<TrailRenderer>(),
                selectionPoint,
                capturedIds,
                records);

            records.Sort((left, right) => left.Distance.CompareTo(right.Distance));
            return records;
        }

        private static void CaptureNearbyComponents<T>(
            T[] components,
            Vector3 selectionPoint,
            HashSet<int> capturedIds,
            List<DiagnosticEffectRecord> records) where T : Component
        {
            foreach (var component in components)
            {
                if (component == null
                    || !component.gameObject.scene.IsValid()
                    || !capturedIds.Add(component.GetInstanceID()))
                {
                    continue;
                }

                var distance = GetEffectDistance(component, selectionPoint);
                if (distance > NearbyEffectRadius)
                {
                    continue;
                }

                records.Add(CaptureEffect(component, distance));
            }
        }

        private static float GetEffectDistance(Component component, Vector3 selectionPoint)
        {
            return Vector3.Distance(component.transform.position, selectionPoint);
        }

        private static DiagnosticEffectRecord CaptureEffect(Component component, float distance)
        {
            var record = new DiagnosticEffectRecord
            {
                InstanceId = component.GetInstanceID(),
                Type = component.GetType().FullName,
                Name = component.name,
                HierarchyPath = GetHierarchyPath(component.transform),
                Position = FormatVector3(component.transform.position),
                Distance = distance,
                ActiveInHierarchy = component.gameObject.activeInHierarchy,
                PreservedByBlackoutFilter = MapLightFilter.ShouldPreserve(component)
            };

            if (component is Light light)
            {
                record.Enabled = light.enabled;
                record.Details = $"type={light.type}, intensity={FormatFloat(light.intensity)}, "
                    + $"range={FormatFloat(light.range)}, color={FormatColor(light.color)}";
            }
            else if (component is ParticleSystem particleSystem)
            {
                var main = particleSystem.main;
                record.Enabled = particleSystem.gameObject.activeInHierarchy;
                record.Details = $"playing={particleSystem.isPlaying}, emitting={particleSystem.isEmitting}, "
                    + $"particles={particleSystem.particleCount}, loop={main.loop}";
            }
            else if (component is TrailRenderer trailRenderer)
            {
                record.Enabled = trailRenderer.enabled;
                record.Details = $"positions={trailRenderer.positionCount}, "
                    + $"time={FormatFloat(trailRenderer.time)}, "
                    + $"width={FormatFloat(trailRenderer.widthMultiplier)}";
            }

            return record;
        }

        private static string GetHierarchyPath(Transform transform)
        {
            var names = new List<string>();
            var current = transform;
            while (current != null)
            {
                names.Add(current.name);
                current = current.parent;
            }

            names.Reverse();
            return string.Join("/", names);
        }

        private static string FormatTexture(Texture texture)
        {
            return texture == null ? null : $"{texture.name} ({texture.GetType().Name})";
        }

        private static string FormatColor(Color color)
        {
            return $"({FormatFloat(color.r)}, {FormatFloat(color.g)}, "
                + $"{FormatFloat(color.b)}, {FormatFloat(color.a)})";
        }

        private static string FormatVector3(Vector3 vector)
        {
            return $"({FormatFloat(vector.x)}, {FormatFloat(vector.y)}, {FormatFloat(vector.z)})";
        }

        private static string FormatVector4(Vector4 vector)
        {
            return $"({FormatFloat(vector.x)}, {FormatFloat(vector.y)}, "
                + $"{FormatFloat(vector.z)}, {FormatFloat(vector.w)})";
        }

        private static string FormatFloat(float value)
        {
            return value.ToString("0.#####", CultureInfo.InvariantCulture);
        }

        private static string SanitizeFileName(string fileName)
        {
            foreach (var invalidCharacter in Path.GetInvalidFileNameChars())
            {
                fileName = fileName.Replace(invalidCharacter, '_');
            }

            return fileName;
        }
    }

    internal sealed class DiagnosticReport
    {
        public string ModVersion { get; set; }
        public string LocationId { get; set; }
        public string CreatedAt { get; set; }
        public string LastUpdatedAt { get; set; }
        public List<DiagnosticTargetRecord> Targets { get; } = new List<DiagnosticTargetRecord>();
    }

    internal sealed class DiagnosticTargetRecord
    {
        public int Number { get; set; }
        public string CapturedAt { get; set; }
        public int RendererInstanceId { get; set; }
        public string Name { get; set; }
        public string HierarchyPath { get; set; }
        public string Scene { get; set; }
        public string RendererType { get; set; }
        public string SelectionMethod { get; set; }
        public string SelectionPoint { get; set; }
        public string Position { get; set; }
        public string BoundsCenter { get; set; }
        public string BoundsSize { get; set; }
        public List<string> Components { get; set; } = new List<string>();
        public List<DiagnosticMaterialRecord> Materials { get; } = new List<DiagnosticMaterialRecord>();
        public List<DiagnosticEffectRecord> NearbyEffects { get; }
            = new List<DiagnosticEffectRecord>();
        public string LiveTestAppliedAt { get; set; }
        public List<string> LiveTestProperties { get; } = new List<string>();
        public List<DiagnosticSurfaceTestRecord> SurfaceTests { get; }
            = new List<DiagnosticSurfaceTestRecord>();
        public bool RendererBlinkUsed { get; set; }
    }

    internal sealed class DiagnosticEffectRecord
    {
        public int InstanceId { get; set; }
        public string Type { get; set; }
        public string Name { get; set; }
        public string HierarchyPath { get; set; }
        public string Position { get; set; }
        public float Distance { get; set; }
        public bool ActiveInHierarchy { get; set; }
        public bool Enabled { get; set; }
        public bool PreservedByBlackoutFilter { get; set; }
        public string Details { get; set; }
    }

    internal sealed class DiagnosticSurfaceTestRecord
    {
        public string AppliedAt { get; set; }
        public string Stage { get; set; }
        public List<string> Properties { get; } = new List<string>();
    }

    internal sealed class DiagnosticMaterialRecord
    {
        public int Index { get; set; }
        public string Name { get; set; }
        public string Shader { get; set; }
        public int RenderQueue { get; set; }
        public List<string> Keywords { get; } = new List<string>();
        public List<DiagnosticShaderPropertyRecord> Properties { get; }
            = new List<DiagnosticShaderPropertyRecord>();
    }

    internal sealed class DiagnosticShaderPropertyRecord
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Type { get; set; }
        public string Flags { get; set; }
        public string MaterialValue { get; set; }
        public bool HasRendererOverride { get; set; }
        public string RendererOverrideValue { get; set; }
        public bool EmissionCandidate { get; set; }
    }

    internal enum SurfaceTestStage
    {
        BaseColor = 0,
        Reflection = 1,
        MainTexture = 2,
        Combined = 3,
        Restored = 4
    }

    internal sealed class SurfaceTestSession
    {
        public Renderer Renderer { get; set; }
        public int NextStage { get; set; }
        public string CurrentStage { get; set; }
        public List<MaterialPropertyBlockSnapshot> OriginalPropertyBlocks { get; }
            = new List<MaterialPropertyBlockSnapshot>();
    }

    internal sealed class MaterialPropertyBlockSnapshot
    {
        public int MaterialIndex { get; set; }
        public MaterialPropertyBlock PropertyBlock { get; set; }
        public bool WasEmpty { get; set; }
    }
}
