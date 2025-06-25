using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

namespace U3D.Editor
{
    public static class GitHubAPI
    {
        private const string GITHUB_API_BASE = "https://api.github.com";
        private const string TEMPLATE_REPO_OWNER = "unreality3d-platform";
        private const string TEMPLATE_REPO_NAME = "u3d-sdk-template";

        public static async Task<GitHubRepositoryResult> CreateRepository(string repositoryName, string description = "")
        {
            if (!GitHubTokenManager.HasValidToken)
            {
                return new GitHubRepositoryResult
                {
                    Success = false,
                    ErrorMessage = "No valid GitHub token available"
                };
            }

            try
            {
                using (var client = CreateAuthenticatedClient())
                {
                    var repoData = new
                    {
                        name = repositoryName,
                        description = string.IsNullOrEmpty(description) ? $"Unity project created with Unreality3D SDK" : description,
                        @private = false,
                        has_issues = true,
                        has_projects = false,
                        has_wiki = false,
                        auto_init = false
                    };

                    var json = JsonConvert.SerializeObject(repoData);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    var response = await client.PostAsync($"{GITHUB_API_BASE}/user/repos", content);
                    var responseText = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        var result = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseText);

                        return new GitHubRepositoryResult
                        {
                            Success = true,
                            RepositoryName = repositoryName,
                            FullName = result.ContainsKey("full_name") ? result["full_name"].ToString() : $"{GitHubTokenManager.GitHubUsername}/{repositoryName}",
                            CloneUrl = result.ContainsKey("clone_url") ? result["clone_url"].ToString() : "",
                            SshUrl = result.ContainsKey("ssh_url") ? result["ssh_url"].ToString() : "",
                            HtmlUrl = result.ContainsKey("html_url") ? result["html_url"].ToString() : "",
                            Message = "Repository created successfully"
                        };
                    }
                    else
                    {
                        var errorMessage = await ParseGitHubError(responseText);
                        return new GitHubRepositoryResult
                        {
                            Success = false,
                            ErrorMessage = errorMessage
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"GitHub repository creation error: {ex.Message}");
                return new GitHubRepositoryResult
                {
                    Success = false,
                    ErrorMessage = $"Repository creation failed: {ex.Message}"
                };
            }
        }

        public static async Task<GitHubRepositoryResult> CopyFromTemplate(string newRepositoryName, string description = "")
        {
            if (!GitHubTokenManager.HasValidToken)
            {
                return new GitHubRepositoryResult
                {
                    Success = false,
                    ErrorMessage = "No valid GitHub token available"
                };
            }

            try
            {
                using (var client = CreateAuthenticatedClient())
                {
                    var templateData = new
                    {
                        name = newRepositoryName,
                        description = string.IsNullOrEmpty(description) ? $"Unity project created with Unreality3D SDK" : description,
                        @private = false,
                        include_all_branches = false
                    };

                    var json = JsonConvert.SerializeObject(templateData);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    var templateUrl = $"{GITHUB_API_BASE}/repos/{TEMPLATE_REPO_OWNER}/{TEMPLATE_REPO_NAME}/generate";
                    var response = await client.PostAsync(templateUrl, content);
                    var responseText = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        var result = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseText);

                        return new GitHubRepositoryResult
                        {
                            Success = true,
                            RepositoryName = newRepositoryName,
                            FullName = result.ContainsKey("full_name") ? result["full_name"].ToString() : $"{GitHubTokenManager.GitHubUsername}/{newRepositoryName}",
                            CloneUrl = result.ContainsKey("clone_url") ? result["clone_url"].ToString() : "",
                            SshUrl = result.ContainsKey("ssh_url") ? result["ssh_url"].ToString() : "",
                            HtmlUrl = result.ContainsKey("html_url") ? result["html_url"].ToString() : "",
                            Message = "Repository created from template successfully"
                        };
                    }
                    else
                    {
                        var errorMessage = await ParseGitHubError(responseText);
                        return new GitHubRepositoryResult
                        {
                            Success = false,
                            ErrorMessage = errorMessage
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"GitHub template copy error: {ex.Message}");
                return new GitHubRepositoryResult
                {
                    Success = false,
                    ErrorMessage = $"Template copy failed: {ex.Message}"
                };
            }
        }

        public static async Task<bool> CheckRepositoryExists(string repositoryName)
        {
            if (!GitHubTokenManager.HasValidToken)
            {
                return false;
            }

            try
            {
                using (var client = CreateAuthenticatedClient())
                {
                    var response = await client.GetAsync($"{GITHUB_API_BASE}/repos/{GitHubTokenManager.GitHubUsername}/{repositoryName}");
                    return response.IsSuccessStatusCode;
                }
            }
            catch
            {
                return false;
            }
        }

        public static async Task<string> GenerateUniqueRepositoryName(string baseName)
        {
            var safeName = SanitizeRepositoryName(baseName);
            var uniqueName = safeName;
            var counter = 1;

            while (await CheckRepositoryExists(uniqueName))
            {
                uniqueName = $"{safeName}-{counter}";
                counter++;

                if (counter > 100) // Prevent infinite loop
                {
                    uniqueName = $"{safeName}-{DateTime.Now.Ticks}";
                    break;
                }
            }

            return uniqueName;
        }

        public static string SanitizeRepositoryName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "unity-project";
            }

            // Convert to lowercase and replace spaces/special chars with hyphens
            var sanitized = name.ToLower()
                .Replace(" ", "-")
                .Replace("_", "-")
                .Replace(".", "-");

            // Remove invalid characters
            var validChars = "abcdefghijklmnopqrstuvwxyz0123456789-";
            var result = new StringBuilder();

            foreach (char c in sanitized)
            {
                if (validChars.Contains(c))
                {
                    result.Append(c);
                }
            }

            var finalName = result.ToString().Trim('-');

            // Ensure it starts with a letter or number
            if (finalName.Length > 0 && finalName[0] == '-')
            {
                finalName = finalName.Substring(1);
            }

            // Ensure minimum length
            if (finalName.Length < 3)
            {
                finalName = "unity-project";
            }

            // Ensure maximum length (GitHub limit is 100 chars)
            if (finalName.Length > 80)
            {
                finalName = finalName.Substring(0, 80).TrimEnd('-');
            }

            return finalName;
        }

        private static HttpClient CreateAuthenticatedClient()
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", $"token {GitHubTokenManager.Token}");
            client.DefaultRequestHeaders.Add("User-Agent", "Unreality3D-Unity-SDK");
            client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
            client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
            return client;
        }

        private static async Task<string> ParseGitHubError(string responseText)
        {
            try
            {
                var errorData = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseText);

                if (errorData.ContainsKey("message"))
                {
                    var message = errorData["message"].ToString();

                    // Handle common GitHub error messages
                    if (message.Contains("name already exists"))
                    {
                        return "Repository name already exists. Please try a different name.";
                    }
                    else if (message.Contains("Bad credentials"))
                    {
                        return "Invalid GitHub token. Please check your token and try again.";
                    }
                    else if (message.Contains("rate limit"))
                    {
                        return "GitHub API rate limit exceeded. Please wait a moment and try again.";
                    }

                    return message;
                }

                if (errorData.ContainsKey("errors"))
                {
                    var errors = JsonConvert.DeserializeObject<object[]>(errorData["errors"].ToString());
                    if (errors.Length > 0)
                    {
                        var firstError = JsonConvert.DeserializeObject<Dictionary<string, object>>(errors[0].ToString());
                        if (firstError.ContainsKey("message"))
                        {
                            return firstError["message"].ToString();
                        }
                    }
                }
            }
            catch
            {
                // If we can't parse the error, return the raw response
            }

            return $"GitHub API error: {responseText}";
        }
    }

    [System.Serializable]
    public class GitHubRepositoryResult
    {
        public bool Success { get; set; }
        public string RepositoryName { get; set; }
        public string FullName { get; set; }
        public string CloneUrl { get; set; }
        public string SshUrl { get; set; }
        public string HtmlUrl { get; set; }
        public string Message { get; set; }
        public string ErrorMessage { get; set; }
    }
}