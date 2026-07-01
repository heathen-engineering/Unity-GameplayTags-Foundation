using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Heathen.Editor; // SettingsMetadata

namespace Heathen.GameplayTags.Editor
{
    /// <summary>
    /// Project Settings ▸ Gameplay Tags. Edits the project's standalone tag vocabulary
    /// (<see cref="GameplayTagSettings"/> in <c>ProjectSettings</c>) as two groups: <b>Registered</b>
    /// (hierarchy-aware — registered live and baked to runtime) and <b>Unregistered</b> (authored drafts that
    /// are stored but not registered or baked). Tool-owned tags (Ogham stories, HATE worlds, …) are not shown
    /// here — each tool owns its own tags. The <c>.gptags</c> format is a runtime mod / UGC source only.
    /// </summary>
    public class GameplayTagsSettingsProvider : SettingsProvider
    {
        // ── State ─────────────────────────────────────────────────────────────

        private GameplayTagSettings _settings;
        private string  _filter   = "";
        private string  _newTag   = "";
        private bool    _newTagRegistered = true;
        private Vector2 _scroll;

        private readonly Dictionary<string, bool> _expanded = new();

        // ── Data cache ────────────────────────────────────────────────────────

        private struct TagEntry
        {
            public string FullPath;
            public string Segment;
            public int    Depth;
            public bool   IsVirtual;    // an implied parent, not an explicitly authored tag
            public bool   Registered;   // explicit tag's group (meaningless for virtual)
            public bool   HasChildren;
        }

        private HashSet<string> _registered;
        private HashSet<string> _unregistered;
        private List<TagEntry>  _treeCache;
        private string          _lastFilter;
        private bool            _dataDirty = true;
        private bool            _stale;

        // Read-only provenance view: tags contributed by other tools (via ITagSource), plus a catch-all of any
        // live-registry tags not attributed to the project store or a source. Computed in EnsureData (the ITagSource
        // getters scan files), not per repaint.
        private List<(string name, List<string> tags, bool registered)> _contributed;
        private List<string> _unattributed;

        private void InvalidateAll()
        {
            _dataDirty = true;
            _treeCache = null;
            Repaint();
        }

        private void EnsureData()
        {
            if (!_dataDirty && _settings != null) return;

            _settings     = GameplayTagSettings.GetOrCreate();
            _registered   = new HashSet<string>(_settings.RegisteredTags   ?? new List<string>());
            _unregistered = new HashSet<string>(_settings.UnregisteredTags ?? new List<string>());
            _stale        = GameplayTagsCodeGenerator.IsStale();
            BuildContributed();
            _dataDirty    = false;
            _treeCache    = null;
        }

        // Aggregate the provenance view once per refresh: each ITagSource's tags, plus any registry tags not
        // attributed to the project store or a source (so nothing in the system is invisible).
        private void BuildContributed()
        {
            _contributed  = new List<(string, List<string>, bool)>();
            var claimed   = new HashSet<string>();
            foreach (var t in _registered)   AddWithParents(claimed, t);
            foreach (var t in _unregistered) AddWithParents(claimed, t);

            foreach (var src in SettingsMetadata.All<ITagSource>().OrderBy(s => s.SourceName, StringComparer.Ordinal))
            {
                var tags = (src.Tags ?? Enumerable.Empty<string>())
                    .Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim())
                    .Distinct().OrderBy(t => t, StringComparer.Ordinal).ToList();
                foreach (var t in tags) AddWithParents(claimed, t);
                if (tags.Count > 0) _contributed.Add((src.SourceName, tags, src.Registered));
            }

            _unattributed = GameplayTagRegistry.GetAllNames()
                .Where(n => !claimed.Contains(n))
                .OrderBy(n => n, StringComparer.Ordinal).ToList();
        }

        private static void AddWithParents(HashSet<string> set, string tag)
        {
            var parts = tag.Split('.');
            var sb = new StringBuilder();
            for (int i = 0; i < parts.Length; i++)
            {
                if (i > 0) sb.Append('.');
                sb.Append(parts[i]);
                set.Add(sb.ToString());
            }
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

        // ── SettingsProvider plumbing ─────────────────────────────────────────

        public GameplayTagsSettingsProvider()
            : base("Project/Subsystems/Gameplay Tags", SettingsScope.Project) { }

        [SettingsProvider]
        public static SettingsProvider Create() => new GameplayTagsSettingsProvider
        {
            keywords = new HashSet<string>(new[] { "gameplay", "tags", "heathen" }),
        };

        public override void OnActivate(string searchContext, UnityEngine.UIElements.VisualElement rootElement)
        {
            GameplayTagRegistry.RegistryChanged += OnRegistryChanged;
            InvalidateAll();
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
            DrawAddRow();
            EditorGUILayout.Space(2);
            DrawFilter();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawTagTree();
            DrawContributedSources();
            EditorGUILayout.EndScrollView();
        }

        // ── Toolbar ──────────────────────────────────────────────────────────

        private void DrawToolbar()
        {
            EnsureData();
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar, GUILayout.ExpandWidth(true)))
            {
                EditorGUILayout.LabelField("Gameplay Tags", EditorStyles.whiteLabel, GUILayout.Width(110));

                if (_stale)
                {
                    var warn = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.95f, 0.7f, 0.2f) } };
                    EditorGUILayout.LabelField("⚠ tag code needs regenerating", warn, GUILayout.Width(190));
                }

                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(54)))
                {
                    GameplayTagsEditorRegistrar.Refresh();
                    InvalidateAll();
                }
                // Bake the registered tags to code (accessors + Register). Manual + on-build only (never auto —
                // would thrash recompiles). Shared Heathen Build/status button.
                var status = !GameplayTagsCodeGenerator.ScriptExists() ? HeathenEditorStyles.BuildStatus.NotBuilt
                           : _stale ? HeathenEditorStyles.BuildStatus.Dirty
                           : HeathenEditorStyles.BuildStatus.UpToDate;
                if (HeathenEditorStyles.BuildStatusButton(status))
                {
                    GameplayTagsCodeGenerator.GenerateAll();
                    InvalidateAll();
                }
            }
        }

        // ── Add row ──────────────────────────────────────────────────────────

        private void DrawAddRow()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                _newTag = EditorGUILayout.TextField(_newTag, GUILayout.ExpandWidth(true));
                _newTagRegistered = GUILayout.Toggle(_newTagRegistered, "Registered",
                    EditorStyles.miniButton, GUILayout.Width(80));
                EditorGUI.BeginDisabledGroup(string.IsNullOrWhiteSpace(_newTag));
                if (GUILayout.Button("Add Tag", EditorStyles.miniButton, GUILayout.Width(60)))
                    CommitNewTag();
                EditorGUI.EndDisabledGroup();
            }
        }

        private void DrawFilter()
        {
            var newFilter = EditorGUILayout.TextField(_filter, EditorStyles.toolbarSearchField);
            if (newFilter != _filter)
            {
                _filter    = newFilter;
                _treeCache = null;
            }
        }

        // ── Tree ─────────────────────────────────────────────────────────────

        private List<TagEntry> BuildTree()
        {
            var explicitTags = new HashSet<string>(_registered);
            explicitTags.UnionWith(_unregistered);

            var all = new HashSet<string>(explicitTags);
            foreach (var tag in explicitTags)
            {
                var parts = tag.Split('.');
                for (int i = 1; i < parts.Length; i++)
                    all.Add(string.Join(".", parts.Take(i)));
            }

            var sorted = all.OrderBy(p => p, StringComparer.Ordinal).ToList();
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
                    Registered  = _registered.Contains(path),
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
                        ? "No project tags yet. Add one above. (Tool tags — Ogham stories, HATE worlds — are owned by their tools and not shown here.)"
                        : "No tags match the filter.",
                    MessageType.None);
                return;
            }

            var collapsed = new HashSet<string>(_expanded.Where(kv => !kv.Value).Select(kv => kv.Key));
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
            const float regW   = 84f;

            var rowRect = GUILayoutUtility.GetRect(0, EditorGUIUtility.singleLineHeight + 2, GUILayout.ExpandWidth(true));
            float x      = rowRect.x + entry.Depth * indent;
            float y      = rowRect.y + 1;
            float h      = EditorGUIUtility.singleLineHeight;
            float availW = rowRect.xMax - x;

            if (entry.HasChildren)
            {
                bool isExpanded  = _expanded.GetValueOrDefault(entry.FullPath, true);
                bool nowExpanded = EditorGUI.Foldout(new Rect(x, y, foldW, h), isExpanded, GUIContent.none);
                if (nowExpanded != isExpanded) { _expanded[entry.FullPath] = nowExpanded; Repaint(); }
            }
            x += foldW; availW -= foldW;

            // Per-explicit-tag "Registered" toggle (moves the tag between groups). Virtual parents have none.
            if (!entry.IsVirtual)
            {
                var toggleRect = new Rect(rowRect.xMax - regW, y, regW, h);
                bool nowReg = GUI.Toggle(toggleRect, entry.Registered, "Registered", EditorStyles.miniButton);
                if (nowReg != entry.Registered) SetRegistered(entry.FullPath, nowReg);
                availW -= regW + 4;
            }

            var labelRect = new Rect(x, y, Mathf.Max(0, availW), h);
            EditorGUI.LabelField(labelRect, entry.Segment, entry.IsVirtual ? VirtualStyle : EditorStyles.label);

            if (!entry.IsVirtual &&
                Event.current.type == EventType.MouseDown &&
                labelRect.Contains(Event.current.mousePosition))
            {
                Event.current.Use();
                var capturedPath = entry.FullPath;
                GameplayTagEditorWindow.Open(
                    capturedPath,
                    newPath => { if (newPath != capturedPath) CommitRename(capturedPath, newPath); },
                    () => DeleteTag(capturedPath));
            }
        }

        // ── Contributed tags (read-only provenance) ──────────────────────────

        private void DrawContributedSources()
        {
            EnsureData();
            if ((_contributed == null || _contributed.Count == 0) && (_unattributed == null || _unattributed.Count == 0))
                return;

            EditorGUILayout.Space(8);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar, GUILayout.ExpandWidth(true)))
                EditorGUILayout.LabelField("Tags From Other Sources (read-only)", EditorStyles.whiteLabel,
                    GUILayout.ExpandWidth(true));

            foreach (var (name, tags, registered) in _contributed)
                DrawSourceGroup(name, tags, registered);

            if (_unattributed != null && _unattributed.Count > 0)
                DrawSourceGroup("Other (unattributed)", _unattributed, true);
        }

        private void DrawSourceGroup(string name, List<string> tags, bool registered)
        {
            var shown = string.IsNullOrEmpty(_filter)
                ? tags
                : tags.Where(t => t.Contains(_filter, StringComparison.OrdinalIgnoreCase)).ToList();
            if (shown.Count == 0) return;

            string key    = "src:" + name;
            bool   exp    = _expanded.GetValueOrDefault(key, true);
            string header = $"{name}  ({shown.Count})" + (registered ? "" : "  — draft");
            bool   now    = EditorGUILayout.Foldout(exp, header, true);
            if (now != exp) { _expanded[key] = now; Repaint(); }
            if (!now) return;

            EditorGUI.indentLevel++;
            foreach (var t in shown)
                EditorGUILayout.LabelField(t, registered ? EditorStyles.label : VirtualStyle);
            EditorGUI.indentLevel--;
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

            EnsureData();
            if (!_registered.Contains(trimmed) && !_unregistered.Contains(trimmed))
            {
                (_newTagRegistered ? _settings.RegisteredTags : _settings.UnregisteredTags).Add(trimmed);
                Commit();
            }

            _newTag = "";
            GUI.FocusControl(null);
        }

        private void SetRegistered(string tag, bool registered)
        {
            EnsureData();
            var from = registered ? _settings.UnregisteredTags : _settings.RegisteredTags;
            var to   = registered ? _settings.RegisteredTags   : _settings.UnregisteredTags;
            if (from.Remove(tag) && !to.Contains(tag)) to.Add(tag);
            Commit();
        }

        private void CommitRename(string oldFullPath, string newFullPath)
        {
            newFullPath = newFullPath.Trim();
            if (string.IsNullOrEmpty(newFullPath) || newFullPath == oldFullPath) return;
            if (!GameplayTagRegistry.ValidateTag(newFullPath))
            {
                EditorUtility.DisplayDialog("Invalid Name", $"'{newFullPath}' is not a valid tag path.", "OK");
                return;
            }

            EnsureData();
            RenameInList(_settings.RegisteredTags,   oldFullPath, newFullPath);
            RenameInList(_settings.UnregisteredTags, oldFullPath, newFullPath);
            Commit();
        }

        private void DeleteTag(string fullPath)
        {
            EnsureData();
            _settings.RegisteredTags.RemoveAll(t => t == fullPath || t.StartsWith(fullPath + "."));
            _settings.UnregisteredTags.RemoveAll(t => t == fullPath || t.StartsWith(fullPath + "."));
            Commit();
        }

        private static void RenameInList(List<string> tags, string oldFullPath, string newFullPath)
        {
            for (int i = 0; i < tags.Count; i++)
            {
                if (tags[i] == oldFullPath) tags[i] = newFullPath;
                else if (tags[i].StartsWith(oldFullPath + ".")) tags[i] = newFullPath + tags[i].Substring(oldFullPath.Length);
            }
        }

        // Persist + re-register so the live registry, validation and pickers reflect the edit immediately.
        private void Commit()
        {
            _settings.Save();
            GameplayTagsEditorRegistrar.Refresh();
            InvalidateAll();
        }
    }
}
