using UnityEngine;
using UnityEngine.UI;
using System.Runtime.InteropServices;
using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;

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

    // PayPal Integration (Existing)
    [DllImport("__Internal")]
    private static extern void UnityCallTestFunction();

    [DllImport("__Internal")]
    private static extern void UnityCheckContentAccess(string contentId);

    [DllImport("__Internal")]
    private static extern void UnityRequestPayment(string contentId, string price);

    // Photon Fusion Integration (New)
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

    void Start()
    {
        // Existing PayPal button setup
        if (testButton != null)
            testButton.onClick.AddListener(TestFirebaseConnection);

        if (paymentButton != null)
            paymentButton.onClick.AddListener(RequestPayment);

        // New Multiplayer button setup
        if (joinMultiplayerButton != null)
            joinMultiplayerButton.onClick.AddListener(JoinMultiplayer);

        if (createRoomButton != null)
            createRoomButton.onClick.AddListener(CreateRoom);

        CheckContentAccess();
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

    // ========== NEW MULTIPLAYER FUNCTIONS ==========

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
        UpdateStatus("Multiplayer requires WebGL build");
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
        UpdateStatus("Multiplayer requires WebGL build");
#endif
    }

    // ========== FIREBASE CALLBACKS (EXISTING + NEW) ==========

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

    // New Photon callbacks
    public void OnPhotonTokenReceived(string tokenData)
    {
        try
        {
            var tokenInfo = JsonUtility.FromJson<PhotonTokenInfo>(tokenData);
            _photonAppId = tokenInfo.appId;

            UpdateStatus("Token received, connecting to Photon...");
            StartFusionConnection(tokenInfo);
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
            UnityGetPhotonToken(sessionInfo.roomName, contentId);
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

    // ========== PHOTON FUSION NETWORKING ==========

    private async void StartFusionConnection(PhotonTokenInfo tokenInfo)
    {
        if (_runner != null)
        {
            await _runner.Shutdown();
        }

        _isConnecting = true;

        // Create NetworkRunner
        _runner = gameObject.AddComponent<NetworkRunner>();
        _runner.ProvideInput = true;

        // Configure for WebGL Shared Mode
        var gameMode = GameMode.Shared;
        var sceneRef = SceneRef.FromIndex(0);

        var args = new StartGameArgs()
        {
            GameMode = gameMode,
            SessionName = _pendingRoomName,
            Scene = sceneRef,
            SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>()
        };

        UpdateStatus("Connecting to Photon Fusion...");

        var result = await _runner.StartGame(args);

        if (result.Ok)
        {
            UpdateStatus($"Connected to room: {_pendingRoomName}");
        }
        else
        {
            UpdateStatus($"Connection failed: {result.ShutdownReason}");
        }

        _isConnecting = false;
    }

    // ========== FUSION CALLBACKS ==========

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (player == runner.LocalPlayer)
        {
            UpdateStatus("Successfully joined multiplayer session!");

            // Spawn player if we have a prefab configured
            if (playerPrefab != null)
            {
                runner.Spawn(playerPrefab, Vector3.zero, Quaternion.identity, player);
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

    // Minimal required Fusion callbacks
    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
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

// Data classes for JSON parsing
[System.Serializable]
public class PhotonTokenInfo
{
    public string appId;
    public string region;
    public int maxPlayers;
}

[System.Serializable]
public class SessionInfo
{
    public string sessionId;
    public string roomName;
    public int maxPlayers;
    public string status;
}