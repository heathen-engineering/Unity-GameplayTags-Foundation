using System;
using System.IO;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Heathen.GameplayTags.Editor
{
    // Imports a .gptags JSON source. The JSON is the source of truth (no ScriptableObject):
    //   { "registered": true, "tags": [ "Category.Group.Label", … ] }
    //
    // The imported asset's main object is the raw JSON text (source-controlled, engine-portable). The tag
    // forest is registered into the live registry by GameplayTagsEditorRegistrar (editor, reads this JSON)
    // and by the generated Register() at runtime (baked literals). See GameplayTags-CodeGen-Spec.
    [ScriptedImporter(3, "gptags")]
    public class GameplayTagsImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var json = File.ReadAllText(ctx.assetPath);
            try
            {
                GameplayTagsCompiler.ParseSource(json, out bool registered, out string[] tags);

                // Tags present but not registered almost always means a forgotten flag — surface it.
                if (!registered && tags.Length > 0)
                    ctx.LogImportWarning(
                        $"[GameplayTags] '{Path.GetFileNameWithoutExtension(ctx.assetPath)}.gptags' has " +
                        $"{tags.Length} tag(s) but \"registered\" is not true — none will be registered. " +
                        "Set \"registered\": true to activate them.");
            }
            catch (Exception e)
            {
                ctx.LogImportError($"Failed to parse .gptags JSON: {e.Message}");
            }

            var text = new TextAsset(json) { name = Path.GetFileNameWithoutExtension(ctx.assetPath) };
            ctx.AddObjectToAsset("main", text);
            ctx.SetMainObject(text);

            // Refresh the editor-side registry so the tag picker / validation reflect this edit immediately.
            GameplayTagsEditorRegistrar.Refresh();
        }
    }
}
