using UnityEditor;
using UnityEngine;

namespace Heathen.GameplayTags.Editor
{
    [CustomPropertyDrawer(typeof(GameplayTagOperation))]
    public class GameplayTagOperationDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            float w = position.width;
            float x = position.x;
            float y = position.y;
            float h = EditorGUIUtility.singleLineHeight;
            float gap = 3f;

            float tagW   = w * 0.38f;
            float arithW = w * 0.27f;
            float valW   = w - tagW - arithW - gap * 2;

            var tagRect   = new Rect(x,                    y, tagW,   h);
            var arithRect = new Rect(x + tagW + gap,       y, arithW, h);
            var valRect   = new Rect(x + tagW + arithW + gap * 2, y, valW, h);

            EditorGUI.PropertyField(tagRect,   property.FindPropertyRelative("Tag"),        GUIContent.none);
            EditorGUI.PropertyField(arithRect, property.FindPropertyRelative("Arithmetic"), GUIContent.none);
            EditorGUI.PropertyField(valRect,   property.FindPropertyRelative("Value"),      GUIContent.none);

            var condProp = property.FindPropertyRelative("Conditions");
            if (condProp != null)
            {
                var condRect = new Rect(x, y + h + 2, w, EditorGUI.GetPropertyHeight(condProp, true));
                EditorGUI.PropertyField(condRect, condProp, new GUIContent("Conditions"), true);
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float h = EditorGUIUtility.singleLineHeight + 2;
            var condProp = property.FindPropertyRelative("Conditions");
            if (condProp != null)
                h += EditorGUI.GetPropertyHeight(condProp, true);
            return h;
        }
    }
}
