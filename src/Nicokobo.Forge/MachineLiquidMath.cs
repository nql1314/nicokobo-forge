namespace Nicokobo.Forge;

internal static class MachineLiquidMath
{
    internal const int PartsPerMillilitre = ForgeNumbers.NativeUnits.LiquidPartsPerMillilitre;
    internal static int ToParts(int millilitres)
    {
        if (millilitres <= 0 || millilitres > int.MaxValue / PartsPerMillilitre)
            throw new ArgumentOutOfRangeException(nameof(millilitres));
        return millilitres * PartsPerMillilitre;
    }

    internal static int ToInputParts(int millilitres) => millilitres == 0 ? 0 : ToParts(millilitres);
}
