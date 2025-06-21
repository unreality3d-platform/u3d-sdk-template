using UnityEngine;
using UnityEngine.UI;
using System.Runtime.InteropServices;
using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using U3D.Networking;

public class FirebaseIntegration : MonoBehaviour, INetworkRunnerCallbacks
{
    [Header("UI References")]
    public Button testButton;
    public Button paymentButton;
    public Button joinMultiplayerButton;
    public Button createRoomButton;
    public TMPro.TextMeshProUGUI statusText;
    public TMPro.TMP_InputField roomNameInput;

    [Header("Content Settings")]
    public string contentId = "test-area-1";
    public float contentPrice = 15.99f;

    [Header("Multiplayer Settings")]
    public NetworkPrefabRef playerPrefab;
    public int maxPlayers = 10;

    [Header("Network Manager Integration")]
    public U3DFusionNetworkManager networkManager;
    public U3DPlayerSpawner playerSpawner;

    // PayPal Integration (Existing)
    [DllImport("__Internal")]
    private static extern void UnityCallTestFunction();

    [DllImport("__Internal")]
    private static extern void UnityCheckContentAccess(string contentId);

    [DllImport("__Internal")]
    private static extern void UnityRequestPayment(string contentId, string price);

    // Photon Fusion Integration
    [DllImport("__Internal")]
    private static extern void UnityGetPhotonToken(string roomName, string contentId);

    [DllImport("__Internal")]
    private static extern void UnityCreateMultiplayerSession(string contentId, string sessionName, string maxPlayers);

    [DllImport("__Internal")]
    private static extern void UnityJoinMultiplayerSession(string roomName);

    // Network State
    private NetworkRunner _runner;
    private bool _isConnecting = false;
    private string _pendingRoomName = "";
    private string _photonAppId = "a3df46ef-b10a-4954-8526-7a9fdd553543";
    private UserInfo _currentUserInfo;

    // User data structure
    [System.Serializable]
    public class UserInfo
    {
        public string userId;
        public string displayName;
        public string userType;
        public bool paypalConnected;
        public string creatorUsername;
    }

    void Start()
    {
        // Existing PayPal button setup
        if (testButton != null)
            testButton.onClick.AddListener(TestFirebaseConnection);

        if (paymentButton != null)
            paymentButton.onClick.AddListener(RequestPayment);

        // Multiplayer button setup
        if (joinMultiplayerButton != null)
            joinMultiplayerButton.onClick.AddListener(JoinMultiplayer);

        if (createRoomButton != null)
            createRoomButton.onClick.AddListener(CreateRoom);

        // Initialize network components
        InitializeNetworkComponents();

        // Subscribe to network events
        SubscribeToNetworkEvents();

        CheckContentAccess();
    }

    void InitializeNetworkComponents()
    {
        // Find network manager if not assigned
        if (networkManager == null)
        {
            networkManager = FindAnyObjectByType<U3DFusionNetworkManager>();
        }

        // Find player spawner if not assigned
        if (playerSpawner == null)
        {
            playerSpawner = FindAnyObjectByType<U3DPlayerSpawner>();
        }

        // Create network manager if none exists
        if (networkManager == null)
        {
            var networkManagerObject = new GameObject("U3D Network Manager");
            networkManager = networkManagerObject.AddComponent<U3DFusionNetworkManager>();
            Debug.Log("Created U3D Network Manager automatically");
        }

        // Create player spawner if none exists
        if (playerSpawner == null)
        {
            var spawnerObject = new GameObject("U3D Player Spawner");
            playerSpawner = spawnerObject.AddComponent<U3DPlayerSpawner>();
            Debug.Log("Created U3D Player Spawner automatically");
        }
    }

    void SubscribeToNetworkEvents()
    {
        // Subscribe to network manager events with corrected event names
        U3DFusionNetworkManager.OnNetworkStatusChanged += HandleNetworkStatusChanged;
        U3DFusionNetworkManager.OnPlayerJoinedEvent += HandlePlayerJoined;
        U3DFusionNetworkManager.OnPlayerLeftEvent += HandlePlayerLeft;
        U3DFusionNetworkManager.OnPlayerCountChanged += HandlePlayerCountChanged;
    }

    void OnDestroy()
    {
        // Unsubscribe from events with corrected event names
        U3DFusionNetworkManager.OnNetworkStatusChanged -= HandleNetworkStatusChanged;
        U3DFusionNetworkManager.OnPlayerJoinedEvent -= HandlePlayerJoined;
        U3DFusionNetworkManager.OnPlayerLeftEvent -= HandlePlayerLeft;
        U3DFusionNetworkManager.OnPlayerCountChanged -= HandlePlayerCountChanged;

        // Cleanup network runner
        if (_runner != null)
        {
            _runner.Shutdown();
        }
    }

    // ========== EXISTING PAYPAL FUNCTIONS (UNCHANGED) ==========

    public void TestFirebaseConnection()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        UpdateStatus("Testing Firebase connection...");
        try
        {
            UnityCallTestFunction();
        }
        catch (System.Exception e)
        {
            UpdateStatus("Firebase function not available: " + e.Message);
        }
#else
        UpdateStatus("Firebase testing requires WebGL build");
#endif
    }

    public void CheckContentAccess()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        UpdateStatus("Checking content access...");
        try
        {
            UnityCheckContentAccess(contentId);
        }
        catch (System.Exception e)
        {
            UpdateStatus("Access check failed: " + e.Message);
        }
#else
        UpdateStatus("Ready for WebGL build - Firebase integration active");
#endif
    }

    public void RequestPayment()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        UpdateStatus("Processing payment request...");
        try
        {
            UnityRequestPayment(contentId, contentPrice.ToString());
        }
        catch (System.Exception e)
        {
            UpdateStatus("Payment system not available: " + e.Message);
        }
#else
        UpdateStatus("PayPal integration requires WebGL build");
#endif
    }

    // ========== MULTIPLAYER FUNCTIONS ==========

    public void JoinMultiplayer()
    {
        if (_isConnecting)
        {
            UpdateStatus("Already connecting...");
            return;
        }

        string roomName = roomNameInput != null ? roomNameInput.text : "DefaultRoom";
        if (string.IsNullOrEmpty(roomName))
        {
            UpdateStatus("Please enter a room name");
            return;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        UpdateStatus("Requesting multiplayer access...");
        _pendingRoomName = roomName;
        try
        {
            UnityGetPhotonToken(roomName, contentId);
        }
        catch (System.Exception e)
        {
            UpdateStatus("Multiplayer system not available: " + e.Message);
        }
#else
        // Editor testing - simulate token reception
        UpdateStatus("Editor Mode - Simulating Photon connection...");
        _pendingRoomName = roomName;
        var mockToken = new PhotonTokenInfo
        {
            appId = _photonAppId,
            region = "auto",
            maxPlayers = maxPlayers
        };
        StartNetworkingWithToken(mockToken);
#endif
    }

    public void CreateRoom()
    {
        string sessionName = roomNameInput != null ? roomNameInput.text : "DefaultRoom";
        if (string.IsNullOrEmpty(sessionName))
        {
            UpdateStatus("Please enter a session name");
            return;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        UpdateStatus("Creating multiplayer session...");
        try
        {
            UnityCreateMultiplayerSession(contentId, sessionName, maxPlayers.ToString());
        }
        catch (System.Exception e)
        {
            UpdateStatus("Session creation failed: " + e.Message);
        }
#else
        // Editor testing - simulate session creation
        UpdateStatus("Editor Mode - Simulating session creation...");
        var mockSession = new SessionInfo
        {
            sessionId = $"test_{sessionName}_{System.DateTime.Now.Ticks}",
            roomName = sessionName,
            maxPlayers = maxPlayers,
            status = "created"
        };
        OnSessionCreated(JsonUtility.ToJson(mockSession));
#endif
    }

    // ========== FIREBASE CALLBACKS ==========

    public void UpdateStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
        Debug.Log($"Firebase Integration: {message}");
    }

    // Existing PayPal callbacks
    public void OnTestComplete(string result)
    {
        UpdateStatus($"Test Result: {result}");
    }

    public void OnAccessCheckComplete(string hasAccess)
    {
        if (hasAccess == "true")
        {
            UpdateStatus("Access granted - Welcome!");
        }
        else
        {
            UpdateStatus("Payment required for access");
        }
    }

    public void OnPaymentComplete(string success)
    {
        if (success == "true")
        {
            UpdateStatus("Payment successful - Access granted!");
            CheckContentAccess();
        }
        else
        {
            UpdateStatus("Payment failed - Please try again");
        }
    }

    // Photon callbacks
    public void OnPhotonTokenReceived(string tokenData)
    {
        try
        {
            var tokenInfo = JsonUtility.FromJson<PhotonTokenInfo>(tokenData);
            _photonAppId = tokenInfo.appId;

            UpdateStatus("Token received, connecting to Photon...");
            StartNetworkingWithToken(tokenInfo);
        }
        catch (System.Exception e)
        {
            UpdateStatus("Token parsing failed: " + e.Message);
        }
    }

    public void OnSessionCreated(string sessionData)
    {
        try
        {
            var sessionInfo = JsonUtility.FromJson<SessionInfo>(sessionData);
            UpdateStatus($"Session created: {sessionInfo.roomName}");

            // Auto-join the created session
            _pendingRoomName = sessionInfo.roomName;

#if UNITY_WEBGL && !UNITY_EDITOR
            UnityGetPhotonToken(sessionInfo.roomName, contentId);
#else
            // Editor mode - directly start networking
            var mockToken = new PhotonTokenInfo
            {
                appId = _photonAppId,
                region = "auto",
                maxPlayers = sessionInfo.maxPlayers
            };
            StartNetworkingWithToken(mockToken);
#endif
        }
        catch (System.Exception e)
        {
            UpdateStatus("Session creation response error: " + e.Message);
        }
    }

    public void OnSessionJoinResponse(string responseData)
    {
        UpdateStatus($"Session join response: {responseData}");
    }

    public void OnUserProfileReceived(string userDataJson)
    {
        try
        {
            _currentUserInfo = JsonUtility.FromJson<UserInfo>(userDataJson);
            UpdateStatus($"Welcome, {_currentUserInfo.displayName}!");

            // Update networked player with user info when spawned
            UpdateNetworkedPlayerInfo();
        }
        catch (System.Exception e)
        {
            UpdateStatus("User profile parsing failed: " + e.Message);
        }
    }

    // ========== NETWORKING INTEGRATION ==========

    async void StartNetworkingWithToken(PhotonTokenInfo tokenInfo)
    {
        if (networkManager == null)
        {
            UpdateStatus("Network manager not found!");
            return;
        }

        _isConnecting = true;

        try
        {
            bool success = await networkManager.StartNetworking(_pendingRoomName, tokenInfo.appId);

            if (success)
            {
                UpdateStatus($"Connected to multiplayer: {_pendingRoomName}");
            }
            else
            {
                UpdateStatus("Failed to connect to multiplayer");
            }
        }
        catch (System.Exception e)
        {
            UpdateStatus($"Networking error: {e.Message}");
        }
        finally
        {
            _isConnecting = false;
        }
    }

    void UpdateNetworkedPlayerInfo()
    {
        if (_currentUserInfo == null) return;

        // Find local networked player and update info
        var networkedPlayers = FindObjectsByType<U3DNetworkedPlayer>(FindObjectsSortMode.None);

        foreach (var player in networkedPlayers)
        {
            if (player.Object != null && player.Object.HasInputAuthority)
            {
                string displayName = !string.IsNullOrEmpty(_currentUserInfo.displayName)
                    ? _currentUserInfo.displayName
                    : $"Player{UnityEngine.Random.Range(1000, 9999)}";

                player.SetPlayerInfo(displayName, _currentUserInfo.userType, _currentUserInfo.paypalConnected);
                break;
            }
        }
    }

    // ========== NETWORK EVENT HANDLERS ==========

    void HandleNetworkStatusChanged(bool isConnected)
    {
        if (isConnected)
        {
            UpdateStatus("Multiplayer connection established");

            // Disable multiplayer buttons when connected
            if (joinMultiplayerButton != null)
                joinMultiplayerButton.interactable = false;
            if (createRoomButton != null)
                createRoomButton.interactable = false;
        }
        else
        {
            UpdateStatus("Multiplayer disconnected");

            // Re-enable multiplayer buttons
            if (joinMultiplayerButton != null)
                joinMultiplayerButton.interactable = true;
            if (createRoomButton != null)
                createRoomButton.interactable = true;

            _isConnecting = false;
        }
    }

    void HandlePlayerJoined(PlayerRef player)
    {
        UpdateStatus($"Player joined the session");
    }

    void HandlePlayerLeft(PlayerRef player)
    {
        UpdateStatus($"Player left the session");
    }

    void HandlePlayerCountChanged(int playerCount)
    {
        UpdateStatus($"Players in session: {playerCount}");
    }

    // ========== FUSION CALLBACKS (CORRECTED FOR FUSION 2) ==========

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (player == runner.LocalPlayer)
        {
            UpdateStatus("Successfully joined multiplayer session!");

            if (playerPrefab.IsValid && playerSpawner != null)
            {
                Vector3 spawnPosition = playerSpawner.GetSpawnPosition();
                var playerObject = runner.Spawn(playerPrefab, spawnPosition, Quaternion.identity, player);

                // Update player info after spawning
                UpdateNetworkedPlayerInfo();
            }
        }
        else
        {
            UpdateStatus($"Player {player} joined the session");
        }
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        UpdateStatus($"Player {player} left the session");
    }

    public void OnConnectedToServer(NetworkRunner runner)
    {
        UpdateStatus("Connected to Photon server");
    }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        UpdateStatus($"Disconnected: {reason}");
        _isConnecting = false;
    }

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
        UpdateStatus($"Connection failed: {reason}");
        _isConnecting = false;
    }

    // Required Fusion callbacks
    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        UpdateStatus($"Network shutdown: {shutdownReason}");
        _isConnecting = false;
    }
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

    // ========== PUBLIC API ==========

    public bool IsMultiplayerActive()
    {
        return networkManager != null && networkManager.IsConnected;
    }

    public int GetPlayerCount()
    {
        return networkManager != null ? networkManager.PlayerCount : 0;
    }

    public async void DisconnectMultiplayer()
    {
        if (networkManager != null)
        {
            await networkManager.StopNetworking();
            UpdateStatus("Disconnected from multiplayer");
        }
    }

    public void UpdateUserInfo(string displayName, string userType, bool paypalConnected)
    {
        _currentUserInfo = new UserInfo
        {
            displayName = displayName,
            userType = userType,
            paypalConnected = paypalConnected
        };

        UpdateNetworkedPlayerInfo();
    }
}

// Data classes for JSON parsing
[System.Serializable]
public class PhotonTokenInfo
{
    public string appId;
    public string region;
    public int maxPlayers;
    public string userId;
    public string username;
}

[System.Serializable]
public class SessionInfo
{
    public string sessionId;
    public string roomName;
    public int maxPlayers;
    public string status;
    public int currentPlayers;
}