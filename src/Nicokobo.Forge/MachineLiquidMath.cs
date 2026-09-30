namespace Nicokobo.Forge;

internal static class MachineLiquidMath
{
    internal const int PartsPerMillilitre = 1000;

    internal static int PartsPerOutput(int millilitres)
    {
        if (millilitres <= 0 || millilitres >
            int.MaxValue / PartsPerMillilitre)
            throw new ArgumentOutOfRangeException(nameof(millilitres));
        return millilitres * PartsPerMillilitre;
    }

    internal static int AvailableOutputs(int target, int produced,
        int availableParts, int partsPerOutput)
    {
        if (target <= 0 || produced < 0 || produced >= target ||
            availableParts < 0 || partsPerOutput <= 0)
            throw new ArgumentOutOfRangeException(nameof(target));
        return Math.Min(target - produced,
            availableParts / partsPerOutput);
    }

    internal static int DebitParts(int outputCount, int partsPerOutput)
    {
        if (outputCount <= 0 || partsPerOutput <= 0 ||
            outputCount > int.MaxValue / partsPerOutput)
            throw new ArgumentOutOfRangeException(nameof(outputCount));
        return outputCount * partsPerOutput;
    }

    /// <summary>Total value of one merged stack. The game stores a per-unit
    /// value plus a unit count, so a stack is priced by its total. Saturation
    /// keeps an absurd count from wrapping; owners bound the target anyway.</summary>
    internal static long MergedStackValue(long unitValue, int unitCount)
    {
        long units = Math.Max(1, unitCount);
        if (unitValue <= 0) return unitValue;
        return unitValue > long.MaxValue / units
            ? long.MaxValue : unitValue * units;
    }
}
