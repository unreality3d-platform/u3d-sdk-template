using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using U3D.Editor;
using UnityEngine;

public class FirebaseStorageUploader
{
    private readonly string _storageBucket;
    private readonly string _idToken;
    private readonly HttpClient _httpClient;

    public FirebaseStorageUploader(string storageBucket, string idToken)
    {
        _storageBucket = storageBucket;
        _idToken = idToken;
        _httpClient = new HttpClient() { Timeout = TimeSpan.FromMinutes(10) };
    }

    public async Task<bool> UploadBuildToStorage(string buildPath, string creatorUsername, string projectName)
    {
        try
        {
            Debug.Log($"🔄 Starting Firebase Storage upload for {projectName}...");

            // 1. Collect all files from build directory
            var buildFiles = CollectBuildFiles(buildPath);
            Debug.Log($"📦 Found {buildFiles.Count} files to upload");

            // 2. Upload each file to Firebase Storage
            var uploadTasks = new List<Task<bool>>();
            foreach (var file in buildFiles)
            {
                var uploadTask = UploadFileToStorage(file, creatorUsername, projectName);
                uploadTasks.Add(uploadTask);
            }

            // Wait for all uploads to complete
            var results = await Task.WhenAll(uploadTasks);

            // Check if all uploads succeeded
            foreach (var result in results)
            {
                if (!result)
                {
                    Debug.LogError("❌ One or more file uploads failed");
                    return false;
                }
            }

            Debug.Log("✅ All files uploaded successfully to Firebase Storage");

            // 3. Call Firebase Function to trigger GitHub deployment
            return await TriggerGitHubDeployment(creatorUsername, projectName, buildFiles);
        }
        catch (Exception ex)
        {
            Debug.LogError($"🚨 Firebase Storage upload failed: {ex.Message}");
            return false;
        }
    }

    private List<BuildFileInfo> CollectBuildFiles(string buildPath)
    {
        var files = new List<BuildFileInfo>();
        var buildDirectory = new DirectoryInfo(buildPath);

        foreach (var file in buildDirectory.GetFiles("*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(buildPath, file.FullName).Replace('\\', '/');

            files.Add(new BuildFileInfo
            {
                LocalPath = file.FullName,
                StoragePath = relativePath,
                Size = file.Length,
                ContentType = GetContentType(file.Extension)
            });
        }

        return files;
    }

    private async Task<bool> UploadFileToStorage(BuildFileInfo file, string creatorUsername, string projectName)
    {
        try
        {
            Debug.Log($"⬆️ Uploading {file.StoragePath} ({file.Size / 1024.0 / 1024.0:F2} MB)...");

            // Read file content
            var fileBytes = await File.ReadAllBytesAsync(file.LocalPath);

            // Construct Firebase Storage URL
            var storageUrl = $"https://firebasestorage.googleapis.com/v0/b/{_storageBucket}/o" +
                           $"?name=creators/{creatorUsername}/builds/{projectName}/{file.StoragePath}";

            // Create HTTP request
            using var content = new ByteArrayContent(fileBytes);
            content.Headers.Add("Content-Type", file.ContentType);

            using var request = new HttpRequestMessage(HttpMethod.Post, storageUrl);
            request.Content = content;
            request.Headers.Add("Authorization", $"Bearer {_idToken}");

            // Send request
            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                Debug.Log($"✅ Uploaded {file.StoragePath}");
                return true;
            }
            else
            {
                var errorText = await response.Content.ReadAsStringAsync();
                Debug.LogError($"❌ Upload failed for {file.StoragePath}: {response.StatusCode} - {errorText}");
                return false;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"❌ Exception uploading {file.StoragePath}: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> TriggerGitHubDeployment(string creatorUsername, string projectName, List<BuildFileInfo> files)
    {
        try
        {
            Debug.Log("🚀 Triggering GitHub Pages deployment...");

            var fileList = files.ConvertAll(f => f.StoragePath);

            var deploymentRequest = new Dictionary<string, object>
            {
                { "project", projectName },
                { "creatorUsername", creatorUsername },
                { "fileList", fileList },
                { "githubToken", GitHubTokenManager.Token }
            };

            // Use reflection to access the private CallFirebaseFunction method (same pattern as PublishTab)
            var result = await CallFirebaseFunction("deployFromStorage", deploymentRequest);

            if (result.ContainsKey("url"))
            {
                var liveUrl = result["url"].ToString();
                Debug.Log($"🎉 Deployment successful! Live at: {liveUrl}");
                return true;
            }
            else
            {
                Debug.LogError("❌ Deployment failed - no URL returned");
                return false;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"❌ GitHub deployment trigger failed: {ex.Message}");
            return false;
        }
    }

    private static async Task<Dictionary<string, object>> CallFirebaseFunction(string functionName, Dictionary<string, object> data)
    {
        // Use reflection to access the private method (same pattern as your PublishTab)
        var method = typeof(U3DAuthenticator).GetMethod("CallFirebaseFunction",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        if (method == null)
        {
            throw new Exception("CallFirebaseFunction method not found in U3DAuthenticator");
        }

        var task = method.Invoke(null, new object[] { functionName, data }) as Task<Dictionary<string, object>>;
        return await task;
    }

    private string GetContentType(string extension)
    {
        return extension.ToLower() switch
        {
            ".html" => "text/html",
            ".js" => "application/javascript",
            ".css" => "text/css",
            ".wasm" => "application/wasm",
            ".data" => "application/octet-stream",
            ".json" => "application/json",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            _ => "application/octet-stream"
        };
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}

public class BuildFileInfo
{
    public string LocalPath { get; set; }
    public string StoragePath { get; set; }
    public long Size { get; set; }
    public string ContentType { get; set; }
}