using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

namespace U3D
{
    /// <summary>
    /// Unified interaction system that connects PlayerController with all interaction systems
    /// Replaces the placeholder in PlayerController.OnInteract()
    /// </summary>
    public class U3DInteractionManager : MonoBehaviour
    {
        [Header("Interaction System Configuration")]
        [Tooltip("Maximum distance to check for interactables")]
        [SerializeField] private float interactionRange = 5f;

        [Tooltip("Layer mask for interaction raycasting")]
        [SerializeField] private LayerMask interactionLayerMask = -1;

        [Tooltip("Show debug information about nearby interactables")]
        [SerializeField] private bool debugMode = false;

        private static U3DInteractionManager instance;
        private List<IU3DInteractable> nearbyInteractables = new List<IU3DInteractable>();
        private IU3DInteractable currentInteractable;
        private U3DPlayerController playerController;

        public static U3DInteractionManager Instance
        {
            get
            {
                if (instance == null)
                    instance = FindAnyObjectByType<U3DInteractionManager>();
                return instance;
            }
        }

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                playerController = GetComponent<U3DPlayerController>();
            }
            else if (instance != this)
            {
                Debug.LogWarning("Multiple InteractionManagers found. Destroying duplicate on: " + gameObject.name);
                Destroy(this);
            }
        }

        private void Start()
        {
            // Initialize key management system
            U3DKeyManager.InitializeFromPlayerController(playerController);
        }

        /// <summary>
        /// Called by PlayerController when interact button is pressed
        /// This replaces the placeholder implementation
        /// </summary>
        public void OnPlayerInteract()
        {
            if (currentInteractable != null)
            {
                currentInteractable.OnInteract();
                Debug.Log($"Interacted with: {currentInteractable.GetInteractionPrompt()}");
            }
            else
            {
                // Check for interactables in range
                FindNearbyInteractables();

                if (nearbyInteractables.Count > 0)
                {
                    // Interact with closest one
                    IU3DInteractable closest = GetClosestInteractable();
                    closest.OnInteract();
                    Debug.Log($"Interacted with: {closest.GetInteractionPrompt()}");
                }
                else
                {
                    if (debugMode)
                        Debug.Log("No interactables in range");
                }
            }
        }

        private void Update()
        {
            UpdateNearbyInteractables();
        }

        /// <summary>
        /// Update list of nearby interactables and current primary target
        /// </summary>
        private void UpdateNearbyInteractables()
        {
            FindNearbyInteractables();

            // Determine primary interactable based on priority and distance
            IU3DInteractable newPrimary = GetPrimaryInteractable();

            // Handle range enter/exit events
            if (newPrimary != currentInteractable)
            {
                // Exit previous
                if (currentInteractable != null)
                {
                    currentInteractable.OnPlayerExitRange();
                }

                // Enter new
                if (newPrimary != null)
                {
                    newPrimary.OnPlayerEnterRange();
                }

                currentInteractable = newPrimary;
            }
        }

        /// <summary>
        /// Find all interactables within range
        /// </summary>
        private void FindNearbyInteractables()
        {
            nearbyInteractables.Clear();

            // Use sphere overlap to find all colliders in range
            Collider[] colliders = Physics.OverlapSphere(transform.position, interactionRange, interactionLayerMask);

            foreach (Collider col in colliders)
            {
                // Check for IU3DInteractable components
                MonoBehaviour[] components = col.GetComponentsInChildren<MonoBehaviour>();
                IU3DInteractable[] interactables = components.OfType<IU3DInteractable>().ToArray();

                foreach (IU3DInteractable interactable in interactables)
                {
                    if (interactable.CanInteract())
                    {
                        nearbyInteractables.Add(interactable);
                    }
                }
            }

            if (debugMode && nearbyInteractables.Count > 0)
            {
                Debug.Log($"Found {nearbyInteractables.Count} interactables in range");
            }
        }

        /// <summary>
        /// Get the closest interactable object
        /// </summary>
        private IU3DInteractable GetClosestInteractable()
        {
            if (nearbyInteractables.Count == 0) return null;

            IU3DInteractable closest = null;
            float closestDistance = Mathf.Infinity;

            foreach (IU3DInteractable interactable in nearbyInteractables)
            {
                if (interactable.CanInteract())
                {
                    Vector3 targetPos = ((MonoBehaviour)interactable).transform.position;
                    float distance = Vector3.Distance(transform.position, targetPos);

                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closest = interactable;
                    }
                }
            }

            return closest;
        }

        /// <summary>
        /// Get primary interactable based on priority and distance
        /// Higher priority objects take precedence
        /// </summary>
        private IU3DInteractable GetPrimaryInteractable()
        {
            if (nearbyInteractables.Count == 0) return null;

            IU3DInteractable primary = null;
            int highestPriority = int.MinValue;
            float closestDistance = Mathf.Infinity;

            foreach (IU3DInteractable interactable in nearbyInteractables)
            {
                if (interactable.CanInteract())
                {
                    int priority = interactable.GetInteractionPriority();
                    Vector3 targetPos = ((MonoBehaviour)interactable).transform.position;
                    float distance = Vector3.Distance(transform.position, targetPos);

                    // Higher priority wins, or closer distance if same priority
                    if (priority > highestPriority || (priority == highestPriority && distance < closestDistance))
                    {
                        primary = interactable;
                        highestPriority = priority;
                        closestDistance = distance;
                    }
                }
            }

            return primary;
        }

        private void OnDrawGizmosSelected()
        {
            // Draw interaction range
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, interactionRange);

            // Draw connections to nearby interactables
            if (debugMode && nearbyInteractables != null)
            {
                Gizmos.color = Color.yellow;
                foreach (IU3DInteractable interactable in nearbyInteractables)
                {
                    if (interactable != null)
                    {
                        Vector3 targetPos = ((MonoBehaviour)interactable).transform.position;
                        Gizmos.DrawLine(transform.position, targetPos);
                    }
                }

                // Highlight current primary
                if (currentInteractable != null)
                {
                    Gizmos.color = Color.red;
                    Vector3 primaryPos = ((MonoBehaviour)currentInteractable).transform.position;
                    Gizmos.DrawWireSphere(primaryPos, 0.5f);
                }
            }
        }
    }

    /// <summary>
    /// Interface that all interactable objects must implement
    /// This creates a unified system for all interaction types
    /// </summary>
    public interface IU3DInteractable
    {
        /// <summary>
        /// Called when player interacts with this object
        /// </summary>
        void OnInteract();

        /// <summary>
        /// Called when player enters interaction range
        /// </summary>
        void OnPlayerEnterRange();

        /// <summary>
        /// Called when player exits interaction range
        /// </summary>
        void OnPlayerExitRange();

        /// <summary>
        /// Check if this object can currently be interacted with
        /// </summary>
        bool CanInteract();

        /// <summary>
        /// Get interaction priority (higher = more important)
        /// QuestGivers = 100, Regular objects = 50, etc.
        /// </summary>
        int GetInteractionPriority();

        /// <summary>
        /// Get text to show in interaction prompt
        /// </summary>
        string GetInteractionPrompt();
    }

    /// <summary>
    /// Updated QuestGiver that implements the unified interaction interface
    /// RESTORED: Original working behavior where dialog only shows on interaction
    /// </summary>
    public partial class U3DQuestGiver : IU3DInteractable
    {
        // IU3DInteractable implementation - methods have different names than UnityEvents
        public void OnInteract()
        {
            StartInteraction();
        }

        public void OnPlayerEnterRange()
        {
            // RESTORED: Just invoke Creator's custom event, don't auto-show dialog
            // Dialog will be shown when player actually interacts via OnInteract() → StartInteraction()
            OnPlayerEnterRangeEvent?.Invoke();
        }

        public void OnPlayerExitRange()
        {
            // System method - hide dialog canvas
            CloseDialog();

            // Invoke Creator's custom UnityEvent (use field names)  
            OnPlayerExitRangeEvent?.Invoke();
        }

        public bool CanInteract()
        {
            return CanGiveQuest();
        }

        public int GetInteractionPriority()
        {
            return 100; // QuestGivers have high priority
        }

        public string GetInteractionPrompt()
        {
            if (questToGive == null) return "Talk";

            if (questToGive.IsCompleted)
                return "Quest Complete";
            else if (questToGive.IsActive)
                return "Quest Active";
            else
                return "New Quest";
        }
    }

    /// <summary>
    /// Extension methods to help with the LINQ operations
    /// </summary>
    public static class U3DInteractionExtensions
    {
        public static System.Collections.Generic.IEnumerable<T> OfType<T>(this MonoBehaviour[] source)
        {
            foreach (var item in source)
            {
                if (item is T result)
                    yield return result;
            }
        }

        public static T[] ToArray<T>(this System.Collections.Generic.IEnumerable<T> source)
        {
            var list = new System.Collections.Generic.List<T>();
            foreach (var item in source)
                list.Add(item);
            return list.ToArray();
        }
    }
}