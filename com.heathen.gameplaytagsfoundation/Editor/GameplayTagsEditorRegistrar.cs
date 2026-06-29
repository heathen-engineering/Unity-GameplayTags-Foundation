using System;
using UnityEditor;

namespace Heathen.GameplayTags.Editor
{
    /// <summary>
    /// Registers the project's standalone tags (the <see cref="GameplayTagSettings.RegisteredTags"/> in
    /// <c>ProjectSettings</c>) into the live <see cref="GameplayTagRegistry"/> in the EDITOR, so the tag picker,
    /// settings panel, and validation reflect them. Runs on every domain reload and is re-run when the settings
    /// change. Tool-owned tags are registered by each tool's own editor path (e.g. Ogham's tag registrar); this
    /// covers only the project-level vocabulary.
    ///
    /// <para>At runtime the same tags arrive via the generated <c>Register()</c>
    /// (<c>[RuntimeInitializeOnLoadMethod]</c>, baked literals) — see GameplayTags-CodeGen-Spec. The
    /// <c>.gptags</c> format is no longer an editor source; it remains only as a runtime mod / UGC source.</para>
    /// </summary>
    [InitializeOnLoad]
    public static class GameplayTagsEditorRegistrar
    {
        static GameplayTagsEditorRegistrar() => EditorApplication.delayCall += Refresh;

        /// <summary>Re-read the project tag settings and register their hierarchy-aware tags into the live registry.</summary>
        public static void Refresh()
        {
            var settings = GameplayTagSettings.Reload();
            var tags     = settings.RegisteredTags?.ToArray() ?? Array.Empty<string>();
            if (tags.Length == 0) return;
            GameplayTagRegistry.RegisterBaked(GameplayTagsCompiler.BuildEntries(tags));
        }
    }
}
