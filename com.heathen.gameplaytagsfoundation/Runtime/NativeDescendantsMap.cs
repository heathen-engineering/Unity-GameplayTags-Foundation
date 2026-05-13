using System;
using Unity.Collections;
using Unity.Mathematics;

namespace Heathen.GameplayTags
{
    // Burst-readable CSR-format descendants map.
    // FlatDescendants: all descendant ids packed contiguously.
    // Index: maps ancestor id -> (offset, count) into FlatDescendants.
    public struct NativeDescendantsMap : IDisposable
    {
        public NativeArray<ulong> FlatDescendants;
        public NativeHashMap<ulong, int2> Index;

        public bool IsCreated => FlatDescendants.IsCreated && Index.IsCreated;

        public bool HasDescendants(ulong ancestorId) =>
            Index.ContainsKey(ancestorId);

        // Returns a slice of FlatDescendants for the given ancestor.
        // Returns empty span if no descendants registered.
        public NativeSlice<ulong> GetDescendants(ulong ancestorId)
        {
            if (Index.TryGetValue(ancestorId, out var range))
                return new NativeSlice<ulong>(FlatDescendants, range.x, range.y);
            return default;
        }

        public bool IsDescendantOf(ulong candidateId, ulong ancestorId)
        {
            var slice = GetDescendants(ancestorId);
            for (int i = 0; i < slice.Length; i++)
                if (slice[i] == candidateId) return true;
            return false;
        }

        public void Dispose()
        {
            if (FlatDescendants.IsCreated) FlatDescendants.Dispose();
            if (Index.IsCreated) Index.Dispose();
        }
    }
}
