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
        public List<GameplayTagCondition> Conditions = new();

        // Returns true if conditions are met (or there are none).
        public bool ShouldApply(GameplayTagCollection collection) =>
            GameplayTagCondition.EvaluateAll(Conditions, collection);

        // Applies if conditions are met. Returns true if the operation was applied.
        public bool Apply(GameplayTagCollection collection)
        {
            if (!ShouldApply(collection)) return false;
            collection.Apply(Tag, Arithmetic, Value);
            return true;
        }
    }
}
