using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Heathen.GameplayTags.Editor
{
    /// <summary>
    /// Bakes the project's standalone tag vocabulary (the <see cref="GameplayTagSettings.RegisteredTags"/> in
    /// <c>ProjectSettings</c>) into a single C# file (GameplayTags-CodeGen-Spec, S2): a flat accessor class of
    /// <see cref="GameplayTag"/> constants (purpose 2 — autocomplete, no fat-fingering) plus a baked
    /// <c>Register()</c> that hands the registry literal Id/ParentId/Name (purpose 1 — instant load, no
    /// parse/hash/SO). Ids are baked from <see cref="GameplayTagRegistry.Hash"/> at generation time.
    ///
    /// <para>Tool-owned tags are NOT generated here — each tool (Ogham, HATE, …) bakes its own tags through its
    /// own generator. This handles only the project-level tags a developer authors directly.</para>
    ///
    /// The string-producing core (<see cref="SanitizeMember"/>, <see cref="GenerateSource"/>,
    /// <see cref="ContentHash"/>) is pure and unit-tested; <see cref="Generate"/> is the editor I/O wrapper,
    /// driven on demand (menu / Project Settings button) and by the framework build hook via
    /// <c>GameplayTagsSettingsGenerator</c>.
    /// </summary>
    public static class GameplayTagsCodeGenerator
    {
        public const string GeneratedNamespace = "Heathen.GameplayTags.Generated";

        /// <summary>The generated accessor + registration class name for the project's standalone tags.</summary>
        public const string ClassName = "ProjectGameplayTags";

        /// <summary>
        /// Project-relative path of the single generated file. It sits under <c>Assets</c> so it compiles into
        /// the player (Assembly-CSharp), where its <c>[RuntimeInitializeOnLoadMethod] Register()</c> registers
        /// the baked tag hierarchy at startup.
        /// </summary>
        public const string GeneratedPath = "Assets/Generated/GameplayTags/" + ClassName + ".g.cs";

        private const string HashMarker = "// gptags-hash:0x";

        [MenuItem("Tools/Heathen/GameplayTags/Generate Tag Code")]
        public static void GenerateAll()
        {
            Generate();
            AssetDatabase.Refresh();
            Debug.Log("[GameplayTags] Generated project tag code.");
        }

        /// <summary>
        /// Bakes the project's registered tags to <see cref="GeneratedPath"/>. When there are no registered
        /// project tags, removes the generated file instead of emitting an empty one.
        /// </summary>
        public static void Generate()
        {
            var entries = LoadEntries();
            string full = Path.GetFullPath(GeneratedPath);

            if (entries.Length == 0)
            {
                DeleteIfExists(full);
                DeleteIfExists(full + ".meta");
                return;
            }

            string source = GenerateSource(ClassName, GeneratedNamespace, entries);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllText(full, source);
        }

        /// <summary>True when a generated tags file already exists in the project.</summary>
        public static bool ScriptExists() => File.Exists(Path.GetFullPath(GeneratedPath));

        /// <summary>
        /// True when the generated file is behind the project's registered tags: missing while tags exist, an
        /// embedded hash that differs from the current set, or present while there are no tags (needs removal).
        /// </summary>
        public static bool IsStale()
        {
            var entries = LoadEntries();
            string full  = Path.GetFullPath(GeneratedPath);
            bool exists  = File.Exists(full);

            if (entries.Length == 0) return exists;     // a stale leftover that should be deleted
            if (!exists)             return true;

            ulong want = ContentHash(entries);
            foreach (var line in File.ReadLines(full))
            {
                int idx = line.IndexOf(HashMarker, StringComparison.Ordinal);
                if (idx < 0) continue;
                string hex = line.Substring(idx + HashMarker.Length).Trim();
                return !(ulong.TryParse(hex, System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture, out ulong have) && have == want);
            }
            return true; // no marker → regenerate
        }

        // Build the baked entries from the ProjectSettings registered tags (parents auto-included).
        private static CompiledTagEntry[] LoadEntries()
        {
            var settings = GameplayTagSettings.GetOrCreate();
            var tags = settings.RegisteredTags?.ToArray() ?? Array.Empty<string>();
            return GameplayTagsCompiler.BuildEntries(tags);
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path)) File.Delete(path);
        }

        /// <summary>Deterministic hash over the baked entries (Id/ParentId/Name) — embedded in the generated
        /// file as the staleness marker. Entries arrive in a stable order, so this is reproducible.</summary>
        public static ulong ContentHash(CompiledTagEntry[] entries)
        {
            var sb = new StringBuilder();
            foreach (var e in entries)
                sb.Append(e.Id.ToString("X16")).Append(':').Append(e.ParentId.ToString("X16")).Append(':').Append(e.Name).Append('|');
            return GameplayTagRegistry.Hash(sb.ToString());
        }

        /// <summary>
        /// The pure code emitter (no I/O). Produces the accessor class + baked <c>Register()</c> text for a
        /// set of baked entries. Deterministic given the same entries (they arrive parent-before-child).
        /// </summary>
        public static string GenerateSource(string className, string @namespace, CompiledTagEntry[] entries)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated>");
            sb.AppendLine("//   Generated from the project's Gameplay Tag settings by GameplayTagsCodeGenerator. DO NOT EDIT.");
            sb.AppendLine("//   Edit the tags in Project Settings ▸ Gameplay Tags, then re-run Tools ▸ Heathen ▸ GameplayTags ▸ Generate Tag Code.");
            sb.AppendLine("// </auto-generated>");
            sb.AppendLine($"{HashMarker}{ContentHash(entries):X16}  // staleness marker — do not edit");
            sb.AppendLine("using Heathen.GameplayTags;");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine();
            bool hasNs = !string.IsNullOrEmpty(@namespace);
            string ind = hasNs ? "    " : "";
            if (hasNs) { sb.AppendLine($"namespace {@namespace}"); sb.AppendLine("{"); }

            sb.AppendLine($"{ind}public static class {className}");
            sb.AppendLine($"{ind}{{");

            // ── Accessors (purpose 2): baked-Id GameplayTag constants, source path in a comment. ──
            foreach (var e in entries)
                sb.AppendLine($"{ind}    public static readonly GameplayTag {SanitizeMember(e.Name)} = GameplayTag.FromId(0x{e.Id:X16}UL); // \"{e.Name}\"");
            sb.AppendLine();

            // ── Baked registration data (purpose 1): pure literals, no parse/hash. ──
            sb.AppendLine($"{ind}    static readonly CompiledTagEntry[] _baked =");
            sb.AppendLine($"{ind}    {{");
            foreach (var e in entries)
                sb.AppendLine($"{ind}        new CompiledTagEntry {{ Id = 0x{e.Id:X16}UL, ParentId = 0x{e.ParentId:X16}UL, Name = \"{e.Name}\" }},");
            sb.AppendLine($"{ind}    }};");
            sb.AppendLine();
            sb.AppendLine($"{ind}    /// <summary>Registers the project's tags (baked literals) into the GameplayTagRegistry.</summary>");
            sb.AppendLine($"{ind}    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]");
            sb.AppendLine($"{ind}    public static void Register() => GameplayTagRegistry.RegisterBaked(_baked);");

            sb.AppendLine($"{ind}}}");
            if (hasNs) sb.AppendLine("}");
            return sb.ToString();
        }

        /// <summary>Turn a tag path (or file name) into a valid C# identifier: dots and any non-identifier
        /// character become <c>_</c>, and a leading digit is prefixed with <c>_</c>.</summary>
        public static string SanitizeMember(string path)
        {
            if (string.IsNullOrEmpty(path)) return "_";
            var sb = new StringBuilder(path.Length);
            foreach (char c in path)
                sb.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
            if (char.IsDigit(sb[0])) sb.Insert(0, '_');
            return sb.ToString();
        }
    }
}
