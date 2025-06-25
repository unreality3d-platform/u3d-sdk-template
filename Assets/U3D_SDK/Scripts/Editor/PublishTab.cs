using UnityEngine;
using UnityEditor;
using System.IO;
using System.Threading.Tasks;

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

            if (GUILayout.Button("Make It Live!", GUILayout.Height(50)))
            {
                StartRealPublishProcess();
            }
            EditorGUILayout.EndVertical();
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

                // Step 2: Build Unity WebGL Project
                currentStep = PublishStep.SavingProject;
                currentStatus = "Building Unity WebGL project...";

                var buildResult = await BuildUnityProject(repoResult.LocalPath);
                if (!buildResult.Success)
                {
                    throw new System.Exception(buildResult.ErrorMessage);
                }

                projectSaved = true;
                currentStatus = "Unity build completed successfully";

                // Step 3: Deploy to GitHub Pages
                currentStep = PublishStep.MakingLive;
                currentStatus = "Deploying to GitHub Pages...";

                var deployResult = await DeployToGitHub(repoResult.LocalPath, repoResult.CloneUrl);
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

        private async Task<UnityBuildResult> BuildUnityProject(string outputPath)
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

            // Build WebGL
            var buildResult = await UnityBuildHelper.BuildWebGL(outputPath, (status) =>
            {
                currentStatus = status;
            });

            return buildResult;
        }

        private async Task<GitOperationResult> DeployToGitHub(string localPath, string cloneUrl)
        {
            // Check if git is available
            if (!await GitIntegration.IsGitAvailable())
            {
                return new GitOperationResult
                {
                    Success = false,
                    ErrorMessage = "Git is not installed or not available in PATH. Please install Git to continue."
                };
            }

            // Initialize git repository
            var initResult = await GitIntegration.InitializeRepository(localPath);
            if (!initResult.Success)
            {
                return initResult;
            }

            // Set up git user
            var username = GitHubTokenManager.GitHubUsername ?? "Unity User";
            var email = U3DAuthenticator.UserEmail ?? "user@example.com";
            await GitIntegration.SetupGitUser(localPath, username, email);

            // Add remote origin
            var remoteResult = await GitIntegration.AddRemoteOrigin(localPath, cloneUrl);
            if (!remoteResult.Success)
            {
                return remoteResult;
            }

            // Add all files
            var addResult = await GitIntegration.AddAllFiles(localPath);
            if (!addResult.Success)
            {
                return addResult;
            }

            // Commit changes
            var commitMessage = $"Initial Unity WebGL build for {Application.productName}";
            var commitResult = await GitIntegration.CommitChanges(localPath, commitMessage);
            if (!commitResult.Success)
            {
                return commitResult;
            }

            // Push to GitHub
            var pushResult = await GitIntegration.PushToRemote(localPath, "main");
            return pushResult;
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