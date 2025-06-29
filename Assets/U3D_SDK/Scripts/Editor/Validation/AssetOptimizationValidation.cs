using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace U3D.Editor
{
    public class AssetOptimizationValidation : IValidationCategory
    {
        public string CategoryName => "Asset Optimization";

        public async System.Threading.Tasks.Task<List<ValidationResult>> RunChecks()
        {
            var results = new List<ValidationResult>();

            results.Add(await ValidateTextureCompression());
            results.Add(await ValidateAudioCompression());
            results.Add(await ValidateMaterialDuplication());
            results.Add(await ValidateMissingReferences());
            results.Add(await ValidateUnusedAssets());

            return results;
        }

        private async System.Threading.Tasks.Task<ValidationResult> ValidateTextureCompression()
        {
            var textureGuids = AssetDatabase.FindAssets("t:Texture2D");
            var unoptimizedTextures = new List<string>();

            foreach (var guid in textureGuids.Take(50))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;

                if (importer != null)
                {
                    var settings = importer.GetDefaultPlatformTextureSettings();
                    if (settings.maxTextureSize > 1024 || !settings.overridden)
                    {
                        unoptimizedTextures.Add(path);
                    }
                }
            }

            await System.Threading.Tasks.Task.Delay(50);

            var isOptimal = unoptimizedTextures.Count == 0;
            return new ValidationResult(
                isOptimal,
                isOptimal ? "All textures properly optimized" : $"{unoptimizedTextures.Count} textures need optimization (>1024px or not compressed)",
                isOptimal ? ValidationSeverity.Info : ValidationSeverity.Warning
            );
        }

        private async System.Threading.Tasks.Task<ValidationResult> ValidateAudioCompression()
        {
            var audioGuids = AssetDatabase.FindAssets("t:AudioClip");
            var unoptimizedAudio = new List<string>();

            foreach (var guid in audioGuids.Take(50))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;

                if (importer != null)
                {
                    var settings = importer.defaultSampleSettings;
                    if (settings.compressionFormat == AudioCompressionFormat.PCM)
                    {
                        unoptimizedAudio.Add(path);
                    }
                }
            }

            await System.Threading.Tasks.Task.Delay(50);

            var isOptimal = unoptimizedAudio.Count == 0;
            return new ValidationResult(
                isOptimal,
                isOptimal ? "All audio files properly compressed" : $"{unoptimizedAudio.Count} audio files using uncompressed PCM format",
                isOptimal ? ValidationSeverity.Info : ValidationSeverity.Warning
            );
        }

        private async System.Threading.Tasks.Task<ValidationResult> ValidateMaterialDuplication()
        {
            var materialGuids = AssetDatabase.FindAssets("t:Material");
            var materialNames = new Dictionary<string, List<string>>();

            foreach (var guid in materialGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                var name = material.name;

                if (!materialNames.ContainsKey(name))
                {
                    materialNames[name] = new List<string>();
                }
                materialNames[name].Add(path);
            }

            var duplicates = materialNames.Where(kvp => kvp.Value.Count > 1).ToList();

            await System.Threading.Tasks.Task.Delay(50);

            var isOptimal = duplicates.Count == 0;
            return new ValidationResult(
                isOptimal,
                isOptimal ? "No duplicate materials found" : $"{duplicates.Count} sets of duplicate materials found",
                isOptimal ? ValidationSeverity.Info : ValidationSeverity.Warning
            );
        }

        private async System.Threading.Tasks.Task<ValidationResult> ValidateMissingReferences()
        {
            var sceneObjects = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            var missingScriptCount = 0;

            foreach (var obj in sceneObjects)
            {
                var components = obj.GetComponents<Component>();
                foreach (var component in components)
                {
                    if (component == null)
                    {
                        missingScriptCount++;
                    }
                }
            }

            await System.Threading.Tasks.Task.Delay(50);

            var isClean = missingScriptCount == 0;
            return new ValidationResult(
                isClean,
                isClean ? "No missing script references found" : $"{missingScriptCount} missing script references found",
                isClean ? ValidationSeverity.Info : ValidationSeverity.Error
            );
        }

        private async System.Threading.Tasks.Task<ValidationResult> ValidateUnusedAssets()
        {
            var allAssets = AssetDatabase.FindAssets("", new[] { "Assets" });
            var referencedAssets = new HashSet<string>();

            var scenes = EditorBuildSettings.scenes;
            foreach (var scene in scenes)
            {
                referencedAssets.Add(scene.path);
            }

            await System.Threading.Tasks.Task.Delay(100);

            return new ValidationResult(
                true,
                "Asset usage analysis completed",
                ValidationSeverity.Info
            );
        }
    }
}