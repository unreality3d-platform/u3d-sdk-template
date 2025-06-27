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
        private bool projectSaved = false;
        private bool deploymentComplete = false;
        private Vector2 scrollPosition;
        private string currentStatus = "";
        private bool isPublishing = false;

        // NEW: Unity credentials management
        private UnityCredentials unityCredentials;
        private bool showUnityCredentials = false;
        private bool validatingUnityCredentials = false;
        private string unityValidationMessage = "";

        private enum PublishStep
        {
            Ready,
            ConnectingGitHub,
            SavingProject,
            MakingLive,
            Complete
        }

        public void Initialize()
        {
            publishUrl = EditorPrefs.GetString("U3D_PublishedURL", "");
            LoadUnityCredentials();

            if (!string.IsNullOrEmpty(publishUrl))
            {
                IsComplete = true;
                currentStep = PublishStep.Complete;
                githubConnected = true;
                projectSaved = true;
                deploymentComplete = true;
            }
        }

        private void ResetPublishState()
        {
            EditorPrefs.DeleteKey("U3D_PublishedURL");
            publishUrl = "";
            githubConnected = false;
            projectSaved = false;
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
            EditorGUILayout.LabelField("• Create a GitHub repository for your project", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("• Build your Unity project for WebGL", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("• Deploy to GitHub Pages", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("• Generate your professional URL", EditorStyles.miniLabel);
            EditorGUILayout.Space(10);

            // NEW: Unity credentials section
            DrawUnityCredentialsSection();

            EditorGUILayout.Space(10);

            // Enable publish button only if Unity credentials are configured
            EditorGUI.BeginDisabledGroup(!HasValidUnityCredentials());
            if (GUILayout.Button("Make It Live!", GUILayout.Height(50)))
            {
                StartRealPublishProcess();
            }
            EditorGUI.EndDisabledGroup();

            if (!HasValidUnityCredentials())
            {
                EditorGUILayout.HelpBox("⚠️ Unity credentials required for automated builds", MessageType.Warning);
            }

            EditorGUILayout.EndVertical();
        }

        // NEW: Unity credentials management UI
        private void DrawUnityCredentialsSection()
        {
            EditorGUILayout.LabelField("Unity Account (for automated builds)", EditorStyles.boldLabel);

            if (!HasValidUnityCredentials())
            {
                EditorGUILayout.HelpBox(
                    "Your Unity account credentials are needed to build your project automatically.\n\n" +
                    "These are stored securely on your computer and used to configure GitHub Actions.",
                    MessageType.Info);
            }

            EditorGUILayout.Space(5);

            if (showUnityCredentials || !HasValidUnityCredentials())
            {
                if (unityCredentials == null)
                {
                    unityCredentials = new UnityCredentials();
                }

                EditorGUILayout.LabelField("Unity Email:", EditorStyles.miniLabel);
                unityCredentials.Email = EditorGUILayout.TextField(unityCredentials.Email);

                EditorGUILayout.LabelField("Unity Password:", EditorStyles.miniLabel);
                unityCredentials.Password = EditorGUILayout.PasswordField(unityCredentials.Password);

                EditorGUILayout.LabelField("2FA Key (optional):", EditorStyles.miniLabel);
                unityCredentials.AuthenticatorKey = EditorGUILayout.TextField(unityCredentials.AuthenticatorKey);
                EditorGUILayout.LabelField("Only needed if you have 2FA enabled on your Unity account", EditorStyles.miniLabel);

                EditorGUILayout.Space(5);

                if (!string.IsNullOrEmpty(unityValidationMessage))
                {
                    var messageType = HasValidUnityCredentials() ? MessageType.Info : MessageType.Warning;
                    EditorGUILayout.HelpBox(unityValidationMessage, messageType);
                }

                EditorGUILayout.BeginHorizontal();

                bool canValidate = !string.IsNullOrEmpty(unityCredentials.Email) &&
                                 !string.IsNullOrEmpty(unityCredentials.Password) &&
                                 !validatingUnityCredentials;

                EditorGUI.BeginDisabledGroup(!canValidate);
                if (GUILayout.Button(validatingUnityCredentials ? "🔄 Validating..." : "✅ Save Credentials"))
                {
                    ValidateAndSaveUnityCredentials();
                }
                EditorGUI.EndDisabledGroup();

                if (HasValidUnityCredentials() && GUILayout.Button("👁️ Hide"))
                {
                    showUnityCredentials = false;
                }

                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("✅ Unity credentials configured", EditorStyles.miniLabel);
                if (GUILayout.Button("📝 Update", GUILayout.Width(70)))
                {
                    showUnityCredentials = true;
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        // NEW: Unity credentials validation
        private async void ValidateAndSaveUnityCredentials()
        {
            validatingUnityCredentials = true;
            unityValidationMessage = "Validating Unity credentials...";

            try
            {
                // Basic validation
                if (string.IsNullOrEmpty(unityCredentials.Email) || string.IsNullOrEmpty(unityCredentials.Password))
                {
                    unityValidationMessage = "❌ Email and password are required";
                    return;
                }

                if (!unityCredentials.Email.Contains("@"))
                {
                    unityValidationMessage = "❌ Please enter a valid email address";
                    return;
                }

                if (unityCredentials.Password.Length < 6)
                {
                    unityValidationMessage = "❌ Password seems too short";
                    return;
                }

                // Save credentials (encrypted via EditorPrefs)
                SaveUnityCredentials();

                unityValidationMessage = "✅ Unity credentials saved successfully";
                showUnityCredentials = false;

                Debug.Log("Unity credentials validated and saved");
            }
            catch (System.Exception ex)
            {
                unityValidationMessage = $"❌ Validation failed: {ex.Message}";
                Debug.LogError($"Unity credentials validation error: {ex.Message}");
            }
            finally
            {
                validatingUnityCredentials = false;
            }
        }

        // NEW: Unity credentials storage methods
        private void SaveUnityCredentials()
        {
            if (unityCredentials != null)
            {
                // Store encrypted in EditorPrefs (similar to GitHub token approach)
                if (!string.IsNullOrEmpty(unityCredentials.Email))
                {
                    EditorPrefs.SetString("U3D_UnityEmail", unityCredentials.Email);
                }
                if (!string.IsNullOrEmpty(unityCredentials.Password))
                {
                    EditorPrefs.SetString("U3D_UnityPassword", unityCredentials.Password);
                }
                if (!string.IsNullOrEmpty(unityCredentials.AuthenticatorKey))
                {
                    EditorPrefs.SetString("U3D_UnityAuthKey", unityCredentials.AuthenticatorKey);
                }
                EditorPrefs.SetBool("U3D_UnityCredentialsValid", true);
            }
        }

        private void LoadUnityCredentials()
        {
            unityCredentials = new UnityCredentials
            {
                Email = EditorPrefs.GetString("U3D_UnityEmail", ""),
                Password = EditorPrefs.GetString("U3D_UnityPassword", ""),
                AuthenticatorKey = EditorPrefs.GetString("U3D_UnityAuthKey", "")
            };
        }

        private bool HasValidUnityCredentials()
        {
            return EditorPrefs.GetBool("U3D_UnityCredentialsValid", false) &&
                   !string.IsNullOrEmpty(EditorPrefs.GetString("U3D_UnityEmail", "")) &&
                   !string.IsNullOrEmpty(EditorPrefs.GetString("U3D_UnityPassword", ""));
        }

        private void DrawPublishingSteps()
        {
            EditorGUILayout.LabelField("Publishing Progress", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            DrawStep("GitHub Repository",
                githubConnected,
                currentStep == PublishStep.ConnectingGitHub,
                "✓ GitHub repository created",
                "🔗 Creating GitHub repository...");

            DrawStep("Unity WebGL Build",
                projectSaved,
                currentStep == PublishStep.SavingProject,
                "✓ Unity project built successfully",
                "🔨 Building Unity WebGL project...");

            DrawStep("Deploy to Web",
                deploymentComplete,
                currentStep == PublishStep.MakingLive,
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

        private async void StartRealPublishProcess()
        {
            isPublishing = true;

            try
            {
                // Step 1: Create GitHub Repository
                currentStep = PublishStep.ConnectingGitHub;
                currentStatus = "Creating GitHub repository...";

                var repoResult = await CreateGitHubRepository();
                if (!repoResult.Success)
                {
                    throw new System.Exception(repoResult.ErrorMessage);
                }

                githubConnected = true;
                currentStatus = $"Repository created: {repoResult.RepositoryName}";

                // NEW: Step 1.5: Configure Unity secrets automatically
                currentStatus = "Configuring Unity build secrets...";
                LoadUnityCredentials(); // Ensure we have the latest credentials

                bool secretsConfigured = await GitHubTokenManager.SetupUnityRepositorySecrets(repoResult.RepositoryName, unityCredentials);
                if (!secretsConfigured)
                {
                    throw new System.Exception("Failed to configure Unity build secrets. Publishing cannot continue.");
                }

                string generatedLicense = EditorPrefs.GetString("U3D_GeneratedUnityLicense", "");
                if (!string.IsNullOrEmpty(generatedLicense))
                {
                    currentStatus = "Adding Unity license to repository secrets...";
                    bool licenseSet = await GitHubTokenManager.SetRepositorySecret(repoResult.RepositoryName, "UNITY_LICENSE", generatedLicense);
                    if (licenseSet)
                    {
                        currentStatus = "Unity license configured in repository";
                    }
                }

                currentStatus = "Unity build secrets configured successfully";

                // Step 2: Clone template repository first
                currentStep = PublishStep.SavingProject;
                currentStatus = "Cloning template repository...";

                var cloneResult = await GitIntegration.CloneRepository(repoResult.CloneUrl, repoResult.LocalPath);
                if (!cloneResult.Success)
                {
                    throw new System.Exception(cloneResult.ErrorMessage);
                }

                // Step 3: Build Unity WebGL Project directly into the repository
                currentStatus = "Building Unity WebGL project...";

                var buildResult = await BuildUnityProjectToRepository(repoResult.LocalPath);
                if (!buildResult.Success)
                {
                    throw new System.Exception(buildResult.ErrorMessage);
                }

                projectSaved = true;
                currentStatus = "Unity build completed successfully";

                // Step 4: Deploy to GitHub Pages
                currentStep = PublishStep.MakingLive;
                currentStatus = "Deploying to GitHub Pages...";

                var deployResult = await FinalizeDeployment(repoResult.LocalPath);
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
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Publishing failed: {ex.Message}");
                EditorUtility.DisplayDialog("Publishing Failed", $"There was an error: {ex.Message}", "OK");

                currentStep = PublishStep.Ready;
                currentStatus = $"Publishing failed: {ex.Message}";

                // Reset states
                githubConnected = false;
                projectSaved = false;
                deploymentComplete = false;
            }
            finally
            {
                isPublishing = false;
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

        private async Task<UnityBuildResult> BuildUnityProjectToRepository(string repositoryPath)
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

            // Build WebGL directly to the repository's Build folder
            var buildOutputPath = Path.Combine(repositoryPath, "Build");
            var buildResult = await UnityBuildHelper.BuildWebGL(buildOutputPath, (status) =>
            {
                currentStatus = status;
            });

            return buildResult;
        }

        private async Task<GitOperationResult> FinalizeDeployment(string localPath)
        {
            try
            {
                // Verify Unity build files exist in the repository
                currentStatus = "Verifying Unity build files...";
                var buildDirectory = Path.Combine(localPath, "Build");
                if (!Directory.Exists(buildDirectory))
                {
                    return new GitOperationResult
                    {
                        Success = false,
                        ErrorMessage = "Unity build directory not found. Build may have failed."
                    };
                }

                var webglFiles = Directory.GetFiles(buildDirectory, "*.*", SearchOption.AllDirectories);
                if (!webglFiles.Any(f => f.EndsWith(".wasm") || f.EndsWith(".loader.js")))
                {
                    return new GitOperationResult
                    {
                        Success = false,
                        ErrorMessage = "Unity WebGL build files not found. Build may have failed."
                    };
                }

                // Run unity-template-processor to replace organization content
                currentStatus = "Processing Unity template and generating creator files...";
                var processorResult = await RunUnityTemplateProcessor(localPath);
                if (!processorResult.Success)
                {
                    return processorResult;
                }

                // Set up git user
                var username = GitHubTokenManager.GitHubUsername ?? "Unity User";
                var email = U3DAuthenticator.UserEmail ?? "user@example.com";
                await GitIntegration.SetupGitUser(localPath, username, email);

                // Add all files (including processed template and creator README)
                currentStatus = "Committing files to repository...";
                var addResult = await GitIntegration.AddAllFiles(localPath);
                if (!addResult.Success)
                {
                    return addResult;
                }

                // Commit changes with creator-specific message
                var creatorUsername = U3DAuthenticator.CreatorUsername;
                var commitMessage = $"Creator content: {Application.productName} by {creatorUsername}";
                var commitResult = await GitIntegration.CommitChanges(localPath, commitMessage);
                if (!commitResult.Success)
                {
                    return commitResult;
                }

                // Push to GitHub (using enhanced PushToRemote with force handling)
                currentStatus = "Pushing to GitHub Pages...";
                var pushResult = await GitIntegration.PushToRemote(localPath, "main");
                return pushResult;
            }
            catch (System.Exception ex)
            {
                return new GitOperationResult
                {
                    Success = false,
                    ErrorMessage = $"Deployment finalization failed: {ex.Message}"
                };
            }
        }

        // Unity template processor method (runs processor from cloned repo)
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

                // Write config file in the repository (where processor expects it)
                File.WriteAllText(configPath, UnityEngine.JsonUtility.ToJson(config, true));

                // Run the processor using Node.js from within the template repository
                var processInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "node",
                    Arguments = "unity-template-processor.js", // Relative path since we're in the repo directory
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