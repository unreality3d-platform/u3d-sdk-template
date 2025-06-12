using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace U3D.Editor
{
    public class OptimizationToolsCategory : IToolCategory
    {
        public string CategoryName => "Optimization";
        private List<CreatorTool> tools;

        public OptimizationToolsCategory()
        {
            tools = new List<CreatorTool>
            {
                new CreatorTool("Optimize All Textures", "Batch optimize textures by type with compression settings", () => Debug.Log("Applied Texture Optimization")),
                new CreatorTool("Optimize All Audio", "Batch optimize audio files by usage type", () => Debug.Log("Applied Audio Optimization")),
                new CreatorTool("Find Duplicate Materials", "Locate and merge duplicate materials in project", () => Debug.Log("Applied Material Deduplication")),
                new CreatorTool("Extract Model Materials", "Extract materials from imported models for editing", () => Debug.Log("Applied Material Extraction")),
                new CreatorTool("Analyze Build Size", "Generate detailed build size report", () => Debug.Log("Applied Build Analysis")),
                new CreatorTool("Clean Unused Assets", "Remove unreferenced assets from project", () => Debug.Log("Applied Asset Cleanup")),
                new CreatorTool("Optimize Lighting", "Configure optimal light settings for WebGL", () => Debug.Log("Applied Lighting Optimization"))
            };
        }

        public List<CreatorTool> GetTools() => tools;

        public void DrawCategory()
        {
            EditorGUILayout.LabelField("Optimization Tools", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Optimize your project for better performance and smaller builds.", MessageType.Info);
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