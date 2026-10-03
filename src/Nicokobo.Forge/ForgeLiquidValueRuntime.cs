using Il2Cpp;
using Nicokobo.Forge.Runtime;

namespace Nicokobo.Forge;

// Native adapters preserve optional component values during pouring, dilution,
// consumption, emptying and loading. No content item IDs or markups live here.
internal static class ForgeLiquidValueRuntime
{
    private const string Tag = "NICOKOBO_FORGE_LIQUID_VALUES_V1";
    private const string Owner = "nicokobo.forge.liquid_value";
    private static Action<string>? _log;
    private sealed record TransferState(ForgeMachineLiquidSnapshot Source, ForgeMachineLiquidSnapshot Target);

    internal static bool HasLedger(GameItem item) => !string.IsNullOrEmpty(item.GetTagReadonly(Tag)?.valueString);

    internal static ForgeMachineLiquidSnapshot Read(GameItem item, ForgeMachineLiquidSnapshot raw) =>
        LiquidValueLedger.Decode(item.GetTagReadonly(Tag)?.valueString ?? "", raw);

    internal static void Store(GameItem item, ForgeMachineLiquidSnapshot snapshot)
    {
        bool valued = snapshot.Contents.Any(part => part.Value != null && part.Parts > 0);
        string saved = valued ? LiquidValueLedger.Encode(snapshot) : "";
        bool previouslyValued = HasLedger(item);
        if (valued || previouslyValued)
        {
            var tags = item.state?.dict ?? throw new InvalidOperationException("Liquid state unavailable");
            if (!tags.TryGetValue(Tag, out var tag) || tag == null)
                tags[Tag] = tag = new TagState(Tag, Tag);
            tag.Enable(); tag.SetString(saved);
            item.SyncModifiedState();
            if (item.GetTagReadonly(Tag)?.valueString != saved)
                throw new InvalidOperationException("Liquid value ledger readback mismatch");
        }
        if (valued)
        {
            decimal value = decimal.Ceiling(MachineBatchMath.BaseValue(snapshot));
            item.unitValue = value <= long.MaxValue ? (long)value :
                throw new OverflowException("Liquid value too large");
        }
        else if (previouslyValued) item.unitValue = WaterFeatureHelper.GetWaterPrice(item);
    }

    internal static bool Install(Action<string> log)
    {
        _log = log;
        // Patching WaterFeatureHelper here runs its native static constructor,
        // which synchronously loads localization before Unity providers are ready.
        // Container mutations reconcile the ledger; value reads reapply it.
        return NativeHookSet.Install(Owner,
        [
            new(typeof(WaterHelper), nameof(WaterHelper.TransferLiquid), [typeof(GameItem), typeof(GameItem)],
                typeof(void), typeof(ForgeLiquidValueRuntime), nameof(TransferPrefix), nameof(TransferPostfix)),
            new(typeof(WaterHelper), nameof(WaterHelper.AddLiquid), [typeof(GameItem), typeof(string), typeof(int)],
                typeof(void), typeof(ForgeLiquidValueRuntime), nameof(ChangePrefix), nameof(ChangePostfix)),
            new(typeof(WaterHelper), nameof(WaterHelper.EmptyContainer), [typeof(GameItem)],
                typeof(void), typeof(ForgeLiquidValueRuntime), Postfix: nameof(EmptyPostfix)),
            new(typeof(GameItem), nameof(GameItem.GetRefreshedValue), [], typeof(long),
                typeof(ForgeLiquidValueRuntime), Prefix: nameof(ValuePrefix))
        ], log);
    }

    internal static void Uninstall() => NativeHookSet.Remove(Owner, _log);

    private static void ValuePrefix(GameItem __instance) => RefreshPostfix(__instance);
    internal static void RefreshValue(GameItem item)
    {
        if (!HasLedger(item)) return;
        var current = ForgeLiquidApi.Capture(item) ?? throw new InvalidOperationException("Valued container unavailable");
        Store(item, current);
    }
    private static void RefreshPostfix(GameItem __0)
    {
        if (__0 == null || __0.Pointer == IntPtr.Zero || !HasLedger(__0)) return;
        try
        {
            RefreshValue(__0);
        }
        catch (Exception ex) { _log?.Invoke("[WARN] [NicokoboForge/LiquidValue] refresh failed: " + ex.Message); }
    }
    private static void ChangePrefix(GameItem __0, out ForgeMachineLiquidSnapshot? __state) =>
        __state = HasLedger(__0) ? ForgeLiquidApi.Capture(__0) : null;
    private static void ChangePostfix(GameItem __0, ForgeMachineLiquidSnapshot? __state)
    {
        if (__state == null) return;
        var current = ForgeLiquidApi.CaptureRaw(__0);
        if (current != null) Store(__0, LiquidValueLedger.Reconcile(__state, current));
    }
    private static void EmptyPostfix(GameItem __0)
    {
        if (!HasLedger(__0)) return;
        var current = ForgeLiquidApi.CaptureRaw(__0);
        if (current != null) Store(__0, current);
    }
    private static void TransferPrefix(GameItem __0, GameItem __1, out TransferState? __state)
    {
        __state = null;
        if (!HasLedger(__0) && !HasLedger(__1)) return;
        var source = ForgeLiquidApi.Capture(__0); var target = ForgeLiquidApi.Capture(__1);
        if (source != null && target != null) __state = new(source, target);
    }
    private static void TransferPostfix(GameItem __0, GameItem __1, TransferState? __state)
    {
        if (__state == null) return;
        var source = ForgeLiquidApi.CaptureRaw(__0); var target = ForgeLiquidApi.CaptureRaw(__1);
        if (source == null || target == null) throw new InvalidOperationException("Transferred containers unavailable");
        var values = MachineBatchMath.TransferValues(__state.Source, source, __state.Target, target);
        Store(__0, values.Source); Store(__1, values.Target);
    }
}
