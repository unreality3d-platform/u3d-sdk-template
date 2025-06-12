using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace U3D.Editor
{
    public class SystemsToolsCategory : IToolCategory
    {
        public string CategoryName => "Game Systems";
        private List<CreatorTool> tools;

        public SystemsToolsCategory()
        {
            tools = new List<CreatorTool>
            {
                new CreatorTool("Add Quest System", "Create missions and objectives for players", () => Debug.Log("Applied Quest System")),
                new CreatorTool("Add Quiz System", "Interactive questions and knowledge tests", () => Debug.Log("Applied Quiz System")),
                new CreatorTool("Add Scoreboard", "Track and display player achievements", () => Debug.Log("Applied Scoreboard")),
                new CreatorTool("Add Interaction UI", "Worldspace canvas for object interactions", () => Debug.Log("Applied Interaction UI"), true),
                new CreatorTool("Add Scene Portal", "Portal to load different scenes", () => Debug.Log("Applied Scene Portal"), true),
                new CreatorTool("Add 1-Way Portal", "Portal for one-direction travel within scene", () => Debug.Log("Applied 1-Way Portal"), true),
                new CreatorTool("Add 2-Way Portal", "Portal for bi-directional travel within scene", () => Debug.Log("Applied 2-Way Portal"), true)
            };
        }

        public List<CreatorTool> GetTools() => tools;

        public void DrawCategory()
        {
            EditorGUILayout.LabelField("Game Systems", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Add complete game systems to enhance player engagement.", MessageType.Info);
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

            bool canExecute = !tool.requiresSelection || Selection.activeGameObject != null;
            EditorGUI.BeginDisabledGroup(!canExecute);

            if (GUILayout.Button("Apply", GUILayout.Width(80), GUILayout.Height(35)))
            {
                tool.action?.Invoke();
            }

            EditorGUI.EndDisabledGroup();

            if (tool.requiresSelection && Selection.activeGameObject == null)
            {
                EditorGUILayout.LabelField("Select an object", EditorStyles.centeredGreyMiniLabel);
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }
    }
}