using System;
using System.Collections.Generic;
using Heathen.Editor; // ISettingsMetadataProvider, SettingsMetadata

namespace Heathen.GameplayTags.Editor
{
    /// <summary>
    /// Publishes the live gameplay-tag vocabulary to other editor tools through the framework metadata seam
    /// (<see cref="SettingsMetadata"/>). Discovered by type, so the trivial constructor is all the registration
    /// needed. Reads the registry on each call so results reflect current <c>.gptags</c> edits — the editor
    /// registrar keeps the registry in sync with the sources on every domain reload and import.
    /// </summary>
    public sealed class GameplayTagsMetadataProvider : ISettingsMetadataProvider, ITagVocabulary
    {
        /// <inheritdoc/>
        public IEnumerable<string> Tags
        {
            get
            {
                // Snapshot the live dictionary value view to a sorted list: a stable ordinal order for
                // pickers, and safe to enumerate without holding a live view of the registry.
                var names = new List<string>(GameplayTagRegistry.GetAllNames());
                names.Sort(StringComparer.Ordinal);
                return names;
            }
        }
    }
}
