using System;
using UnityEngine;

namespace Heathen.GameplayTags
{
    // Compiled output produced by GameplayTagsImporter from a .gptags source file.
    // Stores the pre-built descendants map as flat arrays — no string parsing at runtime,
    // just array iteration into the registry dictionaries.
    //
    // Source: MyTags.gptags (JSON, human-authored, source-controlled)
    // Output: MyTags.gptags sub-asset (this ScriptableObject, drag-and-drop ready)
    [Serializable]
    public struct CompiledTagEntry
    {
        public ulong   Id;
        public string  Name;        // dot-path string, for _nameMap / debug display
        public ulong[] Descendants; // all descendant IDs at every depth
    }

    public class GameplayTagsCompiledData : ScriptableObject
    {
        // When true, Init() merges this asset's entries into the live registry on startup.
        // Mirrors the "registered" flag in the .gptags source file.
        public bool              AutoRegister = true;
        public CompiledTagEntry[] Entries;
    }
}
