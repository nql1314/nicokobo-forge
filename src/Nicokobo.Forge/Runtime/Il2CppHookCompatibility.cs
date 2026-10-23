using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using HarmonyLib.Public.Patching;
using Il2CppInterop.Common;
using Il2CppInterop.Runtime.Runtime;

namespace Nicokobo.Forge.Runtime;

// Keep this process-wide repair installed while any mod can still rebuild a hook.
internal static class Il2CppHookCompatibility
{
    private const string HookId = "nicokobo.forge.il2cpp_hook_compatibility";
    private static bool _attempted;
    private static bool _ready;

    internal static bool Install(Action<string> log)
    {
        if (_attempted) return _ready;
        _attempted = true;
        var harmony = new HarmonyLib.Harmony(HookId);
        try
        {
            var backend = Type.GetType("Il2CppInterop.HarmonySupport.Il2CppDetourMethodPatcher, Il2CppInterop.HarmonySupport", true)!;
            var rebuild = AccessTools.DeclaredMethod(backend, "DetourTo", [typeof(MethodBase)])
                ?? throw new MissingMethodException(backend.FullName, "DetourTo");
            var instructions = PatchProcessor.GetOriginalInstructions(rebuild);
            bool affected = instructions.Any(IsManagedDetour);
            // Backport upstream 184b996. Patching the native function remains intact;
            // only the additional managed detour that corrupts CopyOriginal is removed.
            if (affected)
            {
                RemoveManagedDetour(instructions); // Validate the complete tail before installing.
                harmony.Patch(rebuild, transpiler: new HarmonyMethod(typeof(Il2CppHookCompatibility), nameof(NativeOnlyRebuild)));
            }

            // All three dictionaries must use the same key: sharing just the backend
            // would silently lose the other alias's prefix/postfix registrations.
            var normalize = new HarmonyMethod(typeof(Il2CppHookCompatibility), nameof(NormalizeTarget)) { priority = Priority.First };
            foreach (string name in new[] { nameof(PatchManager.ToPatchInfo), nameof(PatchManager.GetPatchInfo), nameof(PatchManager.GetMethodPatcher) })
                harmony.Patch(AccessTools.DeclaredMethod(typeof(PatchManager), name, [typeof(MethodBase)])
                    ?? throw new MissingMethodException(typeof(PatchManager).FullName, name), prefix: normalize);
            _ready = true;
            log($"[INFO] [NicokoboForge/Hooks] IL2CPP rebuild={(affected ? "upstream-184b996 backport" : "native-only")}; quantity aliases share patch state when native entries match");
            return true;
        }
        catch (Exception ex)
        {
            _ready = false;
            harmony.UnpatchSelf();
            log("[ERROR] [NicokoboForge/Hooks] IL2CPP hook compatibility unavailable: " + ex);
            return false;
        }
    }

    private static IEnumerable<CodeInstruction> NativeOnlyRebuild(IEnumerable<CodeInstruction> instructions)
        => RemoveManagedDetour(instructions.ToList());

    internal static List<CodeInstruction> RemoveManagedDetour(List<CodeInstruction> instructions)
    {
        int[] constructors = instructions.Select((code, index) => (code, index))
            .Where(x => IsManagedDetour(x.code)).Select(x => x.index).ToArray();
        if (constructors.Length == 0) return instructions;
        if (constructors.Length != 1) throw new InvalidOperationException("Unrecognized IL2CPP managed detour tail");
        int at = constructors[0];
        bool Local(int offset, string operation) => instructions[at + offset].opcode.Name?.StartsWith(operation, StringComparison.Ordinal) == true;
        bool Call(int offset, string type, string name) => instructions[at + offset].operand is MethodInfo method &&
            method.DeclaringType?.FullName == type && method.Name == name;
        if (at < 3 || at + 8 >= instructions.Count || instructions[at - 3].opcode != OpCodes.Ldarg_0 ||
            !Call(-2, "HarmonyLib.Public.Patching.MethodPatcher", "get_Original") || !Local(-1, "ldloc") ||
            !Local(1, "stloc") || !Local(2, "ldloc") || !Call(3, "MonoMod.RuntimeDetour.Detour", "Apply") ||
            instructions[at + 4].opcode != OpCodes.Ldsfld ||
            instructions[at + 4].operand is not FieldInfo { Name: "DetourCache" } cache ||
            cache.DeclaringType?.FullName != "Il2CppInterop.HarmonySupport.Il2CppDetourMethodPatcher" ||
            !Local(5, "ldloc") || !Call(6, typeof(List<object>).FullName!, "Add") ||
            !Local(7, "ldloc") || instructions[at + 8].opcode != OpCodes.Ret)
            throw new InvalidOperationException("Unrecognized IL2CPP managed detour tail");
        // Nops retain branch labels and exception-region markers on the removed tail.
        for (int i = at - 3; i <= at + 6; i++)
        {
            instructions[i] = new CodeInstruction(instructions[i]) { opcode = OpCodes.Nop, operand = null };
        }
        return instructions;
    }

    private static bool IsManagedDetour(CodeInstruction code) => code.opcode == OpCodes.Newobj &&
        code.operand is ConstructorInfo constructor && constructor.DeclaringType?.FullName == "MonoMod.RuntimeDetour.Detour";

    private static void NormalizeTarget(ref MethodBase __0) => __0 = QuantityTarget(__0);

    internal static MethodBase QuantityTarget(MethodBase target)
    {
        if (target is not MethodInfo method || method.DeclaringType?.FullName != "Il2Cpp.GameItem" ||
            method.Name is not ("SetUnitCount" or "SetAmount")) return target;
        var count = AccessTools.DeclaredMethod(method.DeclaringType, "SetUnitCount", [typeof(int)]);
        var amount = AccessTools.DeclaredMethod(method.DeclaringType, "SetAmount", [typeof(int)]);
        if (count == null || amount == null || count.ReturnType != amount.ReturnType ||
            count.IsStatic != amount.IsStatic || (method != count && method != amount)) return target;
        IntPtr entry = NativeEntry(count);
        // Separate native implementations must retain separate Harmony state.
        return entry != IntPtr.Zero && entry == NativeEntry(amount) ? amount : target;
    }

    private static unsafe IntPtr NativeEntry(MethodInfo method)
    {
        var field = Il2CppInteropUtils.GetIl2CppMethodInfoPointerFieldForGeneratedMethod(method);
        if (field?.GetValue(null) is not IntPtr pointer || pointer == IntPtr.Zero) return IntPtr.Zero;
        return UnityVersionHandler.Wrap((Il2CppMethodInfo*)pointer)?.MethodPointer ?? IntPtr.Zero;
    }
}
