using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Heathen.GameplayTags.Editor
{
    public class GameplayTagsSettingsProvider : SettingsProvider
    {
        // ── State ─────────────────────────────────────────────────────────────

        private GameplayTagsSettings _settings;
        private GameplayTagsData _activeDatabase;

        private string _filter = "";
        private string _newTag = "";
        private Vector2 _dbScroll;
        private Vector2 _tagScroll;

        // Per-path foldout expansion (default open)
        private readonly Dictionary<string, bool> _expanded = new();

        // Inline editing
        private string _editingPath;   // full dot-path currently in edit mode
        private string _editBuffer;    // current text in the edit field
        private bool   _focusEdit;     // request focus on the text field next frame

        private static GUIStyle s_virtualStyle;
        private static GUIStyle VirtualStyle => s_virtualStyle ??= new GUIStyle(EditorStyles.label)
        {
            fontStyle = FontStyle.Italic,
            normal = { textColor = new Color(0.5f, 0.5f, 0.5f) }
        };
        private static readonly Color GreenTick = new Color(0.3f, 0.85f, 0.4f);

        // ── SettingsProvider plumbing ─────────────────────────────────────────

        public GameplayTagsSettingsProvider()
            : base("Project/Gameplay Tags", SettingsScope.Project) { }

        [SettingsProvider]
        public static SettingsProvider Create() => new GameplayTagsSettingsProvider
        {
            keywords = new HashSet<string>(new[] { "gameplay", "tags", "heathen" }),
        };

        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            _settings = GameplayTagsSettings.GetOrCreate();
            GameplayTagRegistry.RegistryChanged += Repaint;
            GameplayTagsDataEditor.ForceRefresh();
        }

        public override void OnDeactivate()
        {
            GameplayTagRegistry.RegistryChanged -= Repaint;
        }

        // ── Top-level layout ─────────────────────────────────────────────────

        public override void OnGUI(string searchContext)
        {
            if (_settings == null) _settings = GameplayTagsSettings.GetOrCreate();

            DrawToolsToolbar();
            EditorGUILayout.Space(4);
            DrawDatabaseSection();
            EditorGUILayout.Space(6);
            DrawTagSection();
        }

        // ── Tools toolbar ────────────────────────────────────────────────────

        private void DrawToolsToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar, GUILayout.ExpandWidth(true)))
            {
                EditorGUILayout.LabelField("Gameplay Tags", EditorStyles.whiteLabel, GUILayout.Width(110));
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Visualizer", EditorStyles.toolbarButton, GUILayout.Width(74)))
                    EditorApplication.ExecuteMenuItem("Tools/Heathen/Gameplay Tags Visualizer");
                if (GUILayout.Button("Condition Builder", EditorStyles.toolbarButton, GUILayout.Width(110)))
                    EditorApplication.ExecuteMenuItem("Tools/Heathen/Condition Set Builder");
            }
        }

        // ── Database section ─────────────────────────────────────────────────

        private void DrawDatabaseSection()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar, GUILayout.ExpandWidth(true)))
            {
                EditorGUILayout.LabelField("Tag Databases", EditorStyles.whiteLabel, GUILayout.ExpandWidth(true));

                var newAuto = GUILayout.Toggle(_settings.autoDiscover, "Auto-discover",
                    EditorStyles.toolbarButton, GUILayout.Width(100));
                if (newAuto != _settings.autoDiscover)
                {
                    Undo.RecordObject(_settings, "Gameplay Tags Settings");
                    _settings.autoDiscover = newAuto;
                    EditorUtility.SetDirty(_settings);
                    GameplayTagsDataEditor.ForceRefresh();
                }

                if (GUILayout.Button("New", EditorStyles.toolbarButton, GUILayout.Width(38)))
                    CreateDatabase();
                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(54)))
                    GameplayTagsDataEditor.ForceRefresh();
            }

            var all = _settings.GetAllDatabases();
            _dbScroll = EditorGUILayout.BeginScrollView(_dbScroll, GUILayout.MaxHeight(96));

            var pinnedSet = new HashSet<GameplayTagsData>(_settings.databases);
            for (int i = 0; i < all.Count; i++)
            {
                var db = all[i];
                bool isActive = _activeDatabase == db;
                bool isPinned = pinnedSet.Contains(db);

                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar, GUILayout.ExpandWidth(true)))
                {
                    // Green tick for the active database
                    var prevColor = GUI.contentColor;
                    GUI.contentColor = isActive ? GreenTick : new Color(1, 1, 1, 0);
                    EditorGUILayout.LabelField("✓", GUILayout.Width(16));
                    GUI.contentColor = prevColor;

                    if (GUILayout.Toggle(isActive, $"{db.name}  ({db.tags.Count} tags)",
                        EditorStyles.toolbarButton, GUILayout.ExpandWidth(true)))
                        _activeDatabase = db;

                    if (GUILayout.Button("Select", EditorStyles.toolbarButton, GUILayout.Width(48)))
                        Selection.activeObject = db;

                    if (isPinned)
                    {
                        if (GUILayout.Button("Unpin", EditorStyles.toolbarButton, GUILayout.Width(44)))
                        {
                            var idx = _settings.databases.IndexOf(db);
                            if (idx >= 0)
                            {
                                Undo.RecordObject(_settings, "Unpin Tag Database");
                                _settings.databases.RemoveAt(idx);
                                EditorUtility.SetDirty(_settings);
                                if (_activeDatabase == db) _activeDatabase = null;
                            }
                        }
                    }
                    else
                    {
                        if (GUILayout.Button("Pin", EditorStyles.toolbarButton, GUILayout.Width(30)))
                        {
                            Undo.RecordObject(_settings, "Pin Tag Database");
                            _settings.databases.Add(db);
                            EditorUtility.SetDirty(_settings);
                        }
                    }
                }
            }

            EditorGUILayout.EndScrollView();

            // Drag-drop zone
            var dropRect = GUILayoutUtility.GetRect(0, 20, GUILayout.ExpandWidth(true));
            GUI.Box(dropRect, "Drop GameplayTagsData here to pin", EditorStyles.helpBox);
            HandleDatabaseDrop(dropRect);
        }

        private void HandleDatabaseDrop(Rect rect)
        {
            var ev = Event.current;
            if (!rect.Contains(ev.mousePosition)) return;
            if (ev.type == EventType.DragUpdated)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                ev.Use();
            }
            else if (ev.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                foreach (var obj in DragAndDrop.objectReferences)
                {
                    if (obj is GameplayTagsData db && !_settings.databases.Contains(db))
                    {
                        Undo.RecordObject(_settings, "Pin Tag Database");
                        _settings.databases.Add(db);
                        EditorUtility.SetDirty(_settings);
                    }
                }
                ev.Use();
            }
        }

        // ── Tag section ──────────────────────────────────────────────────────

        private void DrawTagSection()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar, GUILayout.ExpandWidth(true)))
                EditorGUILayout.LabelField("Tags", EditorStyles.whiteLabel, GUILayout.ExpandWidth(true));

            // Filter
            using (new EditorGUILayout.HorizontalScope())
            {
                var newFilter = EditorGUILayout.TextField(_filter, EditorStyles.toolbarSearchField);
                if (newFilter != _filter)
                {
                    _filter = newFilter;
                    _editingPath = null;
                }
            }

            // New tag row
            DrawNewTagRow();

            // Unified tree
            _tagScroll = EditorGUILayout.BeginScrollView(_tagScroll);
            DrawTagTree();
            EditorGUILayout.EndScrollView();
        }

        private void DrawNewTagRow()
        {
            if (_activeDatabase == null)
            {
                EditorGUILayout.HelpBox("Select a database (above) to add new tags to it.", MessageType.None);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                _newTag = EditorGUILayout.TextField(_newTag, GUILayout.ExpandWidth(true));

                EditorGUI.BeginDisabledGroup(string.IsNullOrWhiteSpace(_newTag));
                if (GUILayout.Button("Add Tag", EditorStyles.miniButton, GUILayout.Width(60)) ||
                    (Event.current.isKey && Event.current.keyCode == KeyCode.Return &&
                     GUI.GetNameOfFocusedControl() == "NewTagField"))
                {
                    CommitNewTag();
                }
                EditorGUI.EndDisabledGroup();
            }
        }

        // ── Tree building ────────────────────────────────────────────────────

        private struct TagEntry
        {
            public string FullPath;
            public string Segment;   // last dot-segment
            public int    Depth;
            public bool   IsVirtual; // implied by a child, not explicit in any database
            public bool   HasChildren;
        }

        private List<TagEntry> BuildTree()
        {
            // Collect all explicitly stored tags across all databases
            var explicitTags = new HashSet<string>();
            foreach (var db in _settings.GetAllDatabases())
                foreach (var tag in db.tags)
                    if (!string.IsNullOrWhiteSpace(tag)) explicitTags.Add(tag);

            // Add implied intermediate nodes
            var all = new HashSet<string>(explicitTags);
            foreach (var tag in explicitTags)
            {
                var parts = tag.Split('.');
                for (int i = 1; i < parts.Length; i++)
                    all.Add(string.Join(".", parts.Take(i)));
            }

            var sorted = all.OrderBy(p => p).ToList();

            var result = new List<TagEntry>();
            foreach (var path in sorted)
            {
                // Apply filter: keep if path or any descendant matches
                if (!string.IsNullOrEmpty(_filter) &&
                    !path.Contains(_filter, System.StringComparison.OrdinalIgnoreCase) &&
                    !sorted.Any(p => p != path && p.StartsWith(path + ".") &&
                                     p.Contains(_filter, System.StringComparison.OrdinalIgnoreCase)))
                    continue;

                var dotIndex = path.LastIndexOf('.');
                result.Add(new TagEntry
                {
                    FullPath    = path,
                    Segment     = dotIndex < 0 ? path : path.Substring(dotIndex + 1),
                    Depth       = path.Count(c => c == '.'),
                    IsVirtual   = !explicitTags.Contains(path),
                    HasChildren = sorted.Any(p => p.StartsWith(path + ".")),
                });
            }
            return result;
        }

        private void DrawTagTree()
        {
            var tree = BuildTree();
            if (tree.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    string.IsNullOrEmpty(_filter)
                        ? "No tags registered. Select a database and add tags above."
                        : "No tags match the filter.",
                    MessageType.None);
                return;
            }

            // Collapse state: if a parent is collapsed, skip all children
            var collapsed = new HashSet<string>();

            foreach (var entry in tree)
            {
                // Skip children of collapsed nodes
                if (collapsed.Any(c => entry.FullPath.StartsWith(c + ".")))
                    continue;

                DrawTagRow(entry, collapsed);
            }

            // Consume any lingering Enter key
            if (Event.current.isKey && Event.current.keyCode == KeyCode.Escape)
            {
                _editingPath = null;
                Event.current.Use();
                Repaint();
            }
        }

        private void DrawTagRow(TagEntry entry, HashSet<string> collapsed)
        {
            const float indent = 14f;
            const float xBtnW = 22f;
            const float foldW = 16f;

            var rowRect = GUILayoutUtility.GetRect(0, EditorGUIUtility.singleLineHeight + 2,
                GUILayout.ExpandWidth(true));

            float x = rowRect.x + entry.Depth * indent;
            float y = rowRect.y + 1;
            float h = EditorGUIUtility.singleLineHeight;
            float availW = rowRect.xMax - x - xBtnW - 2;

            // Foldout triangle for nodes with children
            if (entry.HasChildren)
            {
                bool isExpanded = !collapsed.Contains(entry.FullPath) &&
                                  _expanded.GetValueOrDefault(entry.FullPath, true);
                var foldRect = new Rect(x, y, foldW, h);
                bool nowExpanded = EditorGUI.Foldout(foldRect, isExpanded, GUIContent.none);
                if (nowExpanded != isExpanded)
                {
                    _expanded[entry.FullPath] = nowExpanded;
                    if (!nowExpanded) collapsed.Add(entry.FullPath);
                    Repaint();
                }
                x += foldW;
                availW -= foldW;
            }
            else
            {
                x += foldW; availW -= foldW; // keep alignment
            }

            var contentRect = new Rect(x, y, availW, h);
            var deleteRect  = new Rect(rowRect.xMax - xBtnW, y, xBtnW, h);

            // ── Content: editing or label ────────────────────────────────────

            if (_editingPath == entry.FullPath)
            {
                var ctrlName = "TagEdit_" + entry.FullPath;
                GUI.SetNextControlName(ctrlName);
                _editBuffer = EditorGUI.TextField(contentRect, _editBuffer);

                if (_focusEdit)
                {
                    EditorGUI.FocusTextInControl(ctrlName);
                    _focusEdit = false;
                }

                var ev = Event.current;
                if (ev.isKey)
                {
                    if (ev.keyCode == KeyCode.Return || ev.keyCode == KeyCode.KeypadEnter)
                    {
                        CommitRename(entry.FullPath, _editBuffer);
                        ev.Use();
                    }
                    else if (ev.keyCode == KeyCode.Escape)
                    {
                        _editingPath = null;
                        ev.Use();
                        Repaint();
                    }
                }
            }
            else
            {
                var style = entry.IsVirtual ? VirtualStyle : EditorStyles.label;
                EditorGUI.LabelField(contentRect, entry.Segment, style);

                // Click to begin editing
                if (Event.current.type == EventType.MouseDown &&
                    contentRect.Contains(Event.current.mousePosition))
                {
                    _editingPath = entry.FullPath;
                    _editBuffer  = entry.Segment;
                    _focusEdit   = true;
                    Event.current.Use();
                    Repaint();
                }
            }

            // ── [x] delete button ────────────────────────────────────────────

            var prevColor = GUI.contentColor;
            GUI.contentColor = new Color(1f, 0.4f, 0.4f);
            if (GUI.Button(deleteRect, "✕", EditorStyles.miniButton))
                ConfirmDelete(entry);
            GUI.contentColor = prevColor;
        }

        // ── Rename ───────────────────────────────────────────────────────────

        private void CommitNewTag()
        {
            var trimmed = _newTag.Trim();
            if (!GameplayTagRegistry.ValidateTag(trimmed))
            {
                EditorUtility.DisplayDialog("Invalid Tag",
                    $"'{trimmed}' is not a valid tag.\nUse dot-separated PascalCase segments, e.g. Effects.Buff.Strength.",
                    "OK");
                return;
            }

            Undo.RecordObject(_activeDatabase, "Add Gameplay Tag");
            _activeDatabase.tags.Add(trimmed);
            EditorUtility.SetDirty(_activeDatabase);
            GameplayTagRegistry.RegisterDefaults(_activeDatabase);
            _newTag = "";
            GUI.FocusControl(null);
        }

        private void CommitRename(string oldFullPath, string newSegment)
        {
            _editingPath = null;

            var trimmed = newSegment.Trim();
            if (string.IsNullOrEmpty(trimmed)) { Repaint(); return; }

            // Build the new full path by replacing only the renamed segment
            var dotIndex = oldFullPath.LastIndexOf('.');
            var newFullPath = dotIndex < 0 ? trimmed : oldFullPath.Substring(0, dotIndex + 1) + trimmed;

            if (newFullPath == oldFullPath) { Repaint(); return; }

            if (!GameplayTagRegistry.ValidateTag(newFullPath))
            {
                EditorUtility.DisplayDialog("Invalid Name",
                    $"'{newFullPath}' is not a valid tag path.", "OK");
                Repaint();
                return;
            }

            // Apply across all databases
            bool changed = false;
            foreach (var db in _settings.GetAllDatabases())
            {
                for (int i = 0; i < db.tags.Count; i++)
                {
                    var t = db.tags[i];
                    if (t == oldFullPath)
                    {
                        Undo.RecordObject(db, "Rename Gameplay Tag");
                        db.tags[i] = newFullPath;
                        changed = true;
                    }
                    else if (t.StartsWith(oldFullPath + "."))
                    {
                        Undo.RecordObject(db, "Rename Gameplay Tag");
                        db.tags[i] = newFullPath + t.Substring(oldFullPath.Length);
                        changed = true;
                    }
                }
                if (changed) EditorUtility.SetDirty(db);
            }

            GameplayTagsDataEditor.ForceRefresh();
            Repaint();
        }

        // ── Delete ───────────────────────────────────────────────────────────

        private void ConfirmDelete(TagEntry entry)
        {
            // Collect everything that would be removed
            var toRemove = new List<(GameplayTagsData db, string tag)>();
            foreach (var db in _settings.GetAllDatabases())
            {
                foreach (var tag in db.tags)
                {
                    if (tag == entry.FullPath || tag.StartsWith(entry.FullPath + "."))
                        toRemove.Add((db, tag));
                }
            }

            if (toRemove.Count == 0) return;

            string msg = toRemove.Count == 1
                ? $"Remove tag '{toRemove[0].tag}'?"
                : $"This will remove {toRemove.Count} tags:\n\n" +
                  string.Join("\n", toRemove.Select(r => $"  • {r.tag}")) +
                  "\n\nAre you sure?";

            if (!EditorUtility.DisplayDialog("Remove Tag" + (toRemove.Count > 1 ? "s" : ""),
                msg, "Remove", "Cancel"))
                return;

            foreach (var (db, tag) in toRemove)
            {
                Undo.RecordObject(db, "Remove Gameplay Tag");
                db.tags.Remove(tag);
                EditorUtility.SetDirty(db);
            }

            if (_editingPath == entry.FullPath) _editingPath = null;
            GameplayTagsDataEditor.ForceRefresh();
            Repaint();
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private void CreateDatabase()
        {
            var path = EditorUtility.SaveFilePanelInProject(
                "New Tag Database", "TagDatabase", "asset", "Choose save location");
            if (string.IsNullOrEmpty(path)) return;

            var asset = ScriptableObject.CreateInstance<GameplayTagsData>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();

            if (!_settings.databases.Contains(asset))
            {
                Undo.RecordObject(_settings, "Pin Tag Database");
                _settings.databases.Add(asset);
                EditorUtility.SetDirty(_settings);
            }
            _activeDatabase = asset;
            GameplayTagsDataEditor.ForceRefresh();
        }
    }
}
