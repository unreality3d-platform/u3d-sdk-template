using U3D;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace U3D.Editor
{
    /// <summary>
    /// Complete U3D context menu system for right-click hierarchy creation
    /// Organized by functional categories for easy expansion
    /// </summary>
    public static class U3DContextMenu
    {
        // Menu priority constants for organization
        private const int CORE_PRIORITY = 0;
        private const int QUEST_PRIORITY = 100;
        private const int INTERACTION_PRIORITY = 200;
        private const int MEDIA_PRIORITY = 300;
        private const int MONETIZATION_PRIORITY = 400;

        // ========================================
        // CORE SYSTEMS
        // ========================================

        [MenuItem("GameObject/U3D/Core Systems/Player Controller", false, CORE_PRIORITY)]
        public static void CreatePlayerController()
        {
            GameObject playerObj = new GameObject("U3D Player");
            playerObj.AddComponent<U3DPlayerController>();

            // Add basic setup
            CharacterController controller = playerObj.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.5f;
            controller.center = new Vector3(0, 1f, 0);

            // Position in scene
            PositionInScene(playerObj);

            Debug.Log("✅ U3D Player Controller created! Configure input and movement settings in the Inspector.");
        }

        // ========================================
        // QUEST SYSTEM
        // ========================================

        [MenuItem("GameObject/U3D/Quest System/U3D Quest", false, QUEST_PRIORITY)]
        public static void CreateQuest()
        {
            U3DQuestSystemTools.CreateBasicQuest();
        }

        [MenuItem("GameObject/U3D/Quest System/U3D Quest Giver", false, QUEST_PRIORITY + 1)]
        public static void CreateQuestGiver()
        {
            U3DQuestSystemTools.CreateQuestGiver();
        }

        [MenuItem("GameObject/U3D/Quest System/U3D Quest Objective", false, QUEST_PRIORITY + 2)]
        public static void CreateQuestObjective()
        {
            GameObject objectiveObj = new GameObject("Quest Objective");
            U3DQuestObjective objective = objectiveObj.AddComponent<U3DQuestObjective>();
            objective.objectiveDescription = "Complete this objective";

            // If a quest is selected, make this objective a child
            if (Selection.activeGameObject != null && Selection.activeGameObject.GetComponent<U3DQuest>() != null)
            {
                objectiveObj.transform.SetParent(Selection.activeGameObject.transform);

                // Refresh the quest's objectives
                U3DQuest parentQuest = Selection.activeGameObject.GetComponent<U3DQuest>();
                parentQuest.RefreshObjectives();

                Debug.Log("✅ Quest Objective added to selected quest!");
            }
            else
            {
                PositionInScene(objectiveObj);
                Debug.Log("✅ Quest Objective created! Attach it to a Quest or configure it independently.");
            }

            Selection.activeGameObject = objectiveObj;
            EditorGUIUtility.PingObject(objectiveObj);
        }

        [MenuItem("GameObject/U3D/Quest System/U3D Quest Trigger", false, QUEST_PRIORITY + 3)]
        public static void CreateQuestTrigger()
        {
            if (Selection.activeGameObject != null)
            {
                // Add trigger to selected object
                GameObject selectedObj = Selection.activeGameObject;

                if (selectedObj.GetComponent<U3DQuestTrigger>() == null)
                {
                    // Ensure object has a collider
                    Collider collider = selectedObj.GetComponent<Collider>();
                    if (collider == null)
                    {
                        collider = selectedObj.AddComponent<BoxCollider>();
                    }
                    collider.isTrigger = true;

                    // Add quest trigger component
                    U3DQuestTrigger questTrigger = selectedObj.AddComponent<U3DQuestTrigger>();

                    Undo.RegisterCreatedObjectUndo(questTrigger, "Add Quest Trigger");

                    Debug.Log($"✅ Quest Trigger added to {selectedObj.name}! Connect it to a Quest Objective in the Inspector.");
                }
                else
                {
                    Debug.Log($"Quest Trigger already exists on {selectedObj.name}");
                }
            }
            else
            {
                // Create new trigger object
                GameObject triggerObj = CreateTriggerObject("Quest Trigger", Color.yellow);
                triggerObj.AddComponent<U3DQuestTrigger>();

                PositionInScene(triggerObj);
                Selection.activeGameObject = triggerObj;
                EditorGUIUtility.PingObject(triggerObj);

                Debug.Log("✅ Quest Trigger object created! Connect it to a Quest Objective in the Inspector.");
            }
        }

        // ========================================
        // INTERACTION SYSTEMS
        // ========================================

        [MenuItem("GameObject/U3D/Interactions/Collectible Object", false, INTERACTION_PRIORITY)]
        public static void CreateCollectible()
        {
            GameObject collectibleObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            collectibleObj.name = "U3D Collectible";

            // Make it visually distinct
            Renderer renderer = collectibleObj.GetComponent<Renderer>();
            Material material = CreateMaterial(Color.cyan);
            renderer.material = material;

            // Add trigger collider
            SphereCollider collider = collectibleObj.GetComponent<SphereCollider>();
            collider.isTrigger = true;

            // Add physics for potential movement
            Rigidbody rb = collectibleObj.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = true;

            PositionInScene(collectibleObj);
            Selection.activeGameObject = collectibleObj;

            Debug.Log("✅ Collectible Object created! Add interaction scripts or Quest Triggers as needed.");
        }

        [MenuItem("GameObject/U3D/Interactions/Interaction Trigger", false, INTERACTION_PRIORITY + 1)]
        public static void CreateInteractionTrigger()
        {
            GameObject triggerObj = CreateTriggerObject("Interaction Trigger", Color.green);

            PositionInScene(triggerObj);
            Selection.activeGameObject = triggerObj;

            Debug.Log("✅ Interaction Trigger created! Add your interaction logic in the Inspector.");
        }

        [MenuItem("GameObject/U3D/Interactions/Portal Trigger", false, INTERACTION_PRIORITY + 2)]
        public static void CreatePortalTrigger()
        {
            GameObject portalObj = CreateTriggerObject("Portal Trigger", Color.magenta);

            // Make it portal-shaped
            portalObj.transform.localScale = new Vector3(2f, 3f, 0.2f);

            PositionInScene(portalObj);
            Selection.activeGameObject = portalObj;

            Debug.Log("✅ Portal Trigger created! Configure destination and transition settings.");
        }

        // ========================================
        // MEDIA & CONTENT
        // ========================================

        [MenuItem("GameObject/U3D/Media/Video Player Object", false, MEDIA_PRIORITY)]
        public static void CreateVideoPlayer()
        {
            GameObject videoObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
            videoObj.name = "U3D Video Player";

            // Add video player component
            UnityEngine.Video.VideoPlayer videoPlayer = videoObj.AddComponent<UnityEngine.Video.VideoPlayer>();
            videoPlayer.playOnAwake = false;
            videoPlayer.renderMode = UnityEngine.Video.VideoRenderMode.MaterialOverride;

            PositionInScene(videoObj);
            Selection.activeGameObject = videoObj;

            Debug.Log("✅ Video Player Object created! Configure video source and settings in the Inspector.");
        }

        [MenuItem("GameObject/U3D/Media/Audio Trigger", false, MEDIA_PRIORITY + 1)]
        public static void CreateAudioTrigger()
        {
            GameObject audioObj = CreateTriggerObject("Audio Trigger", Color.blue);

            // Add audio source
            AudioSource audioSource = audioObj.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f; // 3D sound

            PositionInScene(audioObj);
            Selection.activeGameObject = audioObj;

            Debug.Log("✅ Audio Trigger created! Configure audio clip and trigger settings in the Inspector.");
        }

        [MenuItem("GameObject/U3D/Media/Text Display", false, MEDIA_PRIORITY + 2)]
        public static void CreateTextDisplay()
        {
            // Create canvas for world space text
            GameObject canvasObj = new GameObject("U3D Text Display");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;

            canvasObj.AddComponent<GraphicRaycaster>();

            // Scale down for world space
            canvasObj.transform.localScale = Vector3.one * 0.01f;

            // Create text
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(canvasObj.transform, false);

            UnityEngine.UI.Text text = textObj.AddComponent<UnityEngine.UI.Text>();
            text.text = "Dynamic Text Display";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 48;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;

            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.sizeDelta = new Vector2(400, 100);

            PositionInScene(canvasObj);
            Selection.activeGameObject = canvasObj;

            Debug.Log("✅ Text Display created! Configure text content and styling in the Inspector.");
        }

        // ========================================
        // MONETIZATION
        // ========================================

        [MenuItem("GameObject/U3D/Monetization/Shop Object", false, MONETIZATION_PRIORITY)]
        public static void CreateShopObject()
        {
            GameObject shopObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shopObj.name = "U3D Shop Object";

            // Make it look like a shop
            shopObj.transform.localScale = new Vector3(2f, 2.5f, 1f);

            Renderer renderer = shopObj.GetComponent<Renderer>();
            Material material = CreateMaterial(new Color(1f, 0.8f, 0f)); // Gold color
            renderer.material = material;

            // Add interaction collider
            BoxCollider collider = shopObj.GetComponent<BoxCollider>();
            collider.isTrigger = true;

            PositionInScene(shopObj);
            Selection.activeGameObject = shopObj;

            Debug.Log("✅ Shop Object created! Configure PayPal integration and items in the Inspector.");
        }

        [MenuItem("GameObject/U3D/Monetization/Purchase Button", false, MONETIZATION_PRIORITY + 1)]
        public static void CreatePurchaseButton()
        {
            GameObject buttonObj = CreateTriggerObject("Purchase Button", new Color(0f, 0.8f, 0f));
            buttonObj.transform.localScale = new Vector3(1.5f, 0.5f, 0.2f);

            PositionInScene(buttonObj);
            Selection.activeGameObject = buttonObj;

            Debug.Log("✅ Purchase Button created! Configure PayPal payment settings in the Inspector.");
        }

        // ========================================
        // UTILITY METHODS
        // ========================================

        private static void PositionInScene(GameObject obj)
        {
            if (SceneView.lastActiveSceneView != null)
            {
                obj.transform.position = SceneView.lastActiveSceneView.pivot;
            }
            else
            {
                obj.transform.position = Vector3.zero;
            }
        }

        private static GameObject CreateTriggerObject(string name, Color color)
        {
            GameObject triggerObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            triggerObj.name = name;

            // Make it a trigger
            Collider collider = triggerObj.GetComponent<Collider>();
            collider.isTrigger = true;

            // Make it visually distinct with semi-transparent material
            Renderer renderer = triggerObj.GetComponent<Renderer>();
            Material material = CreateTransparentMaterial(color);
            renderer.material = material;

            return triggerObj;
        }

        private static Material CreateMaterial(Color color)
        {
            Material material = new Material(Shader.Find("Standard"));
            material.color = color;
            return material;
        }

        private static Material CreateTransparentMaterial(Color color)
        {
            Material material = new Material(Shader.Find("Standard"));
            color.a = 0.5f; // Semi-transparent
            material.color = color;
            material.SetFloat("_Mode", 3); // Transparent mode
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = 3000;
            return material;
        }

        // Validation methods
        [MenuItem("GameObject/U3D/Quest System/U3D Quest Trigger", true)]
        public static bool ValidateCreateQuestTrigger()
        {
            return true; // Always allow creation
        }
    }
}