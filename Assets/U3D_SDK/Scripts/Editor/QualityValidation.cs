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

            results.Add(ValidateQualitySettings());
            results.Add(ValidateVSyncSettings());
            results.Add(ValidateAntiAliasingSettings());
            results.Add(ValidateShadowSettings());

            await System.Threading.Tasks.Task.Delay(100);
            return results;
        }

        private ValidationResult ValidateQualitySettings()
        {
            var currentQuality = QualitySettings.GetQualityLevel();
            var qualityNames = QualitySettings.names;
            var qualityName = qualityNames[currentQuality];

            var isOptimal = qualityName.ToLower().Contains("medium") || qualityName.ToLower().Contains("good");

            return new ValidationResult(
                isOptimal,
                isOptimal ? $"Quality level appropriate: {qualityName}" : $"Consider Medium quality settings for WebGL (current: {qualityName})",
                isOptimal ? ValidationSeverity.Info : ValidationSeverity.Warning
            );
        }

        private ValidationResult ValidateVSyncSettings()
        {
            var vSyncDisabled = QualitySettings.vSyncCount == 0;
            return new ValidationResult(
                vSyncDisabled,
                vSyncDisabled ? "VSync disabled (optimal for WebGL)" : "Disable VSync for better WebGL performance",
                vSyncDisabled ? ValidationSeverity.Info : ValidationSeverity.Warning
            );
        }

        private ValidationResult ValidateAntiAliasingSettings()
        {
            var aaLevel = QualitySettings.antiAliasing;
            var isOptimal = aaLevel <= 2;

            return new ValidationResult(
                isOptimal,
                isOptimal ? $"Anti-aliasing level appropriate: {aaLevel}x" : $"High anti-aliasing ({aaLevel}x) may impact WebGL performance",
                isOptimal ? ValidationSeverity.Info : ValidationSeverity.Warning
            );
        }

        private ValidationResult ValidateShadowSettings()
        {
            var shadowDistance = QualitySettings.shadowDistance;
            var isOptimal = shadowDistance <= 50f;

            return new ValidationResult(
                isOptimal,
                isOptimal ? $"Shadow distance appropriate: {shadowDistance:F1}m" : $"Shadow distance high ({shadowDistance:F1}m) - consider reducing for WebGL",
                isOptimal ? ValidationSeverity.Info : ValidationSeverity.Warning
            );
        }
    }
}