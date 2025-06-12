using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace U3D.Editor
{
    public class ProjectToolsTab : ICreatorTab
    {
        public string TabName => "Project Tools";
        public bool IsComplete => false;

        private List<IToolCategory> categories;
        private int selectedCategoryIndex = 0;
        private string searchText = "";
        private Vector2 categoryScrollPosition;

        private GUIStyle subtabButtonStyle;
        private GUIStyle activeSubtabButtonStyle;
        private bool stylesInitialized = false;

        public void Initialize()
        {
            InitializeToolCategories();
            InitializeStyles();
        }

        private void InitializeStyles()
        {
            if (stylesInitialized) return;

            // Match ONLY the color settings from your main window
            subtabButtonStyle = new GUIStyle(EditorStyles.miniButton)
            {
                normal = {
                    textColor = Color.white
                },
                hover = {
                    textColor = Color.white
                },
                onNormal = {
                    textColor = Color.white
                }
            };

            activeSubtabButtonStyle = new GUIStyle(EditorStyles.miniButtonMid)
            {
                normal = {
                    textColor = Color.white
                },
                hover = {
                    textColor = Color.white
                },
                onNormal = {
                    textColor = Color.white
                },
                fontStyle = FontStyle.Bold
            };

            stylesInitialized = true;
        }

        private void InitializeToolCategories()
        {
            categories = new List<IToolCategory>
            {
                new InteractionToolsCategory(),
                new SystemsToolsCategory(),
                new MediaToolsCategory(),
                new MigrationToolsCategory(),
                new OptimizationToolsCategory()
            };
        }

        public void DrawTab()
        {
            InitializeStyles();

            EditorGUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Search Tools:", GUILayout.Width(80));
            searchText = EditorGUILayout.TextField(searchText);
            if (GUILayout.Button("Clear", GUILayout.Width(50)))
            {
                searchText = "";
                GUI.FocusControl(null);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();
            for (int i = 0; i < categories.Count; i++)
            {
                // Change from Button to Toggle to match main tabs
                bool isSelected = selectedCategoryIndex == i;
                bool newSelection = GUILayout.Toggle(isSelected, categories[i].CategoryName,
                    isSelected ? activeSubtabButtonStyle : subtabButtonStyle);

                if (newSelection && !isSelected)
                {
                    selectedCategoryIndex = i;
                    searchText = "";
                }

                if (i < categories.Count - 1)
                {
                    GUILayout.Space(1f);
                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(10);

            if (categories != null && selectedCategoryIndex < categories.Count)
            {
                categoryScrollPosition = EditorGUILayout.BeginScrollView(categoryScrollPosition);

                if (string.IsNullOrEmpty(searchText))
                {
                    categories[selectedCategoryIndex].DrawCategory();
                }
                else
                {
                    DrawSearchResults();
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawSearchResults()
        {
            EditorGUILayout.LabelField($"Search Results for: \"{searchText}\"", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            bool foundResults = false;

            foreach (var category in categories)
            {
                var matchingTools = category.GetTools().Where(tool =>
                    tool.title.ToLower().Contains(searchText.ToLower()) ||
                    tool.description.ToLower().Contains(searchText.ToLower())).ToList();

                if (matchingTools.Any())
                {
                    foundResults = true;
                    EditorGUILayout.LabelField($"From {category.CategoryName}:", EditorStyles.boldLabel);

                    foreach (var tool in matchingTools)
                    {
                        DrawTool(tool);
                    }
                    EditorGUILayout.Space(10);
                }
            }

            if (!foundResults)
            {
                EditorGUILayout.HelpBox("No tools found matching your search.", MessageType.Info);
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