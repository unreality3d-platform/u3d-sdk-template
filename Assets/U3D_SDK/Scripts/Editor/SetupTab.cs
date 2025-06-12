using UnityEngine;
using UnityEditor;

namespace U3D.Editor
{
    public class SetupTab : ICreatorTab
    {
        public string TabName => "Setup";
        public bool IsComplete { get; private set; }

        private bool paypalConnected = false;
        private string creatorName = "";
        private bool showOnStartup = true;

        public void Initialize()
        {
            creatorName = EditorPrefs.GetString("U3D_CreatorName", "");
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

            EditorGUILayout.LabelField("Your Creator Name", EditorStyles.boldLabel);
            var newName = EditorGUILayout.TextField("Name", creatorName);
            if (newName != creatorName)
            {
                creatorName = newName;
                EditorPrefs.SetString("U3D_CreatorName", creatorName);
                UpdateCompletion();
            }
            EditorGUILayout.Space(10);

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

            EditorGUILayout.Space(20);

            EditorGUILayout.LabelField("Preferences", EditorStyles.boldLabel);
            var newShowOnStartup = EditorGUILayout.Toggle("Show this window on startup", showOnStartup);
            if (newShowOnStartup != showOnStartup)
            {
                showOnStartup = newShowOnStartup;
                EditorPrefs.SetBool("U3D_ShowOnStartup", showOnStartup);
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

        private void UpdateCompletion()
        {
            IsComplete = !string.IsNullOrEmpty(creatorName);
        }
    }
}