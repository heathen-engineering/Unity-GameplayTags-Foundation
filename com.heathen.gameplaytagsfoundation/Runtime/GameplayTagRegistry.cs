using System;
using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Heathen.GameplayTags
{
    public static class GameplayTagRegistry
    {
        // ancestor id -> set of all descendant ids (all depths)
        private static readonly Dictionary<ulong, HashSet<ulong>> _defaults = new();
        private static readonly Dictionary<ulong, HashSet<ulong>> _runtime  = new();
        private static readonly Dictionary<ulong, string>         _nameMap  = new();

        public static event Action RegistryChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Init()
        {
            // Reset for a clean session, including under "Enter Play Mode without Domain Reload" where the
            // event delegate would otherwise retain stale subscribers from the previous session.
            RegistryChanged = null;
            _runtime.Clear();
            _nameMap.Clear();
            foreach (var kv in _defaults)
                _runtime[kv.Key] = new HashSet<ulong>(kv.Value);

            // Register every loaded compiled tag asset, not just those under a Resources folder.
            // FindObjectsOfTypeAll also returns PlayerSettings-preloaded assets (how tag sets ship) and
            // already-loaded scene assets; anything loading later self-registers via its own OnEnable.
            var compiled = Resources.FindObjectsOfTypeAll<GameplayTagsCompiledData>();
            foreach (var asset in compiled)
                if (asset != null && asset.AutoRegister)
                    MergeCompiledData(asset, _runtime);

            RegistryChanged?.Invoke();
        }

        public static void RegisterDefaults(GameplayTagsCompiledData data)
        {
            MergeCompiledData(data, _defaults);
            MergeCompiledData(data, _runtime);
            RegistryChanged?.Invoke();
        }

        /// <summary>
        /// Merges a compiled tag asset into the live registry. Called automatically by
        /// <see cref="GameplayTagsCompiledData"/> on load so a tag set registers wherever and whenever it
        /// loads (including PlayerSettings-preloaded assets that load after <see cref="Init"/>).
        /// </summary>
        /// <param name="data">The compiled tag asset to register. <c>null</c> is ignored.</param>
        public static void Register(GameplayTagsCompiledData data)
        {
            if (data?.Entries == null) return;
            MergeCompiledData(data, _runtime);
            RegistryChanged?.Invoke();
        }

        // Register a single tag path at runtime — for UGC/mod tags not baked into a .gptags asset.
        public static void Register(string dotPath)
        {
            if (string.IsNullOrWhiteSpace(dotPath)) return;
            RegisterHierarchy(dotPath, _runtime);
            RegistryChanged?.Invoke();
        }

        private static void MergeCompiledData(GameplayTagsCompiledData data, Dictionary<ulong, HashSet<ulong>> target)
        {
            if (data?.Entries == null) return;
            foreach (var entry in data.Entries)
            {
                if (!target.TryGetValue(entry.Id, out var set))
                {
                    set = new HashSet<ulong>(entry.Descendants ?? Array.Empty<ulong>());
                    target[entry.Id] = set;
                }
                else if (entry.Descendants != null)
                {
                    foreach (var d in entry.Descendants)
                        set.Add(d);
                }
                if (!string.IsNullOrEmpty(entry.Name))
                    _nameMap[entry.Id] = entry.Name;
            }
        }

        private static void RegisterHierarchy(string dotPath, Dictionary<ulong, HashSet<ulong>> target)
        {
            var parts = dotPath.Split('.');
            var sb    = new StringBuilder();
            var hashes = new ulong[parts.Length];

            for (int i = 0; i < parts.Length; i++)
            {
                if (i > 0) sb.Append('.');
                sb.Append(parts[i]);
                var fullPath = sb.ToString();
                var h        = Hash(fullPath);
                hashes[i]    = h;
                _nameMap[h]  = fullPath;

                if (!target.ContainsKey(h))
                    target[h] = new HashSet<ulong>();
            }

            for (int ancestor = 0; ancestor < parts.Length - 1; ancestor++)
                for (int descendant = ancestor + 1; descendant < parts.Length; descendant++)
                {
                    if (!target.TryGetValue(hashes[ancestor], out var set))
                    { set = new HashSet<ulong>(); target[hashes[ancestor]] = set; }
                    set.Add(hashes[descendant]);
                }
        }

        public static ulong Hash(string dotPath)
        {
            if (string.IsNullOrEmpty(dotPath)) return 0;
            return GameplayTag.HashPath(dotPath);
        }

        public static string GetName(ulong id)
        {
            _nameMap.TryGetValue(id, out var name);
            return name;
        }

        public static bool IsAncestor(ulong ancestorId, ulong candidateId)
        {
            if (_runtime.TryGetValue(ancestorId, out var set))
                return set.Contains(candidateId);
            return false;
        }

        public static bool IsRegistered(ulong id) => _nameMap.ContainsKey(id);

        // Validates dot-path format: alphanumeric/underscore segments separated by '.', no empty segments.
        public static bool ValidateTag(string dotPath)
        {
            if (string.IsNullOrWhiteSpace(dotPath)) return false;
            if (dotPath[0] == '.' || dotPath[^1] == '.') return false;
            var parts = dotPath.Split('.');
            foreach (var part in parts)
            {
                if (string.IsNullOrEmpty(part)) return false;
                foreach (var c in part)
                    if (!char.IsLetterOrDigit(c) && c != '_') return false;
            }
            return true;
        }

        public static IEnumerable<ulong>  GetAllIds()   => _nameMap.Keys;
        public static IEnumerable<string> GetAllNames() => _nameMap.Values;

        public static IEnumerable<ulong> GetDescendants(ulong ancestorId)
        {
            if (_runtime.TryGetValue(ancestorId, out var set))
                return set;
            return Array.Empty<ulong>();
        }

        // Returns a Burst-safe copy; caller owns and must Dispose.
        public static NativeHashMap<ulong, bool> GetRegisteredIds(Allocator allocator)
        {
            var map = new NativeHashMap<ulong, bool>(_nameMap.Count, allocator);
            foreach (var id in _nameMap.Keys)
                map.TryAdd(id, true);
            return map;
        }

        // Returns a Burst-safe CSR descendants map; caller owns and must Dispose.
        public static NativeDescendantsMap GetNativeDescendantsMap(Allocator allocator)
        {
            int total = 0;
            foreach (var kv in _runtime)
                total += kv.Value.Count;

            var flat  = new NativeArray<ulong>(total, allocator);
            var index = new NativeHashMap<ulong, int2>(_runtime.Count, allocator);

            int offset = 0;
            foreach (var kv in _runtime)
            {
                int count  = kv.Value.Count;
                index.TryAdd(kv.Key, new int2(offset, count));
                foreach (var desc in kv.Value)
                    flat[offset++] = desc;
            }

            return new NativeDescendantsMap { FlatDescendants = flat, Index = index };
        }
    }
}
