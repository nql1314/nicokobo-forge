using Il2Cpp;

namespace Nicokobo.Forge;

public static class ForgeLiquidApi
{
    public static bool IsContainer(GameItem item) => item != null && item.Pointer != IntPtr.Zero &&
        item.unitCount == 1 && WaterHelper.CanUseWaterContainer(item) && WaterHelper.GetCurrentCapacity(item) > 0;

    public static ForgeMachineLiquidSnapshot? Capture(GameItem container)
    {
        if (!IsContainer(container) || Liquid.Liquids == null) return null;
        var parts = new List<ForgeMachineLiquidPart>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        long total = 0;
        for (int index = 0; index < Liquid.Liquids.Count; index++)
        {
            var liquid = Liquid.Liquids[index];
            if (liquid == null || string.IsNullOrWhiteSpace(liquid.identifier) ||
                string.IsNullOrWhiteSpace(liquid.currentPart) || !ids.Add(liquid.identifier)) return null;
            int amount = container.GetTagReadonly(liquid.currentPart)?.valueInt ?? 0;
            if (amount < 0 || (total += amount) > int.MaxValue) return null;
            parts.Add(new(liquid.identifier, amount));
        }
        int capacity = WaterHelper.GetCurrentCapacity(container);
        return capacity >= total && WaterHelper.GetTotalVolume(container) == total
            ? new(capacity, Array.AsReadOnly(parts.ToArray())) : null;
    }

    /// <summary>0 unknown/unsafe, 1 base, 2 high quality, 3 pure.</summary>
    public static int WaterQuality(GameItem container)
    {
        var id = WaterFeatureHelper.GetConditionFromPurity(WaterHelper.GetWaterPurity(container))?.identifier;
        if (id == ItemConditionList.CreatePureWater().identifier) return 3;
        if (id == ItemConditionList.CreateHighQualityWater().identifier) return 2;
        return id == ItemConditionList.CreateBaseWater().identifier ? 1 : 0;
    }

    internal static bool Matches(ForgeMachineLiquidSnapshot? current, ForgeMachineLiquidSnapshot expected) =>
        current != null && current.CapacityParts == expected.CapacityParts &&
        current.Contents.SequenceEqual(expected.Contents);

    internal static bool Write(GameItem container, ForgeMachineLiquidSnapshot expected)
    {
        if (expected.TotalParts > expected.CapacityParts || expected.Contents.Any(part => part.Parts < 0)) return false;
        WaterHelper.EmptyContainer(container);
        foreach (var part in expected.Contents)
            if (part.Parts > 0) WaterHelper.AddLiquid(container, part.LiquidId, part.Parts);
        return Matches(Capture(container), expected);
    }
}
