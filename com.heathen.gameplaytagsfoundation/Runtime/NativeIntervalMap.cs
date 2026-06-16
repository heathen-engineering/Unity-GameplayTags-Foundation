using System;
using Unity.Collections;

namespace Heathen.GameplayTags
{
    /// <summary>
    /// Interval (nested-set) encoding for a single registered tag. Hierarchical containment is a
    /// range test: tag X is under tag A when <c>A.Lft &lt; X.Lft &amp;&amp; X.Lft &lt;= A.Rgt</c>.
    /// </summary>
    public struct GameplayTagInterval
    {
        /// <summary>Depth-first enter index (preorder position).</summary>
        public uint Lft;
        /// <summary>Depth-first exit index.</summary>
        public uint Rgt;
        /// <summary>Depth in the tag forest (0 = root).</summary>
        public byte Depth;
        /// <summary>Scope membership. Reserved for the future scoped-tag tier; always 0 today.</summary>
        public ushort ScopeId;
    }

    /// <summary>
    /// A Burst-readable snapshot of the registry's interval encoding. Hierarchical queries are
    /// range comparisons, so they run inside jobs without managed registry calls. Caller owns the
    /// returned map and must <see cref="Dispose"/> it. Compare <see cref="Generation"/> against
    /// <see cref="GameplayTagRegistry.IntervalGeneration"/> to detect staleness after a rebuild.
    /// </summary>
    public struct NativeIntervalMap : IDisposable
    {
        /// <summary>Maps tag id to its interval. Only registered (Tier 1) tags are present.</summary>
        public NativeHashMap<ulong, GameplayTagInterval> Intervals;

        /// <summary>The <see cref="GameplayTagRegistry.IntervalGeneration"/> this snapshot was built from.</summary>
        public ulong Generation;

        public bool IsCreated => Intervals.IsCreated;

        /// <summary>Returns <c>true</c> if <paramref name="candidateId"/> is a descendant of
        /// <paramref name="ancestorId"/>. A tag is not a descendant of itself.</summary>
        public bool IsAncestor(ulong ancestorId, ulong candidateId)
        {
            if (Intervals.TryGetValue(ancestorId, out var a) && Intervals.TryGetValue(candidateId, out var c))
                return a.Lft < c.Lft && c.Lft <= a.Rgt;
            return false;
        }

        public void Dispose()
        {
            if (Intervals.IsCreated) Intervals.Dispose();
        }
    }
}
