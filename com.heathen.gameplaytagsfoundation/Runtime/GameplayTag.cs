using System;
using UnityEngine;

namespace Heathen.GameplayTags
{
    [Serializable]
    public readonly struct GameplayTag : IEquatable<GameplayTag>
    {
        [SerializeField] private readonly ulong _id;

        public ulong Id => _id;
        public bool IsValid => _id != 0;
        public string Name => GameplayTagRegistry.GetName(_id);

        public GameplayTag(ulong id) { _id = id; }

        public static GameplayTag FromName(string dotPath) =>
            new GameplayTag(GameplayTagRegistry.Hash(dotPath));

        public bool IsChildOf(GameplayTag parent) =>
            GameplayTagRegistry.IsAncestor(parent._id, _id);

        public bool IsParentOf(GameplayTag child) =>
            GameplayTagRegistry.IsAncestor(_id, child._id);

        public bool Equals(GameplayTag other) => _id == other._id;
        public override bool Equals(object obj) => obj is GameplayTag t && Equals(t);
        public override int GetHashCode() => _id.GetHashCode();
        public override string ToString() => Name ?? _id.ToString("X16");

        public static bool operator ==(GameplayTag a, GameplayTag b) => a._id == b._id;
        public static bool operator !=(GameplayTag a, GameplayTag b) => a._id != b._id;

        public static readonly GameplayTag Invalid = default;

        public static unsafe ulong HashPath(string dotPath)
        {
            if (string.IsNullOrEmpty(dotPath))
                return 0;
            var bytes = System.Text.Encoding.UTF8.GetBytes(dotPath);
            fixed (byte* ptr = bytes)
            {
                var h = Unity.Collections.xxHash3.Hash64(ptr, bytes.Length);
                return ((ulong)h.y << 32) | h.x;
            }
        }
    }
}
