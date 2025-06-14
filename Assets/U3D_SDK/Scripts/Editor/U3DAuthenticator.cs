using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
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

    public static bool IsLoggedIn => !string.IsNullOrEmpty(_idToken);
    public static string UserEmail => _userEmail;
    public static string DisplayName => _displayName;
    public static string CreatorUsername => _creatorUsername;
    public static bool PayPalConnected => _paypalConnected;

    public static async Task<bool> TryAutoLogin()
    {
        LoadCredentials();

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

            using (var client = new HttpClient())
            {
                var requestData = new { data = loginData };
                var json = JsonConvert.SerializeObject(requestData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var functionEndpoint = FirebaseConfigManager.CurrentConfig.GetFunctionEndpoint("loginWithEmailPassword");
                var response = await client.PostAsync(functionEndpoint, content);
                var responseText = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseText);
                    var actualResult = result.ContainsKey("result") ?
                        JsonConvert.DeserializeObject<Dictionary<string, object>>(result["result"].ToString()) :
                        result;

                    if (actualResult.ContainsKey("idToken"))
                    {
                        _idToken = actualResult["idToken"].ToString();
                        _refreshToken = actualResult.ContainsKey("refreshToken") ? actualResult["refreshToken"].ToString() : "";
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

            using (var client = new HttpClient())
            {
                var response = await client.PostAsync(
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

        using (var client = new HttpClient())
        {
            var requestData = new { data = data };
            var json = JsonConvert.SerializeObject(requestData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            if (!string.IsNullOrEmpty(_idToken))
            {
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_idToken}");
            }

            var functionEndpoint = FirebaseConfigManager.CurrentConfig.GetFunctionEndpoint(functionName);
            var response = await client.PostAsync(functionEndpoint, content);

            var responseText = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseText);
                return result.ContainsKey("result") ?
                    JsonConvert.DeserializeObject<Dictionary<string, object>>(result["result"].ToString()) :
                    result;
            }
            else
            {
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
            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(10);

                var configUrl = "https://unreality3d.web.app/api/config";
                var response = await client.GetAsync(configUrl);

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

                    // Set up development config as well
                    var developmentConfig = new FirebaseEnvironmentConfig
                    {
                        apiKey = "AIzaSyCKXaLA86md04yqv_xlno8ZW_ZhNqWaGzg",
                        authDomain = "unreality3d2025.firebaseapp.com",
                        projectId = "unreality3d2025",
                        storageBucket = "unreality3d2025.firebasestorage.app",
                        messagingSenderId = "244081840635",
                        appId = "1:244081840635:web:71c37efb6b172a706dbb5e",
                        measurementId = "G-YXC3XB3PFL"
                    };

                    FirebaseConfigManager.SetEnvironmentConfig("production", productionConfig);
                    FirebaseConfigManager.SetEnvironmentConfig("development", developmentConfig);

                    Debug.Log("Firebase configuration established dynamically");
                }
                else
                {
                    Debug.LogError($"Failed to fetch dynamic configuration: {response.StatusCode}");
                    throw new Exception("Unable to establish Firebase configuration");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Configuration establishment failed: {ex.Message}");
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
    }

    private static void ClearCredentials()
    {
        UnityEngine.PlayerPrefs.DeleteKey("u3d_idToken");
        UnityEngine.PlayerPrefs.DeleteKey("u3d_refreshToken");
        UnityEngine.PlayerPrefs.DeleteKey("u3d_userEmail");
        UnityEngine.PlayerPrefs.DeleteKey("u3d_displayName");
        UnityEngine.PlayerPrefs.DeleteKey("u3d_creatorUsername");
        UnityEngine.PlayerPrefs.DeleteKey("u3d_paypalConnected");
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