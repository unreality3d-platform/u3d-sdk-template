using UnityEditor;
using UnityEngine;

namespace U3D.Editor
{
    /// <summary>
    /// Unified creator preferences system that survives project name changes.
    /// All creator state is stored per-user, not per-project.
    /// </summary>
    public static class U3DCreatorPrefs
    {
        // Fallback key for global settings (when no user email available)
        private const string GLOBAL_PREFIX = "U3D_Global_";

        /// <summary>
        /// Get the current user's email for preference keys.
        /// Returns empty string if not logged in.
        /// </summary>
        private static string GetCurrentUserEmail()
        {
            return U3DAuthenticator.UserEmail ?? "";
        }

        /// <summary>
        /// Generate a user-specific preference key.
        /// Format: "U3D_Creator_{email}_{settingName}" or "U3D_Global_{settingName}" if no user
        /// </summary>
        private static string GetKey(string settingName)
        {
            string userEmail = GetCurrentUserEmail();

            if (string.IsNullOrEmpty(userEmail))
            {
                return GLOBAL_PREFIX + settingName;
            }

            // Sanitize email for use as key (replace @ and . with safe chars)
            string sanitizedEmail = userEmail.Replace("@", "_AT_").Replace(".", "_DOT_");
            return $"U3D_Creator_{sanitizedEmail}_{settingName}";
        }

        #region String Preferences

        public static void SetString(string settingName, string value)
        {
            string key = GetKey(settingName);
            EditorPrefs.SetString(key, value);
            Debug.Log($"🔑 Saved creator pref: {settingName} = {value}");
        }

        public static string GetString(string settingName, string defaultValue = "")
        {
            string key = GetKey(settingName);
            string value = EditorPrefs.GetString(key, defaultValue);
            Debug.Log($"🔑 Loaded creator pref: {settingName} = {value}");
            return value;
        }

        #endregion

        #region Bool Preferences

        public static void SetBool(string settingName, bool value)
        {
            string key = GetKey(settingName);
            EditorPrefs.SetBool(key, value);
            Debug.Log($"🔑 Saved creator pref: {settingName} = {value}");
        }

        public static bool GetBool(string settingName, bool defaultValue = false)
        {
            string key = GetKey(settingName);
            bool value = EditorPrefs.GetBool(key, defaultValue);
            Debug.Log($"🔑 Loaded creator pref: {settingName} = {value}");
            return value;
        }

        #endregion

        #region Int Preferences

        public static void SetInt(string settingName, int value)
        {
            string key = GetKey(settingName);
            EditorPrefs.SetInt(key, value);
            Debug.Log($"🔑 Saved creator pref: {settingName} = {value}");
        }

        public static int GetInt(string settingName, int defaultValue = 0)
        {
            string key = GetKey(settingName);
            int value = EditorPrefs.GetInt(key, defaultValue);
            Debug.Log($"🔑 Loaded creator pref: {settingName} = {value}");
            return value;
        }

        #endregion

        #region Float Preferences

        public static void SetFloat(string settingName, float value)
        {
            string key = GetKey(settingName);
            EditorPrefs.SetFloat(key, value);
            Debug.Log($"🔑 Saved creator pref: {settingName} = {value}");
        }

        public static float GetFloat(string settingName, float defaultValue = 0f)
        {
            string key = GetKey(settingName);
            float value = EditorPrefs.GetFloat(key, defaultValue);
            Debug.Log($"🔑 Loaded creator pref: {settingName} = {value}");
            return value;
        }

        #endregion

        #region Convenience Methods for Common Settings

        /// <summary>
        /// PayPal email for the current creator
        /// </summary>
        public static string PayPalEmail
        {
            get => GetString("PayPalEmail");
            set => SetString("PayPalEmail", value);
        }

        /// <summary>
        /// Last published URL for this creator
        /// </summary>
        public static string LastPublishURL
        {
            get => GetString("LastPublishURL");
            set => SetString("LastPublishURL", value);
        }

        /// <summary>
        /// Last project name published by this creator
        /// </summary>
        public static string LastProjectName
        {
            get => GetString("LastProjectName");
            set => SetString("LastProjectName", value);
        }

        /// <summary>
        /// Default build target platform preference
        /// </summary>
        public static int DefaultBuildTarget
        {
            get => GetInt("DefaultBuildTarget", (int)BuildTarget.WebGL);
            set => SetInt("DefaultBuildTarget", value);
        }

        /// <summary>
        /// Whether to stay logged in preference
        /// </summary>
        public static bool StayLoggedIn
        {
            get => GetBool("StayLoggedIn", true);
            set => SetBool("StayLoggedIn", value);
        }

        #endregion

        #region Migration and Cleanup

        /// <summary>
        /// Migrate old project-specific preferences to new user-specific format.
        /// Call this during login to migrate existing user data.
        /// </summary>
        public static void MigrateOldPreferences()
        {
            if (string.IsNullOrEmpty(GetCurrentUserEmail()))
            {
                Debug.LogWarning("Cannot migrate preferences - no user logged in");
                return;
            }

            // Get old project-specific prefix
            string oldPrefix = $"U3D_{PlayerSettings.companyName}.{PlayerSettings.productName}_";

            // Migration mappings: old key suffix → new setting name
            var migrations = new[]
            {
                ("idToken", "AuthToken"),
                ("refreshToken", "RefreshToken"),
                ("userEmail", "UserEmail"),
                ("displayName", "DisplayName"),
                ("creatorUsername", "CreatorUsername"),
                ("paypalConnected", "PayPalConnected"),
                ("stayLoggedIn", "StayLoggedIn")
            };

            bool migrated = false;

            foreach (var (oldSuffix, newName) in migrations)
            {
                string oldKey = oldPrefix + oldSuffix;

                if (EditorPrefs.HasKey(oldKey))
                {
                    // Migrate based on type
                    if (oldSuffix == "paypalConnected" || oldSuffix == "stayLoggedIn")
                    {
                        bool value = EditorPrefs.GetBool(oldKey);
                        SetBool(newName, value);
                    }
                    else
                    {
                        string value = EditorPrefs.GetString(oldKey);
                        SetString(newName, value);
                    }

                    // Clean up old key
                    EditorPrefs.DeleteKey(oldKey);
                    migrated = true;

                    Debug.Log($"🔄 Migrated {oldKey} → {newName}");
                }
            }

            // Migrate PayPal email from SetupTab format
            string oldPayPalKey = $"U3D_PayPalEmail_{GetCurrentUserEmail()}";
            if (EditorPrefs.HasKey(oldPayPalKey))
            {
                string paypalEmail = EditorPrefs.GetString(oldPayPalKey);
                PayPalEmail = paypalEmail;
                EditorPrefs.DeleteKey(oldPayPalKey);
                migrated = true;
                Debug.Log($"🔄 Migrated PayPal email: {paypalEmail}");
            }

            // Migrate global publish preferences
            if (EditorPrefs.HasKey("U3D_PublishedURL"))
            {
                string url = EditorPrefs.GetString("U3D_PublishedURL");
                LastPublishURL = url;
                EditorPrefs.DeleteKey("U3D_PublishedURL");
                migrated = true;
                Debug.Log($"🔄 Migrated publish URL: {url}");
            }

            if (EditorPrefs.HasKey("U3D_LastProjectName"))
            {
                string projectName = EditorPrefs.GetString("U3D_LastProjectName");
                LastProjectName = projectName;
                EditorPrefs.DeleteKey("U3D_LastProjectName");
                migrated = true;
                Debug.Log($"🔄 Migrated project name: {projectName}");
            }

            if (migrated)
            {
                Debug.Log("✅ Creator preferences migration completed");
            }
        }

        /// <summary>
        /// Clear all preferences for the current user (logout cleanup)
        /// </summary>
        public static void ClearUserPreferences()
        {
            if (string.IsNullOrEmpty(GetCurrentUserEmail()))
            {
                Debug.LogWarning("Cannot clear preferences - no user logged in");
                return;
            }

            // List of all settings to clear
            var settingsTosClear = new[]
            {
                "AuthToken", "RefreshToken", "UserEmail", "DisplayName",
                "CreatorUsername", "PayPalConnected", "PayPalEmail",
                "LastPublishURL", "LastProjectName"
                // Note: Keep StayLoggedIn and DefaultBuildTarget
            };

            foreach (string setting in settingsTosClear)
            {
                string key = GetKey(setting);
                if (EditorPrefs.HasKey(key))
                {
                    EditorPrefs.DeleteKey(key);
                    Debug.Log($"🗑️ Cleared creator pref: {setting}");
                }
            }

            Debug.Log("🗑️ User preferences cleared");
        }

        /// <summary>
        /// Check if a setting exists for the current user
        /// </summary>
        public static bool HasKey(string settingName)
        {
            string key = GetKey(settingName);
            return EditorPrefs.HasKey(key);
        }

        /// <summary>
        /// Delete a specific setting for the current user
        /// </summary>
        public static void DeleteKey(string settingName)
        {
            string key = GetKey(settingName);
            if (EditorPrefs.HasKey(key))
            {
                EditorPrefs.DeleteKey(key);
                Debug.Log($"🗑️ Deleted creator pref: {settingName}");
            }
        }

        /// <summary>
        /// Set WebGL as default build target and persist the preference
        /// </summary>
        public static void EnsureWebGLBuildTarget()
        {
            var currentTarget = EditorUserBuildSettings.activeBuildTarget;

            if (currentTarget != BuildTarget.WebGL)
            {
                Debug.Log($"🎯 Switching build target from {currentTarget} to WebGL");
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);
            }

            // Store preference
            DefaultBuildTarget = (int)BuildTarget.WebGL;
        }
        #endregion

        #region Debug and Utilities

        /// <summary>
        /// Get the actual EditorPrefs key that would be used for a setting.
        /// Useful for debugging.
        /// </summary>
        public static string GetActualKey(string settingName)
        {
            return GetKey(settingName);
        }

        /// <summary>
        /// Log all current user's preferences (for debugging)
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void LogAllPreferences()
        {
            if (string.IsNullOrEmpty(GetCurrentUserEmail()))
            {
                Debug.Log("🔍 No user logged in - cannot show preferences");
                return;
            }

            Debug.Log($"🔍 Creator Preferences for: {GetCurrentUserEmail()}");
            Debug.Log($"  PayPal Email: {PayPalEmail}");
            Debug.Log($"  Last Publish URL: {LastPublishURL}");
            Debug.Log($"  Last Project Name: {LastProjectName}");
            Debug.Log($"  Stay Logged In: {StayLoggedIn}");
            Debug.Log($"  Default Build Target: {(BuildTarget)DefaultBuildTarget}");
        }

        #endregion
    }
}