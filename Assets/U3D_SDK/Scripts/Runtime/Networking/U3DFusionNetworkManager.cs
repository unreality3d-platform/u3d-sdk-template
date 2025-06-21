using UnityEngine;
using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace U3D.Networking
{
    /// <summary>
    /// Core network manager for Photon Fusion integration with Firebase authentication
    /// Manages network lifecycle and player spawning for WebGL deployment
    /// </summary>
    public class U3DFusionNetworkManager : MonoBehaviour, INetworkRunnerCallbacks
    {
        [Header("Network Configuration")]
        [SerializeField] private NetworkPrefabRef playerPrefab;
        [SerializeField] private int maxPlayers = 10;
        [SerializeField] private bool autoStartHost = false;
        [SerializeField] private GameMode gameMode = GameMode.Shared;

        [Header("Spawn Configuration")]
        [SerializeField] private Transform[] spawnPoints;
        [SerializeField] private bool randomizeSpawnPoints = true;
        [SerializeField] private float spawnRadius = 2f;

        [Header("WebGL Optimization")]
        [SerializeField] private bool useClientPrediction = true;
        [SerializeField] private bool enableLagCompensation = true;
        [SerializeField] private int sendRate = 20; // Reduced for WebGL
        [SerializeField] private int simulationTickRate = 60;

        // Network State
        private NetworkRunner _runner;
        private Dictionary<PlayerRef, NetworkObject> _spawnedPlayers = new Dictionary<PlayerRef, NetworkObject>();
        private bool _isInitialized = false;
        private string _currentSessionName = "";
        private FirebaseIntegration _firebaseIntegration;

        // Events for UI integration
        public static event Action<bool> OnNetworkStatusChanged;
        public static event Action<PlayerRef> OnPlayerJoinedEvent;
        public static event Action<PlayerRef> OnPlayerLeftEvent;
        public static event Action<int> OnPlayerCountChanged;

        // Singleton access
        public static U3DFusionNetworkManager Instance { get; private set; }

        // Public Properties
        public bool IsConnected => _runner != null && _runner.IsClient;
        public bool IsHost => _runner != null && _runner.IsServer;
        public int PlayerCount => _spawnedPlayers.Count;
        public NetworkRunner Runner => _runner;

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

            _firebaseIntegration = FindAnyObjectByType<FirebaseIntegration>();
        }

        void Start()
        {
            InitializeNetworking();

            if (autoStartHost)
            {
                _ = StartNetworking("DefaultRoom");
            }
        }

        void InitializeNetworking()
        {
            if (_isInitialized) return;

            // WebGL-specific Fusion configuration
            NetworkProjectConfig.Global.PeerMode = NetworkProjectConfig.PeerModes.Single;

            _isInitialized = true;
            Debug.Log("U3D Fusion Network Manager initialized for WebGL");
        }

        /// <summary>
        /// Start networking with session name from Firebase token
        /// Called by FirebaseIntegration after receiving Photon token
        /// </summary>
        public async Task<bool> StartNetworking(string sessionName, string photonAppId = "")
        {
            if (_runner != null)
            {
                Debug.LogWarning("Network already running, shutting down first");
                await StopNetworking();
            }

            try
            {
                _currentSessionName = sessionName;

                // Create NetworkRunner for this session
                var runnerObject = new GameObject($"NetworkRunner_{sessionName}");
                runnerObject.transform.SetParent(transform);

                _runner = runnerObject.AddComponent<NetworkRunner>();
                _runner.ProvideInput = true;

                // WebGL-optimized configuration
                var args = new StartGameArgs()
                {
                    GameMode = gameMode,
                    SessionName = sessionName,
                    Scene = SceneRef.FromIndex(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex),
                    SceneManager = runnerObject.AddComponent<NetworkSceneManagerDefault>(),
                    PlayerCount = maxPlayers
                };

                UpdateStatus($"Connecting to session: {sessionName}");

                var result = await _runner.StartGame(args);

                if (result.Ok)
                {
                    UpdateStatus($"Successfully connected to: {sessionName}");
                    OnNetworkStatusChanged?.Invoke(true);
                    return true;
                }
                else
                {
                    UpdateStatus($"Connection failed: {result.ShutdownReason}");
                    OnNetworkStatusChanged?.Invoke(false);
                    return false;
                }
            }
            catch (Exception e)
            {
                UpdateStatus($"Network start error: {e.Message}");
                OnNetworkStatusChanged?.Invoke(false);
                return false;
            }
        }

        /// <summary>
        /// Stop networking and cleanup
        /// </summary>
        public async Task StopNetworking()
        {
            if (_runner != null)
            {
                UpdateStatus("Disconnecting from multiplayer...");

                await _runner.Shutdown();

                if (_runner.gameObject != null)
                {
                    Destroy(_runner.gameObject);
                }

                _runner = null;
                _spawnedPlayers.Clear();

                UpdateStatus("Disconnected from multiplayer");
                OnNetworkStatusChanged?.Invoke(false);
                OnPlayerCountChanged?.Invoke(0);
            }
        }

        /// <summary>
        /// Get appropriate spawn position for new player
        /// </summary>
        Vector3 GetSpawnPosition()
        {
            if (spawnPoints == null || spawnPoints.Length == 0)
            {
                // Default spawn at origin with random offset
                Vector3 randomOffset = UnityEngine.Random.insideUnitSphere * spawnRadius;
                randomOffset.y = 0; // Keep players on ground level
                return randomOffset;
            }

            if (randomizeSpawnPoints)
            {
                var spawnPoint = spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)];
                Vector3 randomOffset = UnityEngine.Random.insideUnitSphere * spawnRadius;
                randomOffset.y = 0;
                return spawnPoint.position + randomOffset;
            }
            else
            {
                // Use spawn points in order, cycling through them
                int spawnIndex = _spawnedPlayers.Count % spawnPoints.Length;
                return spawnPoints[spawnIndex].position;
            }
        }

        /// <summary>
        /// Update status text through FirebaseIntegration
        /// </summary>
        void UpdateStatus(string message)
        {
            Debug.Log($"U3D Network Manager: {message}");

            if (_firebaseIntegration != null)
            {
                _firebaseIntegration.UpdateStatus(message);
            }
        }

        // ========== FUSION CALLBACKS ==========

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            Debug.Log($"Player joined: {player}");

            if (player == runner.LocalPlayer)
            {
                // Local player joined
                UpdateStatus($"Joined multiplayer session: {_currentSessionName}");

                // Spawn local player
                if (playerPrefab.IsValid)
                {
                    Vector3 spawnPosition = GetSpawnPosition();
                    var playerObject = runner.Spawn(playerPrefab, spawnPosition, Quaternion.identity, player);
                    _spawnedPlayers[player] = playerObject;
                }
            }
            else
            {
                // Remote player joined
                UpdateStatus($"Player {player} joined the session");
            }

            OnPlayerJoinedEvent?.Invoke(player);
            OnPlayerCountChanged?.Invoke(_spawnedPlayers.Count);
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            Debug.Log($"Player left: {player}");

            if (_spawnedPlayers.TryGetValue(player, out NetworkObject playerObject))
            {
                if (playerObject != null)
                {
                    runner.Despawn(playerObject);
                }
                _spawnedPlayers.Remove(player);
            }

            UpdateStatus($"Player {player} left the session");
            OnPlayerLeftEvent?.Invoke(player);
            OnPlayerCountChanged?.Invoke(_spawnedPlayers.Count);
        }

        public void OnConnectedToServer(NetworkRunner runner)
        {
            Debug.Log("Connected to Photon server");
            UpdateStatus("Connected to Photon server");
        }

        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        {
            Debug.Log($"Disconnected from server: {reason}");
            UpdateStatus($"Disconnected: {reason}");
            OnNetworkStatusChanged?.Invoke(false);
        }

        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
        {
            Debug.Log($"Connection failed: {reason}");
            UpdateStatus($"Connection failed: {reason}");
            OnNetworkStatusChanged?.Invoke(false);
        }

        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            Debug.Log($"Network shutdown: {shutdownReason}");
            UpdateStatus($"Network shutdown: {shutdownReason}");

            _spawnedPlayers.Clear();
            OnNetworkStatusChanged?.Invoke(false);
            OnPlayerCountChanged?.Invoke(0);
        }

        // Required callbacks for Fusion 2
        public void OnInput(NetworkRunner runner, NetworkInput input)
        {
            // Input handling will be implemented in networked player controller
        }

        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        public void OnSessionListUpdated(NetworkRunner runner, List<Fusion.SessionInfo> sessionList) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey reliableKey, ArraySegment<byte> data) { }
        public void OnSceneLoadDone(NetworkRunner runner) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey reliableKey, float progress) { }

        void OnDestroy()
        {
            if (_runner != null)
            {
                _runner.Shutdown();
            }
        }
    }
}