using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using U3D.Editor;
using UnityEngine;

public static class U3DAuthenticator
{
    private static string _idToken;
    private static string _refreshToken;
    private static string _userEmail;
    private static string _displayName;
    private static string _creatorUsername;
    private static bool _paypalConnected;
    private static string _pendingPayPalState;
    private static bool _stayLoggedIn = true; // Default to true for convenience
    private static HttpClient _sharedHttpClient;
    private static bool _networkConfigured = false;

    public static bool IsLoggedIn => !string.IsNullOrEmpty(_idToken);
    public static string UserEmail => _userEmail;
    public static string DisplayName => _displayName;
    public static string CreatorUsername => _creatorUsername;
    public static bool PayPalConnected => _paypalConnected;
    public static class CurrentUser
    {
        public static string UserId => U3DAuthenticator.IsLoggedIn ? "user-id-placeholder" : "";
        public static string Email => U3DAuthenticator.UserEmail;
        public static string DisplayName => U3DAuthenticator.DisplayName;
        public static string CreatorUsername => U3DAuthenticator.CreatorUsername;
        public static string UserType => "creator";
        public static bool PayPalConnected => U3DAuthenticator.PayPalConnected;
    }

    public static bool StayLoggedIn
    {
        get => _stayLoggedIn;
        set
        {
            _stayLoggedIn = value;
            UnityEngine.PlayerPrefs.SetInt("u3d_stayLoggedIn", value ? 1 : 0);
            UnityEngine.PlayerPrefs.Save();

            // If user unchecks "stay logged in", clear stored credentials
            if (!value)
            {
                ClearCredentials();
            }
        }
    }

    // Static constructor to configure networking ONCE when class is first used
    static U3DAuthenticator()
    {
        ConfigureNetworking();
    }

    private static void ConfigureNetworking()
    {
        if (_networkConfigured) return;

        try
        {
            Debug.Log("🔧 Configuring network settings for Unity Editor...");

            // Configure ServicePointManager ONCE (global .NET settings)
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;
            ServicePointManager.DefaultConnectionLimit = 100;
            ServicePointManager.Expect100Continue = false;
            ServicePointManager.UseNagleAlgorithm = false;
            ServicePointManager.MaxServicePointIdleTime = 90000;

            // Create HttpClientHandler with Unity Editor optimized settings  
            var handler = new HttpClientHandler()
            {
                UseProxy = false, // Critical: Bypass Unity Editor proxy detection
                UseCookies = false,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };

            // Create shared HttpClient (recommended pattern for app lifetime)
            _sharedHttpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromMinutes(10)
            };

            // Set headers once
            _sharedHttpClient.DefaultRequestHeaders.Add("User-Agent", "Unreality3D-Unity-SDK/1.0");
            _sharedHttpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");

            _networkConfigured = true;
            Debug.Log("✅ Network configuration completed successfully");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Network configuration failed: {ex.Message}");
        }
    }

    public static async Task<bool> TryAutoLogin()
    {
        LoadCredentials();

        // Check if user wants to stay logged in
        if (!_stayLoggedIn)
        {
            Debug.Log("Auto-login disabled by user preference");
            return false;
        }

        if (string.IsNullOrEmpty(_idToken))
        {
            Debug.Log("No stored credentials found");
            return false;
        }

        try
        {
            bool isValid = await ValidateStoredToken();
            if (isValid)
            {
                await LoadUserProfile();
                Debug.Log("Auto-login successful");
                return true;
            }
            else
            {
                Debug.Log("Stored token is invalid");
                ClearCredentials();
                return false;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Auto-login failed: {ex.Message}");
            ClearCredentials();
            return false;
        }
    }

    public static async Task<bool> LoginWithEmailPassword(string email, string password)
    {
        try
        {
            await EnsureFirebaseConfiguration();

            var loginData = new
            {
                email = email,
                password = password,
                returnSecureToken = true
            };

            var json = JsonConvert.SerializeObject(loginData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _sharedHttpClient.PostAsync(
                $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key={FirebaseConfigManager.CurrentConfig.apiKey}",
                content);

            var responseText = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseText);

                if (result.ContainsKey("idToken"))
                {
                    _idToken = result["idToken"].ToString();
                    _refreshToken = result.ContainsKey("refreshToken") ? result["refreshToken"].ToString() : "";
                    _userEmail = email;

                    SaveCredentials();
                    await LoadUserProfile();

                    Debug.Log("Login successful");
                    return true;
                }
            }
            else
            {
                try
                {
                    var error = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseText);
                    var errorMessage = "Login failed";

                    if (error.ContainsKey("error"))
                    {
                        var errorData = JsonConvert.DeserializeObject<Dictionary<string, object>>(error["error"].ToString());
                        if (errorData.ContainsKey("message"))
                        {
                            var message = errorData["message"].ToString();
                            errorMessage = message switch
                            {
                                "INVALID_LOGIN_CREDENTIALS" => "Invalid email or password",
                                "USER_DISABLED" => "Account has been disabled",
                                "TOO_MANY_ATTEMPTS_TRY_LATER" => "Too many failed attempts. Please try again later.",
                                _ => $"Login failed: {message}"
                            };
                        }
                    }
                    else
                    {
                        errorMessage = $"Login failed: {responseText}";
                    }

                    throw new Exception(errorMessage);
                }
                catch (JsonException)
                {
                    throw new Exception($"Login failed: {responseText}");
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Login error: {ex.Message}");
            throw;
        }
    }

    public static async Task<bool> RegisterWithEmailPassword(string email, string password)
    {
        try
        {
            await EnsureFirebaseConfiguration();

            var registerData = new
            {
                email = email,
                password = password,
                returnSecureToken = true
            };

            var registerJson = JsonConvert.SerializeObject(registerData);
            var registerContent = new StringContent(registerJson, Encoding.UTF8, "application/json");

            var response = await _sharedHttpClient.PostAsync(
                $"https://identitytoolkit.googleapis.com/v1/accounts:signUp?key={FirebaseConfigManager.CurrentConfig.apiKey}",
                registerContent);

            var responseText = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseText);

                if (result.ContainsKey("idToken"))
                {
                    _idToken = result["idToken"].ToString();
                    _refreshToken = result.ContainsKey("refreshToken") ? result["refreshToken"].ToString() : "";
                    _userEmail = email;

                    SaveCredentials();
                    await LoadUserProfile();

                    Debug.Log("Registration successful");
                    return true;
                }
            }
            else
            {
                var error = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseText);
                var errorMessage = "Registration failed";

                if (error.ContainsKey("error"))
                {
                    var errorData = JsonConvert.DeserializeObject<Dictionary<string, object>>(error["error"].ToString());
                    if (errorData.ContainsKey("message"))
                    {
                        var message = errorData["message"].ToString();
                        errorMessage = message switch
                        {
                            "EMAIL_EXISTS" => "Account already exists",
                            "WEAK_PASSWORD" => "Password should be at least 6 characters",
                            "INVALID_EMAIL" => "Invalid email address",
                            _ => "Registration failed"
                        };
                    }
                }

                throw new Exception(errorMessage);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Registration error: {ex.Message}");
            throw;
        }

        return false;
    }

    // PayPal OAuth Integration Methods
    public static async Task<PayPalAuthResult> StartPayPalLogin()
    {
        try
        {
            _pendingPayPalState = Guid.NewGuid().ToString();

            var authRequest = new
            {
                state = _pendingPayPalState,
                linkExisting = false
            };

            var result = await CallFirebaseFunction("getPayPalAuthUrl", authRequest);

            if (result.ContainsKey("authUrl"))
            {
                string authUrl = result["authUrl"].ToString();
                Debug.Log($"PayPal OAuth URL generated: {authUrl}");

                return new PayPalAuthResult
                {
                    Success = true,
                    AuthUrl = authUrl,
                    State = _pendingPayPalState
                };
            }

            return new PayPalAuthResult
            {
                Success = false,
                ErrorMessage = "Failed to generate PayPal authorization URL"
            };
        }
        catch (Exception ex)
        {
            Debug.LogError($"PayPal login initialization error: {ex.Message}");
            return new PayPalAuthResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public static async Task<PayPalAuthResult> LinkPayPalToExistingAccount()
    {
        if (!IsLoggedIn)
        {
            return new PayPalAuthResult
            {
                Success = false,
                ErrorMessage = "Must be logged in to link PayPal account"
            };
        }

        try
        {
            _pendingPayPalState = Guid.NewGuid().ToString();

            var authRequest = new
            {
                state = _pendingPayPalState,
                linkExisting = true
            };

            var result = await CallFirebaseFunction("getPayPalAuthUrl", authRequest);

            if (result.ContainsKey("authUrl"))
            {
                string authUrl = result["authUrl"].ToString();
                Debug.Log($"PayPal linking URL generated: {authUrl}");

                return new PayPalAuthResult
                {
                    Success = true,
                    AuthUrl = authUrl,
                    State = _pendingPayPalState
                };
            }

            return new PayPalAuthResult
            {
                Success = false,
                ErrorMessage = "Failed to generate PayPal linking URL"
            };
        }
        catch (Exception ex)
        {
            Debug.LogError($"PayPal linking initialization error: {ex.Message}");
            return new PayPalAuthResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public static async Task<PayPalAuthResult> PollPayPalAuthStatus(string state)
    {
        if (string.IsNullOrEmpty(state) || state != _pendingPayPalState)
        {
            return new PayPalAuthResult
            {
                Success = false,
                ErrorMessage = "Invalid or expired PayPal authentication state"
            };
        }

        try
        {
            var pollRequest = new
            {
                state = state
            };

            var result = await CallFirebaseFunction("checkPayPalAuthStatus", pollRequest);

            if (result.ContainsKey("status"))
            {
                string status = result["status"].ToString();

                switch (status)
                {
                    case "completed":
                        if (result.ContainsKey("user"))
                        {
                            var userData = JsonConvert.DeserializeObject<Dictionary<string, object>>(result["user"].ToString());

                            if (userData.ContainsKey("customToken"))
                            {
                                _idToken = userData["customToken"].ToString();
                                _userEmail = userData.ContainsKey("email") ? userData["email"].ToString() : "";
                                _displayName = userData.ContainsKey("displayName") ? userData["displayName"].ToString() : "";

                                SaveCredentials();
                                await LoadUserProfile();

                                _pendingPayPalState = null;

                                return new PayPalAuthResult
                                {
                                    Success = true,
                                    Completed = true,
                                    Message = "PayPal authentication successful"
                                };
                            }
                        }
                        break;

                    case "pending":
                        return new PayPalAuthResult
                        {
                            Success = true,
                            Completed = false,
                            Message = "PayPal authentication in progress..."
                        };

                    case "error":
                        string errorMessage = result.ContainsKey("error") ? result["error"].ToString() : "PayPal authentication failed";
                        _pendingPayPalState = null;

                        return new PayPalAuthResult
                        {
                            Success = false,
                            ErrorMessage = errorMessage
                        };
                }
            }

            return new PayPalAuthResult
            {
                Success = false,
                ErrorMessage = "Unexpected response from PayPal authentication status"
            };
        }
        catch (Exception ex)
        {
            Debug.LogError($"PayPal auth status polling error: {ex.Message}");
            return new PayPalAuthResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public static async Task<bool> CheckUsernameAvailability(string username)
    {
        try
        {
            var result = await CallFirebaseFunction("checkUsernameAvailability", new { username = username });
            return result.ContainsKey("available") && (bool)result["available"];
        }
        catch (Exception ex)
        {
            Debug.LogError($"Username check error: {ex.Message}");
            return false;
        }
    }

    public static async Task<string[]> GetUsernameSuggestions(string username)
    {
        try
        {
            var result = await CallFirebaseFunction("checkUsernameAvailability", new { username = username });
            if (result.ContainsKey("suggestions"))
            {
                var suggestions = JsonConvert.DeserializeObject<string[]>(result["suggestions"].ToString());
                return suggestions ?? new string[0];
            }
            return new string[0];
        }
        catch (Exception ex)
        {
            Debug.LogError($"Username suggestions error: {ex.Message}");
            return new string[0];
        }
    }

    public static async Task<bool> ReserveUsername(string username)
    {
        try
        {
            var result = await CallFirebaseFunction("reserveUsername", new { username = username });

            if (result.ContainsKey("success") && (bool)result["success"])
            {
                _creatorUsername = username;
                SaveCredentials();
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Username reservation error: {ex.Message}");
            throw;
        }
    }

    public static void Logout()
    {
        ClearCredentials();
        _pendingPayPalState = null;
        Debug.Log("Logged out successfully");
    }

    private static async Task<Dictionary<string, object>> CallFirebaseFunction(string functionName, object data)
    {
        await EnsureFirebaseConfiguration();

        const int maxRetries = 3;
        const int baseDelayMs = 1000;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                var requestData = new { data = data };
                var json = JsonConvert.SerializeObject(requestData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // Handle auth header properly
                if (!string.IsNullOrEmpty(_idToken))
                {
                    // Remove any existing Authorization header and add new one
                    _sharedHttpClient.DefaultRequestHeaders.Remove("Authorization");
                    _sharedHttpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_idToken}");
                }

                var functionEndpoint = FirebaseConfigManager.CurrentConfig.GetFunctionEndpoint(functionName);

                Debug.Log($"🔗 Attempt {attempt}/{maxRetries}: Calling {functionName}");
                Debug.Log($"📦 Request size: {json.Length} characters ({json.Length / (1024.0 * 1024.0):F2} MB)");

                var startTime = DateTime.Now;
                var response = await _sharedHttpClient.PostAsync(functionEndpoint, content);
                var responseText = await response.Content.ReadAsStringAsync();

                var duration = DateTime.Now - startTime;
                Debug.Log($"✅ Request completed in {duration.TotalSeconds:F1} seconds");

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseText);
                    return result.ContainsKey("result") ?
                        JsonConvert.DeserializeObject<Dictionary<string, object>>(result["result"].ToString()) :
                        result;
                }
                else
                {
                    Debug.LogError($"❌ HTTP Error: {response.StatusCode} - {responseText}");
                    var error = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseText);
                    var errorMessage = "Function call failed";

                    if (error.ContainsKey("error"))
                    {
                        var errorData = JsonConvert.DeserializeObject<Dictionary<string, object>>(error["error"].ToString());
                        if (errorData.ContainsKey("message"))
                        {
                            errorMessage = errorData["message"].ToString();
                        }
                    }

                    throw new Exception(errorMessage);
                }
            }
            catch (HttpRequestException ex) when (attempt < maxRetries)
            {
                var delay = (int)(baseDelayMs * Math.Pow(2, attempt - 1));
                Debug.LogWarning($"⚠️ HttpRequestException on attempt {attempt}/{maxRetries}: {ex.Message}");

                // Log inner exception details for debugging
                if (ex.InnerException != null)
                {
                    Debug.LogWarning($"Inner exception: {ex.InnerException.GetType().Name} - {ex.InnerException.Message}");
                }

                Debug.LogWarning($"🔄 Retrying in {delay}ms...");
                await Task.Delay(delay);
                continue;
            }
            catch (TaskCanceledException ex) when (attempt < maxRetries && !ex.CancellationToken.IsCancellationRequested)
            {
                var delay = (int)(baseDelayMs * Math.Pow(2, attempt - 1));
                Debug.LogWarning($"⏰ Timeout on attempt {attempt}/{maxRetries}: {ex.Message}");
                Debug.LogWarning($"🔄 Retrying in {delay}ms...");
                await Task.Delay(delay);
                continue;
            }
            catch (Exception ex) when (attempt < maxRetries && IsRetriableNetworkError(ex))
            {
                var delay = (int)(baseDelayMs * Math.Pow(2, attempt - 1));
                Debug.LogWarning($"🌐 Network error on attempt {attempt}/{maxRetries}: {ex.Message}");
                Debug.LogWarning($"🔄 Retrying in {delay}ms...");
                await Task.Delay(delay);
                continue;
            }
            catch (HttpRequestException ex)
            {
                Debug.LogError($"💥 Network request failed after {maxRetries} attempts: {ex.Message}");

                // Provide specific guidance for Unity Editor network issues
                string guidance = "This appears to be a Unity Editor network configuration issue. Try:\n" +
                                "1. Restart Unity Editor\n" +
                                "2. Check Windows Firewall settings\n" +
                                "3. Disable proxy/VPN temporarily\n" +
                                "4. Try from a different network";

                throw new Exception($"Network connection failed: {ex.Message}\n\n{guidance}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"🚨 Unexpected error on final attempt: {ex.GetType().Name} - {ex.Message}");
                throw;
            }
        }

        throw new Exception($"Failed to call {functionName} after {maxRetries} attempts");
    }

    public static void Cleanup()
    {
        _sharedHttpClient?.Dispose();
        _sharedHttpClient = null;
        _networkConfigured = false;
    }

    private static bool IsRetriableNetworkError(Exception ex)
    {
        var message = ex.Message?.ToLower() ?? "";
        return message.Contains("connection") ||
               message.Contains("timeout") ||
               message.Contains("network") ||
               message.Contains("dns") ||
               message.Contains("socket") ||
               message.Contains("established connection was aborted") ||
               message.Contains("unable to read data from the transport connection");
    }

    private static async Task EnsureFirebaseConfiguration()
    {
        if (FirebaseConfigManager.IsConfigurationComplete() &&
            FirebaseConfigManager.CurrentConfig.apiKey != "setup-required")
        {
            return;
        }

        Debug.Log("Establishing Firebase configuration...");
        await EstablishDynamicConfiguration();

        if (!FirebaseConfigManager.IsConfigurationComplete())
        {
            throw new Exception("Failed to establish Firebase configuration.");
        }
    }

    private static async Task EstablishDynamicConfiguration()
    {
        try
        {
            var configUrl = "https://unreality3d.web.app/api/config";
            var response = await _sharedHttpClient.GetAsync(configUrl);

            if (response.IsSuccessStatusCode)
            {
                var configJson = await response.Content.ReadAsStringAsync();
                var firebaseConfig = JsonConvert.DeserializeObject<Dictionary<string, object>>(configJson);

                // Create production config from response
                var productionConfig = new FirebaseEnvironmentConfig
                {
                    apiKey = firebaseConfig.ContainsKey("apiKey") ? firebaseConfig["apiKey"].ToString() : "",
                    authDomain = firebaseConfig.ContainsKey("authDomain") ? firebaseConfig["authDomain"].ToString() : "",
                    projectId = firebaseConfig.ContainsKey("projectId") ? firebaseConfig["projectId"].ToString() : "",
                    storageBucket = firebaseConfig.ContainsKey("storageBucket") ? firebaseConfig["storageBucket"].ToString() : "",
                    messagingSenderId = firebaseConfig.ContainsKey("messagingSenderId") ? firebaseConfig["messagingSenderId"].ToString() : "",
                    appId = firebaseConfig.ContainsKey("appId") ? firebaseConfig["appId"].ToString() : "",
                    measurementId = firebaseConfig.ContainsKey("measurementId") ? firebaseConfig["measurementId"].ToString() : ""
                };

                // Fetch development config from secure endpoint too
                var developmentConfig = new FirebaseEnvironmentConfig();

                try
                {
                    var devConfigUrl = "https://unreality3d2025.web.app/api/config";
                    var devResponse = await _sharedHttpClient.GetAsync(devConfigUrl);

                    if (devResponse.IsSuccessStatusCode)
                    {
                        var devConfigJson = await devResponse.Content.ReadAsStringAsync();
                        var devFirebaseConfig = JsonConvert.DeserializeObject<Dictionary<string, object>>(devConfigJson);

                        developmentConfig = new FirebaseEnvironmentConfig
                        {
                            apiKey = devFirebaseConfig.ContainsKey("apiKey") ? devFirebaseConfig["apiKey"].ToString() : "setup-required",
                            authDomain = "unreality3d2025.firebaseapp.com",
                            projectId = "unreality3d2025",
                            storageBucket = "unreality3d2025.firebasestorage.app",
                            messagingSenderId = "244081840635",
                            appId = "1:244081840635:web:71c37efb6b172a706dbb5e",
                            measurementId = "G-YXC3XB3PFL"
                        };
                        Debug.Log("Development Firebase configuration loaded securely");
                    }
                    else
                    {
                        // Fallback if dev endpoint unavailable
                        developmentConfig = new FirebaseEnvironmentConfig
                        {
                            apiKey = "setup-required", // No hardcoded keys
                            authDomain = "unreality3d2025.firebaseapp.com",
                            projectId = "unreality3d2025",
                            storageBucket = "unreality3d2025.firebasestorage.app",
                            messagingSenderId = "244081840635",
                            appId = "1:244081840635:web:71c37efb6b172a706dbb5e",
                            measurementId = "G-YXC3XB3PFL"
                        };
                        Debug.LogWarning("Development config endpoint unavailable, using fallback");
                    }
                }
                catch (Exception devEx)
                {
                    Debug.LogWarning($"Could not fetch development config: {devEx.Message}");
                    // Safe fallback
                    developmentConfig = new FirebaseEnvironmentConfig
                    {
                        apiKey = "setup-required", // No hardcoded keys
                        authDomain = "unreality3d2025.firebaseapp.com",
                        projectId = "unreality3d2025",
                        storageBucket = "unreality3d2025.firebasestorage.app",
                        messagingSenderId = "244081840635",
                        appId = "1:244081840635:web:71c37efb6b172a706dbb5e",
                        measurementId = "G-YXC3XB3PFL"
                    };
                }

                FirebaseConfigManager.SetEnvironmentConfig("production", productionConfig);
                FirebaseConfigManager.SetEnvironmentConfig("development", developmentConfig);

                Debug.Log("Firebase configuration established securely - no hardcoded API keys");
            }
            else
            {
                Debug.LogError($"Failed to fetch secure configuration: {response.StatusCode}");
                throw new Exception("Unable to establish Firebase configuration securely");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Secure configuration establishment failed: {ex.Message}");
            throw;
        }
    }

    private static async Task<bool> ValidateStoredToken()
    {
        try
        {
            var result = await CallFirebaseFunction("getUserProfile", new { });
            return result.ContainsKey("userId");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Token validation failed: {ex.Message}");
            ClearCredentials();
            return false;
        }
    }

    private static async Task LoadUserProfile()
    {
        try
        {
            var result = await CallFirebaseFunction("getUserProfile", new { });

            if (result.ContainsKey("creatorUsername"))
            {
                _creatorUsername = result["creatorUsername"].ToString();
            }

            if (result.ContainsKey("email"))
            {
                _userEmail = result["email"].ToString();
            }

            if (result.ContainsKey("displayName"))
            {
                _displayName = result["displayName"].ToString();
            }

            if (result.ContainsKey("paypalConnected"))
            {
                _paypalConnected = (bool)result["paypalConnected"];
            }

            Debug.Log($"Profile loaded: {_userEmail}, Username: {_creatorUsername}, PayPal: {_paypalConnected}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Profile load failed: {ex.Message}");
        }
    }

    private static void SaveCredentials()
    {
        // Only save credentials if user wants to stay logged in
        if (_stayLoggedIn)
        {
            if (!string.IsNullOrEmpty(_idToken))
            {
                UnityEngine.PlayerPrefs.SetString("u3d_idToken", _idToken);
            }
            if (!string.IsNullOrEmpty(_refreshToken))
            {
                UnityEngine.PlayerPrefs.SetString("u3d_refreshToken", _refreshToken);
            }
            if (!string.IsNullOrEmpty(_userEmail))
            {
                UnityEngine.PlayerPrefs.SetString("u3d_userEmail", _userEmail);
            }
            if (!string.IsNullOrEmpty(_displayName))
            {
                UnityEngine.PlayerPrefs.SetString("u3d_displayName", _displayName);
            }
            if (!string.IsNullOrEmpty(_creatorUsername))
            {
                UnityEngine.PlayerPrefs.SetString("u3d_creatorUsername", _creatorUsername);
            }
            UnityEngine.PlayerPrefs.SetInt("u3d_paypalConnected", _paypalConnected ? 1 : 0);
        }

        // Always save the preference itself
        UnityEngine.PlayerPrefs.SetInt("u3d_stayLoggedIn", _stayLoggedIn ? 1 : 0);
        UnityEngine.PlayerPrefs.Save();
    }

    private static void LoadCredentials()
    {
        _idToken = UnityEngine.PlayerPrefs.GetString("u3d_idToken", "");
        _refreshToken = UnityEngine.PlayerPrefs.GetString("u3d_refreshToken", "");
        _userEmail = UnityEngine.PlayerPrefs.GetString("u3d_userEmail", "");
        _displayName = UnityEngine.PlayerPrefs.GetString("u3d_displayName", "");
        _creatorUsername = UnityEngine.PlayerPrefs.GetString("u3d_creatorUsername", "");
        _paypalConnected = UnityEngine.PlayerPrefs.GetInt("u3d_paypalConnected", 0) == 1;
        _stayLoggedIn = UnityEngine.PlayerPrefs.GetInt("u3d_stayLoggedIn", 1) == 1; // Default to true
    }

    private static void ClearCredentials()
    {
        UnityEngine.PlayerPrefs.DeleteKey("u3d_idToken");
        UnityEngine.PlayerPrefs.DeleteKey("u3d_refreshToken");
        UnityEngine.PlayerPrefs.DeleteKey("u3d_userEmail");
        UnityEngine.PlayerPrefs.DeleteKey("u3d_displayName");
        UnityEngine.PlayerPrefs.DeleteKey("u3d_creatorUsername");
        UnityEngine.PlayerPrefs.DeleteKey("u3d_paypalConnected");
        // Note: Don't clear u3d_stayLoggedIn - preserve user's preference
        UnityEngine.PlayerPrefs.Save();

        _idToken = "";
        _refreshToken = "";
        _userEmail = "";
        _displayName = "";
        _creatorUsername = "";
        _paypalConnected = false;
    }
}

[System.Serializable]
public class PayPalAuthResult
{
    public bool Success { get; set; }
    public bool Completed { get; set; }
    public string AuthUrl { get; set; }
    public string State { get; set; }
    public string Message { get; set; }
    public string ErrorMessage { get; set; }
}

[System.Serializable]
public class U3DConfiguration
{
    public string productionApiKey;
    public string productionAuthDomain;
    public string productionProjectId;
    public string productionStorageBucket;
    public string productionMessagingSenderId;
    public string productionAppId;

    public string developmentApiKey;
    public string developmentAuthDomain;
    public string developmentProjectId;
    public string developmentStorageBucket;
    public string developmentMessagingSenderId;
    public string developmentAppId;
}