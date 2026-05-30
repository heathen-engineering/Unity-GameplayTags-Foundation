using System;
using System.Collections.Generic;
using UnityEngine;

namespace Heathen.GameplayTags
{
    [Serializable]
    public class GameplayTagOperation
    {
        public GameplayTag Tag;
        public GameplayTagArithmetic Arithmetic = GameplayTagArithmetic.Set;
        public ulong Value = 1;
        // When valid, the operand is resolved from the collection at apply time instead of using Value.
        public GameplayTag ValueTag;
        // Controls how Value is interpreted and how arithmetic is performed.
        public GameplayTagValueType ValueType = GameplayTagValueType.Unsigned;
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
