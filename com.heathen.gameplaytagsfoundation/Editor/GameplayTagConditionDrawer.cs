using UnityEditor;
using UnityEngine;

namespace Heathen.GameplayTags.Editor
{
    [CustomPropertyDrawer(typeof(GameplayTagCondition))]
    public class GameplayTagConditionDrawer : PropertyDrawer
    {
        // Layout: [Tag 32%] [Comparison 22%] [CompareValue 14%] [Exact 8%] [LogicOp 18%]
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            float w = position.width;
            float x = position.x;
            float y = position.y;
            float h = EditorGUIUtility.singleLineHeight;
            float gap = 3f;

            float tagW    = w * 0.32f;
            float compW   = w * 0.22f;
            float valW    = w * 0.13f;
            float exactW  = 38f;
            float logicW  = w - tagW - compW - valW - exactW - gap * 5;

            float cx = x;
            var tagRect   = new Rect(cx,                y, tagW,   h); cx += tagW + gap;
            var compRect  = new Rect(cx,                y, compW,  h); cx += compW + gap;
            var valRect   = new Rect(cx,                y, valW,   h); cx += valW + gap;
            var exactRect = new Rect(cx,                y, exactW, h); cx += exactW + gap;
            var logicRect = new Rect(cx,                y, logicW, h);

            var compProp = property.FindPropertyRelative("Comparison");
            bool needsValue = NeedsCompareValue((GameplayTagComparisonOp)compProp.enumValueIndex);

            EditorGUI.PropertyField(tagRect,  property.FindPropertyRelative("Tag"),        GUIContent.none);
            EditorGUI.PropertyField(compRect, compProp,                                     GUIContent.none);

            EditorGUI.BeginDisabledGroup(!needsValue);
            EditorGUI.PropertyField(valRect,  property.FindPropertyRelative("CompareValue"), GUIContent.none);
            EditorGUI.EndDisabledGroup();

            // ExactMatch toggle with label
            EditorGUI.LabelField(new Rect(exactRect.x, exactRect.y, 28, h), "Exct");
            var exactProp = property.FindPropertyRelative("ExactMatch");
            exactProp.boolValue = EditorGUI.Toggle(
                new Rect(exactRect.x + 30, exactRect.y, 16, h), exactProp.boolValue);

            EditorGUI.PropertyField(logicRect, property.FindPropertyRelative("LogicOp"), GUIContent.none);

            EditorGUI.EndProperty();
        }

        private static bool NeedsCompareValue(GameplayTagComparisonOp op) =>
            op != GameplayTagComparisonOp.Exists && op != GameplayTagComparisonOp.NotExists;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
            EditorGUIUtility.singleLineHeight;
    }
}
