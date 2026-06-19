using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Heathen.GameplayTags.Editor
{
    /// <summary>
    /// Shared compilation of a <c>.gptags</c> source into the baked tag forest (one
    /// <see cref="CompiledTagEntry"/> per node: Id / Name / ParentId). Single source of truth used by
    /// BOTH the <see cref="GameplayTagsImporter"/> (legacy SO output, being retired) and the
    /// <see cref="GameplayTagsCodeGenerator"/> (the baked-C# output). See GameplayTags-CodeGen-Spec.
    /// </summary>
    public static class GameplayTagsCompiler
    {
        /// <summary>Parsed <c>.gptags</c> JSON: <c>{ "registered": bool, "tags": [ "A.B.C", … ] }</c>.</summary>
        public static void ParseSource(string json, out bool registered, out string[] tags)
        {
            var root = JObject.Parse(json);
            registered = root["registered"]?.Value<bool>() ?? false;
            tags = root["tags"]?.ToObject<string[]>() ?? Array.Empty<string>();
        }

        /// <summary>
        /// Builds one entry per node — every dot-path prefix — each carrying its immediate parent id.
        /// Ids are the xxHash3 of the path (<see cref="GameplayTagRegistry.Hash"/>); de-duped by id. The
        /// registry derives interval (nested-set) encoding from these parent links after merge. Returned
        /// in a stable, parent-before-child order (so generated output is deterministic + diff-friendly).
        /// </summary>
        public static CompiledTagEntry[] BuildEntries(string[] tags)
        {
            // id -> (name, parentId, order). A node reached via multiple tags keeps one entry (identical
            // parent by construction). `order` preserves first-seen sequence for deterministic output.
            var nodes = new Dictionary<ulong, (string name, ulong parent, int order)>();
            int next = 0;

            if (tags != null)
            {
                foreach (var tag in tags)
                {
                    if (string.IsNullOrWhiteSpace(tag)) continue;
                    var parts = tag.Trim().Split('.');
                    var sb = new StringBuilder();
                    ulong parentHash = 0; // root has no parent

                    for (int i = 0; i < parts.Length; i++)
                    {
                        if (i > 0) sb.Append('.');
                        sb.Append(parts[i]);
                        var path = sb.ToString();
                        var hash = GameplayTagRegistry.Hash(path);

                        if (!nodes.ContainsKey(hash))
                            nodes[hash] = (path, parentHash, next++);

                        parentHash = hash; // becomes the parent of the next, deeper segment
                    }
                }
            }

            var entries = new List<CompiledTagEntry>(nodes.Count);
            foreach (var kv in nodes)
                entries.Add(new CompiledTagEntry { Id = kv.Key, Name = kv.Value.name, ParentId = kv.Value.parent });
            // Stable order: shorter paths (parents) first, then lexical — deterministic regardless of dict order.
            entries.Sort((a, b) =>
            {
                int byDepth = Depth(a.Name).CompareTo(Depth(b.Name));
                return byDepth != 0 ? byDepth : string.CompareOrdinal(a.Name, b.Name);
            });
            return entries.ToArray();
        }

        private static int Depth(string path)
        {
            int d = 1;
            for (int i = 0; i < path.Length; i++) if (path[i] == '.') d++;
            return d;
        }
    }
}
