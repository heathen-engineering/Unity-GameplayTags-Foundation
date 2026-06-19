using System;
using UnityEngine;

namespace Heathen.GameplayTags
{
    /// <summary>
    /// Represents a single gameplay tag as a serialisable 64-bit hash identifier.
    /// Tags are hierarchical dot-separated paths (e.g. <c>Effects.Buff.Strength</c>) registered
    /// in <see cref="GameplayTagRegistry"/> and compared by hash rather than string.
    /// </summary>
    [Serializable]
    public struct GameplayTag : IEquatable<GameplayTag>
    {
        [SerializeField] private ulong _id;

        /// <summary>
        /// The 64-bit xxHash3 identifier that uniquely represents this tag's dot-path.
        /// A value of zero indicates an invalid or unset tag.
        /// </summary>
        public ulong Id => _id;

        /// <summary>
        /// Returns <c>true</c> when the tag has a non-zero identifier, indicating it has been
        /// assigned a value. An invalid tag is the default value of this struct.
        /// </summary>
        public bool IsValid => _id != 0;

        /// <summary>
        /// Returns the full dot-path name of the tag as registered in <see cref="GameplayTagRegistry"/>,
        /// or <c>null</c> if the identifier is not present in the registry.
        /// </summary>
        public string Name => GameplayTagRegistry.GetName(_id);

        /// <summary>
        /// Initialises a <see cref="GameplayTag"/> directly from a pre-computed hash identifier.
        /// Use <see cref="FromName"/> to construct a tag from a dot-path string.
        /// </summary>
        /// <param name="id">The 64-bit hash identifier for the tag.</param>
        public GameplayTag(ulong id) { _id = id; }

        /// <summary>
        /// Creates a <see cref="GameplayTag"/> from a pre-computed 64-bit Id. This is what generated tag
        /// code uses (e.g. <c>Tags.Effects_Buff_Strength = GameplayTag.FromId(0x…u)</c>): the Id is the
        /// xxHash3 of the path, baked at generation time, so no hashing happens at runtime.
        /// </summary>
        public static GameplayTag FromId(ulong id) => new GameplayTag(id);

        /// <summary>
        /// Creates a <see cref="GameplayTag"/> from a dot-separated path string by hashing it
        /// with the same algorithm used by the registry. The tag does not need to be registered
        /// for this to succeed, but runtime queries will only resolve names for registered tags.
        /// </summary>
        /// <param name="dotPath">The dot-separated tag path, e.g. <c>Effects.Buff.Strength</c>.</param>
        /// <returns>A <see cref="GameplayTag"/> whose identifier is the xxHash3 of <paramref name="dotPath"/>.</returns>
        public static GameplayTag FromName(string dotPath) =>
            new GameplayTag(GameplayTagRegistry.Hash(dotPath));

        /// <summary>
        /// Returns <c>true</c> if this tag is a descendant of <paramref name="parent"/> in the
        /// registered hierarchy. A tag is not considered a child of itself.
        /// </summary>
        /// <param name="parent">The tag to test as a potential ancestor.</param>
        /// <returns><c>true</c> if <paramref name="parent"/> is an ancestor of this tag.</returns>
        public bool IsChildOf(GameplayTag parent) =>
            GameplayTagRegistry.IsAncestor(parent._id, _id);

        /// <summary>
        /// Returns <c>true</c> if this tag is an ancestor of <paramref name="child"/> in the
        /// registered hierarchy. A tag is not considered a parent of itself.
        /// </summary>
        /// <param name="child">The tag to test as a potential descendant.</param>
        /// <returns><c>true</c> if this tag is an ancestor of <paramref name="child"/>.</returns>
        public bool IsParentOf(GameplayTag child) =>
            GameplayTagRegistry.IsAncestor(_id, child._id);

        /// <summary>
        /// Determines whether this tag is equal to <paramref name="other"/> by comparing identifiers.
        /// </summary>
        /// <param name="other">The tag to compare against.</param>
        /// <returns><c>true</c> if both tags share the same identifier.</returns>
        public bool Equals(GameplayTag other) => _id == other._id;

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is GameplayTag t && Equals(t);

        /// <inheritdoc/>
        public override int GetHashCode() => _id.GetHashCode();

        /// <summary>
        /// Returns the dot-path name from the registry, or the identifier formatted as a
        /// 16-digit hexadecimal string when the tag is not registered.
        /// </summary>
        /// <returns>A human-readable string representation of the tag.</returns>
        public override string ToString() => Name ?? _id.ToString("X16");

        /// <summary>Compares two tags for equality by identifier.</summary>
        public static bool operator ==(GameplayTag a, GameplayTag b) => a._id == b._id;

        /// <summary>Compares two tags for inequality by identifier.</summary>
        public static bool operator !=(GameplayTag a, GameplayTag b) => a._id != b._id;

        /// <summary>
        /// Implicitly converts a dot-path string to a <see cref="GameplayTag"/> by hashing it.
        /// </summary>
        /// <param name="tagPath">The dot-separated tag path to convert.</param>
        public static implicit operator GameplayTag(string tagPath) => FromName(tagPath);

        /// <summary>
        /// Implicitly wraps a raw 64-bit identifier in a <see cref="GameplayTag"/>.
        /// </summary>
        /// <param name="id">The pre-computed hash identifier.</param>
        public static implicit operator GameplayTag(ulong id)       => new GameplayTag(id);

        /// <summary>
        /// Implicitly extracts the raw 64-bit identifier from a <see cref="GameplayTag"/>.
        /// </summary>
        /// <param name="tag">The tag to convert.</param>
        public static implicit operator ulong(GameplayTag tag)      => tag._id;

        /// <summary>
        /// A sentinel value representing an invalid or unassigned tag, equivalent to <c>default</c>.
        /// Its identifier is zero and <see cref="IsValid"/> returns <c>false</c>.
        /// </summary>
        public static readonly GameplayTag Invalid = default;

        /// <summary>
        /// Computes the 64-bit xxHash3 of a dot-path string using the same algorithm as
        /// <see cref="GameplayTagRegistry"/>. This method is used internally and exposed for
        /// Burst-compatible contexts that cannot call managed registry methods.
        /// </summary>
        /// <param name="dotPath">The dot-separated tag path to hash.</param>
        /// <returns>A 64-bit hash, or zero if <paramref name="dotPath"/> is null or empty.</returns>
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
