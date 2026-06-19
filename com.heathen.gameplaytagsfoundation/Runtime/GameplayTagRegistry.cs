using System;
using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Heathen.GameplayTags
{
    /// <summary>
    /// Holds the registered tag forest and its interval (nested-set) encoding. Hierarchy is stored
    /// as parent links (id -> parentId) and compiled to per-tag <see cref="GameplayTagInterval"/>
    /// pairs by a depth-first pass, so containment queries are O(1) range comparisons rather than
    /// set lookups, and memory is O(N) rather than the transitive closure.
    /// </summary>
    public static class GameplayTagRegistry
    {
        // Authoritative forest (working set): id -> immediate parentId (0 = root).
        private static readonly Dictionary<ulong, ulong> _parent = new();
        // Baked defaults, captured so an enter-play-mode-without-domain-reload reset can restore a
        // clean base before runtime-registered tags are re-applied.
        private static readonly Dictionary<ulong, ulong> _defaultParent = new();
        private static readonly Dictionary<ulong, string> _defaultNames = new();

        private static readonly Dictionary<ulong, string> _nameMap = new();

        // Interval encoding, recomputed by RebuildIntervals whenever the registered set changes.
        private static readonly Dictionary<ulong, GameplayTagInterval> _interval = new();

        /// <summary>
        /// Increments every time the interval encoding is rebuilt. Consumers that cache interval
        /// data (e.g. a DataLens projection or denormalised columns) compare against this to detect
        /// staleness after a runtime tag registration.
        /// </summary>
        public static ulong IntervalGeneration { get; private set; }

        public static event Action RegistryChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Init()
        {
            // Reset for a clean session, including under "Enter Play Mode without Domain Reload" where the
            // event delegate would otherwise retain stale subscribers from the previous session.
            RegistryChanged = null;
            _parent.Clear();
            _nameMap.Clear();
            _interval.Clear();
            IntervalGeneration = 0;

            // Restore the baked default base.
            foreach (var kv in _defaultParent) _parent[kv.Key] = kv.Value;
            foreach (var kv in _defaultNames)  _nameMap[kv.Key] = kv.Value;

            // Register every loaded compiled tag asset, not just those under a Resources folder.
            // FindObjectsOfTypeAll also returns PlayerSettings-preloaded assets (how tag sets ship) and
            // already-loaded scene assets; anything loading later self-registers via its own OnEnable.
            var compiled = Resources.FindObjectsOfTypeAll<GameplayTagsCompiledData>();
            foreach (var asset in compiled)
                if (asset != null && asset.AutoRegister)
                    MergeCompiledData(asset, _parent);

            RebuildIntervals();
            RegistryChanged?.Invoke();
        }

        public static void RegisterDefaults(GameplayTagsCompiledData data)
        {
            if (data?.Entries == null) return;
            RegisterBaked(data.Entries);
        }

        /// <summary>
        /// Register baked tag entries (Id / ParentId / Name) directly from generated C# — the SO-free
        /// registration path the GameplayTags code generator targets (see GameplayTags-CodeGen-Spec). Baked
        /// tags form the default base (restored on an enter-play-mode-without-domain-reload reset). Ids are
        /// the xxHash3 of the dot-path (<see cref="Hash"/>), baked by the generator; this method does no
        /// hashing or parsing — pure data ingest + a single interval rebuild over the merged forest.
        /// </summary>
        public static void RegisterBaked(CompiledTagEntry[] entries)
        {
            if (entries == null) return;
            foreach (var e in entries)
            {
                _defaultParent[e.Id] = e.ParentId;
                _parent[e.Id]        = e.ParentId;
                if (!string.IsNullOrEmpty(e.Name))
                {
                    _defaultNames[e.Id] = e.Name;
                    _nameMap[e.Id]      = e.Name;
                }
            }
            RebuildIntervals();
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
            MergeCompiledData(data, _parent);
            RebuildIntervals();
            RegistryChanged?.Invoke();
        }

        // Register a single tag path at runtime — for UGC/mod tags not baked into a .gptags asset.
        // Forces an interval rebuild (the accepted, rare cost of runtime registration).
        public static void Register(string dotPath)
        {
            if (string.IsNullOrWhiteSpace(dotPath)) return;
            RegisterHierarchy(dotPath, _parent);
            RebuildIntervals();
            RegistryChanged?.Invoke();
        }

        private static void MergeCompiledData(GameplayTagsCompiledData data, Dictionary<ulong, ulong> targetParent)
        {
            if (data?.Entries == null) return;
            foreach (var entry in data.Entries)
            {
                // Parent links are identical across assets by construction, so a plain assign de-dups.
                targetParent[entry.Id] = entry.ParentId;
                if (!string.IsNullOrEmpty(entry.Name))
                    _nameMap[entry.Id] = entry.Name;
            }
        }

        // Synthesises every prefix node of a dot-path and records its immediate parent link.
        private static void RegisterHierarchy(string dotPath, Dictionary<ulong, ulong> target)
        {
            var parts = dotPath.Split('.');
            var sb    = new StringBuilder();
            ulong parentHash = 0;

            for (int i = 0; i < parts.Length; i++)
            {
                if (i > 0) sb.Append('.');
                sb.Append(parts[i]);
                var fullPath = sb.ToString();
                var h        = Hash(fullPath);

                _nameMap[h] = fullPath;
                if (!target.ContainsKey(h))
                    target[h] = parentHash;

                parentHash = h;
            }
        }

        /// <summary>
        /// Recomputes the interval (nested-set) encoding for every registered tag with a single
        /// depth-first pass over the forest. O(N). Child order is sorted by id so numbering is
        /// deterministic across runs and machines.
        /// </summary>
        private static void RebuildIntervals()
        {
            _interval.Clear();

            // Build child lists and identify roots (parent == 0, or parent not registered = orphan).
            var children = new Dictionary<ulong, List<ulong>>();
            var roots    = new List<ulong>();
            foreach (var kv in _parent)
            {
                ulong id = kv.Key, parent = kv.Value;
                if (parent != 0 && _parent.ContainsKey(parent))
                {
                    if (!children.TryGetValue(parent, out var list))
                    {
                        list = new List<ulong>();
                        children[parent] = list;
                    }
                    list.Add(id);
                }
                else
                {
                    roots.Add(id);
                }
            }

            // Deterministic ordering.
            roots.Sort();
            foreach (var list in children.Values) list.Sort();

            // Iterative DFS (explicit stack avoids recursion depth issues on degenerate trees).
            uint counter = 0;
            var stack = new Stack<(ulong id, byte depth, bool exit)>();
            for (int i = roots.Count - 1; i >= 0; i--)
                stack.Push((roots[i], 0, false));

            while (stack.Count > 0)
            {
                var (id, depth, exit) = stack.Pop();
                if (exit)
                {
                    var iv = _interval[id];
                    iv.Rgt = counter++;
                    _interval[id] = iv;
                    continue;
                }

                _interval[id] = new GameplayTagInterval { Lft = counter++, Rgt = 0, Depth = depth, ScopeId = 0 };

                // Schedule the exit visit after this node's subtree, then push children (reversed so
                // the smallest id is processed first, matching the sorted order).
                stack.Push((id, depth, true));
                if (children.TryGetValue(id, out var kids))
                    for (int i = kids.Count - 1; i >= 0; i--)
                        stack.Push((kids[i], (byte)(depth + 1), false));
            }

            IntervalGeneration++;
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

        /// <summary>
        /// Returns <c>true</c> if <paramref name="candidateId"/> is a descendant of
        /// <paramref name="ancestorId"/> in the registered hierarchy. A tag is not a descendant of
        /// itself. Unregistered (Tier 0) tags are never in a hierarchy, so this returns <c>false</c>
        /// for them.
        /// </summary>
        public static bool IsAncestor(ulong ancestorId, ulong candidateId)
        {
            if (_interval.TryGetValue(ancestorId, out var a) && _interval.TryGetValue(candidateId, out var c))
                return a.Lft < c.Lft && c.Lft <= a.Rgt;
            return false;
        }

        /// <summary>Returns the interval encoding for a registered tag.</summary>
        /// <returns><c>true</c> if the tag is registered; otherwise <c>false</c> and a default interval.</returns>
        public static bool TryGetInterval(ulong id, out GameplayTagInterval interval) =>
            _interval.TryGetValue(id, out interval);

        public static bool IsRegistered(ulong id) => _interval.ContainsKey(id);

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

        /// <summary>
        /// Enumerates all registered descendants of <paramref name="ancestorId"/> (every depth).
        /// Implemented as a range scan over the interval table, so it is O(registered tags); prefer
        /// <see cref="IsAncestor"/> for membership tests, which are O(1).
        /// </summary>
        public static IEnumerable<ulong> GetDescendants(ulong ancestorId)
        {
            if (!_interval.TryGetValue(ancestorId, out var a))
                yield break;
            foreach (var kv in _interval)
            {
                if (kv.Key == ancestorId) continue;
                if (a.Lft < kv.Value.Lft && kv.Value.Lft <= a.Rgt)
                    yield return kv.Key;
            }
        }

        // Returns a Burst-safe copy; caller owns and must Dispose.
        public static NativeHashMap<ulong, bool> GetRegisteredIds(Allocator allocator)
        {
            var map = new NativeHashMap<ulong, bool>(_nameMap.Count, allocator);
            foreach (var id in _nameMap.Keys)
                map.TryAdd(id, true);
            return map;
        }

        /// <summary>
        /// Returns a Burst-readable interval snapshot for data-side hierarchy tests (range compares).
        /// Caller owns the result and must Dispose it.
        /// </summary>
        public static NativeIntervalMap GetNativeIntervalMap(Allocator allocator)
        {
            var map = new NativeHashMap<ulong, GameplayTagInterval>(_interval.Count, allocator);
            foreach (var kv in _interval)
                map.TryAdd(kv.Key, kv.Value);
            return new NativeIntervalMap { Intervals = map, Generation = IntervalGeneration };
        }

        // Returns a Burst-safe CSR descendants map; caller owns and must Dispose.
        [Obsolete("Descendant closure superseded by interval encoding. Use GetNativeIntervalMap and range tests (NativeIntervalMap.IsAncestor).")]
        public static NativeDescendantsMap GetNativeDescendantsMap(Allocator allocator)
        {
            // Derived from intervals via range scan; retained for back-compat only.
            var descendants = new Dictionary<ulong, List<ulong>>();
            int total = 0;
            foreach (var anc in _interval)
            {
                var list = new List<ulong>();
                foreach (var cand in _interval)
                    if (cand.Key != anc.Key && anc.Value.Lft < cand.Value.Lft && cand.Value.Lft <= anc.Value.Rgt)
                        list.Add(cand.Key);
                descendants[anc.Key] = list;
                total += list.Count;
            }

            var flat  = new NativeArray<ulong>(total, allocator);
            var index = new NativeHashMap<ulong, int2>(descendants.Count, allocator);

            int offset = 0;
            foreach (var kv in descendants)
            {
                index.TryAdd(kv.Key, new int2(offset, kv.Value.Count));
                foreach (var desc in kv.Value)
                    flat[offset++] = desc;
            }

            return new NativeDescendantsMap { FlatDescendants = flat, Index = index };
        }
    }
}
