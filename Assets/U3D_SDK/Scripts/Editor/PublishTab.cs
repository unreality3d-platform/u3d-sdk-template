using UnityEngine;
using UnityEditor;
using System.IO;
using System.Threading.Tasks;
using System.Linq;

namespace U3D.Editor
{
    public class PublishTab : ICreatorTab
    {
        public string TabName => "Publish";
        public bool IsComplete { get; private set; }
        public System.Action<int> OnRequestTabSwitch { get; set; }

        private PublishStep currentStep = PublishStep.Ready;
        private string publishUrl = "";
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
            EditorGUILayout.LabelField("Ready to go live?", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("This will:", EditorStyles.label);
            EditorGUILayout.LabelField("• Build your Unity project for WebGL locally", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("• Create a GitHub repository for your project", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("• Deploy to GitHub Pages automatically", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("• Generate your professional URL", EditorStyles.miniLabel);
            EditorGUILayout.Space(10);

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("✨ Simplified Local Build Process", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Builds locally, deploys automatically." +
                "You'll receive a professional URL that's ready to share in minutes!",
                MessageType.Info);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            if (GUILayout.Button("Make It Live!", GUILayout.Height(50)))
            {
                StartSimplifiedPublishProcess();
            }

            EditorGUILayout.EndVertical();
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

        private async void StartSimplifiedPublishProcess()
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

                // Step 2: Create GitHub Repository
                currentStep = PublishStep.CreatingRepository;
                currentStatus = "Creating GitHub repository...";

                var repoResult = await CreateGitHubRepository();
                if (!repoResult.Success)
                {
                    throw new System.Exception(repoResult.ErrorMessage);
                }

                githubConnected = true;
                currentStatus = $"Repository created: {repoResult.RepositoryName}";

                // Step 3: Deploy to GitHub Pages
                currentStep = PublishStep.DeployingToGitHub;
                currentStatus = "Deploying to GitHub Pages...";

                var deployResult = await DeployToGitHubPages(repoResult.RepositoryName, repoResult.LocalPath, buildResult.BuildPath);
                if (!deployResult.Success)
                {
                    throw new System.Exception(deployResult.ErrorMessage);
                }

                deploymentComplete = true;

                // Complete
                currentStep = PublishStep.Complete;
                IsComplete = true;

                var creatorUsername = U3DAuthenticator.CreatorUsername;
                var projectName = GitHubAPI.SanitizeRepositoryName(Application.productName);
                publishUrl = $"https://{creatorUsername}.unreality3d.com/{projectName}/";
                EditorPrefs.SetString("U3D_PublishedURL", publishUrl);

                currentStatus = "Publishing completed successfully!";

                ShowDeploymentSummary(repoResult.RepositoryName);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Publishing failed: {ex.Message}");
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


        private async Task<GitHubRepositoryCreationResult> CreateGitHubRepository()
        {
            // Generate unique repository name
            var baseName = GitHubAPI.SanitizeRepositoryName(Application.productName);
            var uniqueName = await GitHubAPI.GenerateUniqueRepositoryName(baseName);

            // Create repository from template
            var repoResult = await GitHubAPI.CopyFromTemplate(uniqueName, $"Unity WebGL project: {Application.productName}");

            if (!repoResult.Success)
            {
                return new GitHubRepositoryCreationResult
                {
                    Success = false,
                    ErrorMessage = repoResult.ErrorMessage
                };
            }

            // Create local working directory
            var projectPath = Path.GetDirectoryName(Application.dataPath);
            var localRepoPath = Path.Combine(projectPath, $"{uniqueName}-repo");

            return new GitHubRepositoryCreationResult
            {
                Success = true,
                RepositoryName = uniqueName,
                CloneUrl = repoResult.CloneUrl,
                LocalPath = localRepoPath
            };
        }

        private async Task<GitOperationResult> DeployToGitHubPages(string repositoryName, string localRepoPath, string buildPath)
        {
            try
            {
                // Clone template repository
                currentStatus = "Cloning template repository...";
                var cloneResult = await GitIntegration.CloneRepository(
                    $"https://github.com/{GitHubTokenManager.GitHubUsername}/{repositoryName}.git",
                    localRepoPath);

                if (!cloneResult.Success)
                {
                    return new GitOperationResult
                    {
                        Success = false,
                        ErrorMessage = cloneResult.ErrorMessage
                    };
                }

                // Copy build files to repository
                currentStatus = "Copying build files to repository...";
                var copyResult = await CopyBuildToRepository(buildPath, localRepoPath);
                if (!copyResult)
                {
                    return new GitOperationResult
                    {
                        Success = false,
                        ErrorMessage = "Failed to copy build files to repository"
                    };
                }

                // Process template with creator information
                currentStatus = "Processing Unity template and generating creator files...";
                var processorResult = await RunUnityTemplateProcessor(localRepoPath);
                if (!processorResult.Success)
                {
                    return processorResult;
                }

                // Set up git user
                var username = GitHubTokenManager.GitHubUsername ?? "Unity User";
                var email = U3DAuthenticator.UserEmail ?? "user@example.com";
                await GitIntegration.SetupGitUser(localRepoPath, username, email);

                // Add all files
                currentStatus = "Committing files to repository...";
                var addResult = await GitIntegration.AddAllFiles(localRepoPath);
                if (!addResult.Success)
                {
                    return addResult;
                }

                // Commit changes
                var creatorUsername = U3DAuthenticator.CreatorUsername;
                var commitMessage = $"Creator content: {Application.productName} by {creatorUsername}";
                var commitResult = await GitIntegration.CommitChanges(localRepoPath, commitMessage);
                if (!commitResult.Success)
                {
                    return commitResult;
                }

                // Push to GitHub
                currentStatus = "Pushing to GitHub Pages...";
                var pushResult = await GitIntegration.PushToRemote(localRepoPath, "main");
                return pushResult;
            }
            catch (System.Exception ex)
            {
                return new GitOperationResult
                {
                    Success = false,
                    ErrorMessage = $"Deployment failed: {ex.Message}"
                };
            }
        }

        private async Task<bool> CopyBuildToRepository(string buildPath, string repositoryPath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var buildDirectory = new DirectoryInfo(buildPath);
                    var repoDirectory = new DirectoryInfo(Path.Combine(repositoryPath, "Build"));

                    if (!buildDirectory.Exists)
                    {
                        Debug.LogError($"Build directory does not exist: {buildPath}");
                        return false;
                    }

                    // Create Build directory in repository
                    if (!repoDirectory.Exists)
                    {
                        repoDirectory.Create();
                    }

                    // Copy all build files
                    CopyDirectoryRecursively(buildDirectory, repoDirectory);

                    Debug.Log($"Build files copied from {buildPath} to {repoDirectory.FullName}");
                    return true;
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"Failed to copy build to repository: {ex.Message}");
                    return false;
                }
            });
        }

        private void CopyDirectoryRecursively(DirectoryInfo source, DirectoryInfo target)
        {
            // Create target directory if it doesn't exist
            if (!target.Exists)
            {
                target.Create();
            }

            // Copy all files
            foreach (var file in source.GetFiles())
            {
                var targetFile = Path.Combine(target.FullName, file.Name);
                file.CopyTo(targetFile, true);
            }

            // Copy all subdirectories
            foreach (var subdir in source.GetDirectories())
            {
                var targetSubdir = target.CreateSubdirectory(subdir.Name);
                CopyDirectoryRecursively(subdir, targetSubdir);
            }
        }

        private async Task<GitOperationResult> RunUnityTemplateProcessor(string repositoryPath)
        {
            try
            {
                currentStatus = "Running Unity template processor...";

                // The processor should be in the cloned template repository root
                var processorPath = Path.Combine(repositoryPath, "unity-template-processor.js");

                if (!File.Exists(processorPath))
                {
                    return new GitOperationResult
                    {
                        Success = false,
                        ErrorMessage = $"Unity template processor not found in template repository at: {processorPath}"
                    };
                }

                // Create unity-template-config.json with project-specific settings
                var configPath = Path.Combine(repositoryPath, "unity-template-config.json");
                var config = new
                {
                    templatePath = Path.Combine(repositoryPath, "template.html"),
                    buildOutputPath = Path.Combine(repositoryPath, "Build"),
                    outputPath = Path.Combine(repositoryPath, "index.html"),
                    contentId = GitHubAPI.SanitizeRepositoryName(Application.productName),
                    companyName = U3DAuthenticator.CreatorUsername ?? "Unity Creator",
                    productName = Application.productName,
                    productVersion = Application.version
                };

                // Write config file
                File.WriteAllText(configPath, UnityEngine.JsonUtility.ToJson(config, true));

                // Run the processor using Node.js
                var processInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "node",
                    Arguments = "unity-template-processor.js",
                    WorkingDirectory = repositoryPath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = System.Diagnostics.Process.Start(processInfo))
                {
                    if (process == null)
                    {
                        return new GitOperationResult
                        {
                            Success = false,
                            ErrorMessage = "Failed to start Node.js process for template processor"
                        };
                    }

                    await Task.Run(() => process.WaitForExit());

                    var output = await process.StandardOutput.ReadToEndAsync();
                    var error = await process.StandardError.ReadToEndAsync();

                    Debug.Log($"Template processor output: {output}");

                    if (process.ExitCode != 0)
                    {
                        return new GitOperationResult
                        {
                            Success = false,
                            ErrorMessage = $"Template processor failed: {error}"
                        };
                    }

                    // Verify that critical files were generated
                    var requiredFiles = new[]
                    {
                        Path.Combine(repositoryPath, "index.html"),
                        Path.Combine(repositoryPath, "README.md"),
                        Path.Combine(repositoryPath, "manifest.webmanifest")
                    };

                    foreach (var file in requiredFiles)
                    {
                        if (!File.Exists(file))
                        {
                            return new GitOperationResult
                            {
                                Success = false,
                                ErrorMessage = $"Template processor did not generate required file: {Path.GetFileName(file)}"
                            };
                        }
                    }

                    currentStatus = "✅ Creator files generated successfully";
                    return new GitOperationResult
                    {
                        Success = true,
                        Message = "Unity template processor completed successfully"
                    };
                }
            }
            catch (System.Exception ex)
            {
                return new GitOperationResult
                {
                    Success = false,
                    ErrorMessage = $"Template processor execution failed: {ex.Message}"
                };
            }
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