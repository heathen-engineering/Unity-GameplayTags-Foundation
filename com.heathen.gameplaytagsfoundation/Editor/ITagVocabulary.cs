using System.Collections.Generic;

namespace Heathen.GameplayTags.Editor
{
    /// <summary>
    /// Editor-time domain contract exposing the project's registered gameplay-tag vocabulary, so authoring
    /// tools (for example a HATE Forge tag picker) can offer tag autocomplete without referencing the
    /// GameplayTags tool's internals or re-scanning <c>.gptags</c> sources. It is the concrete form of the
    /// <c>ITagVocabulary</c> example named in the framework's <see cref="Heathen.Editor.ISettingsMetadataProvider"/>
    /// docs: a consumer discovers it through the framework metadata seam with
    /// <c>SettingsMetadata.First&lt;ITagVocabulary&gt;()</c>, and an implementer pairs it with
    /// <see cref="Heathen.Editor.ISettingsMetadataProvider"/>.
    /// <para>
    /// The vocabulary is the set of <em>registered</em> tags (those baked to runtime); draft tags in
    /// unregistered sources are excluded, since picking one would resolve to nothing at runtime.
    /// </para>
    /// </summary>
    public interface ITagVocabulary
    {
        /// <summary>
        /// Every registered tag as its dot-path name (for example <c>"Effects.Buff.Strength"</c>), in a stable
        /// ordinal order. Reflects the live registry at call time, so it tracks current <c>.gptags</c> edits.
        /// </summary>
        IEnumerable<string> Tags { get; }
    }
}
