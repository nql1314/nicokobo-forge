using System.Reflection;
using Il2Cpp;
using Nicokobo.Forge.Runtime;

namespace Nicokobo.Forge;

// Native innate/final stages retain purity, condition and charge. Market
// features and current-customer/event markups do not become manufacturing cost.
internal static class NativeProductionValue
{
    private delegate void AccumulateStages(GameItem item, ref long innate,
        ref long outer, ref double final, bool flag);
    private delegate long ComposeStaged(long raw, long innate, long extra,
        double final, int lateUnitValue, int containerPrice);

    private static MethodInfo? _accumulateMethod, _composeMethod;
    private static AccumulateStages? _accumulate;
    private static ComposeStaged? _compose;

    internal static void Validate()
    {
        _accumulateMethod = NativeHookSet.Require(typeof(GameItem), "AccumulateFeatureStages",
            [typeof(long).MakeByRefType(), typeof(long).MakeByRefType(), typeof(double).MakeByRefType(), typeof(bool)], typeof(void));
        _composeMethod = NativeHookSet.Require(typeof(GameItem), "ComposeStagedValue",
            [typeof(long), typeof(long), typeof(long), typeof(double), typeof(int), typeof(int)], typeof(long));
        // Bind once per installation. Both targets are resolved here, so the
        // nightly batch path never builds an argument array or boxes a value.
        // Any binding failure keeps the reflection path instead of disabling
        // the whole machine runtime.
        _accumulate = Bind<AccumulateStages>(_accumulateMethod);
        _compose = Bind<ComposeStaged>(_composeMethod);
    }

    private static T? Bind<T>(MethodInfo method) where T : Delegate
    {
        try { return method.CreateDelegate<T>(); }
        catch (Exception ex)
        {
            ForgeMachineRegistrationApi.Log(
                $"[WARN] [NicokoboForge/Value] delegate binding fell back to reflection: " +
                $"{ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    private static (long Innate, double Final) Stages(GameItem item)
    {
        if (_accumulate != null)
        {
            long innate = 0L, outer = 0L;
            double final = 1d;
            _accumulate(item, ref innate, ref outer, ref final, true);
            return (innate, final);
        }
        object[] args = [0L, 0L, 1d, true];
        (_accumulateMethod ?? throw new InvalidOperationException("Production value adapter unavailable")).Invoke(item, args);
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
            int lateUnitValue = checked((int)current.lateUnitValue);
            int containerPrice = WaterHelper.GetContainerPrice(current);
            long value = _compose != null
                ? _compose(raw, innate, 0L, final, lateUnitValue, containerPrice)
                : (long)(_composeMethod ?? throw new InvalidOperationException("Production value adapter unavailable"))
                    .Invoke(null, [raw, innate, 0L, final, lateUnitValue, containerPrice])!;
            total = checked(total + Math.Max(0L, value));
        }
        foreach (var descendant in items) Accumulate(descendant);
        Accumulate(item);
        return total;
    }
}
