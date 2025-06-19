using UnityEngine;
using UnityEditor;

namespace U3D.Editor
{
    /// <summary>
    /// Custom property drawer for U3DInteractionChoice to make it display nicely in Inspector
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

            // Get properties
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
    /// </summary>
    [CustomEditor(typeof(U3DQuestGiver))]
    public class U3DQuestGiverEditor : UnityEditor.Editor
    {
        private SerializedProperty questToGive;
        private SerializedProperty interactionChoices;
        private SerializedProperty dialogCanvas;
        private SerializedProperty giverNameText;
        private SerializedProperty questDescriptionText;
        private SerializedProperty choicesParent;

        private void OnEnable()
        {
            questToGive = serializedObject.FindProperty("questToGive");
            interactionChoices = serializedObject.FindProperty("interactionChoices");
            dialogCanvas = serializedObject.FindProperty("dialogCanvas");
            giverNameText = serializedObject.FindProperty("giverNameText");
            questDescriptionText = serializedObject.FindProperty("questDescriptionText");
            choicesParent = serializedObject.FindProperty("choicesParent");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            U3DQuestGiver questGiver = (U3DQuestGiver)target;

            // Draw default properties first
            DrawPropertiesExcluding(serializedObject,
                "interactionChoices",
                "dialogCanvas",
                "giverNameText",
                "questDescriptionText",
                "choicesParent");

            // Custom section for interaction choices
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Interaction Choices", EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "Define what choices players have when interacting. Default is 'Accept [E]'. " +
                "Add more choices for multiple options like 'Accept [E], Decline [X]' or quiz-style interactions.",
                MessageType.Info);

            // Show interaction choices with custom labels
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField("Label                           Key", EditorStyles.miniLabel);

            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(interactionChoices, new GUIContent("Available Choices"), true);
            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();

            // Add quick setup buttons
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add Accept/Decline", EditorStyles.miniButtonLeft))
            {
                AddDefaultChoices();
            }
            if (GUILayout.Button("Add Quiz Style (1,2,3)", EditorStyles.miniButtonRight))
            {
                AddQuizChoices();
            }
            EditorGUILayout.EndHorizontal();

            // UI References section with helpful text
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

            // Show current quest status
            if (questToGive.objectReferenceValue != null)
            {
                EditorGUILayout.Space(10);
                U3DQuest quest = questToGive.objectReferenceValue as U3DQuest;
                EditorGUILayout.HelpBox($"Quest Status: {quest.GetQuestStatus()}", MessageType.None);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void AddDefaultChoices()
        {
            interactionChoices.ClearArray();

            // Add Accept choice
            interactionChoices.InsertArrayElementAtIndex(0);
            var acceptChoice = interactionChoices.GetArrayElementAtIndex(0);
            acceptChoice.FindPropertyRelative("choiceLabel").stringValue = "Accept";
            acceptChoice.FindPropertyRelative("choiceKey").enumValueFlag = (int)KeyCode.E;
            acceptChoice.FindPropertyRelative("choiceID").stringValue = "accept";

            // Add Decline choice
            interactionChoices.InsertArrayElementAtIndex(1);
            var declineChoice = interactionChoices.GetArrayElementAtIndex(1);
            declineChoice.FindPropertyRelative("choiceLabel").stringValue = "Decline";
            declineChoice.FindPropertyRelative("choiceKey").enumValueFlag = (int)KeyCode.X;
            declineChoice.FindPropertyRelative("choiceID").stringValue = "decline";

            serializedObject.ApplyModifiedProperties();
        }

        private void AddQuizChoices()
        {
            interactionChoices.ClearArray();

            string[] labels = { "Option A", "Option B", "Option C" };
            KeyCode[] keys = { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3 };

            for (int i = 0; i < 3; i++)
            {
                interactionChoices.InsertArrayElementAtIndex(i);
                var choice = interactionChoices.GetArrayElementAtIndex(i);
                choice.FindPropertyRelative("choiceLabel").stringValue = labels[i];
                choice.FindPropertyRelative("choiceKey").enumValueFlag = (int)keys[i];
                choice.FindPropertyRelative("choiceID").stringValue = $"option_{i + 1}";
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}