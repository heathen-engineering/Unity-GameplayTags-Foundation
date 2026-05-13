using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Heathen.GameplayTags.Editor
{
    [CustomPropertyDrawer(typeof(GameplayTagCollection))]
    public class GameplayTagCollectionDrawer : PropertyDrawer
    {
        private ReorderableList _list;
        private SerializedProperty _serializedProp;

        private void Init(SerializedProperty property)
        {
            _serializedProp = property.FindPropertyRelative("_serialized");
            _list = new ReorderableList(property.serializedObject, _serializedProp, true, true, true, true)
            {
                drawHeaderCallback = rect =>
                {
                    var tagW = rect.width * 0.65f;
                    EditorGUI.LabelField(new Rect(rect.x, rect.y, tagW, rect.height), "Tag");
                    EditorGUI.LabelField(new Rect(rect.x + tagW + 4, rect.y, rect.width - tagW - 4, rect.height), "Value");
                },
                drawElementCallback = DrawElement,
                elementHeight = EditorGUIUtility.singleLineHeight + 4,
            };
        }

        private void DrawElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            var element = _serializedProp.GetArrayElementAtIndex(index);
            var idProp    = element.FindPropertyRelative("id");
            var valueProp = element.FindPropertyRelative("value");

            float tagW = rect.width * 0.65f;
            var tagRect   = new Rect(rect.x, rect.y + 2, tagW, EditorGUIUtility.singleLineHeight);
            var valueRect = new Rect(rect.x + tagW + 4, rect.y + 2, rect.width - tagW - 4, EditorGUIUtility.singleLineHeight);

            // Draw a tag picker for the id field using the same GenericMenu as GameplayTagDrawer
            DrawTagIdField(tagRect, idProp);
            EditorGUI.PropertyField(valueRect, valueProp, GUIContent.none);
        }

        private static void DrawTagIdField(Rect rect, SerializedProperty idProp)
        {
            var id = (ulong)idProp.ulongValue;
            var name = GameplayTagRegistry.GetName(id) ?? (id == 0 ? "(none)" : $"[Unknown:{id:X8}]");
            bool isBroken = id != 0 && GameplayTagRegistry.GetName(id) == null;

            var style = isBroken ? new GUIStyle(EditorStyles.popup) { normal = { textColor = Color.yellow } }
                                 : EditorStyles.popup;

            if (GUI.Button(rect, name, style))
                ShowTagMenu(idProp);
        }

        private static void ShowTagMenu(SerializedProperty idProp)
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
                var capturedId = GameplayTagRegistry.Hash(name);
                bool selected = idProp.ulongValue == capturedId;
                menu.AddItem(new GUIContent(menuPath), selected, () =>
                {
                    idProp.ulongValue = capturedId;
                    idProp.serializedObject.ApplyModifiedProperties();
                });
            }
            menu.ShowAsContext();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (_list == null) Init(property);
            return _list.GetHeight();
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (_list == null) Init(property);
            _list.DoList(position);
        }
    }
}
