using System;
using UnityEngine;

namespace Heathen.GameplayTags
{
    // Compiled output produced by GameplayTagsImporter from a .gptags source file.
    // Stores the tag forest as parent links — no string parsing at runtime, just array
    // iteration into the registry. The registry computes interval (nested-set) encoding from
    // these parent links after merging all loaded sets (see GameplayTagRegistry.RebuildIntervals).
    //
    // Source: MyTags.gptags (JSON, human-authored, source-controlled)
    // Output: MyTags.gptags sub-asset (this ScriptableObject, drag-and-drop ready)
    [Serializable]
    public struct CompiledTagEntry
    {
        public ulong  Id;       // xxHash3 of the dot-path
        public string Name;     // dot-path string, for _nameMap / debug display
        public ulong  ParentId; // immediate parent id (0 = root); the authoritative forest edge
    }

    public class GameplayTagsCompiledData : ScriptableObject
    {
        // When true, Init() merges this asset's entries into the live registry on startup.
        // Mirrors the "registered" flag in the .gptags source file.
        public bool              AutoRegister = true;
        public CompiledTagEntry[] Entries;

        private void OnEnable()
        {
#if !UNITY_EDITOR
            // Self-register whenever the asset loads at runtime — including PlayerSettings-preloaded assets
            // that may load after the registry's subsystem-registration pass. Editor-time registration is
            // handled by GameplayTagsCompiledDataRefresh.
            if (AutoRegister)
                GameplayTagRegistry.Register(this);
#endif
        }
    }
}
