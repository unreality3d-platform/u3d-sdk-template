using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Reporting;
using System.IO;
using System.Threading.Tasks;
using System;
using System.Linq;

namespace U3D.Editor
{
    public static class UnityBuildHelper
    {
        public static async Task<UnityBuildResult> BuildWebGL(string outputPath, System.Action<string> onProgress = null)
        {
            // Ensure we're on the main thread for Unity operations
            await Awaitable.MainThreadAsync();

            try
            {
                onProgress?.Invoke("Preparing WebGL build...");

                // Ensure output directory exists
                if (Directory.Exists(outputPath))
                {
                    Directory.Delete(outputPath, true);
                }
                Directory.CreateDirectory(outputPath);

                // Get all scenes in build settings
                var scenes = EditorBuildSettings.scenes
                    .Where(scene => scene.enabled)
                    .Select(scene => scene.path)
                    .ToArray();

                if (scenes.Length == 0)
                {
                    return new UnityBuildResult
                    {
                        Success = false,
                        ErrorMessage = "No scenes enabled in Build Settings. Please add at least one scene."
                    };
                }

                onProgress?.Invoke($"Building {scenes.Length} scene(s) to WebGL...");

                // Configure build options
                var buildPlayerOptions = new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = outputPath,
                    target = BuildTarget.WebGL,
                    options = BuildOptions.None
                };

                // Optimize WebGL build settings
                SetOptimalWebGLSettings();

                onProgress?.Invoke("Starting Unity build process...");

                // Build the player - this must run on main thread
                var report = BuildPipeline.BuildPlayer(buildPlayerOptions);
                var summary = report.summary;

                if (summary.result == BuildResult.Succeeded)
                {
                    onProgress?.Invoke("Build completed successfully!");

                    return new UnityBuildResult
                    {
                        Success = true,
                        BuildPath = outputPath,
                        BuildSize = (long)summary.totalSize,
                        BuildTime = summary.totalTime,
                        Message = $"Build completed in {summary.totalTime.TotalSeconds:F1} seconds"
                    };
                }
                else
                {
                    var errorMessage = "Build failed";

                    if (summary.result == BuildResult.Failed)
                    {
                        errorMessage = "Build failed. Check the Console for detailed error messages.";
                    }
                    else if (summary.result == BuildResult.Cancelled)
                    {
                        errorMessage = "Build was cancelled.";
                    }

                    return new UnityBuildResult
                    {
                        Success = false,
                        ErrorMessage = errorMessage,
                        BuildTime = summary.totalTime
                    };
                }
            }
            catch (Exception ex)
            {
                return new UnityBuildResult
                {
                    Success = false,
                    ErrorMessage = $"Build process failed: {ex.Message}"
                };
            }
        }

        public static async Task<bool> CopyBuildToRepository(string buildPath, string repositoryPath)
        {
            // File operations can safely run on background thread
            return await Task.Run(() =>
            {
                try
                {
                    var sourceDirectory = new DirectoryInfo(buildPath);
                    var targetDirectory = new DirectoryInfo(repositoryPath);

                    if (!sourceDirectory.Exists)
                    {
                        Debug.LogError($"Build source directory does not exist: {buildPath}");
                        return false;
                    }

                    // Copy all build files to repository root
                    CopyDirectoryRecursively(sourceDirectory, targetDirectory);

                    Debug.Log($"Build files copied from {buildPath} to {repositoryPath}");
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to copy build to repository: {ex.Message}");
                    return false;
                }
            });
        }

        public static bool ValidateBuildRequirements()
        {
            // Check if WebGL support is installed
            var webglSupport = BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL);
            if (!webglSupport)
            {
                Debug.LogError("WebGL build support is not installed. Please install WebGL Build Support through Unity Hub.");
                return false;
            }

            // Check if there are scenes in build settings
            var enabledScenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).ToArray();
            if (enabledScenes.Length == 0)
            {
                Debug.LogError("No scenes are enabled in Build Settings. Please add at least one scene to build.");
                return false;
            }

            return true;
        }

        public static void SetOptimalWebGLSettings()
        {
            // Set WebGL-specific player settings for optimal performance
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.memorySize = 256; // MB
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.WebGL.nameFilesAsHashes = true;

            // Set quality settings for WebGL
            QualitySettings.pixelLightCount = 1;
            QualitySettings.shadows = ShadowQuality.Disable;
            QualitySettings.shadowResolution = ShadowResolution.Low;

            Debug.Log("WebGL build settings optimized for web deployment");
        }

        public static string GetDefaultBuildPath()
        {
            var projectPath = Path.GetDirectoryName(Application.dataPath);
            return Path.Combine(projectPath, "WebGLBuild");
        }

        public static long GetBuildSize(string buildPath)
        {
            try
            {
                if (!Directory.Exists(buildPath))
                {
                    return 0;
                }

                var directory = new DirectoryInfo(buildPath);
                return directory.EnumerateFiles("*", SearchOption.AllDirectories).Sum(file => file.Length);
            }
            catch
            {
                return 0;
            }
        }

        private static void CopyDirectoryRecursively(DirectoryInfo source, DirectoryInfo target)
        {
            // Create target directory if it doesn't exist
            if (!target.Exists)
            {
                target.Create();
            }

            // Copy all files
            foreach (var file in source.GetFiles())
            {
                var targetFile = Path.Combine(target.FullName, file.Name);
                file.CopyTo(targetFile, true);
            }

            // Copy all subdirectories
            foreach (var subdir in source.GetDirectories())
            {
                var targetSubdir = target.CreateSubdirectory(subdir.Name);
                CopyDirectoryRecursively(subdir, targetSubdir);
            }
        }
    }

    [System.Serializable]
    public class UnityBuildResult
    {
        public bool Success { get; set; }
        public string BuildPath { get; set; }
        public long BuildSize { get; set; }
        public TimeSpan BuildTime { get; set; }
        public string Message { get; set; }
        public string ErrorMessage { get; set; }
    }
}