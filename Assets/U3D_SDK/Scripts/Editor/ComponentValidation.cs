using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace U3D.Editor
{
    public class ComponentValidation : IValidationCategory
    {
        public string CategoryName => "Components & Setup";

        public async System.Threading.Tasks.Task<List<ValidationResult>> RunChecks()
        {
            var results = new List<ValidationResult>();

            results.Add(ValidateRequiredComponents());
            results.Add(ValidateSpawnPoints());
            results.Add(ValidateAudioConfiguration());
            results.Add(ValidateInputSystem());

            await System.Threading.Tasks.Task.Delay(100);
            return results;
        }

        private ValidationResult ValidateRequiredComponents()
        {
            var hasFirebaseIntegration = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Any(mb => mb.GetType().Name == "FirebaseIntegration");
            var hasPlayerController = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Any(mb => mb.GetType().Name == "U3DPlayerController");

            var componentsFound = 0;
            var missingComponents = new List<string>();

            if (hasFirebaseIntegration) componentsFound++;
            else missingComponents.Add("FirebaseIntegration");

            if (hasPlayerController) componentsFound++;
            else missingComponents.Add("U3DPlayerController");

            var isComplete = componentsFound == 2;
            var message = isComplete ?
                "All required U3D components found" :
                $"Missing components: {string.Join(", ", missingComponents)}";

            return new ValidationResult(isComplete, message, isComplete ? ValidationSeverity.Info : ValidationSeverity.Error);
        }

        private ValidationResult ValidateSpawnPoints()
        {
            var spawnPoints = GameObject.FindGameObjectsWithTag("Respawn");
            var hasSpawnPoints = spawnPoints.Length > 0;

            return new ValidationResult(
                hasSpawnPoints,
                hasSpawnPoints ? $"{spawnPoints.Length} spawn points found" : "Add spawn points for player positioning",
                hasSpawnPoints ? ValidationSeverity.Info : ValidationSeverity.Warning
            );
        }

        private ValidationResult ValidateAudioConfiguration()
        {
            var audioSources = Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
            var playOnAwakeSources = audioSources.Where(a => a.playOnAwake).ToList();

            var isOptimal = playOnAwakeSources.Count <= 2;
            return new ValidationResult(
                isOptimal,
                isOptimal ? "Audio configuration appropriate" : $"{playOnAwakeSources.Count} audio sources set to play on awake (may impact WebGL loading)",
                isOptimal ? ValidationSeverity.Info : ValidationSeverity.Warning
            );
        }

        private ValidationResult ValidateInputSystem()
        {
            var inputActions = AssetDatabase.FindAssets("t:InputActionAsset");
            var hasInputActions = inputActions.Length > 0;

            return new ValidationResult(
                hasInputActions,
                hasInputActions ? "Input System configured" : "Consider setting up Input System for better control handling",
                hasInputActions ? ValidationSeverity.Info : ValidationSeverity.Warning
            );
        }
    }
}