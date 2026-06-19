using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;

namespace Heathen.GameplayTags.Tests
{
    // Behaviour + parity coverage for interval (nested-set) encoding (Sprint 1 / B1).
    // Tests register under a unique root each time so they never collide in the shared static registry.
    public class GameplayTagIntervalTests
    {
        private static string FreshRoot() => "T" + Guid.NewGuid().ToString("N");

        private static GameplayTag Reg(string path)
        {
            GameplayTagRegistry.Register(path);
            return GameplayTag.FromName(path);
        }

        [Test]
        public void Ancestry_BasicContainment_AnyDepth_AndSelfExcluded()
        {
            var r   = FreshRoot();
            var a   = Reg($"{r}.A");
            var ab  = Reg($"{r}.A.B");
            var abc = Reg($"{r}.A.B.C");
            var ad  = Reg($"{r}.A.D");

            Assert.IsTrue(a.IsParentOf(ab),  "immediate parent");
            Assert.IsTrue(ab.IsChildOf(a));
            Assert.IsTrue(a.IsParentOf(abc), "ancestor at any depth");
            Assert.IsTrue(abc.IsChildOf(a));

            Assert.IsFalse(ab.IsParentOf(ad), "sibling subtrees do not contain each other");
            Assert.IsFalse(ad.IsChildOf(ab));

            Assert.IsFalse(a.IsChildOf(a),  "a tag is not its own descendant");
            Assert.IsFalse(a.IsParentOf(a), "a tag is not its own ancestor");
        }

        [Test]
        public void GetDescendants_ReturnsWholeSubtree_ExcludingSelf()
        {
            var r   = FreshRoot();
            var a   = Reg($"{r}.A");
            var ab  = Reg($"{r}.A.B");
            var abc = Reg($"{r}.A.B.C");
            var ad  = Reg($"{r}.A.D");

            var desc = new HashSet<ulong>(GameplayTagRegistry.GetDescendants(a.Id));
            Assert.IsTrue(desc.Contains(ab.Id));
            Assert.IsTrue(desc.Contains(abc.Id));
            Assert.IsTrue(desc.Contains(ad.Id));
            Assert.IsFalse(desc.Contains(a.Id));
            Assert.AreEqual(3, desc.Count);
        }

        [Test]
        public void Rollup_MaxAcrossDescendants_AndHierarchicalContains()
        {
            var r       = FreshRoot();
            var debuffs = Reg($"{r}.Effects.Debuffs");
            var poison  = Reg($"{r}.Effects.Debuffs.Poison");
            var burn    = Reg($"{r}.Effects.Debuffs.Burn");

            var c = new GameplayTagCollection();
            c.Apply(poison, GameplayTagArithmetic.Set, 3ul);
            c.Apply(burn,   GameplayTagArithmetic.Set, 7ul);

            Assert.AreEqual(7ul, c.GetMaxValueUnder(debuffs), "max across descendants present");
            Assert.AreEqual(0ul, c.GetValue(debuffs),         "exact value absent");
            Assert.IsTrue(c.Contains(debuffs),                "hierarchical contains");
            Assert.IsFalse(c.Contains(debuffs, exactMatch: true));
        }

        [Test]
        public void Rollup_IncludesTheExactTagValue()
        {
            var r     = FreshRoot();
            var d     = Reg($"{r}.D");
            var child = Reg($"{r}.D.C");

            var c = new GameplayTagCollection();
            c.Apply(d,     GameplayTagArithmetic.Set, 5ul);
            c.Apply(child, GameplayTagArithmetic.Set, 2ul);

            Assert.AreEqual(5ul, c.GetMaxValueUnder(d));
        }

        [Test]
        public void RuntimeRegister_RebuildsIntervals_AndBumpsGeneration()
        {
            var r  = FreshRoot();
            var a  = Reg($"{r}.A");
            var ab = Reg($"{r}.A.B");

            var before = GameplayTagRegistry.IntervalGeneration;
            var abc = Reg($"{r}.A.B.C"); // forces a rebuild

            Assert.Greater(GameplayTagRegistry.IntervalGeneration, before);
            Assert.IsTrue(a.IsParentOf(abc),  "newly registered node placed under existing ancestor");
            Assert.IsTrue(ab.IsParentOf(abc));
        }

        [Test]
        public void MultiSet_Merge_CrossAssetAncestry()
        {
            // Two assets: one defines the upper path, the other a deeper node and a separate root.
            var r = FreshRoot();
            var upper = MakeEntries(
                ($"{r}.A", 0),
                ($"{r}.A.B", Hash($"{r}.A")));
            var lower = MakeEntries(
                ($"{r}.A.B.C", Hash($"{r}.A.B")),
                ($"{r}.X", 0));

            GameplayTagRegistry.RegisterBaked(upper);
            GameplayTagRegistry.RegisterBaked(lower);

            Assert.IsTrue(GameplayTag.FromName($"{r}.A").IsParentOf(GameplayTag.FromName($"{r}.A.B.C")),
                "ancestry resolves across set boundaries");
            Assert.IsFalse(GameplayTag.FromName($"{r}.A").IsParentOf(GameplayTag.FromName($"{r}.X")),
                "separate root is not under A");
        }

        [Test]
        public void OrphanParent_TreatedAsRoot_NoThrow()
        {
            var orphanChild = "Orphan" + Guid.NewGuid().ToString("N") + ".Child";
            var childId     = GameplayTagRegistry.Hash(orphanChild);
            const ulong danglingParent = 123456789UL; // never registered

            var data = MakeEntries((orphanChild, danglingParent));
            Assert.DoesNotThrow(() => GameplayTagRegistry.RegisterBaked(data));
            Assert.IsTrue(GameplayTagRegistry.IsRegistered(childId));
            Assert.IsFalse(GameplayTagRegistry.IsAncestor(danglingParent, childId),
                "an unregistered dangling parent is not an ancestor");
        }

        [Test]
        public void Unregistered_Tier0_HasNoHierarchy_ButEqualityWorks()
        {
            var x = GameplayTag.FromName("Z" + Guid.NewGuid().ToString("N") + ".Nope");
            var y = GameplayTag.FromName("Z" + Guid.NewGuid().ToString("N") + ".Nada");

            Assert.IsFalse(GameplayTagRegistry.IsRegistered(x.Id));
            Assert.IsFalse(x.IsParentOf(y));
            Assert.IsFalse(x.IsChildOf(y));
            Assert.IsFalse(x.Equals(y));
            Assert.IsTrue(x.Equals(x));
        }

        [Test]
        public void NativeIntervalMap_MatchesManagedAncestry()
        {
            var r   = FreshRoot();
            var a   = Reg($"{r}.A");
            var abc = Reg($"{r}.A.B.C");

            var map = GameplayTagRegistry.GetNativeIntervalMap(Allocator.Temp);
            try
            {
                Assert.AreEqual(GameplayTagRegistry.IntervalGeneration, map.Generation);
                Assert.IsTrue(map.IsAncestor(a.Id, abc.Id));
                Assert.IsFalse(map.IsAncestor(abc.Id, a.Id));
            }
            finally
            {
                map.Dispose();
            }
        }

        // ── helpers ──────────────────────────────────────────────────────────

        private static ulong Hash(string path) => GameplayTagRegistry.Hash(path);

        // Build a baked-entry set the way the .gptags code generator would (SO-free), for RegisterBaked.
        private static CompiledTagEntry[] MakeEntries(params (string path, ulong parentId)[] entries)
        {
            var arr = new CompiledTagEntry[entries.Length];
            for (int i = 0; i < entries.Length; i++)
                arr[i] = new CompiledTagEntry
                {
                    Id = Hash(entries[i].path),
                    Name = entries[i].path,
                    ParentId = entries[i].parentId,
                };
            return arr;
        }
    }
}
