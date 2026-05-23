using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Heathen.GameplayTags.Editor
{
    // Compiles .gptags JSON source files into GameplayTagsCompiledData sub-assets.
    //
    // Source format (.gptags):
    // {
    //   "registered": true,
    //   "tags": [
    //     "Category.Group.Label",
    //     "Category.Group.Other"
    //   ]
    // }
    //
    // "registered": false (or absent) produces an empty non-registering asset.
    // Unity tracks the file either way so reimport triggers automatically on change.
    //
    // The compiled asset stores the pre-built descendants map as flat arrays.
    // GameplayTagRegistry.Init() merges them directly — no string parsing at runtime.
    [ScriptedImporter(1, "gptags")]
    public class GameplayTagsImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            GptagsSource source = null;
            try
            {
                var json = File.ReadAllText(ctx.assetPath);
                source = JsonUtility.FromJson<GptagsSource>(json);
            }
            catch (Exception e)
            {
                ctx.LogImportError($"Failed to parse .gptags JSON: {e.Message}");
            }

            var compiled = CreateInstance<GameplayTagsCompiledData>();
            compiled.AutoRegister = source?.registered ?? false;

            if (compiled.AutoRegister && source?.tags is { Length: > 0 })
            {
                compiled.Entries = BuildEntries(source.tags);
            }
            else
            {
                compiled.Entries = Array.Empty<CompiledTagEntry>();
            }

            ctx.AddObjectToAsset("main", compiled);
            ctx.SetMainObject(compiled);

            // Immediately merge into the editor-time registry so tag pickers
            // reflect the new data without a manual refresh.
            if (compiled.AutoRegister)
                GameplayTagRegistry.RegisterDefaults(compiled);
        }

        // Mirrors O3DE's BuildDescendantsMap: each prefix node maps to all deeper nodes.
        private static CompiledTagEntry[] BuildEntries(string[] tags)
        {
            var hierarchy = new Dictionary<ulong, HashSet<ulong>>();
            var nameMap   = new Dictionary<ulong, string>();

            foreach (var tag in tags)
            {
                if (string.IsNullOrWhiteSpace(tag)) continue;
                var trimmed = tag.Trim();

                var parts  = trimmed.Split('.');
                var sb     = new StringBuilder();
                var hashes = new ulong[parts.Length];

                for (int i = 0; i < parts.Length; i++)
                {
                    if (i > 0) sb.Append('.');
                    sb.Append(parts[i]);
                    var path = sb.ToString();
                    var hash = GameplayTagRegistry.Hash(path);
                    hashes[i]    = hash;
                    nameMap[hash] = path;
                    if (!hierarchy.ContainsKey(hash))
                        hierarchy[hash] = new HashSet<ulong>();
                }

                for (int ancestor = 0; ancestor < parts.Length - 1; ancestor++)
                    for (int descendant = ancestor + 1; descendant < parts.Length; descendant++)
                        hierarchy[hashes[ancestor]].Add(hashes[descendant]);
            }

            var entries = new CompiledTagEntry[hierarchy.Count];
            int idx = 0;
            foreach (var kv in hierarchy)
            {
                var children = new ulong[kv.Value.Count];
                kv.Value.CopyTo(children);
                entries[idx++] = new CompiledTagEntry
                {
                    Id          = kv.Key,
                    Name        = nameMap.TryGetValue(kv.Key, out var n) ? n : string.Empty,
                    Descendants = children,
                };
            }
            return entries;
        }

        [Serializable]
        private class GptagsSource
        {
            public bool     registered = false;
            public string[] tags       = Array.Empty<string>();
        }
    }

    // Refreshes the editor-time registry from all compiled .gptags assets on load
    // and after any asset import. Mirrors GameplayTagsDataEditor's [InitializeOnLoad] pattern.
    [InitializeOnLoad]
    internal static class GameplayTagsCompiledDataRefresh
    {
        static GameplayTagsCompiledDataRefresh()
        {
            EditorApplication.delayCall += Refresh;
        }

        internal static void Refresh()
        {
            var guids = AssetDatabase.FindAssets("t:GameplayTagsCompiledData");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<GameplayTagsCompiledData>(path);
                if (asset != null && asset.AutoRegister)
                    GameplayTagRegistry.RegisterDefaults(asset);
            }
        }
    }

    [CustomEditor(typeof(GameplayTagsCompiledData))]
    internal class GameplayTagsCompiledDataEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var data = (GameplayTagsCompiledData)target;

            EditorGUILayout.LabelField("Source", EditorStyles.boldLabel);
            var path = AssetDatabase.GetAssetPath(data);
            EditorGUILayout.LabelField("Compiled from", path, EditorStyles.miniLabel);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.Toggle("Auto Register", data.AutoRegister);
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Compiled Hierarchy", EditorStyles.boldLabel);

            if (data.Entries == null || data.Entries.Length == 0)
            {
                EditorGUILayout.HelpBox("No entries — set \"registered\": true in the .gptags source.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField($"{data.Entries.Length} nodes", EditorStyles.miniLabel);
            EditorGUI.BeginDisabledGroup(true);
            foreach (var entry in data.Entries)
            {
                EditorGUILayout.LabelField(
                    entry.Name,
                    $"{entry.Descendants?.Length ?? 0} descendants",
                    EditorStyles.miniLabel);
            }
            EditorGUI.EndDisabledGroup();
        }
    }
}
