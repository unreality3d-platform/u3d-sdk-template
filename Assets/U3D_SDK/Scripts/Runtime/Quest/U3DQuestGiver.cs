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
    /// FIXED: UnityEvent field names updated to avoid interface method conflicts
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

        [Header("Creator Events")]
        [Tooltip("Called when a choice is selected")]
        public U3DInteractionChoiceEvent OnChoiceSelected;

        [Tooltip("Called when player enters interaction range")]
        public UnityEvent OnPlayerEnterRangeEvent;

        [Tooltip("Called when player exits interaction range")]
        public UnityEvent OnPlayerExitRangeEvent;

        private Transform playerTransform;
        private bool playerInRange = false;
        private bool questGiven = false;
        private List<TextMeshProUGUI> choiceDisplays = new List<TextMeshProUGUI>();

        private void Awake()
        {
            InitializeDefaultChoices();
        }

        private void InitializeDefaultChoices()
        {
            if (interactionChoices.Count == 0)
            {
                // PROTECTED: Use U3DKeyManager to get safe key (respects PlayerController)
                KeyCode safeKey = U3DKeyManager.GetSafeAlternative(KeyCode.E);
                if (safeKey != KeyCode.E)
                {
                    Debug.LogWarning($"QuestGiver: E key conflicts with PlayerController. Using {safeKey} instead.");
                }

                interactionChoices.Add(new U3DInteractionChoice(safeKey, "Accept", "accept"));
                Debug.Log($"QuestGiver: Initialized with safe 'Accept [{safeKey}]' choice");
            }
        }

        private void Start()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                playerTransform = player.transform;

            PreventQuestAutoStart();
            SetupUI();

            if (dialogCanvas != null)
                dialogCanvas.gameObject.SetActive(false);

            UpdateVisualState();
        }

        private void PreventQuestAutoStart()
        {
            if (questToGive != null)
            {
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

        private void CheckPlayerProximity()
        {
            if (playerTransform == null || interactionRange <= 0) return;

            float distance = Vector3.Distance(transform.position, playerTransform.position);
            bool wasInRange = playerInRange;
            playerInRange = distance <= interactionRange;

            if (playerInRange != wasInRange)
            {
                if (interactionPrompt != null)
                    interactionPrompt.SetActive(playerInRange && CanGiveQuest());

                // NOTE: The interface methods will handle system behavior automatically
                // These UnityEvents are for Creator customization only
                if (playerInRange)
                    OnPlayerEnterRangeEvent?.Invoke();
                else
                    OnPlayerExitRangeEvent?.Invoke();

                if (playerInRange && autoInteract && CanGiveQuest())
                {
                    StartInteraction();
                }
            }
        }

        private void HandleInteractionChoices()
        {
            if (!playerInRange || !CanGiveQuest()) return;

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
                foreach (U3DInteractionChoice choice in interactionChoices)
                {
                    if (choice.WasKeyPressed())
                    {
                        StartInteraction();
                        return;
                    }
                }

                if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                {
                    StartInteraction();
                }
            }
        }

        private void HandleChoiceSelected(U3DInteractionChoice choice)
        {
            OnChoiceSelected?.Invoke(choice);

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
                    CloseDialog();
                    break;
            }

            Debug.Log($"Choice selected: {choice.choiceLabel} [{choice.choiceKey}]");
        }

        public void StartInteraction()
        {
            if (questToGive == null) return;

            string dialogText = GetAppropriateDialogText();
            ShowDialog(dialogText);
        }

        private string GetAppropriateDialogText()
        {
            if (questToGive.IsCompleted)
                return questCompletedText;
            else if (questToGive.IsActive)
                return questActiveText;
            else
                return questOfferText;
        }

        private void ShowDialog(string dialogText)
        {
            if (dialogCanvas != null)
            {
                dialogCanvas.gameObject.SetActive(true);

                if (giverNameText != null)
                    giverNameText.text = giverName;

                if (questDescriptionText != null)
                    questDescriptionText.text = dialogText;

                UpdateChoicesDisplay();
            }
        }

        /// <summary>
        /// FIXED: Update choices display without layout components
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

            // Create choice displays
            foreach (U3DInteractionChoice choice in availableChoices)
            {
                CreateChoiceDisplay(choice);
            }

            Debug.Log($"Updated choices display with {availableChoices.Count} choices");
        }

        private List<U3DInteractionChoice> GetAvailableChoices()
        {
            List<U3DInteractionChoice> available = new List<U3DInteractionChoice>();

            if (questToGive.IsCompleted || questToGive.IsActive)
            {
                KeyCode safeKey = U3DKeyManager.GetSafeAlternative(KeyCode.E);
                available.Add(new U3DInteractionChoice(safeKey, "Close", "close"));
            }
            else
            {
                available.AddRange(interactionChoices);

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
                    KeyCode safeKey = U3DKeyManager.GetSafeAlternative(KeyCode.X);
                    available.Add(new U3DInteractionChoice(safeKey, "Decline", "decline"));
                }
            }

            return available;
        }

        /// <summary>
        /// FIXED: Create choice display using direct TextMeshPro creation
        /// </summary>
        private void CreateChoiceDisplay(U3DInteractionChoice choice)
        {
            if (choicesParent == null) return;

            // Create choice text object directly
            GameObject choiceObj = new GameObject($"Choice_{choice.choiceID}");
            choiceObj.transform.SetParent(choicesParent, false);
            choiceObj.layer = LayerMask.NameToLayer("UI");

            // Add RectTransform and position manually
            RectTransform choiceRect = choiceObj.AddComponent<RectTransform>();
            float yPosition = -40f * choiceDisplays.Count;
            choiceRect.anchorMin = new Vector2(0f, 1f);
            choiceRect.anchorMax = new Vector2(1f, 1f);
            choiceRect.anchoredPosition = new Vector2(0f, yPosition);
            choiceRect.sizeDelta = new Vector2(0f, 40f);

            // Create TextMeshPro component directly
            TextMeshProUGUI choiceText = choiceObj.AddComponent<TextMeshProUGUI>();
            choiceText.text = choice.GetDisplayText();
            choiceText.fontSize = 32;
            choiceText.color = Color.white;
            choiceText.alignment = TextAlignmentOptions.MidlineLeft;
            choiceText.raycastTarget = false;

            choiceDisplays.Add(choiceText);

            Debug.Log($"Created choice display: {choiceText.text}");
        }

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

        public void CloseDialog()
        {
            if (dialogCanvas != null)
                dialogCanvas.gameObject.SetActive(false);
        }

        private bool CanGiveQuest()
        {
            if (questToGive == null) return false;
            return !questGiven || allowRepeatQuests || (questToGive.IsCompleted && allowRepeatQuests);
        }

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

        private void SetupUI()
        {
            if (dialogCanvas == null)
            {
                CreateDefaultDialogUI();
            }
        }

        /// <summary>
        /// FIXED: Create dialog UI using Unity DefaultControls with direct TextMeshPro creation
        /// </summary>
        private void CreateDefaultDialogUI()
        {
            // Create canvas
            GameObject canvasObj = new GameObject("QuestGiverDialog");
            canvasObj.transform.SetParent(transform);
            dialogCanvas = canvasObj.AddComponent<Canvas>();
            dialogCanvas.renderMode = RenderMode.WorldSpace;
            dialogCanvas.worldCamera = Camera.main;
            canvasObj.AddComponent<GraphicRaycaster>();

            // Position canvas above quest giver
            canvasObj.transform.localPosition = Vector3.up * 2f;
            canvasObj.transform.localScale = Vector3.one * 0.01f;

            // Use Unity's DefaultControls to create background panel
            DefaultControls.Resources uiResources = new DefaultControls.Resources();
            GameObject panelObj = DefaultControls.CreatePanel(uiResources);
            panelObj.name = "DialogPanel";
            panelObj.transform.SetParent(canvasObj.transform, false);

            Image panelImage = panelObj.GetComponent<Image>();
            panelImage.color = new Color(0, 0, 0, 0.8f);

            RectTransform panelRect = panelObj.GetComponent<RectTransform>();
            panelRect.sizeDelta = new Vector2(400, 350);

            // Create giver name text - DIRECT TextMeshPro creation
            GameObject nameObj = new GameObject("GiverName");
            nameObj.transform.SetParent(panelObj.transform, false);
            nameObj.layer = LayerMask.NameToLayer("UI");

            RectTransform nameRect = nameObj.AddComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0, 0.8f);
            nameRect.anchorMax = new Vector2(1, 1);
            nameRect.offsetMin = Vector2.zero;
            nameRect.offsetMax = Vector2.zero;

            giverNameText = nameObj.AddComponent<TextMeshProUGUI>();
            giverNameText.text = giverName;
            giverNameText.fontSize = 36;
            giverNameText.fontStyle = FontStyles.Bold;
            giverNameText.color = Color.white;
            giverNameText.alignment = TextAlignmentOptions.Center;
            giverNameText.raycastTarget = false;

            // Create description text - DIRECT TextMeshPro creation
            GameObject descObj = new GameObject("QuestDescription");
            descObj.transform.SetParent(panelObj.transform, false);
            descObj.layer = LayerMask.NameToLayer("UI");

            RectTransform descRect = descObj.AddComponent<RectTransform>();
            descRect.anchorMin = new Vector2(0, 0.4f);
            descRect.anchorMax = new Vector2(1, 0.8f);
            descRect.offsetMin = new Vector2(10, 0);
            descRect.offsetMax = new Vector2(-10, 0);

            questDescriptionText = descObj.AddComponent<TextMeshProUGUI>();
            questDescriptionText.text = questOfferText;
            questDescriptionText.fontSize = 28;
            questDescriptionText.color = Color.white;
            questDescriptionText.alignment = TextAlignmentOptions.Center;
            questDescriptionText.textWrappingMode = TextWrappingModes.Normal;
            questDescriptionText.raycastTarget = false;

            // Create choices parent as simple container
            GameObject choicesObj = new GameObject("Choices");
            choicesObj.transform.SetParent(panelObj.transform, false);
            choicesObj.layer = LayerMask.NameToLayer("UI");
            choicesParent = choicesObj.transform;

            RectTransform choicesRect = choicesObj.AddComponent<RectTransform>();
            choicesRect.anchorMin = new Vector2(0, 0.05f);
            choicesRect.anchorMax = new Vector2(1, 0.4f);
            choicesRect.offsetMin = new Vector2(10, 0);
            choicesRect.offsetMax = new Vector2(-10, 0);

            Debug.Log($"Created default dialog UI for {giverName} using DefaultControls with key protection");
        }

        [ContextMenu("Reset Quest Giver")]
        public void ResetQuestGiver()
        {
            questGiven = false;
            UpdateVisualState();
            CloseDialog();
        }

        private void OnDrawGizmosSelected()
        {
            if (interactionRange > 0)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawWireSphere(transform.position, interactionRange);
            }
        }
    }
}