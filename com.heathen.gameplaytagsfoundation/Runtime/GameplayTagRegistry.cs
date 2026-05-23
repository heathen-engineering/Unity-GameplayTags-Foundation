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
        private static readonly Dictionary<ulong, HashSet<ulong>> _runtime = new();
        private static readonly Dictionary<ulong, string> _nameMap = new();

        public static event Action RegistryChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Init()
        {
            _runtime.Clear();
            _nameMap.Clear();
            foreach (var kv in _defaults)
                _runtime[kv.Key] = new HashSet<ulong>(kv.Value);

            var assets = Resources.LoadAll<GameplayTagsData>("");
            foreach (var asset in assets)
                if (asset.autoRegister)
                    MergeData(asset, _runtime);

            RegistryChanged?.Invoke();
        }

        // Called from [InitializeOnLoad] in Editor and from explicit Register calls.
        public static void RegisterDefaults(GameplayTagsData data)
        {
            MergeData(data, _defaults);
            // Mirror into runtime dict if already initialized
            MergeData(data, _runtime);
            RegistryChanged?.Invoke();
        }

        public static void UnregisterDefaults(GameplayTagsData data)
        {
            // Full rebuild from remaining defaults is safest
            _defaults.Clear();
            _nameMap.Clear();
            _runtime.Clear();
            // Caller must re-register remaining assets
            RegistryChanged?.Invoke();
        }

        // Register a single tag path at runtime. Use for UGC/mod tags not in a GameplayTagsData asset.
        public static void Register(string dotPath)
        {
            if (string.IsNullOrWhiteSpace(dotPath)) return;
            RegisterHierarchy(dotPath, _runtime);
            RegistryChanged?.Invoke();
        }

        private static void MergeData(GameplayTagsData data, Dictionary<ulong, HashSet<ulong>> target)
        {
            if (data == null || data.tags == null) return;
            foreach (var tagDef in data.tags)
            {
                if (string.IsNullOrWhiteSpace(tagDef)) continue;
                RegisterHierarchy(tagDef, target);
            }
        }

        private static void RegisterHierarchy(string dotPath, Dictionary<ulong, HashSet<ulong>> target)
        {
            var parts = dotPath.Split('.');
            var pathBuilder = new StringBuilder();
            ulong[] hashes = new ulong[parts.Length];

            for (int i = 0; i < parts.Length; i++)
            {
                if (i > 0) pathBuilder.Append('.');
                pathBuilder.Append(parts[i]);
                var fullPath = pathBuilder.ToString();
                var h = Hash(fullPath);
                hashes[i] = h;
                _nameMap[h] = fullPath;

                // Ensure node exists in target
                if (!target.ContainsKey(h))
                    target[h] = new HashSet<ulong>();
            }

            // Register each node as a descendant of all its ancestors
            for (int ancestor = 0; ancestor < parts.Length - 1; ancestor++)
            {
                for (int descendant = ancestor + 1; descendant < parts.Length; descendant++)
                {
                    if (!target.TryGetValue(hashes[ancestor], out var set))
                    {
                        set = new HashSet<ulong>();
                        target[hashes[ancestor]] = set;
                    }
                    set.Add(hashes[descendant]);
                }
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

        // Validates dot-path format: [A-Za-z0-9_] segments separated by '.', no empty segments.
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

        public static IEnumerable<ulong> GetAllIds() => _nameMap.Keys;

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
            // Flatten all descendants into a single array
            int total = 0;
            foreach (var kv in _runtime)
                total += kv.Value.Count;

            var flat = new NativeArray<ulong>(total, allocator);
            var index = new NativeHashMap<ulong, int2>(_runtime.Count, allocator);

            int offset = 0;
            foreach (var kv in _runtime)
            {
                int count = kv.Value.Count;
                int2 range = new int2(offset, count);
                index.TryAdd(kv.Key, range);
                foreach (var desc in kv.Value)
                    flat[offset++] = desc;
            }

            return new NativeDescendantsMap
            {
                FlatDescendants = flat,
                Index = index,
            };
        }
    }
}
