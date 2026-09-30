using System.Reflection;
using HarmonyLib;

namespace Nicokobo.Forge.Runtime;

// Each feature has its own Harmony identity, so rollback cannot unpatch a
// different Forge capability or another mod. Resolve every target first.
internal sealed record NativeHook(Type TargetType, string Method, Type[] Arguments,
    Type ReturnType, Type CallbackType, string? Prefix = null,
    string? Postfix = null, string? Finalizer = null);

internal static class NativeHookSet
{
    internal static bool Install(string id, IReadOnlyList<NativeHook> hooks, Action<string>? log)
    {
        var harmony = new HarmonyLib.Harmony(id);
        try
        {
            var resolved = hooks.Select(h => (
                Target: Require(h.TargetType, h.Method, h.Arguments, h.ReturnType),
                Prefix: Callback(h.CallbackType, h.Prefix),
                Postfix: Callback(h.CallbackType, h.Postfix),
                Finalizer: Callback(h.CallbackType, h.Finalizer))).ToArray();
            foreach (var h in resolved)
                harmony.Patch(h.Target, prefix: h.Prefix, postfix: h.Postfix, finalizer: h.Finalizer);
            return true;
        }
        catch (Exception ex)
        {
            try { harmony.UnpatchSelf(); }
            catch (Exception undo) { SafeLog(log, $"[ERROR] [NicokoboForge/Hooks] id={id}; rollback={undo.Message}"); }
            SafeLog(log, $"[WARN] [NicokoboForge/Hooks] id={id}; disabled={ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }
    internal static MethodInfo Require(Type type, string name, Type[] args, Type returns)
    {
        var method = AccessTools.Method(type, name, args);
        return method?.ReturnType == returns ? method : throw new MissingMethodException(type.FullName, name);
    }
    internal static void Remove(string id, Action<string>? log)
    {
        try { new HarmonyLib.Harmony(id).UnpatchSelf(); }
        catch (Exception ex) { SafeLog(log, $"[WARN] [NicokoboForge/Hooks] id={id}; inactive rollback={ex.Message}"); }
    }
    private static HarmonyMethod? Callback(Type type, string? name) => name == null ? null :
        new HarmonyMethod(AccessTools.Method(type, name) ?? throw new MissingMethodException(type.FullName, name));
    private static void SafeLog(Action<string>? log, string text) { try { log?.Invoke(text); } catch { } }
}
