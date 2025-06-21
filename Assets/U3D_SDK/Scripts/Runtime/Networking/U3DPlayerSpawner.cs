using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace U3D.Networking
{
    /// <summary>
    /// Manages player spawn points and spawn logic for multiplayer sessions
    /// Provides balanced spawning with safety checks for WebGL deployment
    /// </summary>
    public class U3DPlayerSpawner : MonoBehaviour
    {
        [Header("Spawn Configuration")]
        [SerializeField] private List<Transform> spawnPoints = new List<Transform>();
        [SerializeField] private bool randomizeSpawnOrder = true;
        [SerializeField] private float spawnRadius = 2f;
        [SerializeField] private float minDistanceBetweenPlayers = 3f;

        [Header("Safety Checks")]
        [SerializeField] private bool enableGroundCheck = true;
        [SerializeField] private float groundCheckDistance = 5f;
        [SerializeField] private LayerMask groundLayerMask = 1; // Default layer
        [SerializeField] private bool enableObstacleCheck = true;
        [SerializeField] private float playerCapsuleHeight = 2f;
        [SerializeField] private float playerCapsuleRadius = 0.5f;

        [Header("Fallback Settings")]
        [SerializeField] private Vector3 defaultSpawnPosition = Vector3.zero;
        [SerializeField] private bool createDefaultSpawnIfEmpty = true;
        [SerializeField] private int maxSpawnAttempts = 10;

        [Header("Visual Debug")]
        [SerializeField] private bool showGizmos = true;
        [SerializeField] private Color spawnPointColor = Color.green;
        [SerializeField] private Color occupiedSpawnColor = Color.red;
        [SerializeField] private Color safeZoneColor = Color.blue;

        // Runtime state
        private Dictionary<Transform, bool> _spawnPointOccupied = new Dictionary<Transform, bool>();
        private List<Vector3> _currentPlayerPositions = new List<Vector3>();
        private int _lastUsedSpawnIndex = -1;

        // Singleton access
        public static U3DPlayerSpawner Instance { get; private set; }

        // Events
        public static event System.Action<Vector3> OnPlayerSpawned;

        void Awake()
        {
            // Singleton pattern
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            InitializeSpawnSystem();
        }

        void Start()
        {
            ValidateSpawnPoints();
        }

        void InitializeSpawnSystem()
        {
            // Auto-find spawn points if none assigned
            if (spawnPoints.Count == 0)
            {
                FindSpawnPointsInScene();
            }

            // Create default spawn point if still empty
            if (spawnPoints.Count == 0 && createDefaultSpawnIfEmpty)
            {
                CreateDefaultSpawnPoint();
            }

            // Initialize spawn point tracking
            foreach (var spawnPoint in spawnPoints)
            {
                _spawnPointOccupied[spawnPoint] = false;
            }

            Debug.Log($"U3D Player Spawner initialized with {spawnPoints.Count} spawn points");
        }

        void FindSpawnPointsInScene()
        {
            // Look for objects tagged as "SpawnPoint"
            var foundSpawnPoints = GameObject.FindGameObjectsWithTag("SpawnPoint");

            foreach (var spawnPoint in foundSpawnPoints)
            {
                if (!spawnPoints.Contains(spawnPoint.transform))
                {
                    spawnPoints.Add(spawnPoint.transform);
                }
            }

            // Look for objects with "Spawn" in their name
            var allObjects = FindObjectsByType<Transform>(FindObjectsSortMode.None);
            foreach (var obj in allObjects)
            {
                if (obj.name.ToLower().Contains("spawn") && !spawnPoints.Contains(obj))
                {
                    spawnPoints.Add(obj);
                }
            }
        }

        void CreateDefaultSpawnPoint()
        {
            var defaultSpawnObject = new GameObject("Default Spawn Point");
            defaultSpawnObject.transform.position = defaultSpawnPosition;
            defaultSpawnObject.transform.SetParent(transform);
            defaultSpawnObject.tag = "SpawnPoint";

            spawnPoints.Add(defaultSpawnObject.transform);

            Debug.Log("Created default spawn point at origin");
        }

        void ValidateSpawnPoints()
        {
            List<Transform> invalidSpawnPoints = new List<Transform>();

            foreach (var spawnPoint in spawnPoints)
            {
                if (spawnPoint == null)
                {
                    invalidSpawnPoints.Add(spawnPoint);
                    continue;
                }

                if (!IsSpawnPointSafe(spawnPoint.position))
                {
                    Debug.LogWarning($"Spawn point at {spawnPoint.position} may be unsafe - no ground detected or obstacles present");
                }
            }

            // Remove invalid spawn points
            foreach (var invalidPoint in invalidSpawnPoints)
            {
                spawnPoints.Remove(invalidPoint);
            }

            Debug.Log($"Validated {spawnPoints.Count} spawn points");
        }

        /// <summary>
        /// Get the best spawn position for a new player
        /// </summary>
        public Vector3 GetSpawnPosition()
        {
            UpdatePlayerPositions();

            for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
            {
                Vector3 candidatePosition = GetNextSpawnCandidate();

                if (IsSpawnPositionValid(candidatePosition))
                {
                    OnPlayerSpawned?.Invoke(candidatePosition);
                    return candidatePosition;
                }
            }

            // Fallback to default position
            Debug.LogWarning("Failed to find safe spawn position, using default");
            return defaultSpawnPosition;
        }

        Vector3 GetNextSpawnCandidate()
        {
            if (spawnPoints.Count == 0)
            {
                return defaultSpawnPosition;
            }

            Transform selectedSpawnPoint;

            if (randomizeSpawnOrder)
            {
                // Find least crowded spawn points
                var availableSpawnPoints = GetAvailableSpawnPoints();

                if (availableSpawnPoints.Count > 0)
                {
                    selectedSpawnPoint = availableSpawnPoints[Random.Range(0, availableSpawnPoints.Count)];
                }
                else
                {
                    // All spawn points occupied, use random one
                    selectedSpawnPoint = spawnPoints[Random.Range(0, spawnPoints.Count)];
                }
            }
            else
            {
                // Use spawn points in order
                _lastUsedSpawnIndex = (_lastUsedSpawnIndex + 1) % spawnPoints.Count;
                selectedSpawnPoint = spawnPoints[_lastUsedSpawnIndex];
            }

            // Add random offset within spawn radius
            Vector3 spawnPosition = selectedSpawnPoint.position;

            if (spawnRadius > 0)
            {
                Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;
                spawnPosition += new Vector3(randomCircle.x, 0, randomCircle.y);
            }

            return spawnPosition;
        }

        List<Transform> GetAvailableSpawnPoints()
        {
            var available = new List<Transform>();

            foreach (var spawnPoint in spawnPoints)
            {
                if (!IsSpawnPointCrowded(spawnPoint.position))
                {
                    available.Add(spawnPoint);
                }
            }

            return available;
        }

        bool IsSpawnPointCrowded(Vector3 position)
        {
            foreach (var playerPos in _currentPlayerPositions)
            {
                if (Vector3.Distance(position, playerPos) < minDistanceBetweenPlayers)
                {
                    return true;
                }
            }
            return false;
        }

        bool IsSpawnPositionValid(Vector3 position)
        {
            // Check ground
            if (enableGroundCheck && !HasGroundBelow(position))
            {
                return false;
            }

            // Check for obstacles
            if (enableObstacleCheck && HasObstacles(position))
            {
                return false;
            }

            // Check distance from other players
            if (IsSpawnPointCrowded(position))
            {
                return false;
            }

            return true;
        }

        bool IsSpawnPointSafe(Vector3 position)
        {
            // Basic safety check for spawn point validation
            if (enableGroundCheck && !HasGroundBelow(position))
            {
                return false;
            }

            if (enableObstacleCheck && HasObstacles(position))
            {
                return false;
            }

            return true;
        }

        bool HasGroundBelow(Vector3 position)
        {
            Vector3 rayStart = position + Vector3.up * 0.5f;
            return Physics.Raycast(rayStart, Vector3.down, groundCheckDistance, groundLayerMask);
        }

        bool HasObstacles(Vector3 position)
        {
            // Check for overlapping colliders using capsule
            Vector3 capsuleTop = position + Vector3.up * (playerCapsuleHeight - playerCapsuleRadius);
            Vector3 capsuleBottom = position + Vector3.up * playerCapsuleRadius;

            return Physics.CheckCapsule(capsuleBottom, capsuleTop, playerCapsuleRadius);
        }

        void UpdatePlayerPositions()
        {
            _currentPlayerPositions.Clear();

            // Find all networked players
            var networkedPlayers = FindObjectsByType<U3DNetworkedPlayer>(FindObjectsSortMode.None);

            foreach (var player in networkedPlayers)
            {
                _currentPlayerPositions.Add(player.transform.position);
            }
        }

        /// <summary>
        /// Add a custom spawn point at runtime
        /// </summary>
        public void AddSpawnPoint(Vector3 position, string name = "Runtime Spawn Point")
        {
            var spawnObject = new GameObject(name);
            spawnObject.transform.position = position;
            spawnObject.transform.SetParent(transform);
            spawnObject.tag = "SpawnPoint";

            spawnPoints.Add(spawnObject.transform);
            _spawnPointOccupied[spawnObject.transform] = false;

            Debug.Log($"Added spawn point: {name} at {position}");
        }

        /// <summary>
        /// Remove a spawn point
        /// </summary>
        public void RemoveSpawnPoint(Transform spawnPoint)
        {
            if (spawnPoints.Contains(spawnPoint))
            {
                spawnPoints.Remove(spawnPoint);
                _spawnPointOccupied.Remove(spawnPoint);

                if (spawnPoint != null)
                {
                    Destroy(spawnPoint.gameObject);
                }
            }
        }

        /// <summary>
        /// Get spawn point closest to a specific position
        /// </summary>
        public Vector3 GetSpawnPositionNear(Vector3 targetPosition)
        {
            if (spawnPoints.Count == 0)
            {
                return defaultSpawnPosition;
            }

            // Find closest spawn point
            Transform closestSpawnPoint = spawnPoints
                .OrderBy(sp => Vector3.Distance(sp.position, targetPosition))
                .First();

            Vector3 spawnPosition = closestSpawnPoint.position;

            // Add small random offset
            if (spawnRadius > 0)
            {
                Vector2 randomCircle = Random.insideUnitCircle * (spawnRadius * 0.5f);
                spawnPosition += new Vector3(randomCircle.x, 0, randomCircle.y);
            }

            return spawnPosition;
        }

        /// <summary>
        /// Get debug information about spawn system
        /// </summary>
        public string GetDebugInfo()
        {
            return $"Spawn Points: {spawnPoints.Count}, " +
                   $"Active Players: {_currentPlayerPositions.Count}, " +
                   $"Last Used Index: {_lastUsedSpawnIndex}";
        }

        // Debug visualization
        void OnDrawGizmos()
        {
            if (!showGizmos) return;

            // Draw spawn points
            for (int i = 0; i < spawnPoints.Count; i++)
            {
                if (spawnPoints[i] == null) continue;

                Vector3 spawnPos = spawnPoints[i].position;

                // Spawn point indicator
                bool isOccupied = _spawnPointOccupied.ContainsKey(spawnPoints[i]) && _spawnPointOccupied[spawnPoints[i]];
                Gizmos.color = isOccupied ? occupiedSpawnColor : spawnPointColor;
                Gizmos.DrawWireSphere(spawnPos, 0.5f);

                // Spawn radius
                Gizmos.color = safeZoneColor;
                Gizmos.DrawWireSphere(spawnPos, spawnRadius);

                // Player capsule preview
                Gizmos.color = Color.yellow;
                Vector3 capsuleTop = spawnPos + Vector3.up * (playerCapsuleHeight - playerCapsuleRadius);
                Vector3 capsuleBottom = spawnPos + Vector3.up * playerCapsuleRadius;
                Gizmos.DrawWireSphere(capsuleTop, playerCapsuleRadius);
                Gizmos.DrawWireSphere(capsuleBottom, playerCapsuleRadius);
                Gizmos.DrawLine(capsuleTop, capsuleBottom);

                // Ground check ray
                if (enableGroundCheck)
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawRay(spawnPos + Vector3.up * 0.5f, Vector3.down * groundCheckDistance);
                }

                // Spawn point number
                UnityEditor.Handles.Label(spawnPos + Vector3.up * 2f, $"Spawn {i}");
            }

            // Draw minimum distance circles around current players
            Gizmos.color = Color.red;
            foreach (var playerPos in _currentPlayerPositions)
            {
                Gizmos.DrawWireSphere(playerPos, minDistanceBetweenPlayers);
            }
        }
    }
}