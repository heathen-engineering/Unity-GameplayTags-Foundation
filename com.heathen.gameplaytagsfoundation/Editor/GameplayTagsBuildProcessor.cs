using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Heathen.GameplayTags.Editor
{
    /// <summary>
    /// Guards player builds: the generated tag code (accessors + baked <c>Register()</c>) is compiled INTO
    /// the player and registers the tag hierarchy at startup, so it must be current before the build's
    /// compilation. There is no ScriptableObject to preload any more — the JSON doesn't ship, the baked
    /// code is the runtime. See GameplayTags-CodeGen-Spec.
    /// </summary>
    internal sealed class GameplayTagsBuildProcessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            // Regenerating here would be too late to recompile — fail loudly so the dev regenerates and
            // rebuilds, guaranteeing a build never ships stale tag accessors / baked registration.
            int stale = GameplayTagsCodeGenerator.CountStaleRegistered();
            if (stale > 0)
                throw new BuildFailedException(
                    $"[GameplayTags] {stale} tag set(s) have out-of-date generated code. " +
                    "Run Tools ▸ Heathen ▸ GameplayTags ▸ Generate Tag Code (or the Generate button in " +
                    "Project Settings ▸ Gameplay Tags), then rebuild.");
        }
    }
}
