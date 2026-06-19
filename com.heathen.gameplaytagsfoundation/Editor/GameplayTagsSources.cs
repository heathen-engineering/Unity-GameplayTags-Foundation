using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Heathen.GameplayTags.Editor
{
    /// <summary>
    /// Discovers the project's <c>.gptags</c> source files by extension scan. Replaces the old
    /// <c>AssetDatabase.FindAssets("t:GameplayTagsCompiledData")</c> discovery — there is no longer a
    /// compiled ScriptableObject sub-asset to query by type; the <c>.gptags</c> JSON is the source.
    /// </summary>
    public static class GameplayTagsSources
    {
        /// <summary>Project-relative ("Assets/…") paths of every <c>.gptags</c> file, sorted for stable order.</summary>
        public static List<string> FindAll()
        {
            var result = new List<string>();
            string root = Application.dataPath;
            foreach (var full in Directory.GetFiles(root, "*.gptags", SearchOption.AllDirectories))
                result.Add("Assets" + full.Substring(root.Length).Replace('\\', '/'));
            result.Sort(System.StringComparer.Ordinal);
            return result;
        }
    }
}
