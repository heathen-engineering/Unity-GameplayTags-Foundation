namespace Heathen.GameplayTags
{
    /// <summary>
    /// Specifies the arithmetic operation to apply when mutating the numeric value associated
    /// with a tag in a <see cref="GameplayTagCollection"/>. Used by
    /// <see cref="GameplayTagCollection.Apply(GameplayTag, GameplayTagArithmetic, ulong)"/> and
    /// <see cref="GameplayTagOperation"/>.
    /// </summary>
    public enum GameplayTagArithmetic : byte
    {
        /// <summary>Replaces the current value with the supplied operand.</summary>
        Set = 0,
        /// <summary>Adds the operand to the current value.</summary>
        Add = 1,
        /// <summary>Subtracts the operand from the current value, clamping to zero for unsigned storage.</summary>
        Subtract = 2,
        /// <summary>Multiplies the current value by the operand.</summary>
        Multiply = 3,
        /// <summary>Divides the current value by the operand. Division by zero is a no-op.</summary>
        Divide = 4,
        /// <summary>Sets the value to the lesser of the current value and the operand.</summary>
        Min = 5,
        /// <summary>Sets the value to the greater of the current value and the operand.</summary>
        Max = 6,
    }
}
