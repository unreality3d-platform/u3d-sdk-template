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
                new CreatorTool("🚧 Make Grabbable Near", "Objects can be picked up when close", () => Debug.Log("Applied Grabbable Near"), true),
                new CreatorTool("🚧 Make Grabbable Far", "Objects can be picked up from distance", () => Debug.Log("Applied Grabbable Far"), true),
                new CreatorTool("🚧 Make Throwable", "Objects can be thrown around", () => Debug.Log("Applied Throwable"), true),
                new CreatorTool("🚧 Make Swimmable", "Create water volumes players can swim through", () => Debug.Log("Applied Swimmable"), true),
                new CreatorTool("🚧 Make Climbable", "Surfaces players can climb on", () => Debug.Log("Applied Climbable"), true),
                new CreatorTool("🚧 Add Seat", "Triggers avatar sit animation players can exit by resuming movement", () => Debug.Log("Applied Seat"), true),
                new CreatorTool("🚧 Make Rideable", "Players can stand on top and will be moved with the object", () => Debug.Log("Applied Rideable"), true),
                new CreatorTool("🚧 Make Steerable", "Lets player controller movement steer the visual object while W and D smoothly accelerate and decelerate (wheel animations can be added manually)", () => Debug.Log("Applied Steerable"), true),
                new CreatorTool("🚧 Make 1x Trigger", "Trigger that fires once", () => Debug.Log("Applied 1x Trigger"), true),
                new CreatorTool("🚧 Make Toggle", "Switch between two states", () => Debug.Log("Applied Toggle"), true),
                new CreatorTool("🚧 Make Random", "Add component with list of GameObjects (audio, particles, etc.) that randomizes between them on trigger or continuously", () => Debug.Log("Applied Random"), true),
                new CreatorTool("🚧 Make Mutually Exclusive", "Only one can be selected at a time", () => Debug.Log("Applied Mutually Exclusive"), true),
                new CreatorTool("🚧 Make Object Destroy Trigger", "Removes objects when triggered", () => Debug.Log("Applied Object Destroy Trigger"), true),
                new CreatorTool("🚧 Make Object Reset Trigger", "Returns objects to starting position", () => Debug.Log("Applied Object Reset Trigger"), true),
                new CreatorTool("🚧 Add Player Reset Trigger", "Reset player position and state to spawn point", () => Debug.Log("Applied Player Reset Trigger"), true)
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
                ProjectToolsTab.DrawCategoryTool(tool);
            }
        }
    }
}