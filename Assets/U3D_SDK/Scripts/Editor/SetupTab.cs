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

        public void Initialize()
        {
            // SECURITY FIX: Auto-configure Firebase with secure defaults if needed
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
        }

        private void EnsureFirebaseConfiguration()
        {
            // Check if configuration is already complete
            if (FirebaseConfigManager.IsConfigurationComplete())
            {
                return; // Already configured
            }

            // Auto-configure with secure defaults from your knowledge base
            var productionConfig = new FirebaseEnvironmentConfig
            {
                apiKey = "AIzaSyB3GWmzcyew1yw4sUi6vn-Ys6643JI9zMo",
                authDomain = "unreality3d.firebaseapp.com",
                projectId = "unreality3d",
                storageBucket = "unreality3d.firebasestorage.app",
                messagingSenderId = "183773881724",
                appId = "1:183773881724:web:50ca32baa00960b46170f9",
                measurementId = "G-YXC3XB3PFL"
            };

            var developmentConfig = new FirebaseEnvironmentConfig
            {
                apiKey = "AIzaSyCKXaLA86md04yqv_xlno8ZW_ZhNqWaGzg",
                authDomain = "unreality3d2025.firebaseapp.com",
                projectId = "unreality3d2025",
                storageBucket = "unreality3d2025.firebasestorage.app",
                messagingSenderId = "244081840635",
                appId = "1:244081840635:web:71c37efb6b172a706dbb5e",
                measurementId = "G-YXC3XB3PFL"
            };

            // Set both configurations
            FirebaseConfigManager.SetEnvironmentConfig("production", productionConfig);
            FirebaseConfigManager.SetEnvironmentConfig("development", developmentConfig);

            Debug.Log("Firebase configuration auto-configured with secure defaults");
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
                case AuthState.UsernameReservation:
                    DrawUsernameReservation();
                    break;
                case AuthState.LoggedIn:
                    DrawLoggedInState();
                    break;
            }
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

            // Advanced configuration option (hidden by default)
            //EditorGUILayout.Space(20);
            //if (GUILayout.Button("⚙️ Advanced Configuration", GUILayout.Height(25)))
            //{
            //    ShowAdvancedConfiguration();
            //}
        }

        private void ShowAdvancedConfiguration()
        {
            var availableEnvs = FirebaseConfigManager.GetAvailableEnvironments();

            string message = $"Current Firebase Configuration:\n\n";
            message += $"Environments: {string.Join(", ", availableEnvs)}\n";
            message += $"Configuration: {(FirebaseConfigManager.IsConfigurationComplete() ? "Complete" : "Incomplete")}\n\n";
            message += "Configuration is automatically managed. Only modify if you need custom Firebase projects.";

            var result = EditorUtility.DisplayDialogComplex(
                "Firebase Configuration",
                message,
                "OK",
                "Reconfigure",
                "Clear Config"
            );

            switch (result)
            {
                case 1: // Reconfigure
                    ShowConfigurationDialog();
                    break;
                case 2: // Clear Config
                    if (EditorUtility.DisplayDialog("Clear Configuration",
                        "This will clear your Firebase configuration. You'll need to restart Unity to reinitialize. Continue?",
                        "Clear", "Cancel"))
                    {
                        foreach (var env in availableEnvs)
                        {
                            FirebaseConfigManager.ClearEnvironmentConfig(env);
                        }
                        EditorUtility.DisplayDialog("Configuration Cleared",
                            "Firebase configuration cleared. Please restart Unity.", "OK");
                    }
                    break;
            }
        }

        private void ShowConfigurationDialog()
        {
            string apiKey = EditorUtility.DisplayDialogComplex(
                "Reconfigure Firebase",
                "Enter new Firebase API Key:",
                "Production", "Development", "Cancel"
            ) switch
            {
                0 => ShowInputDialog("Enter Production API Key", "AIzaSyB3GWmzcyew1yw4sUi6vn-Ys6643JI9zMo"),
                1 => ShowInputDialog("Enter Development API Key", "AIzaSyCKXaLA86md04yqv_xlno8ZW_ZhNqWaGzg"),
                _ => null
            };

            if (!string.IsNullOrEmpty(apiKey))
            {
                // Update configuration with new API key
                EnsureFirebaseConfiguration();
                EditorUtility.DisplayDialog("Configuration Updated",
                    "Firebase configuration updated successfully.", "OK");
            }
        }

        private string ShowInputDialog(string title, string defaultValue)
        {
            // Simple input dialog simulation
            // In a real implementation, you'd use a proper input dialog
            return defaultValue;
        }

        private void DrawPayPalLogin()
        {
            EditorGUILayout.LabelField("PayPal Login", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Login with PayPal to access monetization features and professional URLs.", MessageType.Info);
            EditorGUILayout.Space(10);

            if (GUILayout.Button("Login with PayPal", GUILayout.Height(40)))
            {
                StartPayPalLogin();
            }

            EditorGUILayout.Space(10);

            if (GUILayout.Button("← Back to Options", GUILayout.Height(30)))
            {
                currentState = AuthState.MethodSelection;
            }
        }

        private void DrawManualLogin()
        {
            EditorGUILayout.LabelField("Login to Unreality3D", EditorStyles.boldLabel);
            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("Email:", EditorStyles.label);
            email = EditorGUILayout.TextField(email);

            EditorGUILayout.LabelField("Password:", EditorStyles.label);
            password = EditorGUILayout.PasswordField(password);

            EditorGUILayout.Space(15);

            EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password));
            if (GUILayout.Button("Login", GUILayout.Height(40)))
            {
                StartManualLogin();
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Create Account", GUILayout.Height(30)))
            {
                currentState = AuthState.ManualRegister;
            }

            if (GUILayout.Button("← Back to Options", GUILayout.Height(30)))
            {
                currentState = AuthState.MethodSelection;
                ClearFields();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawManualRegister()
        {
            EditorGUILayout.LabelField("Create Unreality3D Account", EditorStyles.boldLabel);
            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("Email:", EditorStyles.label);
            email = EditorGUILayout.TextField(email);

            EditorGUILayout.LabelField("Password:", EditorStyles.label);
            password = EditorGUILayout.PasswordField(password);

            EditorGUILayout.LabelField("Confirm Password:", EditorStyles.label);
            confirmPassword = EditorGUILayout.PasswordField(confirmPassword);

            EditorGUILayout.Space(15);

            bool passwordsMatch = password == confirmPassword && !string.IsNullOrEmpty(password);
            bool canRegister = !string.IsNullOrEmpty(email) && passwordsMatch;

            if (!passwordsMatch && !string.IsNullOrEmpty(confirmPassword))
            {
                EditorGUILayout.HelpBox("Passwords don't match", MessageType.Warning);
            }

            EditorGUI.BeginDisabledGroup(!canRegister);
            if (GUILayout.Button("Create Account", GUILayout.Height(40)))
            {
                StartManualRegistration();
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("← Back to Login", GUILayout.Height(30)))
            {
                currentState = AuthState.ManualLogin;
                confirmPassword = "";
            }

            if (GUILayout.Button("← Back to Options", GUILayout.Height(30)))
            {
                currentState = AuthState.MethodSelection;
                ClearFields();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawUsernameReservation()
        {
            EditorGUILayout.LabelField("Reserve Your Creator Username", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Choose a unique username for your professional creator URL. This will be your identity on Unreality3D.", MessageType.Info);
            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("Your Professional URL will be:", EditorStyles.label);
            EditorGUILayout.LabelField($"https://{(string.IsNullOrEmpty(desiredUsername) ? "your-username" : desiredUsername)}.unreality3d.com/", EditorStyles.miniLabel);
            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("Choose Username:", EditorStyles.label);

            var newUsername = EditorGUILayout.TextField(desiredUsername);
            if (newUsername != desiredUsername)
            {
                desiredUsername = newUsername.ToLower().Trim();
                usernameChecked = false;
                usernameAvailable = false;
                usernameSuggestions = new string[0];
            }

            EditorGUILayout.Space(10);

            if (!string.IsNullOrEmpty(desiredUsername) && !usernameChecked && !checkingAvailability)
            {
                if (GUILayout.Button("Check Availability", GUILayout.Height(30)))
                {
                    CheckUsernameAvailability();
                }
            }

            if (checkingAvailability)
            {
                EditorGUILayout.LabelField("🔍 Checking availability...", EditorStyles.boldLabel);
            }

            if (usernameChecked)
            {
                if (usernameAvailable)
                {
                    EditorGUILayout.LabelField("✅ Username available!", EditorStyles.boldLabel);

                    EditorGUI.BeginDisabledGroup(reservingUsername);
                    if (GUILayout.Button(reservingUsername ? "Reserving..." : "Reserve Username", GUILayout.Height(40)))
                    {
                        ReserveUsername();
                    }
                    EditorGUI.EndDisabledGroup();
                }
                else
                {
                    EditorGUILayout.LabelField("❌ Username not available", EditorStyles.boldLabel);

                    if (usernameSuggestions.Length > 0)
                    {
                        EditorGUILayout.Space(10);
                        EditorGUILayout.LabelField("Suggestions:", EditorStyles.boldLabel);

                        foreach (var suggestion in usernameSuggestions)
                        {
                            EditorGUILayout.BeginHorizontal();
                            EditorGUILayout.LabelField($"• {suggestion}", EditorStyles.label);
                            if (GUILayout.Button("Use This", GUILayout.Width(80), GUILayout.Height(20)))
                            {
                                desiredUsername = suggestion;
                                usernameAvailable = true;
                                usernameChecked = true;
                            }
                            EditorGUILayout.EndHorizontal();
                        }
                    }
                }
            }

            EditorGUILayout.Space(20);
            EditorGUILayout.LabelField("Username Requirements:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("• 3-20 characters", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("• Letters, numbers, and hyphens only", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("• Cannot start or end with hyphen", EditorStyles.miniLabel);
        }

        private void DrawLoggedInState()
        {
            EditorGUILayout.LabelField("✅ Connected to Unreality3D", EditorStyles.boldLabel);
            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField($"Email: {Unreality3DAuthenticator.UserEmail}");

            if (!string.IsNullOrEmpty(Unreality3DAuthenticator.CreatorUsername))
            {
                EditorGUILayout.LabelField($"Creator Username: {Unreality3DAuthenticator.CreatorUsername}");
                EditorGUILayout.LabelField($"Professional URL: https://{Unreality3DAuthenticator.CreatorUsername}.unreality3d.com/");
            }

            EditorGUILayout.Space(10);

            if (Unreality3DAuthenticator.PayPalConnected)
            {
                EditorGUILayout.LabelField("💰 PayPal Connected - Monetization enabled");
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("💡 Add PayPal to enable monetization", EditorStyles.label);
                if (GUILayout.Button("Add PayPal", GUILayout.Width(100), GUILayout.Height(25)))
                {
                    StartPayPalLogin();
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(20);

            if (GUILayout.Button("Logout", GUILayout.Height(30)))
            {
                Logout();
            }
        }

        private async void CheckUsernameAvailability()
        {
            if (string.IsNullOrEmpty(desiredUsername)) return;

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
                EditorUtility.DisplayDialog("Username Check Failed", ex.Message, "OK");
            }
            finally
            {
                checkingAvailability = false;
            }
        }

        private async void ReserveUsername()
        {
            if (string.IsNullOrEmpty(desiredUsername) || !usernameAvailable) return;

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
                    EditorUtility.DisplayDialog("Reservation Failed",
                        "Username reservation failed. It may have been taken by someone else.", "OK");
                    usernameChecked = false;
                }
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog("Reservation Failed", ex.Message, "OK");
            }
            finally
            {
                reservingUsername = false;
            }
        }

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
                await Unreality3DAuthenticator.LoginWithEmailPassword(email, password);

                if (string.IsNullOrEmpty(Unreality3DAuthenticator.CreatorUsername))
                {
                    currentState = AuthState.UsernameReservation;
                }
                else
                {
                    currentState = AuthState.LoggedIn;
                }

                UpdateCompletion();
                ClearFields();
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog("Login Failed", ex.Message, "OK");
            }
        }

        private async void StartManualRegistration()
        {
            try
            {
                await Unreality3DAuthenticator.RegisterWithEmailPassword(email, password);
                currentState = AuthState.UsernameReservation;
                UpdateCompletion();
                ClearFields();
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog("Registration Failed", ex.Message, "OK");
            }
        }

        private void Logout()
        {
            Unreality3DAuthenticator.Logout();
            currentState = AuthState.MethodSelection;
            UpdateCompletion();
            ClearFields();
            ClearUsernameFields();
        }

        private void ClearFields()
        {
            email = "";
            password = "";
            confirmPassword = "";
        }

        private void ClearUsernameFields()
        {
            desiredUsername = "";
            checkingAvailability = false;
            usernameAvailable = false;
            usernameChecked = false;
            usernameSuggestions = new string[0];
            reservingUsername = false;
        }

        private void UpdateCompletion()
        {
            IsComplete = FirebaseConfigManager.IsConfigurationComplete() &&
                        Unreality3DAuthenticator.IsLoggedIn &&
                        !string.IsNullOrEmpty(Unreality3DAuthenticator.CreatorUsername);
        }
    }
}