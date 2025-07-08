using Fusion;
using System.Runtime.InteropServices;
using U3D.Networking;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// U3D Platform Integration - Handles PayPal, professional URLs, and multiplayer auto-initialization
/// This component works automatically - no configuration needed by Creators
/// </summary>
public class FirebaseIntegration : MonoBehaviour
{
    [Header("Platform Integration")]
    [Tooltip("This component handles PayPal, professional URLs, and multiplayer automatically")]
    [SerializeField] private bool enableMultiplayer = true;

    [Header("Debug Info (Read-Only)")]
    [SerializeField] private string detectedEnvironment = "Detecting...";
    [SerializeField] private string professionalURL = "";
    [SerializeField] private bool multiplayerActive = false;

    // Internal settings (hidden from Creators)
    private string contentId = "creator-content";
    private float contentPrice = 0f;
    private int maxPlayers = 10;

    // Platform integration components (auto-found)
    private U3DFusionNetworkManager networkManager;
    private U3DPlayerSpawner playerSpawner;

    // PayPal Integration
    [DllImport("__Internal")]
    private static extern void UnityCheckContentAccess(string contentId);

    [DllImport("__Internal")]
    private static extern void UnityRequestPayment(string contentId, string price);

    // Professional URL Detection
    [DllImport("__Internal")]
    private static extern System.IntPtr UnityGetCurrentURL();

    [DllImport("__Internal")]
    private static extern System.IntPtr UnityGetDeploymentInfo();

    [DllImport("__Internal")]
    private static extern void UnityReportDeploymentMetrics(string deploymentType, string loadTime);

    // Photon Fusion Integration
    [DllImport("__Internal")]
    private static extern void UnityGetPhotonToken(string roomName, string contentId);

    // Platform state
    private bool _isConnecting = false;
    private string _pendingRoomName = "";
    private string _photonAppId = "a3df46ef-b10a-4954-8526-7a9fdd553543";
    private UserInfo _currentUserInfo;
    private DeploymentInfo _deploymentInfo;
    private float _startTime;

    [System.Serializable]
    public class UserInfo
    {
        public string userId;
        public string displayName;
        public string userType;
        public bool paypalConnected;
        public string creatorUsername;
    }

    [System.Serializable]
    public class DeploymentInfo
    {
        public string url;
        public string hostname;
        public string pathname;
        public bool isProduction;
        public bool isProfessionalURL;
        public string creatorUsername;
        public string projectName;
        public string deploymentType;
    }

    void Start()
    {
        _startTime = Time.time;

        // Auto-detect platform environment
        DetectDeploymentEnvironment();

        // Auto-find platform components
        InitializeComponents();

        // Auto-check content access
        CheckContentAccess();

        // Auto-initialize multiplayer if enabled
        if (enableMultiplayer)
        {
            AutoInitializeMultiplayer();
        }
    }

    void InitializeComponents()
    {
        // Auto-find network components
        if (networkManager == null)
            networkManager = FindAnyObjectByType<U3DFusionNetworkManager>();

        if (playerSpawner == null)
            playerSpawner = FindAnyObjectByType<U3DPlayerSpawner>();

        // Auto-create if missing
        if (networkManager == null && enableMultiplayer)
        {
            var networkManagerObject = new GameObject("U3D Network Manager");
            networkManager = networkManagerObject.AddComponent<U3DFusionNetworkManager>();
            Debug.Log("Auto-created U3D Network Manager");
        }

        if (playerSpawner == null && enableMultiplayer)
        {
            var spawnerObject = new GameObject("U3D Player Spawner");
            playerSpawner = spawnerObject.AddComponent<U3DPlayerSpawner>();
            Debug.Log("Auto-created U3D Player Spawner");
        }
    }

    void DetectDeploymentEnvironment()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            var deploymentInfoPtr = UnityGetDeploymentInfo();
            var deploymentInfoJson = Marshal.PtrToStringAnsi(deploymentInfoPtr);
            
            if (!string.IsNullOrEmpty(deploymentInfoJson))
            {
                _deploymentInfo = JsonUtility.FromJson<DeploymentInfo>(deploymentInfoJson);
                
                // Update Inspector display
                detectedEnvironment = _deploymentInfo.deploymentType;
                
                if (_deploymentInfo.isProfessionalURL)
                {
                    professionalURL = $"{_deploymentInfo.creatorUsername}.unreality3d.com/{_deploymentInfo.projectName}";
                    contentId = $"{_deploymentInfo.creatorUsername}_{_deploymentInfo.projectName}";
                }
                
                // Report metrics after load
                Invoke(nameof(ReportDeploymentMetrics), 2f);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Platform detection failed: {e.Message}");
            detectedEnvironment = "Detection failed";
        }
#else
        detectedEnvironment = "Unity Editor";
        _deploymentInfo = new DeploymentInfo
        {
            deploymentType = "editor",
            isProduction = false,
            isProfessionalURL = false
        };
#endif
    }

    void ReportDeploymentMetrics()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (_deploymentInfo != null)
        {
            var loadTime = (Time.time - _startTime) * 1000f;
            try
            {
                UnityReportDeploymentMetrics(_deploymentInfo.deploymentType, loadTime.ToString("F0"));
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Failed to report metrics: {e.Message}");
            }
        }
#endif
    }

    void AutoInitializeMultiplayer()
    {
        if (!enableMultiplayer) return;

        string roomName = GetAutoRoomName();

#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            UnityGetPhotonToken(roomName, contentId);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Multiplayer auto-init failed: {e.Message}");
        }
#else
        // Editor simulation
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

    string GetAutoRoomName()
    {
        if (_deploymentInfo != null && _deploymentInfo.isProfessionalURL)
        {
            return $"{_deploymentInfo.creatorUsername}_{_deploymentInfo.projectName}";
        }
        return $"room_{contentId}";
    }

    void CheckContentAccess()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            UnityCheckContentAccess(contentId);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Access check failed: {e.Message}");
        }
#endif
    }

    // ========== PLATFORM CALLBACKS ==========

    public void OnAccessCheckComplete(string hasAccess)
    {
        Debug.Log($"Content access: {(hasAccess == "true" ? "Granted" : "Payment required")}");
    }

    public void OnPaymentComplete(string success)
    {
        if (success == "true")
        {
            Debug.Log("Payment successful - Access granted!");
            CheckContentAccess();
        }
        else
        {
            Debug.Log("Payment failed");
        }
    }

    public void OnPhotonTokenReceived(string tokenData)
    {
        try
        {
            var tokenInfo = JsonUtility.FromJson<PhotonTokenInfo>(tokenData);
            _photonAppId = tokenInfo.appId;
            StartNetworkingWithToken(tokenInfo);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Token parsing failed: {e.Message}");
        }
    }

    public void OnUserProfileReceived(string userDataJson)
    {
        try
        {
            _currentUserInfo = JsonUtility.FromJson<UserInfo>(userDataJson);
            Debug.Log($"User profile received: {_currentUserInfo.displayName}");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"User profile parsing failed: {e.Message}");
        }
    }

    async void StartNetworkingWithToken(PhotonTokenInfo tokenInfo)
    {
        if (networkManager == null) return;

        _isConnecting = true;
        multiplayerActive = false;

        try
        {
            bool success = await networkManager.StartNetworking(_pendingRoomName, tokenInfo.appId);
            multiplayerActive = success;

            if (success)
            {
                Debug.Log($"Multiplayer connected: {_pendingRoomName}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Networking error: {e.Message}");
        }
        finally
        {
            _isConnecting = false;
        }
    }

    // ========== PUBLIC API FOR CREATORS ==========

    /// <summary>
    /// Check if running on a professional URL (username.unreality3d.com)
    /// </summary>
    public bool IsProfessionalURL()
    {
        return _deploymentInfo != null && _deploymentInfo.isProfessionalURL;
    }

    /// <summary>
    /// Get the creator's username (only on professional URLs)
    /// </summary>
    public string GetCreatorUsername()
    {
        return _deploymentInfo?.creatorUsername ?? "";
    }

    /// <summary>
    /// Get the project name
    /// </summary>
    public string GetProjectName()
    {
        return _deploymentInfo?.projectName ?? "";
    }

    /// <summary>
    /// Check if multiplayer is currently active
    /// </summary>
    public bool IsMultiplayerActive()
    {
        return networkManager != null && networkManager.IsConnected;
    }

    /// <summary>
    /// Get current player count
    /// </summary>
    public int GetPlayerCount()
    {
        return networkManager != null ? networkManager.PlayerCount : 0;
    }

    /// <summary>
    /// Enable or disable multiplayer functionality
    /// </summary>
    public void SetMultiplayerEnabled(bool enabled)
    {
        enableMultiplayer = enabled;
        if (!enabled && networkManager != null && networkManager.IsConnected)
        {
            _ = networkManager.StopNetworking();
            multiplayerActive = false;
        }
    }
}

// Data structures for platform integration
[System.Serializable]
public class PhotonTokenInfo
{
    public string appId;
    public string region;
    public int maxPlayers;
    public string userId;
    public string username;
}

#if UNITY_EDITOR
// Custom property drawer for read-only fields
[CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
public class ReadOnlyDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        GUI.enabled = false;
        EditorGUI.PropertyField(position, property, label, true);
        GUI.enabled = true;
    }
}
#endif

// Custom attribute for read-only Inspector fields
public class ReadOnlyAttribute : UnityEngine.PropertyAttribute { }