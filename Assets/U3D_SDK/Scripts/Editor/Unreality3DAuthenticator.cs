using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json;

namespace U3D.Editor
{
    public static class Unreality3DAuthenticator
    {
        private static string _idToken = "";
        private static string _userEmail = "";
        private static string _displayName = "";
        private static string _creatorUsername = "";
        private static bool _paypalConnected = false;
        private static bool _isLoggedIn = false;

        // Auto-login functionality
        private static bool _autoLoginAttempted = false;

        // Public properties
        public static bool IsLoggedIn => _isLoggedIn;
        public static string UserEmail => _userEmail;
        public static string DisplayName => _displayName;
        public static string CreatorUsername => _creatorUsername;
        public static bool PaypalConnected => _paypalConnected;

        static Unreality3DAuthenticator()
        {
            EditorApplication.update += CheckAutoLogin;
        }

        private static async void CheckAutoLogin()
        {
            if (!_autoLoginAttempted && !_isLoggedIn)
            {
                _autoLoginAttempted = true;
                EditorApplication.update -= CheckAutoLogin;

                try
                {
                    await TryAutoLogin();
                }
                catch (Exception ex)
                {
                    Debug.Log($"Auto-login skipped: {ex.Message}");
                }
            }
        }

        private static async Task TryAutoLogin()
        {
            string storedToken = EditorPrefs.GetString("U3D_IdToken", "");
            string storedEmail = EditorPrefs.GetString("U3D_UserEmail", "");

            if (!string.IsNullOrEmpty(storedToken) && !string.IsNullOrEmpty(storedEmail))
            {
                _idToken = storedToken;
                _userEmail = storedEmail;
                _isLoggedIn = true;

                bool isValid = await ValidateStoredToken();
                if (isValid)
                {
                    await LoadUserProfile();
                    Debug.Log($"Auto-login successful: {_userEmail}");
                }
            }
        }

        public static async Task<bool> LoginWithEmailPassword(string email, string password)
        {
            try
            {
                // Establish Firebase configuration if needed
                await EnsureFirebaseConfiguration();

                var loginData = new
                {
                    email = email,
                    password = password,
                    returnSecureToken = true
                };

                var loginJson = JsonConvert.SerializeObject(loginData);
                var loginContent = new StringContent(loginJson, Encoding.UTF8, "application/json");

                using (var client = new HttpClient())
                {
                    var authEndpoint = FirebaseConfigManager.CurrentConfig.GetAuthEndpoint("signInWithPassword");
                    Debug.Log($"Attempting login to: {authEndpoint}");
                    Debug.Log($"Using project: {FirebaseConfigManager.CurrentConfig.projectId}");

                    var response = await client.PostAsync(authEndpoint, loginContent);
                    var responseText = await response.Content.ReadAsStringAsync();

                    // Add detailed logging for debugging
                    Debug.Log($"Response status: {response.StatusCode}");
                    Debug.Log($"Response content: {responseText}");

                    if (response.IsSuccessStatusCode)
                    {
                        var result = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseText);
                        _idToken = result["idToken"].ToString();
                        _userEmail = result["email"].ToString();
                        _isLoggedIn = true;

                        // Load user profile
                        await LoadUserProfile();
                        SaveCredentials();

                        Debug.Log($"Login successful: {_userEmail}");
                        return true;
                    }
                    else
                    {
                        // Parse the error response more thoroughly
                        try
                        {
                            var error = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseText);
                            var errorMessage = "Login failed";

                            if (error.ContainsKey("error"))
                            {
                                var errorData = JsonConvert.DeserializeObject<Dictionary<string, object>>(error["error"].ToString());

                                // Log the complete error structure for debugging
                                Debug.LogError($"Complete error response: {error["error"].ToString()}");

                                if (errorData.ContainsKey("message"))
                                {
                                    var message = errorData["message"].ToString();
                                    Debug.LogError($"Firebase error message: {message}");

                                    errorMessage = message switch
                                    {
                                        "EMAIL_NOT_FOUND" => "Account not found",
                                        "INVALID_PASSWORD" => "Incorrect password",
                                        "USER_DISABLED" => "Account has been disabled",
                                        "INVALID_LOGIN_CREDENTIALS" => "Invalid email or password",
                                        "TOO_MANY_ATTEMPTS_TRY_LATER" => "Too many failed attempts. Please try again later.",
                                        _ => $"Login failed: {message}"
                                    };
                                }
                            }
                            else
                            {
                                // If there's no "error" key, show the full response
                                errorMessage = $"Login failed: {responseText}";
                            }

                            throw new Exception(errorMessage);
                        }
                        catch (JsonException)
                        {
                            // If we can't parse the JSON, show the raw response
                            throw new Exception($"Login failed: {responseText}");
                        }
                    }
                }
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
                // Establish Firebase configuration if needed
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
                    var authEndpoint = FirebaseConfigManager.CurrentConfig.GetAuthEndpoint("signUp");
                    var response = await client.PostAsync(authEndpoint, registerContent);

                    var responseText = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        var result = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseText);
                        _idToken = result["idToken"].ToString();
                        _userEmail = result["email"].ToString();
                        _isLoggedIn = true;

                        // Create initial user profile
                        await LoadUserProfile();
                        SaveCredentials();

                        Debug.Log($"Registration successful: {_userEmail}");
                        return true;
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
            Debug.Log("Logged out successfully");
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

                Debug.Log($"Profile loaded: {_userEmail}, Username: {_creatorUsername}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Profile load failed: {ex.Message}");
                // Don't throw - profile might not exist yet, which is fine
            }
        }

        private static async Task<Dictionary<string, object>> CallFirebaseFunction(string functionName, object data)
        {
            // Establish Firebase configuration if needed
            await EnsureFirebaseConfiguration();

            using (var client = new HttpClient())
            {
                var requestData = new { data = data };
                var json = JsonConvert.SerializeObject(requestData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // Add authentication header if logged in
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
            // Check if we have a valid configuration (not the placeholder)
            if (FirebaseConfigManager.IsConfigurationComplete() &&
                FirebaseConfigManager.CurrentConfig.apiKey != "setup-required")
            {
                return; // Already properly configured
            }

            Debug.Log("Establishing Firebase configuration...");
            await EstablishDynamicConfiguration();

            // Verify configuration was established
            if (!FirebaseConfigManager.IsConfigurationComplete())
            {
                throw new Exception("Failed to establish Firebase configuration. Please check your internet connection and try again.");
            }
        }

        private static async Task EstablishDynamicConfiguration()
        {
            try
            {
                using (var client = new HttpClient())
                {
                    // Use a public endpoint to get public Firebase configuration
                    // These API keys are public anyway (visible in deployed web apps)
                    var response = await client.GetAsync("https://unreality3d.web.app/__/firebase/init.json");

                    if (response.IsSuccessStatusCode)
                    {
                        var configText = await response.Content.ReadAsStringAsync();
                        var firebaseConfig = JsonConvert.DeserializeObject<Dictionary<string, object>>(configText);

                        var productionConfig = new FirebaseEnvironmentConfig
                        {
                            apiKey = firebaseConfig["apiKey"].ToString(),
                            authDomain = firebaseConfig["authDomain"].ToString(),
                            projectId = firebaseConfig["projectId"].ToString(),
                            storageBucket = firebaseConfig["storageBucket"].ToString(),
                            messagingSenderId = firebaseConfig["messagingSenderId"].ToString(),
                            appId = firebaseConfig["appId"].ToString(),
                            measurementId = firebaseConfig.ContainsKey("measurementId") ?
                                firebaseConfig["measurementId"].ToString() : ""
                        };

                        // Also set up development config for completeness
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
                        // Fallback to hardcoded public configuration
                        SetupFallbackConfiguration();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Dynamic configuration failed: {ex.Message}. Using fallback.");
                SetupFallbackConfiguration();
            }
        }

        private static void SetupFallbackConfiguration()
        {
            // Fallback to known public configuration
            // These are public API keys that are already exposed in web deployments
            var productionConfig = new FirebaseEnvironmentConfig
            {
                apiKey = "AIzaSyB3GWmzcyew1yw4sUi6vn-Ys6643JI9zMo",
                authDomain = "unreality3d.firebaseapp.com",
                projectId = "unreality3d",
                storageBucket = "unreality3d.firebasestorage.app",
                messagingSenderId = "183773881724",
                appId = "1:183773881724:web:50ca32baa00960b46170f9",
                measurementId = "G-YXC3XB3PFL"
            };

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

            Debug.Log("Firebase fallback configuration applied");
        }

        private static void SaveCredentials()
        {
            EditorPrefs.SetString("U3D_IdToken", _idToken);
            EditorPrefs.SetString("U3D_UserEmail", _userEmail);
            EditorPrefs.SetString("U3D_DisplayName", _displayName);
            EditorPrefs.SetString("U3D_CreatorUsername", _creatorUsername);
            EditorPrefs.SetBool("U3D_PaypalConnected", _paypalConnected);
        }

        private static void ClearCredentials()
        {
            _idToken = "";
            _userEmail = "";
            _displayName = "";
            _creatorUsername = "";
            _paypalConnected = false;
            _isLoggedIn = false;

            EditorPrefs.DeleteKey("U3D_IdToken");
            EditorPrefs.DeleteKey("U3D_UserEmail");
            EditorPrefs.DeleteKey("U3D_DisplayName");
            EditorPrefs.DeleteKey("U3D_CreatorUsername");
            EditorPrefs.DeleteKey("U3D_PaypalConnected");
        }
    }
}