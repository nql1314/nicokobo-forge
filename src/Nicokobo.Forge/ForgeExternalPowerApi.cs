using HarmonyLib;
using Il2Cpp;
using Nicokobo.Forge.Runtime;

namespace Nicokobo.Forge;

/// <summary>Content defines which real batteries supply a connector. Forge
/// adapts the native power entry points and owns the enclosing transaction.</summary>
public static partial class ForgeExternalPowerApi
{
    private sealed record Provider(Func<GameItem, int> Energy,
        Func<GameItem, GameItem, int, IForgePowerTransaction?> Plan);
    private static readonly Dictionary<string, Provider> Providers = new(StringComparer.Ordinal);
    private sealed record MachineProvider(Func<GameItem, int> Energy, Func<GameItem, int, IForgePowerTransaction?> Plan);
    private static readonly Dictionary<string, MachineProvider> MachineProviders = new(StringComparer.Ordinal);
    private static Action<string>? _log;
    private static bool _installed;
    [ThreadStatic] private static NativeBatch? _native;
    [ThreadStatic] private static int _executingReceipt;
    public static bool IsAvailable => _installed;
    private static Provider? ProviderFor(GameItem? item) => item == null || item.Pointer == IntPtr.Zero
        ? null : PowerConnectorIdentity.Find(Providers, item.identifier);
    public static bool IsConnector(GameItem? item) => ProviderFor(item) != null;
    public static IDisposable Register(string ownerId, string itemId, Func<GameItem, int> energy,
        Func<GameItem, GameItem, int, IForgePowerTransaction?> plan)
    {
        if (!_installed) throw new InvalidOperationException("External power adapter unavailable");
        if (!OwnedCallbacks<bool, bool>.ValidId(ownerId, itemId) || energy == null || plan == null ||
            !Providers.TryAdd(itemId, new(energy, plan))) throw new ArgumentException("Unique owned connector and callbacks required");
        return new CallbackLease(() => Providers.Remove(itemId));
    }
    /// <summary>Supply a machine directly, even with an empty battery slot.
    /// Return -1 to keep native battery behavior, or >=0 to claim supply exclusively.
    /// A claimed empty or paused supply never falls back to the local battery.</summary>
    public static IDisposable RegisterMachine(string ownerId, string callbackId, Func<GameItem, int> energy,
        Func<GameItem, int, IForgePowerTransaction?> plan)
    {
        if (!_installed) throw new InvalidOperationException("External power adapter unavailable");
        if (!OwnedCallbacks<bool, bool>.ValidId(ownerId, callbackId) || energy == null || plan == null ||
            !MachineProviders.TryAdd(callbackId, new(energy, plan))) throw new ArgumentException("Unique owned machine supply callback required");
        return new CallbackLease(() => MachineProviders.Remove(callbackId));
    }
    private static bool MachineSupply(GameItem machine, out MachineProvider? found, out int energy)
    {
        found = null; energy = 0; bool claimed = false;
        if (!CanSupplyDevice(machine)) return false;
        foreach (var provider in MachineProviders.Values)
        {
            int amount;
            try { amount = provider.Energy(machine); }
            catch (Exception ex)
            { _log?.Invoke("[WARN] [NicokoboForge/Power] machine supply query failed: " + ex.Message); found = null; energy = 0; return true; }
            if (amount < 0) continue;
            if (claimed) { found = null; energy = 0; return true; }
            claimed = true; found = provider; energy = amount;
        }
        return claimed;
    }
    public static bool TryGetMachineEnergy(GameItem machine, out int energy) => MachineSupply(machine, out _, out energy);
    internal static IForgePowerTransaction? PlanMachine(GameItem machine, int cost) =>
        cost > 0 && MachineSupply(machine, out var provider, out int energy) && energy >= cost ? provider?.Plan(machine, cost) : null;

    public static int GetEnergy(GameItem connector)
    {
        if (!_installed || ProviderFor(connector) is not { } provider) return 0;
        try { return Math.Max(0, provider.Energy(connector)); }
        catch (Exception ex) { _log?.Invoke("[WARN] [NicokoboForge/Power] query rejected: " + ex.Message); return 0; }
    }
    private static bool Connected(GameItem connector)
    {
        if (!_installed || ProviderFor(connector) is not { } provider) return false;
        try { return provider.Energy(connector) >= 0; } catch { return false; }
    }
    internal static IForgePowerTransaction? Plan(GameItem machine, GameItem connector, int cost) =>
        _installed && cost > 0 && ProviderFor(connector) is { } provider
            ? provider.Plan(machine, connector, cost) : null;

    internal static void Install(bool allowed, Action<string> log)
    {
        _log = log;
        NativeDevices.Clear();
        if (!allowed) { _installed = false; return; }
        _installed = NativeHookSet.Install("nicokobo.forge.external_power",
        [
            new(typeof(PowerHelper), nameof(PowerHelper.GetAvailableEnergyFromItem), [typeof(GameItem), typeof(bool)], typeof(int), typeof(ForgeExternalPowerApi), nameof(ReadEnergy)),
            new(typeof(PowerHelper), nameof(PowerHelper.CanDrawPowerSource), [typeof(GameItem), typeof(int)], typeof(bool), typeof(ForgeExternalPowerApi), nameof(CanDraw)),
            new(typeof(PowerHelper), nameof(PowerHelper.DrawPowerSource), [typeof(GameItem), typeof(int)], typeof(bool), typeof(ForgeExternalPowerApi), nameof(Draw)),
            new(typeof(PowerHelper), nameof(PowerHelper.ChargeBattery), [typeof(GameItem)], typeof(void), typeof(ForgeExternalPowerApi), nameof(RejectRecharge)),
            new(typeof(PowerHelper), nameof(PowerHelper.ChargeBattery), [typeof(GameItem), typeof(int), typeof(bool)], typeof(void), typeof(ForgeExternalPowerApi), nameof(RejectRecharge)),
            new(typeof(PowerHelper), nameof(PowerHelper.SetPowerSourceAt), [typeof(GameItem), typeof(int)], typeof(void), typeof(ForgeExternalPowerApi), nameof(RejectCharge)),
            new(typeof(PowerHelper), nameof(PowerHelper.InitPowerSourceItem), [typeof(GameItem), typeof(int), typeof(int)], typeof(void), typeof(ForgeExternalPowerApi), nameof(RejectCharge)),
            new(typeof(PowerHelper), nameof(PowerHelper.IsPowerSourceFull), [typeof(GameItem)], typeof(bool), typeof(ForgeExternalPowerApi), nameof(ConnectorFull)),
            new(typeof(PowerHelper), nameof(PowerHelper.TryRemoveEnergy), [typeof(GameItem), typeof(int), typeof(bool)], typeof(int), typeof(ForgeExternalPowerApi), nameof(RejectRemoval)),
            new(typeof(MachineHelper), nameof(MachineHelper.CanPower), [typeof(GameItem)], typeof(bool), typeof(ForgeExternalPowerApi), nameof(CanPowerMachine)),
            new(typeof(MachineHelper), nameof(MachineHelper.CanPower), [typeof(GameItem), typeof(GameSlotInventory)], typeof(bool), typeof(ForgeExternalPowerApi), nameof(CanPowerMachine)),
            new(typeof(MachineHelper), nameof(MachineHelper.HasBattery), [typeof(GameItem)], typeof(bool), typeof(ForgeExternalPowerApi), nameof(HasMachineSupply)),
            new(typeof(MachineHelper), nameof(MachineHelper.TryDrawCyclePower), [typeof(GameItem), typeof(GameSlotInventory)], typeof(bool), typeof(ForgeExternalPowerApi), nameof(DrawMachine)),
            new(typeof(PowerHelper), nameof(PowerHelper.CanDrawPowerSource), [typeof(GameItem), typeof(int)], typeof(bool), typeof(ForgeExternalPowerApi), nameof(CanDrawToolQuery)),
            // The generated GameItem wrapper hides a native abstract method.
            // GameItemElement owns the current game's concrete destruction body;
            // GameCharacterItem inherits it without another override.
            new(typeof(GameItemElement), nameof(GameItemElement.Destroy), [], typeof(void), typeof(ForgeExternalPowerApi), nameof(DeferConsumedDestroy))
        ], log);
        if (!_installed) return;
        foreach (var (device, hooks) in NativeMachineHooks())
            if (NativeHookSet.Install("nicokobo.forge.external_power." + device, hooks, log))
                NativeDevices.Add(device);
        // Display adapters never authorize energy debits or native actions.
        NativeHookSet.Install("nicokobo.forge.external_power.tooltip",
            [new(typeof(PowerHelper), nameof(PowerHelper.CreatePowerSourceItemTooltip), [typeof(RichTextBuilder), typeof(GameItem)], typeof(void), typeof(ForgeExternalPowerApi), nameof(ConnectorTooltip))], log);
        NativeHookSet.Install("nicokobo.forge.external_power.aug_scanner_display",
            [new(typeof(AugScanner), nameof(AugScanner.CreateTooltip), [typeof(RichTextBuilder), typeof(GameItem)], typeof(void), typeof(ForgeExternalPowerApi),
                nameof(BeforeAugScannerTooltip), nameof(AfterToolPowerQuery), nameof(FinishToolPowerQuery))], log);
        NativeHookSet.Install("nicokobo.forge.external_power.booster_display",
            [new(typeof(MachineTurboBoosterAdv), nameof(MachineTurboBoosterAdv.UpdateSprite), [typeof(GameItem), typeof(GameSlotInventory)], typeof(void), typeof(ForgeExternalPowerApi),
                nameof(BeforeBoosterSprite), nameof(AfterToolPowerQuery), nameof(FinishToolPowerQuery))], log);
    }
    private static bool ReadEnergy(GameItem __0, ref int __result)
    {
        if (_executingReceipt != 0) return true;
        if (__0 == null) return true;
        if (_native is { Direct: true } batch && batch.Machine.Pointer == __0.Pointer)
        { TryGetMachineEnergy(batch.Machine, out __result); return false; }
        if (!IsConnector(__0)) return true; __result = GetEnergy(__0); return false;
    }
    private static bool CanPowerMachine(GameItem __0, ref bool __result)
    {
        if (_executingReceipt != 0) return true;
        if (!TryGetMachineEnergy(__0, out int energy)) return true;
        int cost = ForgePowerApi.GetMachineCost(__0);
        __result = cost >= 0 && energy >= cost; return false;
    }
    private static bool HasMachineSupply(GameItem __0, ref bool __result)
    { if (_executingReceipt != 0 || !TryGetMachineEnergy(__0, out _)) return true; __result = true; return false; }
    private static bool DrawMachine(GameItem __0, ref bool __result)
    {
        if (_executingReceipt != 0 || !TryGetMachineEnergy(__0, out _)) return true;
        __result = false; var batch = _native;
        if (batch == null || !batch.Direct || batch.Machine.Pointer != __0.Pointer) return false;
        int cost = ForgePowerApi.GetMachineCost(__0);
        __result = DrawNativePower(batch, cost);
        return false;
    }
    private static bool CanDraw(GameItem __0, int __1, ref bool __result)
    {
        if (_executingReceipt != 0 || __0 == null) return true;
        if (_native is { Direct: true } batch && batch.Machine.Pointer == __0.Pointer)
        {
            __result = __1 >= 0 && (batch.Power.HasReservation && !batch.Power.WasDrawn
                ? batch.Power.Prepare(__1) : TryGetMachineEnergy(batch.Machine, out int energy) && energy >= __1 &&
                    (batch.ReadinessOnly || batch.Power.WasDrawn || batch.Power.Prepare(__1)));
            return false;
        }
        if (!IsConnector(__0)) return true;
        var connectorBatch = _native?.Connector?.Pointer == __0.Pointer ? _native : null;
        __result = __1 >= 0 && (connectorBatch is { Power.HasReservation: true, Power.WasDrawn: false }
            ? connectorBatch.Power.Prepare(__1) : Connected(__0) && GetEnergy(__0) >= __1 &&
                (connectorBatch == null || connectorBatch.ReadinessOnly || connectorBatch.Power.WasDrawn || connectorBatch.Power.Prepare(__1)));
        return false;
    }
    private static bool RejectCharge(GameItem __0) => !IsConnector(__0);
    private static bool RejectRecharge(GameItem __0) => !IsConnector(__0) && !ForgeInventoryFreezeApi.IsFrozen(__0);
    private static bool RejectRemoval(GameItem __0, ref int __result)
    { if (!IsConnector(__0)) return true; __result = 0; return false; }
    private static bool ConnectorFull(GameItem __0, ref bool __result)
    { if (!IsConnector(__0)) return true; __result = true; return false; }
    private static bool ConnectorTooltip(RichTextBuilder __0, GameItem __1)
    {
        if (!IsConnector(__1)) return true;
        __0.AddLine(Connected(__1) ? "电网连接 · 当前可用电量 " + GetEnergy(__1) : "电网未连接或供电已暂停");
        __0.AddLine("电线不储电、无容量，不能充电"); return false;
    }
    private static bool Draw(GameItem __0, int __1, ref bool __result)
    {
        if (_executingReceipt != 0 || __0 == null) return true;
        if (_native is { Direct: true } direct && direct.Machine.Pointer == __0.Pointer)
        { __result = DrawNativePower(direct, __1); return false; }
        if (!IsConnector(__0)) return true;
        __result = false;
        var batch = _native;
        if (batch != null && batch.Connector?.Pointer == __0.Pointer && !batch.ReadinessOnly)
            __result = DrawNativePower(batch, __1);
        return false;
    }

    private static bool DrawNativePower(NativeBatch batch, int cost)
    {
        if (batch.ReadinessOnly) return false;
        // A normal native no-power branch may clear powered/active tags. Keep
        // that result; only a failed attempted receipt requires compensation.
        if (!batch.Power.HasReservation && cost >= 0 && (batch.Direct
            ? !TryGetMachineEnergy(batch.Machine, out int energy) || energy < cost
            : batch.Connector == null || !Connected(batch.Connector) || GetEnergy(batch.Connector) < cost)) return false;
        return batch.Power.Draw(cost);
    }
}
