using UnityEditor;

namespace Heathen.GameplayTags.Editor
{
    [InitializeOnLoad]
    public static class GameplayTagsDataEditor
    {
        static GameplayTagsDataEditor()
        {
            EditorApplication.delayCall += ForceRefresh;
        }

        public static void ForceRefresh() => GameplayTagsCompiledDataRefresh.Refresh();
    }
}
