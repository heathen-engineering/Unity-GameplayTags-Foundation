namespace Heathen.GameplayTags
{
    public enum GameplayTagComparisonOp : byte
    {
        Exists       = 0,   // value != 0
        NotExists    = 1,   // value == 0
        Equal        = 2,   // value == compareValue
        NotEqual     = 3,   // value != compareValue
        Less         = 4,   // value < compareValue
        LessEqual    = 5,   // value <= compareValue
        Greater      = 6,   // value > compareValue
        GreaterEqual = 7,   // value >= compareValue
        IsMemberOf   = 8,   // value (as tag id) is a descendant of CompareTag
        IsParentOf   = 9,   // value (as tag id) is an ancestor of CompareTag
        IsExactly    = 10,  // value (as tag id) equals CompareTag exactly
    }
}
