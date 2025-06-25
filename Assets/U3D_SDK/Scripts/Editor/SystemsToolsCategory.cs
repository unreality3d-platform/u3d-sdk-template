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
                // High Impact Core Systems (Ordered by Implementation Priority)
                new CreatorTool("🟢 Add Quest System", "Create missions and objectives for players", () => U3DQuestSystemTools.CreateQuestSystem()),
                new CreatorTool("🚧 Add Inventory System", "Essential for collecting, managing, and using items in exploration games", () => Debug.Log("Applied Inventory System")),
                new CreatorTool("🚧 Add Dialogue System", "Critical for storytelling, NPCs, and guided experiences", () => Debug.Log("Applied Dialogue System")),
                new CreatorTool("🚧 Add Timer System", "Countdown timers, time limits, scheduled events", () => Debug.Log("Applied Timer System")),
                new CreatorTool("🚧 Add State Machine", "Object state management (locked/unlocked, powered/unpowered, etc.)", () => Debug.Log("Applied State Machine")),
                
                // Player Progression Systems
                new CreatorTool("🚧 Add Health/Lives System", "Player progression, challenge mechanics", () => Debug.Log("Applied Health/Lives System")),
                new CreatorTool("🚧 Add Checkpoint System", "Save progress, restart points for complex experiences", () => Debug.Log("Applied Checkpoint System")),
                new CreatorTool("🚧 Add Achievement System", "Unlock rewards, progression tracking", () => Debug.Log("Applied Achievement System")),
                
                // Original Systems (Maintained)
                new CreatorTool("🚧 Add Quiz System", "Interactive questions and knowledge tests", () => Debug.Log("Applied Quiz System")),
                new CreatorTool("🚧 Add Scoreboard Canvas", "Track and display player achievements", () => Debug.Log("Applied Scoreboard Canvas")),
                
                // Social & Community Systems
                new CreatorTool("🚧 Add Social Sharing", "Screenshot/share moments from experiences", () => Debug.Log("Applied Social Sharing")),
                new CreatorTool("🚧 Add Guestbook", "Adds a screen space UI panel with built-in interactivity, instructing visitors to 'Press E to leave '[Your Name] was here!' note.' that gets the user's name and adds the message to the displayed text", () => Debug.Log("Applied Guestbook")),
                
                // UI & Navigation Systems
                new CreatorTool("🚧 Add Worldspace Interaction UI", "3D world canvas for object interactions", () => Debug.Log("Applied Worldspace Interaction UI"), true),
                new CreatorTool("🚧 Add Screenspace Interaction UI", "Screen overlay canvas for user interfaces", () => Debug.Log("Applied Screen Interaction UI")),
                
                // Portal & Navigation Systems
                new CreatorTool("🚧 Add Scene-to-Scene Portal", "Portal to load different scenes", () => Debug.Log("Applied Scene-to-Scene Portal"), true),
                new CreatorTool("🚧 Add 1-Way In-Scene Portal", "Portal for one-direction travel within scene", () => Debug.Log("Applied 1-Way Portal"), true),
                new CreatorTool("🚧 Add 2-Way In-Scene Portal", "Portal for bi-directional travel within scene", () => Debug.Log("Applied 2-Way Portal"), true)
            };
        }

        public List<CreatorTool> GetTools() => tools;

        public void DrawCategory()
        {
            EditorGUILayout.LabelField("Game Systems", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Add complete game systems to enhance player engagement. Systems are organized by impact priority - start with Quest and Inventory for maximum creator value!", MessageType.Info);
            EditorGUILayout.Space(10);

            foreach (var tool in tools)
            {
                ProjectToolsTab.DrawCategoryTool(tool);
            }
        }
    }
}