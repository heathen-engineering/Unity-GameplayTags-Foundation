namespace Heathen.GameplayTags
{
    /// <summary>
    /// Specifies the logical operator used to combine a <see cref="GameplayTagCondition"/> with
    /// the next condition in a sequence evaluated by
    /// <see cref="GameplayTagCondition.EvaluateAll"/>. Operators are applied with
    /// AND having the highest precedence, followed by OR, then XOR.
    /// </summary>
    public enum GameplayTagLogicOp : byte
    {
        /// <summary>Both this condition and the next must pass (highest precedence).</summary>
        And = 0,
        /// <summary>Either this condition or the next must pass.</summary>
        Or = 1,
        /// <summary>Exactly one of the two grouped results must pass (lowest precedence).</summary>
        Xor = 2,
    }
}
