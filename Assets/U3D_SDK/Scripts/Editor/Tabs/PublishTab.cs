using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System.Collections.Generic;
using Newtonsoft.Json;
using Firebase.Functions;
using ICSharpCode.SharpZipLib.Zip;

namespace U3D.Editor
{
    public class PublishTab : ICreatorTab
    {
        public string TabName => "Publish";
        public bool IsComplete { get; private set; }
        public System.Action<int> OnRequestTabSwitch { get; set; }

        private PublishStep currentStep = PublishStep.Ready;
        private string publishUrl = "";
        private string cachedProductName;
        private bool githubConnected = false;
        private bool projectBuilt = false;
        private bool deploymentComplete = false;
        private Vector2 scrollPosition;
        private string currentStatus = "";
        private bool isPublishing = false;

        private enum PublishStep
        {
            Ready,
            BuildingLocally,
            CreatingRepository,
            DeployingToGitHub,
            Complete
        }

        public void Initialize()
        {
            // Cache product name on main thread to avoid threading issues
            cachedProductName = Application.productName;

            publishUrl = EditorPrefs.GetString("U3D_PublishedURL", "");

            if (!string.IsNullOrEmpty(publishUrl))
            {
                IsComplete = true;
                currentStep = PublishStep.Complete;
                githubConnected = true;
                projectBuilt = true;
                deploymentComplete = true;
            }
        }

        private void ResetPublishState()
        {
            EditorPrefs.DeleteKey("U3D_PublishedURL");
            publishUrl = "";
            githubConnected = false;
            projectBuilt = false;
            deploymentComplete = false;
            currentStep = PublishStep.Ready;
            IsComplete = false;
            currentStatus = "";
            isPublishing = false;

            Debug.Log("Publish state reset - ready to publish again");
        }

        public void DrawTab()
        {
            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("Publish Your Content", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Share your creation with the world! This will make your content live on the internet.", MessageType.Info);
            EditorGUILayout.Space(15);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            // Check prerequisites
            if (!CanPublish())
            {
                DrawPrerequisites();
            }
            else if (currentStep == PublishStep.Ready && !isPublishing)
            {
                DrawReadyToPublish();
            }

            EditorGUILayout.Space(10);

            if (isPublishing || currentStep != PublishStep.Ready)
            {
                DrawPublishingSteps();
            }

            if (!string.IsNullOrEmpty(currentStatus))
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox(currentStatus, MessageType.Info);
            }

            if (currentStep == PublishStep.Complete)
            {
                DrawSuccessSection();
            }

            EditorGUILayout.EndScrollView();
        }

        private bool CanPublish()
        {
            return U3DAuthenticator.IsLoggedIn &&
                   !string.IsNullOrEmpty(U3DAuthenticator.CreatorUsername) &&
                   GitHubTokenManager.HasValidToken;
        }

        private void DrawPrerequisites()
        {
            EditorGUILayout.LabelField("Prerequisites", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            if (!U3DAuthenticator.IsLoggedIn)
            {
                EditorGUILayout.HelpBox("❌ Please complete authentication in the Setup tab first.", MessageType.Warning);
                if (GUILayout.Button("Go to Setup Tab"))
                {
                    OnRequestTabSwitch?.Invoke(0);
                }
            }
            else if (string.IsNullOrEmpty(U3DAuthenticator.CreatorUsername))
            {
                EditorGUILayout.HelpBox("❌ Please reserve your creator username in the Setup tab first.", MessageType.Warning);
                if (GUILayout.Button("Go to Setup Tab"))
                {
                    OnRequestTabSwitch?.Invoke(0);
                }
            }
            else if (!GitHubTokenManager.HasValidToken)
            {
                EditorGUILayout.HelpBox("❌ GitHub token not configured. Please set up your GitHub token in the Setup tab.", MessageType.Warning);
                if (GUILayout.Button("Go to Setup Tab"))
                {
                    OnRequestTabSwitch?.Invoke(0);
                }
            }
        }

        private void DrawReadyToPublish()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Ready to go?", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("This will:", EditorStyles.label);
            EditorGUILayout.LabelField("• Build your project for WebGL", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("• Deploy to the web automatically", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("• Give you a professional URL that's ready to share!", EditorStyles.miniLabel);
            EditorGUILayout.Space(10);

            if (GUILayout.Button("Make It Live!", GUILayout.Height(50)))
            {
                // Use fire-and-forget pattern for UI event handler
                _ = StartFirebasePublishProcessAsync();
            }

            EditorGUILayout.EndVertical();
        }

        private async System.Threading.Tasks.Task StartFirebasePublishProcessAsync()
        {
            try
            {
                await StartFirebasePublishProcess();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Firebase async publishing failed: {ex.Message}");
                EditorUtility.DisplayDialog("Publishing Failed", $"There was an error: {ex.Message}", "OK");

                currentStep = PublishStep.Ready;
                currentStatus = $"Publishing failed: {ex.Message}";

                // Reset states
                githubConnected = false;
                projectBuilt = false;
                deploymentComplete = false;
                isPublishing = false;
            }
        }

        private void DrawPublishingSteps()
        {
            EditorGUILayout.LabelField("Publishing Progress", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            DrawStep("Local Unity Build",
                projectBuilt,
                currentStep == PublishStep.BuildingLocally,
                "✓ Unity WebGL build completed",
                "🔨 Building Unity WebGL project locally...");

            DrawStep("GitHub Repository",
                githubConnected,
                currentStep == PublishStep.CreatingRepository,
                "✓ GitHub repository created",
                "🔗 Creating GitHub repository...");

            DrawStep("Deploy to Web",
                deploymentComplete,
                currentStep == PublishStep.DeployingToGitHub,
                "✓ Your content is now live!",
                "🚀 Deploying to GitHub Pages...");
        }

        private void DrawStep(string stepName, bool isComplete, bool isActive, string completeMessage, string activeMessage)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            if (isComplete)
            {
                EditorGUILayout.LabelField("🟢", GUILayout.Width(25));
                EditorGUILayout.LabelField(completeMessage, EditorStyles.boldLabel);
            }
            else if (isActive)
            {
                EditorGUILayout.LabelField("⏳", GUILayout.Width(25));
                EditorGUILayout.LabelField(activeMessage);
            }
            else
            {
                EditorGUILayout.LabelField("⏸️", GUILayout.Width(25));
                EditorGUILayout.LabelField($"Waiting: {stepName}");
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(3);
        }

        private async System.Threading.Tasks.Task StartFirebasePublishProcess()
        {
            isPublishing = true;

            try
            {
                // Step 1: Build Unity WebGL Project Locally
                currentStep = PublishStep.BuildingLocally;
                currentStatus = "Building Unity WebGL project locally...";

                var buildResult = await BuildUnityProjectLocally();
                if (!buildResult.Success)
                {
                    throw new System.Exception(buildResult.ErrorMessage);
                }

                projectBuilt = true;
                currentStatus = "Unity build completed successfully";

                // Step 2: Create ZIP package and prepare for Firebase deployment
                currentStep = PublishStep.CreatingRepository;
                currentStatus = "Preparing build for Firebase Cloud Functions...";

                var zipResult = await CreateBuildZipPackage(buildResult.BuildPath);
                if (!zipResult.Success)
                {
                    throw new System.Exception(zipResult.ErrorMessage);
                }

                githubConnected = true;
                currentStatus = "Build package prepared successfully";

                // Step 3: Deploy via Firebase Cloud Functions
                currentStep = PublishStep.DeployingToGitHub;
                currentStatus = "Deploying via Firebase Cloud Functions...";

                var deployResult = await DeployViaFirebaseCloudFunctions(zipResult.ZipData);
                if (!deployResult.Success)
                {
                    throw new System.Exception(deployResult.ErrorMessage);
                }

                deploymentComplete = true;

                // Complete
                currentStep = PublishStep.Complete;
                IsComplete = true;

                var creatorUsername = U3DAuthenticator.CreatorUsername;
                var projectName = deployResult.ProjectName ?? GitHubAPI.SanitizeRepositoryName(cachedProductName);
                publishUrl = deployResult.ProfessionalUrl ?? $"https://{creatorUsername}.unreality3d.com/{projectName}/";
                EditorPrefs.SetString("U3D_PublishedURL", publishUrl);

                currentStatus = "Publishing completed successfully!";

                ShowDeploymentSummary(deployResult.RepositoryName ?? projectName);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Firebase publishing failed: {ex.Message}");
                EditorUtility.DisplayDialog("Publishing Failed", $"There was an error: {ex.Message}", "OK");

                currentStep = PublishStep.Ready;
                currentStatus = $"Publishing failed: {ex.Message}";

                // Reset states
                githubConnected = false;
                projectBuilt = false;
                deploymentComplete = false;
            }
            finally
            {
                isPublishing = false;
            }
        }

        private async Task<UnityBuildResult> BuildUnityProjectLocally()
        {
            try
            {
                // Validate build requirements
                if (!UnityBuildHelper.ValidateBuildRequirements())
                {
                    return new UnityBuildResult
                    {
                        Success = false,
                        ErrorMessage = "Build requirements not met. Please check the Console for details."
                    };
                }

                // Build WebGL to temporary directory
                var tempBuildPath = Path.Combine(Path.GetTempPath(), "U3D_WebGL_Build", System.Guid.NewGuid().ToString());
                var buildResult = await UnityBuildHelper.BuildWebGL(tempBuildPath, (status) =>
                {
                    currentStatus = status;
                });

                return buildResult;
            }
            catch (System.Exception ex)
            {
                return new UnityBuildResult
                {
                    Success = false,
                    ErrorMessage = $"Local build failed: {ex.Message}"
                };
            }
        }

        private async Task<ZipPackageResult> CreateBuildZipPackage(string buildPath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    currentStatus = "Creating ZIP package for Firebase deployment...";

                    // Create temporary ZIP file
                    var tempZipPath = Path.Combine(Path.GetTempPath(), $"unity_build_{Guid.NewGuid()}.zip");

                    using (var fileStream = new FileStream(tempZipPath, FileMode.Create))
                    using (var zipStream = new ZipOutputStream(fileStream))
                    {
                        zipStream.SetLevel(6); // Compression level (0-9)

                        // Add all files from build directory recursively
                        AddDirectoryToZip(zipStream, buildPath, "");
                    }

                    // Read ZIP file as base64
                    var zipBytes = File.ReadAllBytes(tempZipPath);
                    var base64Zip = Convert.ToBase64String(zipBytes);

                    // Cleanup temp file
                    File.Delete(tempZipPath);

                    Debug.Log($"Created ZIP package: {zipBytes.Length} bytes");

                    return new ZipPackageResult
                    {
                        Success = true,
                        ZipData = base64Zip
                    };
                }
                catch (System.Exception ex)
                {
                    return new ZipPackageResult
                    {
                        Success = false,
                        ErrorMessage = $"ZIP package creation failed: {ex.Message}"
                    };
                }
            });
        }

        private void AddDirectoryToZip(ZipOutputStream zipStream, string directoryPath, string relativeBasePath)
        {
            var dirInfo = new DirectoryInfo(directoryPath);

            // Add all files in current directory
            foreach (var file in dirInfo.GetFiles())
            {
                var entryPath = string.IsNullOrEmpty(relativeBasePath) ? file.Name : $"{relativeBasePath}/{file.Name}";
                var entry = new ZipEntry(entryPath)
                {
                    DateTime = file.LastWriteTime,
                    Size = file.Length
                };

                zipStream.PutNextEntry(entry);

                using (var fileStream = file.OpenRead())
                {
                    fileStream.CopyTo(zipStream);
                }

                zipStream.CloseEntry();
            }

            // Add all subdirectories recursively
            foreach (var subDir in dirInfo.GetDirectories())
            {
                var subRelativePath = string.IsNullOrEmpty(relativeBasePath) ? subDir.Name : $"{relativeBasePath}/{subDir.Name}";
                AddDirectoryToZip(zipStream, subDir.FullName, subRelativePath);
            }
        }

        private async Task<FirebaseDeployResult> DeployViaFirebaseCloudFunctions(string buildZipBase64)
        {
            try
            {
                currentStatus = "Calling Firebase Cloud Functions for deployment...";

                // Prepare deployment data
                var repositoryName = await GitHubAPI.GenerateUniqueRepositoryName(GitHubAPI.SanitizeRepositoryName(cachedProductName));
                var deploymentData = new Dictionary<string, object>
                {
                    { "buildZip", buildZipBase64 },
                    { "repositoryName", repositoryName },
                    { "githubToken", GitHubTokenManager.Token },
                    { "creatorUsername", U3DAuthenticator.CreatorUsername }
                };

                Debug.Log($"Calling deployUnityBuild Firebase Function for repository: {repositoryName}");

                // Call Firebase Cloud Function
                var function = FirebaseFunctions.DefaultInstance.GetHttpsCallable("deployUnityBuild");
                var task = function.CallAsync(deploymentData);

                // Wait for completion with timeout
                var timeoutTask = Task.Delay(TimeSpan.FromMinutes(5)); // 5 minute timeout
                var completedTask = await Task.WhenAny(task, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    throw new Exception("Firebase deployment timed out after 5 minutes");
                }

                if (task.IsFaulted)
                {
                    var innerException = task.Exception?.InnerException;
                    throw new Exception($"Firebase function error: {innerException?.Message ?? task.Exception?.Message}");
                }

                if (task.IsCanceled)
                {
                    throw new Exception("Firebase deployment was canceled");
                }

                // Parse result
                var result = task.Result;
                var resultData = result.Data as Dictionary<string, object>;

                if (resultData == null)
                {
                    throw new Exception("Invalid response from Firebase function");
                }

                var success = resultData.ContainsKey("success") && (bool)resultData["success"];

                if (!success)
                {
                    var errorMessage = resultData.ContainsKey("message") ? resultData["message"].ToString() : "Unknown error";
                    throw new Exception($"Firebase deployment failed: {errorMessage}");
                }

                Debug.Log("Firebase deployment completed successfully!");

                return new FirebaseDeployResult
                {
                    Success = true,
                    RepositoryName = repositoryName,
                    ProjectName = resultData.ContainsKey("projectName") ? resultData["projectName"].ToString() : repositoryName,
                    ProfessionalUrl = resultData.ContainsKey("professionalUrl") ? resultData["professionalUrl"].ToString() : null,
                    Message = resultData.ContainsKey("message") ? resultData["message"].ToString() : "Deployment successful"
                };
            }
            catch (Exception ex)
            {
                Debug.LogError($"Firebase deployment error: {ex.Message}");
                return new FirebaseDeployResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        [System.Serializable]
        public class ZipPackageResult
        {
            public bool Success { get; set; }
            public string ZipData { get; set; }
            public string ErrorMessage { get; set; }
        }

        [System.Serializable]
        public class FirebaseDeployResult
        {
            public bool Success { get; set; }
            public string RepositoryName { get; set; }
            public string ProjectName { get; set; }
            public string ProfessionalUrl { get; set; }
            public string Message { get; set; }
            public string ErrorMessage { get; set; }
        }

        private void ShowDeploymentSummary(string repositoryName)
        {
            string summaryMessage = "🎉 Publishing completed successfully!\n\n";
            summaryMessage += $"🌐 Your URL: {publishUrl}\n\n";
            summaryMessage += "🔧 Build Configuration:\n";
            summaryMessage += "• Unity build: Local (using your Unity license)\n";
            summaryMessage += "• Repository: Creator-owned GitHub repository\n";
            summaryMessage += "• Hosting: GitHub Pages (unlimited bandwidth)\n";
            summaryMessage += "• Professional URL: Unreality3D Load Balancer\n";
            summaryMessage += "\n💡 Next steps:\n";
            summaryMessage += "• Your content is live and accessible\n";
            summaryMessage += "• Push changes to trigger new deployments\n";
            summaryMessage += "• Share your professional URL with anyone\n";

            EditorUtility.DisplayDialog("Publishing Success", summaryMessage, "Great!");
        }

        private void DrawSuccessSection()
        {
            EditorGUILayout.Space(20);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            var successStyle = new GUIStyle(EditorStyles.boldLabel);
            successStyle.normal.textColor = Color.green;
            successStyle.fontSize = 16;

            EditorGUILayout.LabelField("🎉 Success! Your content is live!", successStyle);
            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("Your URL:", EditorStyles.boldLabel);
            EditorGUILayout.SelectableLabel(publishUrl, EditorStyles.textField, GUILayout.Height(30));

            EditorGUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Open in Browser", GUILayout.Height(35)))
            {
                Application.OpenURL(publishUrl);
            }

            if (GUILayout.Button("Copy URL", GUILayout.Height(35)))
            {
                EditorGUIUtility.systemCopyBuffer = publishUrl;
                Debug.Log("URL copied to clipboard!");
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            if (GUILayout.Button("Reset for New Publish", GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog("Reset Publish State",
                    "This will reset the publish tab so you can test publishing again. Continue?",
                    "Yes, Reset", "Cancel"))
                {
                    ResetPublishState();
                }
            }

            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("Share this link with anyone to let them play your creation!", EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.EndVertical();
        }
    }

    [System.Serializable]
    public class GitHubRepositoryCreationResult
    {
        public bool Success { get; set; }
        public string RepositoryName { get; set; }
        public string CloneUrl { get; set; }
        public string LocalPath { get; set; }
        public string ErrorMessage { get; set; }
    }
}