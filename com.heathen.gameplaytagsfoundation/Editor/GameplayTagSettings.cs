using System.Collections.Generic;
using Heathen;
using Heathen.Editor;

namespace Heathen.GameplayTags.Editor
{
    /// <summary>
    /// The project's own standalone gameplay-tag vocabulary, stored as JSON in
    /// <c>ProjectSettings/GameplayTagSettings.json</c> via the Game Framework settings store (no ScriptableObject,
    /// no scattered <c>.gptags</c> files in Assets). Edit via <c>Project Settings ▸ Gameplay Tags</c>.
    /// <para>
    /// Tags a specific tool needs (an Ogham story, a HATE world, etc.) are owned and registered by that tool
    /// through its own codegen/subsystem, so they do NOT live here. This store is only for project-level tags a
    /// developer defines directly. The <c>.gptags</c> file format still exists, but solely as a runtime source
    /// for mods / UGC — never for editor-time authored tags.
    /// </para>
    /// <para>
    /// Two groups preserve the old per-file <c>registered</c> distinction without separate files:
    /// <see cref="RegisteredTags"/> are hierarchy-aware — registered into the live registry and baked into the
    /// runtime <c>Register()</c>; <see cref="UnregisteredTags"/> are authored drafts that are stored but neither
    /// registered nor baked.
    /// </para>
    /// </summary>
    [Settings(Location = SettingsLocation.ProjectSettings)]
    public class GameplayTagSettings
    {
        /// <summary>Hierarchy-aware tags: registered into the live registry and baked into the runtime <c>Register()</c>.</summary>
        public List<string> RegisteredTags = new();

        /// <summary>Authored but inactive tags: stored for editing, but not registered and not baked.</summary>
        public List<string> UnregisteredTags = new();

        private static GameplayTagSettings _instance;

        /// <summary>The project's tag settings, loaded once and cached (a fresh default when no file exists yet).</summary>
        public static GameplayTagSettings GetOrCreate() => _instance ??= SettingsStore.Load<GameplayTagSettings>();

        /// <summary>Drops the cache and re-reads from disk. Use after an external edit to the file.</summary>
        public static GameplayTagSettings Reload() => _instance = SettingsStore.Load<GameplayTagSettings>();

        /// <summary>Persists changes back to ProjectSettings and refreshes the cache.</summary>
        public void Save()
        {
            SettingsStore.Save(this);
            _instance = this;
        }
    }
}
