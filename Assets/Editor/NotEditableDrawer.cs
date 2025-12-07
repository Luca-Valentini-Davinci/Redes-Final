using UnityEditor;
using UnityEngine;


    [CustomPropertyDrawer(typeof(NotEditable))]
    public class NotEditableDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            // Return the standard height for the property.
            return EditorGUI.GetPropertyHeight(property, label, true);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // Temporarily disable GUI interactions.
            GUI.enabled = false;

            // Draw the property field as usual, but it will be non-interactive.
            EditorGUI.PropertyField(position, property, label, true);

            // Re-enable GUI interactions for subsequent fields.
            GUI.enabled = true;
        }
    }

