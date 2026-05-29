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

            // Layout: [Tag 33%] [Arithmetic 24%] [Value 17%] [ValueTag rest]
            float tagW      = w * 0.33f;
            float arithW    = w * 0.24f;
            float valW      = w * 0.17f;
            float valTagW   = w - tagW - arithW - valW - gap * 3;

            float cx = x;
            var tagRect    = new Rect(cx, y, tagW,    h); cx += tagW + gap;
            var arithRect  = new Rect(cx, y, arithW,  h); cx += arithW + gap;
            var valRect    = new Rect(cx, y, valW,    h); cx += valW + gap;
            var valTagRect = new Rect(cx, y, valTagW, h);

            var valueTagProp = property.FindPropertyRelative("ValueTag");
            var valueTagId   = valueTagProp.FindPropertyRelative("_id");
            bool hasValueTag = valueTagId != null && valueTagId.ulongValue != 0;

            EditorGUI.PropertyField(tagRect,   property.FindPropertyRelative("Tag"),        GUIContent.none);
            EditorGUI.PropertyField(arithRect, property.FindPropertyRelative("Arithmetic"), GUIContent.none);

            // Grey out the constant value when a ValueTag is driving the operand.
            EditorGUI.BeginDisabledGroup(hasValueTag);
            EditorGUI.PropertyField(valRect, property.FindPropertyRelative("Value"), GUIContent.none);
            EditorGUI.EndDisabledGroup();

            EditorGUI.PropertyField(valTagRect, valueTagProp, GUIContent.none);

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
