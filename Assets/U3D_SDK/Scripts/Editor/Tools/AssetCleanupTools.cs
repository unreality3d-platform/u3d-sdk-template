using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace U3D.Editor
{
    /// <summary>
    /// Asset cleanup utilities for the U3D SDK.
    /// Handles missing script detection, replacement, and cleanup.
    /// </summary>
    public static class AssetCleanupTools
    {
        /// <summary>
        /// Replace missing script references with placeholder components
        /// </summary>
        public static void ReplaceMissingScriptsWithPlaceholders()
        {
            int replacedCount = 0;
            int suggestionsFound = 0;
            GameObject[] allGameObjects = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);

            foreach (GameObject go in allGameObjects)
            {
                int numComponents = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
                if (numComponents > 0)
                {
                    // Register undo operation
                    Undo.RegisterCompleteObjectUndo(go, "Replace missing scripts with placeholders");

                    // Check for suggestions before adding placeholders
                    string suggestion = ComponentSuggestions.GetSuggestionForGameObject(go.name);
                    if (!string.IsNullOrEmpty(suggestion))
                    {
                        suggestionsFound++;
                    }

                    // Add placeholder components (suggestions auto-populate in Awake)
                    for (int i = 0; i < numComponents; i++)
                    {
                        go.AddComponent<MissingScriptPlaceholder>();
                    }

                    // Remove missing script components
                    GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
                    replacedCount += numComponents;

                    // Mark object as dirty
                    EditorUtility.SetDirty(go);
                }
            }

            if (replacedCount > 0)
            {
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                Debug.Log($"🔧 U3D SDK: Replaced {replacedCount} missing scripts with placeholder components.");

                if (suggestionsFound > 0)
                {
                    Debug.Log($"💡 U3D SDK: Found {suggestionsFound} objects with replacement suggestions! Check placeholder components in Inspector for details.");
                }
            }
            else
            {
                Debug.Log("✅ U3D SDK: No missing scripts found in scene.");
            }
        }

        /// <summary>
        /// Remove all MissingScriptPlaceholder components from the scene
        /// </summary>
        public static void RemovePlaceholderComponents()
        {
            int removedCount = 0;
            GameObject[] allObjects = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);

            foreach (GameObject go in allObjects)
            {
                var placeholders = go.GetComponents<MissingScriptPlaceholder>();
                foreach (var placeholder in placeholders)
                {
                    Undo.DestroyObjectImmediate(placeholder);
                    removedCount++;
                }
            }

            if (removedCount > 0)
            {
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                Debug.Log($"🧼 U3D SDK: Removed {removedCount} placeholder component(s) from scene.");
            }
            else
            {
                Debug.Log("✅ U3D SDK: No placeholder components found in scene.");
            }
        }

        /// <summary>
        /// Remove missing scripts directly from scene GameObjects
        /// </summary>
        public static void RemoveMissingScriptsFromScene()
        {
            int removedCount = 0;
            int gameObjectCount = 0;

            // Loop through all open scenes
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;

                GameObject[] rootObjects = scene.GetRootGameObjects();
                foreach (GameObject root in rootObjects)
                {
                    GameObject[] allObjects = root.GetComponentsInChildren<Transform>(true)
                                                  .Select(t => t.gameObject)
                                                  .ToArray();

                    foreach (GameObject go in allObjects)
                    {
                        gameObjectCount++;
                        int before = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
                        if (before > 0)
                        {
                            Undo.RegisterCompleteObjectUndo(go, "Remove missing scripts");
                            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
                            removedCount += before;
                            EditorUtility.SetDirty(go);
                        }
                    }
                }
            }

            if (removedCount > 0)
            {
                EditorSceneManager.MarkAllScenesDirty();
                Debug.Log($"🗑️ U3D SDK: Removed {removedCount} missing script(s) from {gameObjectCount} GameObject(s) across all open scenes.");
            }
            else
            {
                Debug.Log($"✅ U3D SDK: No missing scripts found in {gameObjectCount} GameObject(s) across all open scenes.");
            }
        }

        /// <summary>
        /// Clean missing scripts from prefabs in a selected folder
        /// </summary>
        public static void CleanPrefabsInFolder()
        {
            string selectedPath = "";

            // Check if we have a selected folder in the project window
            if (Selection.activeObject != null)
            {
                string assetPath = AssetDatabase.GetAssetPath(Selection.activeObject);
                if (AssetDatabase.IsValidFolder(assetPath))
                {
                    selectedPath = assetPath;
                }
            }

            // If no folder selected, prompt user
            if (string.IsNullOrEmpty(selectedPath))
            {
                selectedPath = EditorUtility.OpenFolderPanel("Select folder containing prefabs", "Assets", "");

                if (string.IsNullOrEmpty(selectedPath))
                {
                    Debug.Log("🚫 U3D SDK: Prefab cleanup cancelled - no folder selected.");
                    return;
                }

                // Convert absolute path to relative path
                if (selectedPath.StartsWith(Application.dataPath))
                {
                    selectedPath = "Assets" + selectedPath.Substring(Application.dataPath.Length);
                }
                else
                {
                    Debug.LogError("🚫 U3D SDK: Selected folder must be within the Assets folder.");
                    return;
                }
            }

            CleanPrefabsInFolderInternal(selectedPath);
        }

        private static void CleanPrefabsInFolderInternal(string folderPath)
        {
            int removedComponentsCount = 0;
            int processedPrefabsCount = 0;

            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { folderPath });
            var visitedPrefabs = new HashSet<Object>();

            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);

                EditorUtility.DisplayProgressBar(
                    "U3D SDK: Cleaning Prefabs",
                    $"Processing: {Path.GetFileName(prefabPath)}",
                    (float)i / prefabGuids.Length
                );

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab != null)
                {
                    var result = CleanPrefabRecursively(prefab, visitedPrefabs);
                    removedComponentsCount += result.removedComponents;
                    if (result.wasModified)
                    {
                        processedPrefabsCount++;
                    }
                }
            }

            EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (processedPrefabsCount > 0)
            {
                Debug.Log($"🧹 U3D SDK: Processed {processedPrefabsCount} prefab(s) and removed {removedComponentsCount} missing script component(s) from folder: {folderPath}");
            }
            else
            {
                Debug.Log($"✅ U3D SDK: No missing scripts found in prefabs in folder: {folderPath}");
            }
        }

        private static (int removedComponents, bool wasModified) CleanPrefabRecursively(GameObject prefabRoot, HashSet<Object> visitedPrefabs)
        {
            if (!visitedPrefabs.Add(prefabRoot)) return (0, false);

            int totalRemoved = 0;
            bool wasModified = false;

            var allObjects = prefabRoot.GetComponentsInChildren<Transform>(true)
                .Select(t => t.gameObject);

            foreach (var go in allObjects)
            {
                if (PrefabUtility.IsPartOfAnyPrefab(go))
                {
                    var source = PrefabUtility.GetCorrespondingObjectFromSource(go);
                    if (source != null && visitedPrefabs.Add(source))
                    {
                        var result = CleanGameObjectPrefab(source);
                        totalRemoved += result.removedCount;
                        if (result.wasModified) wasModified = true;
                    }
                }

                var goResult = CleanGameObjectPrefab(go);
                totalRemoved += goResult.removedCount;
                if (goResult.wasModified) wasModified = true;
            }

            return (totalRemoved, wasModified);
        }

        private static (int removedCount, bool wasModified) CleanGameObjectPrefab(GameObject go)
        {
            int missingCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
            if (missingCount > 0)
            {
                Undo.RegisterCompleteObjectUndo(go, "Remove missing scripts from prefab");
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
                EditorUtility.SetDirty(go);
                return (missingCount, true);
            }
            return (0, false);
        }
    }
}