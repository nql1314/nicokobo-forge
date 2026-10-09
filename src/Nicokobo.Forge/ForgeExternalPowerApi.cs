using HarmonyLib;
using Il2Cpp;
using Nicokobo.Forge.Runtime;
using UnityEngine;

namespace Nicokobo.Forge;

/// <summary>Content defines which real batteries supply a connector. Forge
/// adapts the native power entry points and owns the enclosing transaction.</summary>
public static class ForgeExternalPowerApi
{
    private sealed record Provider(Func<GameItem, int> Energy,
        Func<GameItem, GameItem, int, IForgePowerTransaction?> Plan);
    private static readonly Dictionary<string, Provider> Providers = new(StringComparer.Ordinal);
    private static Action<string>? _log;
    private static bool _installed;
    [ThreadStatic] private static NativeBatch? _native;
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
        if (!allowed) return;
        _installed = NativeHookSet.Install("nicokobo.forge.external_power",
        [
            new(typeof(PowerHelper), nameof(PowerHelper.GetAvailableEnergyFromItem), [typeof(GameItem), typeof(bool)], typeof(int), typeof(ForgeExternalPowerApi), nameof(ReadEnergy)),
            new(typeof(PowerHelper), nameof(PowerHelper.CanDrawPowerSource), [typeof(GameItem), typeof(int)], typeof(bool), typeof(ForgeExternalPowerApi), nameof(CanDraw)),
            new(typeof(PowerHelper), nameof(PowerHelper.DrawPowerSource), [typeof(GameItem), typeof(int)], typeof(bool), typeof(ForgeExternalPowerApi), nameof(Draw)),
            new(typeof(PowerHelper), nameof(PowerHelper.ChargeBattery), [typeof(GameItem)], typeof(void), typeof(ForgeExternalPowerApi), nameof(RejectRecharge)),
            new(typeof(PowerHelper), nameof(PowerHelper.ChargeBattery), [typeof(GameItem), typeof(int)], typeof(void), typeof(ForgeExternalPowerApi), nameof(RejectRecharge)),
            new(typeof(PowerHelper), nameof(PowerHelper.SetPowerSourceAt), [typeof(GameItem), typeof(int)], typeof(void), typeof(ForgeExternalPowerApi), nameof(RejectCharge)),
            new(typeof(PowerHelper), nameof(PowerHelper.InitPowerSourceItem), [typeof(GameItem), typeof(int), typeof(int)], typeof(void), typeof(ForgeExternalPowerApi), nameof(RejectCharge)),
            new(typeof(PowerHelper), nameof(PowerHelper.IsPowerSourceFull), [typeof(GameItem)], typeof(bool), typeof(ForgeExternalPowerApi), nameof(ConnectorFull)),
            new(typeof(PowerHelper), nameof(PowerHelper.CreatePowerSourceItemTooltip), [typeof(RichTextBuilder), typeof(GameItem)], typeof(void), typeof(ForgeExternalPowerApi), nameof(ConnectorTooltip)),
            new(typeof(PowerHelper), nameof(PowerHelper.TryRemoveEnergy), [typeof(GameItem), typeof(int), typeof(bool)], typeof(int), typeof(ForgeExternalPowerApi), nameof(RejectRemoval)),
            new(typeof(MachineFurnace.__c__DisplayClass0_0), "_Furnace_b__5", [typeof(GameItem), typeof(GameInventory), typeof(SlotMarker)], typeof(void), typeof(ForgeExternalPowerApi), nameof(BeforeNativeBatch), nameof(AfterNativeBatch), nameof(FinishNativeBatch)),
            // The generated GameItem wrapper hides a native abstract method.
            // GameItemElement owns the current game's concrete destruction body;
            // GameCharacterItem inherits it without another override.
            new(typeof(GameItemElement), nameof(GameItemElement.Destroy), [], typeof(void), typeof(ForgeExternalPowerApi), nameof(DeferConsumedDestroy))
        ], log);
    }
    private static bool ReadEnergy(GameItem __0, ref int __result)
    { if (!IsConnector(__0)) return true; __result = GetEnergy(__0); return false; }
    private static bool CanDraw(GameItem __0, int __1, ref bool __result)
    { if (!IsConnector(__0)) return true; __result = __1 >= 0 && Connected(__0) && GetEnergy(__0) >= __1; return false; }
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
        if (!IsConnector(__0)) return true;
        __result = false;
        if (__1 == 0)
        { __result = Connected(__0); if (__result && _native != null) _native.ZeroCost = true; return false; }
        var batch = _native;
        if (batch == null || batch.Connector.Pointer != __0.Pointer || batch.Power != null || __1 < 0) return false;
        try
        {
            batch.Power = Plan(batch.Machine, __0, __1);
            if (batch.Power == null || !batch.Power.Validate()) return false;
            batch.Power.Debit();
            __result = batch.Power.Verify();
            batch.Debited = __result;
            if (!__result) batch.Failed = true;
        }
        catch (Exception ex) { batch.Failed = true; _log?.Invoke("[WARN] [NicokoboForge/Power] native debit failed: " + ex.Message); }
        return false;
    }

    private sealed record ItemState(GameItem Item, GameInventory Parent, int Units, int Id,
        string Identifier, long Value, GridShape Shape, TagSystem State, int Index,
        IntPtr Handler, Vector3? LocalPosition);
    private static ItemState CaptureSnapshot(GameItem item, GameInventory parent)
    {
        var slot = item.GetIncompleteSlotInInventory();
        if (slot?.item?.Pointer != item.Pointer || slot.inventory?.Pointer != parent.Pointer ||
            slot.itemGridShape == null || slot.index < 0)
            throw new InvalidOperationException("Native batch item has no original inventory slot");
        var handler = item.TryCast<GameItemElement>()?.handler;
        return new(item, parent, item.unitCount, item.GetUniqueID(), item.identifier, item.unitValue,
            slot.itemGridShape.Clone(), item.state.Clone(), slot.index,
            handler?.Pointer ?? IntPtr.Zero, handler?.transform.localPosition);
    }
    private sealed class NativeBatch(GameItem machine, GameItem connector, GameInventory input, GameInventory output,
        IReadOnlyList<ItemState> items, IReadOnlyList<ItemState> outputItems)
    {
        internal readonly GameItem Machine = machine, Connector = connector;
        internal readonly GameInventory Input = input, Output = output;
        internal readonly IReadOnlyList<ItemState> Items = items;
        internal readonly IReadOnlyList<ItemState> OutputItems = outputItems;
        internal readonly IReadOnlySet<IntPtr> Outputs = outputItems.Select(item => item.Item.Pointer).ToHashSet();
        internal readonly List<GameItem> Deferred = [];
        internal IForgePowerTransaction? Power;
        internal bool Debited, ZeroCost, Failed, Finished;
    }
    [HarmonyPriority(int.MinValue)]
    private static bool BeforeNativeBatch(MachineFurnace.__c__DisplayClass0_0 __instance, out NativeBatch? __state)
    {
        __state = null;
        if (__instance == null) return true;
        var machine = __instance.furnace;
        var connector = MachineHelper.GetBatterySlot(machine)?.childItem;
        if (!IsConnector(connector)) return true;
        if (_native != null || machine.GetTagReadonly("NICOKOBO_FORGE_MACHINE_FAULT")?.valueString is { Length: > 0 }) return false;
        try
        {
            var input = __instance.itemInventoryLeft;
            var layout = machine.contentWindow?.childElement?.TryCast<GridPixelElement>();
            var output = layout?.GetElement(3, 1)?.TryCast<GameInventory>();
            if (input == null || output == null || !GeneralHelper.IsItemOwned(machine)) return false;
            var items = input.childItems.ToArray().Select(item => CaptureSnapshot(item, input)).ToArray();
            var outputs = output.childItems.ToArray().Select(item => CaptureSnapshot(item, output)).ToArray();
            __state = new(machine, connector!, input, output, items, outputs);
            _native = __state;
            return true;
        }
        catch (Exception ex) { _log?.Invoke("[WARN] [NicokoboForge/Power] native batch preflight rejected: " + ex.Message); return false; }
    }
    private static bool DeferConsumedDestroy(GameItem __instance)
    {
        var batch = _native;
        if (batch == null || batch.Finished) return true;
        var snapshot = batch.Items.Concat(batch.OutputItems).FirstOrDefault(item => item.Item.Pointer == __instance.Pointer);
        if (snapshot == null) return true;
        if (!batch.Deferred.Any(item => item.Pointer == __instance.Pointer)) batch.Deferred.Add(__instance);
        // Preserve Destroy's initial Expel, but retain UID, content window and
        // Unity handlers until the batch commits. Rollback reuses this instance.
        NativeBatchFinalization.DetachForDeferredDestruction(
            () => __instance.GetUniqueID() == snapshot.Id && __instance.identifier == snapshot.Identifier &&
                (__instance.parentInventory == null || __instance.parentInventory.Pointer == snapshot.Parent.Pointer),
            () => __instance.parentInventory == null,
            () => snapshot.Parent.Expel(__instance),
            reason => { batch.Failed = true; try { _log?.Invoke("[WARN] [NicokoboForge/Power] " + reason); } catch { } });
        return false;
    }
    [HarmonyPriority(int.MaxValue)]
    private static void AfterNativeBatch(NativeBatch? __state, bool __runOriginal)
    { if (__state != null) Finish(__state, !__runOriginal); }
    private static Exception? FinishNativeBatch(Exception? __exception, NativeBatch? __state)
    { if (__state != null && !__state.Finished) Finish(__state, __exception != null); return __exception; }
    private static void Finish(NativeBatch batch, bool failed)
    {
        if (batch.Finished) return;
        var restore = new List<Func<bool>> { () => RemoveNewOutputs(batch) };
        restore.AddRange(batch.Items.Concat(batch.OutputItems).Select(snapshot => (Func<bool>)(() => RestoreSnapshot(snapshot))));
        restore.Add(() => batch.Power?.Restore() ?? true);
        bool committed = NativeBatchFinalization.Complete(() =>
        {
            bool consumed = batch.Items.Any(item => item.Item.parentInventory?.Pointer != item.Parent.Pointer || item.Item.unitCount < item.Units);
            bool produced = batch.Output.childItems.ToArray().Any(item => !batch.Outputs.Contains(item.Pointer)) ||
                batch.Output.childItems.ToArray().Sum(item => (long)item.unitCount) > batch.OutputItems.Sum(item => (long)item.Units);
            return !failed && !batch.Failed && (batch.ZeroCost || batch.Debited && (batch.Power?.Verify() ?? false)) && consumed && produced;
        }, restore, reason => FaultBatch(batch, reason), () =>
        {
            batch.Finished = true; if (ReferenceEquals(_native, batch)) _native = null;
            batch.Power?.Dispose();
        });
        if (committed)
            foreach (var item in batch.Deferred)
                try { if (item.parentInventory == null) item.Destroy(); }
                catch (Exception ex) { FaultBatch(batch, "committed input cleanup failed: " + ex.Message); }
    }
    private static bool RemoveNewOutputs(NativeBatch batch)
    {
        bool restored = true;
        foreach (var item in batch.Output.childItems.ToArray().Where(item => !batch.Outputs.Contains(item.Pointer)))
            try
            {
                bool locked = batch.Output.overrideLockRemove; batch.Output.overrideLockRemove = false;
                try { if (!batch.Output.Expel(item)) restored = false; else item.Destroy(); }
                finally { batch.Output.overrideLockRemove = locked; }
            }
            catch { restored = false; }
        return restored;
    }
    private static bool RestoreSnapshot(ItemState snapshot)
    {
        var item = snapshot.Item;
        if (item.GetUniqueID() != snapshot.Id || item.identifier != snapshot.Identifier ||
            item.parentInventory != null && item.parentInventory.Pointer != snapshot.Parent.Pointer) return false;
        var handler = item.TryCast<GameItemElement>()?.handler;
        if (snapshot.Handler != IntPtr.Zero && handler?.Pointer != snapshot.Handler) return false;
        item.state = snapshot.State.Clone(); item.SyncModifiedState();
        item.SetUnitCount(snapshot.Units); item.unitValue = snapshot.Value; item.modifiedShape = snapshot.Shape.Clone();
        if (snapshot.LocalPosition is { } before) handler!.transform.localPosition = before;
        if (item.parentInventory == null)
        {
            var parent = snapshot.Parent;
            var admission = parent.mayInventoryAddItemFunc;
            var onAdd = parent.onSlotAddItemFunc;
            var onEnd = parent.onSlotAddItemEndFunc;
            var onLate = parent.onSlotAddItemLateFunc;
            var onItemAdd = item.onSlotAddItemFunc;
            bool locked = parent.overrideLockInsert;
            try
            {
                // Recovery is into this instance's vacated slot; it must not
                // merge with another stack or replay insertion side effects.
                parent.mayInventoryAddItemFunc = null;
                parent.onSlotAddItemFunc = parent.onSlotAddItemEndFunc = parent.onSlotAddItemLateFunc = null;
                item.onSlotAddItemFunc = null; parent.overrideLockInsert = false;
                var slot = parent.TryInventorySlot(item, snapshot.Units, snapshot.Shape);
                if (slot == null || !slot.IsValid() || slot.targetItem != null ||
                    slot.item?.Pointer != item.Pointer || slot.inventory?.Pointer != parent.Pointer ||
                    slot.numTransfer < snapshot.Units || !parent.UncheckedAccept(item)) return false;
            }
            finally
            {
                parent.mayInventoryAddItemFunc = admission;
                parent.onSlotAddItemFunc = onAdd; parent.onSlotAddItemEndFunc = onEnd; parent.onSlotAddItemLateFunc = onLate;
                item.onSlotAddItemFunc = onItemAdd; parent.overrideLockInsert = locked;
            }
        }
        if (item.parentInventory?.Pointer != snapshot.Parent.Pointer) return false;
        // Native detachment preserves world render position, while attachment
        // preserves local position and recomputes shape. Restore both explicitly.
        handler = item.TryCast<GameItemElement>()?.handler;
        if (snapshot.Handler != IntPtr.Zero && handler?.Pointer != snapshot.Handler) return false;
        item.modifiedShape = snapshot.Shape.Clone();
        if (snapshot.LocalPosition is { } position) handler!.transform.localPosition = position;
        var children = snapshot.Parent.childItems;
        int current = children.IndexOf(item);
        if (current < 0 || snapshot.Index >= children.Count) return false;
        if (current != snapshot.Index) { children.RemoveAt(current); children.Insert(snapshot.Index, item); }
        var actualSlot = item.GetIncompleteSlotInInventory();
        var shape = actualSlot?.itemGridShape;
        bool sameShape = shape != null && shape.minX == snapshot.Shape.minX && shape.minY == snapshot.Shape.minY &&
            shape.width == snapshot.Shape.width && shape.height == snapshot.Shape.height &&
            shape.globalWidth == snapshot.Shape.globalWidth && shape.globalHeight == snapshot.Shape.globalHeight &&
            shape.flipped == snapshot.Shape.flipped && shape.orientation == snapshot.Shape.orientation;
        bool samePosition = snapshot.LocalPosition == null || handler != null &&
            handler.transform.localPosition.Equals(snapshot.LocalPosition.Value);
        return item.unitCount == snapshot.Units && item.GetUniqueID() == snapshot.Id &&
            actualSlot?.index == snapshot.Index && sameShape && samePosition;
    }
    private static void FaultBatch(NativeBatch batch, string reason)
    {
        try { batch.Power?.Fault(reason); } catch { }
        try
        {
            var tag = batch.Machine.state.GetTag("NICOKOBO_FORGE_MACHINE_FAULT");
            tag.Enable(); tag.SetString(reason); batch.Machine.SyncModifiedState();
        }
        catch { }
        try { _log?.Invoke("[ERROR] [NicokoboForge/Power] native machine and external supply quarantined: " + reason); } catch { }
    }
}
