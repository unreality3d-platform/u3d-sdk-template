using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace U3D.Editor
{
    public class QualityValidation : IValidationCategory
    {
        public string CategoryName => "Quality Settings";

        public async System.Threading.Tasks.Task<List<ValidationResult>> RunChecks()
        {
            var results = new List<ValidationResult>();

            // REMOVED per your request:
            // - Shadow settings
            // - Anti-aliasing levels  
            // - Lighting setup
            // - Shader complexity

            // Only keeping non-quality related checks that are genuinely informational
            results.Add(ValidateQualityLevelNaming());
            results.Add(ValidateWebGLSpecificSettings());

            await System.Threading.Tasks.Task.Delay(100);
            return results;
        }

        private ValidationResult ValidateQualityLevelNaming()
        {
            var currentQuality = QualitySettings.GetQualityLevel();
            var qualityNames = QualitySettings.names;
            var qualityName = qualityNames[currentQuality];

            // Simply report current quality level - no enforcement
            return new ValidationResult(
                true, // Always pass - just informational
                $"ℹ️ Current quality level: {qualityName}",
                ValidationSeverity.Info
            );
        }

        private ValidationResult ValidateWebGLSpecificSettings()
        {
            // Just a general reminder about WebGL optimization - no specific enforcement
            return new ValidationResult(
                true, // Always pass - just informational
                "💡 Remember: WebGL performance is primarily managed by UnityBuildHelper during build process",
                ValidationSeverity.Info
            );
        }
    }
}