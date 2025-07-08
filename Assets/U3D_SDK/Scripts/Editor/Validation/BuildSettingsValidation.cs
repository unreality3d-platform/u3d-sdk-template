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

            // NOTE: UnityBuildHelper now handles critical build validation as authority
            // These checks are now INFORMATIONAL and provide guidance only

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
            // INFORMATIONAL ONLY - UnityBuildHelper enforces this setting
            var compressionOk = PlayerSettings.WebGL.compressionFormat == WebGLCompressionFormat.Disabled;
            return new ValidationResult(
                compressionOk,
                compressionOk ?
                    "✅ WebGL compression properly disabled for GitHub Pages compatibility" :
                    "ℹ️ UnityBuildHelper will set compression to Disabled for GitHub Pages compatibility during build",
                ValidationSeverity.Info // Always informational now
            );
        }

        private ValidationResult ValidateWebGLMemorySize()
        {
            // Unity 6+ uses automatic WebAssembly memory management
            return new ValidationResult(
                true, // Always optimal in Unity 6+
                "✅ Unity 6+ automatic WebAssembly memory management (manual sizing deprecated)",
                ValidationSeverity.Info
            );
        }

        private ValidationResult ValidateColorSpace()
        {
            var colorSpaceOk = PlayerSettings.colorSpace == ColorSpace.Linear;
            return new ValidationResult(
                colorSpaceOk,
                colorSpaceOk ? "✅ Color space set to Linear for optimal rendering" : "⚠️ Consider setting color space to Linear for better rendering quality",
                colorSpaceOk ? ValidationSeverity.Info : ValidationSeverity.Warning
            );
        }

        private ValidationResult ValidateRenderPipeline()
        {
            var urpAsset = GraphicsSettings.defaultRenderPipeline;
            var hasURP = urpAsset != null;
            return new ValidationResult(
                hasURP,
                hasURP ? "✅ Universal Render Pipeline configured for enhanced WebGL features" : "💡 Consider using URP for enhanced WebGL performance and visual features",
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
                hasIntegration ? "✅ Firebase integration components found" : "❌ Missing Firebase integration - required for U3D backend functionality",
                hasIntegration ? ValidationSeverity.Info : ValidationSeverity.Error
            );
        }

        private ValidationResult ValidatePlayerControllerSetup()
        {
            var hasPlayerController = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .Any(mb => mb.GetType().Name == "U3DPlayerController");
            return new ValidationResult(
                hasPlayerController,
                hasPlayerController ?
                    "✅ U3D Player Controller found in scene" :
                    "💡 Consider adding U3D Player Controller for standard movement controls",
                hasPlayerController ? ValidationSeverity.Info : ValidationSeverity.Warning
            );
        }
    }
}