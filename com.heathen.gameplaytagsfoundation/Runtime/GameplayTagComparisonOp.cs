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
    }
}
