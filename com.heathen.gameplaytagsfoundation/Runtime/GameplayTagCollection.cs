using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine;

namespace Heathen.GameplayTags
{
    /// <summary>
    /// A serialisable, observable dictionary that maps <see cref="GameplayTag"/> identifiers to
    /// 64-bit numeric values. Each tag entry records how many times the tag has been applied (or
    /// an arbitrary typed value set via the typed accessors). Supports hierarchical queries,
    /// per-tag change subscriptions, and Burst-compatible snapshots.
    /// </summary>
    [Serializable]
    public class GameplayTagCollection : ISerializationCallbackReceiver
    {
        [Serializable]
        private struct TagEntry { public ulong id; public ulong value; }

        [SerializeField] private List<TagEntry> _serialized = new();

        private Dictionary<ulong, ulong> _map = new();

        // id -> list of (callback, exactMatchOnly)
        private Dictionary<ulong, List<(Action<GameplayTag, ulong, ulong>, bool)>> _subscribers;

        /// <summary>
        /// Raised whenever any tag value in this collection changes, including additions,
        /// removals, and arithmetic mutations. Also raised by <see cref="Clear"/>.
        /// </summary>
        public event Action<GameplayTagCollection> Changed;

        // ── ISerializationCallbackReceiver ───────────────────────────────────

        /// <summary>
        /// Copies the runtime dictionary into the serialisable list before Unity serialises
        /// this object. Part of the <see cref="ISerializationCallbackReceiver"/> contract.
        /// </summary>
        public void OnBeforeSerialize()
        {
            _serialized.Clear();
            foreach (var kv in _map)
                _serialized.Add(new TagEntry { id = kv.Key, value = kv.Value });
        }

        /// <summary>
        /// Rebuilds the runtime dictionary from the serialised list after Unity deserialises
        /// this object. Entries with a value of zero are discarded. Part of the
        /// <see cref="ISerializationCallbackReceiver"/> contract.
        /// </summary>
        public void OnAfterDeserialize()
        {
            _map = new Dictionary<ulong, ulong>();
            foreach (var e in _serialized)
                if (e.value > 0)
                    _map[e.id] = e.value;
        }

        // ── Mutation ─────────────────────────────────────────────────────────

        /// <summary>
        /// Increments the value associated with <paramref name="tag"/> by one, adding the tag
        /// to the collection if it is not already present. Fires change notifications.
        /// </summary>
        /// <param name="tag">The tag whose value should be incremented.</param>
        public void AddTag(GameplayTag tag)
        {
            _map.TryGetValue(tag.Id, out var prev);
            var next = prev + 1;
            _map[tag.Id] = next;
            NotifyChange(tag, prev, next);
        }

        /// <summary>
        /// Removes <paramref name="tag"/> from the collection entirely, regardless of its current
        /// count. If the tag is not present, this method is a no-op. Fires change notifications.
        /// </summary>
        /// <param name="tag">The tag to remove.</param>
        public void RemoveTag(GameplayTag tag)
        {
            if (!_map.TryGetValue(tag.Id, out var prev)) return;
            _map.Remove(tag.Id);
            NotifyChange(tag, prev, 0);
        }

        /// <summary>
        /// Applies an arithmetic operation to the value stored under <paramref name="tag"/>,
        /// inserting or removing the entry as needed. If the result is zero the entry is removed.
        /// Fires change notifications only when the value actually changes.
        /// </summary>
        /// <param name="tag">The tag whose value will be mutated.</param>
        /// <param name="arithmetic">The arithmetic operation to perform.</param>
        /// <param name="value">The operand for the arithmetic operation.</param>
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

        /// <summary>
        /// Convenience overload that applies the tag, arithmetic, and value bundled inside
        /// <paramref name="op"/>. If <paramref name="op"/> is <c>null</c> the call is a no-op.
        /// </summary>
        /// <param name="op">The operation describing the tag mutation to perform.</param>
        public void Apply(GameplayTagOperation op)
        {
            if (op == null) return;
            Apply(op.Tag, op.Arithmetic, op.Value);
        }

        /// <summary>
        /// Removes all tags from the collection, firing a change notification for each entry
        /// that was present and then raising <see cref="Changed"/> once for the collection.
        /// </summary>
        public void Clear()
        {
            var snapshot = new Dictionary<ulong, ulong>(_map);
            _map.Clear();
            // Notify per-tag subscribers for each removed entry, but suppress the collection-wide
            // Changed event until the end so listeners re-evaluate once, not once per entry.
            foreach (var kv in snapshot)
                NotifyChange(new GameplayTag(kv.Key), kv.Value, 0, fireChanged: false);
            Changed?.Invoke(this);
        }

        // ── Query ────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the raw 64-bit value stored for <paramref name="tag"/>, or zero if the tag
        /// is not present in the collection.
        /// </summary>
        /// <param name="tag">The tag whose stored value should be retrieved.</param>
        /// <returns>The current numeric value, or zero when the tag is absent.</returns>
        public ulong GetValue(GameplayTag tag)
        {
            _map.TryGetValue(tag.Id, out var v);
            return v;
        }

        // ── Typed value accessors ─────────────────────────────────────────────
        // Underlying storage is always ulong. Zero means "not present".
        // float/int occupy the lower 32 bits; long/double use all 64 bits.

        /// <summary>
        /// Returns the value stored for <paramref name="tag"/> reinterpreted as a single-precision
        /// float from the lower 32 bits of the underlying ulong storage.
        /// </summary>
        /// <param name="tag">The tag to read.</param>
        /// <returns>The stored bits reinterpreted as a <see cref="float"/>.</returns>
        public float  GetFloat (GameplayTag tag) => FloatUnion.ToFloat((uint)(GetValue(tag) & 0xFFFFFFFFUL));

        /// <summary>
        /// Returns the value stored for <paramref name="tag"/> reinterpreted as a signed 32-bit
        /// integer from the lower 32 bits of the underlying ulong storage.
        /// </summary>
        /// <param name="tag">The tag to read.</param>
        /// <returns>The stored lower 32 bits cast to <see cref="int"/>.</returns>
        public int    GetInt   (GameplayTag tag) => (int)(uint)(GetValue(tag) & 0xFFFFFFFFUL);

        /// <summary>
        /// Returns the value stored for <paramref name="tag"/> reinterpreted as a signed 64-bit
        /// integer using the full ulong storage.
        /// </summary>
        /// <param name="tag">The tag to read.</param>
        /// <returns>The raw ulong bits cast to <see cref="long"/>.</returns>
        public long   GetLong  (GameplayTag tag) => (long)GetValue(tag);

        /// <summary>
        /// Returns the value stored for <paramref name="tag"/> reinterpreted as a double-precision
        /// float using the full 64-bit ulong storage (IEEE 754 bit pattern).
        /// </summary>
        /// <param name="tag">The tag to read.</param>
        /// <returns>The stored bits reinterpreted as a <see cref="double"/>.</returns>
        public double GetDouble(GameplayTag tag) => DoubleUnion.ToDouble(GetValue(tag));

        /// <summary>
        /// Stores <paramref name="value"/> as a <see cref="float"/> by reinterpreting its bits
        /// into the lower 32 bits of the tag's ulong storage, replacing the current value.
        /// </summary>
        /// <param name="tag">The tag whose value should be set.</param>
        /// <param name="value">The float value to store.</param>
        public void SetFloat (GameplayTag tag, float  value) => Apply(tag, GameplayTagArithmetic.Set, (ulong)FloatUnion.ToUInt(value));

        /// <summary>
        /// Stores <paramref name="value"/> as a signed 32-bit integer in the lower 32 bits of the
        /// tag's ulong storage, replacing the current value.
        /// </summary>
        /// <param name="tag">The tag whose value should be set.</param>
        /// <param name="value">The integer value to store.</param>
        public void SetInt   (GameplayTag tag, int    value) => Apply(tag, GameplayTagArithmetic.Set, (ulong)(uint)value);

        /// <summary>
        /// Stores <paramref name="value"/> as a signed 64-bit integer in the full ulong storage
        /// of the tag, replacing the current value.
        /// </summary>
        /// <param name="tag">The tag whose value should be set.</param>
        /// <param name="value">The long value to store.</param>
        public void SetLong  (GameplayTag tag, long   value) => Apply(tag, GameplayTagArithmetic.Set, (ulong)value);

        /// <summary>
        /// Stores <paramref name="value"/> as a double-precision float by reinterpreting its
        /// IEEE 754 bits into the tag's 64-bit ulong storage, replacing the current value.
        /// </summary>
        /// <param name="tag">The tag whose value should be set.</param>
        /// <param name="value">The double value to store.</param>
        public void SetDouble(GameplayTag tag, double value) => Apply(tag, GameplayTagArithmetic.Set, DoubleUnion.ToUlong(value));

        /// <summary>
        /// Stores the xxHash64 of <paramref name="tagPath"/> as the numeric value of
        /// <paramref name="tag"/>, enabling enum-like tag-references inside the collection.
        /// Retrieve the stored tag with
        /// <c>GameplayTag.FromName(GameplayTagRegistry.GetName(collection.GetValue(tag)))</c>.
        /// </summary>
        /// <param name="tag">The tag that will hold the reference value.</param>
        /// <param name="tagPath">The dot-path of the tag to store as the value. Must be a valid registered path.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="tagPath"/> is not a valid tag path.</exception>
        public void SetTagValue(GameplayTag tag, string tagPath)
        {
            if (!GameplayTagRegistry.ValidateTag(tagPath?.Trim() ?? string.Empty))
                throw new ArgumentException($"'{tagPath}' is not a valid GameplayTag path.", nameof(tagPath));
            Apply(tag, GameplayTagArithmetic.Set, GameplayTagRegistry.Hash(tagPath.Trim()));
        }

        /// <summary>Returns <c>true</c> when the collection contains no tag entries.</summary>
        public bool IsEmpty => _map.Count == 0;

        /// <summary>Returns the number of distinct tags currently held in the collection.</summary>
        public int Count   => _map.Count;

        /// <summary>
        /// Returns <c>true</c> if the collection contains <paramref name="tag"/> or, when
        /// <paramref name="exactMatch"/> is <c>false</c>, any registered descendant of it.
        /// </summary>
        /// <param name="tag">The tag to search for.</param>
        /// <param name="exactMatch">When <c>true</c>, only the exact tag satisfies the check.</param>
        /// <returns><c>true</c> if the tag (or a descendant) is present.</returns>
        public bool Contains(GameplayTag tag, bool exactMatch = false)
        {
            if (_map.ContainsKey(tag.Id)) return true;
            if (exactMatch) return false;
            foreach (var desc in GameplayTagRegistry.GetDescendants(tag.Id))
                if (_map.ContainsKey(desc)) return true;
            return false;
        }

        /// <summary>
        /// Returns <c>true</c> if every tag present in <paramref name="other"/> is also
        /// present in this collection, optionally using hierarchical matching.
        /// </summary>
        /// <param name="other">The collection whose tags must all be present.</param>
        /// <param name="exactMatch">When <c>true</c>, only exact tag matches are accepted.</param>
        /// <returns><c>true</c> if this collection contains every tag in <paramref name="other"/>.</returns>
        public bool ContainsAll(GameplayTagCollection other, bool exactMatch = false)
        {
            foreach (var kv in other._map)
                if (!Contains(new GameplayTag(kv.Key), exactMatch)) return false;
            return true;
        }

        /// <summary>
        /// Returns <c>true</c> if at least one tag from <paramref name="other"/> is present
        /// in this collection, optionally using hierarchical matching.
        /// </summary>
        /// <param name="other">The collection to check for any overlap.</param>
        /// <param name="exactMatch">When <c>true</c>, only exact tag matches are accepted.</param>
        /// <returns><c>true</c> if any tag in <paramref name="other"/> is also in this collection.</returns>
        public bool ContainsAny(GameplayTagCollection other, bool exactMatch = false)
        {
            foreach (var kv in other._map)
                if (Contains(new GameplayTag(kv.Key), exactMatch)) return true;
            return false;
        }

        /// <summary>
        /// Returns <c>true</c> if none of the tags in <paramref name="other"/> are present in
        /// this collection, optionally using hierarchical matching.
        /// </summary>
        /// <param name="other">The collection of tags to check for absence.</param>
        /// <param name="exactMatch">When <c>true</c>, only exact tag matches are considered.</param>
        /// <returns><c>true</c> if no tag from <paramref name="other"/> exists in this collection.</returns>
        public bool ContainsNone(GameplayTagCollection other, bool exactMatch = false)
        {
            foreach (var kv in other._map)
                if (Contains(new GameplayTag(kv.Key), exactMatch)) return false;
            return true;
        }

        /// <summary>
        /// Returns all tags currently in this collection that match <paramref name="filter"/> or,
        /// when <paramref name="exactMatch"/> is <c>false</c>, are children of it.
        /// </summary>
        /// <param name="filter">The tag to use as the filter criterion.</param>
        /// <param name="exactMatch">When <c>true</c>, only the exact filter tag is matched.</param>
        /// <returns>A new list containing every matching tag.</returns>
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

        /// <summary>
        /// Returns all tags currently in this collection that do NOT match <paramref name="filter"/>
        /// and are not children of it (when <paramref name="exactMatch"/> is <c>false</c>).
        /// </summary>
        /// <param name="filter">The tag to use as the exclusion criterion.</param>
        /// <param name="exactMatch">When <c>true</c>, only the exact filter tag is excluded.</param>
        /// <returns>A new list containing every tag that does not match the filter.</returns>
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

        /// <summary>
        /// Returns a new <see cref="GameplayTagCollection"/> containing only the tags that exist
        /// in both this collection and <paramref name="other"/> (set intersection).
        /// </summary>
        /// <param name="other">The collection to intersect with.</param>
        /// <param name="exactMatch">When <c>true</c>, only exact tag matches contribute to the intersection.</param>
        /// <returns>A new collection holding the shared tags and their values from this collection.</returns>
        public GameplayTagCollection GetShared(GameplayTagCollection other, bool exactMatch = false)
        {
            var result = new GameplayTagCollection();
            foreach (var kv in _map)
                if (other.Contains(new GameplayTag(kv.Key), exactMatch))
                    result._map[kv.Key] = kv.Value;
            return result;
        }

        /// <summary>
        /// Returns a new <see cref="GameplayTagCollection"/> containing only the tags present in
        /// this collection but not in <paramref name="other"/> (set difference).
        /// </summary>
        /// <param name="other">The collection whose tags should be excluded.</param>
        /// <param name="exactMatch">When <c>true</c>, only exact tag matches are excluded.</param>
        /// <returns>A new collection holding tags unique to this collection.</returns>
        public GameplayTagCollection GetExclusive(GameplayTagCollection other, bool exactMatch = false)
        {
            var result = new GameplayTagCollection();
            foreach (var kv in _map)
                if (!other.Contains(new GameplayTag(kv.Key), exactMatch))
                    result._map[kv.Key] = kv.Value;
            return result;
        }

        /// <summary>
        /// Enumerates all tag-value pairs currently stored in this collection.
        /// The enumeration order is not guaranteed to be stable.
        /// </summary>
        /// <returns>An enumerable sequence of (tag, value) tuples.</returns>
        public IEnumerable<(GameplayTag tag, ulong value)> GetAll()
        {
            foreach (var kv in _map)
                yield return (new GameplayTag(kv.Key), kv.Value);
        }

        // ── Events ───────────────────────────────────────────────────────────

        /// <summary>
        /// Registers <paramref name="callback"/> to be invoked whenever the value of
        /// <paramref name="tag"/> changes. When <paramref name="exactMatch"/> is <c>false</c>
        /// the callback also fires for changes to any registered descendant of the tag.
        /// The callback receives (changedTag, previousValue, newValue).
        /// </summary>
        /// <param name="tag">The tag to observe.</param>
        /// <param name="callback">The delegate to invoke on change.</param>
        /// <param name="exactMatch">When <c>true</c>, only changes to the exact tag trigger the callback.</param>
        public void Subscribe(GameplayTag tag, Action<GameplayTag, ulong, ulong> callback, bool exactMatch = false)
        {
            if (callback == null) return;
            _subscribers ??= new Dictionary<ulong, List<(Action<GameplayTag, ulong, ulong>, bool)>>();
            if (!_subscribers.TryGetValue(tag.Id, out var list))
            {
                list = new List<(Action<GameplayTag, ulong, ulong>, bool)>();
                _subscribers[tag.Id] = list;
            }
            // Skip an identical registration so a double-subscribe doesn't fire the callback twice.
            for (int i = 0; i < list.Count; i++)
                if (list[i].Item1 == callback && list[i].Item2 == exactMatch) return;
            list.Add((callback, exactMatch));
        }

        /// <summary>
        /// Removes a previously registered <paramref name="callback"/> for <paramref name="tag"/>.
        /// If the callback was not registered, or no subscriptions exist for the tag, this method
        /// is a no-op.
        /// </summary>
        /// <param name="tag">The tag the callback was registered against.</param>
        /// <param name="callback">The delegate to remove.</param>
        public void Unsubscribe(GameplayTag tag, Action<GameplayTag, ulong, ulong> callback)
        {
            if (_subscribers == null) return;
            if (!_subscribers.TryGetValue(tag.Id, out var list)) return;
            for (int i = list.Count - 1; i >= 0; i--)
                if (list[i].Item1 == callback) list.RemoveAt(i);
        }

        // ── Burst / Jobs ─────────────────────────────────────────────────────

        /// <summary>
        /// Creates a Burst-compatible copy of the current tag-value map allocated with the
        /// specified <paramref name="allocator"/>. The caller is responsible for disposing the
        /// returned <see cref="NativeHashMap{TKey,TValue}"/> when it is no longer needed.
        /// </summary>
        /// <param name="allocator">The native memory allocator to use for the snapshot.</param>
        /// <returns>A <see cref="NativeHashMap{TKey,TValue}"/> containing all current tag-value pairs.</returns>
        public NativeHashMap<ulong, ulong> GetSnapshot(Allocator allocator)
        {
            var map = new NativeHashMap<ulong, ulong>(_map.Count, allocator);
            foreach (var kv in _map)
                map.TryAdd(kv.Key, kv.Value);
            return map;
        }

        // ── Internal ─────────────────────────────────────────────────────────

        private void NotifyChange(GameplayTag tag, ulong prev, ulong next, bool fireChanged = true)
        {
            if (fireChanged) Changed?.Invoke(this);

            if (_subscribers == null) return;

            // Direct subscribers on this exact tag.
            FireSubscribers(tag.Id, tag, prev, next, descendantNotification: false);

            // Ancestor subscribers (non-exact ones that opted into descendant events). Snapshot the
            // matching ids before invoking so a callback that subscribes/unsubscribes — mutating
            // _subscribers — cannot corrupt this iteration. Allocates only when ancestors actually match.
            List<ulong> ancestors = null;
            foreach (var kv in _subscribers)
                if (kv.Key != tag.Id && GameplayTagRegistry.IsAncestor(kv.Key, tag.Id))
                    (ancestors ??= new List<ulong>()).Add(kv.Key);

            if (ancestors != null)
                foreach (var id in ancestors)
                    FireSubscribers(id, tag, prev, next, descendantNotification: true);
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
