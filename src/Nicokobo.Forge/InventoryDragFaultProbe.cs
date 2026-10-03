using HarmonyLib;
using Il2Cpp;

namespace Nicokobo.Forge;

/// <summary>Diagnostic only. Forge installs native ContainerHelper whitelists, so
/// its own slots never carry an interop bridge; this catches a foreign
/// DelegateSupport delegate that would throw inside InvokeAllReduce.</summary>
internal static class InventoryDragFaultProbe
{
    private const string Owner = "nicokobo.forge.inventory_drag_fault";
    private static readonly HashSet<IntPtr> Seen = new();
    private static Action<string>? _log;
    private static int _faults;

    internal static bool Install(bool knownBuild, Action<string> log)
    {
        if (!knownBuild) return false;
        _log = log;
        var harmony = new HarmonyLib.Harmony(Owner);
        try
        {
            // Every concrete override of the virtual method the drag handler calls.
            // The abstract declaration on GameInventory has no body to patch.
            var prefix = new HarmonyMethod(AccessTools.Method(
                typeof(InventoryDragFaultProbe), nameof(BeforeCheck))!);
            var finalizer = new HarmonyMethod(AccessTools.Method(
                typeof(InventoryDragFaultProbe), nameof(AfterFault))!);
            int patched = 0;
            foreach (var slotType in typeof(GameInventory).Assembly.GetTypes()
                .Where(type => type != typeof(GameInventory) && type.IsSubclassOf(typeof(GameInventory))))
            {
                var target = AccessTools.DeclaredMethod(slotType,
                    nameof(GameSlotInventory.MayHaveValidInventorySlot), [typeof(GameItem)]);
                if (target == null) continue;
                try { harmony.Patch(target, prefix: prefix, finalizer: finalizer); patched++; }
                catch (Exception ex)
                {
                    log($"[WARN] [NicokoboForge/InventoryDrag] {slotType.Name} not patched: " +
                        $"{ex.GetType().Name}: {ex.Message}");
                }
            }
            if (patched == 0) throw new MissingMethodException("GameInventory",
                nameof(GameSlotInventory.MayHaveValidInventorySlot));
            log($"[NicokoboForge/InventoryDrag] probe installed on {patched} slot types");
            return true;
        }
        catch (Exception ex)
        {
            harmony.UnpatchSelf();
            log($"[WARN] [NicokoboForge/InventoryDrag] probe disabled: " +
                $"{ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    private static void BeforeCheck(GameInventory __instance, GameItem item)
    {
        try
        {
            if (__instance == null || __instance.Pointer == IntPtr.Zero ||
                !Seen.Add(__instance.Pointer) || Seen.Count > ForgeNumbers.Diagnostics.MaxDragSlots) return;
            _log?.Invoke($"[WARN] [NicokoboForge/InventoryDrag] slot={Describe(__instance, item)}");
        }
        catch { /* Diagnostics must not interrupt the native drag. */ }
    }

    private static Exception? AfterFault(Exception? __exception,
        GameInventory __instance, GameItem item, ref bool __result)
    {
        if (__exception == null) return null;
        if (!(__exception is System.Reflection.TargetException ||
            __exception.Message.Contains("Object does not match target type",
                StringComparison.Ordinal))) return __exception;
        __result = false;
        if (_faults++ < ForgeNumbers.Diagnostics.MaxDragFaultLogs)
        {
            try
            {
                _log?.Invoke($"[WARN] [NicokoboForge/InventoryDrag] " +
                    $"invalid slot callback; rejected candidate; {Describe(__instance, item)}; " +
                    $"error={__exception.GetType().Name}: {__exception.Message}");
            }
            catch { /* Preserve fail-closed result. */ }
        }
        return null;
    }

    private static string Describe(GameInventory slot, GameItem item)
    {
        string add = DescribeDelegate(slot.mayInventoryAddItemFunc);
        string remove = DescribeDelegate(slot.mayInventoryRemoveItemFunc);
        string owner;
        try { owner = slot.GetParentItem()?.identifier ?? "<none>"; }
        catch (Exception ex) { owner = $"lookup-error:{ex.GetType().Name}"; }
        return $"{slot.Pointer}; slotId={slot.identifier}; " +
            $"item={item?.identifier ?? "<null>"}; owner={owner}; " +
            $"add={add}; remove={remove}";
    }

    private static string DescribeDelegate(
        Il2CppSystem.Func<GameItem, GameInventory, bool>? callback)
    {
        if (callback == null || callback.Pointer == IntPtr.Zero) return "<none>";
        try
        {
            var method = callback.Method;
            var target = callback.Target;
            return $"{method?.DeclaringType?.FullName}.{method?.Name}" +
                $"/target={target?.ToString() ?? "<null>"}";
        }
        catch (Exception ex) { return $"metadata-error:{ex.GetType().Name}"; }
    }
}
