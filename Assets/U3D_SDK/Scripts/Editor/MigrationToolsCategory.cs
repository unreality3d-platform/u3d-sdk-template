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
                new CreatorTool("Missing Scripts Replacer", "Replace missing script references on GameObjects in scene with placeholder components to prevent errors while retaining visual reminders of where components go", () => Debug.Log("Applied Missing Scripts Replacer")),
                new CreatorTool("Remove Missing Script Placeholders", "Companion to Missing Scripts Replacer: Remove placeholder entries for missing scripts from GameObjects and components in scene", () => Debug.Log("Applied Remove Missing Script Placeholders")),
                new CreatorTool("Prefab Missing Script Cleaner", "Remove missing script components from prefabs in selected folder to prevent errors", () => Debug.Log("Applied Prefab Missing Script Cleaner")),
                new CreatorTool("Remove Missing Scripts Tool", "Tool for detecting and removing missing script references from GameObjects in loaded scene", () => Debug.Log("Applied Remove Missing Scripts Tool"))
            };
        }

        public List<CreatorTool> GetTools() => tools;

        public void DrawCategory()
        {
            EditorGUILayout.LabelField("Asset Cleanup Tools", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Clean up missing script references and broken components from your project.", MessageType.Info);
            EditorGUILayout.Space(10);

            foreach (var tool in tools)
            {
                DrawTool(tool);
            }
        }

        private void DrawTool(CreatorTool tool)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField(tool.title, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(tool.description, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndVertical();

            if (GUILayout.Button("Apply", GUILayout.Width(80), GUILayout.Height(35)))
            {
                tool.action?.Invoke();
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }
    }
}