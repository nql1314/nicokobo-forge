using HarmonyLib;
using Il2Cpp;
using Nicokobo.Forge.Runtime;

namespace Nicokobo.Forge;

internal static class ForgeMachineHooks
{
    private static readonly List<IDisposable> Lifecycle = [];
    private const string RoutingOwner = "nicokobo.forge.machines.routing";
    private const string AcceleratorOwner = "nicokobo.forge.machines.accelerators";
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
            if (!NativeHookSet.Install(AcceleratorOwner,
            [
                new(typeof(ToolDirectory.__c__DisplayClass23_0), "_CreateTurboBooster_b__4",
                    [typeof(GameItem), typeof(GameItem)], typeof(void), typeof(ForgeMachineHooks), nameof(ActivateMachine)),
                new(typeof(ToolDirectory.__c__DisplayClass24_0), "_CreateTurboBoosterAdv_b__7",
                    [typeof(GameItem), typeof(GameItem)], typeof(void), typeof(ForgeMachineHooks), nameof(ActivateMachine))
            ], log))
                throw new InvalidOperationException("Native accelerator routing unavailable");
            Lifecycle.Add(ForgeLifecycleApi.Subscribe(owner, owner + ".rebind", ForgeLifecyclePhase.AfterLoad,
                context => { ForgeMachineRuntime.BeforeLoadGame(); ForgeMachineRuntime.AfterLoadGame(context); },
                ForgeNumbers.Machines.LifecyclePriority));
            Lifecycle.Add(ForgeLifecycleApi.Subscribe(owner, owner + ".process", ForgeLifecyclePhase.BeforeNight,
                ForgeMachineRuntime.BeforeEndNight));
            Installed = true;
            log("[INFO] [NicokoboForge/Machine] templates installed; shared lifecycle; machineDropRouting=1; acceleratorRouting=2; globalWindowHooks=0");
            return true;
        }
        catch (Exception ex)
        {
            foreach (var lease in Lifecycle) lease.Dispose();
            Lifecycle.Clear(); Installed = false;
            NativeHookSet.Remove(RoutingOwner, log);
            NativeHookSet.Remove(AcceleratorOwner, log);
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

    private static bool ActivateMachine(GameItem __1)
    {
        if (!Installed || __1 == null || __1.Pointer == IntPtr.Zero ||
            !ForgeMachineRegistrationApi.TryGet(__1.identifier, out var profile) || profile == null) return true;
        try
        {
            // The native tool calls the cleared furnace cycle delegates. Run one
            // real Forge batch, then let it consume the booster/play its sound.
            // A rejected batch leaves the disposable tool or advanced charge intact.
            string status = ForgeMachineRuntime.ProcessActivated(__1, profile);
            ForgeMachineRegistrationApi.Log($"[INFO] [NicokoboForge/Machine] accelerator={__1.identifier}; status={status}");
            return status.StartsWith("produced:", StringComparison.Ordinal);
        }
        catch (Exception ex)
        {
            ForgeMachineRegistrationApi.Log($"[WARN] [NicokoboForge/Machine] accelerator rejected: {ex.Message}");
            return false;
        }
    }

    private static System.Reflection.MethodInfo Require(Type type, string name, Type[] args, Type result)
    {
        var method = AccessTools.Method(type, name, args);
        if (method == null || method.ReturnType != result) throw new MissingMethodException(type.FullName, name);
        return method;
    }
}
