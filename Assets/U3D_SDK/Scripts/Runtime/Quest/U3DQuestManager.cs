using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace U3D
{
    /// <summary>
    /// Manages all quests in the scene. Handles quest progression, completion, and UI updates.
    /// This component should be placed on a single GameObject in your scene.
    /// </summary>
    [AddComponentMenu("U3D/Quest System/U3D Quest Manager")]
    public class U3DQuestManager : MonoBehaviour
    {
        [Header("Quest System Configuration")]
        [Tooltip("Automatically assign available quests to this manager when they're added to the scene")]
        [SerializeField] private bool autoDiscoverQuests = true;

        [Tooltip("List of all quests in the scene. Quests will auto-populate here if Auto Discover is enabled")]
        [SerializeField] private List<U3DQuest> availableQuests = new List<U3DQuest>();

        [Tooltip("Currently active quests that players can work on")]
        [SerializeField] private List<U3DQuest> activeQuests = new List<U3DQuest>();

        [Header("UI References")]
        [Tooltip("Canvas that displays the quest log UI. Leave empty to use default quest UI")]
        [SerializeField] private Canvas questLogCanvas;

        [Tooltip("Parent transform where quest UI elements will be created")]
        [SerializeField] private Transform questListParent;

        [Header("Quest Events")]
        [Tooltip("Called when any quest is started")]
        public UnityEvent<U3DQuest> OnQuestStarted;

        [Tooltip("Called when any quest is completed")]
        public UnityEvent<U3DQuest> OnQuestCompleted;

        [Tooltip("Called when any objective is updated")]
        public UnityEvent<U3DQuestObjective> OnObjectiveUpdated;

        [Header("Audio (Optional)")]
        [Tooltip("Sound played when a quest is started")]
        [SerializeField] private AudioClip questStartSound;

        [Tooltip("Sound played when a quest is completed")]
        [SerializeField] private AudioClip questCompleteSound;

        [Tooltip("Sound played when an objective is completed")]
        [SerializeField] private AudioClip objectiveCompleteSound;

        private AudioSource audioSource;
        private static U3DQuestManager instance;
        private Dictionary<U3DQuest, GameObject> questUIElements = new Dictionary<U3DQuest, GameObject>();

        public static U3DQuestManager Instance
        {
            get
            {
                if (instance == null)
                    instance = FindAnyObjectByType<U3DQuestManager>();
                return instance;
            }
        }

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                    audioSource = gameObject.AddComponent<AudioSource>();
            }
            else if (instance != this)
            {
                Debug.LogWarning("Multiple QuestManagers found. Destroying duplicate on: " + gameObject.name);
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            if (autoDiscoverQuests)
                DiscoverQuests();

            InitializeQuestUI();
        }

        /// <summary>
        /// Automatically finds all U3DQuest components in the scene
        /// </summary>
        private void DiscoverQuests()
        {
            U3DQuest[] allQuests = FindObjectsByType<U3DQuest>(FindObjectsSortMode.None);
            foreach (U3DQuest quest in allQuests)
            {
                if (!availableQuests.Contains(quest))
                {
                    availableQuests.Add(quest);
                    quest.Initialize(this);
                }
            }
        }

        /// <summary>
        /// Start a specific quest
        /// </summary>
        public void StartQuest(U3DQuest quest)
        {
            if (quest == null || activeQuests.Contains(quest))
                return;

            activeQuests.Add(quest);
            quest.StartQuest();

            PlaySound(questStartSound);
            OnQuestStarted?.Invoke(quest);

            CreateQuestUI(quest);
            UpdateQuestLogVisibility(); // CRITICAL: This was missing!

            Debug.Log($"Quest Started: {quest.questTitle}");
        }

        /// <summary>
        /// Start a quest by name
        /// </summary>
        public void StartQuest(string questName)
        {
            U3DQuest quest = availableQuests.Find(q => q.questTitle == questName);
            if (quest != null)
                StartQuest(quest);
            else
                Debug.LogWarning($"Quest not found: {questName}");
        }

        /// <summary>
        /// Complete a quest and handle rewards
        /// </summary>
        public void CompleteQuest(U3DQuest quest)
        {
            if (quest == null || !activeQuests.Contains(quest))
                return;

            activeQuests.Remove(quest);
            quest.CompleteQuest();

            PlaySound(questCompleteSound);
            OnQuestCompleted?.Invoke(quest);

            RemoveQuestUI(quest);
            UpdateQuestLogVisibility();

            Debug.Log($"Quest Completed: {quest.questTitle}");
        }

        /// <summary>
        /// Called when an objective is updated
        /// </summary>
        public void OnObjectiveProgress(U3DQuestObjective objective)
        {
            if (objective.IsCompleted)
                PlaySound(objectiveCompleteSound);

            OnObjectiveUpdated?.Invoke(objective);

            // Check if parent quest is complete
            U3DQuest parentQuest = objective.GetComponentInParent<U3DQuest>();
            if (parentQuest != null && parentQuest.IsCompleted && activeQuests.Contains(parentQuest))
            {
                UpdateQuestUI(parentQuest);

                if (parentQuest.IsCompleted)
                {
                    CompleteQuest(parentQuest);
                }
            }
        }

        /// <summary>
        /// Get all active quests
        /// </summary>
        public List<U3DQuest> GetActiveQuests()
        {
            return new List<U3DQuest>(activeQuests);
        }

        /// <summary>
        /// Check if a quest is currently active
        /// </summary>
        public bool IsQuestActive(U3DQuest quest)
        {
            return activeQuests.Contains(quest);
        }

        /// <summary>
        /// Initialize the quest UI if canvas is assigned
        /// </summary>
        private void InitializeQuestUI()
        {
            if (questLogCanvas != null)
            {
                questLogCanvas.gameObject.layer = LayerMask.NameToLayer("UI");
                questLogCanvas.sortingOrder = 100;
                questLogCanvas.gameObject.SetActive(false);
                questLogCanvas.enabled = true;
            }
            else
            {
                Debug.LogError("QuestLogCanvas is not assigned in QuestManager!");
            }
        }

        /// <summary>
        /// Play audio clip if available
        /// </summary>
        private void PlaySound(AudioClip clip)
        {
            if (clip != null && audioSource != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }

        /// <summary>
        /// Reset all quest progress (useful for testing)
        /// </summary>
        [ContextMenu("Reset All Quests")]
        public void ResetAllQuests()
        {
            foreach (U3DQuest quest in availableQuests)
            {
                quest.ResetQuest();
            }
            activeQuests.Clear();
            Debug.Log("All quests reset");
        }

        /// <summary>
        /// Update quest log visibility based on active quests
        /// </summary>
        private void UpdateQuestLogVisibility()
        {
            if (questLogCanvas != null)
            {
                bool shouldShow = activeQuests.Count > 0;

                if (shouldShow)
                {
                    questLogCanvas.gameObject.SetActive(true);
                    questLogCanvas.enabled = true;
                    Canvas.ForceUpdateCanvases();
                }
                else
                {
                    questLogCanvas.gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// Create UI elements for a quest
        /// </summary>
        private void CreateQuestUI(U3DQuest quest)
        {
            if (questListParent == null || quest == null)
            {
                Debug.LogError("questListParent is null! Cannot create quest UI.");
                return;
            }

            if (questUIElements.ContainsKey(quest))
                return;

            // Create quest container
            GameObject questContainer = new GameObject($"Quest_{quest.questTitle}");
            questContainer.transform.SetParent(questListParent, false);
            questContainer.layer = LayerMask.NameToLayer("UI");

            // Add RectTransform component
            RectTransform containerRect = questContainer.AddComponent<RectTransform>();
            containerRect.anchorMin = Vector2.zero;
            containerRect.anchorMax = Vector2.one;
            containerRect.offsetMin = Vector2.zero;
            containerRect.offsetMax = Vector2.zero;

            // Add LayoutElement for proper sizing
            LayoutElement layoutElement = questContainer.AddComponent<LayoutElement>();
            layoutElement.minHeight = 80;
            layoutElement.preferredHeight = 100;

            // Create vertical layout for quest content
            VerticalLayoutGroup questLayout = questContainer.AddComponent<VerticalLayoutGroup>();
            questLayout.spacing = 5;
            questLayout.padding = new RectOffset(10, 10, 10, 10);
            questLayout.childControlWidth = true;
            questLayout.childControlHeight = false;
            questLayout.childForceExpandWidth = true;

            // Quest title
            GameObject titleObj = new GameObject("QuestTitle");
            titleObj.transform.SetParent(questContainer.transform, false);
            titleObj.layer = LayerMask.NameToLayer("UI");

            RectTransform titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.sizeDelta = new Vector2(0, 25);

            CanvasRenderer titleCanvasRenderer = titleObj.AddComponent<CanvasRenderer>();

            Text titleText = titleObj.AddComponent<Text>();
            titleText.text = quest.questTitle;
            titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleText.fontSize = 16;
            titleText.fontStyle = FontStyle.Bold;
            titleText.color = Color.white;
            titleText.alignment = TextAnchor.MiddleLeft;
            titleText.raycastTarget = false;

            // Create objectives
            foreach (U3DQuestObjective objective in GetQuestObjectives(quest))
            {
                CreateObjectiveUI(objective, questContainer.transform);
            }

            questUIElements[quest] = questContainer;

            // Force layout rebuild
            LayoutRebuilder.ForceRebuildLayoutImmediate(containerRect);
        }

        /// <summary>
        /// Create UI for individual objective
        /// </summary>
        private void CreateObjectiveUI(U3DQuestObjective objective, Transform parent)
        {
            GameObject objContainer = new GameObject($"Objective_{objective.objectiveDescription}");
            objContainer.transform.SetParent(parent, false);
            objContainer.layer = LayerMask.NameToLayer("UI");

            RectTransform objRect = objContainer.AddComponent<RectTransform>();
            objRect.sizeDelta = new Vector2(0, 20);

            CanvasRenderer canvasRenderer = objContainer.AddComponent<CanvasRenderer>();

            Text objText = objContainer.AddComponent<Text>();
            objText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            objText.fontSize = 12;
            objText.color = Color.white;
            objText.text = objective.GetObjectiveStatus();
            objText.alignment = TextAnchor.MiddleLeft;
            objText.raycastTarget = false;

            objContainer.name = $"ObjectiveUI_{objective.GetInstanceID()}";
        }

        /// <summary>
        /// Verify canvas setup for Unity 6+ compatibility
        /// </summary>
        [ContextMenu("Verify Canvas Setup")]
        public void VerifyCanvasSetup()
        {
            if (questLogCanvas == null)
            {
                Debug.LogError("QuestLogCanvas is not assigned!");
                return;
            }

            Debug.Log($"Canvas Name: {questLogCanvas.name}");
            Debug.Log($"Canvas Active: {questLogCanvas.gameObject.activeSelf}");
            Debug.Log($"Canvas Enabled: {questLogCanvas.enabled}");
            Debug.Log($"Canvas Render Mode: {questLogCanvas.renderMode}");
            Debug.Log($"Canvas Sorting Order: {questLogCanvas.sortingOrder}");
            Debug.Log($"Canvas Layer: {questLogCanvas.gameObject.layer}");

            CanvasScaler scaler = questLogCanvas.GetComponent<CanvasScaler>();
            GraphicRaycaster raycaster = questLogCanvas.GetComponent<GraphicRaycaster>();

            Debug.Log($"Has CanvasScaler: {scaler != null}");
            Debug.Log($"Has GraphicRaycaster: {raycaster != null}");

            if (questListParent != null)
            {
                Debug.Log($"Quest List Parent: {questListParent.name}");
                Debug.Log($"Quest List Parent Active: {questListParent.gameObject.activeSelf}");
            }
        }

        /// <summary>
        /// Update UI for a quest
        /// </summary>
        private void UpdateQuestUI(U3DQuest quest)
        {
            if (!questUIElements.ContainsKey(quest))
                return;

            GameObject questContainer = questUIElements[quest];
            if (questContainer == null)
                return;

            // Update all objective texts
            foreach (U3DQuestObjective objective in GetQuestObjectives(quest))
            {
                Transform objUI = questContainer.transform.Find($"ObjectiveUI_{objective.GetInstanceID()}");
                if (objUI != null)
                {
                    Text objText = objUI.GetComponent<Text>();
                    if (objText != null)
                    {
                        objText.text = objective.GetObjectiveStatus();
                        objText.color = objective.IsCompleted ? Color.green : (objective.IsActive ? Color.white : Color.gray);
                    }
                }
            }
        }

        /// <summary>
        /// Remove UI for a quest
        /// </summary>
        private void RemoveQuestUI(U3DQuest quest)
        {
            if (questUIElements.ContainsKey(quest))
            {
                if (questUIElements[quest] != null)
                    Destroy(questUIElements[quest]);
                questUIElements.Remove(quest);
            }
        }

        /// <summary>
        /// Get objectives from a quest
        /// </summary>
        private List<U3DQuestObjective> GetQuestObjectives(U3DQuest quest)
        {
            return new List<U3DQuestObjective>(quest.GetComponentsInChildren<U3DQuestObjective>());
        }
    }
}