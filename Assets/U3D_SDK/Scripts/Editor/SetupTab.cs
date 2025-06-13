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
            // Secure auto-configuration - no hardcoded keys in source
            EnsureFirebaseConfiguration();

            // Check if already logged in
            if (Unreality3DAuthenticator.IsLoggedIn)
            {
                if (string.IsNullOrEmpty(Unreality3DAuthenticator.CreatorUsername))
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
            showOnStartup = EditorPrefs.GetBool("U3D_ShowOnStartup", true);
        }

        private void EnsureFirebaseConfiguration()
        {
            // Check if configuration is already complete
            if (FirebaseConfigManager.IsConfigurationComplete())
            {
                return; // Already configured
            }

            // SECURITY FIX: Load configuration from secure external source
            // instead of hardcoding in source code
            LoadSecureConfiguration();
        }

        private void LoadSecureConfiguration()
        {
            // Try to load from multiple secure sources in order of preference
            if (TryLoadFromEnvironmentVariables()) return;
            if (TryLoadFromConfigFile()) return;
            if (TryLoadFromEditorPrefs()) return;

            // If no configuration found, show setup requirement
            ShowConfigurationRequired();
        }

        private bool TryLoadFromEnvironmentVariables()
        {
            // Check for environment variables (ideal for CI/CD)
            string prodApiKey = System.Environment.GetEnvironmentVariable("U3D_PROD_API_KEY");
            string devApiKey = System.Environment.GetEnvironmentVariable("U3D_DEV_API_KEY");

            if (!string.IsNullOrEmpty(prodApiKey) && !string.IsNullOrEmpty(devApiKey))
            {
                SetupConfigurationFromEnvironment(prodApiKey, devApiKey);
                return true;
            }

            return false;
        }

        private bool TryLoadFromConfigFile()
        {
            // Check for local config file (git-ignored)
            string configPath = Application.dataPath + "/../u3d-config.json";

            if (System.IO.File.Exists(configPath))
            {
                try
                {
                    string configJson = System.IO.File.ReadAllText(configPath);
                    var config = JsonUtility.FromJson<U3DConfiguration>(configJson);

                    if (config != null && !string.IsNullOrEmpty(config.productionApiKey))
                    {
                        SetupConfigurationFromFile(config);
                        return true;
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"Failed to load config file: {ex.Message}");
                }
            }

            return false;
        }

        private bool TryLoadFromEditorPrefs()
        {
            // Check if already stored in EditorPrefs from previous setup
            string prodApiKey = EditorPrefs.GetString("U3D_Firebase_PRODUCTION_ApiKey", "");
            string devApiKey = EditorPrefs.GetString("U3D_Firebase_DEVELOPMENT_ApiKey", "");

            if (!string.IsNullOrEmpty(prodApiKey) && !string.IsNullOrEmpty(devApiKey))
            {
                // Configuration already exists
                return true;
            }

            return false;
        }

        private void SetupConfigurationFromEnvironment(string prodApiKey, string devApiKey)
        {
            var productionConfig = new FirebaseEnvironmentConfig
            {
                apiKey = prodApiKey,
                authDomain = "unreality3d.firebaseapp.com",
                projectId = "unreality3d",
                storageBucket = "unreality3d.firebasestorage.app",
                messagingSenderId = System.Environment.GetEnvironmentVariable("U3D_PROD_MESSAGING_ID") ?? "183773881724",
                appId = System.Environment.GetEnvironmentVariable("U3D_PROD_APP_ID") ?? "1:183773881724:web:50ca32baa00960b46170f9",
                measurementId = "G-YXC3XB3PFL"
            };

            var developmentConfig = new FirebaseEnvironmentConfig
            {
                apiKey = devApiKey,
                authDomain = "unreality3d2025.firebaseapp.com",
                projectId = "unreality3d2025",
                storageBucket = "unreality3d2025.firebasestorage.app",
                messagingSenderId = System.Environment.GetEnvironmentVariable("U3D_DEV_MESSAGING_ID") ?? "244081840635",
                appId = System.Environment.GetEnvironmentVariable("U3D_DEV_APP_ID") ?? "1:244081840635:web:71c37efb6b172a706dbb5e",
                measurementId = "G-YXC3XB3PFL"
            };

            FirebaseConfigManager.SetEnvironmentConfig("production", productionConfig);
            FirebaseConfigManager.SetEnvironmentConfig("development", developmentConfig);

            Debug.Log("Firebase configuration loaded from environment variables");
        }

        private void SetupConfigurationFromFile(U3DConfiguration config)
        {
            var productionConfig = new FirebaseEnvironmentConfig
            {
                apiKey = config.productionApiKey,
                authDomain = config.productionAuthDomain ?? "unreality3d.firebaseapp.com",
                projectId = config.productionProjectId ?? "unreality3d",
                storageBucket = config.productionStorageBucket ?? "unreality3d.firebasestorage.app",
                messagingSenderId = config.productionMessagingSenderId ?? "183773881724",
                appId = config.productionAppId ?? "1:183773881724:web:50ca32baa00960b46170f9",
                measurementId = "G-YXC3XB3PFL"
            };

            var developmentConfig = new FirebaseEnvironmentConfig
            {
                apiKey = config.developmentApiKey,
                authDomain = config.developmentAuthDomain ?? "unreality3d2025.firebaseapp.com",
                projectId = config.developmentProjectId ?? "unreality3d2025",
                storageBucket = config.developmentStorageBucket ?? "unreality3d2025.firebasestorage.app",
                messagingSenderId = config.developmentMessagingSenderId ?? "244081840635",
                appId = config.developmentAppId ?? "1:244081840635:web:71c37efb6b172a706dbb5e",
                measurementId = "G-YXC3XB3PFL"
            };

            FirebaseConfigManager.SetEnvironmentConfig("production", productionConfig);
            FirebaseConfigManager.SetEnvironmentConfig("development", developmentConfig);

            Debug.Log("Firebase configuration loaded from config file");
        }

        private void ShowConfigurationRequired()
        {
            // Instead of blocking, allow manual login which triggers configuration
            Debug.Log("Firebase configuration will be established during login process");

            // Show a helpful message instead of blocking
            EditorUtility.DisplayDialog(
                "Unreality3D Setup",
                "Firebase configuration will be established when you login.\n\n" +
                "This is normal for new projects. Please proceed to login.",
                "Continue"
            );
        }

        public void DrawTab()
        {
            EditorGUILayout.Space(20);

            switch (currentState)
            {
                case AuthState.MethodSelection:
                    DrawMethodSelection();
                    break;
                case AuthState.PayPalLogin:
                    DrawPayPalLogin();
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

            // Show startup preference at bottom
            EditorGUILayout.Space(20);
            EditorGUILayout.BeginHorizontal();
            var newShowOnStartup = EditorGUILayout.Toggle("Show on startup", showOnStartup);
            if (newShowOnStartup != showOnStartup)
            {
                showOnStartup = newShowOnStartup;
                EditorPrefs.SetBool("U3D_ShowOnStartup", showOnStartup);
            }
            EditorGUILayout.EndHorizontal();
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

            if (GUILayout.Button("Continue", GUILayout.Height(40)))
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
            EditorGUILayout.LabelField("PayPal Login", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Login with PayPal to access monetization features and professional URLs.", MessageType.Info);

            EditorGUILayout.Space(10);

            if (GUILayout.Button("🔗 Login with PayPal", GUILayout.Height(40)))
            {
                StartPayPalLogin();
            }

            EditorGUILayout.Space(10);

            if (GUILayout.Button("← Back to Method Selection"))
            {
                currentState = AuthState.MethodSelection;
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

            if (GUILayout.Button("← Back to Login"))
            {
                currentState = AuthState.ManualLogin;
            }
        }

        private void DrawLoggedIn()
        {
            EditorGUILayout.LabelField("✅ Setup Complete!", EditorStyles.boldLabel);
            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField($"Logged in as: {Unreality3DAuthenticator.UserEmail ?? "Unknown"}", EditorStyles.label);
            EditorGUILayout.LabelField($"Creator Username: {Unreality3DAuthenticator.CreatorUsername}", EditorStyles.label);

            EditorGUILayout.Space(10);

            EditorGUILayout.HelpBox("You're ready to start creating content with the Unreality3D SDK!", MessageType.Info);

            EditorGUILayout.Space(10);

            if (GUILayout.Button("Logout"))
            {
                Unreality3DAuthenticator.Logout();
                currentState = AuthState.MethodSelection;
                UpdateCompletion();
            }
        }

        private void DrawUsernameReservation()
        {
            EditorGUILayout.LabelField("Reserve Your Creator Username", EditorStyles.boldLabel);
            EditorGUILayout.Space(10);

            EditorGUILayout.HelpBox(
                "Choose your creator username for professional URLs like: https://yourname.unreality3d.com/",
                MessageType.Info);

            EditorGUILayout.Space(10);

            desiredUsername = EditorGUILayout.TextField("Desired Username", desiredUsername);

            EditorGUILayout.Space(5);

            bool canCheck = !string.IsNullOrWhiteSpace(desiredUsername) && !checkingAvailability;

            EditorGUI.BeginDisabledGroup(!canCheck);
            if (GUILayout.Button("Check Availability"))
            {
                CheckUsernameAvailability();
            }
            EditorGUI.EndDisabledGroup();

            if (checkingAvailability)
            {
                EditorGUILayout.LabelField("Checking availability...", EditorStyles.miniLabel);
            }

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

        // Authentication Methods - Using your existing methods
        private void StartPayPalLogin()
        {
            Debug.Log("Starting PayPal login...");
            EditorUtility.DisplayDialog("PayPal Login",
                "PayPal integration will be available in a future update. For now, you can use email/password authentication.",
                "OK");
        }

        private async void StartManualLogin()
        {
            try
            {
                bool success = await Unreality3DAuthenticator.LoginWithEmailPassword(email, password);
                if (success)
                {
                    if (string.IsNullOrEmpty(Unreality3DAuthenticator.CreatorUsername))
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
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog("Login Error", $"An error occurred: {ex.Message}", "OK");
            }
        }

        private async void StartManualRegister()
        {
            try
            {
                bool success = await Unreality3DAuthenticator.RegisterWithEmailPassword(email, password);
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
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog("Registration Error", $"An error occurred: {ex.Message}", "OK");
            }
        }

        private async void CheckUsernameAvailability()
        {
            checkingAvailability = true;
            usernameChecked = false;

            try
            {
                usernameAvailable = await Unreality3DAuthenticator.CheckUsernameAvailability(desiredUsername);

                if (!usernameAvailable)
                {
                    usernameSuggestions = await Unreality3DAuthenticator.GetUsernameSuggestions(desiredUsername);
                }

                usernameChecked = true;
            }
            catch (System.Exception ex)
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
                bool success = await Unreality3DAuthenticator.ReserveUsername(desiredUsername);
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
            catch (System.Exception ex)
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
                        Unreality3DAuthenticator.IsLoggedIn &&
                        !string.IsNullOrEmpty(Unreality3DAuthenticator.CreatorUsername);
        }

        public void OnEnable()
        {
            Initialize();
        }

        public void OnDisable()
        {
            // Cleanup if needed
        }
    }

    // Configuration data structure for JSON config file
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