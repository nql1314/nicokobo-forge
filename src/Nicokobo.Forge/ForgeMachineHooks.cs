using HarmonyLib;
using Il2Cpp;

namespace Nicokobo.Forge;

// This adapter alone owns machine Harmony hooks and native factory setup.
internal static class ForgeMachineHooks
{
    private const string HarmonyId = "nicokobo.forge.machine_recipes";
    private static bool _installed;
    internal static bool Installed => _installed;

    internal static GameItem CreateMachine(Func<GameItem> factory,
        ForgeMachineProcessMode mode)
    {
        var item = factory();
        if (item == null || item.Pointer == IntPtr.Zero)
            throw new InvalidOperationException("Machine factory returned null");
        item.onCycleEndEarlySlotItemFunc = null;
        item.onCycleEndSlotItemFunc = null;
        item.onCycleEndLateSlotItemFunc = null;
        item.forceDisableActivate = false;
        if (mode == ForgeMachineProcessMode.NightlyLiquid)
            ForgeMachineRuntime.ConfigureLiquidMachine(item);
        return item;
    }

    internal static bool Install(bool knownBuild, Action<string> log)
    {
        if (_installed) return true;
        if (!knownBuild) return false;
        var harmony = new HarmonyLib.Harmony(HarmonyId);
        try
        {
            var closure = typeof(MachineFurnace.__c__DisplayClass0_0);
            Patch(harmony, closure, "_Furnace_b__1",
                [typeof(GameItem), typeof(GameInventory)],
                nameof(AdmissionPrefix), null);
            // b__3/b__4 are module add/remove callbacks, not processing.
            // Blocking them prevents current performance/quality/power updates.
            Patch(harmony, closure, "_Furnace_b__5",
                [typeof(GameItem), typeof(GameInventory), typeof(SlotMarker)],
                nameof(CyclePrefix), null);
            Patch(harmony, typeof(PlayerStore), nameof(PlayerStore.EndNight),
                Type.EmptyTypes, nameof(BeforeEndNight), null);
            Patch(harmony, typeof(PlayerStore), nameof(PlayerStore.LoadGame),
                Type.EmptyTypes, nameof(BeforeLoadGame), nameof(AfterLoadGame));
            Patch(harmony, typeof(PlayerStore), nameof(PlayerStore.DecodeSaveItem),
                [typeof(string), typeof(GameInventory)], null,
                nameof(AfterDecodeSaveItem));
            Patch(harmony, typeof(MachineFurnace),
                nameof(MachineFurnace.CreateMachineInventoryWindow),
                [typeof(int), typeof(int), typeof(bool), typeof(bool), typeof(int)],
                null, nameof(AfterCreateMachineInventoryWindow));
            _installed = true;
            log("[INFO] [NicokoboForge/Machine] native recipe hooks installed");
            return true;
        }
        catch (Exception ex)
        {
            harmony.UnpatchSelf();
            _installed = false;
            log($"[WARN] [NicokoboForge/Machine] hooks disabled: " +
                $"{ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    private static void Patch(HarmonyLib.Harmony harmony, Type type,
        string methodName, Type[] parameters, string? prefix, string? postfix)
    {
        var target = AccessTools.Method(type, methodName, parameters)
            ?? throw new MissingMethodException(type.FullName, methodName);
        harmony.Patch(target,
            prefix: prefix == null ? null : new HarmonyMethod(AccessTools.Method(
                typeof(ForgeMachineHooks), prefix)!),
            postfix: postfix == null ? null : new HarmonyMethod(AccessTools.Method(
                typeof(ForgeMachineHooks), postfix)!));
    }

    private static bool AdmissionPrefix(
        MachineFurnace.__c__DisplayClass0_0 __instance,
        GameItem item, ref bool __result)
    {
        var machineId = __instance?.furnace?.identifier;
        if (machineId == null || !ForgeMachineApi.TryGet(machineId, out var profile) ||
            profile == null) return true;
        // Registered recipe inputs are accepted before the native ore-only
        // predicate. Other native furnace items keep the game's admission.
        if (profile.NativeMachine &&
            (item == null || item.Pointer == IntPtr.Zero ||
             !profile.Accepts(item.identifier))) return true;
        __result = item != null && item.Pointer != IntPtr.Zero &&
            profile.Accepts(item.identifier);
        return false;
    }

    private static bool CyclePrefix(
        MachineFurnace.__c__DisplayClass0_0 __instance)
    {
        var machine = __instance?.furnace;
        if (machine == null || !ForgeMachineApi.TryGet(machine.identifier, out var profile) ||
            profile?.NativeMachine != true) return true;
        var input = __instance?.itemInventoryLeft;
        var items = input?.childItems;
        if (items == null || items.Count == 0) return true;
        // A declared probe owns the whole decision. The native cycle runs
        // whenever the game's own batch is available, and it stays idle only
        // for a slot that holds registered feedstock the game cannot use, so
        // neither side consumes what the other needs.
        if (profile.NativeBatchProbe != null)
        {
            if (ForgeMachineRuntime.NativeBatchAvailable(profile, machine, input!))
                return true;
            for (int index = 0; index < items.Count; index++)
                if (profile.IsFeedstock(items[index].identifier)) return false;
            return true;
        }
        bool glassOnly = true;
        bool hasGlass = false;
        for (int index = 0; index < items.Count; index++)
        {
            hasGlass |= profile.IsFeedstock(items[index].identifier);
            glassOnly &= profile.IsFeedstock(items[index].identifier) ||
                IsFurnaceAuxiliary(items[index].identifier);
        }
        return !(glassOnly && hasGlass);
    }

    // The base furnace accepts flux in the same input inventory. It is not
    // consumed by glass recycling, but must not trigger the native ore cycle.
    internal static bool IsFurnaceAuxiliary(string itemId) =>
        itemId is "flux_agent" or "advanced_flux_agent";

    // Forward native lifecycle callbacks to the processing adapter.
    private static void BeforeLoadGame() => ForgeMachineRuntime.BeforeLoadGame();
    private static void AfterLoadGame(PlayerStore __instance) =>
        ForgeMachineRuntime.AfterLoadGame(__instance);
    private static void AfterDecodeSaveItem(PlayerStore __instance,
        string JSON, GameInventory gameInventory) =>
        ForgeMachineRuntime.AfterDecodeSaveItem(__instance, JSON, gameInventory);
    private static void AfterCreateMachineInventoryWindow(
        Il2CppSystem.ValueTuple<PixelWindow, GameSlotInventory, GameInventory,
            GameGridInventory, GameInventory, GameSlotInventory> __result) =>
        ForgeMachineRuntime.AfterCreateMachineInventoryWindow(__result);
    private static void BeforeEndNight(PlayerStore __instance) =>
        ForgeMachineRuntime.BeforeEndNight(__instance);
}
