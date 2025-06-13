using UnityEngine;
using UnityEditor;

namespace U3D.Editor
{
    public class SetupTab : ICreatorTab
    {
        public string TabName => "Setup";
        public bool IsComplete { get; private set; }

        private bool paypalConnected = false;
        private bool showOnStartup = true;
        private bool showLoginForm = false;
        private bool showUsernameReservation = false;
        private string tempEmail = "";
        private string tempPassword = "";
        private string tempUsername = "";
        private bool isAuthenticating = false;
        private bool isCheckingUsername = false;
        private bool isReservingUsername = false;
        private string authMessage = "";
        private string usernameMessage = "";

        public void Initialize()
        {
            paypalConnected = EditorPrefs.GetBool("U3D_PayPalConnected", false);
            showOnStartup = EditorPrefs.GetBool("U3D_ShowOnStartup", true);
            UpdateCompletion();
        }

        public void DrawTab()
        {
            EditorGUILayout.Space(20);

            EditorGUILayout.LabelField("Welcome to U3D", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Real communities, real creator support. The world's most creator-friendly 3D content platform.", EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space(15);

            // Unreality3D Authentication Section
            DrawAuthenticationSection();

            EditorGUILayout.Space(15);

            // PayPal Section (only show if authenticated)
            if (Unreality3DAuthenticator.IsAuthenticated)
            {
                DrawPayPalSection();
                EditorGUILayout.Space(15);
            }

            // Preferences Section
            DrawPreferencesSection();
        }

        private void DrawAuthenticationSection()
        {
            EditorGUILayout.LabelField("Your Unreality3D Account", EditorStyles.boldLabel);

            if (Unreality3DAuthenticator.IsAuthenticated)
            {
                // Authenticated State
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.LabelField($"✅ Logged in as: {Unreality3DAuthenticator.UserEmail}", EditorStyles.boldLabel);

                if (!string.IsNullOrEmpty(Unreality3DAuthenticator.CreatorUsername))
                {
                    EditorGUILayout.LabelField($"🌟 Creator Username: {Unreality3DAuthenticator.CreatorUsername}");
                    EditorGUILayout.LabelField($"🌐 Your URL: https://{Unreality3DAuthenticator.CreatorUsername}.unreality3d.com/", EditorStyles.miniLabel);
                }
                else
                {
                    EditorGUILayout.LabelField("⚠️ Username not set - required for publishing", EditorStyles.boldLabel);
                    if (GUILayout.Button("Reserve Your Username", GUILayout.Height(30)))
                    {
                        showUsernameReservation = true;
                    }
                }

                EditorGUILayout.Space(10);
                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button("Logout", GUILayout.Width(80)))
                {
                    Unreality3DAuthenticator.Logout();
                    ResetAuthenticationState();
                    UpdateCompletion();
                }

                if (string.IsNullOrEmpty(Unreality3DAuthenticator.CreatorUsername))
                {
                    if (GUILayout.Button("Set Username", GUILayout.Width(100)))
                    {
                        showUsernameReservation = true;
                    }
                }

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();

                // Username Reservation Section
                if (showUsernameReservation)
                {
                    DrawUsernameReservationSection();
                }
            }
            else
            {
                // Not Authenticated State
                EditorGUILayout.HelpBox("Log in to your Unreality3D account to publish experiences and access creator features.", MessageType.Info);

                if (!showLoginForm)
                {
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("Login to Unreality3D", GUILayout.Height(35)))
                    {
                        showLoginForm = true;
                    }
                    if (GUILayout.Button("Create Account", GUILayout.Height(35)))
                    {
                        Unreality3DAuthenticator.OpenSignupPage();
                    }
                    EditorGUILayout.EndHorizontal();
                }
                else
                {
                    DrawLoginForm();
                }
            }
        }

        private void DrawLoginForm()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Login to Unreality3D", EditorStyles.boldLabel);

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Email:");
            tempEmail = EditorGUILayout.TextField(tempEmail);

            EditorGUILayout.LabelField("Password:");
            tempPassword = EditorGUILayout.PasswordField(tempPassword);

            EditorGUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();

            EditorGUI.BeginDisabledGroup(isAuthenticating || string.IsNullOrEmpty(tempEmail) || string.IsNullOrEmpty(tempPassword));
            if (GUILayout.Button(isAuthenticating ? "Logging in..." : "Login", GUILayout.Height(30)))
            {
                AuthenticateUser();
            }
            EditorGUI.EndDisabledGroup();

            if (GUILayout.Button("Cancel", GUILayout.Height(30), GUILayout.Width(60)))
            {
                showLoginForm = false;
                tempEmail = "";
                tempPassword = "";
                authMessage = "";
            }

            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(authMessage))
            {
                EditorGUILayout.Space(5);
                var messageType = authMessage.StartsWith("✅") ? MessageType.Info : MessageType.Error;
                EditorGUILayout.HelpBox(authMessage, messageType);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawUsernameReservationSection()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Reserve Your Creator Username", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("This will be your professional URL: https://yourname.unreality3d.com/", EditorStyles.miniLabel);

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Username (3-20 characters, letters/numbers/hyphens only):");
            var newUsername = EditorGUILayout.TextField(tempUsername).ToLower();

            if (newUsername != tempUsername)
            {
                tempUsername = newUsername;
                usernameMessage = "";
                if (!string.IsNullOrEmpty(tempUsername))
                {
                    CheckUsernameAvailability();
                }
            }

            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();

            EditorGUI.BeginDisabledGroup(isReservingUsername || string.IsNullOrEmpty(tempUsername) || usernameMessage.Contains("taken") || usernameMessage.Contains("invalid"));
            if (GUILayout.Button(isReservingUsername ? "Reserving..." : "Reserve Username", GUILayout.Height(30)))
            {
                ReserveUsername();
            }
            EditorGUI.EndDisabledGroup();

            if (GUILayout.Button("Cancel", GUILayout.Height(30), GUILayout.Width(60)))
            {
                showUsernameReservation = false;
                tempUsername = "";
                usernameMessage = "";
            }

            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(usernameMessage))
            {
                EditorGUILayout.Space(5);
                var messageType = usernameMessage.Contains("available") || usernameMessage.Contains("reserved") ? MessageType.Info : MessageType.Error;
                EditorGUILayout.HelpBox(usernameMessage, messageType);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawPayPalSection()
        {
            EditorGUILayout.LabelField("Accept payments with PayPal", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Connect PayPal to sell your Unity experiences. You keep 95%!", MessageType.Info);

            if (paypalConnected)
            {
                EditorGUILayout.LabelField("✓ PayPal Connected", EditorStyles.boldLabel);
                if (GUILayout.Button("Disconnect PayPal"))
                {
                    paypalConnected = false;
                    EditorPrefs.SetBool("U3D_PayPalConnected", false);
                    UpdateCompletion();
                }
            }
            else
            {
                if (GUILayout.Button("Connect PayPal Account", GUILayout.Height(40)))
                {
                    ConnectPayPalAccount();
                }
            }
        }

        private void DrawPreferencesSection()
        {
            EditorGUILayout.LabelField("Preferences", EditorStyles.boldLabel);
            var newShowOnStartup = EditorGUILayout.Toggle("Show this window on startup", showOnStartup);
            if (newShowOnStartup != showOnStartup)
            {
                showOnStartup = newShowOnStartup;
                EditorPrefs.SetBool("U3D_ShowOnStartup", showOnStartup);
            }
        }

        private async void AuthenticateUser()
        {
            isAuthenticating = true;
            authMessage = "Logging in...";

            try
            {
                var result = await Unreality3DAuthenticator.AuthenticateWithEmailAsync(tempEmail, tempPassword);

                if (result.Success)
                {
                    showLoginForm = false;
                    tempEmail = "";
                    tempPassword = "";
                    authMessage = "";
                    UpdateCompletion();

                    // If user doesn't have a username, prompt for it
                    if (string.IsNullOrEmpty(Unreality3DAuthenticator.CreatorUsername))
                    {
                        showUsernameReservation = true;
                    }
                }
                else
                {
                    authMessage = result.ErrorMessage;
                }
            }
            catch (System.Exception ex)
            {
                authMessage = $"Login failed: {ex.Message}";
            }
            finally
            {
                isAuthenticating = false;
            }
        }

        private async void CheckUsernameAvailability()
        {
            if (isCheckingUsername) return;

            isCheckingUsername = true;
            usernameMessage = "Checking availability...";

            try
            {
                var result = await Unreality3DAuthenticator.CheckUsernameAvailabilityAsync(tempUsername);
                usernameMessage = result.Success ? result.Message : result.ErrorMessage;
            }
            catch (System.Exception ex)
            {
                usernameMessage = $"Check failed: {ex.Message}";
            }
            finally
            {
                isCheckingUsername = false;
            }
        }

        private async void ReserveUsername()
        {
            isReservingUsername = true;
            usernameMessage = "Reserving username...";

            try
            {
                var result = await Unreality3DAuthenticator.ReserveUsernameAsync(tempUsername);

                if (result.Success)
                {
                    usernameMessage = result.Message;
                    showUsernameReservation = false;
                    tempUsername = "";
                    UpdateCompletion();
                }
                else
                {
                    usernameMessage = result.ErrorMessage;
                }
            }
            catch (System.Exception ex)
            {
                usernameMessage = $"Reservation failed: {ex.Message}";
            }
            finally
            {
                isReservingUsername = false;
            }
        }

        private void ConnectPayPalAccount()
        {
            if (EditorUtility.DisplayDialog("PayPal Integration",
                "This will open PayPal in your browser to connect your account.", "Continue", "Cancel"))
            {
                Application.OpenURL("https://www.paypal.com/signin");
                paypalConnected = true;
                EditorPrefs.SetBool("U3D_PayPalConnected", true);
                UpdateCompletion();
            }
        }

        private void ResetAuthenticationState()
        {
            showLoginForm = false;
            showUsernameReservation = false;
            tempEmail = "";
            tempPassword = "";
            tempUsername = "";
            authMessage = "";
            usernameMessage = "";
        }

        private void UpdateCompletion()
        {
            IsComplete = Unreality3DAuthenticator.IsAuthenticated && !string.IsNullOrEmpty(Unreality3DAuthenticator.CreatorUsername);
        }
    }
}