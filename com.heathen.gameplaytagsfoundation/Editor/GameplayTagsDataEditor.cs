using System.IO;
using UnityEditor;
using UnityEngine;

namespace Heathen.GameplayTags.Editor
{
    [InitializeOnLoad]
    [CustomEditor(typeof(GameplayTagsData))]
    public class GameplayTagsDataEditor : UnityEditor.Editor
    {
        static GameplayTagsDataEditor()
        {
            EditorApplication.delayCall += RefreshEditorRegistry;
        }

        public static void ForceRefresh() => RefreshEditorRegistry();

        private static void RefreshEditorRegistry()
        {
            var guids = AssetDatabase.FindAssets("t:GameplayTagsData");
            foreach (var guid in guids)
            {
                var path  = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<GameplayTagsData>(path);
                if (asset != null && asset.autoRegister)
                    GameplayTagRegistry.RegisterDefaults(asset);
            }

            // Also refresh compiled .gptags assets.
            GameplayTagsCompiledDataRefresh.Refresh();
        }

        private SerializedProperty _autoRegister;
        private SerializedProperty _tags;

        private void OnEnable()
        {
            _autoRegister = serializedObject.FindProperty("autoRegister");
            _tags = serializedObject.FindProperty("tags");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_autoRegister);
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Tags", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_tags, includeChildren: true);

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            var errors = ((GameplayTagsData)target).GetValidationErrors();
            if (errors.Count > 0)
            {
                EditorGUILayout.HelpBox(string.Join("\n", errors), MessageType.Warning);
            }

            if (GUILayout.Button("Refresh Editor Registry"))
                RefreshEditorRegistry();
        }
    }
}
