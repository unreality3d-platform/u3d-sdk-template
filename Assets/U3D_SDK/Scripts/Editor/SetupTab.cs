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
            UsernameReservation
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
                case AuthState.LoggedIn:
                    DrawLoggedIn();
                    break;
                case AuthState.UsernameReservation:
                    DrawUsernameReservation();
                    break;
            }

            EditorGUILayout.EndVertical();
        }

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
                "PayPal authentication provides:\n" +
                "• Instant monetization capabilities\n" +
                "• Professional creator verification\n" +
                "• Secure OAuth 2.0 authentication\n" +
                "• Automatic seller tier access",
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
                    "🔄 Waiting for PayPal authentication...\n\n" +
                    "Please complete the authentication in your web browser.\n" +
                    $"Time remaining: {Mathf.Ceil(remainingTime / 60)}:{(remainingTime % 60):00}",
                    MessageType.Info);

                // Show progress bar
                EditorGUI.ProgressBar(
                    GUILayoutUtility.GetRect(18, 18, "TextField"),
                    (TIMEOUT_DURATION - remainingTime) / TIMEOUT_DURATION,
                    "Authenticating..."
                );

                EditorGUILayout.Space(10);

                if (GUILayout.Button("Cancel Authentication"))
                {
                    StopPayPalPolling();
                    currentState = AuthState.PayPalLogin;
                }
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "⏱️ Authentication timed out.\n\n" +
                    "Please try again or use email/password authentication.",
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

        private void DrawLoggedIn()
        {
            EditorGUILayout.LabelField("✅ Authentication Complete", EditorStyles.boldLabel);
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
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // PayPal connection option for email/password users
            if (!U3DAuthenticator.PayPalConnected)
            {
                EditorGUILayout.HelpBox(
                    "💡 Connect PayPal to enable monetization features:\n" +
                    "• Sell content and experiences\n" +
                    "• Upgrade to professional creator tier\n" +
                    "• Access advanced platform features",
                    MessageType.Info);

                EditorGUILayout.Space(5);

                if (GUILayout.Button("🔗 Connect PayPal Account", GUILayout.Height(30)))
                {
                    StartPayPalLinking();
                }

                EditorGUILayout.Space(10);
            }

            if (GUILayout.Button("🚪 Logout"))
            {
                U3DAuthenticator.Logout();
                currentState = AuthState.MethodSelection;
                UpdateCompletion();
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
                    // Open PayPal OAuth URL in browser
                    Application.OpenURL(currentPayPalAuth.AuthUrl);

                    // Start polling for completion
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
                        // Authentication completed successfully
                        StopPayPalPolling();

                        if (string.IsNullOrEmpty(U3DAuthenticator.CreatorUsername))
                        {
                            currentState = AuthState.UsernameReservation;
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
                    // If not completed, continue polling
                }
                else
                {
                    // Authentication failed
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
                // Continue polling unless it's a critical error
            }
        }

        private void StopPayPalPolling()
        {
            isPollingPayPal = false;
            currentPayPalAuth = null;
        }

        // Manual authentication methods (existing implementation)
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

        // Username reservation methods (existing implementation)
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
                    currentState = AuthState.LoggedIn;
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
                        !string.IsNullOrEmpty(U3DAuthenticator.CreatorUsername);
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
            // Handle PayPal polling in the Editor update loop
            if (isPollingPayPal && currentState == AuthState.PayPalPolling)
            {
                // Polling is handled in DrawPayPalPolling method
                // This ensures the UI refreshes during polling
                if (EditorApplication.isPlaying == false) // Only repaint when not playing
                {
                    EditorApplication.QueuePlayerLoopUpdate();
                }
            }
        }
    }

    // Configuration data structure for JSON config file (existing)
    [System.Serializable]
    public class U3DConfiguration
    {
        public string productionApiKey;
        public string productionAuthDomain;
        public string productionProjectId;
        public string productionStorageBucket;
        public string productionMessagingSenderId;
        public string productionAppId;

        public string developmentApiKey;
        public string developmentAuthDomain;
        public string developmentProjectId;
        public string developmentStorageBucket;
        public string developmentMessagingSenderId;
        public string developmentAppId;
    }
}