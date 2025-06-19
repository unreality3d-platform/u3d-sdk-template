using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using UnityEngine.Events;

namespace U3D
{
    /// <summary>
    /// NPC or object that gives quests to players.
    /// FIXED: Proper choice system, TMP display, KeyCode handling
    /// </summary>
    [AddComponentMenu("U3D/Quest System/U3D Quest Giver")]
    public partial class U3DQuestGiver : MonoBehaviour
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

        [Tooltip("Should interaction happen automatically when player gets close?")]
        [SerializeField] private bool autoInteract = false;

        [Header("Interaction Choices")]
        [Tooltip("Available interaction choices for this quest giver. Default is 'Accept [E]'")]
        [SerializeField] private List<U3DInteractionChoice> interactionChoices = new List<U3DInteractionChoice>();

        [Header("UI Dialog")]
        [Tooltip("Canvas for quest dialog UI. An unstyled Dialogue Canvas will be created at runtime if one is not assigned.")]
        [SerializeField] private Canvas dialogCanvas;

        [Tooltip("TextMeshPro component for quest giver's name. Auto-created if not assigned.")]
        [SerializeField] private TextMeshProUGUI giverNameText;

        [Tooltip("TextMeshPro component for quest description. Auto-created if not assigned.")]
        [SerializeField] private TextMeshProUGUI questDescriptionText;

        [Tooltip("Parent object for interaction choice display. Auto-created if not assigned.")]
        [SerializeField] private Transform choicesParent;

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

        [Header("Events")]
        [Tooltip("Called when a choice is selected")]
        public U3DInteractionChoiceEvent OnChoiceSelected;

        private Transform playerTransform;
        private bool playerInRange = false;
        private bool questGiven = false;
        private List<TextMeshProUGUI> choiceDisplays = new List<TextMeshProUGUI>();

        private void Awake()
        {
            // FIXED: Initialize default choices if none are set - using proper list setup
            InitializeDefaultChoices();
        }

        /// <summary>
        /// FIXED: Properly initialize default choices if list is empty
        /// </summary>
        private void InitializeDefaultChoices()
        {
            if (interactionChoices.Count == 0)
            {
                interactionChoices.Add(new U3DInteractionChoice(KeyCode.E, "Accept", "accept"));
                Debug.Log("QuestGiver: Initialized with default 'Accept [E]' choice");
            }
        }

        private void Start()
        {
            // Find player
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                playerTransform = player.transform;

            // FIXED: Ensure quest doesn't auto-start if it has a QuestGiver
            PreventQuestAutoStart();

            // Set up UI
            SetupUI();

            // Hide dialog initially
            if (dialogCanvas != null)
                dialogCanvas.gameObject.SetActive(false);

            UpdateVisualState();
        }

        /// <summary>
        /// FIXED: Prevent quest from auto-starting if it has a QuestGiver
        /// </summary>
        private void PreventQuestAutoStart()
        {
            if (questToGive != null)
            {
                // Check if quest has auto-start enabled and disable it
                // This prevents conflicts between QuestGiver workflow and auto-start
#if UNITY_EDITOR
                UnityEditor.SerializedObject serializedQuest = new UnityEditor.SerializedObject(questToGive);
                UnityEditor.SerializedProperty autoStartProp = serializedQuest.FindProperty("startAutomatically");
                if (autoStartProp != null && autoStartProp.boolValue == true)
                {
                    autoStartProp.boolValue = false;
                    serializedQuest.ApplyModifiedProperties();
                    Debug.Log($"QuestGiver: Disabled auto-start on quest '{questToGive.questTitle}' - will wait for QuestGiver acceptance");
                }
#endif
            }
        }

        private void Update()
        {
            CheckPlayerProximity();
            HandleInteractionChoices();
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
        /// FIXED: Handle input for interaction choices with proper key detection
        /// </summary>
        private void HandleInteractionChoices()
        {
            if (!playerInRange || !CanGiveQuest()) return;

            // Check if dialog is open - only process choices when dialog is visible
            if (dialogCanvas != null && dialogCanvas.gameObject.activeSelf)
            {
                foreach (U3DInteractionChoice choice in interactionChoices)
                {
                    if (choice.WasKeyPressed())
                    {
                        HandleChoiceSelected(choice);
                        return;
                    }
                }

                // Check for additional context-based choices
                foreach (U3DInteractionChoice choice in GetAvailableChoices())
                {
                    if (choice.WasKeyPressed())
                    {
                        HandleChoiceSelected(choice);
                        return;
                    }
                }
            }
            else
            {
                // Check for any interaction key to open dialog
                foreach (U3DInteractionChoice choice in interactionChoices)
                {
                    if (choice.WasKeyPressed())
                    {
                        StartInteraction();
                        return;
                    }
                }

                // Fallback: mouse/touch input to open dialog
                if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                {
                    StartInteraction();
                }
            }
        }

        /// <summary>
        /// Handle when a specific choice is selected
        /// </summary>
        private void HandleChoiceSelected(U3DInteractionChoice choice)
        {
            OnChoiceSelected?.Invoke(choice);

            // Handle built-in quest logic
            switch (choice.choiceID.ToLower())
            {
                case "accept":
                    AcceptQuest();
                    break;
                case "decline":
                case "close":
                    CloseDialog();
                    break;
                default:
                    // Custom choice - just close dialog and let events handle it
                    CloseDialog();
                    break;
            }

            Debug.Log($"Choice selected: {choice.choiceLabel} [{choice.choiceKey}]");
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
        /// FIXED: Show the quest dialog UI with proper choices display
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

                // FIXED: Update choices display properly
                UpdateChoicesDisplay();
            }
        }

        /// <summary>
        /// FIXED: Update the choices display in the dialog with proper TMP components
        /// </summary>
        private void UpdateChoicesDisplay()
        {
            if (choicesParent == null) return;

            // Clear existing choice displays
            foreach (TextMeshProUGUI choiceDisplay in choiceDisplays)
            {
                if (choiceDisplay != null)
                    DestroyImmediate(choiceDisplay.gameObject);
            }
            choiceDisplays.Clear();

            // Show appropriate choices based on quest state
            List<U3DInteractionChoice> availableChoices = GetAvailableChoices();

            // Create choice displays with proper TMP components
            foreach (U3DInteractionChoice choice in availableChoices)
            {
                CreateChoiceDisplay(choice);
            }

            // FIXED: Force layout rebuild
            LayoutRebuilder.ForceRebuildLayoutImmediate(choicesParent.GetComponent<RectTransform>());
        }

        /// <summary>
        /// Get choices available based on current quest state
        /// </summary>
        private List<U3DInteractionChoice> GetAvailableChoices()
        {
            List<U3DInteractionChoice> available = new List<U3DInteractionChoice>();

            if (questToGive.IsCompleted || questToGive.IsActive)
            {
                // Just show close option
                available.Add(new U3DInteractionChoice(KeyCode.E, "Close", "close"));
            }
            else
            {
                // Show configured choices for quest offering
                available.AddRange(interactionChoices);

                // Add decline option if not already present
                bool hasDecline = false;
                foreach (var choice in interactionChoices)
                {
                    if (choice.choiceID.ToLower() == "decline")
                    {
                        hasDecline = true;
                        break;
                    }
                }

                if (!hasDecline)
                {
                    available.Add(new U3DInteractionChoice(KeyCode.X, "Decline", "decline"));
                }
            }

            return available;
        }

        /// <summary>
        /// FIXED: Create a choice display UI element with proper TMP components
        /// </summary>
        private void CreateChoiceDisplay(U3DInteractionChoice choice)
        {
            GameObject choiceObj = new GameObject($"Choice_{choice.choiceID}");
            choiceObj.transform.SetParent(choicesParent, false);
            choiceObj.layer = LayerMask.NameToLayer("UI");

            // Add RectTransform
            RectTransform choiceRect = choiceObj.AddComponent<RectTransform>();
            choiceRect.sizeDelta = new Vector2(0, 40);

            // FIXED: Add LayoutElement for proper sizing
            LayoutElement layoutElement = choiceObj.AddComponent<LayoutElement>();
            layoutElement.minHeight = 40;
            layoutElement.preferredHeight = 40;

            // FIXED: Create TextMeshPro component properly
            TextMeshProUGUI choiceText = choiceObj.AddComponent<TextMeshProUGUI>();
            choiceText.text = choice.GetDisplayText(); // "Accept [E]"
            choiceText.fontSize = 32; // Large, readable font
            choiceText.color = Color.white;
            choiceText.alignment = TextAlignmentOptions.MidlineLeft;
            choiceText.raycastTarget = false;

            // FIXED: Ensure proper anchoring
            choiceRect.anchorMin = Vector2.zero;
            choiceRect.anchorMax = Vector2.one;
            choiceRect.offsetMin = Vector2.zero;
            choiceRect.offsetMax = Vector2.zero;

            choiceDisplays.Add(choiceText);

            Debug.Log($"Created choice display: {choiceText.text}");
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
        /// FIXED: Create a dialog UI with proper TMP and layout components
        /// </summary>
        private void CreateDefaultDialogUI()
        {
            // Create canvas
            GameObject canvasObj = new GameObject("QuestGiverDialog");
            canvasObj.transform.SetParent(transform);
            dialogCanvas = canvasObj.AddComponent<Canvas>();
            dialogCanvas.renderMode = RenderMode.WorldSpace;
            dialogCanvas.worldCamera = Camera.main;

            // Add GraphicRaycaster for interaction
            canvasObj.AddComponent<GraphicRaycaster>();

            // Position canvas above quest giver
            canvasObj.transform.localPosition = Vector3.up * 2f;
            canvasObj.transform.localScale = Vector3.one * 0.01f;

            // Create background panel
            GameObject panelObj = new GameObject("DialogPanel");
            panelObj.transform.SetParent(canvasObj.transform, false);
            Image panelImage = panelObj.AddComponent<Image>();
            panelImage.color = new Color(0, 0, 0, 0.8f);

            RectTransform panelRect = panelObj.GetComponent<RectTransform>();
            panelRect.sizeDelta = new Vector2(400, 350); // Taller for choices

            // Create giver name text
            GameObject nameObj = new GameObject("GiverName");
            nameObj.transform.SetParent(panelObj.transform, false);
            giverNameText = nameObj.AddComponent<TextMeshProUGUI>();
            giverNameText.text = giverName;
            giverNameText.fontSize = 36;
            giverNameText.fontStyle = FontStyles.Bold;
            giverNameText.color = Color.white;
            giverNameText.alignment = TextAlignmentOptions.Center;

            RectTransform nameRect = nameObj.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0, 0.8f);
            nameRect.anchorMax = new Vector2(1, 1);
            nameRect.offsetMin = Vector2.zero;
            nameRect.offsetMax = Vector2.zero;

            // Create description text
            GameObject descObj = new GameObject("QuestDescription");
            descObj.transform.SetParent(panelObj.transform, false);
            questDescriptionText = descObj.AddComponent<TextMeshProUGUI>();
            questDescriptionText.text = questOfferText;
            questDescriptionText.fontSize = 28;
            questDescriptionText.color = Color.white;
            questDescriptionText.alignment = TextAlignmentOptions.Center;
            questDescriptionText.textWrappingMode = TextWrappingModes.Normal;

            RectTransform descRect = descObj.GetComponent<RectTransform>();
            descRect.anchorMin = new Vector2(0, 0.4f);
            descRect.anchorMax = new Vector2(1, 0.8f);
            descRect.offsetMin = new Vector2(10, 0);
            descRect.offsetMax = new Vector2(-10, 0);

            // FIXED: Create choices container with proper layout components
            GameObject choicesObj = new GameObject("Choices");
            choicesObj.transform.SetParent(panelObj.transform, false);
            choicesParent = choicesObj.transform;

            RectTransform choicesRect = choicesObj.GetComponent<RectTransform>();
            choicesRect.anchorMin = new Vector2(0, 0.05f);
            choicesRect.anchorMax = new Vector2(1, 0.4f);
            choicesRect.offsetMin = new Vector2(10, 0);
            choicesRect.offsetMax = new Vector2(-10, 0);

            // FIXED: Add vertical layout for choices with proper settings
            VerticalLayoutGroup choicesLayout = choicesObj.AddComponent<VerticalLayoutGroup>();
            choicesLayout.spacing = 5;
            choicesLayout.childControlWidth = true;
            choicesLayout.childControlHeight = false;
            choicesLayout.childForceExpandWidth = true;
            choicesLayout.childAlignment = TextAnchor.MiddleLeft;
            choicesLayout.padding = new RectOffset(5, 5, 5, 5);

            // FIXED: Add ContentSizeFitter for dynamic sizing
            ContentSizeFitter choicesFitter = choicesObj.AddComponent<ContentSizeFitter>();
            choicesFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            choicesFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
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