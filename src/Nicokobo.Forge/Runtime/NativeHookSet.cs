using System.Reflection;
using HarmonyLib;
using Il2CppInterop.Common;
using Il2CppInterop.Runtime.Runtime;

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
        HarmonyLib.Harmony? harmony = null;
        string target = "resolve";
        try
        {
            var resolved = hooks.Select(h => (
                Target: Require(h.TargetType, h.Method, h.Arguments, h.ReturnType),
                Prefix: Callback(h.CallbackType, h.Prefix),
                Postfix: Callback(h.CallbackType, h.Postfix),
                Finalizer: Callback(h.CallbackType, h.Finalizer))).ToArray();
            // Validate the whole set before touching any detours. Generated IL2CPP
            // wrappers can look concrete even when the native method is abstract;
            // letting the backend attach to address zero terminates the process.
            foreach (var h in resolved)
            {
                target = $"{h.Target.DeclaringType?.FullName}.{h.Target.Name}";
                RequireNativeEntryPoint(h.Target);
            }
            harmony = new HarmonyLib.Harmony(id);
            foreach (var h in resolved)
            {
                target = $"{h.Target.DeclaringType?.FullName}.{h.Target.Name}";
                harmony.Patch(h.Target, prefix: h.Prefix, postfix: h.Postfix, finalizer: h.Finalizer);
            }
            return true;
        }
        catch (Exception ex)
        {
            try { harmony?.UnpatchSelf(); }
            catch (Exception undo) { SafeLog(log, $"[ERROR] [NicokoboForge/Hooks] id={id}; rollback={undo}"); }
            SafeLog(log, $"[WARN] [NicokoboForge/Hooks] id={id}; target={target}; disabled={ex}");
            return false;
        }
    }
    internal static MethodInfo Require(Type type, string name, Type[] args, Type returns)
    {
        var method = AccessTools.Method(type, name, args);
        return method?.ReturnType == returns ? method : throw new MissingMethodException(type.FullName, name);
    }
    internal static unsafe void RequireNativeEntryPoint(MethodInfo method)
    {
        if (method.IsAbstract || method.ContainsGenericParameters)
            throw new InvalidOperationException("Native hook target is abstract or open generic");
        var field = Il2CppInteropUtils.GetIl2CppMethodInfoPointerFieldForGeneratedMethod(method)
            ?? throw new InvalidOperationException("Native hook target has no generated method metadata");
        if (field.GetValue(null) is not IntPtr pointer || pointer == IntPtr.Zero)
            throw new InvalidOperationException("Native hook target has no method metadata pointer");
        var nativeMethod = UnityVersionHandler.Wrap((Il2CppMethodInfo*)pointer);
        if (nativeMethod == null || nativeMethod.MethodPointer == IntPtr.Zero)
            throw new InvalidOperationException("Native hook target has no executable entry point");
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
