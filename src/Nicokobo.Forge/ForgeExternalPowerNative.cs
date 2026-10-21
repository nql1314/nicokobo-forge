using HarmonyLib;
using Il2Cpp;
using Nicokobo.Forge.Runtime;
using UnityEngine;

namespace Nicokobo.Forge;

public static partial class ForgeExternalPowerApi
{
    private static readonly HashSet<string> NativeDevices = new(StringComparer.Ordinal);
    private static bool NativeMachine(string id) => id is "furnace" or "water_purifier" or "moisture_farm" or
        "desequencer" or "wine_rack" or "mirage_projector" or "security_alarm" or "bottle_printer" or
        "box_dispenser" or "fridge" or "vacuum_robot";
    private static bool NativeTool(string id) => id is "turbo_booster_adv" or "aug_scanner";

    /// <summary>Read-only admission for the native operations adapted by this
    /// build and machines actually declared through Forge's machine registry.
    /// Module bays and logistics modules are not power requirements.</summary>
    public static bool CanSupplyMachine(GameItem machine) => CanSupplyDevice(machine);

    /// <summary>The real local battery, or null for an empty/unbuilt battery
    /// slot. Querying never substitutes a slot or attaches a supply item.</summary>
    public static GameItem? GetMachinePowerSource(GameItem machine)
    {
        return GetDevicePowerSource(machine);
    }

    private static NativeHook Hook(Type type, string method, Type[] args, string prefix, Type? returns = null) =>
        new(type, method, args, returns ?? typeof(void), typeof(ForgeExternalPowerApi),
            prefix, nameof(AfterNativeBatch), nameof(FinishNativeBatch));

    private static IReadOnlyList<(string Device, NativeHook[] Hooks)> NativeMachineHooks() =>
    [
        ("furnace", [Hook(typeof(MachineFurnace.__c__DisplayClass0_0), "_Furnace_b__5", [typeof(GameItem), typeof(GameInventory), typeof(SlotMarker)], nameof(BeforeFurnace))]),
        ("water_purifier", [Hook(typeof(MachinePurifier.__c__DisplayClass1_0), "_WaterPurifier_b__5", [typeof(GameItem), typeof(GameInventory), typeof(SlotMarker)], nameof(BeforePurifier))]),
        ("moisture_farm", [Hook(typeof(MachineMoistureFarm.__c__DisplayClass0_0), "_MoistureFarm_b__4", [typeof(GameItem), typeof(GameInventory), typeof(SlotMarker)], nameof(BeforeMoisture))]),
        ("desequencer", [
            Hook(typeof(MachineDesequencer.__c__DisplayClass5_0), "_Desequencer_b__5", [typeof(GameItem), typeof(GameInventory), typeof(SlotMarker)], nameof(BeforeDesequencer)),
            Hook(typeof(MachineDesequencer.__c__DisplayClass5_0), "_Desequencer_b__8", [typeof(GameItem), typeof(GameInventory), typeof(SlotMarker)], nameof(BeforeDesequencerDisplay))]),
        ("wine_rack", [Hook(typeof(MachineWineRack.__c__DisplayClass3_0), "_AgeWell_b__5", [typeof(GameItem), typeof(GameInventory), typeof(SlotMarker)], nameof(BeforeWine))]),
        ("bottle_printer", [Hook(typeof(MachineBottlePrinter.__c__DisplayClass6_0), "Method_Internal_Void_String_Int32_0", [typeof(string), typeof(int)], nameof(BeforeBottle))]),
        ("box_dispenser", [Hook(typeof(MachineBoxDispenser.__c__DisplayClass0_0), "_BoxDispenser_b__1", [], nameof(BeforeBox))]),
        ("fridge", [
            Hook(typeof(MachineFridge.__c__DisplayClass26_0), "_Fridge_b__4", [typeof(GameItem), typeof(GameInventory), typeof(SlotMarker)], nameof(BeforeFridge)),
            Hook(typeof(MachineFridge.__c__DisplayClass26_0), "_Fridge_b__5", [typeof(GameItem), typeof(GameInventory), typeof(SlotMarker)], nameof(BeforeFridgeCooling))]),
        ("mirage_projector", [Hook(typeof(MachineProjector.__c__DisplayClass1_0), "_Projector_b__5", [typeof(GameItem), typeof(GameInventory), typeof(SlotMarker)], nameof(BeforeProjector))]),
        ("security_alarm", [
            Hook(typeof(MachineAlarm.__c__DisplayClass1_0), "_Alarme_b__3", [typeof(GameItem), typeof(GameInventory), typeof(SlotMarker)], nameof(BeforeAlarm)),
            Hook(typeof(MachineAlarm.__c__DisplayClass1_0), "_Alarme_b__5", [typeof(GameItem), typeof(GameInventory), typeof(SlotMarker)], nameof(BeforeAlarmDisplay))]),
        // Build 25382790 wrapper names, mapped through metadata tokens
        // 0x060004DD/DE to native TryCleanup/TryIncinerate (RVA 71EB50/71ED20).
        ("vacuum_robot", [Hook(typeof(HusbandryDirectory.__c__DisplayClass1_0), "Method_Internal_Boolean_GameInventory_PDM_0", [typeof(GameInventory)], nameof(BeforeVacuumCleanup), typeof(bool)),
        Hook(typeof(HusbandryDirectory.__c__DisplayClass1_0), "Method_Internal_Void_PDM_0", [], nameof(BeforeVacuumIncinerate)),
        Hook(typeof(HusbandryDirectory.__c__DisplayClass1_0), "_VacuumRobot_b__18", [typeof(GameItem), typeof(GameInventory), typeof(SlotMarker)], nameof(BeforeVacuumManual)),
        Hook(typeof(HusbandryDirectory.__c__DisplayClass1_0), "_VacuumRobot_b__16", [typeof(GameItem), typeof(GameInventory), typeof(SlotMarker)], nameof(BeforeVacuumDisplay), typeof(bool)),
        Hook(typeof(HusbandryDirectory.__c__DisplayClass1_0), "_VacuumRobot_b__17", [typeof(GameItem), typeof(GameInventory), typeof(SlotMarker)], nameof(BeforeVacuumDisplay), typeof(bool)),
        new(typeof(HusbandryDirectory.__c__DisplayClass1_0), "_VacuumRobot_b__12", [typeof(GameItem), typeof(GameInventory), typeof(SlotMarker)], typeof(void), typeof(ForgeExternalPowerApi), nameof(DispatchVacuumNight))]),
        ("turbo_booster_adv", [Hook(typeof(ToolDirectory.__c__DisplayClass25_0), "_CreateTurboBoosterAdv_b__4", [typeof(GameItem), typeof(GameInventory), typeof(SlotMarker)], nameof(BeforeBooster))]),
        ("aug_scanner", [Hook(typeof(MiscItemDirectory.__c__DisplayClass75_0), "_AugScanner_b__2", [typeof(GameItem), typeof(GameInventory), typeof(SlotMarker)], nameof(BeforeAugScanner)),
        Hook(typeof(MiscItemDirectory.__c__DisplayClass75_0), "_AugScanner_b__0", [typeof(GameItem), typeof(GameInventory), typeof(SlotMarker)], nameof(BeforeAugScannerDisplay), typeof(bool)),
        Hook(typeof(MiscItemDirectory.__c__DisplayClass75_0), "_AugScanner_b__1", [typeof(GameItem), typeof(GameInventory), typeof(SlotMarker)], nameof(BeforeAugScannerDisplay), typeof(bool))])
    ];

    [HarmonyPriority(int.MinValue)]
    private static bool BeforeFurnace(MachineFurnace.__c__DisplayClass0_0 __instance, out NativeBatch? __state) =>
        BeginNative(__instance.furnace, () => __instance.itemInventoryBattery, value => __instance.itemInventoryBattery = value, [], false, out __state);
    [HarmonyPriority(int.MinValue)]
    private static bool BeforePurifier(MachinePurifier.__c__DisplayClass1_0 __instance, out NativeBatch? __state) =>
        BeginNative(__instance.machine, () => __instance.powerInv, value => __instance.powerInv = value, [], false, out __state);
    [HarmonyPriority(int.MinValue)]
    private static bool BeforeMoisture(MachineMoistureFarm.__c__DisplayClass0_0 __instance, out NativeBatch? __state) =>
        BeginNative(__instance.machine, () => __instance.powerInv, value => __instance.powerInv = value, [], false, out __state);
    [HarmonyPriority(int.MinValue)]
    private static bool BeforeDesequencer(MachineDesequencer.__c__DisplayClass5_0 __instance, out NativeBatch? __state) =>
        BeginNative(__instance.machine, () => __instance.powerInv, value => __instance.powerInv = value, [], false, out __state);
    [HarmonyPriority(int.MinValue)]
    private static bool BeforeWine(MachineWineRack.__c__DisplayClass3_0 __instance, out NativeBatch? __state) =>
        BeginNative(__instance.machine, () => __instance.itemInventoryBattery, value => __instance.itemInventoryBattery = value, [], false, out __state);
    [HarmonyPriority(int.MinValue)]
    private static bool BeforeBottle(MachineBottlePrinter.__c__DisplayClass6_0 __instance, out NativeBatch? __state) =>
        BeginNative(__instance.machine, () => __instance.itemInventory, value => __instance.itemInventory = value, [], false, out __state);
    [HarmonyPriority(int.MinValue)]
    private static bool BeforeBox(MachineBoxDispenser.__c__DisplayClass0_0 __instance, out NativeBatch? __state) =>
        BeginNative(__instance.itemInventory.GetParentItem(), () => __instance.itemInventory, value => __instance.itemInventory = value, [], false, out __state);
    [HarmonyPriority(int.MinValue)]
    private static bool BeforeFridge(MachineFridge.__c__DisplayClass26_0 __instance, out NativeBatch? __state) =>
        BeginNative(__instance.machine, () => __instance.batterySlot, value => __instance.batterySlot = value, [], false, out __state);
    [HarmonyPriority(int.MinValue)]
    private static bool BeforeFridgeCooling(MachineFridge.__c__DisplayClass26_0 __instance, out NativeBatch? __state) =>
        BeginNative(__instance.machine, () => __instance.batterySlot, value => __instance.batterySlot = value, [], true, out __state);
    [HarmonyPriority(int.MinValue)]
    private static bool BeforeProjector(MachineProjector.__c__DisplayClass1_0 __instance, out NativeBatch? __state) =>
        BeginNative(__instance.machine, () => __instance.itemInventory, value => __instance.itemInventory = value, [], false, out __state);
    [HarmonyPriority(int.MinValue)]
    private static bool BeforeAlarm(MachineAlarm.__c__DisplayClass1_0 __instance, out NativeBatch? __state) =>
        BeginNative(__instance.machine, () => __instance.itemInventory, value => __instance.itemInventory = value, [], false, out __state);
    [HarmonyPriority(int.MinValue)]
    private static bool BeforeAlarmDisplay(MachineAlarm.__c__DisplayClass1_0 __instance, out NativeBatch? __state) =>
        BeginNative(__instance.machine, () => __instance.itemInventory, value => __instance.itemInventory = value, [], true, out __state);
    [HarmonyPriority(int.MinValue)]
    private static bool BeforeDesequencerDisplay(MachineDesequencer.__c__DisplayClass5_0 __instance, out NativeBatch? __state) =>
        BeginNative(__instance.machine, () => __instance.powerInv, value => __instance.powerInv = value, [], true, out __state);
    [HarmonyPriority(int.MinValue)]
    private static bool BeforeVacuumCleanup(HusbandryDirectory.__c__DisplayClass1_0 __instance, GameInventory __0, out NativeBatch? __state) =>
        BeginNative(__instance.item, () => __instance.batterySlot, value => __instance.batterySlot = value, [__0], false, out __state);
    [HarmonyPriority(int.MinValue)]
    private static bool BeforeVacuumIncinerate(HusbandryDirectory.__c__DisplayClass1_0 __instance, out NativeBatch? __state) =>
        BeginNative(__instance.item, () => __instance.batterySlot, value => __instance.batterySlot = value, [], false, out __state);
    [HarmonyPriority(int.MinValue)]
    private static bool BeforeVacuumManual(HusbandryDirectory.__c__DisplayClass1_0 __instance, GameInventory __1, out NativeBatch? __state) =>
        BeginNative(__instance.item, () => __instance.batterySlot, value => __instance.batterySlot = value, [__1], false, out __state);
    [HarmonyPriority(int.MinValue)]
    private static bool BeforeVacuumDisplay(HusbandryDirectory.__c__DisplayClass1_0 __instance, out NativeBatch? __state) =>
        BeginNative(__instance.item, () => __instance.batterySlot, value => __instance.batterySlot = value, [], true, out __state);
    [HarmonyPriority(int.MinValue)]
    private static bool BeforeBooster(ToolDirectory.__c__DisplayClass25_0 __instance, out NativeBatch? __state) =>
        BeginNative(__instance.item, () => __instance.batterySlot, value => __instance.batterySlot = value, [], false, out __state);
    [HarmonyPriority(int.MinValue)]
    private static bool BeforeAugScannerDisplay(MiscItemDirectory.__c__DisplayClass75_0 __instance, out NativeBatch? __state) =>
        BeginNative(__instance.batterySlot.GetParentItem(), () => __instance.batterySlot, value => __instance.batterySlot = value, [], true, out __state);
    [HarmonyPriority(int.MinValue)]
    private static bool BeforeAugScanner(MiscItemDirectory.__c__DisplayClass75_0 __instance, out NativeBatch? __state)
    {
        if (!BeginNative(__instance.batterySlot.GetParentItem(), () => __instance.batterySlot,
            value => __instance.batterySlot = value, [], false, out __state)) return false;
        if (__state == null) return true;
        var batch = __state;
        var store = PlayerStore.instance;
        var visit = store?.currentClientInstance;
        var client = visit?.storeClient;
        if (client == null) return false;
        bool before = client.isAugScanned;
        bool SameClient() => PlayerStore.instance?.Pointer == store!.Pointer &&
            store.currentClientInstance?.Pointer == visit!.Pointer && visit.storeClient?.Pointer == client.Pointer;
        batch.RestoreEffects = () =>
        {
            if (!SameClient()) return false;
            client.isAugScanned = before; return client.isAugScanned == before;
        };
        batch.VerifyEffects = () => SameClient() && (!batch.Power.WasDrawn || client.isAugScanned);
        // The native scanner ignores Draw's result before writing the customer
        // and playing its sound. Reject an unavailable/unverified budget before
        // entering that body; its real Draw confirms this same reservation.
        return (batch.Direct ? TryGetMachineEnergy(batch.Machine, out int energy) && energy >= 25 :
            batch.Connector != null && Connected(batch.Connector) && GetEnergy(batch.Connector) >= 25) && batch.Power.Prepare(25);
    }

    [ThreadStatic] private static ToolPowerQuery? _toolPowerQuery;
    private sealed record ToolPowerQuery(GameItem Device, GameItem Source, ToolPowerQuery? Previous);
    [HarmonyPriority(int.MinValue)]
    private static void BeforeAugScannerTooltip(GameItem __1, out ToolPowerQuery? __state)
    {
        BeginToolQuery(__1, GetDevicePowerSource(__1), out __state);
    }
    [HarmonyPriority(int.MinValue)]
    private static void BeforeBoosterSprite(GameItem __0, GameSlotInventory __1, out ToolPowerQuery? __state) =>
        BeginToolQuery(__0, __1.childItem, out __state);
    private static void BeginToolQuery(GameItem device, GameItem? source, out ToolPowerQuery? state)
    {
        state = null;
        if (source == null || !CanSupplyDevice(device) || !TryGetMachineEnergy(device, out _)) return;
        state = new(device, source, _toolPowerQuery); _toolPowerQuery = state;
    }
    private static void AfterToolPowerQuery(ToolPowerQuery? __state)
    { if (__state != null && ReferenceEquals(_toolPowerQuery, __state)) _toolPowerQuery = __state.Previous; }
    private static Exception? FinishToolPowerQuery(Exception? __exception, ToolPowerQuery? __state)
    { AfterToolPowerQuery(__state); return __exception; }
    [HarmonyPriority(int.MaxValue)]
    private static bool CanDrawToolQuery(GameItem __0, int __1, ref bool __result)
    {
        if (_executingReceipt != 0 || _toolPowerQuery is not { } query || __0?.Pointer != query.Source.Pointer) return true;
        // Keep the physical source visible to content resolvers while the
        // tooltip's own CanDraw reads the corresponding device supply.
        _executingReceipt++;
        try { __result = __1 >= 0 && TryGetMachineEnergy(query.Device, out int energy) && energy >= __1; return false; }
        finally { _executingReceipt--; }
    }
    [HarmonyPriority(int.MinValue)]
    private static bool DispatchVacuumNight(HusbandryDirectory.__c__DisplayClass1_0 __instance, GameInventory __1)
    {
        var machine = __instance.item;
        if (!TryGetMachineEnergy(machine, out _) && !IsConnector(__instance.batterySlot?.childItem)) return true;
        // The night callback inlines these two independent native operations.
        // Reuse their real bodies so each receipt is released before the next
        // operation tests the remaining power; never recreate cleanup rules.
        __instance.Method_Internal_Boolean_GameInventory_PDM_0(__1);
        __instance.Method_Internal_Void_PDM_0();
        return false;
    }

    private sealed class ReceiptGuard(IForgePowerTransaction receipt) : IForgePowerTransaction
    {
        private static T Run<T>(Func<T> call)
        { _executingReceipt++; try { return call(); } finally { _executingReceipt--; } }
        public bool Validate() => Run(receipt.Validate);
        public void Debit() => Run(() => { receipt.Debit(); return true; });
        public bool Verify() => Run(receipt.Verify);
        public bool Restore() => Run(receipt.Restore);
        public void Fault(string reason) => Run(() => { receipt.Fault(reason); return true; });
        public void Dispose() => Run(() => { receipt.Dispose(); return true; });
        internal static IForgePowerTransaction? Plan(Func<IForgePowerTransaction?> call) =>
            Run(call) is { } receipt ? new ReceiptGuard(receipt) : null;
    }

    private sealed record ItemState(GameItem Item, GameInventory Parent, int Units, int Id,
        string Identifier, long Value, GridShape Shape, TagSystem State, ItemFeature[] Features,
        string SpriteAtlas, string SpritePath, bool SpriteChanged, Action RestoreCallbacks, int Index,
        IntPtr Handler, Vector3? LocalPosition);
    private sealed record InventoryState(GameInventory Inventory, IReadOnlySet<IntPtr> Items, Action RestorePolicy);
    private sealed class NativeBatch(GameItem machine, GameItem? connector, bool direct, bool readinessOnly)
    {
        internal readonly GameItem Machine = machine;
        internal readonly GameItem? Connector = connector;
        internal readonly bool Direct = direct, ReadinessOnly = readinessOnly;
        internal readonly List<ItemState> Items = [];
        internal readonly List<InventoryState> Inventories = [];
        internal readonly List<GameItem> Deferred = [];
        internal readonly NativePowerAction Power = new(cost => ReceiptGuard.Plan(() => direct
            ? PlanMachine(machine, cost) : Plan(machine, connector!, cost)));
        internal Func<bool> RestoreEffects = () => true;
        internal Func<bool> VerifyEffects = () => true;
        internal bool Failed, Finished;
        internal NativeMachinePowerSlot? PowerSlot;
    }

    private sealed class NativeMachinePowerSlot : IDisposable
    {
        private readonly Func<GameSlotInventory> _read;
        private readonly Action<GameSlotInventory> _write;
        private readonly GameSlotInventory _original, _temporary;
        private bool _disposed;
        internal NativeMachinePowerSlot(Func<GameSlotInventory> read, Action<GameSlotInventory> write, GameItem machine)
        {
            _read = read; _write = write; _original = read();
            _temporary = new GameSlotInventory { currentItem = machine };
            write(_temporary);
        }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            bool unchanged = _read()?.Pointer == _temporary.Pointer;
            if (unchanged) _write(_original);
            _temporary.currentItem = null;
            if (!unchanged) throw new InvalidOperationException("Native power slot changed during the operation");
        }
    }

    private static bool BeginNative(GameItem machine, Func<GameSlotInventory> readSlot,
        Action<GameSlotInventory> writeSlot, IReadOnlyList<GameInventory?> extra, bool readinessOnly, out NativeBatch? state)
    {
        state = null;
        if (machine == null || machine.Pointer == IntPtr.Zero) return true;
        var connector = readSlot()?.childItem;
        bool direct = TryGetMachineEnergy(machine, out _);
        if (!direct && !IsConnector(connector)) return true;
        if (_native != null || machine.GetTagReadonly("NICOKOBO_FORGE_MACHINE_FAULT")?.valueString is { Length: > 0 }) return false;
        try
        {
            if (!CanSupplyMachine(machine) || !GeneralHelper.IsItemOwned(machine)) return false;
            var batch = new NativeBatch(machine, connector, direct, readinessOnly);
            CaptureNative(batch, extra);
            state = batch; _native = batch;
            if (direct) batch.PowerSlot = new(readSlot, writeSlot, machine);
            return true;
        }
        catch (Exception ex)
        {
            try { state?.PowerSlot?.Dispose(); } catch { }
            state?.Power.Dispose(); _native = null; state = null;
            _log?.Invoke("[WARN] [NicokoboForge/Power] native operation preflight rejected: " + ex.Message); return false;
        }
    }

    private static TagSystem CloneNativeState(TagSystem state)
    {
        var snapshot = new TagSystem();
        foreach (var pair in state.dict) snapshot.dict.Add(pair.Key, pair.Value.Clone());
        return snapshot;
    }

    private static ItemState CaptureSnapshot(GameItem item, GameInventory parent)
    {
        parent = parent.Cast<GameInventory>();
        var slot = item.GetIncompleteSlotInInventory();
        if (slot?.item?.Pointer != item.Pointer || slot.inventory?.Pointer != parent.Pointer || slot.itemGridShape == null || slot.index < 0)
            throw new InvalidOperationException("Native operation item has no original inventory slot");
        var handler = item.TryCast<GameItemElement>()?.handler;
        var remove = item.mayInventoryRemoveItemFunc;
        var features = new List<ItemFeature>();
        if (item.itemFeatures != null) foreach (var feature in item.itemFeatures) features.Add(feature);
        return new(item, parent, item.unitCount, item.GetUniqueID(), item.identifier, item.unitValue,
            slot.itemGridShape.Clone(), CloneNativeState(item.state), features.ToArray(),
            item.spriteAtlasPath, item.spritePath, item.spriteChanged, () => item.mayInventoryRemoveItemFunc = remove,
            slot.index, handler?.Pointer ?? IntPtr.Zero, handler?.transform.localPosition);
    }

    private static void CaptureNative(NativeBatch batch, IReadOnlyList<GameInventory?> extra)
    {
        var seen = new HashSet<IntPtr>();
        void Element(PixelElement? element)
        {
            if (element == null) return;
            if (element.TryCast<GameInventory>() is { } inventory) { Inventory(inventory); return; }
            if (!seen.Add(element.Pointer)) return;
            if (element.TryCast<GridPixelElement>() is { } grid)
                for (int y = 0; y < grid.gridHeight; y++)
                    for (int x = 0; x < grid.gridWidth; x++) Element(grid.GetElement(x, y));
        }
        void Inventory(GameInventory? inventory)
        {
            if (inventory == null || !seen.Add(inventory.Pointer)) return;
            inventory = inventory.Cast<GameInventory>();
            bool insert = inventory.overrideLockInsert, remove = inventory.overrideLockRemove;
            var admission = inventory.mayInventoryAddItemFunc;
            var onAdd = inventory.onSlotAddItemFunc; var onEnd = inventory.onSlotAddItemEndFunc; var onLate = inventory.onSlotAddItemLateFunc;
            batch.Inventories.Add(new(inventory, inventory.childItems.ToArray().Select(item => item.Pointer).ToHashSet(), () =>
            {
                inventory.overrideLockInsert = insert; inventory.overrideLockRemove = remove;
                inventory.mayInventoryAddItemFunc = admission; inventory.onSlotAddItemFunc = onAdd;
                inventory.onSlotAddItemEndFunc = onEnd; inventory.onSlotAddItemLateFunc = onLate;
            }));
            foreach (var item in inventory.childItems.ToArray()) Item(item);
        }
        void Item(GameItem item)
        {
            if (!seen.Add(item.Pointer)) return;
            if (batch.Items.Count >= ForgeNumbers.Inventory.MaxRunItems) throw new InvalidOperationException("Native operation inventory exceeds limit");
            batch.Items.Add(CaptureSnapshot(item, item.parentInventory ?? throw new InvalidOperationException("Native item has no parent")));
            if (item.children != null)
                foreach (var child in item.children)
                {
                    if (child.TryCast<GameInventory>() is { } inventory) Inventory(inventory);
                    else if (child.TryCast<PixelWindow>() is { } window) Element(window.childElement);
                }
        }
        Item(batch.Machine);
        foreach (var inventory in extra) Inventory(inventory);
        var store = PlayerStore.instance;
        if (batch.Machine.identifier == "mirage_projector" && store != null)
        {
            int before = store.projectorAttractivenessBonus;
            batch.RestoreEffects = () => { if (PlayerStore.instance?.Pointer != store.Pointer) return false; store.projectorAttractivenessBonus = before; return store.projectorAttractivenessBonus == before; };
        }
        else if (batch.Machine.identifier == "security_alarm" && store != null)
        {
            bool before = store.isSecurityAlarmActive;
            batch.RestoreEffects = () => { if (PlayerStore.instance?.Pointer != store.Pointer) return false; store.isSecurityAlarmActive = before; return store.isSecurityAlarmActive == before; };
        }
    }

    private static bool DeferConsumedDestroy(GameItem __instance)
    {
        var batch = _native;
        if (batch == null || batch.Finished) return true;
        var snapshot = batch.Items.FirstOrDefault(item => item.Item.Pointer == __instance.Pointer);
        if (snapshot == null) return true;
        if (!batch.Deferred.Any(item => item.Pointer == __instance.Pointer)) batch.Deferred.Add(__instance);
        NativeBatchFinalization.DetachForDeferredDestruction(
            () => __instance.GetUniqueID() == snapshot.Id && __instance.identifier == snapshot.Identifier &&
                (__instance.parentInventory == null || batch.Inventories.Any(inv => inv.Inventory.Pointer == __instance.parentInventory.Pointer)),
            () => __instance.parentInventory == null,
            () => __instance.parentInventory!.Cast<GameInventory>().Expel(__instance),
            reason => { batch.Failed = true; _log?.Invoke("[WARN] [NicokoboForge/Power] " + reason); });
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
        try { batch.PowerSlot?.Dispose(); }
        catch (Exception ex) { failed = true; FaultBatch(batch, "native power slot restore failed: " + ex.Message); }
        var restore = new List<Func<bool>> { () => RecallNativeWrites(batch) };
        restore.AddRange(batch.Items.Select(snapshot => (Func<bool>)(() => RestoreSnapshot(snapshot))));
        restore.Add(() => RestoreNativeOrder(batch));
        restore.Add(() => { foreach (var inventory in batch.Inventories) inventory.RestorePolicy(); return true; });
        restore.Add(batch.RestoreEffects);
        restore.Add(batch.Power.Restore);
        bool committed = NativeBatchFinalization.Complete(() => !failed && !batch.Failed && batch.Power.Verify() && batch.VerifyEffects() &&
            batch.Items.All(item => item.Item.GetUniqueID() == item.Id && item.Item.identifier == item.Identifier),
            restore, reason => FaultBatch(batch, reason), () =>
            {
                batch.Finished = true; if (ReferenceEquals(_native, batch)) _native = null;
                batch.Power.Dispose();
            });
        if (committed)
            foreach (var item in batch.Deferred)
                try { if (item.parentInventory == null) item.Destroy(); }
                catch (Exception ex) { FaultBatch(batch, "committed input cleanup failed: " + ex.Message); }
    }

    private static bool RecallNativeWrites(NativeBatch batch)
    {
        bool restored = true;
        var originals = batch.Items.Select(item => item.Item.Pointer).ToHashSet();
        foreach (var inventory in batch.Inventories)
            foreach (var item in inventory.Inventory.childItems.ToArray().Where(item => !originals.Contains(item.Pointer)))
                try { if (!DetachNative(item)) restored = false; else item.Destroy(); } catch { restored = false; }
        foreach (var item in batch.Items)
            if (item.Item.parentInventory is { } parent && parent.Pointer != item.Parent.Pointer)
                try
                {
                    if (!batch.Inventories.Any(inv => inv.Inventory.Pointer == parent.Pointer) || !DetachNative(item.Item)) restored = false;
                }
                catch { restored = false; }
        return restored;
    }

    private static bool DetachNative(GameItem item)
    {
        var parent = item.parentInventory?.Cast<GameInventory>();
        if (parent == null) return true;
        bool locked = parent.overrideLockRemove;
        parent.overrideLockRemove = false;
        try { return parent.Expel(item) && item.parentInventory == null; }
        finally { parent.overrideLockRemove = locked; }
    }

    private static bool SameTags(TagSystem current, TagSystem expected)
    {
        if (current.dict.Count != expected.dict.Count) return false;
        foreach (var pair in expected.dict)
            if (!current.dict.TryGetValue(pair.Key, out var tag) || tag.valueEnabled != pair.Value.valueEnabled ||
                tag.valueString != pair.Value.valueString || tag.valueInt != pair.Value.valueInt || tag.valueLong != pair.Value.valueLong ||
                tag.valueBool != pair.Value.valueBool || !tag.valueFloat.Equals(pair.Value.valueFloat) || !tag.valueDouble.Equals(pair.Value.valueDouble)) return false;
        return true;
    }

    private static bool RestoreSnapshot(ItemState snapshot)
    {
        var item = snapshot.Item;
        if (item.GetUniqueID() != snapshot.Id || item.identifier != snapshot.Identifier ||
            item.parentInventory != null && item.parentInventory.Pointer != snapshot.Parent.Pointer) return false;
        var handler = item.TryCast<GameItemElement>()?.handler;
        if (snapshot.Handler != IntPtr.Zero && handler?.Pointer != snapshot.Handler) return false;
        snapshot.RestoreCallbacks();
        item.itemFeatures.Clear(); foreach (var feature in snapshot.Features) item.itemFeatures.Add(feature);
        item.state = CloneNativeState(snapshot.State); item.SyncModifiedState(); item.SetUnitCount(snapshot.Units); item.unitValue = snapshot.Value;
        item.SetSpriteAndShape(snapshot.SpriteAtlas, snapshot.SpritePath); item.spriteChanged = snapshot.SpriteChanged;
        item.modifiedShape = snapshot.Shape.Clone();
        if (snapshot.LocalPosition is { } before) handler!.transform.localPosition = before;
        if (item.parentInventory == null)
        {
            var parent = snapshot.Parent;
            var admission = parent.mayInventoryAddItemFunc; var onAdd = parent.onSlotAddItemFunc;
            var onEnd = parent.onSlotAddItemEndFunc; var onLate = parent.onSlotAddItemLateFunc; var onItemAdd = item.onSlotAddItemFunc;
            bool locked = parent.overrideLockInsert;
            try
            {
                parent.mayInventoryAddItemFunc = null;
                parent.onSlotAddItemFunc = parent.onSlotAddItemEndFunc = parent.onSlotAddItemLateFunc = null;
                item.onSlotAddItemFunc = null; parent.overrideLockInsert = false;
                var slot = parent.TryInventorySlot(item, snapshot.Units, snapshot.Shape);
                if (slot == null || !slot.IsValid() || slot.targetItem != null || slot.item?.Pointer != item.Pointer ||
                    slot.inventory?.Pointer != parent.Pointer || slot.numTransfer < snapshot.Units || !parent.UncheckedAccept(item)) return false;
            }
            finally
            {
                parent.mayInventoryAddItemFunc = admission; parent.onSlotAddItemFunc = onAdd;
                parent.onSlotAddItemEndFunc = onEnd; parent.onSlotAddItemLateFunc = onLate;
                item.onSlotAddItemFunc = onItemAdd; parent.overrideLockInsert = locked;
            }
        }
        if (item.parentInventory?.Pointer != snapshot.Parent.Pointer) return false;
        handler = item.TryCast<GameItemElement>()?.handler;
        if (snapshot.Handler != IntPtr.Zero && handler?.Pointer != snapshot.Handler) return false;
        item.modifiedShape = snapshot.Shape.Clone();
        if (snapshot.LocalPosition is { } position) handler!.transform.localPosition = position;
        var actual = item.GetIncompleteSlotInInventory(); var shape = actual?.itemGridShape;
        bool sameShape = shape != null && shape.minX == snapshot.Shape.minX && shape.minY == snapshot.Shape.minY &&
            shape.width == snapshot.Shape.width && shape.height == snapshot.Shape.height && shape.globalWidth == snapshot.Shape.globalWidth &&
            shape.globalHeight == snapshot.Shape.globalHeight && shape.flipped == snapshot.Shape.flipped && shape.orientation == snapshot.Shape.orientation;
        return item.unitCount == snapshot.Units && item.unitValue == snapshot.Value && SameTags(item.state, snapshot.State) &&
            item.itemFeatures.ToArray().Select(feature => feature.Pointer).SequenceEqual(snapshot.Features.Select(feature => feature.Pointer)) &&
            item.spriteAtlasPath == snapshot.SpriteAtlas && item.spritePath == snapshot.SpritePath && sameShape &&
            (snapshot.LocalPosition == null || handler != null && handler.transform.localPosition.Equals(snapshot.LocalPosition.Value));
    }

    private static bool RestoreNativeOrder(NativeBatch batch)
    {
        // Reattach every original before restoring list indices. The robot may
        // originally follow a moved item in its own parent inventory.
        foreach (var snapshot in batch.Items.OrderBy(item => item.Parent.Pointer.ToInt64()).ThenBy(item => item.Index))
        {
            var children = snapshot.Parent.childItems;
            int current = children.IndexOf(snapshot.Item);
            if (current < 0 || snapshot.Index >= children.Count) return false;
            if (current != snapshot.Index) { children.RemoveAt(current); children.Insert(snapshot.Index, snapshot.Item); }
        }
        return batch.Items.All(item => item.Item.GetIncompleteSlotInInventory()?.index == item.Index);
    }

    private static void FaultBatch(NativeBatch batch, string reason)
    {
        try { batch.Power.Fault(reason); } catch { }
        try
        {
            var tag = batch.Machine.state.GetTag("NICOKOBO_FORGE_MACHINE_FAULT");
            tag.Enable(); tag.SetString(reason); batch.Machine.SyncModifiedState();
        }
        catch { }
        try { _log?.Invoke("[ERROR] [NicokoboForge/Power] native machine and external supply quarantined: " + reason); } catch { }
    }
}
