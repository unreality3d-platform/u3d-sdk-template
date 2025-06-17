using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace U3D.Editor
{
    public class InteractionToolsCategory : IToolCategory
    {
        public string CategoryName => "Interactions";
        private List<CreatorTool> tools;

        public InteractionToolsCategory()
        {
            tools = new List<CreatorTool>
            {
                new CreatorTool("Make Grabbable Near", "Objects can be picked up when close", () => Debug.Log("Applied Grabbable Near"), true),
                new CreatorTool("Make Grabbable Far", "Objects can be picked up from distance", () => Debug.Log("Applied Grabbable Far"), true),
                new CreatorTool("Make Throwable", "Objects can be thrown around", () => Debug.Log("Applied Throwable"), true),
                new CreatorTool("Make Swimmable", "Create water volumes players can swim through", () => Debug.Log("Applied Swimmable"), true),
                new CreatorTool("Make Climbable", "Surfaces players can climb on", () => Debug.Log("Applied Climbable"), true),
                new CreatorTool("Make 1x Trigger", "Trigger that fires once", () => Debug.Log("Applied 1x Trigger"), true),
                new CreatorTool("Make Toggle", "Switch between two states", () => Debug.Log("Applied Toggle"), true),
                new CreatorTool("Make Random", "Add component with list of GameObjects (audio, particles, etc.) that randomizes between them on trigger or continuously", () => Debug.Log("Applied Random"), true),
                new CreatorTool("Make Mutually Exclusive", "Only one can be selected at a time", () => Debug.Log("Applied Mutually Exclusive"), true),
                new CreatorTool("Make Object Destroy Trigger", "Removes objects when triggered", () => Debug.Log("Applied Object Destroy Trigger"), true),
                new CreatorTool("Make Object Reset Trigger", "Returns objects to starting position", () => Debug.Log("Applied Object Reset Trigger"), true),
                new CreatorTool("Add Player Reset Trigger", "Reset player position and state to spawn point", () => Debug.Log("Applied Player Reset Trigger"), true)
            };
        }

        public List<CreatorTool> GetTools() => tools;

        public void DrawCategory()
        {
            EditorGUILayout.LabelField("Interaction Tools", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Add interactive behaviors to your objects. Select an object first, then click Apply.", MessageType.Info);
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

            EditorGUILayout.EndHorizontal();

            if (tool.requiresSelection && Selection.activeGameObject == null)
            {
                EditorGUILayout.LabelField("Select an object", EditorStyles.centeredGreyMiniLabel);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }
    }
}