using System;
using System.IO;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Heathen.GameplayTags.Editor
{
    // Imports a .gptags JSON source as a TextAsset:
    //   { "registered": true, "tags": [ "Category.Group.Label", … ] }
    //
    // The .gptags format is now ONLY a runtime mod / UGC source — editor-authored project tags live in
    // Project Settings ▸ Gameplay Tags (GameplayTagSettings) and tool tags are owned by each tool. So this
    // importer just validates the JSON and exposes the raw text (source-controlled, engine-portable) for a
    // runtime loader to parse; it registers nothing into the editor registry. See GameplayTags-CodeGen-Spec.
    [ScriptedImporter(4, "gptags")]
    public class GameplayTagsImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var json = File.ReadAllText(ctx.assetPath);
            try
            {
                GameplayTagsCompiler.ParseSource(json, out _, out _);
            }
            catch (Exception e)
            {
                ctx.LogImportError($"Failed to parse .gptags JSON: {e.Message}");
            }

            var text = new TextAsset(json) { name = Path.GetFileNameWithoutExtension(ctx.assetPath) };
            ctx.AddObjectToAsset("main", text);
            ctx.SetMainObject(text);
        }
    }
}
