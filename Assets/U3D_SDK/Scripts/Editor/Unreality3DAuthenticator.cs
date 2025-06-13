using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using System.Net.Http;
using Newtonsoft.Json;

namespace U3D.Editor
{
    public static class Unreality3DAuthenticator
    {
        private static string _idToken = "";
        private static string _creatorUsername = "";
        private static string _userEmail = "";
        private static string _displayName = "";
        private static bool _paypalConnected = false;
        private static bool _isLoggedIn = false;

        // Public Properties
        public static bool IsLoggedIn => _isLoggedIn;
        public static string CreatorUsername => _creatorUsername;
        public static string UserEmail => _userEmail;
        public static string DisplayName => _displayName;
        public static bool PayPalConnected => _paypalConnected;

        // Initialize on startup
        [InitializeOnLoadMethod]
        static void Initialize()
        {
            LoadStoredCredentials();
        }

        private static void LoadStoredCredentials()
        {
            _idToken = EditorPrefs.GetString("U3D_IDToken", "");
            _creatorUsername = EditorPrefs.GetString("U3D_CreatorUsername", "");
            _userEmail = EditorPrefs.GetString("U3D_UserEmail", "");
            _displayName = EditorPrefs.GetString("U3D_DisplayName", "");
            _paypalConnected = EditorPrefs.GetBool("U3D_PayPalConnected", false);
            _isLoggedIn = !string.IsNullOrEmpty(_idToken);

            // Validate stored token on startup
            if (_isLoggedIn)
            {
                _ = ValidateStoredToken();
            }
        }

        private static void SaveCredentials()
        {
            EditorPrefs.SetString("U3D_IDToken", _idToken);
            EditorPrefs.SetString("U3D_CreatorUsername", _creatorUsername);
            EditorPrefs.SetString("U3D_UserEmail", _userEmail);
            EditorPrefs.SetString("U3D_DisplayName", _displayName);
            EditorPrefs.SetBool("U3D_PayPalConnected", _paypalConnected);
        }

        private static void ClearCredentials()
        {
            _idToken = "";
            _creatorUsername = "";
            _userEmail = "";
            _displayName = "";
            _paypalConnected = false;
            _isLoggedIn = false;

            EditorPrefs.DeleteKey("U3D_IDToken");
            EditorPrefs.DeleteKey("U3D_CreatorUsername");
            EditorPrefs.DeleteKey("U3D_UserEmail");
            EditorPrefs.DeleteKey("U3D_DisplayName");
            EditorPrefs.DeleteKey("U3D_PayPalConnected");
        }

        public static async Task<bool> LoginWithEmailPassword(string email, string password)
        {
            try
            {
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
                    // Use Firebase Auth REST API
                    var response = await client.PostAsync(
                        "https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key=AIzaSyCKXaLA86md04yqv_xlno8ZW_ZhNqWaGzg",
                        loginContent);

                    var responseText = await response.Content.ReadAsStringAsync();

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
                                    "EMAIL_NOT_FOUND" => "Account not found",
                                    "INVALID_PASSWORD" => "Incorrect password",
                                    "USER_DISABLED" => "Account has been disabled",
                                    _ => "Login failed"
                                };
                            }
                        }

                        throw new Exception(errorMessage);
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
                    // Use Firebase Auth REST API
                    var response = await client.PostAsync(
                        "https://identitytoolkit.googleapis.com/v1/accounts:signUp?key=AIzaSyCKXaLA86md04yqv_xlno8ZW_ZhNqWaGzg",
                        registerContent);

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

                var response = await client.PostAsync(
                    $"https://{functionName}-peaofujdma-uc.a.run.app",
                    content);

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
    }
}