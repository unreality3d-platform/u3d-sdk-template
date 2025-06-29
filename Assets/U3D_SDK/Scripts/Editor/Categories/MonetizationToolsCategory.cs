using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace U3D.Editor
{
    public class MonetizationToolsCategory : IToolCategory
    {
        public string CategoryName => "Monetization";
        private List<CreatorTool> tools;

        public MonetizationToolsCategory()
        {
            tools = new List<CreatorTool>
            {
                new CreatorTool("🚧 Add Shop Object", "3D world PayPal shop with multiple items", () => Debug.Log("Applied Shop Object"), true),
                new CreatorTool("🚧 Add Screen Shop", "Screen overlay PayPal shop interface", () => Debug.Log("Applied Screen Shop")),
                new CreatorTool("🚧 Add Purchase Button", "Single item PayPal purchase button", () => Debug.Log("Applied Purchase Button"), true),
                new CreatorTool("🚧 Add Tip Jar", "Accept donations from visitors to support your work", () => Debug.Log("Applied Tip Jar"), true),
                new CreatorTool("🚧 Add Event Gate", "Timed event access with PayPal payment", () => Debug.Log("Applied Event Gate"), true),
                new CreatorTool("🚧 Add Scene Gate", "Scene entry payment gate with PayPal", () => Debug.Log("Applied Scene Gate"), true)
            };
        }

        public List<CreatorTool> GetTools() => tools;

        public void DrawCategory()
        {
            EditorGUILayout.LabelField("Monetization Tools", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Set up payment systems and revenue generation for your content. Your PayPal integration is ready - creators keep 95% of earnings!", MessageType.Info);
            EditorGUILayout.Space(10);

            foreach (var tool in tools)
            {
                ProjectToolsTab.DrawCategoryTool(tool);
            }
        }
    }
}