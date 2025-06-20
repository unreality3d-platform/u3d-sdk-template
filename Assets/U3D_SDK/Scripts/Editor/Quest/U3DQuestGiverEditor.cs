using UnityEngine;
using UnityEditor;

namespace U3D.Editor
{
    /// <summary>
    /// Custom property drawer for U3DInteractionChoice to make it display nicely in Inspector
    /// PRESERVED: Your working property drawer
    /// </summary>
    [CustomPropertyDrawer(typeof(U3DInteractionChoice))]
    public class U3DInteractionChoiceDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            // Calculate rects for key and label fields
            float labelWidth = position.width * 0.6f;
            float keyWidth = position.width * 0.35f;
            float spacing = 5f;

            Rect labelRect = new Rect(position.x, position.y, labelWidth, position.height);
            Rect keyRect = new Rect(position.x + labelWidth + spacing, position.y, keyWidth - spacing, position.height);

            // Get properties - REMOVED choiceID to hide it as requested
            SerializedProperty choiceLabel = property.FindPropertyRelative("choiceLabel");
            SerializedProperty choiceKey = property.FindPropertyRelative("choiceKey");

            // Draw fields without labels (we'll show custom labels)
            EditorGUI.PropertyField(labelRect, choiceLabel, GUIContent.none);
            EditorGUI.PropertyField(keyRect, choiceKey, GUIContent.none);

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }
    }

    /// <summary>
    /// Custom Editor for U3DQuestGiver to enhance Inspector experience
    /// MODIFIED: Added radio button interface for mutually exclusive choices
    /// </summary>
    [CustomEditor(typeof(U3DQuestGiver))]
    public class U3DQuestGiverEditor : UnityEditor.Editor
    {
        // ADDED: New properties for mutually exclusive system
        private SerializedProperty questToGive;
        private SerializedProperty interactionMode;
        private SerializedProperty singleChoice;
        private SerializedProperty acceptChoice;
        private SerializedProperty declineChoice;
        private SerializedProperty multipleChoices;
        private SerializedProperty dialogCanvas;
        private SerializedProperty giverNameText;
        private SerializedProperty questDescriptionText;
        private SerializedProperty choicesParent;

        private void OnEnable()
        {
            questToGive = serializedObject.FindProperty("questToGive");
            // ADDED: New interaction mode properties
            interactionMode = serializedObject.FindProperty("interactionMode");
            singleChoice = serializedObject.FindProperty("singleChoice");
            acceptChoice = serializedObject.FindProperty("acceptChoice");
            declineChoice = serializedObject.FindProperty("declineChoice");
            multipleChoices = serializedObject.FindProperty("multipleChoices");
            dialogCanvas = serializedObject.FindProperty("dialogCanvas");
            giverNameText = serializedObject.FindProperty("giverNameText");
            questDescriptionText = serializedObject.FindProperty("questDescriptionText");
            choicesParent = serializedObject.FindProperty("choicesParent");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            U3DQuestGiver questGiver = (U3DQuestGiver)target;

            // Draw default properties first - MODIFIED: Exclude new properties
            DrawPropertiesExcluding(serializedObject,
                "interactionMode",
                "singleChoice",
                "acceptChoice",
                "declineChoice",
                "multipleChoices",
                "dialogCanvas",
                "giverNameText",
                "questDescriptionText",
                "choicesParent");

            // ADDED: Custom section for mutually exclusive interaction choices
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Interaction Choices", EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "Choose how players interact with this quest giver. Each mode provides different interaction options.",
                MessageType.Info);

            // ADDED: Interaction Mode Selection with radio button style
            EditorGUILayout.BeginVertical(GUI.skin.box);

            U3DInteractionMode currentMode = (U3DInteractionMode)interactionMode.enumValueIndex;

            // Single Mode
            EditorGUILayout.BeginHorizontal();
            bool singleSelected = EditorGUILayout.Toggle(currentMode == U3DInteractionMode.Single, GUILayout.Width(20));
            if (singleSelected && currentMode != U3DInteractionMode.Single)
            {
                interactionMode.enumValueIndex = (int)U3DInteractionMode.Single;
            }
            EditorGUILayout.LabelField("Single", EditorStyles.boldLabel, GUILayout.Width(60));
            EditorGUILayout.LabelField("Default choice only", GUILayout.ExpandWidth(true));
            EditorGUILayout.EndHorizontal();

            if (currentMode == U3DInteractionMode.Single)
            {
                EditorGUI.indentLevel++;
                SerializedProperty choiceLabel = singleChoice.FindPropertyRelative("choiceLabel");
                SerializedProperty choiceKey = singleChoice.FindPropertyRelative("choiceKey");
                EditorGUILayout.PropertyField(choiceKey, new GUIContent("Choice Key"));
                EditorGUILayout.PropertyField(choiceLabel, new GUIContent("Choice Label"));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(5);

            // Binary Mode  
            EditorGUILayout.BeginHorizontal();
            bool binarySelected = EditorGUILayout.Toggle(currentMode == U3DInteractionMode.Binary, GUILayout.Width(20));
            if (binarySelected && currentMode != U3DInteractionMode.Binary)
            {
                interactionMode.enumValueIndex = (int)U3DInteractionMode.Binary;
            }
            EditorGUILayout.LabelField("Binary", EditorStyles.boldLabel, GUILayout.Width(60));
            EditorGUILayout.LabelField("Accept and Decline options", GUILayout.ExpandWidth(true));
            EditorGUILayout.EndHorizontal();

            if (currentMode == U3DInteractionMode.Binary)
            {
                EditorGUI.indentLevel++;
                SerializedProperty acceptLabel = acceptChoice.FindPropertyRelative("choiceLabel");
                SerializedProperty acceptKey = acceptChoice.FindPropertyRelative("choiceKey");
                SerializedProperty declineLabel = declineChoice.FindPropertyRelative("choiceLabel");
                SerializedProperty declineKey = declineChoice.FindPropertyRelative("choiceKey");

                EditorGUILayout.LabelField("Accept Choice", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(acceptKey, new GUIContent("Choice Key"));
                EditorGUILayout.PropertyField(acceptLabel, new GUIContent("Choice Label"));
                EditorGUI.indentLevel--;

                EditorGUILayout.LabelField("Decline Choice", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(declineKey, new GUIContent("Choice Key"));
                EditorGUILayout.PropertyField(declineLabel, new GUIContent("Choice Label"));
                EditorGUI.indentLevel--;
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(5);

            // Multiple Mode
            EditorGUILayout.BeginHorizontal();
            bool multipleSelected = EditorGUILayout.Toggle(currentMode == U3DInteractionMode.Multiple, GUILayout.Width(20));
            if (multipleSelected && currentMode != U3DInteractionMode.Multiple)
            {
                interactionMode.enumValueIndex = (int)U3DInteractionMode.Multiple;
            }
            EditorGUILayout.LabelField("Multiple", EditorStyles.boldLabel, GUILayout.Width(60));
            EditorGUILayout.LabelField("Quiz-style multiple choices", GUILayout.ExpandWidth(true));
            EditorGUILayout.EndHorizontal();

            if (currentMode == U3DInteractionMode.Multiple)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(multipleChoices, new GUIContent("Multiple Choices"), true);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();

            // PRESERVED: Your working UI References section
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("UI References (Optional)", EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "These UI fields are optional. An unstyled Dialogue Canvas will be created at runtime if not assigned. " +
                "Assign your own UI elements here only if you want to customize the appearance.",
                MessageType.Info);

            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(dialogCanvas);
            EditorGUILayout.PropertyField(giverNameText);
            EditorGUILayout.PropertyField(questDescriptionText);
            EditorGUILayout.PropertyField(choicesParent);
            EditorGUI.indentLevel--;

            // PRESERVED: Your working quest status display
            if (questToGive.objectReferenceValue != null)
            {
                EditorGUILayout.Space(10);
                U3DQuest quest = questToGive.objectReferenceValue as U3DQuest;
                if (quest != null)
                {
                    EditorGUILayout.HelpBox($"Quest: {quest.questTitle}", MessageType.None);
                }
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}