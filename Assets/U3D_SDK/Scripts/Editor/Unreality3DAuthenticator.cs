using System;
using System.Collections;
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
        private const string AUTH_TOKEN_KEY = "U3D_AuthToken";
        private const string USER_PROFILE_KEY = "U3D_UserProfile";
        private const string FIREBASE_FUNCTIONS_BASE = "https://us-central1-unreality3d.cloudfunctions.net";

        private static HttpClient httpClient;
        private static Unreality3DUserProfile cachedProfile;

        public static bool IsAuthenticated => !string.IsNullOrEmpty(GetStoredAuthToken());
        public static string CreatorUsername => cachedProfile?.creatorUsername ?? "";
        public static string UserEmail => cachedProfile?.email ?? "";

        static Unreality3DAuthenticator()
        {
            httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Unreality3D-Unity-SDK/1.0");
            LoadCachedProfile();
        }

        public static async Task<AuthResult> AuthenticateWithEmailAsync(string email, string password)
        {
            try
            {
                var authRequest = new
                {
                    email = email,
                    password = password,
                    returnSecureToken = true
                };

                var json = JsonConvert.SerializeObject(authRequest);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // Firebase Auth REST API
                var authUrl = "https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key=AIzaSyCKXaLA86md04yqv_xlno8ZW_ZhNqWaGzg";
                var response = await httpClient.PostAsync(authUrl, content);
                var responseText = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    var error = JsonConvert.DeserializeObject<FirebaseAuthError>(responseText);
                    return new AuthResult { Success = false, ErrorMessage = GetFriendlyErrorMessage(error.error.message) };
                }

                var authResponse = JsonConvert.DeserializeObject<FirebaseAuthResponse>(responseText);

                // Get user profile from your Firebase Functions
                var profileResult = await GetUserProfile(authResponse.idToken);
                if (!profileResult.Success)
                {
                    return profileResult;
                }

                // Store authentication data
                StoreAuthToken(authResponse.idToken);
                cachedProfile = profileResult.Profile;
                StoreCachedProfile(cachedProfile);

                return new AuthResult
                {
                    Success = true,
                    Profile = cachedProfile,
                    Message = $"Welcome back, {cachedProfile.creatorUsername}!"
                };
            }
            catch (Exception ex)
            {
                return new AuthResult { Success = false, ErrorMessage = $"Authentication failed: {ex.Message}" };
            }
        }

        public static async Task<AuthResult> CheckUsernameAvailabilityAsync(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return new AuthResult { Success = false, ErrorMessage = "Please enter a username" };
            }

            if (!IsValidUsername(username))
            {
                return new AuthResult { Success = false, ErrorMessage = "Username contains invalid characters. Use only letters, numbers, and hyphens." };
            }

            try
            {
                var checkRequest = new { username = username.ToLower() };
                var json = JsonConvert.SerializeObject(checkRequest);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var authToken = GetStoredAuthToken();
                if (!string.IsNullOrEmpty(authToken))
                {
                    httpClient.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authToken);
                }

                var response = await httpClient.PostAsync($"{FIREBASE_FUNCTIONS_BASE}/checkUsernameAvailability", content);
                var responseText = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return new AuthResult { Success = false, ErrorMessage = "Unable to check username availability" };
                }

                var availabilityResponse = JsonConvert.DeserializeObject<UsernameAvailabilityResponse>(responseText);

                if (!availabilityResponse.available)
                {
                    return new AuthResult { Success = false, ErrorMessage = $"'{username}' is already taken. Try: {string.Join(", ", availabilityResponse.suggestions)}" };
                }

                return new AuthResult { Success = true, Message = $"'{username}' is available!" };
            }
            catch (Exception ex)
            {
                return new AuthResult { Success = false, ErrorMessage = $"Username check failed: {ex.Message}" };
            }
        }

        public static async Task<AuthResult> ReserveUsernameAsync(string username)
        {
            if (!IsAuthenticated)
            {
                return new AuthResult { Success = false, ErrorMessage = "Please log in first" };
            }

            try
            {
                var reserveRequest = new { username = username.ToLower() };
                var json = JsonConvert.SerializeObject(reserveRequest);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var authToken = GetStoredAuthToken();
                httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authToken);

                var response = await httpClient.PostAsync($"{FIREBASE_FUNCTIONS_BASE}/reserveUsername", content);
                var responseText = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    var error = JsonConvert.DeserializeObject<dynamic>(responseText);
                    return new AuthResult { Success = false, ErrorMessage = error.error?.message ?? "Username reservation failed" };
                }

                // Update cached profile
                if (cachedProfile != null)
                {
                    cachedProfile.creatorUsername = username.ToLower();
                    StoreCachedProfile(cachedProfile);
                }

                return new AuthResult { Success = true, Message = $"Username '{username}' reserved successfully!" };
            }
            catch (Exception ex)
            {
                return new AuthResult { Success = false, ErrorMessage = $"Username reservation failed: {ex.Message}" };
            }
        }

        private static async Task<AuthResult> GetUserProfile(string idToken)
        {
            try
            {
                httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", idToken);

                var response = await httpClient.GetAsync($"{FIREBASE_FUNCTIONS_BASE}/getUserProfile");
                var responseText = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return new AuthResult { Success = false, ErrorMessage = "Failed to load user profile" };
                }

                var profile = JsonConvert.DeserializeObject<Unreality3DUserProfile>(responseText);
                return new AuthResult { Success = true, Profile = profile };
            }
            catch (Exception ex)
            {
                return new AuthResult { Success = false, ErrorMessage = $"Profile loading failed: {ex.Message}" };
            }
        }

        private static bool IsValidUsername(string username)
        {
            if (string.IsNullOrWhiteSpace(username) || username.Length < 3 || username.Length > 20)
                return false;

            // Only letters, numbers, and hyphens, can't start/end with hyphen
            if (username.StartsWith("-") || username.EndsWith("-"))
                return false;

            foreach (char c in username)
            {
                if (!char.IsLetterOrDigit(c) && c != '-')
                    return false;
            }

            // Check against reserved words
            var reserved = new[] { "www", "api", "admin", "root", "support", "help", "about", "blog", "shop", "store", "unreality3d" };
            return !Array.Exists(reserved, r => r.Equals(username, StringComparison.OrdinalIgnoreCase));
        }

        private static string GetFriendlyErrorMessage(string firebaseError)
        {
            return firebaseError switch
            {
                "EMAIL_NOT_FOUND" => "No account found with this email address.",
                "INVALID_PASSWORD" => "Incorrect password. Please try again.",
                "USER_DISABLED" => "This account has been disabled.",
                "TOO_MANY_ATTEMPTS_TRY_LATER" => "Too many failed attempts. Please try again later.",
                _ => "Login failed. Please check your credentials."
            };
        }

        public static void Logout()
        {
            EditorPrefs.DeleteKey(AUTH_TOKEN_KEY);
            EditorPrefs.DeleteKey(USER_PROFILE_KEY);
            cachedProfile = null;

            if (httpClient != null)
            {
                httpClient.DefaultRequestHeaders.Authorization = null;
            }
        }

        private static void StoreAuthToken(string token)
        {
            var encryptedToken = Convert.ToBase64String(Encoding.UTF8.GetBytes(token));
            EditorPrefs.SetString(AUTH_TOKEN_KEY, encryptedToken);
        }

        private static string GetStoredAuthToken()
        {
            var encryptedToken = EditorPrefs.GetString(AUTH_TOKEN_KEY, "");
            if (string.IsNullOrEmpty(encryptedToken))
                return "";

            try
            {
                return Encoding.UTF8.GetString(Convert.FromBase64String(encryptedToken));
            }
            catch
            {
                EditorPrefs.DeleteKey(AUTH_TOKEN_KEY);
                return "";
            }
        }

        private static void StoreCachedProfile(Unreality3DUserProfile profile)
        {
            var json = JsonConvert.SerializeObject(profile);
            EditorPrefs.SetString(USER_PROFILE_KEY, json);
        }

        private static void LoadCachedProfile()
        {
            var json = EditorPrefs.GetString(USER_PROFILE_KEY, "");
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    cachedProfile = JsonConvert.DeserializeObject<Unreality3DUserProfile>(json);
                }
                catch
                {
                    EditorPrefs.DeleteKey(USER_PROFILE_KEY);
                }
            }
        }

        public static void OpenSignupPage()
        {
            Application.OpenURL("https://unreality3d.web.app/register");
        }

        public static void OpenLoginPage()
        {
            Application.OpenURL("https://unreality3d.web.app/login");
        }
    }

    [Serializable]
    public class Unreality3DUserProfile
    {
        public string userId;
        public string email;
        public string creatorUsername;
        public string displayName;
        public string userType; // visitor, creator, seller
        public bool paypalConnected;
    }

    [Serializable]
    public class FirebaseAuthResponse
    {
        public string idToken;
        public string email;
        public string refreshToken;
        public string expiresIn;
        public string localId;
    }

    [Serializable]
    public class FirebaseAuthError
    {
        public FirebaseErrorDetails error;
    }

    [Serializable]
    public class FirebaseErrorDetails
    {
        public string message;
        public int code;
    }

    [Serializable]
    public class UsernameAvailabilityResponse
    {
        public bool available;
        public string[] suggestions;
    }

    public class AuthResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string ErrorMessage { get; set; }
        public Unreality3DUserProfile Profile { get; set; }
    }
}