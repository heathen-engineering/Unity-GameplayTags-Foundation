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

            float w   = position.width;
            float x   = position.x;
            float y   = position.y;
            float h   = EditorGUIUtility.singleLineHeight;
            float gap = 3f;

            var valueTypeProp = property.FindPropertyRelative("ValueType");
            var valueTagProp  = property.FindPropertyRelative("ValueTag");
            var valueTagId    = valueTagProp?.FindPropertyRelative("_id");
            var vt            = valueTypeProp != null
                ? (GameplayTagValueType)valueTypeProp.enumValueIndex
                : GameplayTagValueType.Unsigned;

            // Layout: [Tag 30%] [Arithmetic 22%] [Type 8%] [Value/ValueTag rest]
            float tagW    = w * 0.30f;
            float arithW  = w * 0.22f;
            float typeW   = w * 0.08f;
            float valW    = w - tagW - arithW - typeW - gap * 3;

            float cx = x;
            var tagRect   = new Rect(cx, y, tagW,   h); cx += tagW   + gap;
            var arithRect = new Rect(cx, y, arithW, h); cx += arithW + gap;
            var typeRect  = new Rect(cx, y, typeW,  h); cx += typeW  + gap;
            var valRect   = new Rect(cx, y, valW,   h);

            EditorGUI.PropertyField(tagRect,   property.FindPropertyRelative("Tag"),        GUIContent.none);
            EditorGUI.PropertyField(arithRect, property.FindPropertyRelative("Arithmetic"), GUIContent.none);
            if (valueTypeProp != null)
                EditorGUI.PropertyField(typeRect, valueTypeProp, GUIContent.none);

            // Show Value when Unsigned/Signed/Decimal, ValueTag when Tag type.
            bool isTagType = vt == GameplayTagValueType.Tag
                || (valueTagId != null && valueTagId.ulongValue != 0);
            if (isTagType)
            {
                if (valueTagProp != null)
                    EditorGUI.PropertyField(valRect, valueTagProp, GUIContent.none);
            }
            else
            {
                EditorGUI.PropertyField(valRect, property.FindPropertyRelative("Value"), GUIContent.none);
            }

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
