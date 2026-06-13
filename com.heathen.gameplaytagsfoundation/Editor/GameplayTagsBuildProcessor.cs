using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using Object = UnityEngine.Object;

namespace Heathen.GameplayTags.Editor
{
    /// <summary>
    /// Keeps every auto-registering compiled tag asset in PlayerSettings → Preloaded Assets so the tag
    /// hierarchy is baked into player builds and registers itself at startup, regardless of where the
    /// <c>.gptags</c> files live in the project. Runs before each build and is also invoked on editor load.
    /// </summary>
    public static class GameplayTagsPreload
    {
        /// <summary>
        /// Ensures the preloaded-assets list contains exactly the current set of auto-registering compiled
        /// tag assets (plus whatever non-tag assets were already there), writing back only when it changes.
        /// </summary>
        public static void EnsureTagsPreloaded()
        {
            var auto = AssetDatabase.FindAssets("t:GameplayTagsCompiledData")
                .Select(g => AssetDatabase.LoadAssetAtPath<GameplayTagsCompiledData>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(a => a != null && a.AutoRegister)
                .Cast<Object>()
                .ToList();

            var current = PlayerSettings.GetPreloadedAssets().ToList();
            // Keep all non-tag entries (and drop null holes Unity leaves when assets are deleted), then
            // append the current auto-register tag set.
            var desired = current.Where(a => a != null && !(a is GameplayTagsCompiledData)).ToList();
            desired.AddRange(auto);

            if (!current.SequenceEqual(desired))
                PlayerSettings.SetPreloadedAssets(desired.ToArray());
        }
    }

    internal sealed class GameplayTagsBuildProcessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report) => GameplayTagsPreload.EnsureTagsPreloaded();
    }
}
