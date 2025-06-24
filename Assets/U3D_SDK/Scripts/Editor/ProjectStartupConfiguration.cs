using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
public class ProjectStartupConfiguration
{
    private const string STARTUP_SCENE_PATH = "Assets/Scenes/_My Scene.unity";
    private const string PREF_KEY = "HasConfiguredStartup";
    private const string BUILD_TARGET_KEY = "HasSetWebGLTarget";

    // Project-specific key to avoid cross-template conflicts
    private static string PROJECT_SPECIFIC_KEY => $"{PREF_KEY}_{Application.dataPath.GetHashCode()}";
    private static string BUILD_TARGET_SPECIFIC_KEY => $"{BUILD_TARGET_KEY}_{Application.dataPath.GetHashCode()}";

    static ProjectStartupConfiguration()
    {
        // Enhanced initialization with project-specific tracking
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

        // Use project-specific keys to avoid conflicts between template downloads
        bool hasConfiguredStartup = EditorPrefs.GetBool(PROJECT_SPECIFIC_KEY, false);
        bool hasSetBuildTarget = EditorPrefs.GetBool(BUILD_TARGET_SPECIFIC_KEY, false);

        // Force scene check on every first load regardless of previous preference
        bool forceSceneCheck = !hasConfiguredStartup;

        if (!hasConfiguredStartup || !hasSetBuildTarget || forceSceneCheck)
        {
            try
            {
                // Set build target to WebGL if not already set
                if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
                {
                    Debug.Log("🔄 U3D SDK: Switching build target to WebGL...");

                    bool success = EditorUserBuildSettings.SwitchActiveBuildTarget(
                        BuildTargetGroup.WebGL,
                        BuildTarget.WebGL
                    );

                    if (success)
                    {
                        Debug.Log("✅ U3D SDK: Build target switched to WebGL successfully");
                        EditorPrefs.SetBool(BUILD_TARGET_SPECIFIC_KEY, true);
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
                else
                {
                    Debug.Log("✅ U3D SDK: Build target is already set to WebGL");
                    EditorPrefs.SetBool(BUILD_TARGET_SPECIFIC_KEY, true);
                }

                // Enhanced scene opening with additional verification
                if (forceSceneCheck || !hasConfiguredStartup)
                {
                    if (System.IO.File.Exists(STARTUP_SCENE_PATH))
                    {
                        var currentScene = EditorSceneManager.GetActiveScene();

                        // More robust scene path comparison
                        string currentScenePath = currentScene.path;
                        bool isCorrectScene = string.Equals(currentScenePath, STARTUP_SCENE_PATH, System.StringComparison.OrdinalIgnoreCase);

                        Debug.Log($"🔍 U3D SDK: Current scene: '{currentScenePath}' | Target: '{STARTUP_SCENE_PATH}' | Match: {isCorrectScene}");

                        if (!isCorrectScene)
                        {
                            try
                            {
                                // Additional delay to ensure Unity is fully ready
                                EditorApplication.delayCall += () => {
                                    if (!EditorApplication.isPlayingOrWillChangePlaymode &&
                                        !EditorApplication.isCompiling)
                                    {
                                        EditorSceneManager.OpenScene(STARTUP_SCENE_PATH);
                                        Debug.Log($"✅ U3D SDK: Successfully opened startup scene: {STARTUP_SCENE_PATH}");
                                    }
                                };
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

                        // List available scenes for debugging
                        if (System.IO.Directory.Exists("Assets/Scenes/"))
                        {
                            string[] allScenes = System.IO.Directory.GetFiles("Assets/Scenes/", "*.unity");
                            Debug.Log($"📁 U3D SDK: Available scenes: {string.Join(", ", allScenes)}");
                        }
                    }

                    EditorPrefs.SetBool(PROJECT_SPECIFIC_KEY, true);
                }

                Debug.Log("✅ U3D SDK: Project startup configuration complete");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"❌ U3D SDK: Error during startup configuration: {e.Message}");
            }
        }
    }
}