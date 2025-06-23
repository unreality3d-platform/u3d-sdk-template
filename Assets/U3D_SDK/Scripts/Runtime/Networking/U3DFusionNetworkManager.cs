using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

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

        [Header("Input System")]
        [SerializeField] private InputActionAsset inputActionAsset;
        [SerializeField] private float mouseSensitivityMultiplier = 1f; // ADDED: Configurable sensitivity

        // Network State
        private NetworkRunner _runner;
        private Dictionary<PlayerRef, NetworkObject> _spawnedPlayers = new Dictionary<PlayerRef, NetworkObject>();
        private bool _isInitialized = false;
        private string _currentSessionName = "";
        private FirebaseIntegration _firebaseIntegration;

        // Input caching fields
        private Vector2 _cachedMovementInput;
        private Vector2 _cachedLookInput;
        private bool _jumpPressed;
        private bool _sprintPressed;
        private bool _crouchPressed;
        private bool _flyPressed;
        private bool _interactPressed;
        private bool _zoomPressed;
        private bool _teleportPressed;

        // Input Actions references
        private InputAction _moveAction;
        private InputAction _lookAction;
        private InputAction _jumpAction;
        private InputAction _sprintAction;
        private InputAction _crouchAction;
        private InputAction _flyAction;
        private InputAction _interactAction;
        private InputAction _zoomAction;
        private InputAction _teleportAction;
        private InputAction _perspectiveSwitchAction;
        private float _perspectiveScrollValue;

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
            SetupInputActions();

            // 🚨 CRITICAL: Ensure Input Actions stay enabled for networking
            ForceEnableInputActions();

            if (autoStartHost)
            {
                _ = StartNetworking("DefaultRoom");
            }
        }

        // 🚨 UPDATE: Modified ForceEnableInputActions
        void ForceEnableInputActions()
        {
            // This method is now less critical since we're using PlayerInput's copy
            // But we'll keep it as a backup

            if (inputActionAsset == null) return;

            var playerInput = FindAnyObjectByType<PlayerInput>();
            if (playerInput != null && playerInput.actions != null)
            {
                var actionMap = playerInput.actions.FindActionMap("Player");
                if (actionMap != null)
                {
                    actionMap.Enable();
                    Debug.Log($"✅ Force enabled PlayerInput's action map. Enabled: {actionMap.enabled}");
                }
            }
            else
            {
                // Fallback to original asset
                inputActionAsset.Enable();
                Debug.Log($"✅ Force enabled original InputActionAsset as fallback");
            }
        }

        void Update()
        {
            // Only cache input if actions are properly set up
            if (_moveAction == null) return;

            // Cache input values - NO sensitivity modifications
            _cachedMovementInput = _moveAction.ReadValue<Vector2>();

            if (_lookAction != null)
            {
                // Use raw Input Actions values - they're already correctly scaled
                _cachedLookInput = _lookAction.ReadValue<Vector2>();
            }

            // Cache button presses
            if (_jumpAction != null && _jumpAction.WasPressedThisFrame())
                _jumpPressed = true;

            if (_sprintAction != null && _sprintAction.WasPressedThisFrame())
                _sprintPressed = true;

            if (_crouchAction != null && _crouchAction.WasPressedThisFrame())
                _crouchPressed = true;

            if (_flyAction != null && _flyAction.WasPressedThisFrame())
                _flyPressed = true;

            if (_interactAction != null && _interactAction.WasPressedThisFrame())
                _interactPressed = true;

            if (_zoomAction != null)
                _zoomPressed = _zoomAction.IsPressed();

            // FIXED: Proper MultiTap detection without spam
            if (_teleportAction != null && _teleportAction.WasPerformedThisFrame())
            {
                _teleportPressed = true;
                Debug.Log("✅ Teleport double-click completed");
            }

            if (_perspectiveSwitchAction != null)
            {
                float scroll = _perspectiveSwitchAction.ReadValue<float>();
                if (Mathf.Abs(scroll) > 0.1f)
                    _perspectiveScrollValue = scroll;
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

        void SetupInputActions()
        {
            // 🚨 CRITICAL FIX: Get the actions from the spawned player's PlayerInput component
            // instead of the original InputActionAsset

            // Wait for player to be spawned first
            if (inputActionAsset == null)
            {
                Debug.LogError("Input Action Asset not assigned in NetworkManager! Please assign U3DInputActions in the inspector.");
                return;
            }

            // Find any PlayerInput component in the scene (from spawned player)
            var playerInput = FindAnyObjectByType<PlayerInput>();

            InputActionAsset actionsToUse;

            if (playerInput != null && playerInput.actions != null)
            {
                // 🚨 USE THE PLAYERINPUT'S PRIVATE COPY, not the original asset
                actionsToUse = playerInput.actions;
                Debug.Log("✅ Using PlayerInput's private copy of actions");
            }
            else
            {
                // Fallback to original asset if no PlayerInput found yet
                actionsToUse = inputActionAsset;
                Debug.Log("⚠️ Using original InputActionAsset as fallback");
            }

            // Get the Player action map from the correct actions instance
            var actionMap = actionsToUse.FindActionMap("Player");
            if (actionMap == null)
            {
                Debug.LogError("'Player' action map not found in Input Actions");
                return;
            }

            // Cache all the input actions from the correct instance
            _moveAction = actionMap.FindAction("Move");
            _lookAction = actionMap.FindAction("Look");
            _jumpAction = actionMap.FindAction("Jump");
            _sprintAction = actionMap.FindAction("Sprint");
            _crouchAction = actionMap.FindAction("Crouch");
            _flyAction = actionMap.FindAction("Fly");
            _interactAction = actionMap.FindAction("Interact");
            _zoomAction = actionMap.FindAction("Zoom");
            _teleportAction = actionMap.FindAction("Teleport");
            _perspectiveSwitchAction = actionMap.FindAction("PerspectiveSwitch");

            // 🚨 CRITICAL: Enable the action map that we're actually using
            actionMap.Enable();

            Debug.Log("✅ Input actions cached and enabled from correct source");
            Debug.Log($"Found actions: Move={_moveAction != null}, Look={_lookAction != null}, Jump={_jumpAction != null}");
            Debug.Log($"Action map enabled: {actionMap.enabled}");
        }

        // 🚨 NEW METHOD: Call this AFTER player spawns to re-setup input actions
        public void RefreshInputActions()
        {
            SetupInputActions();
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

                // ⭐ CRITICAL FIX: Register this component as callback handler
                _runner.AddCallbacks(this);

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

                // ⭐ REMOVE CALLBACKS BEFORE SHUTDOWN
                _runner.RemoveCallbacks(this);

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
            Debug.Log($"=== U3DFusionNetworkManager: Player joined: {player} ===");
            Debug.Log($"Is Local Player: {player == runner.LocalPlayer}");
            Debug.Log($"Player Prefab Valid: {playerPrefab.IsValid}");
            Debug.Log($"Runner Is Server: {runner.IsServer}");
            Debug.Log($"Runner Is Client: {runner.IsClient}");
            Debug.Log($"Runner GameMode: {runner.GameMode}");

            // CRITICAL FIX: In Shared Mode, each client spawns their own player
            // In Server Mode, only server spawns for all players
            bool shouldSpawn = false;

            if (runner.GameMode == GameMode.Shared)
            {
                // In Shared Mode: Each client spawns their own player only
                shouldSpawn = (player == runner.LocalPlayer);
                Debug.Log($"Shared Mode - Local player spawn check: {shouldSpawn}");
            }
            else
            {
                // In Server/Host Mode: Only server spawns for all players
                shouldSpawn = runner.IsServer;
                Debug.Log($"Server Mode - Server spawn check: {shouldSpawn}");
            }

            Debug.Log($"Should Spawn Player: {shouldSpawn}");

            if (shouldSpawn && playerPrefab.IsValid)
            {
                Vector3 spawnPosition = GetSpawnPosition();
                Debug.Log($"Spawning player at position: {spawnPosition}");

                try
                {
                    var playerObject = runner.Spawn(playerPrefab, spawnPosition, Quaternion.identity, player);

                    if (playerObject != null)
                    {
                        _spawnedPlayers[player] = playerObject;
                        Debug.Log($"✅ Player spawned successfully: {playerObject.name}");
                        Debug.Log($"Player GameObject active: {playerObject.gameObject.activeInHierarchy}");
                        Debug.Log($"Player position: {playerObject.transform.position}");

                        // Additional validation
                        var networkObj = playerObject.GetComponent<NetworkObject>();
                        Debug.Log($"NetworkObject valid: {networkObj != null}");
                        if (networkObj != null)
                        {
                            Debug.Log($"NetworkObject InputAuthority: {networkObj.InputAuthority}");
                            Debug.Log($"NetworkObject StateAuthority: {networkObj.StateAuthority}");
                        }
                    }
                    else
                    {
                        Debug.LogError("❌ Player object is null after spawning!");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"❌ Failed to spawn player: {e.Message}");
                    Debug.LogError($"Stack trace: {e.StackTrace}");
                }
            }
            else if (!playerPrefab.IsValid)
            {
                Debug.LogError("❌ Cannot spawn player - Player prefab is not valid!");
                Debug.LogError("Make sure the Player Prefab field is assigned in the U3DFusionNetworkManager inspector!");
            }
            else
            {
                Debug.Log($"Not spawning player - GameMode: {runner.GameMode}, IsServer: {runner.IsServer}, IsLocalPlayer: {player == runner.LocalPlayer}");
            }

            // Update status based on local vs remote player
            if (player == runner.LocalPlayer)
            {
                UpdateStatus($"Joined multiplayer session: {_currentSessionName}");
            }
            else
            {
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

        // ✅ CORRECTED: Proper Fusion 2 callback signatures
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

        // FIXED: Input processing with proper sensitivity and button detection
        public void OnInput(NetworkRunner runner, NetworkInput input)
        {
            var data = new U3DNetworkInputData();

            // Use cached movement input
            data.MovementInput = _cachedMovementInput;

            // FIXED: Use properly scaled look input (Unity Input Actions already provide delta)
            data.LookInput = _cachedLookInput;
            data.PerspectiveScroll = _perspectiveScrollValue;

            // Set button states
            if (_jumpPressed)
                data.Buttons.Set(U3DInputButtons.Jump, true);
            if (_sprintPressed)
                data.Buttons.Set(U3DInputButtons.Sprint, true);
            if (_crouchPressed)
                data.Buttons.Set(U3DInputButtons.Crouch, true);
            if (_flyPressed)
                data.Buttons.Set(U3DInputButtons.Fly, true);
            if (_interactPressed)
                data.Buttons.Set(U3DInputButtons.Interact, true);
            if (_zoomPressed)
                data.Buttons.Set(U3DInputButtons.Zoom, true);
            if (_teleportPressed)
                data.Buttons.Set(U3DInputButtons.Teleport, true);

            // Send input to Fusion
            input.Set(data);

            // Reset one-shot button presses after they've been sent
            _jumpPressed = false;
            _sprintPressed = false;
            _crouchPressed = false;
            _flyPressed = false;
            _interactPressed = false;
            _teleportPressed = false;
            _perspectiveScrollValue = 0f;
            // Note: _zoomPressed is not reset as it's a hold action
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

        // 🚨 UPDATE: Modified OnDestroy to properly disable the asset
        void OnDestroy()
        {
            // Clean disable of input actions when destroying
            if (inputActionAsset != null && inputActionAsset.enabled)
            {
                inputActionAsset.Disable();
                Debug.Log("✅ Disabled InputActionAsset on NetworkManager destroy");
            }

            if (_runner != null)
            {
                _runner.Shutdown();
            }
        }
    }
}