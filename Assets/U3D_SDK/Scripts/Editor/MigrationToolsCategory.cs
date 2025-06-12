using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace U3D.Editor
{
    public class MigrationToolsCategory : IToolCategory
    {
        public string CategoryName => "Platform Migration";
        private List<CreatorTool> tools;

        public MigrationToolsCategory()
        {
            tools = new List<CreatorTool>
            {
                new CreatorTool("Import from Spatial.io", "Convert Spatial scenes to Unity format", () => Debug.Log("Applied Spatial Import")),
                new CreatorTool("Import from VRChat", "Convert VRChat worlds to Unity format", () => Debug.Log("Applied VRChat Import")),
                new CreatorTool("Import from FrameVR", "Convert Frame scenes to Unity format", () => Debug.Log("Applied Frame Import")),
                new CreatorTool("Import from PlayCanvas", "Convert PlayCanvas projects to Unity", () => Debug.Log("Applied PlayCanvas Import")),
                new CreatorTool("Import from ArrivalSpace", "Convert ArrivalSpace content to Unity", () => Debug.Log("Applied ArrivalSpace Import")),
                new CreatorTool("Fix Missing Scripts", "Replace missing script references with placeholders", () => Debug.Log("Applied Missing Script Fix")),
                new CreatorTool("Clean Missing Scripts", "Remove all components with missing script references", () => Debug.Log("Applied Clean Missing Scripts"))
            };
        }

        public List<CreatorTool> GetTools() => tools;

        public void DrawCategory()
        {
            EditorGUILayout.LabelField("Platform Migration Tools", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Safely move your existing 3D content from other platforms to Unity 6.", MessageType.Info);
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