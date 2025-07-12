using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public class ProjectStartupConfiguration
{
    private const string STARTUP_SCENE_PATH = "Assets/Scenes/_My Scene.unity";
    private const string BUILD_TARGET_KEY = "HasSetWebGLTarget";

    // Project-specific keys to avoid cross-template conflicts
    private static string BUILD_TARGET_SPECIFIC_KEY => $"{BUILD_TARGET_KEY}_{Application.dataPath.GetHashCode()}";
    private static string SCENE_LOADED_KEY => $"HasLoadedStartupScene_{Application.dataPath.GetHashCode()}";

    /// <summary>
    /// CRITICAL: Check if we should skip operations during builds
    /// </summary>
    private static bool ShouldSkipDuringBuild()
    {
        return BuildPipeline.isBuildingPlayer ||
               EditorApplication.isCompiling ||
               EditorApplication.isUpdating;
    }

    static ProjectStartupConfiguration()
    {
        //// CRITICAL: Skip initialization during builds to prevent IndexOutOfRangeException
        //if (ShouldSkipDuringBuild())
        //{
        //    Debug.Log("🚫 ProjectStartupConfiguration: Skipping initialization during build process");
        //    return;
        //}

        //// Enhanced initialization with project-specific tracking
        //EditorApplication.delayCall += () => {
        //    EditorApplication.delayCall += () => {
        //        EditorApplication.delayCall += ConfigureProjectStartup;
        //    };
        //};
    }

    private static void ConfigureProjectStartup()
    {
        // Enhanced safety checks
        if (ShouldSkipDuringBuild())
        {
            // Retry later if editor is busy
            EditorApplication.delayCall += ConfigureProjectStartup;
            return;
        }

        // FIXED: Use build guards for EditorPrefs access
        bool hasSetBuildTarget = false;
        if (!ShouldSkipDuringBuild())
        {
            hasSetBuildTarget = EditorPrefs.GetBool(BUILD_TARGET_SPECIFIC_KEY, false);
        }

        try
        {
            // CRITICAL FIX: Restore the missing hasSetBuildTarget check
            // Set build target to WebGL if not already set
            if (!hasSetBuildTarget && EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
            {
                Debug.Log("🔄 U3D SDK: Switching build target to WebGL...");

                bool success = EditorUserBuildSettings.SwitchActiveBuildTarget(
                    BuildTargetGroup.WebGL,
                    BuildTarget.WebGL
                );

                if (success)
                {
                    Debug.Log("✅ U3D SDK: Build target switched to WebGL successfully");
                    // FIXED: Use build guards for EditorPrefs access
                    if (!ShouldSkipDuringBuild())
                    {
                        EditorPrefs.SetBool(BUILD_TARGET_SPECIFIC_KEY, true);
                    }
                }
                else
                {
                    Debug.LogError("❌ U3D SDK: WEBGL BUILD SUPPORT NOT INSTALLED");
                    Debug.LogError("📋 TO FIX: Unity Hub → Installs → Your Unity Version → Add Modules → WebGL Build Support");

                    EditorUtility.DisplayDialog(
                        "WebGL Build Support Required",
                        "This Unreality3D template requires WebGL Build Support to function properly.\n\n" +
                        "To install:\n" +
                        "1. Open Unity Hub\n" +
                        "2. Go to Installs tab\n" +
                        "3. Click the gear icon next to your Unity version\n" +
                        "4. Select 'Add Modules'\n" +
                        "5. Check 'WebGL Build Support'\n" +
                        "6. Install and restart Unity",
                        "OK"
                    );

                    return;
                }
            }
            else if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.WebGL)
            {
                if (!hasSetBuildTarget)
                {
                    Debug.Log("✅ U3D SDK: Build target is already set to WebGL");
                    // FIXED: Use build guards for EditorPrefs access
                    if (!ShouldSkipDuringBuild())
                    {
                        EditorPrefs.SetBool(BUILD_TARGET_SPECIFIC_KEY, true);
                    }
                }
            }

            // ONLY load startup scene on first Unity startup, not every editor event
            bool hasLoadedStartupScene = false;
            if (!ShouldSkipDuringBuild())
            {
                hasLoadedStartupScene = EditorPrefs.GetBool(SCENE_LOADED_KEY, false);
            }

            if (!hasLoadedStartupScene && System.IO.File.Exists(STARTUP_SCENE_PATH))
            {
                var currentScene = EditorSceneManager.GetActiveScene();
                string currentScenePath = currentScene.path;

                // Only switch if we're on a truly empty/default scene
                bool isEmptyScene = string.IsNullOrEmpty(currentScenePath) ||
                                   currentScenePath.Contains("Untitled") ||
                                   currentScenePath.Contains("New Scene");

                Debug.Log($"🔍 U3D SDK: First startup - Current scene: '{currentScenePath}' | Is empty: {isEmptyScene}");

                if (isEmptyScene)
                {
                    EditorSceneManager.OpenScene(STARTUP_SCENE_PATH);
                    Debug.Log($"✅ U3D SDK: Opened startup scene on first launch: {STARTUP_SCENE_PATH}");
                }

                // Mark as completed so we never do this again
                // FIXED: Use build guards for EditorPrefs access
                if (!ShouldSkipDuringBuild())
                {
                    EditorPrefs.SetBool(SCENE_LOADED_KEY, true);
                }
            }
            else if (hasLoadedStartupScene)
            {
                Debug.Log("✅ U3D SDK: Startup scene already loaded previously, respecting user's current scene");
            }
            else if (!System.IO.File.Exists(STARTUP_SCENE_PATH))
            {
                Debug.LogWarning($"⚠️ U3D SDK: Startup scene not found: {STARTUP_SCENE_PATH}");
                Debug.LogWarning("💡 U3D SDK: Please ensure your main scene is located at Assets/Scenes/_My Scene.unity");

                // List available scenes for debugging
                if (System.IO.Directory.Exists("Assets/Scenes/"))
                {
                    string[] allScenes = System.IO.Directory.GetFiles("Assets/Scenes/", "*.unity");
                    Debug.Log($"📁 U3D SDK: Available scenes: {string.Join(", ", allScenes)}");
                }
            }

            Debug.Log("✅ U3D SDK: Project startup configuration complete");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ U3D SDK: Error during startup configuration: {e.Message}");
        }
    }
}