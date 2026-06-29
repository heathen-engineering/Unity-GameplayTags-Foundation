using Heathen.Editor; // ISettingsGenerator, GeneratorOutput

namespace Heathen.GameplayTags.Editor
{
    /// <summary>
    /// Adapts the GameplayTags code generator to the framework generation pipeline
    /// (<see cref="ISettingsGenerator"/>): the shared build hook (<c>SettingsBuildPreprocessor</c>) guards
    /// staleness and fails a build with stale tag code, the play-mode guard offers Build &amp; Play, and the
    /// shared menu drives generation. The actual bake stays in <see cref="GameplayTagsCodeGenerator"/>; this is
    /// the thin contract wrapper (the same role <c>OghamStoryGenerator</c> plays for Ogham). The generated tag
    /// accessors + baked <c>Register()</c> are C# compiled into the player, so the output is
    /// <see cref="GeneratorOutput.SourceCode"/> (regenerate-before-build, never mid-build).
    /// </summary>
    public sealed class GameplayTagsSettingsGenerator : ISettingsGenerator
    {
        public string Name => "Gameplay Tags";

        public GeneratorOutput Output => GeneratorOutput.SourceCode;

        public bool IsStale() => GameplayTagsCodeGenerator.IsStale();

        public void Generate() => GameplayTagsCodeGenerator.Generate();
    }
}
