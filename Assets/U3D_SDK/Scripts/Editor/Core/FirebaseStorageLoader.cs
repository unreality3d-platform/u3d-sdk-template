using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
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

        // ADD THESE DEBUG LINES:
        Debug.Log($"🔑 Storage Bucket: {_storageBucket}");
        Debug.Log($"🔑 ID Token Status: {(string.IsNullOrEmpty(_idToken) ? "NULL/EMPTY" : "Present")}");
        Debug.Log($"🔑 ID Token Length: {_idToken?.Length ?? 0}");
        if (!string.IsNullOrEmpty(_idToken))
        {
            Debug.Log($"🔑 ID Token (first 50 chars): {_idToken.Substring(0, Math.Min(50, _idToken.Length))}...");
        }
    }

    public async Task<bool> UploadBuildToStorage(string buildPath, string creatorUsername, string projectName)
    {
        try
        {
            Debug.Log($"🔄 Starting Firebase Storage upload for {projectName}...");

            // 1. Collect all files from build directory
            var buildFiles = CollectBuildFiles(buildPath);
            Debug.Log($"📦 Found {buildFiles.Count} files to upload");

            // 2. Upload files with throttling (max 3 concurrent uploads)
            const int maxConcurrentUploads = 3;
            var semaphore = new SemaphoreSlim(maxConcurrentUploads, maxConcurrentUploads);
            var uploadTasks = new List<Task<bool>>();

            foreach (var file in buildFiles)
            {
                var uploadTask = UploadFileWithThrottling(file, creatorUsername, projectName, semaphore);
                uploadTasks.Add(uploadTask);
            }

            // Wait for all uploads to complete
            var results = await Task.WhenAll(uploadTasks);

            // DEBUG: Analyze results by file type
            Debug.Log($"🔍 UPLOAD RESULTS ANALYSIS:");
            var wasmFiles = buildFiles.Where(f => f.StoragePath.EndsWith(".wasm")).ToList();
            var jsFiles = buildFiles.Where(f => f.StoragePath.EndsWith(".js")).ToList();
            var dataFiles = buildFiles.Where(f => f.StoragePath.EndsWith(".data")).ToList();

            var wasmResults = results.Take(wasmFiles.Count).ToArray();
            var jsResults = results.Skip(wasmFiles.Count).Take(jsFiles.Count).ToArray();
            var dataResults = results.Skip(wasmFiles.Count + jsFiles.Count).Take(dataFiles.Count).ToArray();

            Debug.Log($"📊 .wasm upload success rate: {wasmResults.Count(r => r)}/{wasmResults.Length}");
            Debug.Log($"📊 .js upload success rate: {jsResults.Count(r => r)}/{jsResults.Length}");
            Debug.Log($"📊 .data upload success rate: {dataResults.Count(r => r)}/{dataResults.Length}");

            // Check if all uploads succeeded
            var failedCount = results.Count(r => !r);
            if (failedCount > 0)
            {
                Debug.LogError($"❌ {failedCount} out of {buildFiles.Count} file uploads failed");
                return false;
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

    public async Task<DeploymentResult> UploadBuildToStorageWithIntent(string buildPath, string creatorUsername, string baseProjectName, string deploymentIntent)
    {
        try
        {
            Debug.Log($"🔄 Starting Firebase Storage upload for {baseProjectName} with intent: {deploymentIntent}...");

            var buildFiles = CollectBuildFiles(buildPath);
            Debug.Log($"📦 Found {buildFiles.Count} files to upload");

            // Upload files to temporary path first
            const int maxConcurrentUploads = 3;
            var semaphore = new SemaphoreSlim(maxConcurrentUploads, maxConcurrentUploads);
            var uploadTasks = new List<Task<bool>>();

            foreach (var file in buildFiles)
            {
                var uploadTask = UploadFileWithThrottling(file, creatorUsername, baseProjectName, semaphore);
                uploadTasks.Add(uploadTask);
            }

            var results = await Task.WhenAll(uploadTasks);
            var failedCount = results.Count(r => !r);

            if (failedCount > 0)
            {
                Debug.LogError($"❌ {failedCount} out of {buildFiles.Count} file uploads failed");
                return new DeploymentResult { Success = false, ErrorMessage = "File upload failed" };
            }

            Debug.Log("✅ All files uploaded successfully to Firebase Storage");

            // Trigger GitHub deployment with intent
            return await TriggerGitHubDeploymentWithIntent(creatorUsername, baseProjectName, deploymentIntent, buildFiles);
        }
        catch (Exception ex)
        {
            Debug.LogError($"🚨 Firebase Storage upload failed: {ex.Message}");
            return new DeploymentResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    private async Task<DeploymentResult> TriggerGitHubDeploymentWithIntent(string creatorUsername, string baseProjectName, string deploymentIntent, List<BuildFileInfo> files)
    {
        try
        {
            Debug.Log("🚀 Triggering GitHub Pages deployment...");

            var fileList = files.ConvertAll(f => f.StoragePath);

            var deploymentRequest = new Dictionary<string, object>
        {
            { "project", baseProjectName },
            { "creatorUsername", creatorUsername },
            { "githubOwner", GitHubTokenManager.GitHubUsername },
            { "fileList", fileList },
            { "githubToken", GitHubTokenManager.Token },
            { "deploymentIntent", deploymentIntent }
        };

            var result = await CallFirebaseFunction("deployFromStorage", deploymentRequest);

            Debug.Log($"🔍 Function response: {JsonConvert.SerializeObject(result)}");

            if (result.ContainsKey("success") && (bool)result["success"])
            {
                var actualProjectName = result.ContainsKey("actualProjectName") ? result["actualProjectName"].ToString() : baseProjectName;
                var liveUrl = result.ContainsKey("url") ? result["url"].ToString() : "";

                Debug.Log($"🎉 Deployment successful! Live at: {liveUrl}");
                Debug.Log($"📝 Actual project name: {actualProjectName}");

                return new DeploymentResult
                {
                    Success = true,
                    ActualProjectName = actualProjectName,
                    Url = liveUrl
                };
            }
            else
            {
                var errorMessage = result.ContainsKey("error") ? result["error"].ToString() : "Unknown deployment error";
                Debug.LogError($"❌ Deployment failed: {errorMessage}");
                return new DeploymentResult { Success = false, ErrorMessage = errorMessage };
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"❌ GitHub deployment trigger failed: {ex.Message}");
            return new DeploymentResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    public class DeploymentResult
    {
        public bool Success { get; set; }
        public string ActualProjectName { get; set; }
        public string Url { get; set; }
        public string ErrorMessage { get; set; }
    }

    private async Task<bool> UploadFileWithThrottling(BuildFileInfo file, string creatorUsername, string projectName, SemaphoreSlim semaphore)
    {
        await semaphore.WaitAsync(); // Wait for available slot
        try
        {
            return await UploadFileToStorage(file, creatorUsername, projectName);
        }
        finally
        {
            semaphore.Release(); // Release the slot
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

        // DEBUG: Log file collection details
        Debug.Log($"📁 COLLECTED FILES BREAKDOWN:");
        var wasmFiles = files.Where(f => f.StoragePath.EndsWith(".wasm")).ToList();
        var jsFiles = files.Where(f => f.StoragePath.EndsWith(".js")).ToList();
        var dataFiles = files.Where(f => f.StoragePath.EndsWith(".data")).ToList();
        var htmlFiles = files.Where(f => f.StoragePath.EndsWith(".html")).ToList();
        var otherFiles = files.Where(f => !f.StoragePath.EndsWith(".wasm") && !f.StoragePath.EndsWith(".js") && !f.StoragePath.EndsWith(".data") && !f.StoragePath.EndsWith(".html")).ToList();

        Debug.Log($"📁 Found .wasm files: {wasmFiles.Count}");
        foreach (var file in wasmFiles)
            Debug.Log($"   📄 {file.StoragePath} ({file.Size / 1024.0 / 1024.0:F2} MB)");

        Debug.Log($"📁 Found .js files: {jsFiles.Count}");
        foreach (var file in jsFiles)
            Debug.Log($"   📄 {file.StoragePath} ({file.Size / 1024.0:F2} KB)");

        Debug.Log($"📁 Found .data files: {dataFiles.Count}");
        foreach (var file in dataFiles)
            Debug.Log($"   📄 {file.StoragePath} ({file.Size / 1024.0 / 1024.0:F2} MB)");

        Debug.Log($"📁 Found .html files: {htmlFiles.Count}");
        foreach (var file in htmlFiles)
            Debug.Log($"   📄 {file.StoragePath} ({file.Size / 1024.0:F2} KB)");

        Debug.Log($"📁 Found other files: {otherFiles.Count}");
        foreach (var file in otherFiles)
            Debug.Log($"   📄 {file.StoragePath} ({file.Size / 1024.0:F2} KB)");

        return files;
    }

    private async Task<bool> UploadFileToStorage(BuildFileInfo file, string creatorUsername, string projectName)
    {
        try
        {
            Debug.Log($"⬆️ Uploading {file.StoragePath} ({file.Size / 1024.0 / 1024.0:F2} MB)...");

            Debug.Log($"🔍 Auth Header: Bearer {(_idToken?.Length > 20 ? _idToken.Substring(0, 20) + "..." : _idToken ?? "NULL")}");

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
                { "creatorUsername", creatorUsername },  // For professional URL
                { "githubOwner", GitHubTokenManager.GitHubUsername },  // For GitHub operations
                { "fileList", fileList },
                { "githubToken", GitHubTokenManager.Token }
            };

            // Use reflection to access the private CallFirebaseFunction method (same pattern as PublishTab)
            var result = await CallFirebaseFunction("deployFromStorage", deploymentRequest);

            Debug.Log($"🔍 Function response: {JsonConvert.SerializeObject(result)}");

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