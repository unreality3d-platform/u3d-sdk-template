using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using U3D;

namespace U3D.Editor
{
    /// <summary>
    /// Editor tools for creating quest system components with Unity 6+ default setup
    /// UPDATED: Now includes automatic interaction choice setup
    /// </summary>
    public static class U3DQuestSystemTools
    {
        /// <summary>
        /// Create a complete quest system in the scene
        /// </summary>
        public static void CreateQuestSystem()
        {
            // Check if QuestManager already exists
            U3DQuestManager existingManager = Object.FindAnyObjectByType<U3DQuestManager>();
            if (existingManager != null)
            {
                EditorUtility.DisplayDialog("Quest System",
                    "Quest System already exists in the scene!\n\nFound QuestManager on: " + existingManager.gameObject.name,
                    "OK");
                Selection.activeGameObject = existingManager.gameObject;
                return;
            }

            // Create QuestManager GameObject
            GameObject questManagerObj = new GameObject("QuestManager");
            U3DQuestManager questManager = questManagerObj.AddComponent<U3DQuestManager>();

            // Add AudioSource for quest sounds
            AudioSource audioSource = questManagerObj.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;

            // Create Quest Log UI Canvas
            GameObject questLogObj = CreateQuestLogUI();

            // Connect QuestManager to UI
            Canvas questLogCanvas = questLogObj.GetComponent<Canvas>();

            // Use reflection or SerializedObject to set the private field
            SerializedObject serializedManager = new SerializedObject(questManager);
            SerializedProperty canvasProperty = serializedManager.FindProperty("questLogCanvas");
            SerializedProperty listParentProperty = serializedManager.FindProperty("questListParent");

            if (canvasProperty != null)
                canvasProperty.objectReferenceValue = questLogCanvas;

            Transform scrollContent = questLogObj.transform.Find("QuestLog/Scroll View/Viewport/Content");
            if (listParentProperty != null && scrollContent != null)
                listParentProperty.objectReferenceValue = scrollContent;

            serializedManager.ApplyModifiedProperties();

            // Position objects nicely
            questManagerObj.transform.position = Vector3.zero;
            questLogObj.transform.position = Vector3.zero;

            // Select the QuestManager for immediate configuration
            Selection.activeGameObject = questManagerObj;
            EditorGUIUtility.PingObject(questManagerObj);

            Debug.Log("✅ Quest System created successfully! Configure quests in the QuestManager component.");
        }

        /// <summary>
        /// Create a quest log UI using Unity's internal DefaultControls.CreateScrollView method
        /// </summary>
        private static GameObject CreateQuestLogUI()
        {
            // Create main canvas
            GameObject canvasObj = new GameObject("QuestLogCanvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            canvasObj.AddComponent<GraphicRaycaster>();

            // Create quest log panel
            GameObject questLogPanel = new GameObject("QuestLog");
            questLogPanel.transform.SetParent(canvasObj.transform, false);

            Image panelImage = questLogPanel.AddComponent<Image>();
            panelImage.color = new Color(0, 0, 0, 0.8f);

            RectTransform panelRect = questLogPanel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.02f, 0.02f);
            panelRect.anchorMax = new Vector2(0.35f, 0.6f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            // Create title using TextMeshPro with MUCH larger font
            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(questLogPanel.transform, false);

            TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = "QUESTS";
            titleText.fontSize = 48; // INCREASED from 24 
            titleText.fontStyle = FontStyles.Bold;
            titleText.color = Color.white;
            titleText.alignment = TextAlignmentOptions.Center;

            RectTransform titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 0.9f);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;

            // Create ScrollView using Unity's internal method (same as GameObject > UI > Scroll View)
            DefaultControls.Resources uiResources = new DefaultControls.Resources();
            GameObject scrollViewObj = DefaultControls.CreateScrollView(uiResources);

            // Parent the ScrollView to QuestLog panel 
            scrollViewObj.transform.SetParent(questLogPanel.transform, false);

            // Position the scroll view within the quest log panel
            RectTransform scrollRect = scrollViewObj.GetComponent<RectTransform>();
            scrollRect.anchorMin = new Vector2(0.05f, 0.05f);
            scrollRect.anchorMax = new Vector2(0.95f, 0.85f);
            scrollRect.offsetMin = Vector2.zero;
            scrollRect.offsetMax = Vector2.zero;

            // Canvas is always enabled when QuestManager exists
            canvasObj.SetActive(true);

            return canvasObj;
        }

        /// <summary>
        /// Create a basic quest with one objective
        /// </summary>
        public static void CreateBasicQuest()
        {
            GameObject questObj = new GameObject("New Quest");

            // Add Quest component
            U3DQuest quest = questObj.AddComponent<U3DQuest>();
            quest.questTitle = "Complete the Task";
            quest.questDescription = "This is a sample quest. Configure the objectives below.";

            // Create objective child object
            GameObject objectiveObj = new GameObject("Objective 1");
            objectiveObj.transform.SetParent(questObj.transform);

            U3DQuestObjective objective = objectiveObj.AddComponent<U3DQuestObjective>();
            objective.objectiveDescription = "Complete this objective";

            // Refresh objectives in quest
            quest.RefreshObjectives();

            // Position nicely in scene
            if (SceneView.lastActiveSceneView != null)
            {
                questObj.transform.position = SceneView.lastActiveSceneView.pivot;
            }

            Selection.activeGameObject = questObj;
            EditorGUIUtility.PingObject(questObj);

            Debug.Log("✅ Basic quest created! Configure the quest details in the Inspector.");
        }

        /// <summary>
        /// Create a quest giver NPC with automatic setup - UPDATED for choice system
        /// </summary>
        public static void CreateQuestGiver()
        {
            GameObject giverObj = new GameObject("Quest Giver");

            // Add visual representation (cube)
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Visual";
            visual.transform.SetParent(giverObj.transform);
            visual.transform.localPosition = Vector3.zero;

            // Color it differently
            Renderer renderer = visual.GetComponent<Renderer>();
            Material material = new Material(Shader.Find("Standard"));
            material.color = Color.yellow;
            renderer.material = material;

            // Add quest giver component
            U3DQuestGiver questGiver = giverObj.AddComponent<U3DQuestGiver>();
            questGiver.giverName = "Quest Giver";

            // AUTOMATIC SETUP: Default interaction choice system
            // Uses SerializedObject to set up default "Accept [E]" choice
            SerializedObject serializedGiver = new SerializedObject(questGiver);
            SerializedProperty choicesProperty = serializedGiver.FindProperty("interactionChoices");

            // Clear any existing choices and add default
            choicesProperty.ClearArray();
            choicesProperty.InsertArrayElementAtIndex(0);

            SerializedProperty firstChoice = choicesProperty.GetArrayElementAtIndex(0);
            firstChoice.FindPropertyRelative("choiceLabel").stringValue = "Accept";
            firstChoice.FindPropertyRelative("choiceKey").enumValueIndex = (int)KeyCode.E;
            firstChoice.FindPropertyRelative("choiceID").stringValue = "accept";

            serializedGiver.ApplyModifiedProperties();

            // Add interaction collider
            SphereCollider interactionCollider = giverObj.AddComponent<SphereCollider>();
            interactionCollider.isTrigger = true;
            interactionCollider.radius = 3f;

            // Position nicely in scene
            if (SceneView.lastActiveSceneView != null)
            {
                giverObj.transform.position = SceneView.lastActiveSceneView.pivot;
            }

            Selection.activeGameObject = giverObj;
            EditorGUIUtility.PingObject(giverObj);

            Debug.Log("✅ Quest Giver created with default 'Accept [E]' interaction! Assign a quest and customize choices in the Inspector.");
        }

        /// <summary>
        /// Create a quest trigger for collectibles or interactions
        /// </summary>
        public static void CreateQuestTrigger()
        {
            GameObject[] selectedObjects = Selection.gameObjects;

            if (selectedObjects.Length == 0)
            {
                // Create new trigger object
                GameObject triggerObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                triggerObj.name = "Quest Trigger";

                // Configure as trigger
                Collider collider = triggerObj.GetComponent<Collider>();
                collider.isTrigger = true;

                // Make it semi-transparent
                Renderer renderer = triggerObj.GetComponent<Renderer>();
                Material material = new Material(Shader.Find("Standard"));
                material.color = new Color(0, 1, 0, 0.3f);
                material.SetFloat("_Mode", 3); // Transparent mode
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                material.DisableKeyword("_ALPHATEST_ON");
                material.EnableKeyword("_ALPHABLEND_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.renderQueue = 3000;
                renderer.material = material;

                // Add quest trigger component
                U3DQuestTrigger questTrigger = triggerObj.AddComponent<U3DQuestTrigger>();

                // Position nicely
                if (SceneView.lastActiveSceneView != null)
                {
                    triggerObj.transform.position = SceneView.lastActiveSceneView.pivot;
                }

                Selection.activeGameObject = triggerObj;
                Debug.Log("✅ Quest Trigger created! Connect it to a Quest Objective in the Inspector.");
            }
            else
            {
                // Add trigger components to selected objects
                foreach (GameObject obj in selectedObjects)
                {
                    if (obj.GetComponent<U3DQuestTrigger>() == null)
                    {
                        // Ensure object has a collider
                        Collider collider = obj.GetComponent<Collider>();
                        if (collider == null)
                        {
                            collider = obj.AddComponent<BoxCollider>();
                        }
                        collider.isTrigger = true;

                        // Add quest trigger component
                        U3DQuestTrigger questTrigger = obj.AddComponent<U3DQuestTrigger>();

                        Undo.RegisterCreatedObjectUndo(questTrigger, "Add Quest Trigger");
                    }
                }

                Debug.Log($"✅ Added Quest Trigger components to {selectedObjects.Length} object(s). Connect them to Quest Objectives in the Inspector.");
            }
        }

        /// <summary>
        /// Create a quest giver with Accept/Decline choices - Quick setup option
        /// </summary>
        public static void CreateQuestGiverWithChoices()
        {
            GameObject giverObj = new GameObject("Quest Giver (Multiple Choice)");

            // Add visual representation (cube)
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Visual";
            visual.transform.SetParent(giverObj.transform);
            visual.transform.localPosition = Vector3.zero;

            // Color it differently
            Renderer renderer = visual.GetComponent<Renderer>();
            Material material = new Material(Shader.Find("Standard"));
            material.color = Color.cyan;
            renderer.material = material;

            // Add quest giver component
            U3DQuestGiver questGiver = giverObj.AddComponent<U3DQuestGiver>();
            questGiver.giverName = "Quest Giver";

            // Set up Accept/Decline choices
            SerializedObject serializedGiver = new SerializedObject(questGiver);
            SerializedProperty choicesProperty = serializedGiver.FindProperty("interactionChoices");

            choicesProperty.ClearArray();

            // Add Accept choice
            choicesProperty.InsertArrayElementAtIndex(0);
            SerializedProperty acceptChoice = choicesProperty.GetArrayElementAtIndex(0);
            acceptChoice.FindPropertyRelative("choiceLabel").stringValue = "Accept";
            acceptChoice.FindPropertyRelative("choiceKey").enumValueIndex = (int)KeyCode.E;
            acceptChoice.FindPropertyRelative("choiceID").stringValue = "accept";

            // Add Decline choice
            choicesProperty.InsertArrayElementAtIndex(1);
            SerializedProperty declineChoice = choicesProperty.GetArrayElementAtIndex(1);
            declineChoice.FindPropertyRelative("choiceLabel").stringValue = "Decline";
            declineChoice.FindPropertyRelative("choiceKey").enumValueIndex = (int)KeyCode.X;
            declineChoice.FindPropertyRelative("choiceID").stringValue = "decline";

            serializedGiver.ApplyModifiedProperties();

            // Add interaction collider
            SphereCollider interactionCollider = giverObj.AddComponent<SphereCollider>();
            interactionCollider.isTrigger = true;
            interactionCollider.radius = 3f;

            // Position nicely in scene
            if (SceneView.lastActiveSceneView != null)
            {
                giverObj.transform.position = SceneView.lastActiveSceneView.pivot;
            }

            Selection.activeGameObject = giverObj;
            EditorGUIUtility.PingObject(giverObj);

            Debug.Log("✅ Quest Giver created with Accept/Decline choices! Assign a quest in the Inspector.");
        }

        /// <summary>
        /// Create a quiz-style quest giver with numbered choices - Quick setup option
        /// </summary>
        public static void CreateQuizQuestGiver()
        {
            GameObject giverObj = new GameObject("Quiz Quest Giver");

            // Add visual representation (cube)
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Visual";
            visual.transform.SetParent(giverObj.transform);
            visual.transform.localPosition = Vector3.zero;

            // Color it differently
            Renderer renderer = visual.GetComponent<Renderer>();
            Material material = new Material(Shader.Find("Standard"));
            material.color = Color.magenta;
            renderer.material = material;

            // Add quest giver component
            U3DQuestGiver questGiver = giverObj.AddComponent<U3DQuestGiver>();
            questGiver.giverName = "Quiz Master";

            // Set up quiz-style numbered choices
            SerializedObject serializedGiver = new SerializedObject(questGiver);
            SerializedProperty choicesProperty = serializedGiver.FindProperty("interactionChoices");

            choicesProperty.ClearArray();

            string[] labels = { "Option A", "Option B", "Option C" };
            KeyCode[] keys = { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3 };

            for (int i = 0; i < 3; i++)
            {
                choicesProperty.InsertArrayElementAtIndex(i);
                SerializedProperty choice = choicesProperty.GetArrayElementAtIndex(i);
                choice.FindPropertyRelative("choiceLabel").stringValue = labels[i];
                choice.FindPropertyRelative("choiceKey").enumValueIndex = (int)keys[i];
                choice.FindPropertyRelative("choiceID").stringValue = $"option_{i + 1}";
            }

            serializedGiver.ApplyModifiedProperties();

            // Add interaction collider
            SphereCollider interactionCollider = giverObj.AddComponent<SphereCollider>();
            interactionCollider.isTrigger = true;
            interactionCollider.radius = 3f;

            // Position nicely in scene
            if (SceneView.lastActiveSceneView != null)
            {
                giverObj.transform.position = SceneView.lastActiveSceneView.pivot;
            }

            Selection.activeGameObject = giverObj;
            EditorGUIUtility.PingObject(giverObj);

            Debug.Log("✅ Quiz Quest Giver created with 3 numbered options! Perfect for quiz-style interactions.");
        }
    }
}