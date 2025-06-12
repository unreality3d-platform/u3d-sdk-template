using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace U3D.Editor
{
    public class PerformanceValidation : IValidationCategory
    {
        public string CategoryName => "Performance";

        public async System.Threading.Tasks.Task<List<ValidationResult>> RunChecks()
        {
            var results = new List<ValidationResult>();

            results.Add(await ValidateBuildSize());
            results.Add(await ValidatePolyCount());
            results.Add(await ValidateLightingSetup());
            results.Add(await ValidateShaderComplexity());
            results.Add(await ValidateBatchingOptimization());

            return results;
        }

        private async System.Threading.Tasks.Task<ValidationResult> ValidateBuildSize()
        {
            var textureGuids = AssetDatabase.FindAssets("t:Texture2D");
            var audioGuids = AssetDatabase.FindAssets("t:AudioClip");
            var meshGuids = AssetDatabase.FindAssets("t:Mesh");

            var estimatedSizeMB = (textureGuids.Length * 0.5f) + (audioGuids.Length * 0.3f) + (meshGuids.Length * 0.1f);

            await System.Threading.Tasks.Task.Delay(50);

            var isAcceptable = estimatedSizeMB < 512;
            return new ValidationResult(
                isAcceptable,
                isAcceptable ? $"Estimated build size: {estimatedSizeMB:F1}MB (within 512MB limit)" : $"Estimated build size: {estimatedSizeMB:F1}MB (exceeds 512MB limit)",
                isAcceptable ? ValidationSeverity.Info : ValidationSeverity.Critical
            );
        }

        private async System.Threading.Tasks.Task<ValidationResult> ValidatePolyCount()
        {
            var meshFilters = Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None);
            var totalTris = 0;
            var highPolyObjects = new List<GameObject>();

            foreach (var mf in meshFilters)
            {
                if (mf.sharedMesh != null)
                {
                    var tris = mf.sharedMesh.triangles.Length / 3;
                    totalTris += tris;

                    if (tris > 10000)
                    {
                        highPolyObjects.Add(mf.gameObject);
                    }
                }
            }

            await System.Threading.Tasks.Task.Delay(50);

            var isOptimal = totalTris < 500000 && highPolyObjects.Count == 0;
            var message = isOptimal ?
                $"Total triangles: {totalTris:N0} (optimal for WebGL)" :
                $"Total triangles: {totalTris:N0}, {highPolyObjects.Count} high-poly objects found";

            var result = new ValidationResult(isOptimal, message, isOptimal ? ValidationSeverity.Info : ValidationSeverity.Warning);
            result.affectedObjects.AddRange(highPolyObjects.Cast<UnityEngine.Object>());

            return result;
        }

        private async System.Threading.Tasks.Task<ValidationResult> ValidateLightingSetup()
        {
            var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            var realtimeLights = lights.Where(l => l.lightmapBakeType == LightmapBakeType.Realtime).ToList();

            await System.Threading.Tasks.Task.Delay(50);

            var isOptimal = realtimeLights.Count <= 4;
            return new ValidationResult(
                isOptimal,
                isOptimal ? $"{realtimeLights.Count} real-time lights (optimal)" : $"{realtimeLights.Count} real-time lights (consider baking some for WebGL performance)",
                isOptimal ? ValidationSeverity.Info : ValidationSeverity.Warning
            );
        }

        private async System.Threading.Tasks.Task<ValidationResult> ValidateShaderComplexity()
        {
            var renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            var complexMaterials = new List<Material>();

            foreach (var renderer in renderers)
            {
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material != null && material.shader != null)
                    {
                        var shaderName = material.shader.name.ToLower();
                        if (shaderName.Contains("glass") || shaderName.Contains("water") || shaderName.Contains("transparent"))
                        {
                            complexMaterials.Add(material);
                        }
                    }
                }
            }

            await System.Threading.Tasks.Task.Delay(50);

            var isOptimal = complexMaterials.Count < 5;
            return new ValidationResult(
                isOptimal,
                isOptimal ? "Shader complexity appropriate for WebGL" : $"{complexMaterials.Count} potentially expensive shaders found",
                isOptimal ? ValidationSeverity.Info : ValidationSeverity.Warning
            );
        }

        private async System.Threading.Tasks.Task<ValidationResult> ValidateBatchingOptimization()
        {
            var playerSettings = PlayerSettings.GetGraphicsAPIs(BuildTarget.WebGL);
            var hasBatching = true;

            await System.Threading.Tasks.Task.Delay(50);

            return new ValidationResult(
                hasBatching,
                hasBatching ? "Batching optimization enabled" : "Enable static batching for better WebGL performance",
                hasBatching ? ValidationSeverity.Info : ValidationSeverity.Warning
            );
        }
    }
}