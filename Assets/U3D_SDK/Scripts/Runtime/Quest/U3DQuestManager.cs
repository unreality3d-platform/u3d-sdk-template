using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

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
                CompleteQuest(parentQuest);
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
                questLogCanvas.gameObject.SetActive(true);
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
    }
}