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
        private bool unity2FAEnabled = false; 
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

                EditorGUILayout.Space(5);

                unity2FAEnabled = EditorGUILayout.Toggle("I have 2FA enabled on my Unity account", unity2FAEnabled);

                if (unity2FAEnabled)
                {
                    EditorGUILayout.Space(5);

                    // Enhanced 2FA options with interactive support
                    EditorGUILayout.LabelField("2FA Configuration Options:", EditorStyles.boldLabel);

                    // Method 1: TOTP Key (Automated)
                    EditorGUILayout.BeginVertical("box");
                    EditorGUILayout.LabelField("🔑 Method 1: TOTP Key (Fully Automated)", EditorStyles.boldLabel);
                    EditorGUILayout.HelpBox(
                        "You'll need your authenticator secret key (not the 6-digit codes).\n" +
                        "This is the long string of letters/numbers used to set up your authenticator app.",
                        MessageType.Info);

                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("📱 Open Unity 2FA Settings", GUILayout.Height(25)))
                    {
                        Application.OpenURL("https://id.unity.com/en/account/edit");
                    }

                    if (GUILayout.Button("❓ How to find my key?", GUILayout.Height(25)))
                    {
                        EditorUtility.DisplayDialog("Finding Your 2FA Secret Key",
                            "1. Go to Unity ID → Security\n" +
                            "2. Find 'Two Factor Authentication' section\n" +
                            "3. If setting up new: Look for 'Manual entry key' or 'Secret key'\n" +
                            "4. If already set up: You may need to reconfigure to see the key\n\n" +
                            "The key looks like: JBSWY3DPEHPK3PXP\n" +
                            "(NOT the 6-digit codes that change every 30 seconds)",
                            "Got it!");
                    }
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.Space(3);
                    EditorGUILayout.LabelField("Authenticator Secret Key:", EditorStyles.miniLabel);
                    unityCredentials.AuthenticatorKey = EditorGUILayout.TextField(unityCredentials.AuthenticatorKey);

                    // Validation for the key format
                    if (!string.IsNullOrEmpty(unityCredentials.AuthenticatorKey))
                    {
                        if (unityCredentials.AuthenticatorKey.Length < 16)
                        {
                            EditorGUILayout.HelpBox("⚠️ This seems too short for a secret key. Make sure you're using the secret key, not a 6-digit code.", MessageType.Warning);
                        }
                        else if (unityCredentials.AuthenticatorKey.Contains(" "))
                        {
                            EditorGUILayout.HelpBox("ℹ️ Secret keys usually don't contain spaces. Remove any spaces if copied incorrectly.", MessageType.Info);
                            unityCredentials.AuthenticatorKey = unityCredentials.AuthenticatorKey.Replace(" ", "");
                        }
                        else
                        {
                            EditorGUILayout.HelpBox("✅ TOTP key provided - builds will be fully automated!", MessageType.Info);
                        }
                    }
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.Space(5);

                    // Method 2: Interactive 2FA
                    EditorGUILayout.BeginVertical("box");
                    EditorGUILayout.LabelField("🎯 Method 2: Interactive 2FA", EditorStyles.boldLabel);
                    bool useInteractive2FA = EditorPrefs.GetBool("U3D_UnityInteractive2FA", false);
                    useInteractive2FA = EditorGUILayout.Toggle("Enable Interactive 2FA Mode", useInteractive2FA);
                    EditorPrefs.SetBool("U3D_UnityInteractive2FA", useInteractive2FA);

                    if (useInteractive2FA)
                    {
                        EditorGUILayout.HelpBox(
                            "Interactive mode benefits:\n" +
                            "• No need to find your TOTP secret key\n" +
                            "• You can manually enter 2FA codes when needed\n" +
                            "• Fallback option if TOTP key doesn't work\n" +
                            "• Uses GitHub's workflow dispatch feature",
                            MessageType.Info);

                        EditorGUILayout.Space(3);
                        EditorGUILayout.LabelField("Current 2FA Code (optional for immediate publish):", EditorStyles.miniLabel);
                        string current2FACode = EditorPrefs.GetString("U3D_Current2FACode", "");
                        current2FACode = EditorGUILayout.TextField(current2FACode);
                        EditorPrefs.SetString("U3D_Current2FACode", current2FACode);

                        if (!string.IsNullOrEmpty(current2FACode))
                        {
                            if (current2FACode.Length == 6 && current2FACode.All(char.IsDigit))
                            {
                                EditorGUILayout.HelpBox("✅ Valid 2FA code - will be used for immediate publishing", MessageType.Info);
                            }
                            else
                            {
                                EditorGUILayout.HelpBox("⚠️ 2FA codes are typically 6 digits", MessageType.Warning);
                            }
                        }
                        else
                        {
                            EditorGUILayout.HelpBox("💡 Leave empty to manually trigger builds via GitHub Actions interface", MessageType.Info);
                        }
                    }
                    EditorGUILayout.EndVertical();
                }
                else
                {
                    // Clear 2FA related fields when disabled
                    unityCredentials.AuthenticatorKey = "";
                    EditorPrefs.SetBool("U3D_UnityInteractive2FA", false);
                    EditorPrefs.SetString("U3D_Current2FACode", "");
                    EditorGUILayout.Space(3);
                    EditorGUILayout.LabelField("No 2FA configuration needed", EditorStyles.miniLabel);
                }

                EditorGUILayout.Space(5);

                if (!string.IsNullOrEmpty(unityValidationMessage))
                {
                    var messageType = HasValidUnityCredentials() ? MessageType.Info : MessageType.Warning;
                    EditorGUILayout.HelpBox(unityValidationMessage, messageType);
                }

                EditorGUILayout.BeginHorizontal();

                bool canValidate = !string.IsNullOrEmpty(unityCredentials.Email) &&
                                 !string.IsNullOrEmpty(unityCredentials.Password) &&
                                 !validatingUnityCredentials &&
                                 (!unity2FAEnabled || !string.IsNullOrEmpty(unityCredentials.AuthenticatorKey) || EditorPrefs.GetBool("U3D_UnityInteractive2FA", false));

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
                string statusText = GetUnityCredentialsStatusText();
                EditorGUILayout.LabelField(statusText, EditorStyles.miniLabel);
                if (GUILayout.Button("📝 Update", GUILayout.Width(70)))
                {
                    showUnityCredentials = true;
                }
                EditorGUILayout.EndHorizontal();
            }
        }

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

                // Enhanced 2FA validation
                if (unity2FAEnabled)
                {
                    bool hasValidTOTP = !string.IsNullOrEmpty(unityCredentials.AuthenticatorKey) &&
                                       unityCredentials.AuthenticatorKey.Length >= 16;
                    bool useInteractive2FA = EditorPrefs.GetBool("U3D_UnityInteractive2FA", false);

                    if (!hasValidTOTP && !useInteractive2FA)
                    {
                        unityValidationMessage = "❌ Please provide TOTP key or enable Interactive 2FA mode";
                        return;
                    }

                    if (hasValidTOTP && useInteractive2FA)
                    {
                        unityValidationMessage = "💡 Both TOTP and Interactive enabled - TOTP will be tried first";
                    }
                }

                // Save credentials with enhanced options
                SaveEnhancedUnityCredentials();

                string successMessage = "✅ Unity credentials saved successfully";
                if (unity2FAEnabled)
                {
                    if (!string.IsNullOrEmpty(unityCredentials.AuthenticatorKey))
                    {
                        successMessage += " (TOTP automated)";
                    }
                    else if (EditorPrefs.GetBool("U3D_UnityInteractive2FA", false))
                    {
                        successMessage += " (Interactive 2FA enabled)";
                    }
                }

                unityValidationMessage = successMessage;
                showUnityCredentials = false;

                Debug.Log("Enhanced Unity credentials validated and saved");
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

        private void SaveEnhancedUnityCredentials()
        {
            if (unityCredentials != null)
            {
                // Store basic credentials
                if (!string.IsNullOrEmpty(unityCredentials.Email))
                {
                    EditorPrefs.SetString("U3D_UnityEmail", unityCredentials.Email);
                }
                if (!string.IsNullOrEmpty(unityCredentials.Password))
                {
                    EditorPrefs.SetString("U3D_UnityPassword", unityCredentials.Password);
                }

                // Store 2FA configuration
                if (unity2FAEnabled)
                {
                    if (!string.IsNullOrEmpty(unityCredentials.AuthenticatorKey))
                    {
                        EditorPrefs.SetString("U3D_UnityAuthKey", unityCredentials.AuthenticatorKey);
                    }
                    EditorPrefs.SetBool("U3D_Unity2FAEnabled", true);
                }
                else
                {
                    // Clear 2FA settings when disabled
                    EditorPrefs.DeleteKey("U3D_UnityAuthKey");
                    EditorPrefs.SetBool("U3D_UnityInteractive2FA", false);
                    EditorPrefs.SetBool("U3D_Unity2FAEnabled", false);
                    EditorPrefs.SetString("U3D_Current2FACode", "");
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

            // Restore 2FA preferences
            unity2FAEnabled = EditorPrefs.GetBool("U3D_Unity2FAEnabled", false);

            // Auto-detect 2FA if we have saved keys
            if (!unity2FAEnabled && !string.IsNullOrEmpty(unityCredentials.AuthenticatorKey))
            {
                unity2FAEnabled = true;
                EditorPrefs.SetBool("U3D_Unity2FAEnabled", true);
            }
        }

        private bool HasValidUnityCredentials()
        {
            return EditorPrefs.GetBool("U3D_UnityCredentialsValid", false) &&
                   !string.IsNullOrEmpty(EditorPrefs.GetString("U3D_UnityEmail", "")) &&
                   !string.IsNullOrEmpty(EditorPrefs.GetString("U3D_UnityPassword", ""));
        }

        private string GetUnityCredentialsStatusText()
        {
            bool hasAuthKey = !string.IsNullOrEmpty(EditorPrefs.GetString("U3D_UnityAuthKey", ""));
            bool hasInteractive = EditorPrefs.GetBool("U3D_UnityInteractive2FA", false);

            if (hasAuthKey)
            {
                return "✅ Unity credentials configured (TOTP automated)";
            }
            else if (hasInteractive)
            {
                return "✅ Unity credentials configured (Interactive 2FA)";
            }
            else
            {
                return "✅ Unity credentials configured";
            }
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

                // Step 1.5: Configure Unity secrets with enhanced 2FA support
                currentStatus = "Configuring Unity build secrets with 2FA support...";
                LoadUnityCredentials(); // Ensure we have the latest credentials

                bool secretsConfigured = await SetupEnhancedUnityRepositorySecrets(repoResult.RepositoryName, unityCredentials);
                if (!secretsConfigured)
                {
                    throw new System.Exception("Failed to configure Unity build secrets. Publishing cannot continue.");
                }

                currentStatus = "Unity build secrets and enhanced 2FA workflow configured successfully";

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

                // Show deployment summary with 2FA info
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
                projectSaved = false;
                deploymentComplete = false;
            }
            finally
            {
                isPublishing = false;
            }
        }

        private async Task<bool> SetupEnhancedUnityRepositorySecrets(string repositoryName, UnityCredentials credentials)
        {
            try
            {
                Debug.Log("🔧 Setting up enhanced Unity repository secrets...");

                // Set basic Unity credentials
                bool emailSet = await GitHubTokenManager.SetRepositorySecret(repositoryName, "UNITY_EMAIL", credentials.Email);
                bool passwordSet = await GitHubTokenManager.SetRepositorySecret(repositoryName, "UNITY_PASSWORD", credentials.Password);

                if (!emailSet || !passwordSet)
                {
                    Debug.LogError("Failed to set basic Unity credentials");
                    return false;
                }

                // Set 2FA credentials based on configuration
                bool unity2FAEnabled = EditorPrefs.GetBool("U3D_Unity2FAEnabled", false);
                if (unity2FAEnabled)
                {
                    string authKey = EditorPrefs.GetString("U3D_UnityAuthKey", "");
                    if (!string.IsNullOrEmpty(authKey))
                    {
                        bool totpSet = await GitHubTokenManager.SetRepositorySecret(repositoryName, "UNITY_TOTP_KEY", authKey);
                        if (totpSet)
                        {
                            Debug.Log("✅ TOTP key configured - builds will be fully automated");
                        }
                        else
                        {
                            Debug.LogWarning("Failed to set TOTP key - will fall back to interactive mode");
                        }
                    }
                    else
                    {
                        Debug.Log("🎯 No TOTP key provided - interactive 2FA mode will be available");
                    }
                }

                // Add any existing Unity license if available
                string existingLicense = EditorPrefs.GetString("U3D_GeneratedUnityLicense", "");
                if (!string.IsNullOrEmpty(existingLicense))
                {
                    bool licenseSet = await GitHubTokenManager.SetRepositorySecret(repositoryName, "UNITY_LICENSE", existingLicense);
                    if (licenseSet)
                    {
                        Debug.Log("✅ Existing Unity license added to repository");
                    }
                }

                Debug.Log("✅ Enhanced Unity repository secrets configured successfully");
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to setup enhanced Unity repository secrets: {ex.Message}");
                return false;
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

        private void ShowDeploymentSummary(string repositoryName)
        {
            bool unity2FAEnabled = EditorPrefs.GetBool("U3D_Unity2FAEnabled", false);
            bool hasAuthKey = !string.IsNullOrEmpty(EditorPrefs.GetString("U3D_UnityAuthKey", ""));
            bool hasInteractive = EditorPrefs.GetBool("U3D_UnityInteractive2FA", false);

            string summaryMessage = "🎉 Publishing completed successfully!\n\n";
            summaryMessage += $"🌐 Your URL: {publishUrl}\n\n";
            summaryMessage += "🔧 Build Configuration:\n";

            if (unity2FAEnabled)
            {
                if (hasAuthKey)
                {
                    summaryMessage += "• Unity license: Fully automated (TOTP)\n";
                    summaryMessage += "• Future builds: Automatic on every push\n";
                }
                else if (hasInteractive)
                {
                    summaryMessage += "• Unity license: Interactive 2FA enabled\n";
                    summaryMessage += "• Manual builds: Available via GitHub Actions\n";
                    summaryMessage += "• You can trigger builds with 2FA codes when needed\n";
                }
            }
            else
            {
                summaryMessage += "• Unity license: Standard automation\n";
                summaryMessage += "• Builds: Automatic on every push\n";
            }

            summaryMessage += "\n💡 Next steps:\n";
            summaryMessage += "• Your content is live and accessible\n";
            summaryMessage += "• Push changes to trigger new builds\n";

            if (unity2FAEnabled && !hasAuthKey)
            {
                summaryMessage += "• Use GitHub Actions tab for manual 2FA builds\n";
            }

            EditorUtility.DisplayDialog("Publishing Success", summaryMessage, "Great!");

            // Optionally open the GitHub Actions page for interactive 2FA users
            if (unity2FAEnabled && !hasAuthKey && hasInteractive)
            {
                if (EditorUtility.DisplayDialog("GitHub Actions",
                    "Would you like to open GitHub Actions where you can manually trigger builds with 2FA codes?",
                    "Open GitHub", "Later"))
                {
                    string actionsUrl = $"https://github.com/{GitHubTokenManager.GetUsername()}/{repositoryName}/actions";
                    Application.OpenURL(actionsUrl);
                }
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