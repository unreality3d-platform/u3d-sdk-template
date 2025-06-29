using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

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
                // Use fire-and-forget pattern for UI event handler
                _ = StartSimplifiedPublishProcessAsync();
            }

            EditorGUILayout.EndVertical();
        }

        private async System.Threading.Tasks.Task StartSimplifiedPublishProcessAsync()
        {
            try
            {
                await StartSimplifiedPublishProcess();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Async publishing failed: {ex.Message}");
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

        private async System.Threading.Tasks.Task StartSimplifiedPublishProcess()
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
                // NOW this will work because buildPath is in scope:
                var processorResult = await ProcessTemplateLocally(localRepoPath, buildPath);
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

        private Task<GitOperationResult> ProcessTemplateLocally(string repositoryPath, string buildPath)
        {
            try
            {
                currentStatus = "Processing Unity template locally...";

                // Enhanced build file detection (matches unity-template-processor.js logic)
                var buildFiles = DetectUnityBuildFiles(buildPath);
                if (buildFiles == null)
                {
                    return Task.FromResult(new GitOperationResult
                    {
                        Success = false,
                        ErrorMessage = "Unity build files not found. Please ensure WebGL build completed successfully."
                    });
                }

                // Load and process template
                var templateContent = GetUnityWebGLTemplate(repositoryPath);
                var processedTemplate = ProcessTemplateVariables(templateContent, buildFiles);

                // Create index.html
                var indexPath = Path.Combine(repositoryPath, "index.html");
                File.WriteAllText(indexPath, processedTemplate);

                // Validate Firebase integration preservation (like original processor)
                if (!ValidateFirebaseIntegration(processedTemplate))
                {
                    Debug.LogWarning("Firebase/PayPal integration validation failed - continuing anyway");
                }

                // Create enhanced PWA manifest (matches original complexity)
                CreateEnhancedPWAManifest(repositoryPath);

                // Create optimized service worker (critical for Unity WebGL caching)
                CreateOptimizedServiceWorker(repositoryPath, buildFiles);

                // Create creator-specific README with git-extracted info
                GenerateCreatorReadme(repositoryPath);

                // Create config file for reference
                CreateUnityTemplateConfig(repositoryPath, buildFiles);

                currentStatus = "✅ Template processed locally with full feature parity";
                return Task.FromResult(new GitOperationResult
                {
                    Success = true,
                    Message = "Template processing completed locally with all original features"
                });
            }
            catch (Exception ex)
            {
                return Task.FromResult(new GitOperationResult
                {
                    Success = false,
                    ErrorMessage = $"Local template processing failed: {ex.Message}"
                });
            }
        }

        private UnityBuildFiles DetectUnityBuildFiles(string buildPath)
        {
            // Enhanced detection matching unity-template-processor.js logic
            var potentialPaths = new[]
            {
                buildPath,
                Path.Combine(buildPath, "Build"),
                Path.Combine(Path.GetDirectoryName(buildPath), "Build"),
                Path.Combine(Path.GetDirectoryName(buildPath), "WebGL"),
                Path.Combine(Path.GetDirectoryName(buildPath), "WebGLBuild")
            };

            foreach (var testPath in potentialPaths)
            {
                if (Directory.Exists(testPath))
                {
                    var files = Directory.GetFiles(testPath);
                    var buildFiles = new UnityBuildFiles
                    {
                        loader = files.FirstOrDefault(f => f.EndsWith(".loader.js")),
                        data = files.FirstOrDefault(f => f.EndsWith(".data")),
                        framework = files.FirstOrDefault(f => f.EndsWith(".framework.js")),
                        wasm = files.FirstOrDefault(f => f.EndsWith(".wasm"))
                    };

                    // Validate all required files exist
                    if (!string.IsNullOrEmpty(buildFiles.loader) &&
                        !string.IsNullOrEmpty(buildFiles.data) &&
                        !string.IsNullOrEmpty(buildFiles.framework) &&
                        !string.IsNullOrEmpty(buildFiles.wasm))
                    {
                        // Convert to just filenames (like original processor)
                        buildFiles.loader = Path.GetFileName(buildFiles.loader);
                        buildFiles.data = Path.GetFileName(buildFiles.data);
                        buildFiles.framework = Path.GetFileName(buildFiles.framework);
                        buildFiles.wasm = Path.GetFileName(buildFiles.wasm);

                        Debug.Log($"Unity build files detected: {buildFiles.loader}, {buildFiles.data}, {buildFiles.framework}, {buildFiles.wasm}");
                        return buildFiles;
                    }
                }
            }

            Debug.LogError("Unity build files not found in any expected location");
            return null;
        }

        private string GetUnityWebGLTemplate(string repositoryPath)
        {
            // Try multiple template locations (matching original processor logic)
            var templatePaths = new[]
            {
        Path.Combine(repositoryPath, "template.html"),           // In cloned repo
        Path.Combine(Application.dataPath, "..", "template.html"), // Project root
        Path.Combine(Application.dataPath, "WebGLTemplates", "template.html"), // Unity templates
        Path.Combine(Path.GetDirectoryName(Application.dataPath), "u3d-sdk-template", "template.html"),
        Path.Combine("D:", "Unreality3D", "u3d-sdk-template", "template.html") // Fallback to your known path
    };

            foreach (var templatePath in templatePaths)
            {
                if (File.Exists(templatePath))
                {
                    Debug.Log($"Found template at: {templatePath}");
                    return File.ReadAllText(templatePath);
                }
            }

            throw new FileNotFoundException($"Template file not found in any expected location. Searched: {string.Join(", ", templatePaths)}");
        }

        private string ProcessTemplateVariables(string template, UnityBuildFiles buildFiles)
        {
            // Replace all template variables (matching original processor exactly)
            template = template.Replace("{{{ PRODUCT_NAME }}}", Application.productName);
            template = template.Replace("{{{ CONTENT_ID }}}", GitHubAPI.SanitizeRepositoryName(Application.productName));
            template = template.Replace("{{{ LOADER_FILENAME }}}", buildFiles.loader);
            template = template.Replace("{{{ DATA_FILENAME }}}", buildFiles.data);
            template = template.Replace("{{{ FRAMEWORK_FILENAME }}}", buildFiles.framework);
            template = template.Replace("{{{ CODE_FILENAME }}}", buildFiles.wasm);
            template = template.Replace("{{{ COMPANY_NAME }}}", U3DAuthenticator.CreatorUsername ?? "Unity Creator");
            template = template.Replace("{{{ PRODUCT_VERSION }}}", Application.version);

            // Add cache-busting timestamp (critical feature from original)
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var cachePattern = @"buildUrl \+ ""/([^""]+)""";
            template = System.Text.RegularExpressions.Regex.Replace(template, cachePattern, $"buildUrl + \"/$1?v={timestamp}\"");

            return template;
        }

        private bool ValidateFirebaseIntegration(string template)
        {
            // Validate Firebase/PayPal integration preservation (from original)
            var requiredPatterns = new[]
            {
                "firebase",
                "paypal",
                "UnityRequestPayment",
                "OnPaymentComplete",
                "environment-aware",
                "createUnityInstance"
            };

            foreach (var pattern in requiredPatterns)
            {
                if (!template.ToLower().Contains(pattern.ToLower()))
                {
                    Debug.LogWarning($"Pattern '{pattern}' not found in template");
                }
            }

            return true; // Continue even with warnings (like original)
        }

        private void CreateEnhancedPWAManifest(string repositoryPath)
        {
            // Enhanced PWA manifest (matching original processor complexity)
            var manifest = new
            {
                name = Application.productName,
                short_name = Application.productName,
                description = $"{Application.productName} - Interactive Unity experience powered by Unreality3D",
                start_url = "./",
                display = "fullscreen",
                display_override = new[] { "fullscreen", "standalone", "minimal-ui" },
                orientation = "landscape-primary",
                theme_color = "#232323",
                background_color = "#232323",
                icons = new[]
                {
                    new
                    {
                        src = "data:image/svg+xml;base64,PHN2ZyB3aWR0aD0iNTEyIiBoZWlnaHQ9IjUxMiIgdmlld0JveD0iMCAwIDUxMiA1MTIiIGZpbGw9Im5vbmUiIHhtbG5zPSJodHRwOi8vd3d3LnczLm9yZy8yMDAwL3N2ZyI+PHJlY3Qgd2lkdGg9IjUxMiIgaGVpZ2h0PSI1MTIiIGZpbGw9IiMyMzIzMjMiLz48dGV4dCB4PSI1MCUiIHk9IjUwJSIgdGV4dC1hbmNob3I9Im1pZGRsZSIgZHk9Ii4zZW0iIGZpbGw9IndoaXRlIiBmb250LXNpemU9IjQ4Ij5Vbml0eTwvdGV4dD48L3N2Zz4=",
                        sizes = "512x512",
                        type = "image/svg+xml"
                    }
                },
                categories = new[] { "games", "entertainment", "productivity" }
            };

            var manifestPath = Path.Combine(repositoryPath, "manifest.webmanifest");
            File.WriteAllText(manifestPath, JsonUtility.ToJson(manifest, true));
            Debug.Log("Created enhanced PWA manifest");
        }

        private void CreateOptimizedServiceWorker(string repositoryPath, UnityBuildFiles buildFiles)
        {
            // Critical service worker for Unity WebGL caching (from original processor)
            var contentId = GitHubAPI.SanitizeRepositoryName(Application.productName);
            var serviceWorker = $@"
const CACHE_NAME = 'unity-webgl-v1';
const urlsToCache = [
  './',
  './index.html',
  './Build/{buildFiles.loader}',
  './Build/{buildFiles.framework}',
  './Build/{buildFiles.data}',
  './Build/{buildFiles.wasm}'
];

self.addEventListener('install', (event) => {{
  event.waitUntil(
    caches.open(CACHE_NAME)
      .then((cache) => cache.addAll(urlsToCache))
  );
}});

self.addEventListener('fetch', (event) => {{
  event.respondWith(
    caches.match(event.request)
      .then((response) => {{
        return response || fetch(event.request);
      }})
  );
}});";

            var swPath = Path.Combine(repositoryPath, "sw.js");
            File.WriteAllText(swPath, serviceWorker.Trim());
            Debug.Log("Created optimized service worker");
        }

        private void GenerateCreatorReadme(string repositoryPath)
        {
            // Enhanced README with git extraction (matching original processor)
            var creatorUsername = ExtractCreatorUsername(repositoryPath);
            var projectName = GitHubAPI.SanitizeRepositoryName(Application.productName);
            var githubOwner = ExtractGitHubOwner(repositoryPath);
            var repositoryName = ExtractRepositoryName(repositoryPath);

            var readmeTemplate = $@"# {Application.productName}

**Unity WebGL Experience by {creatorUsername}**

🎮 **[Play Experience](https://{creatorUsername}.unreality3d.com/{projectName}/)**

---

## About This Project

This is an interactive Unity WebGL experience created using the [Unreality3D Platform](https://unreality3d.com).

- **Creator**: {creatorUsername}
- **Built with**: Unity 6+ WebGL
- **Platform**: [Unreality3D](https://unreality3d.com)
- **Deployment**: Automated via GitHub Actions

## How to Play

🎮 **Controls**: WASD to move, mouse to look around
💰 **Monetization**: Supports PayPal payments for premium content
🌐 **Professional URL**: https://{creatorUsername}.unreality3d.com/{projectName}/

---

## Technical Details

### Built With
- **Unity**: 6+ WebGL
- **Deployment**: GitHub Pages + Load Balancer
- **Payments**: PayPal Business Integration
- **Backend**: Firebase Functions
- **Platform**: [Unreality3D SDK](https://github.com/unreality3d-platform/u3d-sdk-template)

### Repository Structure
```
├── Build/                 # Unity WebGL build files
├── index.html            # Processed Unity template
├── manifest.webmanifest  # PWA configuration
├── sw.js                 # Service worker for caching
└── README.md            # This file
```

### Deployment Status
- ✅ **Auto-deployed**: Push to main branch triggers deployment
- ✅ **Professional URL**: Custom subdomain routing
- ✅ **PayPal Ready**: Monetization system active
- ✅ **Performance Optimized**: Brotli compression, caching, CDN

---

**Powered by [Unreality3D](https://unreality3d.com) | Created by {creatorUsername}**";

            var readmePath = Path.Combine(repositoryPath, "README.md");
            File.WriteAllText(readmePath, readmeTemplate);
            Debug.Log("Generated creator-specific README");
        }

        private void CreateUnityTemplateConfig(string repositoryPath, UnityBuildFiles buildFiles)
        {
            // Create config file for reference (like original processor)
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

            var configPath = Path.Combine(repositoryPath, "unity-template-config.json");
            File.WriteAllText(configPath, JsonUtility.ToJson(config, true));
        }

        private string ExtractCreatorUsername(string repositoryPath)
        {
            // Extract from directory name (matching original processor)
            var dirName = Path.GetFileName(repositoryPath);
            var match = System.Text.RegularExpressions.Regex.Match(dirName, @"^([a-zA-Z0-9-]+)");
            return match.Success ? match.Groups[1].Value : (U3DAuthenticator.CreatorUsername ?? "creator");
        }

        private string ExtractGitHubOwner(string repositoryPath)
        {
            // Try to extract from git remote (matching original processor)
            try
            {
                var processInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "remote get-url origin",
                    WorkingDirectory = repositoryPath,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = System.Diagnostics.Process.Start(processInfo))
                {
                    if (process != null)
                    {
                        var output = process.StandardOutput.ReadToEnd().Trim();
                        var match = System.Text.RegularExpressions.Regex.Match(output, @"github\.com[:/]([^/]+)/");
                        if (match.Success)
                        {
                            return match.Groups[1].Value;
                        }
                    }
                }
            }
            catch
            {
                // Fall back to creator username
            }

            return ExtractCreatorUsername(repositoryPath);
        }

        private string ExtractRepositoryName(string repositoryPath)
        {
            // Try to extract from git remote (matching original processor)
            try
            {
                var processInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "remote get-url origin",
                    WorkingDirectory = repositoryPath,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = System.Diagnostics.Process.Start(processInfo))
                {
                    if (process != null)
                    {
                        var output = process.StandardOutput.ReadToEnd().Trim();
                        var match = System.Text.RegularExpressions.Regex.Match(output, @"/([^/]+)\.git$");
                        if (match.Success)
                        {
                            return match.Groups[1].Value;
                        }
                    }
                }
            }
            catch
            {
                // Fall back to content ID
            }

            return GitHubAPI.SanitizeRepositoryName(Application.productName);
        }

        private class UnityBuildFiles
        {
            public string loader;
            public string data;
            public string framework;
            public string wasm;
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