using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

namespace U3D.Editor
{
    public class BuildSettingsValidation : IValidationCategory
    {
        public string CategoryName => "Build Settings";

        public async System.Threading.Tasks.Task<List<ValidationResult>> RunChecks()
        {
            var results = new List<ValidationResult>();

            results.Add(ValidateWebGLCompression());
            results.Add(ValidateWebGLMemorySize());
            results.Add(ValidateColorSpace());
            results.Add(ValidateRenderPipeline());
            results.Add(ValidateFirebaseIntegration());
            results.Add(ValidatePlayerControllerSetup());

            await System.Threading.Tasks.Task.Delay(100);
            return results;
        }

        private ValidationResult ValidateWebGLCompression()
        {
            var compressionOk = PlayerSettings.WebGL.compressionFormat == WebGLCompressionFormat.Disabled;
            return new ValidationResult(
                compressionOk,
                compressionOk ? "WebGL compression properly disabled" : "WebGL compression must be disabled for GitHub Pages compatibility",
                compressionOk ? ValidationSeverity.Info : ValidationSeverity.Critical
            );
        }

        private ValidationResult ValidateWebGLMemorySize()
        {
            var memorySizeOk = PlayerSettings.WebGL.memorySize == 512;
            return new ValidationResult(
                memorySizeOk,
                memorySizeOk ? "WebGL memory size set correctly (512MB)" : "WebGL memory size should be 512MB for optimal performance",
                memorySizeOk ? ValidationSeverity.Info : ValidationSeverity.Warning
            );
        }

        private ValidationResult ValidateColorSpace()
        {
            var colorSpaceOk = PlayerSettings.colorSpace == ColorSpace.Linear;
            return new ValidationResult(
                colorSpaceOk,
                colorSpaceOk ? "Color space set to Linear" : "Color space should be Linear for better rendering",
                colorSpaceOk ? ValidationSeverity.Info : ValidationSeverity.Warning
            );
        }

        private ValidationResult ValidateRenderPipeline()
        {
            var urpAsset = GraphicsSettings.defaultRenderPipeline;
            var hasURP = urpAsset != null;
            return new ValidationResult(
                hasURP,
                hasURP ? "Universal Render Pipeline configured" : "Consider using URP for better WebGL performance",
                hasURP ? ValidationSeverity.Info : ValidationSeverity.Warning
            );
        }

        private ValidationResult ValidateFirebaseIntegration()
        {
            var firebaseScript = AssetDatabase.FindAssets("FirebaseIntegration").Length > 0;
            var firebasePlugin = AssetDatabase.FindAssets("FirebasePlugin").Length > 0;

            var hasIntegration = firebaseScript && firebasePlugin;
            return new ValidationResult(
                hasIntegration,
                hasIntegration ? "Firebase integration components found" : "Missing Firebase integration - required for U3D backend",
                hasIntegration ? ValidationSeverity.Info : ValidationSeverity.Critical
            );
        }

        private ValidationResult ValidatePlayerControllerSetup()
        {
            var hasPlayerController = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Any(mb => mb.GetType().Name == "U3DPlayerController");
            return new ValidationResult(
                hasPlayerController,
                hasPlayerController ? "U3D Player Controller found in scene" : "Add U3D Player Controller for standard movement controls",
                hasPlayerController ? ValidationSeverity.Info : ValidationSeverity.Warning
            );
        }
    }
}