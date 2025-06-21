using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
public class ProjectStartupConfiguration
{
    private const string STARTUP_SCENE_PATH = "Assets/Scenes/_My Scene.unity";
    private const string PREF_KEY = "HasConfiguredStartup";
    private const string BUILD_TARGET_KEY = "HasSetWebGLTarget";

    static ProjectStartupConfiguration()
    {
        // Multiple delay calls with additional safety checks
        EditorApplication.delayCall += () => {
            EditorApplication.delayCall += () => {
                EditorApplication.delayCall += ConfigureProjectStartup;
            };
        };
    }

    private static void ConfigureProjectStartup()
    {
        // Enhanced safety checks
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling ||
            EditorApplication.isUpdating ||
            BuildPipeline.isBuildingPlayer)
        {
            // Retry later if editor is busy
            EditorApplication.delayCall += ConfigureProjectStartup;
            return;
        }

        // Check if this is the first time opening this project
        bool hasConfiguredStartup = EditorPrefs.GetBool(PREF_KEY, false);
        bool hasSetBuildTarget = EditorPrefs.GetBool(BUILD_TARGET_KEY, false);

        if (!hasConfiguredStartup || !hasSetBuildTarget)
        {
            try
            {
                // Set build target to WebGL if not already set
                if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
                {
                    Debug.Log("🔄 U3D SDK: Switching build target to WebGL...");

                    // Use the synchronous version which is more reliable for startup
                    bool success = EditorUserBuildSettings.SwitchActiveBuildTarget(
                        BuildTargetGroup.WebGL,
                        BuildTarget.WebGL
                    );

                    if (success)
                    {
                        Debug.Log("✅ U3D SDK: Build target switched to WebGL successfully");
                        EditorPrefs.SetBool(BUILD_TARGET_KEY, true);
                    }
                    else
                    {
                        Debug.LogError("❌ U3D SDK: WEBGL BUILD SUPPORT NOT INSTALLED");
                        Debug.LogError("📋 TO FIX: Unity Hub → Installs → Your Unity Version → Add Modules → WebGL Build Support");
                        Debug.LogError("🔄 After installing WebGL support, use menu: U3D/Reset Startup Configuration");

                        // Show user-friendly dialog
                        EditorUtility.DisplayDialog(
                            "WebGL Build Support Required",
                            "This Unreality3D template requires WebGL Build Support to function properly.\n\n" +
                            "To install:\n" +
                            "1. Open Unity Hub\n" +
                            "2. Go to Installs tab\n" +
                            "3. Click the gear icon next to your Unity version\n" +
                            "4. Select 'Add Modules'\n" +
                            "5. Check 'WebGL Build Support'\n" +
                            "6. Install and restart Unity\n\n" +
                            "After installation, use menu: U3D → Reset Startup Configuration",
                            "Open Unity Hub"
                        );

                        // Don't set the pref key so it retries next time
                        return;
                    }
                }
                else
                {
                    Debug.Log("✅ U3D SDK: Build target is already set to WebGL");
                    EditorPrefs.SetBool(BUILD_TARGET_KEY, true);
                }

                // Open startup scene with enhanced safety checks
                if (!hasConfiguredStartup)
                {
                    if (System.IO.File.Exists(STARTUP_SCENE_PATH))
                    {
                        // Additional check to ensure scene isn't already open
                        var currentScene = EditorSceneManager.GetActiveScene();
                        if (currentScene.path != STARTUP_SCENE_PATH)
                        {
                            try
                            {
                                EditorSceneManager.OpenScene(STARTUP_SCENE_PATH);
                                Debug.Log($"✅ U3D SDK: Opened startup scene: {STARTUP_SCENE_PATH}");
                            }
                            catch (System.Exception e)
                            {
                                Debug.LogWarning($"⚠️ U3D SDK: Could not open startup scene: {e.Message}");
                            }
                        }
                        else
                        {
                            Debug.Log($"✅ U3D SDK: Startup scene already active: {STARTUP_SCENE_PATH}");
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"⚠️ U3D SDK: Startup scene not found: {STARTUP_SCENE_PATH}");
                        Debug.LogWarning("💡 U3D SDK: Please ensure your main scene is located at Assets/Scenes/_My Scene.unity");
                    }

                    EditorPrefs.SetBool(PREF_KEY, true);
                }

                Debug.Log("✅ U3D SDK: Project startup configuration complete");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"❌ U3D SDK: Error during startup configuration: {e.Message}");
                // Don't set pref keys on error so it retries next time
            }
        }
    }

    // Optional: Add menu item to reset configuration for testing
    [MenuItem("U3D/Reset Startup Configuration")]
    private static void ResetStartupConfiguration()
    {
        EditorPrefs.DeleteKey(PREF_KEY);
        EditorPrefs.DeleteKey(BUILD_TARGET_KEY);
        Debug.Log("🔄 U3D SDK: Startup configuration reset. Restart Unity to reconfigure.");
    }

    // Optional: Add menu item to manually set WebGL target
    [MenuItem("U3D/Force WebGL Build Target")]
    private static void ForceWebGLTarget()
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
        {
            bool success = EditorUserBuildSettings.SwitchActiveBuildTarget(
                BuildTargetGroup.WebGL,
                BuildTarget.WebGL
            );

            if (success)
            {
                Debug.Log("✅ U3D SDK: Manually switched to WebGL build target");
                EditorPrefs.SetBool(BUILD_TARGET_KEY, true);
            }
            else
            {
                Debug.LogError("❌ U3D SDK: Failed to switch to WebGL. Check WebGL support installation.");
            }
        }
        else
        {
            Debug.Log("✅ U3D SDK: Already using WebGL build target");
        }
    }
}