using System.IO;
using UnityEditor;

namespace Heathen.GameplayTags.Editor
{
    /// <summary>
    /// Populates the live <see cref="GameplayTagRegistry"/> in the EDITOR by reading the <c>.gptags</c> JSON
    /// sources directly (no ScriptableObject). Runs on every domain reload so the tag picker / settings /
    /// validation reflect the current sources, and is re-run by the importer when a <c>.gptags</c> changes.
    ///
    /// At runtime (play / builds) registration is instead the generated <c>Register()</c>
    /// (<c>[RuntimeInitializeOnLoadMethod]</c>, baked literals) — see GameplayTags-CodeGen-Spec. This editor
    /// path is the live-authoring half of the same contract: JSON drives the editor, baked code drives the game.
    /// </summary>
    [InitializeOnLoad]
    public static class GameplayTagsEditorRegistrar
    {
        static GameplayTagsEditorRegistrar() => EditorApplication.delayCall += Refresh;

        /// <summary>Re-read every <c>.gptags</c> source and register its tags into the live registry.</summary>
        public static void Refresh()
        {
            foreach (var path in GameplayTagsSources.FindAll())
            {
                try
                {
                    var json = File.ReadAllText(Path.GetFullPath(path));
                    GameplayTagsCompiler.ParseSource(json, out bool registered, out string[] tags);
                    if (registered && tags.Length > 0)
                        GameplayTagRegistry.RegisterBaked(GameplayTagsCompiler.BuildEntries(tags));
                }
                catch
                {
                    // A malformed .gptags is surfaced by the importer's LogImportError; skip it here.
                }
            }
        }
    }
}
