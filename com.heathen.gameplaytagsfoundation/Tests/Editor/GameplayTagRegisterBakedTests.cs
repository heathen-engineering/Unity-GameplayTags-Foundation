using System;
using NUnit.Framework;

namespace Heathen.GameplayTags.Tests
{
    // S1 of GameplayTags-CodeGen-Spec: the SO-free RegisterBaked path the generator targets, plus the
    // drift guard that the baked Id == the runtime hash of the path (so a generator that bakes
    // GameplayTagRegistry.Hash(path) produces Ids the runtime resolves identically).
    public class GameplayTagRegisterBakedTests
    {
        private static string FreshRoot() => "B" + Guid.NewGuid().ToString("N");

        [Test]
        public void RegisterBaked_RegistersHierarchyNamesAndIntervals()
        {
            string r = FreshRoot();
            ulong idR  = GameplayTagRegistry.Hash(r);
            ulong idA  = GameplayTagRegistry.Hash(r + ".A");
            ulong idAB = GameplayTagRegistry.Hash(r + ".A.B");

            // What the generator would bake (Id / ParentId / Name), with no runtime hashing or parse.
            var baked = new[]
            {
                new CompiledTagEntry { Id = idR,  ParentId = 0,   Name = r },
                new CompiledTagEntry { Id = idA,  ParentId = idR, Name = r + ".A" },
                new CompiledTagEntry { Id = idAB, ParentId = idA, Name = r + ".A.B" },
            };
            GameplayTagRegistry.RegisterBaked(baked);

            var tagR  = GameplayTag.FromName(r);
            var tagA  = GameplayTag.FromName(r + ".A");
            var tagAB = GameplayTag.FromName(r + ".A.B");

            // Hierarchy (interval encoding) is live.
            Assert.IsTrue(tagA.IsChildOf(tagR),  "A is a child of the root");
            Assert.IsTrue(tagR.IsParentOf(tagAB), "root is an ancestor of A.B at any depth");
            Assert.IsFalse(tagR.IsChildOf(tagR), "a tag is not its own descendant");

            // Name map is populated from the baked names (no reconstruction).
            Assert.AreEqual(r + ".A.B", GameplayTagRegistry.GetName(tagAB.Id));
        }

        [Test]
        public void DriftGuard_BakedId_EqualsRuntimeHashOfPath()
        {
            // The generator bakes GameplayTagRegistry.Hash(path); FromName hashes the same way at runtime.
            string r = FreshRoot();
            string path = r + ".A.B";

            ulong bakedId = GameplayTagRegistry.Hash(path);   // what the generator would emit as the literal
            Assert.AreEqual(bakedId, GameplayTag.FromName(path).Id,
                "baked Id must equal the runtime hash of the path — if this fails the hash function changed " +
                "and generated tags would point at the wrong Ids.");
        }
    }
}
