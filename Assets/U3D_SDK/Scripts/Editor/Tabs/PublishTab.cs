﻿using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Text;
using System.Reflection;


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
        private bool shouldCreateNewRepository = false;

        private List<ProjectOption> availableOptions = new List<ProjectOption>();
        private bool optionsLoaded = false;
        private bool loadingOptions = false;
        private int selectedOptionIndex = -1;

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
            shouldCreateNewRepository = true;

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

            // Show current Unity project name
            EditorGUILayout.LabelField("Current Project Settings:", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Product Name:", EditorStyles.miniLabel, GUILayout.Width(80));
            EditorGUILayout.LabelField(cachedProductName, EditorStyles.boldLabel);
            if (GUILayout.Button("Change", GUILayout.Width(60)))
            {
                // Open Project Settings > Player (Unity 6+ compatible)
                EditorApplication.ExecuteMenuItem("Edit/Project Settings...");
            }

            EditorGUILayout.Space(5);

            // Instructions for changing project name
            EditorGUILayout.HelpBox(
                "💡 To publish as a completely new project: Click 'Change' above, then update the Product Name in Project Settings > Player",
                MessageType.Info
            );

            EditorGUILayout.Space(10);

            // Load repository options if not loaded
            if (!optionsLoaded && !loadingOptions)
            {
                EditorGUILayout.LabelField("Analyzing your GitHub repositories...", EditorStyles.centeredGreyMiniLabel);
                loadingOptions = true;
                _ = LoadProjectOptionsAsync();
            }
            else if (loadingOptions)
            {
                EditorGUILayout.LabelField("🔍 Checking your GitHub repositories...", EditorStyles.centeredGreyMiniLabel);
            }
            else if (optionsLoaded)
            {
                DrawProjectOptions();
            }

            EditorGUILayout.EndVertical();
        }

        // ADD THESE NEW METHODS TO PublishTab.cs:

        private async Task LoadProjectOptionsAsync()
        {
            try
            {
                availableOptions.Clear();

                // Get repositories that match or are similar to current project name
                var repoResult = await GitHubAPI.GetUserRepositories(cachedProductName, 50);

                if (!repoResult.Success)
                {
                    Debug.LogError($"Failed to load repositories: {repoResult.ErrorMessage}");
                    optionsLoaded = true;
                    loadingOptions = false;
                    return;
                }

                // Analyze each repository
                foreach (var repo in repoResult.Repositories)
                {
                    // Check if it's a Unity project
                    repo.IsUnityProject = await GitHubAPI.HasUnityWebGLFiles(repo.Name);

                    // Get GitHub Pages URL if available
                    if (repo.HasPages)
                    {
                        repo.GitHubPagesUrl = await GitHubAPI.GetGitHubPagesUrl(repo.Name);
                    }

                    // Create update option for existing repos
                    if (IsRelatedRepository(repo.Name, cachedProductName))
                    {
                        availableOptions.Add(new ProjectOption
                        {
                            Type = ProjectOption.OptionType.UpdateExisting,
                            RepositoryName = repo.Name,
                            DisplayName = $"Update \"{repo.Name}\"",
                            Description = repo.IsUnityProject ? "Unity WebGL project" : "Repository",
                            ProfessionalUrl = $"https://{U3DAuthenticator.CreatorUsername}.unreality3d.com/{repo.Name}/",
                            GitHubPagesUrl = repo.GitHubPagesUrl,
                            LastUpdated = repo.UpdatedAt,
                            IsUnityProject = repo.IsUnityProject
                        });
                    }
                }

                // Always add "Create New" option
                var newRepoName = await GitHubAPI.GenerateUniqueRepositoryName(cachedProductName);
                availableOptions.Add(new ProjectOption
                {
                    Type = ProjectOption.OptionType.CreateNew,
                    RepositoryName = newRepoName,
                    DisplayName = $"Create New \"{newRepoName}\"",
                    Description = "New Unity WebGL project",
                    ProfessionalUrl = $"https://{U3DAuthenticator.CreatorUsername}.unreality3d.com/{newRepoName}/",
                    GitHubPagesUrl = null,
                    LastUpdated = null,
                    IsUnityProject = false
                });

                // Auto-select the first option
                if (availableOptions.Count > 0)
                {
                    selectedOptionIndex = 0;
                }

                optionsLoaded = true;
                loadingOptions = false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error loading project options: {ex.Message}");
                optionsLoaded = true;
                loadingOptions = false;
            }
        }

        private void DrawProjectOptions()
        {
            if (availableOptions.Count == 0)
            {
                EditorGUILayout.HelpBox("No GitHub repositories found. A new repository will be created.", MessageType.Info);

                if (GUILayout.Button("Make It Live!", GUILayout.Height(50)))
                {
                    shouldCreateNewRepository = true;
                    _ = StartFirebasePublishProcessAsync();
                }
                return;
            }

            EditorGUILayout.LabelField("Choose your publishing option:", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // Draw radio button options
            for (int i = 0; i < availableOptions.Count; i++)
            {
                var option = availableOptions[i];
                var isSelected = selectedOptionIndex == i;

                EditorGUILayout.BeginVertical(isSelected ? EditorStyles.helpBox : EditorStyles.textArea);

                EditorGUILayout.BeginHorizontal();

                // Radio button
                var newSelected = EditorGUILayout.Toggle(isSelected, GUILayout.Width(20));
                if (newSelected && !isSelected)
                {
                    selectedOptionIndex = i;
                }

                // Option details
                EditorGUILayout.BeginVertical();

                var style = new GUIStyle(EditorStyles.boldLabel);
                if (option.Type == ProjectOption.OptionType.UpdateExisting)
                {
                    style.normal.textColor = Color.blue;
                }
                else
                {
                    style.normal.textColor = Color.green;
                }

                EditorGUILayout.LabelField(option.DisplayName, style);
                EditorGUILayout.LabelField(option.Description, EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"URL: {option.ProfessionalUrl}", EditorStyles.miniLabel);

                if (option.LastUpdated.HasValue)
                {
                    EditorGUILayout.LabelField($"Last updated: {option.LastUpdated.Value:MMM dd, yyyy}", EditorStyles.miniLabel);
                }

                if (!string.IsNullOrEmpty(option.GitHubPagesUrl))
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Currently live at:", EditorStyles.miniLabel, GUILayout.Width(90));
                    if (GUILayout.Button(option.GitHubPagesUrl, EditorStyles.linkLabel))
                    {
                        Application.OpenURL(option.GitHubPagesUrl);
                    }
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();

                EditorGUILayout.Space(3);
            }

            EditorGUILayout.Space(10);

            // Publish button
            GUI.enabled = selectedOptionIndex >= 0;
            if (GUILayout.Button("Make It Live!", GUILayout.Height(50)))
            {
                var selectedOption = availableOptions[selectedOptionIndex];
                shouldCreateNewRepository = selectedOption.Type == ProjectOption.OptionType.CreateNew;

                // Override the cached product name if using existing repo with different name
                if (selectedOption.Type == ProjectOption.OptionType.UpdateExisting)
                {
                    cachedProductName = selectedOption.RepositoryName;
                }

                _ = StartFirebasePublishProcessAsync();
            }
            GUI.enabled = true;
        }

        private bool IsRelatedRepository(string repoName, string projectName)
        {
            var sanitizedProject = GitHubAPI.SanitizeRepositoryName(projectName);
            var lowerRepo = repoName.ToLower();
            var lowerProject = sanitizedProject.ToLower();

            // Exact match
            if (lowerRepo == lowerProject)
                return true;

            // Starts with project name (handles increments like "myproject-1", "myproject-2")
            if (lowerRepo.StartsWith(lowerProject + "-"))
                return true;

            // Project name is contained in repo name
            if (lowerRepo.Contains(lowerProject))
                return true;

            return false;
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


                // Step 2: Deploy via Firebase Cloud Functions
                currentStep = PublishStep.DeployingToGitHub;
                currentStatus = "Deploying via Firebase Cloud Functions...";

                var deployResult = await DeployViaFirebaseStorage(buildResult.BuildPath);
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

                // Build WebGL to persistent directory (FIXED)
                var projectPath = Path.GetDirectoryName(Application.dataPath);
                var timestampedBuildName = $"WebGLBuild_{DateTime.Now:yyyyMMdd_HHmmss}";
                var buildPath = Path.Combine(projectPath, "U3D_Builds", timestampedBuildName);

                // FIXED: Use buildPath instead of tempBuildPath
                var buildResult = await UnityBuildHelper.BuildWebGL(buildPath, (status) =>
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

        private async Task<FirebaseDeployResult> DeployViaFirebaseStorage(string buildPath)
        {
            try
            {
                currentStatus = "Determining project name...";

                // Send base name and intent to Firebase
                var baseName = string.IsNullOrEmpty(cachedProductName) ? "unity-webgl-project" : cachedProductName;
                var sanitizedBaseName = GitHubAPI.SanitizeRepositoryName(baseName);
                var deploymentIntent = shouldCreateNewRepository ? "create_new" : "update_existing";

                Debug.Log($"🎯 Deployment intent: {deploymentIntent}, Base name: {sanitizedBaseName}");

                currentStatus = "Uploading build to Firebase Storage...";
                var storageBucket = FirebaseConfigManager.CurrentConfig?.storageBucket ?? "unreality3d.firebasestorage.app";
                if (string.IsNullOrEmpty(storageBucket) || storageBucket == "setup-required")
                {
                    storageBucket = "unreality3d.firebasestorage.app";
                }

                // Create uploader with updated method
                var uploader = new FirebaseStorageUploader(storageBucket, U3DAuthenticator.GetIdToken());

                var result = await uploader.UploadBuildToStorageWithIntent(
                    buildPath,
                    U3DAuthenticator.CreatorUsername,
                    sanitizedBaseName,
                    deploymentIntent
                );

                uploader.Dispose();

                if (result.Success)
                {
                    // Use the actual project name returned from Firebase
                    var actualProjectName = result.ActualProjectName ?? sanitizedBaseName;

                    // Store for future updates
                    EditorPrefs.SetString("U3D_LastProjectName", actualProjectName);
                    shouldCreateNewRepository = false; // Reset flag

                    return new FirebaseDeployResult
                    {
                        Success = true,
                        RepositoryName = actualProjectName,
                        ProjectName = actualProjectName,
                        ProfessionalUrl = $"https://{U3DAuthenticator.CreatorUsername}.unreality3d.com/{actualProjectName}/",
                        Message = "Deployment successful via Firebase Storage"
                    };
                }
                else
                {
                    return new FirebaseDeployResult
                    {
                        Success = false,
                        ErrorMessage = result.ErrorMessage ?? "Firebase Storage upload failed"
                    };
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Firebase Storage deployment error: {ex.Message}");
                return new FirebaseDeployResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        private static async Task<Dictionary<string, object>> CallFirebaseFunction(string functionName, Dictionary<string, object> data)
        {
            // Use reflection to access the private method
            var method = typeof(U3DAuthenticator).GetMethod("CallFirebaseFunction",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            if (method == null)
            {
                throw new Exception("CallFirebaseFunction method not found in U3DAuthenticator");
            }

            var task = method.Invoke(null, new object[] { functionName, data }) as Task<Dictionary<string, object>>;
            return await task;
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

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Update Project", GUILayout.Height(35)))
            {
                // Republish to same repository (don't increment name)
                shouldCreateNewRepository = false;
                currentStep = PublishStep.Ready;
                deploymentComplete = false;
                // Keep publishUrl and other success state
                _ = StartFirebasePublishProcessAsync();
            }

            if (GUILayout.Button("Reset for New Publish", GUILayout.Height(35)))
            {
                if (EditorUtility.DisplayDialog("Reset Publish State",
                    "This will create a new project with an incremented name. Continue?",
                    "Yes, Create New", "Cancel"))
                {
                    ResetPublishState(); // This sets shouldCreateNewRepository = true
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("Share this link with anyone to let them play your creation!", EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.EndVertical();
        }
    }

    [System.Serializable]
    public class ProjectOption
    {
        public enum OptionType
        {
            UpdateExisting,
            CreateNew
        }

        public OptionType Type { get; set; }
        public string RepositoryName { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public string ProfessionalUrl { get; set; }
        public string GitHubPagesUrl { get; set; }
        public DateTime? LastUpdated { get; set; }
        public bool IsUnityProject { get; set; }
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