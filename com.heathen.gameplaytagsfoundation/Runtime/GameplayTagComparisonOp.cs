namespace Heathen.GameplayTags
{
    /// <summary>
    /// Specifies the comparison operator used by a <see cref="GameplayTagCondition"/> when
    /// evaluating a tag's value against a constant or another tag's value in a
    /// <see cref="GameplayTagCollection"/>. The interpretation of the stored value is further
    /// controlled by <see cref="GameplayTagValueType"/>.
    /// </summary>
    public enum GameplayTagComparisonOp : byte
    {
        /// <summary>Passes when the tag's stored value is non-zero (the tag is present).</summary>
        Exists       = 0,
        /// <summary>Passes when the tag's stored value is zero (the tag is absent).</summary>
        NotExists    = 1,
        /// <summary>Passes when the tag's value equals the compare value.</summary>
        Equal        = 2,
        /// <summary>Passes when the tag's value does not equal the compare value.</summary>
        NotEqual     = 3,
        /// <summary>Passes when the tag's value is strictly less than the compare value.</summary>
        Less         = 4,
        /// <summary>Passes when the tag's value is less than or equal to the compare value.</summary>
        LessEqual    = 5,
        /// <summary>Passes when the tag's value is strictly greater than the compare value.</summary>
        Greater      = 6,
        /// <summary>Passes when the tag's value is greater than or equal to the compare value.</summary>
        GreaterEqual = 7,
        /// <summary>Treats the stored value as a tag identifier and passes when it is a descendant of <c>CompareTag</c>.</summary>
        IsMemberOf   = 8,
        /// <summary>Treats the stored value as a tag identifier and passes when it is an ancestor of <c>CompareTag</c>.</summary>
        IsParentOf   = 9,
        /// <summary>Treats the stored value as a tag identifier and passes when it exactly matches <c>CompareTag</c>.</summary>
        IsExactly    = 10,
    }
}
