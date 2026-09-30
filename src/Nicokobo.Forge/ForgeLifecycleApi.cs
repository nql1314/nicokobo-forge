using Il2Cpp;
using Nicokobo.Forge.Runtime;

namespace Nicokobo.Forge;

public enum ForgeLifecyclePhase
{
    BeforeLoad, AfterLoad, BeforeNight, AfterNight,
    BeforeNightServices, AfterNightServices, AfterSleep, AfterEndDay
}
public sealed record ForgeRunIdentity(string RunId, int SlotId, int Day);

/// <summary>Items are captured lazily once for this event. Native handles remain
/// live; subscribers must revalidate before writing and must not retain context.</summary>
public sealed class ForgeLifecycleContext
{
    private IReadOnlyList<GameItem>? _items;
    internal ForgeLifecycleContext(PlayerStore store)
    {
        Store = store;
        int day;
        try { day = StoreStation.GetDayCounter(); } catch { day = -1; }
        Run = new(store.runID, store.saveSlotId, day);
    }
    public PlayerStore Store { get; }
    public ForgeRunIdentity Run { get; }
    public IReadOnlyList<GameItem> Items => _items ??= ForgeInventoryApi.CaptureRunItems(Store);
}

/// <summary>Shared synchronous game boundaries. Order is ascending then owned
/// callback ID. Hooks observe native calls; they never suppress a game method.</summary>
public static class ForgeLifecycleApi
{
    private static readonly OwnedCallbacks<ForgeLifecyclePhase, ForgeLifecycleContext> Callbacks = new();
    private static readonly HashSet<string> Installed = new(StringComparer.Ordinal);
    private static bool _allowed;
    private static Action<string>? _log;
    internal static void Configure(bool allowed, Action<string> log) { _allowed = allowed; _log = log; }
    public static IDisposable Subscribe(string ownerId, string callbackId, ForgeLifecyclePhase phase,
        Action<ForgeLifecycleContext> callback, int order = 0)
    {
        if (!Enum.IsDefined(typeof(ForgeLifecyclePhase), phase) || !OwnedCallbacks<ForgeLifecyclePhase, ForgeLifecycleContext>.ValidId(ownerId, callbackId))
            throw new ArgumentException("Invalid lifecycle phase or owned callback ID");
        if (!_allowed) throw new InvalidOperationException("Game lifecycle adapter is unavailable");
        var lease = Callbacks.Add(ownerId, callbackId, phase, callback, order);
        if (Install(phase)) return new CallbackLease(() =>
        {
            lease.Dispose();
            string id = HookId(phase);
            if (Enum.GetValues<ForgeLifecyclePhase>().Any(p => HookId(p) == id && Callbacks.Has(p))) return;
            if (Installed.Remove(id)) NativeHookSet.Remove(id, _log);
        });
        lease.Dispose();
        throw new InvalidOperationException($"Game lifecycle hook is unavailable: {phase}");
    }
    private static string HookId(ForgeLifecyclePhase phase) => "nicokobo.forge.lifecycle." + (phase switch
    {
        ForgeLifecyclePhase.BeforeLoad or ForgeLifecyclePhase.AfterLoad => nameof(PlayerStore.LoadGame),
        ForgeLifecyclePhase.BeforeNight or ForgeLifecyclePhase.AfterNight => nameof(PlayerStore.EndNight),
        ForgeLifecyclePhase.BeforeNightServices => nameof(ModHook.FireOnHandlingNightlyServicesEarly),
        ForgeLifecyclePhase.AfterNightServices => nameof(ModHook.FireOnHandlingNightlyServicesLate),
        ForgeLifecyclePhase.AfterSleep => nameof(ModHook.FireOnGoingSleepLate),
        _ => nameof(PlayerStore.EndDay)
    }).ToLowerInvariant();
    private static bool Install(ForgeLifecyclePhase phase)
    {
        var (type, method, prefix, postfix) = phase switch
        {
            ForgeLifecyclePhase.BeforeLoad or ForgeLifecyclePhase.AfterLoad =>
                (typeof(PlayerStore), nameof(PlayerStore.LoadGame), nameof(BeforeLoad), nameof(AfterLoad)),
            ForgeLifecyclePhase.BeforeNight or ForgeLifecyclePhase.AfterNight =>
                (typeof(PlayerStore), nameof(PlayerStore.EndNight), nameof(BeforeNight), nameof(AfterNight)),
            ForgeLifecyclePhase.BeforeNightServices =>
                (typeof(ModHook), nameof(ModHook.FireOnHandlingNightlyServicesEarly), nameof(BeforeServices), (string?)null),
            ForgeLifecyclePhase.AfterNightServices =>
                (typeof(ModHook), nameof(ModHook.FireOnHandlingNightlyServicesLate), (string?)null, nameof(AfterServices)),
            ForgeLifecyclePhase.AfterSleep =>
                (typeof(ModHook), nameof(ModHook.FireOnGoingSleepLate), (string?)null, nameof(AfterSleep)),
            _ => (typeof(PlayerStore), nameof(PlayerStore.EndDay), (string?)null, nameof(AfterEndDay))
        };
        string id = HookId(phase);
        if (Installed.Contains(id)) return true;
        if (!NativeHookSet.Install(id, [new(type, method, [], typeof(void), typeof(ForgeLifecycleApi), prefix, postfix)], _log)) return false;
        Installed.Add(id);
        return true;
    }
    private static void Dispatch(ForgeLifecyclePhase phase, PlayerStore? store)
    {
        if (!Callbacks.Has(phase) || store == null || store.Pointer == IntPtr.Zero) return;
        try { Callbacks.Dispatch(phase, new(store), _log); }
        catch (Exception ex) { try { _log?.Invoke($"[WARN] [NicokoboForge/Lifecycle] phase={phase}; {ex.Message}"); } catch { } }
    }
    private static void BeforeLoad(PlayerStore __instance) => Dispatch(ForgeLifecyclePhase.BeforeLoad, __instance);
    private static void AfterLoad(PlayerStore __instance) => Dispatch(ForgeLifecyclePhase.AfterLoad, __instance);
    private static void BeforeNight(PlayerStore __instance) => Dispatch(ForgeLifecyclePhase.BeforeNight, __instance);
    private static void AfterNight(PlayerStore __instance) => Dispatch(ForgeLifecyclePhase.AfterNight, __instance);
    private static void BeforeServices() => Dispatch(ForgeLifecyclePhase.BeforeNightServices, PlayerStore.Instance);
    private static void AfterServices() => Dispatch(ForgeLifecyclePhase.AfterNightServices, PlayerStore.Instance);
    private static void AfterSleep() => Dispatch(ForgeLifecyclePhase.AfterSleep, PlayerStore.Instance);
    private static void AfterEndDay(PlayerStore __instance) => Dispatch(ForgeLifecyclePhase.AfterEndDay, __instance);
}
