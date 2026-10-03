namespace Nicokobo.Forge;

/// <summary>Content supplies the markup. Calculations exclude retained containers
/// and machine power, and reject overflow before the resource transaction.</summary>
public static class ForgeProductionValueMath
{
    public static long PerOutput(decimal inputValue, int markupPercent, int outputCount = ForgeNumbers.Machines.DefaultOutputCount)
    {
        if (inputValue < 0 || markupPercent < 0 || outputCount is < 1 or > ForgeNumbers.Machines.MaxOutputCount)
            throw new ArgumentOutOfRangeException(nameof(inputValue));
        decimal value = decimal.Ceiling(checked(inputValue * (100m + markupPercent) / 100m / outputCount));
        return value <= long.MaxValue ? (long)value : throw new OverflowException("Production value too large");
    }

    // Native liquid has 1000 parts/ml and a volume base price of 0.04/ml.
    internal static decimal NativeLiquidValue(int parts) => parts >= 0
        ? parts / ForgeNumbers.NativeUnits.LiquidPartsPerCredit : throw new ArgumentOutOfRangeException(nameof(parts));

    internal static decimal RemainingLiquidValue(decimal value, int beforeParts, int afterParts)
    {
        if (value < 0 || beforeParts <= 0 || afterParts < 0 || afterParts > beforeParts)
            throw new ArgumentOutOfRangeException(nameof(afterParts));
        return afterParts == 0 ? 0 : value * afterParts / beforeParts;
    }
}
