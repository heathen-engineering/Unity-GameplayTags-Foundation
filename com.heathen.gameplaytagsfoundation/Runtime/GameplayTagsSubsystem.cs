using System.Collections.Generic;
using System.Linq;
using Heathen;

namespace Heathen.GameplayTags
{
    /// <summary>
    /// Global framework subsystem that owns the <see cref="GameplayTagRegistry"/> session lifecycle. It runs
    /// the per-session reset at framework boot (<see cref="SubsystemScope.Global"/> subsystems initialise at
    /// <c>RuntimeInitializeLoadType.SubsystemRegistration</c>), replacing the registry's former ad-hoc
    /// <c>[RuntimeInitializeOnLoadMethod]</c> bootstrap so the lifecycle is framework-managed and other
    /// subsystems can declare an ordering dependency on it (e.g. a HATE world subsystem via <c>DependsOn</c>).
    /// <para>
    /// The static <see cref="GameplayTagRegistry"/> remains the ergonomic facade for tag queries; this
    /// subsystem only governs when the registry is reset. Reset runs before the generated tag <c>Register()</c>
    /// (<c>BeforeSceneLoad</c>), which re-applies the baked tags on top of the clean base.
    /// </para>
    /// </summary>
    [Subsystem(SubsystemScope.Global)]
    public sealed class GameplayTagsSubsystem : Subsystem, ISubsystemDebug
    {
        /// <summary>Resets the registry to a clean session state and restores the baked default base.</summary>
        protected override void Initialize() => GameplayTagRegistry.ResetForSession();

        /// <inheritdoc/>
        public IEnumerable<(string label, string value)> GetDebugInfo()
        {
            yield return ("Registered tags", GameplayTagRegistry.GetAllIds().Count().ToString());
            yield return ("Interval generation", GameplayTagRegistry.IntervalGeneration.ToString());
        }
    }
}
