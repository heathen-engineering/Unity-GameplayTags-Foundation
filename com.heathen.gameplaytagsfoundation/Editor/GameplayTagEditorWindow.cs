using System;
using UnityEditor;
using UnityEngine;

namespace Heathen.GameplayTags.Editor
{
    internal class GameplayTagEditorWindow : EditorWindow
    {
        private static GameplayTagEditorWindow _current;
        private static Action<string> _onConfirm;
        private static Action         _onDelete;

        private string _editBuffer;

        public static void Open(string fullPath, Action<string> onConfirm, Action onDelete)
        {
            if (_current != null) _current.Close();

            _current              = CreateInstance<GameplayTagEditorWindow>();
            _current.titleContent = new GUIContent("Edit Tag");
            _current.minSize      = new Vector2(400, 90);
            _current.maxSize      = new Vector2(700, 160);
            _current._editBuffer  = fullPath;
            _onConfirm = onConfirm;
            _onDelete  = onDelete;
            _current.ShowUtility();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(6);
            _editBuffer = EditorGUILayout.TextField("Full Path", _editBuffer);

            bool isValid = !string.IsNullOrWhiteSpace(_editBuffer) &&
                           GameplayTagRegistry.ValidateTag(_editBuffer.Trim());

            if (!string.IsNullOrWhiteSpace(_editBuffer) && !isValid)
                EditorGUILayout.HelpBox(
                    "Use dot-separated alphanumeric/underscore segments — e.g. Shop.Bakery.Back",
                    MessageType.Warning);

            GUILayout.FlexibleSpace();

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginDisabledGroup(!isValid);
                if (GUILayout.Button("OK", GUILayout.Width(90)))
                {
                    var path = _editBuffer.Trim();
                    var cb   = _onConfirm;
                    Close();
                    cb?.Invoke(path);
                }
                EditorGUI.EndDisabledGroup();

                if (GUILayout.Button("Cancel", GUILayout.Width(90)))
                    Close();

                GUILayout.FlexibleSpace();

                var prevBg = GUI.backgroundColor;
                GUI.backgroundColor = new Color(1f, 0.35f, 0.35f);
                if (GUILayout.Button("Delete", GUILayout.Width(90)))
                {
                    var cb = _onDelete;
                    Close();
                    cb?.Invoke();
                }
                GUI.backgroundColor = prevBg;
            }

            EditorGUILayout.Space(6);
        }

        private void OnDestroy() => _current = null;
    }
}
