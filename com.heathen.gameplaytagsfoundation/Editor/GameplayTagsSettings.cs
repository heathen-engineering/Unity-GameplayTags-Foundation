using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Heathen.GameplayTags.Editor
{
    public class GameplayTagsSettings : ScriptableObject
    {
        public bool autoDiscover = true;
        [HideInInspector] public List<GameplayTagsData> databases = new();

        private const string DefaultPath = "Assets/Settings/GameplayTagsSettings.asset";

        public static GameplayTagsSettings GetOrCreate()
        {
            var guids = AssetDatabase.FindAssets("t:GameplayTagsSettings");
            if (guids != null && guids.Length > 0)
            {
                var path  = AssetDatabase.GUIDToAssetPath(guids[0]);
                var found = AssetDatabase.LoadAssetAtPath<GameplayTagsSettings>(path);
                if (found) return found;
            }

            System.IO.Directory.CreateDirectory(
                System.IO.Path.GetDirectoryName(DefaultPath) ?? string.Empty);

            var asset = CreateInstance<GameplayTagsSettings>();
            AssetDatabase.CreateAsset(asset, DefaultPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[GameplayTagsSettings] Created new settings asset at " + DefaultPath);
            return asset;
        }

        // Resolves the full database list: explicit list + Resources scan (if autoDiscover).
        public List<GameplayTagsData> GetAllDatabases()
        {
            var result = new List<GameplayTagsData>(databases);
            if (!autoDiscover) return result;

            var guids = AssetDatabase.FindAssets("t:GameplayTagsData");
            foreach (var guid in guids)
            {
                var db = AssetDatabase.LoadAssetAtPath<GameplayTagsData>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (db != null && !result.Contains(db))
                    result.Add(db);
            }
            return result;
        }
    }
}
