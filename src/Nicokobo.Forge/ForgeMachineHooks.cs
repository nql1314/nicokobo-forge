using HarmonyLib;
using Il2Cpp;
using Nicokobo.Forge.Runtime;

namespace Nicokobo.Forge;

internal static class ForgeMachineHooks
{
    private static readonly List<IDisposable> Lifecycle = [];
    private const string RoutingOwner = "nicokobo.forge.machines.routing";
    internal static bool Installed { get; private set; }

    internal static bool Install(bool knownBuild, Action<string> log)
    {
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
            NativeProductionValue.Validate();
            if (!ForgeLiquidValueRuntime.Install(log))
                throw new InvalidOperationException("Native liquid value adapters unavailable");
            if (!NativeHookSet.Install(RoutingOwner,
                [new(typeof(GameItem), nameof(GameItem.TryFindOneValidInventorySlot),
                    [typeof(GameItem)], typeof(SlotMarker), typeof(ForgeMachineHooks), nameof(FindDropSlot))], log))
                throw new InvalidOperationException("Native machine drop routing unavailable");
            Lifecycle.Add(ForgeLifecycleApi.Subscribe(owner, owner + ".reset", ForgeLifecyclePhase.BeforeLoad,
                _ => ForgeMachineRuntime.BeforeLoadGame(), ForgeNumbers.Machines.LifecyclePriority));
            Lifecycle.Add(ForgeLifecycleApi.Subscribe(owner, owner + ".rebind", ForgeLifecyclePhase.AfterLoad,
                ForgeMachineRuntime.AfterLoadGame, ForgeNumbers.Machines.LifecyclePriority));
            Lifecycle.Add(ForgeLifecycleApi.Subscribe(owner, owner + ".process", ForgeLifecyclePhase.BeforeNight,
                ForgeMachineRuntime.BeforeEndNight));
            Installed = true;
            log("[INFO] [NicokoboForge/Machine] templates installed; shared lifecycle; machineDropRouting=1; globalWindowHooks=0");
            return true;
        }
        catch (Exception ex)
        {
            foreach (var lease in Lifecycle) lease.Dispose();
            Lifecycle.Clear(); Installed = false;
            NativeHookSet.Remove(RoutingOwner, log);
            ForgeLiquidValueRuntime.Uninstall();
            log($"[WARN] [NicokoboForge/Machine] template runtime disabled: {ex.Message}");
            return false;
        }
    }

    private static bool FindDropSlot(GameItem __instance, GameItem item, ref SlotMarker? __result)
    {
        if (!Installed || __instance == null || __instance.Pointer == IntPtr.Zero ||
            !ForgeMachineRegistrationApi.TryGet(__instance.identifier, out _)) return true;
        try { __result = ForgeMachineUi.FindDropSlot(__instance, item); }
        catch (Exception ex)
        {
            // Fall back to the native slot search instead of rejecting the drop.
            ForgeMachineRegistrationApi.Log($"[WARN] [NicokoboForge/Machine] drop routing failed; native fallback: {ex.Message}");
            return true;
        }
        return false;
    }

    private static System.Reflection.MethodInfo Require(Type type, string name, Type[] args, Type result)
    {
        var method = AccessTools.Method(type, name, args);
        if (method == null || method.ReturnType != result) throw new MissingMethodException(type.FullName, name);
        return method;
    }
}
