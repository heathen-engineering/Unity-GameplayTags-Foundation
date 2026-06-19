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

        private string  _activeSourcePath;
        private string  _filter    = "";
        private string  _newTag    = "";
        private Vector2 _srcScroll;
        private Vector2 _tagScroll;

        private readonly Dictionary<string, bool> _expanded = new();

        // ── Data cache ────────────────────────────────────────────────────────

        private struct SourceEntry
        {
            public string Path;
            public string DisplayName;
            public int    TagCount;
            public bool   Registered;
        }

        private struct TagEntry
        {
            public string FullPath;
            public string Segment;
            public int    Depth;
            public bool   IsVirtual;
            public bool   HasChildren;
        }

        private List<SourceEntry> _sourcesCache;
        private HashSet<string>   _allTagsCache;
        private List<TagEntry>    _treeCache;
        private bool              _dataDirty  = true;
        private string            _lastFilter;
        private int               _staleCount; // registered sources whose generated code is out of date

        private void InvalidateAll()
        {
            _dataDirty = true;
            _treeCache = null;
            Repaint();
        }

        private void EnsureData()
        {
            if (!_dataDirty && _sourcesCache != null) return;

            _sourcesCache = new List<SourceEntry>();
            _allTagsCache = new HashSet<string>();

            foreach (var path in GameplayTagsSources.FindAll())
            {
                var (tags, registered) = ReadGpTagsSource(path);
                _sourcesCache.Add(new SourceEntry
                {
                    Path        = path,
                    DisplayName = System.IO.Path.GetFileNameWithoutExtension(path),
                    TagCount    = tags.Count,
                    Registered  = registered,
                });
                foreach (var t in tags)
                    if (!string.IsNullOrWhiteSpace(t)) _allTagsCache.Add(t.Trim());
            }

            // Cache how many registered sources have out-of-date generated code (drives the Generate
            // button's nudge). Computed here, not per-repaint, since it reads files.
            _staleCount = 0;
            foreach (var s in _sourcesCache)
                if (s.Registered && GameplayTagsCodeGenerator.IsStale(s.Path)) _staleCount++;

            _dataDirty = false;
            _treeCache = null;
        }

        private List<TagEntry> GetTree()
        {
            EnsureData();
            if (_treeCache != null && _filter == _lastFilter) return _treeCache;
            _treeCache  = BuildTree();
            _lastFilter = _filter;
            return _treeCache;
        }

        // ── Styles ────────────────────────────────────────────────────────────

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
            GameplayTagRegistry.RegistryChanged += OnRegistryChanged;
            GameplayTagsDataEditor.ForceRefresh();
        }

        public override void OnDeactivate()
        {
            GameplayTagRegistry.RegistryChanged -= OnRegistryChanged;
        }

        private void OnRegistryChanged() => InvalidateAll();

        // ── Top-level layout ─────────────────────────────────────────────────

        public override void OnGUI(string searchContext)
        {
            DrawToolbar();
            EditorGUILayout.Space(4);
            DrawSourcesSection();
            EditorGUILayout.Space(6);
            DrawTagSection();
        }

        // ── Toolbar ──────────────────────────────────────────────────────────

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar, GUILayout.ExpandWidth(true)))
            {
                EditorGUILayout.LabelField("Gameplay Tags", EditorStyles.whiteLabel, GUILayout.Width(110));

                // Staleness nudge: how far the generated tag code is behind the .gptags sources.
                EnsureData();
                if (_staleCount > 0)
                {
                    var warn = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.95f, 0.7f, 0.2f) } };
                    EditorGUILayout.LabelField($"⚠ {_staleCount} set(s) need regenerating", warn, GUILayout.Width(190));
                }

                GUILayout.FlexibleSpace();
                if (GUILayout.Button("New", EditorStyles.toolbarButton, GUILayout.Width(38)))
                    CreateGpTagsFile();
                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(54)))
                {
                    InvalidateAll();
                    GameplayTagsDataEditor.ForceRefresh();
                }
                // Generate the baked tag code (accessors + Register). Manual + on-build only (never auto —
                // would thrash recompiles). Emphasised when something is stale.
                var prev = GUI.backgroundColor;
                if (_staleCount > 0) GUI.backgroundColor = new Color(0.95f, 0.7f, 0.2f);
                if (GUILayout.Button("Generate Code", EditorStyles.toolbarButton, GUILayout.Width(100)))
                {
                    GameplayTagsCodeGenerator.GenerateAll();
                    InvalidateAll();
                }
                GUI.backgroundColor = prev;
            }
        }

        // ── Sources section ──────────────────────────────────────────────────

        private void DrawSourcesSection()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar, GUILayout.ExpandWidth(true)))
                EditorGUILayout.LabelField("Tag Sources", EditorStyles.whiteLabel, GUILayout.ExpandWidth(true));

            EnsureData();

            _srcScroll = EditorGUILayout.BeginScrollView(_srcScroll, GUILayout.MaxHeight(120));
            foreach (var src in _sourcesCache)
                DrawSourceRow(src);
            EditorGUILayout.EndScrollView();
        }

        private void DrawSourceRow(SourceEntry src)
        {
            bool isActive = _activeSourcePath == src.Path;

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar, GUILayout.ExpandWidth(true)))
            {
                var prev = GUI.contentColor;
                GUI.contentColor = isActive ? GreenTick : new Color(1, 1, 1, 0);
                EditorGUILayout.LabelField("✓", GUILayout.Width(16));
                GUI.contentColor = prev;

                bool nowActive = GUILayout.Toggle(isActive,
                    $"{src.DisplayName}  ({src.TagCount} tags)",
                    EditorStyles.toolbarButton, GUILayout.ExpandWidth(true));
                if (nowActive != isActive)
                    _activeSourcePath = nowActive ? src.Path : null;

                bool nowReg = GUILayout.Toggle(src.Registered, "Registered",
                    EditorStyles.toolbarButton, GUILayout.Width(80));
                if (nowReg != src.Registered)
                    SetSourceRegistered(src.Path, nowReg);
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
                    _filter    = newFilter;
                    _treeCache = null;
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

        // ── Tree ─────────────────────────────────────────────────────────────

        private List<TagEntry> BuildTree()
        {
            var explicitTags = _allTagsCache ?? new HashSet<string>();

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
            var tree = GetTree();

            if (tree.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    string.IsNullOrEmpty(_filter)
                        ? "No tags found. Select a source and add tags above."
                        : "No tags match the filter.",
                    MessageType.None);
                return;
            }

            // Rebuild collapsed set from persistent state every frame (foldout fix).
            var collapsed = new HashSet<string>(
                _expanded.Where(kv => !kv.Value).Select(kv => kv.Key));

            foreach (var entry in tree)
            {
                if (collapsed.Any(c => entry.FullPath.StartsWith(c + "."))) continue;
                DrawTagRow(entry);
            }
        }

        private void DrawTagRow(TagEntry entry)
        {
            const float indent = 14f;
            const float foldW  = 16f;

            var rowRect = GUILayoutUtility.GetRect(0, EditorGUIUtility.singleLineHeight + 2,
                GUILayout.ExpandWidth(true));

            float x      = rowRect.x + entry.Depth * indent;
            float y      = rowRect.y + 1;
            float h      = EditorGUIUtility.singleLineHeight;
            float availW = rowRect.xMax - x;

            if (entry.HasChildren)
            {
                bool isExpanded  = _expanded.GetValueOrDefault(entry.FullPath, true);
                bool nowExpanded = EditorGUI.Foldout(new Rect(x, y, foldW, h), isExpanded, GUIContent.none);
                if (nowExpanded != isExpanded)
                {
                    _expanded[entry.FullPath] = nowExpanded;
                    Repaint();
                }
                x += foldW; availW -= foldW;
            }
            else
            {
                x += foldW; availW -= foldW;
            }

            EditorGUI.LabelField(new Rect(x, y, availW, h), entry.Segment,
                entry.IsVirtual ? VirtualStyle : EditorStyles.label);

            if (!entry.IsVirtual &&
                Event.current.type == EventType.MouseDown &&
                new Rect(x, y, availW, h).Contains(Event.current.mousePosition))
            {
                Event.current.Use();
                var capturedPath = entry.FullPath;
                GameplayTagEditorWindow.Open(
                    capturedPath,
                    newPath => { if (newPath != capturedPath) CommitRename(capturedPath, newPath); },
                    () => DeleteTag(capturedPath));
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
                    "Use dot-separated alphanumeric segments, e.g. Effects.Buff.Strength.", "OK");
                return;
            }

            var (tags, registered) = ReadGpTagsSource(_activeSourcePath);
            if (!tags.Contains(trimmed))
            {
                tags.Add(trimmed);
                WriteGpTagsFile(_activeSourcePath, tags, registered);
            }

            _newTag = "";
            GUI.FocusControl(null);
            InvalidateAll();
        }

        private void CommitRename(string oldFullPath, string newFullPath)
        {
            newFullPath = newFullPath.Trim();
            if (string.IsNullOrEmpty(newFullPath) || newFullPath == oldFullPath) return;

            if (!GameplayTagRegistry.ValidateTag(newFullPath))
            {
                EditorUtility.DisplayDialog("Invalid Name",
                    $"'{newFullPath}' is not a valid tag path.", "OK");
                return;
            }

            EnsureData();
            foreach (var src in _sourcesCache)
                RenameInGpTagsFile(src.Path, oldFullPath, newFullPath);

            GameplayTagsDataEditor.ForceRefresh();
            InvalidateAll();
        }

        private void DeleteTag(string fullPath)
        {
            EnsureData();
            foreach (var src in _sourcesCache)
            {
                var (tags, registered) = ReadGpTagsSource(src.Path);
                int before = tags.Count;
                tags.RemoveAll(t => t == fullPath || t.StartsWith(fullPath + "."));
                if (tags.Count != before)
                    WriteGpTagsFile(src.Path, tags, registered);
            }

            GameplayTagsDataEditor.ForceRefresh();
            InvalidateAll();
        }

        private void SetSourceRegistered(string path, bool registered)
        {
            var (tags, _) = ReadGpTagsSource(path);
            WriteGpTagsFile(path, tags, registered);
            InvalidateAll();
        }

        // ── .gptags file I/O ─────────────────────────────────────────────────

        private static (List<string> tags, bool registered) ReadGpTagsSource(string assetPath)
        {
            try
            {
                var root       = JObject.Parse(File.ReadAllText(assetPath));
                var tags       = root["tags"]?.ToObject<List<string>>() ?? new List<string>();
                var registered = root["registered"]?.Value<bool>() ?? false;
                return (tags, registered);
            }
            catch { return (new List<string>(), false); }
        }

        private static void WriteGpTagsFile(string assetPath, List<string> tags, bool registered)
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
            var (tags, registered) = ReadGpTagsSource(assetPath);
            bool changed = false;
            for (int i = 0; i < tags.Count; i++)
            {
                if (tags[i] == oldFullPath)
                { tags[i] = newFullPath; changed = true; }
                else if (tags[i].StartsWith(oldFullPath + "."))
                { tags[i] = newFullPath + tags[i].Substring(oldFullPath.Length); changed = true; }
            }
            if (changed) WriteGpTagsFile(assetPath, tags, registered);
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
            InvalidateAll();
        }
    }
}
