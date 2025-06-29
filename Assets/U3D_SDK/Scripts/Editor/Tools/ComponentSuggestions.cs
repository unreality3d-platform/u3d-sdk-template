using System.Collections.Generic;
using UnityEngine;

namespace U3D.Editor
{
    /// <summary>
    /// Simple helper for suggesting U3D SDK tool replacements during asset cleanup
    /// </summary>
    public static class ComponentSuggestions
    {
        private static Dictionary<string, string> commonReplacements = new Dictionary<string, string>
        {
            // Interaction Systems
            {"grabbable", "Make Grabbable Near/Far (Interactions)"},
            {"interactable", "Make 1x Trigger or Make Toggle (Interactions)"},
            {"pickup", "Make Grabbable Near (Interactions)"},
            {"trigger", "Make 1x Trigger (Interactions)"},
            {"switch", "Make Toggle (Interactions)"},
            {"door", "Make Toggle (Interactions)"},
            {"collect", "Make Object Destroy Trigger (Interactions)"},
            
            // Core Game Systems
            {"quest", "Add Quest System (Game Systems)"},
            {"mission", "Add Quest System (Game Systems)"},
            {"objective", "Add Quest System (Game Systems)"},
            {"inventory", "Add Inventory System (Game Systems)"},
            {"bag", "Add Inventory System (Game Systems)"},
            {"item", "Add Inventory System (Game Systems)"},
            {"dialogue", "Add Dialogue System (Game Systems)"},
            {"conversation", "Add Dialogue System (Game Systems)"},
            {"npc", "Add Dialogue System (Game Systems)"},
            {"timer", "Add Timer System (Game Systems)"},
            {"countdown", "Add Timer System (Game Systems)"},
            {"clock", "Add Timer System (Game Systems)"},
            
            // Player Progression Systems
            {"health", "Add Health/Lives System (Game Systems)"},
            {"lives", "Add Health/Lives System (Game Systems)"},
            {"damage", "Add Health/Lives System (Game Systems)"},
            {"checkpoint", "Add Checkpoint System (Game Systems)"},
            {"save", "Add Checkpoint System (Game Systems)"},
            {"respawn", "Add Checkpoint System (Game Systems)"},
            {"achievement", "Add Achievement System (Game Systems)"},
            {"unlock", "Add Achievement System (Game Systems)"},
            {"reward", "Add Achievement System (Game Systems)"},
            
            // State & Logic Systems
            {"state", "Add State Machine (Game Systems)"},
            {"power", "Add State Machine (Game Systems)"},
            {"lock", "Add State Machine (Game Systems)"},
            {"active", "Add State Machine (Game Systems)"},
            
            // Social Systems
            {"share", "Add Social Sharing (Game Systems)"},
            {"screenshot", "Add Social Sharing (Game Systems)"},
            {"photo", "Add Social Sharing (Game Systems)"},
            {"guestbook", "Add Guest Book (Game Systems)"},
            {"message", "Add Guest Book (Game Systems)"},
            {"feedback", "Add Guest Book (Game Systems)"},
            
            // Navigation Systems
            {"portal", "Add Scene-to-Scene Portal (Game Systems)"},
            {"teleport", "Add 1-Way Portal (Game Systems)"},
            {"warp", "Add 1-Way Portal (Game Systems)"},
            
            // Original Systems
            {"quiz", "Add Quiz System (Game Systems)"},
            {"question", "Add Quiz System (Game Systems)"},
            {"score", "Add Scoreboard Canvas (Game Systems)"},
            {"scoreboard", "Add Scoreboard Canvas (Game Systems)"},
            
            // Monetization Systems
            {"shop", "Add Shop Object (Monetization)"},
            {"purchase", "Add Purchase Button (Monetization)"}
        };

        public static string GetSuggestionForGameObject(string gameObjectName)
        {
            if (string.IsNullOrEmpty(gameObjectName)) return "";

            string lowerName = gameObjectName.ToLower();

            foreach (var kvp in commonReplacements)
            {
                if (lowerName.Contains(kvp.Key))
                {
                    return kvp.Value;
                }
            }

            return "";
        }
    }
}