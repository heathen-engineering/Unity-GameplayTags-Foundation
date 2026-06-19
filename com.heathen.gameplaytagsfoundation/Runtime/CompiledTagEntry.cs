using System;

namespace Heathen.GameplayTags
{
    /// <summary>
    /// One baked tag node: its Id (xxHash3 of the dot-path), the dot-path Name (for the registry's name
    /// map / debug display), and its immediate ParentId (0 = root) — the authoritative forest edge. This
    /// is the unit the registry ingests via <see cref="GameplayTagRegistry.RegisterBaked"/>, produced by
    /// the <c>.gptags</c> code generator (and the editor registrar) — no ScriptableObject involved.
    /// </summary>
    [Serializable]
    public struct CompiledTagEntry
    {
        public ulong  Id;
        public string Name;
        public ulong  ParentId;
    }
}
