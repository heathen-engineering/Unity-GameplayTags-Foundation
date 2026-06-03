using System;
using System.Collections.Generic;
using UnityEngine;

namespace Heathen.GameplayTags
{
    /// <summary>
    /// A serialisable rule that compares a <see cref="GameplayTag"/>'s value within a
    /// <see cref="GameplayTagCollection"/> against a constant or another tag's value.
    /// Multiple conditions are combined via <see cref="LogicOp"/> using AND-before-OR-before-XOR
    /// precedence when evaluated through <see cref="EvaluateAll"/>.
    /// </summary>
    [Serializable]
    public class GameplayTagCondition
    {
        /// <summary>The tag whose value is read from the collection as the left-hand operand.</summary>
        public GameplayTag Tag;

        /// <summary>The comparison operator applied between the tag's value and the compare value.</summary>
        public GameplayTagComparisonOp Comparison = GameplayTagComparisonOp.Exists;

        /// <summary>
        /// The constant right-hand operand used when <see cref="CompareTag"/> is not set.
        /// Interpreted according to <see cref="CompareValueType"/>.
        /// </summary>
        public ulong CompareValue = 1;

        /// <summary>
        /// When valid, the right-hand side of the comparison is resolved from this tag's value
        /// in the collection at evaluation time rather than using the constant <see cref="CompareValue"/>.
        /// This enables tag-versus-tag comparisons.
        /// </summary>
        public GameplayTag CompareTag;

        /// <summary>
        /// When <c>true</c>, only the exact tag is read; when <c>false</c>, the maximum value
        /// across the tag and all of its registered descendants is used as the left-hand operand.
        /// </summary>
        public bool ExactMatch = true;

        /// <summary>
        /// The logical operator used to combine this condition's result with the next condition
        /// in a list evaluated by <see cref="EvaluateAll"/>. AND has highest precedence,
        /// followed by OR, then XOR.
        /// </summary>
        public GameplayTagLogicOp LogicOp = GameplayTagLogicOp.And;

        /// <summary>
        /// Controls how <see cref="CompareValue"/> (and the tag's stored value) are interpreted
        /// during numeric comparisons. Tag-identity operators (<see cref="GameplayTagComparisonOp.IsMemberOf"/> etc.)
        /// are unaffected by this setting.
        /// </summary>
        public GameplayTagValueType CompareValueType = GameplayTagValueType.Unsigned;

        /// <summary>
        /// Evaluates this condition against the provided <paramref name="collection"/> and
        /// returns whether the condition passes. The result is determined by reading the tag's
        /// value, applying the comparison operator, and respecting the value type interpretation.
        /// </summary>
        /// <param name="collection">The collection to read tag values from.</param>
        /// <returns><c>true</c> if this condition's comparison passes; otherwise <c>false</c>.</returns>
        public bool Evaluate(GameplayTagCollection collection)
        {
            ulong lhsRaw = GetValue(collection);

            // Tag-identity operators treat lhsRaw as a tag id — not affected by CompareValueType.
            if (Comparison == GameplayTagComparisonOp.IsMemberOf)
                return CompareTag.IsValid && new GameplayTag(lhsRaw).IsChildOf(CompareTag);
            if (Comparison == GameplayTagComparisonOp.IsParentOf)
                return CompareTag.IsValid && new GameplayTag(lhsRaw).IsParentOf(CompareTag);
            if (Comparison == GameplayTagComparisonOp.IsExactly)
                return CompareTag.IsValid && lhsRaw == CompareTag.Id;

            // Exists/NotExists are presence checks — not affected by value type.
            if (Comparison == GameplayTagComparisonOp.Exists)    return lhsRaw != 0;
            if (Comparison == GameplayTagComparisonOp.NotExists) return lhsRaw == 0;

            ulong rhsRaw = CompareTag.Id != 0 ? collection.GetValue(CompareTag) : CompareValue;
            switch (CompareValueType)
            {
                case GameplayTagValueType.Signed:
                {
                    long lhs = (long)lhsRaw;
                    long rhs = (long)rhsRaw;
                    return Comparison switch {
                        GameplayTagComparisonOp.Equal        => lhs == rhs,
                        GameplayTagComparisonOp.NotEqual     => lhs != rhs,
                        GameplayTagComparisonOp.Less         => lhs < rhs,
                        GameplayTagComparisonOp.LessEqual    => lhs <= rhs,
                        GameplayTagComparisonOp.Greater      => lhs > rhs,
                        GameplayTagComparisonOp.GreaterEqual => lhs >= rhs,
                        _                                    => false,
                    };
                }
                case GameplayTagValueType.Decimal:
                {
                    double lhs = System.BitConverter.Int64BitsToDouble((long)lhsRaw);
                    double rhs = System.BitConverter.Int64BitsToDouble((long)rhsRaw);
                    return Comparison switch {
                        GameplayTagComparisonOp.Equal        => lhs.Equals(rhs),
                        GameplayTagComparisonOp.NotEqual     => !lhs.Equals(rhs),
                        GameplayTagComparisonOp.Less         => lhs < rhs,
                        GameplayTagComparisonOp.LessEqual    => lhs <= rhs,
                        GameplayTagComparisonOp.Greater      => lhs > rhs,
                        GameplayTagComparisonOp.GreaterEqual => lhs >= rhs,
                        _                                    => false,
                    };
                }
                default: // Unsigned or Tag (CompareTag already baked into rhsRaw above)
                    return Comparison switch {
                        GameplayTagComparisonOp.Equal        => lhsRaw == rhsRaw,
                        GameplayTagComparisonOp.NotEqual     => lhsRaw != rhsRaw,
                        GameplayTagComparisonOp.Less         => lhsRaw < rhsRaw,
                        GameplayTagComparisonOp.LessEqual    => lhsRaw <= rhsRaw,
                        GameplayTagComparisonOp.Greater      => lhsRaw > rhsRaw,
                        GameplayTagComparisonOp.GreaterEqual => lhsRaw >= rhsRaw,
                        _                                    => false,
                    };
            }
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

        /// <summary>
        /// Evaluates a list of conditions against <paramref name="collection"/> using
        /// AND-before-OR-before-XOR operator precedence across the sequence. An empty or
        /// <c>null</c> list returns <c>true</c>.
        /// </summary>
        /// <param name="conditions">The ordered list of conditions to evaluate.</param>
        /// <param name="collection">The collection to read tag values from.</param>
        /// <returns><c>true</c> if the combined result of all conditions passes; otherwise <c>false</c>.</returns>
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
