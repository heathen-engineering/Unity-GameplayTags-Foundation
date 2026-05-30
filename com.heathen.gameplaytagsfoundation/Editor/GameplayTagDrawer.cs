using System.Collections.Generic;
using System.Linq;
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

            var idProp      = property.FindPropertyRelative("_id");
            var currentId   = (ulong)idProp.ulongValue;
            var currentName = GameplayTagRegistry.GetName(currentId)
                ?? (currentId == 0 ? "(none)" : $"[Unknown:{currentId:X8}]");

            var labelRect  = new Rect(position.x, position.y, EditorGUIUtility.labelWidth, position.height);
            var buttonRect = new Rect(position.x + EditorGUIUtility.labelWidth, position.y,
                position.width - EditorGUIUtility.labelWidth, position.height);

            EditorGUI.LabelField(labelRect, label);
            if (GUI.Button(buttonRect, currentName, EditorStyles.popup))
                ShowTagMenu(idProp);

            EditorGUI.EndProperty();
        }

        // Shared tag picker used by this drawer and GameplayTagCollectionDrawer.
        // Tags that are parents of other tags are placed inside their own subfolder as "(select)"
        // to avoid GenericMenu's folder-vs-leaf conflict (both cannot share the same path).
        public static void ShowTagMenu(SerializedProperty idProp)
        {
            var allNames = GameplayTagRegistry.GetAllNames();
            if (allNames == null)
            {
                EditorUtility.DisplayDialog("Gameplay Tags",
                    "No tags registered. Add tags via Project Settings > Gameplay Tags.", "OK");
                return;
            }

            var nameSet = new HashSet<string>(allNames);
            var menu    = new GenericMenu();

            menu.AddItem(new GUIContent("(none)"), idProp.ulongValue == 0, () =>
            {
                idProp.ulongValue = 0;
                idProp.serializedObject.ApplyModifiedProperties();
            });
            menu.AddSeparator("");

            foreach (var name in nameSet.OrderBy(n => n))
            {
                // If any other tag starts with "name.", this tag is a parent folder in the menu.
                // Adding it as "Parent" conflicts with the "Parent/Child" folder path, so we place
                // it one level deeper as "Parent/(select)" to keep it clickable.
                bool hasChildren = nameSet.Any(n => n.Length > name.Length + 1
                    && n[name.Length] == '.'
                    && n.StartsWith(name));

                string menuPath  = hasChildren
                    ? name.Replace('.', '/') + "/(select)"
                    : name.Replace('.', '/');

                var  capturedId = GameplayTagRegistry.Hash(name);
                bool isSelected = idProp.ulongValue == capturedId;

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
