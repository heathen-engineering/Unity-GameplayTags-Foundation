using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Heathen.GameplayTags.Editor
{
    /// <summary>
    /// Generates baked C# from a <c>.gptags</c> source (GameplayTags-CodeGen-Spec, S2): a flat accessor
    /// class of <see cref="GameplayTag"/> constants (purpose 2 — autocomplete, no fat-fingering) plus a
    /// baked <c>Register()</c> that hands the registry literal Id/ParentId/Name (purpose 1 — instant load,
    /// no parse/hash/SO). Ids are baked from <see cref="GameplayTagRegistry.Hash"/> at generation time.
    ///
    /// The string-producing core (<see cref="SanitizeMember"/>, <see cref="GenerateSource"/>) is pure and
    /// unit-tested; <see cref="Generate"/>/<see cref="GenerateAll"/> are the editor I/O wrappers. The
    /// Generate button + build hook + staleness indicator are S3.
    /// </summary>
    public static class GameplayTagsCodeGenerator
    {
        public const string GeneratedNamespace = "Heathen.GameplayTags.Generated";
        public const string GeneratedFolder    = "Generated";

        [MenuItem("Tools/Heathen/GameplayTags/Generate Tag Code")]
        public static void GenerateAll()
        {
            string[] files = Directory.GetFiles(Application.dataPath, "*.gptags", SearchOption.AllDirectories);
            int generated = 0;
            foreach (var full in files)
            {
                // Convert absolute path → project-relative "Assets/…".
                string assetPath = "Assets" + full.Substring(Application.dataPath.Length).Replace('\\', '/');
                if (Generate(assetPath)) generated++;
            }
            AssetDatabase.Refresh();
            Debug.Log($"[GameplayTags] Generated tag code for {generated} of {files.Length} .gptags file(s).");
        }

        /// <summary>Generate the <c>.g.cs</c> for one <c>.gptags</c>. Returns true if it emitted code
        /// (false when the source isn't <c>registered</c> or has no tags).</summary>
        public static bool Generate(string gptagsAssetPath)
        {
            string full = Path.GetFullPath(gptagsAssetPath);
            string json = File.ReadAllText(full);
            GameplayTagsCompiler.ParseSource(json, out bool registered, out string[] tags);
            if (!registered || tags.Length == 0)
                return false;

            var entries = GameplayTagsCompiler.BuildEntries(tags);
            string className = SanitizeMember(Path.GetFileNameWithoutExtension(gptagsAssetPath));
            string source = GenerateSource(className, GeneratedNamespace, entries);

            string outPath = GeneratedPathFor(gptagsAssetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(outPath));
            File.WriteAllText(outPath, source);
            return true;
        }

        /// <summary>The absolute path of the <c>.g.cs</c> a given <c>.gptags</c> generates.</summary>
        public static string GeneratedPathFor(string gptagsAssetPath)
        {
            string full = Path.GetFullPath(gptagsAssetPath);
            string className = SanitizeMember(Path.GetFileNameWithoutExtension(gptagsAssetPath));
            string dir = Path.Combine(Path.GetDirectoryName(full) ?? ".", GeneratedFolder);
            return Path.Combine(dir, className + ".g.cs");
        }

        /// <summary>True if a registered <c>.gptags</c> has no generated file, or one whose embedded content
        /// hash differs from the current source — i.e. the generated code is behind the JSON. Unregistered /
        /// empty sources are never "stale" (they generate nothing).</summary>
        public static bool IsStale(string gptagsAssetPath)
        {
            string json;
            try { json = File.ReadAllText(Path.GetFullPath(gptagsAssetPath)); }
            catch { return false; }
            GameplayTagsCompiler.ParseSource(json, out bool registered, out string[] tags);
            if (!registered || tags.Length == 0) return false;

            ulong want = ContentHash(GameplayTagsCompiler.BuildEntries(tags));
            string outPath = GeneratedPathFor(gptagsAssetPath);
            if (!File.Exists(outPath)) return true;

            foreach (var line in File.ReadLines(outPath))
            {
                int idx = line.IndexOf(HashMarker, System.StringComparison.Ordinal);
                if (idx < 0) continue;
                string hex = line.Substring(idx + HashMarker.Length).Trim();
                return !(ulong.TryParse(hex, System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture, out ulong have) && have == want);
            }
            return true; // no marker → regenerate
        }

        /// <summary>How many registered <c>.gptags</c> in the project have out-of-date generated code.</summary>
        public static int CountStaleRegistered()
        {
            int n = 0;
            foreach (var full in Directory.GetFiles(Application.dataPath, "*.gptags", SearchOption.AllDirectories))
            {
                string assetPath = "Assets" + full.Substring(Application.dataPath.Length).Replace('\\', '/');
                if (IsStale(assetPath)) n++;
            }
            return n;
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

        private const string HashMarker = "// gptags-hash:0x";

        /// <summary>
        /// The pure code emitter (no I/O). Produces the accessor class + baked <c>Register()</c> text for a
        /// set of baked entries. Deterministic given the same entries (they arrive parent-before-child).
        /// </summary>
        public static string GenerateSource(string className, string @namespace, CompiledTagEntry[] entries)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated>");
            sb.AppendLine("//   Generated from a .gptags source by GameplayTagsCodeGenerator. DO NOT EDIT.");
            sb.AppendLine("//   Edit the .gptags file and re-run Tools ▸ Heathen ▸ GameplayTags ▸ Generate Tag Code.");
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
            sb.AppendLine($"{ind}    /// <summary>Registers this set's tags (baked literals) into the GameplayTagRegistry.</summary>");
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
