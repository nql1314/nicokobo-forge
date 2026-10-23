using Il2Cpp;

namespace Nicokobo.Forge;

public static partial class ForgeLiquidApi
{
    internal static bool Write(GameItem container, ForgeMachineLiquidSnapshot expected)
    {
        if (expected.TotalParts > expected.CapacityParts || expected.Contents.Any(part => part.Parts < 0 ||
            part.Value < 0 || part.QualityBasis < 0 || part.QualityBasis != null && part.Value == null)) return false;
        bool identified = WaterFeatureHelper.GetWaterFeature(container)?.IsExposed() == true;
        int purity = identified ? WaterHelper.GetWaterPurity(container) : 0;
        WaterHelper.EmptyContainer(container);
        foreach (var part in expected.Contents)
            if (part.Parts > 0) WaterHelper.AddLiquid(container, part.LiquidId, part.Parts);
        // Native empty/add clears identification even when only the volume
        // changes. Re-identify unchanged water, without revealing unknown or
        // genuinely changed mixtures. Refresh before restoring priced liquids.
        if (identified && expected.TotalParts > 0 && WaterHelper.GetWaterPurity(container) == purity)
            WaterFeatureHelper.UpdateWaterFeature(container, true);
        ForgeLiquidValueRuntime.Store(container, expected);
        return Matches(Capture(container), expected);
    }
}
