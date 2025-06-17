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
            {"grabbable", "Make Grabbable Near/Far (Interactions)"},
            {"interactable", "Make 1x Trigger or Make Toggle (Interactions)"},
            {"pickup", "Make Grabbable Near (Interactions)"},
            {"trigger", "Make 1x Trigger (Interactions)"},
            {"switch", "Make Toggle (Interactions)"},
            {"door", "Make Toggle (Interactions)"},
            {"collect", "Make Object Destroy Trigger (Interactions)"},
            {"quest", "Add Quest System (Game Systems)"},
            {"portal", "Add Scene-to-Scene Portal (Game Systems)"},
            {"teleport", "Add 1-Way Portal (Game Systems)"},
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