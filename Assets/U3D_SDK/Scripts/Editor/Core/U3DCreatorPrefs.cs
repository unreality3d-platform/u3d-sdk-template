using UnityEditor;
using UnityEngine;

namespace U3D.Editor
{
    /// <summary>
    /// Simple creator preferences system using static keys only.
    /// NO email-based key generation, NO cross-class dependencies.
    /// Build-safe by design.
    /// </summary>
    public static class U3DCreatorPrefs
    {
        // Simple static key prefix - no dynamic generation
        private const string KEY_PREFIX = "U3D_Creator_";

        /// <summary>
        /// CRITICAL: Check if we should skip operations during builds
        /// </summary>
        private static bool ShouldSkipDuringBuild()
        {
            return BuildPipeline.isBuildingPlayer ||
                   EditorApplication.isCompiling ||
                   EditorApplication.isUpdating;
        }

        /// <summary>
        /// Generate a simple static preference key.
        /// Format: "U3D_Creator_{settingName}" - NO email parsing
        /// </summary>
        private static string GetKey(string settingName)
        {
            return KEY_PREFIX + settingName;
        }

        #region String Preferences

        public static void SetString(string settingName, string value)
        {
            if (ShouldSkipDuringBuild())
            {
                Debug.LogWarning($"🚫 U3DCreatorPrefs: Skipping SetString({settingName}) during build");
                return;
            }

            string key = GetKey(settingName);
            EditorPrefs.SetString(key, value);
            Debug.Log($"🔑 Saved creator pref: {settingName} = {value}");
        }

        public static string GetString(string settingName, string defaultValue = "")
        {
            if (ShouldSkipDuringBuild())
            {
                Debug.LogWarning($"🚫 U3DCreatorPrefs: Skipping GetString({settingName}) during build, returning default");
                return defaultValue;
            }

            string key = GetKey(settingName);
            string value = EditorPrefs.GetString(key, defaultValue);
            Debug.Log($"🔑 Loaded creator pref: {settingName} = {value}");
            return value;
        }

        #endregion

        #region Bool Preferences

        public static void SetBool(string settingName, bool value)
        {
            if (ShouldSkipDuringBuild())
            {
                Debug.LogWarning($"🚫 U3DCreatorPrefs: Skipping SetBool({settingName}) during build");
                return;
            }

            string key = GetKey(settingName);
            EditorPrefs.SetBool(key, value);
            Debug.Log($"🔑 Saved creator pref: {settingName} = {value}");
        }

        public static bool GetBool(string settingName, bool defaultValue = false)
        {
            if (ShouldSkipDuringBuild())
            {
                Debug.LogWarning($"🚫 U3DCreatorPrefs: Skipping GetBool({settingName}) during build, returning default");
                return defaultValue;
            }

            string key = GetKey(settingName);
            bool value = EditorPrefs.GetBool(key, defaultValue);
            Debug.Log($"🔑 Loaded creator pref: {settingName} = {value}");
            return value;
        }

        #endregion

        #region Int Preferences

        public static void SetInt(string settingName, int value)
        {
            if (ShouldSkipDuringBuild())
            {
                Debug.LogWarning($"🚫 U3DCreatorPrefs: Skipping SetInt({settingName}) during build");
                return;
            }

            string key = GetKey(settingName);
            EditorPrefs.SetInt(key, value);
            Debug.Log($"🔑 Saved creator pref: {settingName} = {value}");
        }

        public static int GetInt(string settingName, int defaultValue = 0)
        {
            if (ShouldSkipDuringBuild())
            {
                Debug.LogWarning($"🚫 U3DCreatorPrefs: Skipping GetInt({settingName}) during build, returning default");
                return defaultValue;
            }

            string key = GetKey(settingName);
            int value = EditorPrefs.GetInt(key, defaultValue);
            Debug.Log($"🔑 Loaded creator pref: {settingName} = {value}");
            return value;
        }

        #endregion

        #region Float Preferences

        public static void SetFloat(string settingName, float value)
        {
            if (ShouldSkipDuringBuild())
            {
                Debug.LogWarning($"🚫 U3DCreatorPrefs: Skipping SetFloat({settingName}) during build");
                return;
            }

            string key = GetKey(settingName);
            EditorPrefs.SetFloat(key, value);
            Debug.Log($"🔑 Saved creator pref: {settingName} = {value}");
        }

        public static float GetFloat(string settingName, float defaultValue = 0f)
        {
            if (ShouldSkipDuringBuild())
            {
                Debug.LogWarning($"🚫 U3DCreatorPrefs: Skipping GetFloat({settingName}) during build, returning default");
                return defaultValue;
            }

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
        /// Migrate old email-based preferences to new simple format.
        /// Run this ONCE to move existing data to simple keys.
        /// </summary>
        public static void MigrateFromEmailBasedKeys()
        {
            if (ShouldSkipDuringBuild())
            {
                Debug.LogWarning("🚫 U3DCreatorPrefs: Skipping migration during build");
                return;
            }

            bool migrated = false;

            // Check for old email-based keys pattern: U3D_Creator_{email}_*
            // This is a simplified migration - if you have specific email patterns you know about,
            // add them here. For now, we'll migrate common known patterns.

            // Migrate common old patterns we can identify
            var commonEmails = new[]
            {
                "test_AT_example_DOT_com",
                "user_AT_gmail_DOT_com",
                "creator_AT_unreality3d_DOT_com"
            };

            var settingsToMigrate = new[]
            {
                "PayPalEmail", "LastPublishURL", "LastProjectName",
                "StayLoggedIn", "DefaultBuildTarget"
            };

            foreach (var emailPattern in commonEmails)
            {
                foreach (var setting in settingsToMigrate)
                {
                    string oldKey = $"U3D_Creator_{emailPattern}_{setting}";

                    if (EditorPrefs.HasKey(oldKey))
                    {
                        // Migrate based on type
                        if (setting == "StayLoggedIn")
                        {
                            bool value = EditorPrefs.GetBool(oldKey);
                            SetBool(setting, value);
                        }
                        else if (setting == "DefaultBuildTarget")
                        {
                            int value = EditorPrefs.GetInt(oldKey);
                            SetInt(setting, value);
                        }
                        else
                        {
                            string value = EditorPrefs.GetString(oldKey);
                            SetString(setting, value);
                        }

                        // Clean up old key
                        EditorPrefs.DeleteKey(oldKey);
                        migrated = true;

                        Debug.Log($"🔄 Migrated {oldKey} → {setting}");
                    }
                }
            }

            // Also migrate old project-specific keys
            string oldProjectPrefix = $"U3D_{PlayerSettings.companyName}.{PlayerSettings.productName}_";
            var projectMigrations = new[]
            {
                ("idToken", "AuthToken"),
                ("refreshToken", "RefreshToken"),
                ("userEmail", "UserEmail"),
                ("displayName", "DisplayName"),
                ("creatorUsername", "CreatorUsername"),
                ("paypalConnected", "PayPalConnected"),
                ("stayLoggedIn", "StayLoggedIn")
            };

            foreach (var (oldSuffix, newName) in projectMigrations)
            {
                string oldKey = oldProjectPrefix + oldSuffix;

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

            // Migrate global keys
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
                Debug.Log("✅ Creator preferences migration completed - now using simple static keys");
            }
            else
            {
                Debug.Log("ℹ️ No old email-based preferences found to migrate");
            }
        }

        /// <summary>
        /// Clear all preferences for the current user (logout cleanup)
        /// </summary>
        public static void ClearUserPreferences()
        {
            if (ShouldSkipDuringBuild())
            {
                Debug.LogWarning("🚫 U3DCreatorPrefs: Skipping ClearUserPreferences during build");
                return;
            }

            // List of all settings to clear
            var settingsToClear = new[]
            {
                "AuthToken", "RefreshToken", "UserEmail", "DisplayName",
                "CreatorUsername", "PayPalConnected", "PayPalEmail",
                "LastPublishURL", "LastProjectName"
                // Note: Keep StayLoggedIn and DefaultBuildTarget
            };

            foreach (string setting in settingsToClear)
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
            if (ShouldSkipDuringBuild())
            {
                Debug.LogWarning($"🚫 U3DCreatorPrefs: Skipping HasKey({settingName}) during build, returning false");
                return false;
            }

            string key = GetKey(settingName);
            return EditorPrefs.HasKey(key);
        }

        /// <summary>
        /// Delete a specific setting for the current user
        /// </summary>
        public static void DeleteKey(string settingName)
        {
            if (ShouldSkipDuringBuild())
            {
                Debug.LogWarning($"🚫 U3DCreatorPrefs: Skipping DeleteKey({settingName}) during build");
                return;
            }

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
            if (ShouldSkipDuringBuild())
            {
                Debug.LogWarning("🚫 U3DCreatorPrefs: Skipping EnsureWebGLBuildTarget during build");
                return;
            }

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
            if (ShouldSkipDuringBuild())
            {
                Debug.LogWarning("🚫 U3DCreatorPrefs: Skipping LogAllPreferences during build");
                return;
            }

            Debug.Log($"🔍 Creator Preferences (Simple Static Keys):");
            Debug.Log($"  PayPal Email: {PayPalEmail}");
            Debug.Log($"  Last Publish URL: {LastPublishURL}");
            Debug.Log($"  Last Project Name: {LastProjectName}");
            Debug.Log($"  Stay Logged In: {StayLoggedIn}");
            Debug.Log($"  Default Build Target: {(BuildTarget)DefaultBuildTarget}");
        }

        #endregion
    }
}