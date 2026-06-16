using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
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
    // The compiled asset stores the tag forest as parent links (one entry per node, with its
    // immediate parent id). GameplayTagRegistry merges these and computes interval encoding —
    // no string parsing at runtime.
    [ScriptedImporter(2, "gptags")]
    public class GameplayTagsImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var compiled = ScriptableObject.CreateInstance<GameplayTagsCompiledData>();

            try
            {
                var json = File.ReadAllText(ctx.assetPath);
                var root = JObject.Parse(json);
                compiled.AutoRegister = root["registered"]?.Value<bool>() ?? false;
                var tags = root["tags"]?.ToObject<string[]>() ?? Array.Empty<string>();
                compiled.Entries = compiled.AutoRegister && tags.Length > 0
                    ? BuildEntries(tags)
                    : Array.Empty<CompiledTagEntry>();

                // A file with tags but no "registered": true compiles to nothing — surface that, it is
                // almost always a mistake rather than an intentionally-disabled tag set.
                if (!compiled.AutoRegister && tags.Length > 0)
                    ctx.LogImportWarning(
                        $"[GameplayTags] '{Path.GetFileNameWithoutExtension(ctx.assetPath)}.gptags' has " +
                        $"{tags.Length} tag(s) but \"registered\" is not true — none will be registered. " +
                        "Set \"registered\": true to activate them.");
            }
            catch (Exception e)
            {
                ctx.LogImportError($"Failed to parse .gptags JSON: {e.Message}");
                compiled.AutoRegister = false;
                compiled.Entries      = Array.Empty<CompiledTagEntry>();
            }

            ctx.AddObjectToAsset("main", compiled);
            ctx.SetMainObject(compiled);

            if (compiled.AutoRegister)
                GameplayTagRegistry.RegisterDefaults(compiled);
        }

        // Builds one entry per node (every dot-path prefix), each carrying its immediate parent id.
        // The registry derives interval encoding from these parent links post-merge.
        private static CompiledTagEntry[] BuildEntries(string[] tags)
        {
            // id -> (name, parentId). Synthesises every prefix node; de-dups by id (a node reached
            // via multiple tags keeps one entry with an identical parent by construction).
            var nodes = new Dictionary<ulong, (string name, ulong parent)>();

            foreach (var tag in tags)
            {
                if (string.IsNullOrWhiteSpace(tag)) continue;
                var trimmed = tag.Trim();

                var parts = trimmed.Split('.');
                var sb    = new StringBuilder();
                ulong parentHash = 0; // root has no parent

                for (int i = 0; i < parts.Length; i++)
                {
                    if (i > 0) sb.Append('.');
                    sb.Append(parts[i]);
                    var path = sb.ToString();
                    var hash = GameplayTagRegistry.Hash(path);

                    if (!nodes.ContainsKey(hash))
                        nodes[hash] = (path, parentHash);

                    parentHash = hash; // becomes the parent of the next, deeper segment
                }
            }

            var entries = new CompiledTagEntry[nodes.Count];
            int idx = 0;
            foreach (var kv in nodes)
            {
                entries[idx++] = new CompiledTagEntry
                {
                    Id       = kv.Key,
                    Name     = kv.Value.name,
                    ParentId = kv.Value.parent,
                };
            }
            return entries;
        }
    }

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
                var path  = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<GameplayTagsCompiledData>(path);
                if (asset != null && asset.AutoRegister)
                    GameplayTagRegistry.RegisterDefaults(asset);
            }

            // Keep the preloaded-assets list in sync so tag sets ship in builds and register at startup.
            GameplayTagsPreload.EnsureTagsPreloaded();
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
                var parent = entry.ParentId == 0 ? "(root)" : GameplayTagRegistry.GetName(entry.ParentId) ?? entry.ParentId.ToString("X16");
                EditorGUILayout.LabelField(entry.Name, $"parent: {parent}", EditorStyles.miniLabel);
            }
            EditorGUI.EndDisabledGroup();
        }
    }
}
