using UnityEngine;
using UnityEngine.UI;

namespace U3D
{
    /// <summary>
    /// NPC or object that gives quests to players.
    /// Uses Unity's built-in Canvas and UI components for familiar creator workflow.
    /// </summary>
    [AddComponentMenu("U3D/Quest System/U3D Quest Giver")]
    public class U3DQuestGiver : MonoBehaviour
    {
        [Header("Quest Giver Configuration")]
        [Tooltip("Name of this quest giver (shown in dialog)")]
        public string giverName = "Quest Giver";

        [Tooltip("The quest this NPC will give to players")]
        [SerializeField] private U3DQuest questToGive;

        [Tooltip("Can this quest giver give the same quest multiple times?")]
        [SerializeField] private bool allowRepeatQuests = false;

        [Header("Interaction Settings")]
        [Tooltip("How close the player needs to be to interact (0 = no distance limit)")]
        [SerializeField] private float interactionRange = 3f;

        [Tooltip("Key players press to interact (leave empty to use click/touch)")]
        [SerializeField] private KeyCode interactionKey = KeyCode.E;

        [Tooltip("Should interaction happen automatically when player gets close?")]
        [SerializeField] private bool autoInteract = false;

        [Header("UI Dialog")]
        [Tooltip("Canvas for quest dialog UI. Will create default if not assigned")]
        [SerializeField] private Canvas dialogCanvas;

        [Tooltip("Text component for quest giver's name")]
        [SerializeField] private Text giverNameText;

        [Tooltip("Text component for quest description")]
        [SerializeField] private Text questDescriptionText;

        [Tooltip("Button to accept the quest")]
        [SerializeField] private Button acceptQuestButton;

        [Tooltip("Button to decline/close dialog")]
        [SerializeField] private Button declineButton;

        [Header("Visual Indicators")]
        [Tooltip("GameObject shown when quest is available (like an exclamation mark)")]
        [SerializeField] private GameObject questAvailableIndicator;

        [Tooltip("GameObject shown when player is in interaction range")]
        [SerializeField] private GameObject interactionPrompt;

        [Tooltip("GameObject shown when quest has been completed")]
        [SerializeField] private GameObject questCompletedIndicator;

        [Header("Dialog Text")]
        [Tooltip("What the quest giver says when offering the quest")]
        [TextArea(2, 4)]
        [SerializeField] private string questOfferText = "I have a task for you. Are you interested?";

        [Tooltip("What the quest giver says if quest is already active")]
        [TextArea(2, 4)]
        [SerializeField] private string questActiveText = "You're already working on my quest. Good luck!";

        [Tooltip("What the quest giver says if quest is completed")]
        [TextArea(2, 4)]
        [SerializeField] private string questCompletedText = "Thank you for completing my quest!";

        private Transform playerTransform;
        private bool playerInRange = false;
        private bool questGiven = false;

        private void Start()
        {
            // Find player
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                playerTransform = player.transform;

            // Set up UI
            SetupUI();

            // Hide dialog initially
            if (dialogCanvas != null)
                dialogCanvas.gameObject.SetActive(false);

            UpdateVisualState();
        }

        private void Update()
        {
            CheckPlayerProximity();
            HandleInteraction();
        }

        /// <summary>
        /// Check if player is within interaction range
        /// </summary>
        private void CheckPlayerProximity()
        {
            if (playerTransform == null || interactionRange <= 0) return;

            float distance = Vector3.Distance(transform.position, playerTransform.position);
            bool wasInRange = playerInRange;
            playerInRange = distance <= interactionRange;

            // Show/hide interaction prompt based on proximity
            if (playerInRange != wasInRange)
            {
                if (interactionPrompt != null)
                    interactionPrompt.SetActive(playerInRange && CanGiveQuest());

                // Auto-interact if enabled
                if (playerInRange && autoInteract && CanGiveQuest())
                {
                    StartInteraction();
                }
            }
        }

        /// <summary>
        /// Handle player input for interaction
        /// </summary>
        private void HandleInteraction()
        {
            if (!playerInRange || !CanGiveQuest()) return;

            // Check for interaction input
            bool interactPressed = false;

            if (interactionKey != KeyCode.None)
                interactPressed = Input.GetKeyDown(interactionKey);
            else
                interactPressed = Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began);

            if (interactPressed)
            {
                StartInteraction();
            }
        }

        /// <summary>
        /// Start interaction with the quest giver
        /// </summary>
        public void StartInteraction()
        {
            if (questToGive == null) return;

            string dialogText = GetAppropriateDialogText();
            ShowDialog(dialogText);
        }

        /// <summary>
        /// Get appropriate dialog text based on quest state
        /// </summary>
        private string GetAppropriateDialogText()
        {
            if (questToGive.IsCompleted)
                return questCompletedText;
            else if (questToGive.IsActive)
                return questActiveText;
            else
                return questOfferText;
        }

        /// <summary>
        /// Show the quest dialog UI
        /// </summary>
        private void ShowDialog(string dialogText)
        {
            if (dialogCanvas != null)
            {
                dialogCanvas.gameObject.SetActive(true);

                if (giverNameText != null)
                    giverNameText.text = giverName;

                if (questDescriptionText != null)
                    questDescriptionText.text = dialogText;

                // Configure buttons based on quest state
                ConfigureDialogButtons();
            }
        }

        /// <summary>
        /// Configure dialog buttons based on current quest state
        /// </summary>
        private void ConfigureDialogButtons()
        {
            if (acceptQuestButton != null)
            {
                bool canAcceptQuest = !questToGive.IsActive && !questToGive.IsCompleted && (!questGiven || allowRepeatQuests);
                acceptQuestButton.gameObject.SetActive(canAcceptQuest);

                if (canAcceptQuest)
                {
                    acceptQuestButton.onClick.RemoveAllListeners();
                    acceptQuestButton.onClick.AddListener(AcceptQuest);

                    Text buttonText = acceptQuestButton.GetComponentInChildren<Text>();
                    if (buttonText != null)
                        buttonText.text = "Accept Quest";
                }
            }

            if (declineButton != null)
            {
                declineButton.onClick.RemoveAllListeners();
                declineButton.onClick.AddListener(CloseDialog);

                Text buttonText = declineButton.GetComponentInChildren<Text>();
                if (buttonText != null)
                    buttonText.text = questToGive.IsActive || questToGive.IsCompleted ? "Close" : "Decline";
            }
        }

        /// <summary>
        /// Accept the quest and start it
        /// </summary>
        public void AcceptQuest()
        {
            if (questToGive == null || questToGive.IsActive) return;

            U3DQuestManager questManager = U3DQuestManager.Instance;
            if (questManager != null)
            {
                questManager.StartQuest(questToGive);
                questGiven = true;
                UpdateVisualState();
                CloseDialog();

                Debug.Log($"Quest '{questToGive.questTitle}' accepted from {giverName}");
            }
            else
            {
                Debug.LogWarning("No QuestManager found in scene. Add a QuestManager to use quest givers.");
            }
        }

        /// <summary>
        /// Close the dialog UI
        /// </summary>
        public void CloseDialog()
        {
            if (dialogCanvas != null)
                dialogCanvas.gameObject.SetActive(false);
        }

        /// <summary>
        /// Check if this quest giver can give their quest
        /// </summary>
        private bool CanGiveQuest()
        {
            if (questToGive == null) return false;

            // Can give quest if: not given yet, or is repeatable, or quest was completed and is repeatable
            return !questGiven || allowRepeatQuests || (questToGive.IsCompleted && allowRepeatQuests);
        }

        /// <summary>
        /// Update visual indicators based on quest state
        /// </summary>
        private void UpdateVisualState()
        {
            bool hasQuestAvailable = CanGiveQuest() && !questToGive.IsActive && !questToGive.IsCompleted;
            bool questCompleted = questToGive != null && questToGive.IsCompleted;

            if (questAvailableIndicator != null)
                questAvailableIndicator.SetActive(hasQuestAvailable);

            if (questCompletedIndicator != null)
                questCompletedIndicator.SetActive(questCompleted);

            if (interactionPrompt != null)
                interactionPrompt.SetActive(playerInRange && (hasQuestAvailable || questToGive.IsActive || questCompleted));
        }

        /// <summary>
        /// Set up UI components if not manually assigned
        /// </summary>
        private void SetupUI()
        {
            // Create default dialog canvas if none assigned
            if (dialogCanvas == null)
            {
                CreateDefaultDialogUI();
            }
        }

        /// <summary>
        /// Create a basic dialog UI using Unity's built-in components
        /// </summary>
        private void CreateDefaultDialogUI()
        {
            // Create canvas
            GameObject canvasObj = new GameObject("QuestGiverDialog");
            canvasObj.transform.SetParent(transform);
            dialogCanvas = canvasObj.AddComponent<Canvas>();
            dialogCanvas.renderMode = RenderMode.WorldSpace;
            dialogCanvas.worldCamera = Camera.main;

            // Add GraphicRaycaster for button interaction
            canvasObj.AddComponent<GraphicRaycaster>();

            // Position canvas above quest giver
            canvasObj.transform.localPosition = Vector3.up * 2f;
            canvasObj.transform.localScale = Vector3.one * 0.01f; // Scale down for world space

            // Create background panel
            GameObject panelObj = new GameObject("DialogPanel");
            panelObj.transform.SetParent(canvasObj.transform, false);
            Image panelImage = panelObj.AddComponent<Image>();
            panelImage.color = new Color(0, 0, 0, 0.8f);

            RectTransform panelRect = panelObj.GetComponent<RectTransform>();
            panelRect.sizeDelta = new Vector2(400, 300);

            // Create giver name text
            GameObject nameObj = new GameObject("GiverName");
            nameObj.transform.SetParent(panelObj.transform, false);
            giverNameText = nameObj.AddComponent<Text>();
            giverNameText.text = giverName;
            giverNameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            giverNameText.fontSize = 24;
            giverNameText.color = Color.white;
            giverNameText.alignment = TextAnchor.MiddleCenter;

            RectTransform nameRect = nameObj.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0, 0.7f);
            nameRect.anchorMax = new Vector2(1, 1);
            nameRect.offsetMin = Vector2.zero;
            nameRect.offsetMax = Vector2.zero;

            // Create description text
            GameObject descObj = new GameObject("QuestDescription");
            descObj.transform.SetParent(panelObj.transform, false);
            questDescriptionText = descObj.AddComponent<Text>();
            questDescriptionText.text = questOfferText;
            questDescriptionText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            questDescriptionText.fontSize = 16;
            questDescriptionText.color = Color.white;
            questDescriptionText.alignment = TextAnchor.MiddleCenter;
            questDescriptionText.horizontalOverflow = HorizontalWrapMode.Wrap;
            questDescriptionText.verticalOverflow = VerticalWrapMode.Overflow;

            RectTransform descRect = descObj.GetComponent<RectTransform>();
            descRect.anchorMin = new Vector2(0, 0.3f);
            descRect.anchorMax = new Vector2(1, 0.7f);
            descRect.offsetMin = new Vector2(10, 0);
            descRect.offsetMax = new Vector2(-10, 0);

            // Create accept button
            GameObject acceptObj = new GameObject("AcceptButton");
            acceptObj.transform.SetParent(panelObj.transform, false);
            acceptQuestButton = acceptObj.AddComponent<Button>();
            Image acceptImage = acceptObj.AddComponent<Image>();
            acceptImage.color = new Color(0.2f, 0.8f, 0.2f, 1f);

            RectTransform acceptRect = acceptObj.GetComponent<RectTransform>();
            acceptRect.anchorMin = new Vector2(0.1f, 0.05f);
            acceptRect.anchorMax = new Vector2(0.45f, 0.25f);
            acceptRect.offsetMin = Vector2.zero;
            acceptRect.offsetMax = Vector2.zero;

            GameObject acceptTextObj = new GameObject("Text");
            acceptTextObj.transform.SetParent(acceptObj.transform, false);
            Text acceptText = acceptTextObj.AddComponent<Text>();
            acceptText.text = "Accept";
            acceptText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            acceptText.fontSize = 14;
            acceptText.color = Color.white;
            acceptText.alignment = TextAnchor.MiddleCenter;

            RectTransform acceptTextRect = acceptTextObj.GetComponent<RectTransform>();
            acceptTextRect.anchorMin = Vector2.zero;
            acceptTextRect.anchorMax = Vector2.one;
            acceptTextRect.offsetMin = Vector2.zero;
            acceptTextRect.offsetMax = Vector2.zero;

            // Create decline button
            GameObject declineObj = new GameObject("DeclineButton");
            declineObj.transform.SetParent(panelObj.transform, false);
            declineButton = declineObj.AddComponent<Button>();
            Image declineImage = declineObj.AddComponent<Image>();
            declineImage.color = new Color(0.8f, 0.2f, 0.2f, 1f);

            RectTransform declineRect = declineObj.GetComponent<RectTransform>();
            declineRect.anchorMin = new Vector2(0.55f, 0.05f);
            declineRect.anchorMax = new Vector2(0.9f, 0.25f);
            declineRect.offsetMin = Vector2.zero;
            declineRect.offsetMax = Vector2.zero;

            GameObject declineTextObj = new GameObject("Text");
            declineTextObj.transform.SetParent(declineObj.transform, false);
            Text declineText = declineTextObj.AddComponent<Text>();
            declineText.text = "Decline";
            declineText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            declineText.fontSize = 14;
            declineText.color = Color.white;
            declineText.alignment = TextAnchor.MiddleCenter;

            RectTransform declineTextRect = declineTextObj.GetComponent<RectTransform>();
            declineTextRect.anchorMin = Vector2.zero;
            declineTextRect.anchorMax = Vector2.one;
            declineTextRect.offsetMin = Vector2.zero;
            declineTextRect.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// Reset this quest giver (useful for testing)
        /// </summary>
        [ContextMenu("Reset Quest Giver")]
        public void ResetQuestGiver()
        {
            questGiven = false;
            UpdateVisualState();
            CloseDialog();
        }

        private void OnDrawGizmosSelected()
        {
            // Draw interaction range
            if (interactionRange > 0)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawWireSphere(transform.position, interactionRange);
            }
        }
    }
}