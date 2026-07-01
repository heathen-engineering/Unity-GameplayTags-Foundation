using Heathen.Editor;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace Heathen.GameplayTags.Editor
{
    /// <summary>
    /// Reports to the Game Framework when the baked tag code is behind the project's registered tags, so the
    /// <see cref="GameplayTagsSubsystem"/> shows a "Build" attention chip on Project ▸ Subsystems (and in the
    /// play-mode guard / Scene-view overlay). Reuses the registered "Gameplay Tags" generator so it always agrees
    /// with the shared build pipeline.
    /// </summary>
    public sealed class GameplayTagsSubsystemHealth : ISubsystemHealth
    {
        public Type SubsystemType => typeof(GameplayTagsSubsystem);

        public IEnumerable<SubsystemIssue> GetIssues()
        {
            var generator = SettingsGenerators.All.FirstOrDefault(g => g.Name == "Gameplay Tags");
            if (generator != null && generator.IsStale())
                yield return new SubsystemIssue(
                    SubsystemHealthSeverity.Warning,
                    "Tag code is out of date. Build to apply your latest registered tags.",
                    "Build",
                    () => { generator.Generate(); AssetDatabase.Refresh(); });
        }
    }
}
