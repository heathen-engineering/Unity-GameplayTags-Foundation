using System.Collections.Generic;

namespace Heathen.GameplayTags.Editor
{
    /// <summary>
    /// Editor-time, provenance-carrying view of the tags a tool or package contributes to the project, so the
    /// Gameplay Tags settings panel (and other tooling) can show a developer <em>what</em> tags exist and
    /// <em>where</em> they come from. Each tool that authors its own tags — an Ogham Storyteller, a HATE world,
    /// etc. — exposes one by pairing this with <see cref="Heathen.Editor.ISettingsMetadataProvider"/>; consumers
    /// enumerate them via <c>SettingsMetadata.All&lt;ITagSource&gt;()</c>.
    /// <para>
    /// This complements <see cref="ITagVocabulary"/> (the flat, source-agnostic list of all registered tags used
    /// for autocomplete): a tool need not implement this for its tags to work in pickers (those read the live
    /// registry); this only adds the named, grouped provenance view. Read live so it tracks current edits.
    /// </para>
    /// </summary>
    public interface ITagSource
    {
        /// <summary>A short provenance label for this group of tags, e.g. "Ogham Storyteller" or "HATE: Combat".</summary>
        string SourceName { get; }

        /// <summary>The dot-path tags this source contributes (the authored tags; implied parents may be omitted).</summary>
        IEnumerable<string> Tags { get; }

        /// <summary>
        /// Whether these tags are registered/active in the system (hierarchy-aware and baked to runtime) as
        /// opposed to inert drafts. Lets the panel mark a source's status without inspecting the registry.
        /// </summary>
        bool Registered { get; }
    }
}
