using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace U3D.Editor
{
    public class PerformanceValidation : IValidationCategory
    {
        public string CategoryName => "Performance Analysis";

        public async System.Threading.Tasks.Task<List<ValidationResult>> RunChecks()
        {
            var results = new List<ValidationResult>();

            // REMOVED per your request:
            // - Shadow settings validation
            // - Anti-aliasing level checks
            // - Lighting setup optimization
            // - Shader complexity analysis

            // Keeping only asset-level performance checks that don't relate to quality settings
            results.Add(await ValidatePolygonCount());
            results.Add(await ValidateTextureMemoryUsage());
            results.Add(await ValidateAudioFileCount());

            return results;
        }

        private async System.Threading.Tasks.Task<ValidationResult> ValidatePolygonCount()
        {
            var meshFilters = UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None);
            int totalTris = 0;
            var highPolyObjects = new List<GameObject>();

            foreach (var meshFilter in meshFilters)
            {
                if (meshFilter.sharedMesh != null)
                {
                    int tris = meshFilter.sharedMesh.triangles.Length / 3;
                    totalTris += tris;

                    if (tris > 10000) // High poly threshold
                    {
                        highPolyObjects.Add(meshFilter.gameObject);
                    }
                }
            }

            await System.Threading.Tasks.Task.Delay(50);

            var isOptimal = totalTris < 100000; // WebGL optimal threshold
            var message = isOptimal ?
                $"✅ Total triangles: {totalTris:N0} (good for WebGL)" :
                $"⚠️ Total triangles: {totalTris:N0}, consider optimizing {highPolyObjects.Count} high-poly objects";

            var result = new ValidationResult(isOptimal, message, isOptimal ? ValidationSeverity.Info : ValidationSeverity.Warning);
            result.affectedObjects.AddRange(highPolyObjects.Cast<UnityEngine.Object>());

            return result;
        }

        private async System.Threading.Tasks.Task<ValidationResult> ValidateTextureMemoryUsage()
        {
            var textureGuids = AssetDatabase.FindAssets("t:Texture2D");
            long totalTextureMemory = 0;
            int largeTextureCount = 0;

            foreach (var guid in textureGuids.Take(100)) // Sample for performance
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);

                if (texture != null)
                {
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer != null)
                    {
                        var settings = importer.GetDefaultPlatformTextureSettings();
                        if (settings.maxTextureSize > 1024)
                        {
                            largeTextureCount++;
                        }
                    }

                    // Estimate memory usage (rough calculation)
                    int pixels = texture.width * texture.height;
                    totalTextureMemory += pixels * 4; // Assume 4 bytes per pixel
                }
            }

            await System.Threading.Tasks.Task.Delay(50);

            var memoryMB = totalTextureMemory / (1024 * 1024);
            var isOptimal = memoryMB < 100; // 100MB threshold for WebGL

            return new ValidationResult(
                isOptimal,
                isOptimal ?
                    $"✅ Estimated texture memory: {memoryMB}MB (good for WebGL)" :
                    $"⚠️ Estimated texture memory: {memoryMB}MB, {largeTextureCount} large textures found",
                isOptimal ? ValidationSeverity.Info : ValidationSeverity.Warning
            );
        }

        private async System.Threading.Tasks.Task<ValidationResult> ValidateAudioFileCount()
        {
            var audioClips = UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
            var playOnAwakeCount = audioClips.Count(a => a.playOnAwake);

            await System.Threading.Tasks.Task.Delay(50);

            var isOptimal = playOnAwakeCount <= 2;
            return new ValidationResult(
                isOptimal,
                isOptimal ?
                    $"✅ Audio sources with Play On Awake: {playOnAwakeCount} (optimal for WebGL loading)" :
                    $"⚠️ {playOnAwakeCount} audio sources set to Play On Awake (may slow WebGL loading)",
                isOptimal ? ValidationSeverity.Info : ValidationSeverity.Warning
            );
        }
    }
}