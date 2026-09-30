namespace Nicokobo.Forge;

internal static class MachineLiquidMath
{
    internal const int PartsPerMillilitre = 1000;
    internal static int ToParts(int millilitres)
    {
        if (millilitres <= 0 || millilitres > int.MaxValue / PartsPerMillilitre)
            throw new ArgumentOutOfRangeException(nameof(millilitres));
        return millilitres * PartsPerMillilitre;
    }
}
