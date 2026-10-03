using System.Reflection;
using Il2Cpp;
using Nicokobo.Forge.Runtime;

namespace Nicokobo.Forge;

// Native innate/final stages retain purity, condition and charge. Market
// features and current-customer/event markups do not become manufacturing cost.
internal static class NativeProductionValue
{
    private static MethodInfo? _accumulate, _compose;

    internal static void Validate()
    {
        _accumulate = NativeHookSet.Require(typeof(GameItem), "AccumulateFeatureStages",
            [typeof(long).MakeByRefType(), typeof(long).MakeByRefType(), typeof(double).MakeByRefType(), typeof(bool)], typeof(void));
        _compose = NativeHookSet.Require(typeof(GameItem), "ComposeStagedValue",
            [typeof(long), typeof(long), typeof(long), typeof(double), typeof(int), typeof(int)], typeof(long));
    }

    private static (long Innate, double Final) Stages(GameItem item)
    {
        object[] args = [0L, 0L, 1d, true];
        (_accumulate ?? throw new InvalidOperationException("Production value adapter unavailable")).Invoke(item, args);
        return ((long)args[0], (double)args[2]);
    }

    internal static decimal LiquidValue(GameItem item, decimal basis)
    {
        var (innate, final) = Stages(item);
        return checked(basis * Math.Max(0m, 100m + innate) / 100m * (decimal)final);
    }

    internal static long ItemValue(GameItem item)
    {
        // Match GetRefreshedValue's native descendant list. It stages each
        // descendant once; only the selected root's count multiplies a take.
        var items = GraphUtils.FindAllChildrenType<GameItem>(item.Cast<GraphNodeStorage>(), null, null);
        if (items == null || items.Count > ForgeNumbers.Inventory.MaxValuationChildren)
            throw new InvalidOperationException("Invalid production input graph");
        var seen = new HashSet<IntPtr>();
        long total = 0;
        void Accumulate(GameItem current)
        {
            if (current == null || current.Pointer == IntPtr.Zero || !seen.Add(current.Pointer) || seen.Count > ForgeNumbers.Inventory.MaxRunItems)
                throw new InvalidOperationException("Invalid production input graph");
            ForgeLiquidValueRuntime.RefreshValue(current);
            var (innate, final) = Stages(current);
            long raw = checked(current.unitValue + PowerHelper.GetPowerSourceItemValue(current));
            long value = (long)(_compose ?? throw new InvalidOperationException("Production value adapter unavailable"))
                .Invoke(null, [raw, innate, 0L, final, checked((int)current.lateUnitValue), WaterHelper.GetContainerPrice(current)])!;
            total = checked(total + Math.Max(0L, value));
        }
        foreach (var descendant in items) Accumulate(descendant);
        Accumulate(item);
        return total;
    }
}
