using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
public class ProjectStartupConfiguration
{
    private const string STARTUP_SCENE_PATH = "Assets/Scenes/_My Scene.unity";
    private const string PREF_KEY = "HasConfiguredStartup";

    static ProjectStartupConfiguration()
    {
        EditorApplication.delayCall += ConfigureProjectStartup;
    }

    private static void ConfigureProjectStartup()
    {
        if (!EditorPrefs.GetBool(PREF_KEY, false))
        {
            // Set build target to WebGL - Unity 6 compatible
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);

            // Open startup scene
            if (System.IO.File.Exists(STARTUP_SCENE_PATH))
            {
                EditorSceneManager.OpenScene(STARTUP_SCENE_PATH);
            }

            EditorPrefs.SetBool(PREF_KEY, true);
        }
    }
}