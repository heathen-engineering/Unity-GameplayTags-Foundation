using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Heathen.GameplayTags.Editor
{
    public class GameplayTagsSettingsProvider : SettingsProvider
    {
        // ── State ─────────────────────────────────────────────────────────────

        private string _activeSourcePath; // asset path of selected source (.gptags or .asset)
        private string _filter = "";
        private string _newTag = "";
        private Vector2 _srcScroll;
        private Vector2 _tagScroll;

        private readonly Dictionary<string, bool> _expanded = new();

        private string _editingPath;
        private string _editBuffer;
        private bool   _focusEdit;

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
            DrawToolsToolbar();
            EditorGUILayout.Space(4);
            DrawSourcesSection();
            EditorGUILayout.Space(6);
            DrawTagSection();
        }

        // ── Toolbar ──────────────────────────────────────────────────────────

        private void DrawToolsToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar, GUILayout.ExpandWidth(true)))
            {
                EditorGUILayout.LabelField("Gameplay Tags", EditorStyles.whiteLabel, GUILayout.Width(110));
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("New", EditorStyles.toolbarButton, GUILayout.Width(38)))
                    CreateGpTagsFile();
                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(54)))
                    GameplayTagsDataEditor.ForceRefresh();
            }
        }

        // ── Sources section ──────────────────────────────────────────────────

        private void DrawSourcesSection()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar, GUILayout.ExpandWidth(true)))
                EditorGUILayout.LabelField("Tag Sources", EditorStyles.whiteLabel, GUILayout.ExpandWidth(true));

            _srcScroll = EditorGUILayout.BeginScrollView(_srcScroll, GUILayout.MaxHeight(120));

            // .gptags files (primary format)
            var gpGuids = AssetDatabase.FindAssets("t:GameplayTagsCompiledData");
            foreach (var guid in gpGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".gptags", StringComparison.OrdinalIgnoreCase)) continue;
                var tags = ReadGpTagsSourceTags(path);
                DrawSourceRow(path, Path.GetFileNameWithoutExtension(path), tags.Count, true);
            }

            // Legacy GameplayTagsData assets
            var dbGuids = AssetDatabase.FindAssets("t:GameplayTagsData");
            foreach (var guid in dbGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var db   = AssetDatabase.LoadAssetAtPath<GameplayTagsData>(path);
                if (db == null) continue;
                DrawSourceRow(path, db.name, db.tags.Count, false);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawSourceRow(string path, string displayName, int tagCount, bool isGpTags)
        {
            bool isActive = _activeSourcePath == path;
            var  label    = isGpTags
                ? $"{displayName}  ({tagCount} tags)"
                : $"{displayName}  ({tagCount} tags)  [legacy]";

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar, GUILayout.ExpandWidth(true)))
            {
                var prevColor = GUI.contentColor;
                GUI.contentColor = isActive ? GreenTick : new Color(1, 1, 1, 0);
                EditorGUILayout.LabelField("✓", GUILayout.Width(16));
                GUI.contentColor = prevColor;

                bool nowActive = GUILayout.Toggle(isActive, label, EditorStyles.toolbarButton, GUILayout.ExpandWidth(true));
                if (nowActive != isActive)
                    _activeSourcePath = nowActive ? path : null;

                if (GUILayout.Button("Select", EditorStyles.toolbarButton, GUILayout.Width(48)))
                    Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(path);
            }
        }

        // ── Tag section ──────────────────────────────────────────────────────

        private void DrawTagSection()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar, GUILayout.ExpandWidth(true)))
                EditorGUILayout.LabelField("Tags", EditorStyles.whiteLabel, GUILayout.ExpandWidth(true));

            using (new EditorGUILayout.HorizontalScope())
            {
                var newFilter = EditorGUILayout.TextField(_filter, EditorStyles.toolbarSearchField);
                if (newFilter != _filter)
                {
                    _filter = newFilter;
                    _editingPath = null;
                }
            }

            DrawNewTagRow();

            _tagScroll = EditorGUILayout.BeginScrollView(_tagScroll);
            DrawTagTree();
            EditorGUILayout.EndScrollView();
        }

        private void DrawNewTagRow()
        {
            if (string.IsNullOrEmpty(_activeSourcePath))
            {
                EditorGUILayout.HelpBox("Select a source above to add new tags to it.", MessageType.None);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                _newTag = EditorGUILayout.TextField(_newTag, GUILayout.ExpandWidth(true));
                EditorGUI.BeginDisabledGroup(string.IsNullOrWhiteSpace(_newTag));
                if (GUILayout.Button("Add Tag", EditorStyles.miniButton, GUILayout.Width(60)))
                    CommitNewTag();
                EditorGUI.EndDisabledGroup();
            }
        }

        // ── Tree building ────────────────────────────────────────────────────

        private struct TagEntry
        {
            public string FullPath;
            public string Segment;
            public int    Depth;
            public bool   IsVirtual;
            public bool   HasChildren;
        }

        private List<TagEntry> BuildTree()
        {
            var explicitTags = new HashSet<string>();

            var gpGuids = AssetDatabase.FindAssets("t:GameplayTagsCompiledData");
            foreach (var guid in gpGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".gptags", StringComparison.OrdinalIgnoreCase)) continue;
                foreach (var tag in ReadGpTagsSourceTags(path))
                    if (!string.IsNullOrWhiteSpace(tag)) explicitTags.Add(tag.Trim());
            }

            var dbGuids = AssetDatabase.FindAssets("t:GameplayTagsData");
            foreach (var guid in dbGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var db   = AssetDatabase.LoadAssetAtPath<GameplayTagsData>(path);
                if (db == null) continue;
                foreach (var tag in db.tags)
                    if (!string.IsNullOrWhiteSpace(tag)) explicitTags.Add(tag.Trim());
            }

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
                if (!string.IsNullOrEmpty(_filter) &&
                    !path.Contains(_filter, StringComparison.OrdinalIgnoreCase) &&
                    !sorted.Any(p => p != path && p.StartsWith(path + ".") &&
                                     p.Contains(_filter, StringComparison.OrdinalIgnoreCase)))
                    continue;

                var dotIdx = path.LastIndexOf('.');
                result.Add(new TagEntry
                {
                    FullPath    = path,
                    Segment     = dotIdx < 0 ? path : path.Substring(dotIdx + 1),
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
                        ? "No tags found. Select a source and add tags above."
                        : "No tags match the filter.",
                    MessageType.None);
                return;
            }

            var collapsed = new HashSet<string>();
            foreach (var entry in tree)
            {
                if (collapsed.Any(c => entry.FullPath.StartsWith(c + "."))) continue;
                DrawTagRow(entry, collapsed);
            }

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

            float x      = rowRect.x + entry.Depth * indent;
            float y      = rowRect.y + 1;
            float h      = EditorGUIUtility.singleLineHeight;
            float availW = rowRect.xMax - x - xBtnW - 2;

            if (entry.HasChildren)
            {
                bool isExpanded  = !collapsed.Contains(entry.FullPath) &&
                                   _expanded.GetValueOrDefault(entry.FullPath, true);
                bool nowExpanded = EditorGUI.Foldout(new Rect(x, y, foldW, h), isExpanded, GUIContent.none);
                if (nowExpanded != isExpanded)
                {
                    _expanded[entry.FullPath] = nowExpanded;
                    if (!nowExpanded) collapsed.Add(entry.FullPath);
                    Repaint();
                }
                x += foldW; availW -= foldW;
            }
            else
            {
                x += foldW; availW -= foldW;
            }

            var contentRect = new Rect(x, y, availW, h);
            var deleteRect  = new Rect(rowRect.xMax - xBtnW, y, xBtnW, h);

            // ── Content: inline edit or label ────────────────────────────────

            if (!entry.IsVirtual && _editingPath == entry.FullPath)
            {
                var ctrlName = "TagEdit_" + entry.FullPath;
                GUI.SetNextControlName(ctrlName);
                _editBuffer = EditorGUI.TextField(contentRect, _editBuffer);

                if (_focusEdit) { EditorGUI.FocusTextInControl(ctrlName); _focusEdit = false; }

                var ev = Event.current;
                if (ev.isKey)
                {
                    if (ev.keyCode == KeyCode.Return || ev.keyCode == KeyCode.KeypadEnter)
                    { CommitRename(entry.FullPath, _editBuffer); ev.Use(); }
                    else if (ev.keyCode == KeyCode.Escape)
                    { _editingPath = null; ev.Use(); Repaint(); }
                }
            }
            else
            {
                EditorGUI.LabelField(contentRect, entry.Segment,
                    entry.IsVirtual ? VirtualStyle : EditorStyles.label);

                if (!entry.IsVirtual &&
                    Event.current.type == EventType.MouseDown &&
                    contentRect.Contains(Event.current.mousePosition))
                {
                    _editingPath = entry.FullPath;
                    _editBuffer  = entry.Segment;
                    _focusEdit   = true;
                    Event.current.Use();
                    Repaint();
                }
            }

            // ── Delete button (not for virtual/implied nodes) ────────────────

            if (!entry.IsVirtual)
            {
                var prev = GUI.contentColor;
                GUI.contentColor = new Color(1f, 0.4f, 0.4f);
                if (GUI.Button(deleteRect, "✕", EditorStyles.miniButton))
                    ConfirmDelete(entry.FullPath);
                GUI.contentColor = prev;
            }
        }

        // ── Mutations ────────────────────────────────────────────────────────

        private void CommitNewTag()
        {
            var trimmed = _newTag.Trim();
            if (!GameplayTagRegistry.ValidateTag(trimmed))
            {
                EditorUtility.DisplayDialog("Invalid Tag",
                    $"'{trimmed}' is not a valid tag.\n" +
                    "Use dot-separated PascalCase segments, e.g. Effects.Buff.Strength.",
                    "OK");
                return;
            }

            if (_activeSourcePath.EndsWith(".gptags", StringComparison.OrdinalIgnoreCase))
            {
                var tags = ReadGpTagsSourceTags(_activeSourcePath);
                if (!tags.Contains(trimmed))
                {
                    tags.Add(trimmed);
                    WriteGpTagsSourceTags(_activeSourcePath, tags);
                }
            }
            else
            {
                var db = AssetDatabase.LoadAssetAtPath<GameplayTagsData>(_activeSourcePath);
                if (db == null) return;
                Undo.RecordObject(db, "Add Gameplay Tag");
                if (!db.tags.Contains(trimmed))
                {
                    db.tags.Add(trimmed);
                    EditorUtility.SetDirty(db);
                    GameplayTagRegistry.RegisterDefaults(db);
                }
            }

            _newTag = "";
            GUI.FocusControl(null);
            Repaint();
        }

        private void CommitRename(string oldFullPath, string newSegment)
        {
            _editingPath = null;
            var trimmed = newSegment.Trim();
            if (string.IsNullOrEmpty(trimmed)) { Repaint(); return; }

            var dotIdx     = oldFullPath.LastIndexOf('.');
            var newFullPath = dotIdx < 0 ? trimmed : oldFullPath.Substring(0, dotIdx + 1) + trimmed;
            if (newFullPath == oldFullPath) { Repaint(); return; }

            if (!GameplayTagRegistry.ValidateTag(newFullPath))
            {
                EditorUtility.DisplayDialog("Invalid Name", $"'{newFullPath}' is not a valid tag path.", "OK");
                Repaint();
                return;
            }

            // Update all .gptags source files
            var gpGuids = AssetDatabase.FindAssets("t:GameplayTagsCompiledData");
            foreach (var guid in gpGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".gptags", StringComparison.OrdinalIgnoreCase)) continue;
                RenameInGpTagsFile(path, oldFullPath, newFullPath);
            }

            // Update all GameplayTagsData assets
            var dbGuids = AssetDatabase.FindAssets("t:GameplayTagsData");
            foreach (var guid in dbGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var db   = AssetDatabase.LoadAssetAtPath<GameplayTagsData>(path);
                if (db == null) continue;
                bool changed = false;
                for (int i = 0; i < db.tags.Count; i++)
                {
                    var t = db.tags[i];
                    if (t == oldFullPath)
                    {
                        if (!changed) Undo.RecordObject(db, "Rename Gameplay Tag");
                        db.tags[i] = newFullPath;
                        changed = true;
                    }
                    else if (t.StartsWith(oldFullPath + "."))
                    {
                        if (!changed) Undo.RecordObject(db, "Rename Gameplay Tag");
                        db.tags[i] = newFullPath + t.Substring(oldFullPath.Length);
                        changed = true;
                    }
                }
                if (changed) EditorUtility.SetDirty(db);
            }

            GameplayTagsDataEditor.ForceRefresh();
            Repaint();
        }

        private void ConfirmDelete(string fullPath)
        {
            // Count how many tags will be removed across all sources
            var gpTagsChanges = new Dictionary<string, List<string>>();
            var dbChanges     = new List<(GameplayTagsData db, List<string> toRemove)>();

            var gpGuids = AssetDatabase.FindAssets("t:GameplayTagsCompiledData");
            foreach (var guid in gpGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".gptags", StringComparison.OrdinalIgnoreCase)) continue;
                var removing = ReadGpTagsSourceTags(path)
                    .Where(t => t == fullPath || t.StartsWith(fullPath + "."))
                    .ToList();
                if (removing.Count > 0) gpTagsChanges[path] = removing;
            }

            var dbGuids = AssetDatabase.FindAssets("t:GameplayTagsData");
            foreach (var guid in dbGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var db   = AssetDatabase.LoadAssetAtPath<GameplayTagsData>(path);
                if (db == null) continue;
                var removing = db.tags.Where(t => t == fullPath || t.StartsWith(fullPath + ".")).ToList();
                if (removing.Count > 0) dbChanges.Add((db, removing));
            }

            int total = gpTagsChanges.Values.Sum(l => l.Count) + dbChanges.Sum(x => x.toRemove.Count);
            if (total == 0) return;

            var msg = total == 1
                ? $"Remove tag '{fullPath}'?"
                : $"Remove '{fullPath}' and all {total} tags in that subtree?";

            if (!EditorUtility.DisplayDialog("Remove Tag" + (total > 1 ? "s" : ""), msg, "Remove", "Cancel"))
                return;

            foreach (var (path, removing) in gpTagsChanges)
            {
                var tags = ReadGpTagsSourceTags(path);
                foreach (var r in removing) tags.Remove(r);
                WriteGpTagsSourceTags(path, tags);
            }

            foreach (var (db, removing) in dbChanges)
            {
                Undo.RecordObject(db, "Remove Gameplay Tag");
                foreach (var r in removing) db.tags.Remove(r);
                EditorUtility.SetDirty(db);
            }

            if (_editingPath == fullPath) _editingPath = null;
            GameplayTagsDataEditor.ForceRefresh();
            Repaint();
        }

        // ── .gptags file I/O ─────────────────────────────────────────────────

        private static List<string> ReadGpTagsSourceTags(string assetPath)
        {
            try
            {
                var root = JObject.Parse(File.ReadAllText(assetPath));
                return root["tags"]?.ToObject<List<string>>() ?? new List<string>();
            }
            catch { return new List<string>(); }
        }

        private static void WriteGpTagsSourceTags(string assetPath, List<string> tags, bool registered = true)
        {
            var root = new JObject
            {
                ["registered"] = registered,
                ["tags"]       = JArray.FromObject(tags)
            };
            File.WriteAllText(assetPath, root.ToString(Newtonsoft.Json.Formatting.Indented));
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        }

        private static void RenameInGpTagsFile(string assetPath, string oldFullPath, string newFullPath)
        {
            var tags    = ReadGpTagsSourceTags(assetPath);
            bool changed = false;
            for (int i = 0; i < tags.Count; i++)
            {
                if (tags[i] == oldFullPath)
                {
                    tags[i]  = newFullPath;
                    changed  = true;
                }
                else if (tags[i].StartsWith(oldFullPath + "."))
                {
                    tags[i]  = newFullPath + tags[i].Substring(oldFullPath.Length);
                    changed  = true;
                }
            }
            if (changed) WriteGpTagsSourceTags(assetPath, tags);
        }

        // ── Create new .gptags file ──────────────────────────────────────────

        private void CreateGpTagsFile()
        {
            var path = EditorUtility.SaveFilePanelInProject(
                "New Tag Source", "TagSource", "gptags", "Choose save location");
            if (string.IsNullOrEmpty(path)) return;

            var root = new JObject
            {
                ["registered"] = true,
                ["tags"]       = new JArray()
            };
            File.WriteAllText(path, root.ToString(Newtonsoft.Json.Formatting.Indented));
            AssetDatabase.ImportAsset(path);
            _activeSourcePath = path;
            Repaint();
        }
    }
}
