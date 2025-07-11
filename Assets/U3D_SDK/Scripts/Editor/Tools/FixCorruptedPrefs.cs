using UnityEditor;
using UnityEngine;

public class FixCorruptedPrefs
{
    [MenuItem("U3D/Fix Corrupted EditorPrefs")]
    public static void ClearCorruptedU3DPrefs()
    {
        if (EditorUtility.DisplayDialog("Fix Corrupted EditorPrefs",
            "This will clear all U3D-related EditorPrefs that may be causing build issues. Continue?",
            "Yes, Fix It", "Cancel"))
        {
            // Clear all our potentially corrupted keys
            var keysToCheck = new[]
            {
                // ProjectStartupConfiguration keys
                $"HasSetWebGLTarget_{Application.dataPath.GetHashCode()}",
                $"HasLoadedStartupScene_{Application.dataPath.GetHashCode()}",
                "HasSetWebGLTarget",
                
                // Old project-specific preferences
                $"U3D_{PlayerSettings.companyName}.{PlayerSettings.productName}_idToken",
                $"U3D_{PlayerSettings.companyName}.{PlayerSettings.productName}_refreshToken",
                $"U3D_{PlayerSettings.companyName}.{PlayerSettings.productName}_userEmail",
                $"U3D_{PlayerSettings.companyName}.{PlayerSettings.productName}_displayName",
                $"U3D_{PlayerSettings.companyName}.{PlayerSettings.productName}_creatorUsername",
                $"U3D_{PlayerSettings.companyName}.{PlayerSettings.productName}_paypalConnected",
                $"U3D_{PlayerSettings.companyName}.{PlayerSettings.productName}_stayLoggedIn",
                
                // Global preferences
                "U3D_PublishedURL",
                "U3D_LastProjectName",
                "U3D_CurrentRepository",
                
                // Any other U3D prefixed keys
            };

            int clearedCount = 0;

            foreach (var key in keysToCheck)
            {
                if (EditorPrefs.HasKey(key))
                {
                    EditorPrefs.DeleteKey(key);
                    Debug.Log($"🗑️ Cleared corrupted key: {key}");
                    clearedCount++;
                }
            }

            // Also clear any U3D_Creator_ prefixed keys (our new system)
            // This is harder to enumerate, so we'll clear common patterns
            var userEmail = ""; // We can't safely get this during potential corruption
            var commonPatterns = new[]
            {
                "U3D_Global_AuthToken",
                "U3D_Global_RefreshToken",
                "U3D_Global_UserEmail",
                "U3D_Global_DisplayName",
                "U3D_Global_CreatorUsername",
                "U3D_Global_PayPalConnected",
                "U3D_Global_PayPalEmail",
                "U3D_Global_LastPublishURL",
                "U3D_Global_LastProjectName",
                "U3D_Global_StayLoggedIn",
                "U3D_Global_DefaultBuildTarget"
            };

            foreach (var pattern in commonPatterns)
            {
                if (EditorPrefs.HasKey(pattern))
                {
                    EditorPrefs.DeleteKey(pattern);
                    Debug.Log($"🗑️ Cleared corrupted pattern: {pattern}");
                    clearedCount++;
                }
            }

            Debug.Log($"✅ Cleared {clearedCount} corrupted EditorPrefs keys");

            EditorUtility.DisplayDialog("Fixed!",
                $"Cleared {clearedCount} potentially corrupted EditorPrefs keys.\n\n" +
                "Now try your build again. If it works, the corruption is fixed!",
                "OK");
        }
    }
}