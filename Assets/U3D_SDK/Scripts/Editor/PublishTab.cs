using UnityEngine;
using UnityEditor;

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
            // Clear all publish-related EditorPrefs
            EditorPrefs.DeleteKey("U3D_PublishedURL");

            // Reset internal state
            publishUrl = "";
            githubConnected = false;
            projectSaved = false;
            deploymentComplete = false;
            currentStep = PublishStep.Ready;
            IsComplete = false;

            Debug.Log("Publish state reset - ready to publish again");
        }

        public void DrawTab()
        {
            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("Publish Your Experience", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Share your creation with the world! This will make your experience live on the internet.", MessageType.Info);
            EditorGUILayout.Space(15);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            if (currentStep == PublishStep.Ready)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Ready to go live?", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("This will:", EditorStyles.label);
                EditorGUILayout.LabelField("• Connect to online storage", EditorStyles.miniLabel);
                EditorGUILayout.LabelField("• Save your project safely", EditorStyles.miniLabel);
                EditorGUILayout.LabelField("• Make it live on the internet", EditorStyles.miniLabel);
                EditorGUILayout.LabelField("• Give you a URL to share", EditorStyles.miniLabel);
                EditorGUILayout.Space(10);

                if (GUILayout.Button("Make It Live!", GUILayout.Height(50)))
                {
                    StartPublishProcess();
                }
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(10);

            DrawPublishingSteps();

            if (currentStep == PublishStep.Complete)
            {
                DrawSuccessSection();
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawPublishingSteps()
        {
            EditorGUILayout.LabelField("Publishing Progress", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            DrawStep("Connect Online Storage",
                githubConnected,
                currentStep == PublishStep.ConnectingGitHub,
                "✓ Ready to save your project online",
                "🔗 Connecting to online storage...");

            DrawStep("Save Your Project",
                projectSaved,
                currentStep == PublishStep.SavingProject,
                "✓ Project saved safely",
                "💾 Saving your project...");

            DrawStep("Make It Live",
                deploymentComplete,
                currentStep == PublishStep.MakingLive,
                "✓ Your experience is now live!",
                "🚀 Making it live on the internet...");
        }

        private void DrawStep(string stepName, bool isComplete, bool isActive, string completeMessage, string activeMessage)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            if (isComplete)
            {
                EditorGUILayout.LabelField("✅", GUILayout.Width(25));
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

        private async void StartPublishProcess()
        {
            try
            {
                currentStep = PublishStep.ConnectingGitHub;
                await SimulateGitHubConnection();
                githubConnected = true;

                currentStep = PublishStep.SavingProject;
                await SimulateProjectSave();
                projectSaved = true;

                currentStep = PublishStep.MakingLive;
                await SimulateDeployment();
                deploymentComplete = true;

                currentStep = PublishStep.Complete;
                IsComplete = true;

                var creatorName = EditorPrefs.GetString("U3D_CreatorName", "creator");
                var projectName = Application.productName.ToLower().Replace(" ", "-");
                publishUrl = $"https://{creatorName}.u3d.com/{projectName}/";
                EditorPrefs.SetString("U3D_PublishedURL", publishUrl);

            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Publishing failed: {ex.Message}");
                EditorUtility.DisplayDialog("Publishing Failed", $"There was an error: {ex.Message}", "OK");
                currentStep = PublishStep.Ready;
            }
        }

        private async System.Threading.Tasks.Task SimulateGitHubConnection()
        {
            await System.Threading.Tasks.Task.Delay(2000);
        }

        private async System.Threading.Tasks.Task SimulateProjectSave()
        {
            await System.Threading.Tasks.Task.Delay(3000);
        }

        private async System.Threading.Tasks.Task SimulateDeployment()
        {
            await System.Threading.Tasks.Task.Delay(4000);
        }

        private void DrawSuccessSection()
        {
            EditorGUILayout.Space(20);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            var successStyle = new GUIStyle(EditorStyles.boldLabel);
            successStyle.normal.textColor = Color.green;
            successStyle.fontSize = 16;

            EditorGUILayout.LabelField("🎉 Success! Your experience is live!", successStyle);
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

            EditorGUILayout.LabelField("Share this URL with friends to let them experience your creation!", EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.EndVertical();
        }
    }
}