using Il2Cpp;
using Nicokobo.Forge.Runtime;

namespace Nicokobo.Forge;

public enum ForgeModulePhase { BeforeAggregate, AfterAggregate, AfterTemp, BeforeAdded, AfterRemoved }
public sealed record ForgeModuleContext(IReadOnlyList<GameItem> Modules, GameItem? Machine = null, GameItem? Module = null);

public static partial class ForgeModuleApi
{
    private static readonly OwnedCallbacks<ForgeModulePhase, ForgeModuleContext> Events = new();
    private sealed class LearningContext(GameItem module)
    {
        internal GameItem Module { get; } = module;
        internal List<Action> Completed { get; } = [];
    }
    private static readonly OwnedCallbacks<bool, LearningContext> Learning = new();
    private static readonly HashSet<string> EventHooks = new(StringComparer.Ordinal);
    public static IDisposable Subscribe(string ownerId, string callbackId, ForgeModulePhase phase,
        Action<ForgeModuleContext> callback, int order = 0)
    {
        if (!Enum.IsDefined(typeof(ForgeModulePhase), phase)) throw new ArgumentException("Invalid module phase");
        var lease = Events.Add(ownerId, callbackId, phase, callback, order);
        if (_allowed && InstallEvent(phase)) return new CallbackLease(() =>
        {
            lease.Dispose();
            string id = EventId(phase);
            if (Enum.GetValues<ForgeModulePhase>().Any(p => EventId(p) == id && Events.Has(p))) return;
            if (EventHooks.Remove(id)) NativeHookSet.Remove(id, _log);
        });
        lease.Dispose();
        throw new InvalidOperationException("Module computation adapter unavailable");
    }
    public static IDisposable SubscribeLearning<T>(string ownerId, string callbackId,
        Func<GameItem, T?> capture, Action<GameItem, T?> completed) where T : class
    {
        if (capture == null || completed == null) throw new ArgumentException("Learning callbacks required");
        var lease = Learning.Add(ownerId, callbackId, true, context =>
        {
            var state = capture(context.Module);
            context.Completed.Add(() => completed(context.Module, state));
        });
        const string id = "nicokobo.forge.module_learning";
        if (_allowed && (EventHooks.Contains(id) || NativeHookSet.Install(id,
            [new(typeof(ModuleEffectHelper), nameof(ModuleEffectHelper.OnUsedLearningAlgo), [typeof(GameItem)],
                typeof(void), typeof(ForgeModuleApi), nameof(BeforeLearning), nameof(AfterLearning))], _log)))
        {
            EventHooks.Add(id);
            return new CallbackLease(() =>
            {
                lease.Dispose();
                if (Learning.Count == 0 && EventHooks.Remove(id)) NativeHookSet.Remove(id, _log);
            });
        }
        lease.Dispose();
        throw new InvalidOperationException("Module learning adapter unavailable");
    }
    private static string EventId(ForgeModulePhase phase) => "nicokobo.forge.module_events." + (phase switch
    {
        ForgeModulePhase.BeforeAggregate or ForgeModulePhase.AfterAggregate => nameof(ModuleHelper.ComputeModuleEffect),
        ForgeModulePhase.AfterTemp => nameof(ModuleEffectHelper.ComputeAllModuleTempStat),
        ForgeModulePhase.BeforeAdded => nameof(ModuleEffectHelper.OnAddedModuleToGrid),
        _ => nameof(ModuleEffectHelper.OnRemovedModuleFromGrid)
    }).ToLowerInvariant();
    private static bool InstallEvent(ForgeModulePhase phase)
    {
        var list = typeof(Il2CppSystem.Collections.Generic.List<GameItem>);
        NativeHook hook = phase switch
        {
            ForgeModulePhase.BeforeAggregate or ForgeModulePhase.AfterAggregate => new(typeof(ModuleHelper), nameof(ModuleHelper.ComputeModuleEffect),
                [list, typeof(GameItem)], typeof(void), typeof(ForgeModuleApi), nameof(BeforeAggregate), nameof(AfterAggregate)),
            ForgeModulePhase.AfterTemp => new(typeof(ModuleEffectHelper), nameof(ModuleEffectHelper.ComputeAllModuleTempStat),
                [list], typeof(void), typeof(ForgeModuleApi), Postfix: nameof(AfterTemp)),
            ForgeModulePhase.BeforeAdded => new(typeof(ModuleEffectHelper), nameof(ModuleEffectHelper.OnAddedModuleToGrid),
                [typeof(GameItem), typeof(GameItem), list], typeof(void), typeof(ForgeModuleApi), Prefix: nameof(BeforeAdded)),
            _ => new(typeof(ModuleEffectHelper), nameof(ModuleEffectHelper.OnRemovedModuleFromGrid),
                [typeof(GameItem), typeof(GameItem), list], typeof(void), typeof(ForgeModuleApi), Postfix: nameof(AfterRemoved))
        };
        string id = EventId(phase);
        if (EventHooks.Contains(id)) return true;
        if (!NativeHookSet.Install(id, [hook], _log)) return false;
        EventHooks.Add(id); return true;
    }
    private static void Dispatch(ForgeModulePhase phase, Il2CppSystem.Collections.Generic.List<GameItem>? modules,
        GameItem? machine = null, GameItem? module = null)
    {
        if (!Events.Has(phase)) return;
        try
        {
            var snapshot = new List<GameItem>();
            if (modules != null) for (int i = 0; i < modules.Count; i++)
                if (modules[i] != null && modules[i].Pointer != IntPtr.Zero) snapshot.Add(modules[i]);
            Events.Dispatch(phase, new(snapshot.AsReadOnly(), machine, module), _log);
        }
        catch (Exception ex) { try { _log?.Invoke($"[WARN] [NicokoboForge/Modules] phase={phase}; {ex.Message}"); } catch { } }
    }
    private static void BeforeAggregate(Il2CppSystem.Collections.Generic.List<GameItem> __0, GameItem __1) => Dispatch(ForgeModulePhase.BeforeAggregate, __0, __1);
    private static void AfterAggregate(Il2CppSystem.Collections.Generic.List<GameItem> __0, GameItem __1) => Dispatch(ForgeModulePhase.AfterAggregate, __0, __1);
    private static void AfterTemp(Il2CppSystem.Collections.Generic.List<GameItem> __0) => Dispatch(ForgeModulePhase.AfterTemp, __0);
    private static void BeforeAdded(GameItem __0, GameItem __1, Il2CppSystem.Collections.Generic.List<GameItem> __2) => Dispatch(ForgeModulePhase.BeforeAdded, __2, __1, __0);
    private static void AfterRemoved(GameItem __0, GameItem __1, Il2CppSystem.Collections.Generic.List<GameItem> __2) => Dispatch(ForgeModulePhase.AfterRemoved, __2, __1, __0);
    private static void BeforeLearning(GameItem __0, out LearningContext? __state)
    {
        __state = null;
        if (__0 == null || __0.Pointer == IntPtr.Zero || !Learning.Has(true)) return;
        __state = new(__0); Learning.Dispatch(true, __state, _log);
    }
    private static void AfterLearning(LearningContext? __state)
    {
        if (__state == null) return;
        foreach (var callback in __state.Completed)
            try { callback(); } catch (Exception ex) { try { _log?.Invoke($"[WARN] [NicokoboForge/Modules] learning: {ex.Message}"); } catch { } }
    }
}
