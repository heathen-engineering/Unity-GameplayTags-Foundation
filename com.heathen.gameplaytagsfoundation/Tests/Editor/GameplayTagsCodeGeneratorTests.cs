using NUnit.Framework;
using Heathen.GameplayTags.Editor;

namespace Heathen.GameplayTags.Tests
{
    // S2 of GameplayTags-CodeGen-Spec: the pure codegen core (sanitiser + source emitter). File I/O
    // (Generate/GenerateAll) is exercised by hand via the menu item; this covers the deterministic string
    // output a build depends on.
    public class GameplayTagsCodeGeneratorTests
    {
        [Test]
        public void SanitizeMember_MakesValidIdentifiers()
        {
            Assert.AreEqual("A_B_C", GameplayTagsCodeGenerator.SanitizeMember("A.B.C"));
            Assert.AreEqual("a_b",   GameplayTagsCodeGenerator.SanitizeMember("a-b"));
            Assert.AreEqual("_1Bad", GameplayTagsCodeGenerator.SanitizeMember("1Bad")); // leading digit
            Assert.AreEqual("_",     GameplayTagsCodeGenerator.SanitizeMember(""));
        }

        [Test]
        public void BuildEntries_SynthesisesPrefixNodes_ParentBeforeChild()
        {
            var entries = GameplayTagsCompiler.BuildEntries(new[] { "Dialogue.Act1.Node1" });
            // Three nodes: Dialogue, Dialogue.Act1, Dialogue.Act1.Node1 — parents first.
            Assert.AreEqual(3, entries.Length);
            Assert.AreEqual("Dialogue", entries[0].Name);
            Assert.AreEqual("Dialogue.Act1", entries[1].Name);
            Assert.AreEqual("Dialogue.Act1.Node1", entries[2].Name);
            // Parent links + baked Ids match the runtime hash.
            Assert.AreEqual(0UL, entries[0].ParentId);
            Assert.AreEqual(entries[0].Id, entries[1].ParentId);
            Assert.AreEqual(entries[1].Id, entries[2].ParentId);
            Assert.AreEqual(GameplayTagRegistry.Hash("Dialogue.Act1.Node1"), entries[2].Id);
        }

        [Test]
        public void GenerateSource_EmitsAccessorsBakedDataAndRegister()
        {
            var entries = GameplayTagsCompiler.BuildEntries(new[] { "Dialogue.Act1.Node1" });
            string src = GameplayTagsCodeGenerator.GenerateSource("Dialogue", "Heathen.GameplayTags.Generated", entries);

            // Accessor for the leaf, with the baked Id literal + source-path comment.
            ulong leafId = GameplayTagRegistry.Hash("Dialogue.Act1.Node1");
            StringAssert.Contains(
                $"public static readonly GameplayTag Dialogue_Act1_Node1 = GameplayTag.FromId(0x{leafId:X16}UL);",
                src);
            // Baked registration data + the auto-registering Register().
            StringAssert.Contains("static readonly CompiledTagEntry[] _baked", src);
            StringAssert.Contains("[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]", src);
            StringAssert.Contains("public static void Register() => GameplayTagRegistry.RegisterBaked(_baked);", src);
            StringAssert.Contains("public static class Dialogue", src);
            StringAssert.Contains("namespace Heathen.GameplayTags.Generated", src);
        }

        [Test]
        public void GenerateSource_EmbedsContentHashMarker()
        {
            var entries = GameplayTagsCompiler.BuildEntries(new[] { "A.B" });
            ulong h = GameplayTagsCodeGenerator.ContentHash(entries);
            string src = GameplayTagsCodeGenerator.GenerateSource("A", "NS", entries);
            // The embedded marker is exactly ContentHash(entries) — what IsStale recomputes + compares.
            StringAssert.Contains($"// gptags-hash:0x{h:X16}", src);
        }

        [Test]
        public void ContentHash_StableForSameTags_DiffersForDifferent()
        {
            var a  = GameplayTagsCompiler.BuildEntries(new[] { "A.B" });
            var a2 = GameplayTagsCompiler.BuildEntries(new[] { "A.B" });
            var b  = GameplayTagsCompiler.BuildEntries(new[] { "A.C" });
            Assert.AreEqual(GameplayTagsCodeGenerator.ContentHash(a), GameplayTagsCodeGenerator.ContentHash(a2));
            Assert.AreNotEqual(GameplayTagsCodeGenerator.ContentHash(a), GameplayTagsCodeGenerator.ContentHash(b));
        }
    }
}
