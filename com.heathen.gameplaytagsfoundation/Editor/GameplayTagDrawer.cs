using UnityEditor;
using UnityEngine;

namespace Heathen.GameplayTags.Editor
{
    [CustomPropertyDrawer(typeof(GameplayTag))]
    public class GameplayTagDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var idProp = property.FindPropertyRelative("_id");
            var currentId = (ulong)idProp.ulongValue;
            var currentName = GameplayTagRegistry.GetName(currentId) ?? "(none)";

            var labelRect = new Rect(position.x, position.y, EditorGUIUtility.labelWidth, position.height);
            var buttonRect = new Rect(position.x + EditorGUIUtility.labelWidth, position.y,
                position.width - EditorGUIUtility.labelWidth, position.height);

            EditorGUI.LabelField(labelRect, label);
            if (GUI.Button(buttonRect, currentName, EditorStyles.popup))
                ShowTagMenu(idProp);

            EditorGUI.EndProperty();
        }

        private void ShowTagMenu(SerializedProperty idProp)
        {
            var menu = new GenericMenu();

            menu.AddItem(new GUIContent("(none)"), idProp.ulongValue == 0, () =>
            {
                idProp.ulongValue = 0;
                idProp.serializedObject.ApplyModifiedProperties();
            });

            menu.AddSeparator("");

            foreach (var name in GameplayTagRegistry.GetAllNames())
            {
                var menuPath = name.Replace('.', '/');
                var capturedName = name;
                var capturedId = GameplayTagRegistry.Hash(name);
                var isSelected = idProp.ulongValue == capturedId;

                menu.AddItem(new GUIContent(menuPath), isSelected, () =>
                {
                    idProp.ulongValue = capturedId;
                    idProp.serializedObject.ApplyModifiedProperties();
                });
            }

            menu.ShowAsContext();
        }
    }
}
