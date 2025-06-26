using UnityEngine;
using UnityEditor;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Collections.Generic;
using System;
using System.Text;

namespace U3D.Editor
{
    public static class GitHubTokenManager
    {
        private static string _githubToken;
        private static string _githubUsername;
        private static bool _tokenValidated = false;

        private const string TOKEN_KEY = "U3D_GitHubToken";
        private const string USERNAME_KEY = "U3D_GitHubUsername";
        private const string VALIDATED_KEY = "U3D_GitHubTokenValidated";

        public static bool HasValidToken => !string.IsNullOrEmpty(_githubToken) && _tokenValidated;
        public static string GitHubUsername => _githubUsername;
        public static string Token => _githubToken;

        static GitHubTokenManager()
        {
            LoadCredentials();
        }

        public static void SetToken(string token)
        {
            _githubToken = token;
            _tokenValidated = false;
            SaveCredentials();
        }

        public static async Task<GitHubValidationResult> ValidateAndSetToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return new GitHubValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "Token cannot be empty"
                };
            }

            try
            {
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("Authorization", $"token {token}");
                    client.DefaultRequestHeaders.Add("User-Agent", "Unreality3D-Unity-SDK");
                    client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");

                    var response = await client.GetAsync("https://api.github.com/user");
                    var responseText = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        var userData = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseText);

                        if (userData.ContainsKey("login"))
                        {
                            _githubToken = token;
                            _githubUsername = userData["login"].ToString();
                            _tokenValidated = true;
                            SaveCredentials();

                            return new GitHubValidationResult
                            {
                                IsValid = true,
                                Username = _githubUsername,
                                Message = $"Token validated successfully for user: {_githubUsername}"
                            };
                        }
                    }
                    else
                    {
                        var errorMessage = "Invalid token";

                        try
                        {
                            var errorData = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseText);
                            if (errorData.ContainsKey("message"))
                            {
                                errorMessage = errorData["message"].ToString();
                            }
                        }
                        catch
                        {
                            // Use default error message
                        }

                        return new GitHubValidationResult
                        {
                            IsValid = false,
                            ErrorMessage = errorMessage
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"GitHub token validation error: {ex.Message}");
                return new GitHubValidationResult
                {
                    IsValid = false,
                    ErrorMessage = $"Validation failed: {ex.Message}"
                };
            }

            return new GitHubValidationResult
            {
                IsValid = false,
                ErrorMessage = "Unexpected validation result"
            };
        }

        public static async Task<bool> CheckRepositoryPermissions()
        {
            if (!HasValidToken)
            {
                return false;
            }

            try
            {
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_githubToken}");
                    client.DefaultRequestHeaders.Add("User-Agent", "Unreality3D-Unity-SDK");
                    client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");

                    // Check if we can access repositories
                    var response = await client.GetAsync("https://api.github.com/user/repos?per_page=1");
                    return response.IsSuccessStatusCode;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"GitHub permissions check error: {ex.Message}");
                return false;
            }
        }

        // NEW: Unity licensing automation methods
        public static async Task<bool> SetRepositorySecret(string repositoryName, string secretName, string secretValue)
        {
            if (!HasValidToken)
            {
                Debug.LogError("No valid GitHub token available for setting repository secrets");
                return false;
            }

            try
            {
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_githubToken}");
                    client.DefaultRequestHeaders.Add("User-Agent", "Unreality3D-Unity-SDK");
                    client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
                    client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");

                    // Step 1: Get repository public key for encryption
                    var publicKeyUrl = $"https://api.github.com/repos/{_githubUsername}/{repositoryName}/actions/secrets/public-key";
                    var keyResponse = await client.GetAsync(publicKeyUrl);

                    if (!keyResponse.IsSuccessStatusCode)
                    {
                        var keyError = await keyResponse.Content.ReadAsStringAsync();
                        Debug.LogError($"Failed to get repository public key: {keyError}");
                        return false;
                    }

                    var keyData = JsonConvert.DeserializeObject<Dictionary<string, object>>(await keyResponse.Content.ReadAsStringAsync());
                    var publicKey = keyData["key"].ToString();
                    var keyId = keyData["key_id"].ToString();

                    // Step 2: Encrypt the secret value using libsodium-compatible encryption
                    var encryptedValue = EncryptSecretValue(secretValue, publicKey);

                    // Step 3: Set the repository secret
                    var secretData = new
                    {
                        encrypted_value = encryptedValue,
                        key_id = keyId
                    };

                    var json = JsonConvert.SerializeObject(secretData);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    var secretUrl = $"https://api.github.com/repos/{_githubUsername}/{repositoryName}/actions/secrets/{secretName}";
                    var secretResponse = await client.PutAsync(secretUrl, content);

                    if (secretResponse.IsSuccessStatusCode)
                    {
                        Debug.Log($"Successfully set repository secret: {secretName}");
                        return true;
                    }
                    else
                    {
                        var secretError = await secretResponse.Content.ReadAsStringAsync();
                        Debug.LogError($"Failed to set repository secret {secretName}: {secretError}");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error setting repository secret {secretName}: {ex.Message}");
                return false;
            }
        }

        public static async Task<bool> SetupUnityRepositorySecrets(string repositoryName, UnityCredentials credentials)
        {
            if (credentials == null)
            {
                Debug.LogError("Unity credentials are null");
                return false;
            }

            try
            {
                Debug.Log($"Setting up Unity repository secrets for: {repositoryName}");

                // Set UNITY_EMAIL secret
                if (!string.IsNullOrEmpty(credentials.Email))
                {
                    var emailResult = await SetRepositorySecret(repositoryName, "UNITY_EMAIL", credentials.Email);
                    if (!emailResult)
                    {
                        Debug.LogError("Failed to set UNITY_EMAIL secret");
                        return false;
                    }
                }

                // Set UNITY_PASSWORD secret
                if (!string.IsNullOrEmpty(credentials.Password))
                {
                    var passwordResult = await SetRepositorySecret(repositoryName, "UNITY_PASSWORD", credentials.Password);
                    if (!passwordResult)
                    {
                        Debug.LogError("Failed to set UNITY_PASSWORD secret");
                        return false;
                    }
                }

                // Set UNITY_AUTHENTICATOR_KEY secret (optional for 2FA)
                if (!string.IsNullOrEmpty(credentials.AuthenticatorKey))
                {
                    var authKeyResult = await SetRepositorySecret(repositoryName, "UNITY_AUTHENTICATOR_KEY", credentials.AuthenticatorKey);
                    if (!authKeyResult)
                    {
                        Debug.LogWarning("Failed to set UNITY_AUTHENTICATOR_KEY secret (this is optional for 2FA)");
                        // Don't return false here since 2FA key is optional
                    }
                }

                Debug.Log("Unity repository secrets configured successfully");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error setting up Unity repository secrets: {ex.Message}");
                return false;
            }
        }

        // Encrypt secret value using GitHub-compatible sealed box encryption
        private static string EncryptSecretValue(string secretValue, string base64PublicKey)
        {
            return GitHubSecurityHelper.EncryptForGitHubSecret(secretValue, base64PublicKey);
        }

        public static void ClearToken()
        {
            _githubToken = "";
            _githubUsername = "";
            _tokenValidated = false;

            EditorPrefs.DeleteKey(TOKEN_KEY);
            EditorPrefs.DeleteKey(USERNAME_KEY);
            EditorPrefs.DeleteKey(VALIDATED_KEY);
        }

        private static void SaveCredentials()
        {
            if (!string.IsNullOrEmpty(_githubToken))
            {
                EditorPrefs.SetString(TOKEN_KEY, _githubToken);
            }

            if (!string.IsNullOrEmpty(_githubUsername))
            {
                EditorPrefs.SetString(USERNAME_KEY, _githubUsername);
            }

            EditorPrefs.SetBool(VALIDATED_KEY, _tokenValidated);
        }

        private static void LoadCredentials()
        {
            _githubToken = EditorPrefs.GetString(TOKEN_KEY, "");
            _githubUsername = EditorPrefs.GetString(USERNAME_KEY, "");
            _tokenValidated = EditorPrefs.GetBool(VALIDATED_KEY, false);
        }
    }

    [System.Serializable]
    public class GitHubValidationResult
    {
        public bool IsValid { get; set; }
        public string Username { get; set; }
        public string Message { get; set; }
        public string ErrorMessage { get; set; }
    }

    [System.Serializable]
    public class UnityCredentials
    {
        public string Email { get; set; }
        public string Password { get; set; }
        public string AuthenticatorKey { get; set; } // Optional for 2FA
    }
}