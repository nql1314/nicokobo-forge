using HarmonyLib;
using Il2Cpp;

namespace Nicokobo.Forge;

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
            var target = AccessTools.Method(typeof(GameSlotInventory),
                nameof(GameSlotInventory.MayHaveValidInventorySlot),
                [typeof(GameItem)]) ?? throw new MissingMethodException(
                nameof(GameSlotInventory),
                nameof(GameSlotInventory.MayHaveValidInventorySlot));
            harmony.Patch(target,
                prefix: new HarmonyMethod(AccessTools.Method(
                    typeof(InventoryDragFaultProbe), nameof(BeforeCheck))!),
                finalizer: new HarmonyMethod(AccessTools.Method(
                    typeof(InventoryDragFaultProbe), nameof(AfterFault))!));
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

    private static void BeforeCheck(GameSlotInventory __instance, GameItem item)
    {
        try
        {
            if (__instance == null || __instance.Pointer == IntPtr.Zero ||
                !Seen.Add(__instance.Pointer) || Seen.Count > 128) return;
            _log?.Invoke($"[WARN] [NicokoboForge/InventoryDrag] slot={Describe(__instance, item)}");
        }
        catch { /* Diagnostics must not interrupt the native drag. */ }
    }

    private static Exception? AfterFault(Exception? __exception,
        GameSlotInventory __instance, GameItem item, ref bool __result)
    {
        if (__exception == null) return null;
        if (!(__exception is System.Reflection.TargetException ||
            __exception.Message.Contains("Object does not match target type",
                StringComparison.Ordinal))) return __exception;
        __result = false;
        if (_faults++ < 8)
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

    private static string Describe(GameSlotInventory slot, GameItem item)
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
