using System.Collections.Generic;
using UnityEngine;

namespace Heathen.GameplayTags
{
    [AddComponentMenu("Heathen/Gameplay Tags/Gameplay Tags Manager")]
    public class GameplayTagsManager : MonoBehaviour
    {
        public GameplayTagCollection Tags = new();

        public void ApplyOperation(GameplayTagOperation op) => op?.Apply(Tags);

        public void ApplyOperations(List<GameplayTagOperation> ops)
        {
            foreach (var op in ops)
                op?.Apply(Tags);
        }

        public bool EvaluateConditions(List<GameplayTagCondition> conditions) =>
            GameplayTagCondition.EvaluateAll(conditions, Tags);
    }
}
