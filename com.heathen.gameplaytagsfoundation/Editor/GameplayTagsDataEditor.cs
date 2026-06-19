using UnityEditor;

namespace Heathen.GameplayTags.Editor
{
    // The settings panel's "Refresh" entry point. Domain-reload auto-refresh is owned by
    // GameplayTagsEditorRegistrar ([InitializeOnLoad]); this just forwards manual refreshes.
    public static class GameplayTagsDataEditor
    {
        public static void ForceRefresh() => GameplayTagsEditorRegistrar.Refresh();
    }
}
