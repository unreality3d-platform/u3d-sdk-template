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
    /// CORRECTED: Unity 6 + Fusion 2 Network Manager with proper Input System integration
    /// Key Fix: Uses PlayerInput for device pairing but polls actions directly for Fusion input
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
        [SerializeField] private int sendRate = 20;
        [SerializeField] private int simulationTickRate = 60;

        [Header("Input System Integration")]
        [SerializeField] private InputActionAsset inputActionAsset;

        // Network State
        private NetworkRunner _runner;
        private Dictionary<PlayerRef, NetworkObject> _spawnedPlayers = new Dictionary<PlayerRef, NetworkObject>();
        private bool _isInitialized = false;
        private string _currentSessionName = "";
        private FirebaseIntegration _firebaseIntegration;

        // CORRECTED: Input handling using direct Action polling
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

        // Input state caching
        private Vector2 _cachedMovementInput;
        private Vector2 _cachedLookInput;
        private bool _jumpPressed;
        private bool _sprintPressed;
        private bool _crouchPressed;
        private bool _flyPressed;
        private bool _interactPressed;
        private bool _zoomPressed;
        private bool _teleportPressed;
        private float _perspectiveScrollValue;
        private float _lastTeleportClickTime = 0f;

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
        }

        void Start()
        {
            InitializeNetworking();
            SetupInputActions();

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

        // CORRECTED: Setup input actions properly for Unity 6 + Fusion 2
        void SetupInputActions()
        {
            if (inputActionAsset == null)
            {
                Debug.LogError("Input Action Asset not assigned! Please assign U3DInputActions in the inspector.");
                return;
            }

            // Get the Player action map
            var actionMap = inputActionAsset.FindActionMap("Player");
            if (actionMap == null)
            {
                Debug.LogError("'Player' action map not found in Input Actions");
                return;
            }

            // Cache all input actions directly from the asset
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

            // CRITICAL: Enable the action map for input polling
            actionMap.Enable();

            Debug.Log("✅ Input actions setup complete");
            Debug.Log($"Action map enabled: {actionMap.enabled}");
            Debug.Log($"Found actions: Move={_moveAction != null}, Look={_lookAction != null}, Jump={_jumpAction != null}");
        }

        void Update()
        {
            // CORRECTED: Poll input actions directly for Fusion
            if (_moveAction == null) return;

            // Check if we should process input (not escaped from WebGL)
            var cursorManager = FindAnyObjectByType<U3DWebGLCursorManager>();
            bool shouldProcessInput = cursorManager == null || cursorManager.ShouldProcessGameInput();

            if (!shouldProcessInput) return;

            // Cache input values for Fusion's OnInput callback
            _cachedMovementInput = _moveAction.ReadValue<Vector2>();

            if (_lookAction != null)
                _cachedLookInput = _lookAction.ReadValue<Vector2>();

            // Cache button states using proper Input System polling
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

            // FIXED: Simple double-click detection that works in WebGL (no MultiTap needed)
            if (_teleportAction != null && _teleportAction.WasPressedThisFrame())
            {
                float currentTime = Time.time;
                if (currentTime - _lastTeleportClickTime < 0.5f) // Within 0.5 seconds
                {
                    _teleportPressed = true;
                    Debug.Log("✅ Double-click teleport detected");
                }
                _lastTeleportClickTime = currentTime;
            }

            if (_zoomAction != null)
                _zoomPressed = _zoomAction.IsPressed();

            if (_perspectiveSwitchAction != null)
            {
                float scroll = _perspectiveSwitchAction.ReadValue<float>();
                if (Mathf.Abs(scroll) > 0.1f)
                    _perspectiveScrollValue = scroll;
            }
        }

        /// <summary>
        /// Start networking with session name from Firebase token
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

                // Register this component as callback handler
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
                Vector3 randomOffset = UnityEngine.Random.insideUnitSphere * spawnRadius;
                randomOffset.y = 0;
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
                int spawnIndex = _spawnedPlayers.Count % spawnPoints.Length;
                return spawnPoints[spawnIndex].position;
            }
        }

        void UpdateStatus(string message)
        {
            Debug.Log($"U3D Network Manager: {message}");
        }

        // ========== FUSION CALLBACKS ==========

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            Debug.Log($"=== U3DFusionNetworkManager: Player joined: {player} ===");

            bool shouldSpawn = false;

            if (runner.GameMode == GameMode.Shared)
            {
                shouldSpawn = (player == runner.LocalPlayer);
            }
            else
            {
                shouldSpawn = runner.IsServer;
            }

            if (shouldSpawn && playerPrefab.IsValid)
            {
                Vector3 spawnPosition = GetSpawnPosition();

                try
                {
                    var playerObject = runner.Spawn(playerPrefab, spawnPosition, Quaternion.identity, player);

                    if (playerObject != null)
                    {
                        _spawnedPlayers[player] = playerObject;
                        Debug.Log($"✅ Player spawned successfully: {playerObject.name}");

                        // IMPORTANT: Refresh input setup for the spawned player
                        var playerController = playerObject.GetComponent<U3DPlayerController>();
                        if (playerController != null)
                        {
                            playerController.RefreshInputActionsFromNetworkManager(this);
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
                }
            }

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

        // FIXED: Correct Fusion 2 callback signatures
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

        // CORRECTED: Fusion input processing using cached values
        public void OnInput(NetworkRunner runner, NetworkInput input)
        {
            var data = new U3DNetworkInputData();

            // Use cached input values from Update()
            data.MovementInput = _cachedMovementInput;
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

            // Reset one-shot button presses
            _jumpPressed = false;
            _sprintPressed = false;
            _crouchPressed = false;
            _flyPressed = false;
            _interactPressed = false;
            _teleportPressed = false;
            _perspectiveScrollValue = 0f;
        }

        // Get input actions for PlayerController integration
        public InputAction GetMoveAction() => _moveAction;
        public InputAction GetLookAction() => _lookAction;
        public InputAction GetJumpAction() => _jumpAction;
        public InputAction GetSprintAction() => _sprintAction;
        public InputAction GetCrouchAction() => _crouchAction;
        public InputAction GetFlyAction() => _flyAction;
        public InputAction GetInteractAction() => _interactAction;
        public InputAction GetZoomAction() => _zoomAction;
        public InputAction GetTeleportAction() => _teleportAction;
        public InputAction GetPerspectiveSwitchAction() => _perspectiveSwitchAction;

        // Standard Fusion callbacks
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
            // Clean shutdown
            if (_runner != null)
            {
                _runner.Shutdown();
            }

            // Disable input actions
            if (inputActionAsset != null)
            {
                var actionMap = inputActionAsset.FindActionMap("Player");
                if (actionMap != null && actionMap.enabled)
                {
                    actionMap.Disable();
                }
            }
        }
    }
}