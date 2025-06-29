using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace U3D.Editor
{
    public class MigrationToolsCategory : IToolCategory
    {
        public string CategoryName => "Asset Cleanup";
        private List<CreatorTool> tools;

        public MigrationToolsCategory()
        {
            tools = new List<CreatorTool>
            {
                new CreatorTool("🟢 Replace Missing Scripts", "Replace missing script references with placeholder components to prevent errors while retaining visual reminders", AssetCleanupTools.ReplaceMissingScriptsWithPlaceholders),
                new CreatorTool("🟢 Remove Placeholder Components", "Remove placeholder components added by the Replace Missing Scripts tool", AssetCleanupTools.RemovePlaceholderComponents),
                new CreatorTool("🟢 Clean Missing Scripts from Scene", "Remove missing script components directly from all GameObjects in loaded scenes", AssetCleanupTools.RemoveMissingScriptsFromScene),
                new CreatorTool("🟢 Clean Missing Scripts from Prefabs", "Remove missing script components from prefabs in selected folder", AssetCleanupTools.CleanPrefabsInFolder)
            };
        }

        public List<CreatorTool> GetTools() => tools;

        public void DrawCategory()
        {
            EditorGUILayout.LabelField("Asset Cleanup Tools", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Clean up missing script references and broken components from your project. These tools help maintain a healthy codebase when migrating or updating assets.", MessageType.Info);
            EditorGUILayout.Space(10);

            // Add workflow guidance
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("🔄 Recommended Workflow:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("1. Replace Missing Scripts → Creates placeholders for safety", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("2. Fix/restore your scripts as needed", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("3. Remove Placeholder Components → Clean up when done", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(10);

            foreach (var tool in tools)
            {
                DrawTool(tool);
            }
        }

        private void DrawTool(CreatorTool tool)
        {
            // Use responsive drawing but keep confirmation
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            float windowWidth = EditorGUIUtility.currentViewWidth;

            if (windowWidth < 400f)
            {
                // Vertical layout
                EditorGUILayout.LabelField(tool.title, EditorStyles.boldLabel);
                EditorGUILayout.LabelField(tool.description, EditorStyles.wordWrappedMiniLabel);

                if (GUILayout.Button("Apply", GUILayout.Height(35)))
                {
                    if (EditorUtility.DisplayDialog("Confirm Asset Cleanup",
                        $"This will run: {tool.title}\n\n{tool.description}\n\nThis action can be undone with Ctrl+Z.",
                        "Continue", "Cancel"))
                    {
                        tool.action?.Invoke();
                    }
                }
            }
            else
            {
                // Horizontal layout
                EditorGUILayout.BeginHorizontal();

                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField(tool.title, EditorStyles.boldLabel);
                EditorGUILayout.LabelField(tool.description, EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.EndVertical();

                if (GUILayout.Button("Apply", GUILayout.Width(80), GUILayout.Height(35)))
                {
                    if (EditorUtility.DisplayDialog("Confirm Asset Cleanup",
                        $"This will run: {tool.title}\n\n{tool.description}\n\nThis action can be undone with Ctrl+Z.",
                        "Continue", "Cancel"))
                    {
                        tool.action?.Invoke();
                    }
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }
    }
}