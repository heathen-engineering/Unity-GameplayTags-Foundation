using System.Collections.Generic;
using UnityEngine;

namespace Heathen.GameplayTags
{
    [CreateAssetMenu(menuName = "Heathen/Gameplay Tags/Tags Data", fileName = "GameplayTagsData")]
    public class GameplayTagsData : ScriptableObject
    {
        public bool autoRegister = true;
        public List<string> tags = new();

        private void OnEnable()
        {
#if UNITY_EDITOR
            // Editor-time registration handled by GameplayTagsDataEditor via [InitializeOnLoad]
#else
            if (autoRegister)
                GameplayTagRegistry.RegisterDefaults(this);
#endif
        }

        private void OnDisable()
        {
#if !UNITY_EDITOR
            if (autoRegister)
                GameplayTagRegistry.UnregisterDefaults(this);
#endif
        }

        public List<string> GetValidationErrors()
        {
            var errors = new List<string>();
            var seen = new HashSet<string>();
            foreach (var tag in tags)
            {
                if (string.IsNullOrWhiteSpace(tag))
                {
                    errors.Add("Empty tag entry found.");
                    continue;
                }
                if (!seen.Add(tag))
                    errors.Add($"Duplicate tag: {tag}");
            }
            return errors;
        }
    }
}
