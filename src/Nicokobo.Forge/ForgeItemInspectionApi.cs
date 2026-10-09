using Il2Cpp;

namespace Nicokobo.Forge;

/// <summary>Use the actual instance and the ordinary nontrade tooltip value
/// path. No factory samples, temporary inventories or copied attributes.</summary>
public static class ForgeItemInspectionApi
{
    public static long? CurrentUnitValue(GameItem item)
    {
        if (!ForgeCapabilities.Current.KnownGameBuild || item == null || item.Pointer == IntPtr.Zero) return null;
        try { return item.GetCurrentValue(false, true, true, false, 0); }
        catch { return null; }
    }
    public static RichText BuildTooltip(GameItem item)
    {
        if (!ForgeCapabilities.Current.KnownGameBuild || item == null || item.Pointer == IntPtr.Zero)
            throw new InvalidOperationException("Actual item unavailable");
        var element = item.TryCast<GameItemElement>() ?? throw new InvalidOperationException("Native tooltip element unavailable");
        return element.GetTooltipBasic().Build();
    }
}
