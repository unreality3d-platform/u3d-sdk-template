using UnityEngine;
using UnityEditor;

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
            LoggedIn
        }

        private AuthState currentState = AuthState.MethodSelection;
        private bool paypalSelected = true;
        private string email = "";
        private string password = "";
        private string confirmPassword = "";

        public void Initialize()
        {
            // Check if already logged in
            if (Unreality3DAuthenticator.IsLoggedIn)
            {
                currentState = AuthState.LoggedIn;
                UpdateCompletion();
            }
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
            EditorGUILayout.LabelField("• Publish to professional URLs", EditorStyles.miniLabel);
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

            if (GUILayout.Button("Login with PayPal", GUILayout.Height(40)))
            {
                // Integrate with your existing PayPal authentication
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

        private void DrawLoggedInState()
        {
            EditorGUILayout.LabelField("✅ Connected to Unreality3D", EditorStyles.boldLabel);
            EditorGUILayout.Space(10);

            // Show current user info
            EditorGUILayout.LabelField($"Email: {Unreality3DAuthenticator.UserEmail}");

            if (!string.IsNullOrEmpty(Unreality3DAuthenticator.CreatorUsername))
            {
                EditorGUILayout.LabelField($"Creator Username: {Unreality3DAuthenticator.CreatorUsername}");
                EditorGUILayout.LabelField($"Professional URL: https://{Unreality3DAuthenticator.CreatorUsername}.unreality3d.com/");
            }
            else
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Complete your setup in the Publish tab to reserve your professional URL.", EditorStyles.wordWrappedLabel);
                if (GUILayout.Button("Go to Publish", GUILayout.Width(100), GUILayout.Height(30)))
                {
                    OnRequestTabSwitch?.Invoke(3); // Publish tab is index 3
                }
                EditorGUILayout.EndHorizontal();
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

        private void StartPayPalLogin()
        {
            // Integration with your existing Firebase PayPal auth
            Debug.Log("Starting PayPal login...");
            // This would trigger your existing PayPal OAuth flow
        }

        private async void StartManualLogin()
        {
            try
            {
                await Unreality3DAuthenticator.LoginWithEmailPassword(email, password);
                currentState = AuthState.LoggedIn;
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
                currentState = AuthState.LoggedIn;
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
        }

        private void ClearFields()
        {
            email = "";
            password = "";
            confirmPassword = "";
        }

        private void UpdateCompletion()
        {
            IsComplete = Unreality3DAuthenticator.IsLoggedIn;
        }
    }
}