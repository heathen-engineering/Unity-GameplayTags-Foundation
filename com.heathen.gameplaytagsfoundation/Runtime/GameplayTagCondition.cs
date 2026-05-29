using System;
using System.Collections.Generic;
using UnityEngine;

namespace Heathen.GameplayTags
{
    [Serializable]
    public class GameplayTagCondition
    {
        public GameplayTag Tag;
        public GameplayTagComparisonOp Comparison = GameplayTagComparisonOp.Exists;
        public ulong CompareValue = 1;
        // When Id != 0, the right-hand side is drawn from the collection at runtime
        // instead of the constant CompareValue. Enables tag-vs-tag comparisons.
        public GameplayTag CompareTag;
        public bool ExactMatch = true;
        public GameplayTagLogicOp LogicOp = GameplayTagLogicOp.And;

        public bool Evaluate(GameplayTagCollection collection)
        {
            ulong lhs = GetValue(collection);

            // Tag-identity operators treat lhs as a tag id and compare against CompareTag directly.
            if (Comparison == GameplayTagComparisonOp.IsMemberOf)
                return CompareTag.IsValid && new GameplayTag(lhs).IsChildOf(CompareTag);
            if (Comparison == GameplayTagComparisonOp.IsParentOf)
                return CompareTag.IsValid && new GameplayTag(lhs).IsParentOf(CompareTag);
            if (Comparison == GameplayTagComparisonOp.IsExactly)
                return CompareTag.IsValid && lhs == CompareTag.Id;

            ulong rhs = CompareTag.Id != 0 ? collection.GetValue(CompareTag) : CompareValue;
            return Comparison switch
            {
                GameplayTagComparisonOp.Exists       => lhs != 0,
                GameplayTagComparisonOp.NotExists    => lhs == 0,
                GameplayTagComparisonOp.Equal        => lhs == rhs,
                GameplayTagComparisonOp.NotEqual     => lhs != rhs,
                GameplayTagComparisonOp.Less         => lhs < rhs,
                GameplayTagComparisonOp.LessEqual    => lhs <= rhs,
                GameplayTagComparisonOp.Greater      => lhs > rhs,
                GameplayTagComparisonOp.GreaterEqual => lhs >= rhs,
                _                                    => false,
            };
        }

        private ulong GetValue(GameplayTagCollection collection)
        {
            if (ExactMatch)
                return collection.GetValue(Tag);

            // Non-exact: return the max value across the tag and all descendants
            ulong max = collection.GetValue(Tag);
            foreach (var descId in GameplayTagRegistry.GetDescendants(Tag.Id))
            {
                var v = collection.GetValue(new GameplayTag(descId));
                if (v > max) max = v;
            }
            return max;
        }

        // AND > OR > XOR precedence. Empty list = true.
        public static bool EvaluateAll(IList<GameplayTagCondition> conditions, GameplayTagCollection collection)
        {
            if (conditions == null || conditions.Count == 0) return true;

            // Phase 1 — collapse AND chains
            var phase2 = new List<(bool value, GameplayTagLogicOp op)>();
            int i = 0;
            while (i < conditions.Count)
            {
                bool andResult = conditions[i].Evaluate(collection);
                GameplayTagLogicOp nextOp = i < conditions.Count - 1
                    ? conditions[i].LogicOp : GameplayTagLogicOp.Or;

                while (nextOp == GameplayTagLogicOp.And && i + 1 < conditions.Count)
                {
                    i++;
                    andResult = andResult && conditions[i].Evaluate(collection);
                    nextOp = i < conditions.Count - 1
                        ? conditions[i].LogicOp : GameplayTagLogicOp.Or;
                }

                phase2.Add((andResult, nextOp));
                i++;
            }

            if (phase2.Count == 1) return phase2[0].value;

            // Phase 2 — collapse OR chains
            var phase3 = new List<bool>();
            int j = 0;
            while (j < phase2.Count)
            {
                bool orResult = phase2[j].value;
                GameplayTagLogicOp op = phase2[j].op;

                while (op == GameplayTagLogicOp.Or && j + 1 < phase2.Count)
                {
                    j++;
                    orResult = orResult || phase2[j].value;
                    op = phase2[j].op;
                }

                phase3.Add(orResult);
                j++;
            }

            if (phase3.Count == 1) return phase3[0];

            // Phase 3 — XOR remaining
            bool xorResult = phase3[0];
            for (int k = 1; k < phase3.Count; k++)
                xorResult ^= phase3[k];
            return xorResult;
        }
    }
}
