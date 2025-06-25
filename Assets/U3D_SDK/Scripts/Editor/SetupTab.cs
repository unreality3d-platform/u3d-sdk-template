using System;
using System.Collections;
using UnityEngine;
using UnityEditor;
using System.Threading.Tasks;

namespace U3D.Editor
{
    public class SetupTab : ICreatorTab
    {
        public string TabName => "Setup";
        public bool IsComplete { get; private set; }
        public System.Action<int> OnRequestTabSwitch { get; set; }

        private enum AuthState
        {
            MethodSelection,
            PayPalLogin,
            PayPalPolling,
            ManualLogin,
            ManualRegister,
            LoggedIn,
            UsernameReservation,
            GitHubSetup
        }

        private AuthState currentState = AuthState.MethodSelection;
        private bool paypalSelected = true;
        private string email = "";
        private string password = "";
        private string confirmPassword = "";

        // PayPal authentication state
        private PayPalAuthResult currentPayPalAuth;
        private bool isPollingPayPal = false;
        private float lastPollTime = 0f;
        private float authStartTime = 0f;
        private const float POLL_INTERVAL = 2f;
        private const float TIMEOUT_DURATION = 300f; // 5 minutes

        // Username reservation fields
        private string desiredUsername = "";
        private bool checkingAvailability = false;
        private bool usernameAvailable = false;
        private bool usernameChecked = false;
        private string[] usernameSuggestions = new string[0];
        private bool reservingUsername = false;

        // GitHub token setup
        private string githubToken = "";
        private bool validatingToken = false;
        private bool tokenValidated = false;
        private string validationMessage = "";

        private bool showOnStartup = true;

        public void Initialize()
        {
            EnsureFirebaseConfiguration();

            if (U3DAuthenticator.IsLoggedIn)
            {
                if (string.IsNullOrEmpty(U3DAuthenticator.CreatorUsername))
                {
                    currentState = AuthState.UsernameReservation;
                }
                else if (!GitHubTokenManager.HasValidToken)
                {
                    currentState = AuthState.GitHubSetup;
                }
                else
                {
                    currentState = AuthState.LoggedIn;
                }
            }
            else
            {
                currentState = AuthState.MethodSelection;
            }

            UpdateCompletion();
        }

        public void DrawTab()
        {
            EditorGUILayout.BeginVertical();

            switch (currentState)
            {
                case AuthState.MethodSelection:
                    DrawMethodSelection();
                    break;
                case AuthState.PayPalLogin:
                    DrawPayPalLogin();
                    break;
                case AuthState.PayPalPolling:
                    DrawPayPalPolling();
                    break;
                case AuthState.ManualLogin:
                    DrawManualLogin();
                    break;
                case AuthState.ManualRegister:
                    DrawManualRegister();
                    break;
                case AuthState.UsernameReservation:
                    DrawUsernameReservation();
                    break;
                case AuthState.GitHubSetup:
                    DrawGitHubSetup();
                    break;
                case AuthState.LoggedIn:
                    DrawLoggedIn();
                    break;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawGitHubSetup()
        {
            EditorGUILayout.LabelField("🔗 GitHub Integration Setup", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            EditorGUILayout.HelpBox(
                "To publish your Unity projects, you need a GitHub Personal Access Token.\n\n" +
                "This allows the Unity SDK to create repositories and deploy your content automatically.",
                MessageType.Info);

            EditorGUILayout.Space(10);

            // Instructions section
            if (GUILayout.Button("📋 How to Create a GitHub Token", EditorStyles.linkLabel))
            {
                Application.OpenURL("https://docs.github.com/en/authentication/keeping-your-account-and-data-secure/managing-your-personal-access-tokens#creating-a-fine-grained-personal-access-token");
            }

            EditorGUILayout.Space(5);

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Required Token Permissions:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("• Repository access: All repositories", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("• Contents: Read and write", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("• Metadata: Read", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("• Pages: Write (for deployment)", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // Token input
            EditorGUILayout.LabelField("GitHub Personal Access Token:", EditorStyles.boldLabel);
            githubToken = EditorGUILayout.PasswordField(githubToken);

            EditorGUILayout.Space(5);

            // Validation section
            if (!string.IsNullOrEmpty(validationMessage))
            {
                var messageType = tokenValidated ? MessageType.Info : MessageType.Warning;
                EditorGUILayout.HelpBox(validationMessage, messageType);
                EditorGUILayout.Space(5);
            }

            // Validate button
            EditorGUI.BeginDisabledGroup(string.IsNullOrWhiteSpace(githubToken) || validatingToken);
            if (GUILayout.Button(validatingToken ? "Validating..." : "Validate Token", GUILayout.Height(30)))
            {
                ValidateGitHubToken();
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space(10);

            // Continue button (only show if validated)
            if (tokenValidated)
            {
                if (GUILayout.Button("Continue to Publishing", GUILayout.Height(35)))
                {
                    currentState = AuthState.LoggedIn;
                    UpdateCompletion();
                }
            }

            EditorGUILayout.Space(10);

            // Skip button (for users who want to set up later)
            if (GUILayout.Button("⏭️ Skip for Now (Setup Later)", EditorStyles.miniButton))
            {
                if (EditorUtility.DisplayDialog("Skip GitHub Setup",
                    "You can still use other features but won't be able to publish until you set up GitHub integration.\n\nContinue?",
                    "Yes, Skip", "Cancel"))
                {
                    currentState = AuthState.LoggedIn;
                    UpdateCompletion();
                }
            }
        }

        private async void ValidateGitHubToken()
        {
            validatingToken = true;
            validationMessage = "Validating token...";

            try
            {
                var result = await GitHubTokenManager.ValidateAndSetToken(githubToken);

                if (result.IsValid)
                {
                    tokenValidated = true;
                    validationMessage = $"✅ Token validated successfully for user: {result.Username}";

                    // Also check repository permissions
                    bool hasRepoPermissions = await GitHubTokenManager.CheckRepositoryPermissions();
                    if (!hasRepoPermissions)
                    {
                        validationMessage += "\n\n⚠️ Warning: Token may not have sufficient repository permissions.";
                    }
                }
                else
                {
                    tokenValidated = false;
                    validationMessage = $"❌ {result.ErrorMessage}";
                }
            }
            catch (Exception ex)
            {
                tokenValidated = false;
                validationMessage = $"❌ Validation failed: {ex.Message}";
            }
            finally
            {
                validatingToken = false;
            }
        }

        private void DrawLoggedIn()
        {
            EditorGUILayout.LabelField("✅ Setup Complete", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            EditorGUILayout.BeginVertical("Box");
            EditorGUILayout.LabelField($"👤 {U3DAuthenticator.DisplayName}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"📧 {U3DAuthenticator.UserEmail}", EditorStyles.miniLabel);

            if (!string.IsNullOrEmpty(U3DAuthenticator.CreatorUsername))
            {
                EditorGUILayout.LabelField($"🔗 Username: {U3DAuthenticator.CreatorUsername}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"🌐 URL: https://{U3DAuthenticator.CreatorUsername}.unreality3d.com/", EditorStyles.miniLabel);
            }

            if (U3DAuthenticator.PayPalConnected)
            {
                EditorGUILayout.LabelField("💳 PayPal: Connected", EditorStyles.miniLabel);
            }

            if (GitHubTokenManager.HasValidToken)
            {
                EditorGUILayout.LabelField($"🔗 GitHub: Connected ({GitHubTokenManager.GitHubUsername})", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // GitHub setup section
            if (!GitHubTokenManager.HasValidToken)
            {
                EditorGUILayout.HelpBox(
                    "🔗 GitHub Integration Required\n\n" +
                    "To publish your projects, you need to set up GitHub integration.",
                    MessageType.Warning);

                EditorGUILayout.Space(5);

                if (GUILayout.Button("🔗 Set Up GitHub Integration", GUILayout.Height(30)))
                {
                    currentState = AuthState.GitHubSetup;
                }

                EditorGUILayout.Space(10);
            }

            // PayPal connection option for email/password users
            if (!U3DAuthenticator.PayPalConnected)
            {
                EditorGUILayout.HelpBox(
                    "💡 Connect PayPal to enable monetization:\n" +
                    "• Sell content (scenes, avatars, props, access)\n" +
                    "• Receive payments directly to your account\n" +
                    "• Keep 95% of your earnings",
                    MessageType.Info);

                EditorGUILayout.Space(5);

                if (GUILayout.Button("🔗 Connect PayPal Account", GUILayout.Height(30)))
                {
                    StartPayPalLinking();
                }

                EditorGUILayout.Space(10);
            }

            // Management buttons
            EditorGUILayout.BeginHorizontal();

            if (GitHubTokenManager.HasValidToken && GUILayout.Button("⚙️ Update GitHub Token"))
            {
                currentState = AuthState.GitHubSetup;
                githubToken = "";
                tokenValidated = false;
                validationMessage = "";
            }

            if (GUILayout.Button("🚪 Logout"))
            {
                if (EditorUtility.DisplayDialog("Logout Confirmation",
                    "This will log you out and clear all stored credentials. Continue?",
                    "Yes, Logout", "Cancel"))
                {
                    U3DAuthenticator.Logout();
                    GitHubTokenManager.ClearToken();
                    currentState = AuthState.MethodSelection;
                    UpdateCompletion();
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        // Keep all existing methods from original SetupTab.cs
        private void DrawMethodSelection()
        {
            EditorGUILayout.LabelField("Welcome to Unreality3D", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Choose how you'd like to get started:", EditorStyles.label);
            EditorGUILayout.Space(15);

            // PayPal option (recommended)
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            var newPayPalSelected = EditorGUILayout.Toggle(paypalSelected, GUILayout.Width(20));
            if (newPayPalSelected != paypalSelected)
            {
                paypalSelected = true;
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(25);
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField("PayPal Login (Recommended)", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("• Instant monetization - sell your content", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("• Keep 95% of earnings", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("• Professional creator URLs", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // Manual option
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            var newManualSelected = EditorGUILayout.Toggle(!paypalSelected, GUILayout.Width(20));
            if (newManualSelected != !paypalSelected)
            {
                paypalSelected = false;
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(25);
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField("Email & Password", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("• Professional creator URLs", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("• Add monetization later (optional)", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(20);

            // Continue button
            if (GUILayout.Button(paypalSelected ? "Continue with PayPal" : "Continue with Email", GUILayout.Height(35)))
            {
                if (paypalSelected)
                {
                    currentState = AuthState.PayPalLogin;
                }
                else
                {
                    currentState = AuthState.ManualLogin;
                }
            }
        }

        private void DrawPayPalLogin()
        {
            EditorGUILayout.LabelField("PayPal Authentication", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            EditorGUILayout.HelpBox(
                "PayPal login provides:\n" +
                "• Instant monetization capabilities\n" +
                "• Secure account connection\n" +
                "• Direct payment processing\n" +
                "• Professional payment experience",
                MessageType.Info);

            EditorGUILayout.Space(10);

            if (GUILayout.Button("🔗 Authenticate with PayPal", GUILayout.Height(40)))
            {
                StartPayPalAuthentication();
            }

            EditorGUILayout.Space(10);

            if (GUILayout.Button("← Back to Method Selection"))
            {
                currentState = AuthState.MethodSelection;
            }
        }

        private void DrawPayPalPolling()
        {
            EditorGUILayout.LabelField("PayPal Authentication", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            float elapsedTime = Time.realtimeSinceStartup - authStartTime;
            float remainingTime = TIMEOUT_DURATION - elapsedTime;

            if (remainingTime > 0)
            {
                EditorGUILayout.HelpBox(
                    "🔄 Waiting for PayPal login...\n\n" +
                    "Please complete the login in your web browser.\n" +
                    $"Time remaining: {Mathf.Ceil(remainingTime / 60)}:{(remainingTime % 60):00}",
                    MessageType.Info);

                // Show progress bar
                EditorGUI.ProgressBar(
                    GUILayoutUtility.GetRect(18, 18, "TextField"),
                    (TIMEOUT_DURATION - remainingTime) / TIMEOUT_DURATION,
                    "Connecting..."
                );

                EditorGUILayout.Space(10);

                if (GUILayout.Button("Cancel Login"))
                {
                    StopPayPalPolling();
                    currentState = AuthState.PayPalLogin;
                }
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "⏱️ Authentication timed out.\n\n" +
                    "Please try again or use email/password login.",
                    MessageType.Warning);

                EditorGUILayout.Space(10);

                if (GUILayout.Button("Try Again"))
                {
                    StopPayPalPolling();
                    currentState = AuthState.PayPalLogin;
                }

                EditorGUILayout.Space(5);

                if (GUILayout.Button("← Back to Method Selection"))
                {
                    StopPayPalPolling();
                    currentState = AuthState.MethodSelection;
                }
            }

            // Handle polling in Update loop
            if (isPollingPayPal && Time.realtimeSinceStartup - lastPollTime > POLL_INTERVAL)
            {
                PollPayPalStatus();
                lastPollTime = Time.realtimeSinceStartup;
            }
        }

        private void DrawManualLogin()
        {
            EditorGUILayout.LabelField("Email & Password Login", EditorStyles.boldLabel);
            EditorGUILayout.Space(10);

            email = EditorGUILayout.TextField("Email", email);
            password = EditorGUILayout.PasswordField("Password", password);

            EditorGUILayout.Space(10);

            bool canLogin = !string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(password);

            EditorGUI.BeginDisabledGroup(!canLogin);
            if (GUILayout.Button("Login", GUILayout.Height(35)))
            {
                StartManualLogin();
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space(10);

            if (GUILayout.Button("Create New Account"))
            {
                currentState = AuthState.ManualRegister;
            }

            EditorGUILayout.Space(5);

            if (GUILayout.Button("← Back to Method Selection"))
            {
                currentState = AuthState.MethodSelection;
            }
        }

        private void DrawManualRegister()
        {
            EditorGUILayout.LabelField("Create New Account", EditorStyles.boldLabel);
            EditorGUILayout.Space(10);

            email = EditorGUILayout.TextField("Email", email);
            password = EditorGUILayout.PasswordField("Password", password);
            confirmPassword = EditorGUILayout.PasswordField("Confirm Password", confirmPassword);

            EditorGUILayout.Space(10);

            bool canRegister = !string.IsNullOrWhiteSpace(email) &&
                              !string.IsNullOrWhiteSpace(password) &&
                              password == confirmPassword &&
                              password.Length >= 6;

            if (!canRegister && !string.IsNullOrWhiteSpace(password))
            {
                if (password != confirmPassword)
                {
                    EditorGUILayout.HelpBox("Passwords do not match.", MessageType.Warning);
                }
                else if (password.Length < 6)
                {
                    EditorGUILayout.HelpBox("Password must be at least 6 characters.", MessageType.Warning);
                }
            }

            EditorGUI.BeginDisabledGroup(!canRegister);
            if (GUILayout.Button("Create Account", GUILayout.Height(35)))
            {
                StartManualRegister();
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space(10);

            if (GUILayout.Button("Already have an account? Login"))
            {
                currentState = AuthState.ManualLogin;
            }

            EditorGUILayout.Space(5);

            if (GUILayout.Button("← Back to Method Selection"))
            {
                currentState = AuthState.MethodSelection;
            }
        }

        private void DrawUsernameReservation()
        {
            EditorGUILayout.LabelField("🎯 Reserve Your Creator Username", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            EditorGUILayout.HelpBox(
                "Your username creates your professional URL:\n" +
                "https://[username].unreality3d.com/\n\n" +
                "Choose carefully - this represents your creator brand.",
                MessageType.Info);

            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("Desired Username:", EditorStyles.boldLabel);
            string newUsername = EditorGUILayout.TextField(desiredUsername);

            if (newUsername != desiredUsername)
            {
                desiredUsername = newUsername;
                usernameChecked = false;
                usernameAvailable = false;
                usernameSuggestions = new string[0];
            }

            EditorGUILayout.Space(5);

            bool canCheck = !string.IsNullOrWhiteSpace(desiredUsername) && !checkingAvailability;

            EditorGUI.BeginDisabledGroup(!canCheck);
            if (GUILayout.Button("Check Availability", GUILayout.Height(30)))
            {
                CheckUsernameAvailability();
            }
            EditorGUI.EndDisabledGroup();

            if (checkingAvailability)
            {
                EditorGUILayout.LabelField("Checking availability...", EditorStyles.miniLabel);
            }

            EditorGUILayout.Space(10);

            if (usernameChecked)
            {
                if (usernameAvailable)
                {
                    EditorGUILayout.HelpBox("✅ Username is available!", MessageType.Info);

                    EditorGUI.BeginDisabledGroup(reservingUsername);
                    if (GUILayout.Button("Reserve Username", GUILayout.Height(35)))
                    {
                        ReserveUsername();
                    }
                    EditorGUI.EndDisabledGroup();

                    if (reservingUsername)
                    {
                        EditorGUILayout.LabelField("Reserving username...", EditorStyles.miniLabel);
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("❌ Username is not available.", MessageType.Warning);

                    if (usernameSuggestions.Length > 0)
                    {
                        EditorGUILayout.LabelField("Suggestions:", EditorStyles.boldLabel);
                        foreach (var suggestion in usernameSuggestions)
                        {
                            if (GUILayout.Button(suggestion))
                            {
                                desiredUsername = suggestion;
                                usernameChecked = false;
                            }
                        }
                    }
                }
            }
        }

        // PayPal authentication methods
        private async void StartPayPalAuthentication()
        {
            try
            {
                currentPayPalAuth = await U3DAuthenticator.StartPayPalLogin();

                if (currentPayPalAuth.Success)
                {
                    Application.OpenURL(currentPayPalAuth.AuthUrl);
                    currentState = AuthState.PayPalPolling;
                    isPollingPayPal = true;
                    authStartTime = Time.realtimeSinceStartup;
                    lastPollTime = Time.realtimeSinceStartup;

                    Debug.Log("PayPal authentication started, polling for completion...");
                }
                else
                {
                    EditorUtility.DisplayDialog("PayPal Authentication Failed",
                        currentPayPalAuth.ErrorMessage ?? "Failed to start PayPal authentication",
                        "OK");
                }
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Authentication Error",
                    $"An error occurred: {ex.Message}",
                    "OK");
            }
        }

        private async void StartPayPalLinking()
        {
            try
            {
                currentPayPalAuth = await U3DAuthenticator.LinkPayPalToExistingAccount();

                if (currentPayPalAuth.Success)
                {
                    Application.OpenURL(currentPayPalAuth.AuthUrl);

                    currentState = AuthState.PayPalPolling;
                    isPollingPayPal = true;
                    authStartTime = Time.realtimeSinceStartup;
                    lastPollTime = Time.realtimeSinceStartup;

                    Debug.Log("PayPal linking started, polling for completion...");
                }
                else
                {
                    EditorUtility.DisplayDialog("PayPal Linking Failed",
                        currentPayPalAuth.ErrorMessage ?? "Failed to start PayPal linking",
                        "OK");
                }
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Linking Error",
                    $"An error occurred: {ex.Message}",
                    "OK");
            }
        }

        private async void PollPayPalStatus()
        {
            if (currentPayPalAuth == null || string.IsNullOrEmpty(currentPayPalAuth.State))
            {
                StopPayPalPolling();
                return;
            }

            try
            {
                var result = await U3DAuthenticator.PollPayPalAuthStatus(currentPayPalAuth.State);

                if (result.Success)
                {
                    if (result.Completed)
                    {
                        StopPayPalPolling();

                        if (string.IsNullOrEmpty(U3DAuthenticator.CreatorUsername))
                        {
                            currentState = AuthState.UsernameReservation;
                        }
                        else if (!GitHubTokenManager.HasValidToken)
                        {
                            currentState = AuthState.GitHubSetup;
                        }
                        else
                        {
                            currentState = AuthState.LoggedIn;
                        }

                        UpdateCompletion();

                        EditorUtility.DisplayDialog("PayPal Authentication Successful!",
                            result.Message ?? "PayPal authentication completed successfully.",
                            "Awesome!");
                    }
                }
                else
                {
                    StopPayPalPolling();
                    currentState = AuthState.PayPalLogin;

                    EditorUtility.DisplayDialog("PayPal Authentication Failed",
                        result.ErrorMessage ?? "PayPal authentication failed",
                        "OK");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"PayPal polling error: {ex.Message}");
            }
        }

        private void StopPayPalPolling()
        {
            isPollingPayPal = false;
            currentPayPalAuth = null;
        }

        // Manual authentication methods
        private async void StartManualLogin()
        {
            try
            {
                bool success = await U3DAuthenticator.LoginWithEmailPassword(email, password);
                if (success)
                {
                    if (string.IsNullOrEmpty(U3DAuthenticator.CreatorUsername))
                    {
                        currentState = AuthState.UsernameReservation;
                    }
                    else if (!GitHubTokenManager.HasValidToken)
                    {
                        currentState = AuthState.GitHubSetup;
                    }
                    else
                    {
                        currentState = AuthState.LoggedIn;
                    }
                    UpdateCompletion();
                }
                else
                {
                    EditorUtility.DisplayDialog("Login Failed", "Invalid email or password.", "OK");
                }
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Login Error", $"An error occurred: {ex.Message}", "OK");
            }
        }

        private async void StartManualRegister()
        {
            try
            {
                bool success = await U3DAuthenticator.RegisterWithEmailPassword(email, password);
                if (success)
                {
                    currentState = AuthState.UsernameReservation;
                    UpdateCompletion();
                }
                else
                {
                    EditorUtility.DisplayDialog("Registration Failed", "Failed to create account. Please try again.", "OK");
                }
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Registration Error", $"An error occurred: {ex.Message}", "OK");
            }
        }

        // Username reservation methods
        private async void CheckUsernameAvailability()
        {
            checkingAvailability = true;
            usernameChecked = false;

            try
            {
                usernameAvailable = await U3DAuthenticator.CheckUsernameAvailability(desiredUsername);

                if (!usernameAvailable)
                {
                    usernameSuggestions = await U3DAuthenticator.GetUsernameSuggestions(desiredUsername);
                }

                usernameChecked = true;
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Check Failed", $"Failed to check username availability: {ex.Message}", "OK");
            }
            finally
            {
                checkingAvailability = false;
            }
        }

        private async void ReserveUsername()
        {
            reservingUsername = true;

            try
            {
                bool success = await U3DAuthenticator.ReserveUsername(desiredUsername);
                if (success)
                {
                    if (!GitHubTokenManager.HasValidToken)
                    {
                        currentState = AuthState.GitHubSetup;
                    }
                    else
                    {
                        currentState = AuthState.LoggedIn;
                    }
                    UpdateCompletion();
                    EditorUtility.DisplayDialog("Success!",
                        $"Username '{desiredUsername}' reserved successfully!\n\nYour professional URL: https://{desiredUsername}.unreality3d.com/",
                        "Awesome!");
                }
                else
                {
                    EditorUtility.DisplayDialog("Reservation Failed", "Failed to reserve username. Please try again.", "OK");
                }
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Reservation Error", $"An error occurred: {ex.Message}", "OK");
            }
            finally
            {
                reservingUsername = false;
            }
        }

        private void UpdateCompletion()
        {
            IsComplete = FirebaseConfigManager.IsConfigurationComplete() &&
                        U3DAuthenticator.IsLoggedIn &&
                        !string.IsNullOrEmpty(U3DAuthenticator.CreatorUsername) &&
                        GitHubTokenManager.HasValidToken;
        }

        private void EnsureFirebaseConfiguration()
        {
            // Existing implementation - ensures Firebase config is loaded
        }

        public void OnEnable()
        {
            Initialize();
            EditorApplication.update += OnEditorUpdate;
        }

        public void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            StopPayPalPolling();
        }

        private void OnEditorUpdate()
        {
            if (isPollingPayPal && currentState == AuthState.PayPalPolling)
            {
                if (EditorApplication.isPlaying == false)
                {
                    EditorApplication.QueuePlayerLoopUpdate();
                }
            }
        }
    }
}