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
        bool previouslyValued = HasLedger(item);
        if (!valued && !previouslyValued) return;
        string saved = valued ? LiquidValueLedger.Encode(snapshot) : "";
        long target = valued ? UnitValue(snapshot) : WaterFeatureHelper.GetWaterPrice(item);
        // Read paths reapply the ledger every frame; skip the tag write, the
        // JSON round trip and the modified-state sync when nothing changed.
        if (item.unitValue == target &&
            string.Equals(item.GetTagReadonly(Tag)?.valueString, saved, StringComparison.Ordinal))
            return;
        var tags = item.state?.dict ?? throw new InvalidOperationException("Liquid state unavailable");
        if (!tags.TryGetValue(Tag, out var tag) || tag == null)
            tags[Tag] = tag = new TagState(Tag, Tag);
        tag.Enable(); tag.SetString(saved);
        item.SyncModifiedState();
        if (item.GetTagReadonly(Tag)?.valueString != saved)
            throw new InvalidOperationException("Liquid value ledger readback mismatch");
        item.unitValue = target;
    }

    private static long UnitValue(ForgeMachineLiquidSnapshot snapshot)
    {
        decimal value = decimal.Ceiling(MachineBatchMath.BaseValue(snapshot));
        return value <= long.MaxValue ? (long)value :
            throw new OverflowException("Liquid value too large");
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

    private static bool _refreshing;
    private static void ValuePrefix(GameItem __instance)
    {
        if (_refreshing) return;
        _refreshing = true;
        try { RefreshPostfix(__instance); }
        finally { _refreshing = false; }
    }
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
        catch (Exception ex) { Warn("refresh", ex); }
    }
    private static void ChangePrefix(GameItem __0, out ForgeMachineLiquidSnapshot? __state) =>
        __state = HasLedger(__0) ? ForgeLiquidApi.Capture(__0) : null;
    private static void ChangePostfix(GameItem __0, ForgeMachineLiquidSnapshot? __state)
    {
        if (__state == null) return;
        try
        {
            var current = ForgeLiquidApi.CaptureRaw(__0);
            if (current != null) Store(__0, LiquidValueLedger.Reconcile(__state, current));
        }
        catch (Exception ex) { Warn("change", ex); }
    }
    private static void EmptyPostfix(GameItem __0)
    {
        if (!HasLedger(__0)) return;
        try
        {
            var current = ForgeLiquidApi.CaptureRaw(__0);
            if (current != null) Store(__0, current);
        }
        catch (Exception ex) { Warn("empty", ex); }
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
        try
        {
            var source = ForgeLiquidApi.CaptureRaw(__0); var target = ForgeLiquidApi.CaptureRaw(__1);
            if (source == null || target == null)
            {
                // Native postfixes must not raise into the IL2CPP call site.
                Warn("transfer", new InvalidOperationException("Transferred containers unavailable"));
                return;
            }
            var values = MachineBatchMath.TransferValues(__state.Source, source, __state.Target, target);
            Store(__0, values.Source); Store(__1, values.Target);
        }
        catch (Exception ex) { Warn("transfer", ex); }
    }
    private static void Warn(string stage, Exception ex) =>
        TryLog($"[WARN] [NicokoboForge/LiquidValue] {stage} postfix skipped: " +
            $"{ex.GetType().Name}: {ex.Message}");
    private static void TryLog(string message) { try { _log?.Invoke(message); } catch { } }
}
