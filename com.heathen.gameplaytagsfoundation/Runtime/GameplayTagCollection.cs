using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine;

namespace Heathen.GameplayTags
{
    [Serializable]
    public class GameplayTagCollection : ISerializationCallbackReceiver
    {
        [Serializable]
        private struct TagEntry { public ulong id; public ulong value; }

        [SerializeField] private List<TagEntry> _serialized = new();

        private Dictionary<ulong, ulong> _map = new();

        // id -> list of (callback, exactMatchOnly)
        private Dictionary<ulong, List<(Action<GameplayTag, ulong, ulong>, bool)>> _subscribers;

        public event Action<GameplayTagCollection> Changed;

        // ── ISerializationCallbackReceiver ───────────────────────────────────

        public void OnBeforeSerialize()
        {
            _serialized.Clear();
            foreach (var kv in _map)
                _serialized.Add(new TagEntry { id = kv.Key, value = kv.Value });
        }

        public void OnAfterDeserialize()
        {
            _map = new Dictionary<ulong, ulong>();
            foreach (var e in _serialized)
                if (e.value > 0)
                    _map[e.id] = e.value;
        }

        // ── Mutation ─────────────────────────────────────────────────────────

        public void AddTag(GameplayTag tag)
        {
            _map.TryGetValue(tag.Id, out var prev);
            var next = prev + 1;
            _map[tag.Id] = next;
            NotifyChange(tag, prev, next);
        }

        public void RemoveTag(GameplayTag tag)
        {
            if (!_map.TryGetValue(tag.Id, out var prev)) return;
            _map.Remove(tag.Id);
            NotifyChange(tag, prev, 0);
        }

        public void Apply(GameplayTag tag, GameplayTagArithmetic arithmetic, ulong value)
        {
            _map.TryGetValue(tag.Id, out var prev);
            var next = arithmetic switch
            {
                GameplayTagArithmetic.Set      => value,
                GameplayTagArithmetic.Add      => prev + value,
                GameplayTagArithmetic.Subtract => prev > value ? prev - value : 0,
                GameplayTagArithmetic.Multiply => prev * value,
                GameplayTagArithmetic.Divide   => value > 0 ? prev / value : prev,
                GameplayTagArithmetic.Min      => Math.Min(prev, value),
                GameplayTagArithmetic.Max      => Math.Max(prev, value),
                _                              => prev,
            };

            if (next == 0)
                _map.Remove(tag.Id);
            else
                _map[tag.Id] = next;

            if (prev != next)
                NotifyChange(tag, prev, next);
        }

        // Convenience overload used by GameplayTagOperation
        public void Apply(GameplayTagOperation op)
        {
            if (op == null) return;
            Apply(op.Tag, op.Arithmetic, op.Value);
        }

        public void Clear()
        {
            var snapshot = new Dictionary<ulong, ulong>(_map);
            _map.Clear();
            foreach (var kv in snapshot)
                NotifyChange(new GameplayTag(kv.Key), kv.Value, 0);
            Changed?.Invoke(this);
        }

        // ── Query ────────────────────────────────────────────────────────────

        public ulong GetValue(GameplayTag tag)
        {
            _map.TryGetValue(tag.Id, out var v);
            return v;
        }

        // ── Typed value accessors ─────────────────────────────────────────────
        // Underlying storage is always ulong. Zero means "not present".
        // float/int occupy the lower 32 bits; long/double use all 64 bits.

        public float  GetFloat (GameplayTag tag) => FloatUnion.ToFloat((uint)(GetValue(tag) & 0xFFFFFFFFUL));
        public int    GetInt   (GameplayTag tag) => (int)(uint)(GetValue(tag) & 0xFFFFFFFFUL);
        public long   GetLong  (GameplayTag tag) => (long)GetValue(tag);
        public double GetDouble(GameplayTag tag) => DoubleUnion.ToDouble(GetValue(tag));

        public void SetFloat (GameplayTag tag, float  value) => Apply(tag, GameplayTagArithmetic.Set, (ulong)FloatUnion.ToUInt(value));
        public void SetInt   (GameplayTag tag, int    value) => Apply(tag, GameplayTagArithmetic.Set, (ulong)(uint)value);
        public void SetLong  (GameplayTag tag, long   value) => Apply(tag, GameplayTagArithmetic.Set, (ulong)value);
        public void SetDouble(GameplayTag tag, double value) => Apply(tag, GameplayTagArithmetic.Set, DoubleUnion.ToUlong(value));

        // Stores the xxHash64 of tagPath as the value of tag.
        // Useful for enum-like patterns: collection["Player.Class"] = "Classes.Melee.Warrior"
        // Retrieve with: GameplayTag.FromName(GameplayTagRegistry.GetName(collection.GetValue(tag)))
        public void SetTagValue(GameplayTag tag, string tagPath)
        {
            if (!GameplayTagRegistry.ValidateTag(tagPath?.Trim() ?? string.Empty))
                throw new ArgumentException($"'{tagPath}' is not a valid GameplayTag path.", nameof(tagPath));
            Apply(tag, GameplayTagArithmetic.Set, GameplayTagRegistry.Hash(tagPath.Trim()));
        }

        public bool IsEmpty => _map.Count == 0;
        public int Count   => _map.Count;

        // exactMatch=false → tag OR any registered descendant satisfies the check
        public bool Contains(GameplayTag tag, bool exactMatch = false)
        {
            if (_map.ContainsKey(tag.Id)) return true;
            if (exactMatch) return false;
            foreach (var desc in GameplayTagRegistry.GetDescendants(tag.Id))
                if (_map.ContainsKey(desc)) return true;
            return false;
        }

        public bool ContainsAll(GameplayTagCollection other, bool exactMatch = false)
        {
            foreach (var kv in other._map)
                if (!Contains(new GameplayTag(kv.Key), exactMatch)) return false;
            return true;
        }

        public bool ContainsAny(GameplayTagCollection other, bool exactMatch = false)
        {
            foreach (var kv in other._map)
                if (Contains(new GameplayTag(kv.Key), exactMatch)) return true;
            return false;
        }

        public bool ContainsNone(GameplayTagCollection other, bool exactMatch = false)
        {
            foreach (var kv in other._map)
                if (Contains(new GameplayTag(kv.Key), exactMatch)) return false;
            return true;
        }

        public List<GameplayTag> GetMatchingTags(GameplayTag filter, bool exactMatch = false)
        {
            var results = new List<GameplayTag>();
            foreach (var id in _map.Keys)
            {
                var tag = new GameplayTag(id);
                if (tag == filter || (!exactMatch && tag.IsChildOf(filter)))
                    results.Add(tag);
            }
            return results;
        }

        public List<GameplayTag> GetExcludingTags(GameplayTag filter, bool exactMatch = false)
        {
            var results = new List<GameplayTag>();
            foreach (var id in _map.Keys)
            {
                var tag = new GameplayTag(id);
                if (tag != filter && (exactMatch || !tag.IsChildOf(filter)))
                    results.Add(tag);
            }
            return results;
        }

        public GameplayTagCollection GetShared(GameplayTagCollection other, bool exactMatch = false)
        {
            var result = new GameplayTagCollection();
            foreach (var kv in _map)
                if (other.Contains(new GameplayTag(kv.Key), exactMatch))
                    result._map[kv.Key] = kv.Value;
            return result;
        }

        public GameplayTagCollection GetExclusive(GameplayTagCollection other, bool exactMatch = false)
        {
            var result = new GameplayTagCollection();
            foreach (var kv in _map)
                if (!other.Contains(new GameplayTag(kv.Key), exactMatch))
                    result._map[kv.Key] = kv.Value;
            return result;
        }

        public IEnumerable<(GameplayTag tag, ulong value)> GetAll()
        {
            foreach (var kv in _map)
                yield return (new GameplayTag(kv.Key), kv.Value);
        }

        // ── Events ───────────────────────────────────────────────────────────

        public void Subscribe(GameplayTag tag, Action<GameplayTag, ulong, ulong> callback, bool exactMatch = false)
        {
            _subscribers ??= new Dictionary<ulong, List<(Action<GameplayTag, ulong, ulong>, bool)>>();
            if (!_subscribers.TryGetValue(tag.Id, out var list))
            {
                list = new List<(Action<GameplayTag, ulong, ulong>, bool)>();
                _subscribers[tag.Id] = list;
            }
            list.Add((callback, exactMatch));
        }

        public void Unsubscribe(GameplayTag tag, Action<GameplayTag, ulong, ulong> callback)
        {
            if (_subscribers == null) return;
            if (!_subscribers.TryGetValue(tag.Id, out var list)) return;
            for (int i = list.Count - 1; i >= 0; i--)
                if (list[i].Item1 == callback) list.RemoveAt(i);
        }

        // ── Burst / Jobs ─────────────────────────────────────────────────────

        public NativeHashMap<ulong, ulong> GetSnapshot(Allocator allocator)
        {
            var map = new NativeHashMap<ulong, ulong>(_map.Count, allocator);
            foreach (var kv in _map)
                map.TryAdd(kv.Key, kv.Value);
            return map;
        }

        // ── Internal ─────────────────────────────────────────────────────────

        private void NotifyChange(GameplayTag tag, ulong prev, ulong next)
        {
            Changed?.Invoke(this);

            if (_subscribers == null) return;

            // Direct subscribers on this exact tag
            FireSubscribers(tag.Id, tag, prev, next, descendantNotification: false);

            // Ancestor subscribers — only non-exact ones (opted into descendant events)
            foreach (var kv in _subscribers)
            {
                if (kv.Key == tag.Id) continue;
                if (GameplayTagRegistry.IsAncestor(kv.Key, tag.Id))
                    FireSubscribers(kv.Key, tag, prev, next, descendantNotification: true);
            }
        }

        private void FireSubscribers(ulong subscribedId, GameplayTag changedTag, ulong prev, ulong next, bool descendantNotification)
        {
            if (!_subscribers.TryGetValue(subscribedId, out var list)) return;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var (cb, isExact) = list[i];
                if (descendantNotification && isExact) continue;
                cb?.Invoke(changedTag, prev, next);
            }
        }

        // ── Bit-reinterpretation helpers (no unsafe required) ─────────────────

        [StructLayout(LayoutKind.Explicit)]
        private struct FloatUnion
        {
            [FieldOffset(0)] private float _f;
            [FieldOffset(0)] private uint  _u;
            public static uint  ToUInt (float f) => new FloatUnion { _f = f }._u;
            public static float ToFloat(uint  u) => new FloatUnion { _u = u }._f;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct DoubleUnion
        {
            [FieldOffset(0)] private double _f;
            [FieldOffset(0)] private ulong  _u;
            public static ulong  ToUlong (double f) => new DoubleUnion { _f = f }._u;
            public static double ToDouble(ulong  u) => new DoubleUnion { _u = u }._f;
        }
    }
}
