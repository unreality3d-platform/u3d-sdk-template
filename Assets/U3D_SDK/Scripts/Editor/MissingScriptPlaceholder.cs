using UnityEngine;

namespace U3D.Editor
{
    /// <summary>
    /// Placeholder component to mark where missing scripts were removed.
    /// Part of the U3D SDK Asset Cleanup system.
    /// </summary>
    public class MissingScriptPlaceholder : MonoBehaviour
    {
        [SerializeField]
        [TextArea(3, 5)]
        private string missingScriptNote = "⚠️ This component replaced a missing script.\n\nUse 'Remove Missing Script Placeholders' tool to clean up when ready.";

        [SerializeField]
        private string replacementDateTime = "";

        [SerializeField]
        [TextArea(2, 3)]
        private string u3dSuggestion = "";

        private void Awake()
        {
            if (string.IsNullOrEmpty(replacementDateTime))
            {
                replacementDateTime = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            }

            if (string.IsNullOrEmpty(u3dSuggestion))
            {
                string suggestion = ComponentSuggestions.GetSuggestionForGameObject(gameObject.name);
                if (!string.IsNullOrEmpty(suggestion))
                {
                    u3dSuggestion = $"💡 Try: {suggestion}";
                }
            }
        }
    }
}