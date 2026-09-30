using HarmonyLib;
using Il2Cpp;

namespace Nicokobo.Forge;

internal static class ForgeMachineHooks
{
    private static bool _knownBuild;
    private static readonly List<IDisposable> Lifecycle = [];
    internal static bool Installed { get; private set; }
    internal static bool NativeFurnaceInstalled { get; private set; }

    internal static bool Install(bool knownBuild, Action<string> log)
    {
        _knownBuild = knownBuild;
        if (Installed) return true;
        if (!knownBuild) return false;
        try
        {
            Require(typeof(MachineFurnace), nameof(MachineFurnace.Furnace), [], typeof(GameItem));
            Require(typeof(PixelWindow), nameof(PixelWindow.Detach), [], typeof(bool));
            Require(typeof(PixelWindow), nameof(PixelWindow.Attach), [typeof(PixelElement)], typeof(bool));
            Require(typeof(GridPixelElement), nameof(GridPixelElement.GetElement),
                [typeof(int), typeof(int)], typeof(PixelElement));
            Require(typeof(WaterHelper), nameof(WaterHelper.EmptyContainer), [typeof(GameItem)], typeof(void));
            Require(typeof(WaterHelper), nameof(WaterHelper.AddLiquid),
                [typeof(GameItem), typeof(string), typeof(int)], typeof(void));
            Require(typeof(PowerHelper), nameof(PowerHelper.DrawPowerSource),
                [typeof(GameItem), typeof(int)], typeof(bool));
            const string owner = "nicokobo.forge.machines";
            Lifecycle.Add(ForgeLifecycleApi.Subscribe(owner, owner + ".reset", ForgeLifecyclePhase.BeforeLoad,
                _ => ForgeMachineRuntime.BeforeLoadGame(), -100));
            Lifecycle.Add(ForgeLifecycleApi.Subscribe(owner, owner + ".rebind", ForgeLifecyclePhase.AfterLoad,
                ForgeMachineRuntime.AfterLoadGame, -100));
            Lifecycle.Add(ForgeLifecycleApi.Subscribe(owner, owner + ".process", ForgeLifecyclePhase.BeforeNight,
                ForgeMachineRuntime.BeforeEndNight));
            Installed = true;
            log("[INFO] [NicokoboForge/Machine] templates installed; shared lifecycle; globalWindowHooks=0");
            return true;
        }
        catch (Exception ex)
        {
            foreach (var lease in Lifecycle) lease.Dispose();
            Lifecycle.Clear(); Installed = false;
            log($"[WARN] [NicokoboForge/Machine] template runtime disabled: {ex.Message}");
            return false;
        }
    }

    internal static bool InstallNativeFurnace()
    {
        if (NativeFurnaceInstalled) return true;
        if (!_knownBuild || !Installed) return false;
        var harmony = new HarmonyLib.Harmony("nicokobo.forge.native_furnace_recipes");
        try
        {
            var closure = typeof(MachineFurnace.__c__DisplayClass0_0);
            Patch(harmony, closure, "_Furnace_b__1", [typeof(GameItem), typeof(GameInventory)], nameof(AdmissionPrefix), null);
            Patch(harmony, closure, "_Furnace_b__5", [typeof(GameItem), typeof(GameInventory), typeof(SlotMarker)], nameof(CyclePrefix), null);
            NativeFurnaceInstalled = true;
            ForgeMachineRegistrationApi.Log("[INFO] [NicokoboForge/Machine] optional native furnace hooks installed; hooks=2");
            return true;
        }
        catch (Exception ex)
        {
            harmony.UnpatchSelf(); NativeFurnaceInstalled = false;
            ForgeMachineRegistrationApi.Log($"[WARN] [NicokoboForge/Machine] native furnace extension disabled: {ex.Message}");
            return false;
        }
    }

    private static System.Reflection.MethodInfo Require(Type type, string name, Type[] args, Type result)
    {
        var method = AccessTools.Method(type, name, args);
        if (method == null || method.ReturnType != result) throw new MissingMethodException(type.FullName, name);
        return method;
    }
    private static void Patch(HarmonyLib.Harmony harmony, Type type, string name, Type[] args, string? prefix, string? postfix)
    {
        var target = Require(type, name, args, prefix == nameof(AdmissionPrefix) ? typeof(bool) : typeof(void));
        harmony.Patch(target,
            prefix: prefix == null ? null : new HarmonyMethod(AccessTools.Method(typeof(ForgeMachineHooks), prefix)!),
            postfix: postfix == null ? null : new HarmonyMethod(AccessTools.Method(typeof(ForgeMachineHooks), postfix)!));
    }
    private static bool AdmissionPrefix(MachineFurnace.__c__DisplayClass0_0 __instance,
        GameItem item, ref bool __result)
    {
        var id = __instance?.furnace?.identifier;
        if (id == null || item == null || item.Pointer == IntPtr.Zero ||
            !ForgeMachineRegistrationApi.TryGet(id, out var profile) || profile?.NativeMachine != true ||
            !profile.Accepts(item.identifier)) return true;
        __result = true; return false;
    }
    private static bool CyclePrefix(MachineFurnace.__c__DisplayClass0_0 __instance)
    {
        try
        {
            var machine = __instance?.furnace;
            if (machine == null || !ForgeMachineRegistrationApi.TryGet(machine.identifier, out var profile) ||
                profile?.NativeMachine != true) return true;
            return ForgeMachineRuntime.NativeOwnsNight(profile, machine, __instance!.itemInventoryLeft);
        }
        catch (Exception ex)
        {
            ForgeMachineRegistrationApi.Log($"[WARN] [NicokoboForge/Machine] native cycle decision failed: {ex.Message}");
            return true;
        }
    }
}
