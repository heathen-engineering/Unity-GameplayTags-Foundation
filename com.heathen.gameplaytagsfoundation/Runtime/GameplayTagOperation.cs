using System;
using System.Collections.Generic;
using UnityEngine;

namespace Heathen.GameplayTags
{
    /// <summary>
    /// A serialisable, conditional mutation that applies an arithmetic operation to a tag's
    /// value in a <see cref="GameplayTagCollection"/>. The operation is only executed when
    /// all entries in <see cref="Conditions"/> evaluate to <c>true</c>.
    /// </summary>
    [Serializable]
    public class GameplayTagOperation
    {
        /// <summary>The tag in the collection whose value will be mutated by this operation.</summary>
        public GameplayTag Tag;

        /// <summary>The arithmetic operator to apply to the tag's current value.</summary>
        public GameplayTagArithmetic Arithmetic = GameplayTagArithmetic.Set;

        /// <summary>
        /// The constant operand used when <see cref="ValueTag"/> is not set.
        /// Interpreted according to <see cref="ValueType"/>.
        /// </summary>
        public ulong Value = 1;

        /// <summary>
        /// When valid, the operand is resolved from this tag's value in the collection at
        /// apply time, overriding the constant <see cref="Value"/>.
        /// </summary>
        public GameplayTag ValueTag;

        /// <summary>
        /// Controls how <see cref="Value"/> is interpreted and how typed arithmetic is performed.
        /// Has no effect on <see cref="GameplayTagArithmetic"/> when <see cref="ValueTag"/> is used.
        /// </summary>
        public GameplayTagValueType ValueType = GameplayTagValueType.Unsigned;

        /// <summary>
        /// An optional list of conditions that must all pass before this operation is applied.
        /// An empty list means the operation is applied unconditionally.
        /// </summary>
        public List<GameplayTagCondition> Conditions = new();

        // Returns true if conditions are met (or there are none).
        public bool ShouldApply(GameplayTagCollection collection) =>
            GameplayTagCondition.EvaluateAll(Conditions, collection);

        // Applies if conditions are met. Returns true if the operation was applied.
        public bool Apply(GameplayTagCollection collection)
        {
            if (!ShouldApply(collection)) return false;
            switch (ValueType)
            {
                case GameplayTagValueType.Signed:
                {
                    long current = collection.GetLong(Tag);
                    long operand = (long)Value;
                    long result  = Arithmetic switch {
                        GameplayTagArithmetic.Set      => operand,
                        GameplayTagArithmetic.Add      => current + operand,
                        GameplayTagArithmetic.Subtract => current - operand,
                        GameplayTagArithmetic.Multiply => current * operand,
                        GameplayTagArithmetic.Divide   => operand != 0 ? current / operand : current,
                        GameplayTagArithmetic.Min      => Math.Min(current, operand),
                        GameplayTagArithmetic.Max      => Math.Max(current, operand),
                        _                              => current,
                    };
                    collection.SetLong(Tag, result);
                    break;
                }
                case GameplayTagValueType.Decimal:
                {
                    double current = collection.GetDouble(Tag);
                    double operand = System.BitConverter.Int64BitsToDouble((long)Value);
                    double result  = Arithmetic switch {
                        GameplayTagArithmetic.Set      => operand,
                        GameplayTagArithmetic.Add      => current + operand,
                        GameplayTagArithmetic.Subtract => current - operand,
                        GameplayTagArithmetic.Multiply => current * operand,
                        GameplayTagArithmetic.Divide   => operand != 0.0 ? current / operand : current,
                        GameplayTagArithmetic.Min      => Math.Min(current, operand),
                        GameplayTagArithmetic.Max      => Math.Max(current, operand),
                        _                              => current,
                    };
                    collection.SetDouble(Tag, result);
                    break;
                }
                default: // Unsigned or Tag (ValueTag takes precedence over Value when valid)
                {
                    ulong operand = ValueTag.IsValid ? collection.GetValue(ValueTag) : Value;
                    collection.Apply(Tag, Arithmetic, operand);
                    break;
                }
            }
            return true;
        }
    }
}
