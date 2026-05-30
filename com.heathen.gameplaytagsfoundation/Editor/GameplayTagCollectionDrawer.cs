using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Heathen.GameplayTags.Editor
{
    [CustomPropertyDrawer(typeof(GameplayTagCollection))]
    public class GameplayTagCollectionDrawer : PropertyDrawer
    {
        private ReorderableList    _list;
        private SerializedProperty _serializedProp;

        private void Init(SerializedProperty property)
        {
            _serializedProp = property.FindPropertyRelative("_serialized");
            _list = new ReorderableList(property.serializedObject, _serializedProp,
                draggable: true, displayHeader: true, displayAddButton: true, displayRemoveButton: true)
            {
                drawHeaderCallback = rect =>
                {
                    float tagW = rect.width * 0.65f;
                    EditorGUI.LabelField(new Rect(rect.x,          rect.y, tagW,                 rect.height), "Tag");
                    EditorGUI.LabelField(new Rect(rect.x + tagW + 4, rect.y, rect.width - tagW - 4, rect.height), "Value");
                },
                drawElementCallback  = DrawElement,
                elementHeight        = EditorGUIUtility.singleLineHeight + 4,

                // Default Unity add appends a zeroed struct {id=0, value=0}.
                // OnAfterDeserialize ignores entries with value=0, so the row silently disappears.
                // Override to insert a row with value=1 so it survives the round-trip.
                onAddCallback = list =>
                {
                    int idx = _serializedProp.arraySize;
                    _serializedProp.InsertArrayElementAtIndex(idx);
                    var elem = _serializedProp.GetArrayElementAtIndex(idx);
                    elem.FindPropertyRelative("id").ulongValue    = 0;
                    elem.FindPropertyRelative("value").ulongValue = 1;
                    _serializedProp.serializedObject.ApplyModifiedProperties();
                },
            };
        }

        private void DrawElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            var element   = _serializedProp.GetArrayElementAtIndex(index);
            var idProp    = element.FindPropertyRelative("id");
            var valueProp = element.FindPropertyRelative("value");

            float tagW     = rect.width * 0.65f;
            var   tagRect  = new Rect(rect.x,          rect.y + 2, tagW,                 EditorGUIUtility.singleLineHeight);
            var   valRect  = new Rect(rect.x + tagW + 4, rect.y + 2, rect.width - tagW - 4, EditorGUIUtility.singleLineHeight);

            DrawTagIdField(tagRect, idProp);
            EditorGUI.PropertyField(valRect, valueProp, GUIContent.none);
        }

        private static void DrawTagIdField(Rect rect, SerializedProperty idProp)
        {
            var    id       = (ulong)idProp.ulongValue;
            var    name     = GameplayTagRegistry.GetName(id) ?? (id == 0 ? "(none)" : $"[Unknown:{id:X8}]");
            bool   isBroken = id != 0 && GameplayTagRegistry.GetName(id) == null;
            var    style    = isBroken
                ? new GUIStyle(EditorStyles.popup) { normal = { textColor = Color.yellow } }
                : EditorStyles.popup;

            if (GUI.Button(rect, name, style))
                GameplayTagDrawer.ShowTagMenu(idProp);
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
