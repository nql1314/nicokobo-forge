using Il2Cpp;
using Nicokobo.Forge.Runtime;

namespace Nicokobo.Forge;

public static partial class ForgeExternalPowerApi
{
    private static readonly Dictionary<string, Func<GameItem, GameItem?>> Consumers = new(StringComparer.Ordinal);

    /// <summary>Declare an owned powered device and resolve its real local source.
    /// This metadata also works without an installed external supply. The caller
    /// uses ForgePowerApi.GetDeviceEnergy / TryDebitDevice at its actual operation.</summary>
    public static IDisposable RegisterConsumer(string ownerId, string itemId, Func<GameItem, GameItem?> source)
    {
        if (!OwnedCallbacks<bool, bool>.ValidId(ownerId, itemId) || source == null ||
            !Consumers.TryAdd(itemId, source)) throw new ArgumentException("Unique owned power consumer and source resolver required");
        return new CallbackLease(() => Consumers.Remove(itemId));
    }

    /// <summary>Whether the adapter recognizes a native, registered-machine or
    /// content-declared power consumer. Supply ownership and energy are queried separately.</summary>
    public static bool CanSupplyDevice(GameItem device) => _installed && device != null && device.Pointer != IntPtr.Zero &&
        (NativeMachine(device.identifier) || NativeTool(device.identifier)
            ? NativeDevices.Contains(device.identifier)
            : Consumers.ContainsKey(device.identifier) || ForgeMachineRegistrationApi.TryGet(device.identifier, out _));

    /// <summary>Resolve the actual source, including when external power is unavailable.
    /// This never attaches an inventory or substitutes a virtual battery.</summary>
    public static GameItem? GetDevicePowerSource(GameItem device)
    {
        if (device == null || device.Pointer == IntPtr.Zero) return null;
        if (Consumers.TryGetValue(device.identifier, out var resolve))
        {
            try { return resolve(device); }
            catch (Exception ex) { _log?.Invoke("[WARN] [NicokoboForge/Power] consumer source unavailable: " + ex.Message); return null; }
        }
        if (ForgeMachineRegistrationApi.TryGet(device.identifier, out _))
            return ForgeMachineRuntimeApi.TryGetInventory(device, out var inventory) ? inventory?.Battery?.childItem : null;
        if (!NativeMachine(device.identifier) && !NativeTool(device.identifier)) return null;
        var layout = device.contentWindow?.childElement?.TryCast<GridPixelElement>();
        if (layout == null || layout.gridWidth < 1 || layout.gridHeight < 2) return null;
        return layout.GetElement(0, 1)?.TryCast<GameSlotInventory>()?.childItem;
    }

    internal static ForgeEnergyDebitResult DebitConsumer(GameItem device, int cost, int before)
    {
        using var action = new NativePowerAction(amount => PlanMachine(device, amount) is { } receipt ? new ReceiptGuard(receipt) : null);
        try
        {
            if (action.Draw(cost) && action.Verify())
                return new(ForgeEnergyDebitStatus.Committed, before, ForgePowerApi.GetDeviceEnergy(device));
        }
        catch { /* A failed observation must compensate the attempted debit. */ }
        bool restored;
        try { restored = action.Restore(); } catch { restored = false; }
        if (!restored)
        {
            ReportBatchFailure("Power consumer debit could not be restored");
        }
        int after;
        try { after = ForgePowerApi.GetDeviceEnergy(device); } catch { after = -1; }
        return new(restored ? ForgeEnergyDebitStatus.Restored : ForgeEnergyDebitStatus.Faulted, before, after);
    }
}

public static partial class ForgePowerApi
{
    /// <summary>Read usable device energy. Without a claimed external supply,
    /// read only the device's real local source.</summary>
    public static int GetDeviceEnergy(GameItem device)
    {
        Require(device);
        if (ForgeExternalPowerApi.TryGetMachineEnergy(device, out int energy)) return Math.Max(0, energy);
        var source = ForgeExternalPowerApi.GetDevicePowerSource(device);
        return source == null || source.Pointer == IntPtr.Zero ? 0 : GetEnergy(source);
    }

    /// <summary>Debit one actual device operation, using the external receipt if claimed.
    /// Status is authoritative; Before/After are usable-energy observations and may
    /// remain capacity-capped when a larger external reserve still supplies the device.</summary>
    public static ForgeEnergyDebitResult TryDebitDevice(GameItem device, int cost)
    {
        if (!ForgeCapabilities.Current.KnownGameBuild || device == null || device.Pointer == IntPtr.Zero || cost < 0)
            return new(ForgeEnergyDebitStatus.Invalid, 0, 0);
        var source = ForgeExternalPowerApi.GetDevicePowerSource(device);
        if (source == null || source.Pointer == IntPtr.Zero || source.unitCount <= 0)
            return new(ForgeEnergyDebitStatus.Invalid, 0, 0);
        if (!ForgeExternalPowerApi.TryGetMachineEnergy(device, out int before)) return TryDebit(source, cost);
        if (before < cost) return new(ForgeEnergyDebitStatus.Insufficient, before, before);
        if (cost == 0) return new(ForgeEnergyDebitStatus.Committed, before, before);
        return ForgeExternalPowerApi.DebitConsumer(device, cost, before);
    }
}
