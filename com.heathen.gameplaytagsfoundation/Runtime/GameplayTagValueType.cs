namespace Heathen.GameplayTags
{
    public enum GameplayTagValueType : byte
    {
        Unsigned = 0,  // ulong, stored and compared directly (default)
        Signed   = 1,  // long, stored as two's complement bits in ulong
        Decimal  = 2,  // double, stored as IEEE 754 bits in ulong
        Tag      = 3,  // value resolved from a GameplayTag in the collection at runtime
    }
}
