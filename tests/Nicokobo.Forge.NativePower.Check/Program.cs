using Il2Cpp;
using Nicokobo.Forge;

int checks = 0;
void Check(bool value, string reason) { checks++; if (!value) throw new Exception(reason); }
Nicokobo.Forge.Runtime.NativeHookSet.FailTarget = nameof(AugScanner.CreateTooltip);
ForgeExternalPowerApi.Install(true, _ => { });
Check(ForgeExternalPowerApi.IsAvailable && ForgeExternalPowerApi.CanSupplyDevice(new GameItemElement { identifier = "aug_scanner" }),
    "A missing scanner tooltip must not disable device power or the shared external supply");
Nicokobo.Forge.Runtime.NativeHookSet.FailTarget = "_WaterPurifier_b__5";
ForgeExternalPowerApi.Install(true, _ => { });
Check(ForgeExternalPowerApi.IsAvailable && ForgeExternalPowerApi.CanSupplyDevice(new GameItemElement { identifier = "furnace" }) &&
    !ForgeExternalPowerApi.CanSupplyDevice(new GameItemElement { identifier = "water_purifier" }),
    "A failed purifier action must disable only that device's supply admission");
using (var rejectedDeviceProvider = ForgeExternalPowerApi.RegisterMachine("checks", "unsupported", _ => 500, (_, _) => null))
    Check(!ForgeExternalPowerApi.TryGetMachineEnergy(new GameItemElement { identifier = "water_purifier" }, out _),
        "An unpatched native device must not observe external energy even when a provider claims it");
Nicokobo.Forge.Runtime.NativeHookSet.FailTarget = nameof(PowerHelper.DrawPowerSource);
ForgeExternalPowerApi.Install(true, _ => { });
Check(!ForgeExternalPowerApi.IsAvailable, "A missing shared debit hook must still close the external supply gate");
Nicokobo.Forge.Runtime.NativeHookSet.FailTarget = null;
ForgeExternalPowerApi.Install(true, _ => { });
PlayerStore.instance = new();
var supply = new Supply();
using var provider = ForgeExternalPowerApi.RegisterMachine("checks", "native", supply.Energy, supply.Plan);
var names = new[] { "furnace", "water_purifier", "moisture_farm", "desequencer", "wine_rack", "bottle_printer", "box_dispenser", "fridge", "mirage_projector", "security_alarm" };
foreach (string name in names)
{
    var world = new World(name); supply.Reset(world);
    var originalSlot = world.ReadSlot(); var originalBattery = world.Battery.childItem;
    Callback.Native(world.Prefix, world.Context, () =>
    {
        Check(world.ReadSlot().Pointer != originalSlot.Pointer && world.ReadSlot().childItem?.Pointer == world.Machine.Pointer,
            name + " should substitute only the captured slot during the native operation");
        if (PowerHelper.CanDrawPowerSource(world.ReadSlot().childItem!, 4))
        {
            PowerHelper.DrawPowerSource(world.ReadSlot().childItem!, 4);
            world.Payload.state.GetTag("progress").valueInt = 1;
        }
    });
    Check(supply.Source.Energy == 16 && supply.Debits == 1 && supply.Disposals == 1 && !supply.Busy,
        name + " must debit once and release its real receipt");
    Check(world.Payload.state.GetTag("progress").valueInt == 1 && world.ReadSlot().Pointer == originalSlot.Pointer &&
        world.Battery.childItem?.Pointer == originalBattery?.Pointer && world.Machine.parentInventory == world.Parent,
        name + " must commit its in-place work and restore the original captured slot");
    Check(ForgeExternalPowerApi.CanSupplyMachine(world.Machine) && ForgeExternalPowerApi.GetMachinePowerSource(world.Machine)?.Pointer == originalBattery?.Pointer,
        name + " must be naturally eligible without a module bay or logistics modules");
}

// Full graph compensation: the machine owns a PixelWindow containing a grid,
// not direct inventory children. Include nested containers and module state.
{
    var world = new World("desequencer"); supply.Reset(world);
    world.Machine.state.GetTag("night-progress").valueInt = 60;
    var feature = new ItemFeature(); world.Payload.itemFeatures.Add(feature);
    var nested = new GameItemElement { identifier = "drink", unitValue = 33 };
    var bottleInventory = new GameGridInventory(); var bottleLayout = new GridPixelElement(1, 1);
    bottleLayout.Put(0, 0, bottleInventory); world.Payload.children.Add(new PixelWindow(bottleLayout)); bottleInventory.UncheckedAccept(nested);
    var originalRemove = new Func<bool>(() => true); world.Payload.mayInventoryRemoveItemFunc = originalRemove;
    var originalPosition = world.Payload.handler.transform.localPosition;
    int payloadId = world.Payload.Id;
    var product = new GameItemElement { identifier = "new-card" };
    bool sawThrow = false;
    try
    {
        Callback.Native(world.Prefix, world.Context, () =>
        {
            Check(PowerHelper.CanDrawPowerSource(world.ReadSlot().childItem!, 4), "Desq should reserve validated power before native writes");
            PowerHelper.DrawPowerSource(world.ReadSlot().childItem!, 4);
            world.Machine.state.GetTag("night-progress").valueInt = 99;
            world.Payload.state.GetTag("LOCKED_TAG").Enable(); world.Payload.mayInventoryRemoveItemFunc = () => false;
            world.Payload.itemFeatures.Clear(); world.Payload.itemFeatures.Add(new()); world.Payload.unitValue = 80;
            world.Payload.SetSpriteAndShape("new-atlas", "new-sprite"); world.Payload.handler.transform.localPosition = new(2, 4, 8);
            nested.state.GetTag("CONTAINER_TEMP_TAG").valueDouble = -18; nested.itemFeatures.Add(new());
            world.Output.overrideLockInsert = true; world.Output.overrideLockRemove = true;
            world.Output.UncheckedAccept(product);
            world.Payload.Destroy();
            Check(!world.Payload.Destroyed && world.Payload.parentInventory == null && world.Payload.Id == payloadId,
                "Whole-input destruction must be deferred with the original UID alive");
            throw new InvalidOperationException("injected native exception");
        });
    }
    catch (InvalidOperationException) { sawThrow = true; }
    Check(sawThrow && supply.Source.Energy == 20 && !supply.Busy, "Native exceptions must refund power and release the scope");
    Check(world.Payload.parentInventory == world.Input && world.Payload.Id == payloadId && !world.Payload.Destroyed && world.Payload.unitValue == 10,
        "Rollback must restore the same original input instance and value");
    Check(world.Payload.itemFeatures.SequenceEqual(new[] { feature }) && world.Payload.mayInventoryRemoveItemFunc == originalRemove &&
        !world.Payload.state.dict.ContainsKey("LOCKED_TAG") && world.Payload.spritePath == "original-sprite" &&
        world.Payload.handler.transform.localPosition == originalPosition,
        "Rollback must restore independent features, removal delegate, tags, sprite, and position");
    Check(!nested.state.dict.ContainsKey("CONTAINER_TEMP_TAG") && nested.itemFeatures.Count == 0 && world.Machine.state.GetTag("night-progress").valueInt == 60,
        "Nested container state and machine progress must be compensated through the real window layout");
    Check(product.Destroyed && world.Output.childItems.Count == 0 && !world.Output.overrideLockInsert && !world.Output.overrideLockRemove,
        "Rollback must recall new output and restore inventory lock policy");
    Check(!world.Machine.state.dict.ContainsKey("NICOKOBO_FORGE_MACHINE_FAULT"), "A certain rollback must not quarantine a healthy machine");
}

foreach (string name in new[] { "water_purifier", "moisture_farm", "desequencer", "wine_rack", "fridge" })
{
    var world = new World(name); supply.Reset(world);
    Callback.Native(world.Prefix, world.Context, () =>
    {
        if (!PowerHelper.CanDrawPowerSource(world.ReadSlot().childItem!, 4)) return;
        Check(PowerHelper.CanDrawPowerSource(world.ReadSlot().childItem!, 4), "Repeated native CanDraw must reuse the reservation");
        world.Payload.state.GetTag("water-parts").valueInt = 250;
        // A full output/native early return after CanDraw never confirms Draw.
    });
    Check(supply.Source.Energy == 20 && supply.Debits == 1 && !world.Payload.state.dict.ContainsKey("water-parts"),
        name + " must refund an unconfirmed reservation and all in-place changes");
    supply.Reset(world); supply.RejectPlan = true;
    Callback.Native(world.Prefix, world.Context, () =>
    {
        // Model the native callbacks that ignore a failed Draw's return value.
        PowerHelper.DrawPowerSource(world.ReadSlot().childItem!, 4);
        world.Payload.state.GetTag("progress").valueInt = 90;
    });
    Check(supply.Source.Energy == 20 && !world.Payload.state.dict.ContainsKey("progress"),
        name + " must compensate output/progress when the native callback ignores Draw failure");
}

foreach (string name in new[] { "mirage_projector", "security_alarm" })
{
    var world = new World(name); supply.Reset(world);
    PlayerStore.instance!.projectorAttractivenessBonus = 7; PlayerStore.instance.isSecurityAlarmActive = false;
    try
    {
        Callback.Native(world.Prefix, world.Context, () =>
        {
            PowerHelper.DrawPowerSource(world.ReadSlot().childItem!, 4);
            PlayerStore.instance.projectorAttractivenessBonus = 100; PlayerStore.instance.isSecurityAlarmActive = true;
            world.Machine.SetSpriteAndShape("Items/items_tool", "alarm_system_r");
            throw new InvalidOperationException("utility failure");
        });
    }
    catch (InvalidOperationException) { }
    Check(supply.Source.Energy == 20 && world.Machine.spritePath == "original-sprite", "Utility failures must refund and restore sprite");
    Check(name == "mirage_projector" ? PlayerStore.instance.projectorAttractivenessBonus == 7 : !PlayerStore.instance.isSecurityAlarmActive,
        name + " must restore its native PlayerStore side effect");
    supply.Reset(world); supply.Source.Energy = 0;
    world.Machine.state.GetTag("POWERED").valueEnabled = true;
    Callback.Native(world.Prefix, world.Context, () =>
    {
        if (!PowerHelper.DrawPowerSource(world.ReadSlot().childItem!, 4)) world.Machine.state.GetTag("POWERED").valueEnabled = false;
    });
    Check(!world.Machine.state.GetTag("POWERED").valueEnabled && supply.Debits == 0,
        "A normal native no-power branch must clear stale powered flags without opening a failed transaction");
}
{
    var world = new World("security_alarm"); supply.Reset(world); supply.Source.Energy = 4;
    Callback.Native(world.Prefix, world.Context, () =>
    {
        Check(PowerHelper.DrawPowerSource(world.ReadSlot().childItem!, 4), "Alarm should draw its exact available energy");
        Check(!PowerHelper.CanDrawPowerSource(world.ReadSlot().childItem!, 4), "Alarm's post-Draw display query must see remaining energy");
        world.Machine.state.GetTag("POWERED").Enable();
    });
    Check(supply.Source.Energy == 0 && supply.Debits == 1 && world.Machine.state.GetTag("POWERED").valueEnabled,
        "A post-Draw readiness query must not reject or debit the committed alarm again");
}
foreach (var (name, display) in new[] { ("desequencer", "BeforeDesequencerDisplay"), ("security_alarm", "BeforeAlarmDisplay"), ("fridge", "BeforeFridgeCooling") })
{
    var world = new World(name); supply.Reset(world);
    Callback.Native(display, world.Context, () =>
    {
        Check(PowerHelper.CanDrawPowerSource(world.ReadSlot().childItem!, 1), name + " display/cooling must see direct network supply");
        world.Payload.state.GetTag("CONTAINER_TEMP_TAG").valueDouble = -5;
    });
    Check(supply.Source.Energy == 20 && supply.Plans == 0 && world.Payload.state.GetTag("CONTAINER_TEMP_TAG").valueDouble == -5,
        name + " query-only native callbacks must not reserve or debit power");
}

// Robot night inlining is dispatched back through the two existing native
// helpers, with independent receipts and the original phase order.
{
    var world = new World("vacuum_robot"); supply.Reset(world); supply.Source.Energy = 4;
    var robot = (HusbandryDirectory.__c__DisplayClass1_0)world.Context;
    var feces = new GameItemElement { identifier = "feces" };
    world.Parent.UncheckedAccept(feces); world.Parent.childItems.Remove(feces); world.Parent.childItems.Insert(0, feces);
    var trace = new List<string>();
    robot.Cleanup = inventory =>
    {
        trace.Add("cleanup");
        if (!PowerHelper.CanDrawPowerSource(robot.batterySlot.childItem!, 4) || !PowerHelper.DrawPowerSource(robot.batterySlot.childItem!, 4)) return;
        inventory.Expel(feces); world.Output.UncheckedAccept(feces);
    };
    robot.Incinerate = () =>
    {
        trace.Add("incinerate");
        if (PowerHelper.DrawPowerSource(robot.batterySlot.childItem!, 4)) feces.Destroy();
    };
    Check(!(bool)Callback.Invoke("DispatchVacuumNight", [robot, world.Parent])!, "Connected robot night must use its original helper boundaries");
    Check(trace.SequenceEqual(new[] { "cleanup", "incinerate" }) && supply.Plans == 1 && supply.Source.Energy == 0 &&
        feces.parentInventory == world.Output && !feces.Destroyed && !supply.Busy,
        "Cleanup must commit before the incineration tests remaining power; lack of a second four units must skip incineration");
    // Put feces before the robot in their original parent. Its restoration must
    // attach all originals before checking/reordering the robot's original index.
    world.Output.Expel(feces); world.Parent.UncheckedAccept(feces); world.Parent.childItems.Remove(feces); world.Parent.childItems.Insert(0, feces);
    supply.Reset(world);
    robot.Cleanup = inventory =>
    {
        PowerHelper.DrawPowerSource(robot.batterySlot.childItem!, 4);
        inventory.Expel(feces); world.Output.UncheckedAccept(feces); feces.Destroy();
        throw new InvalidOperationException("cleanup failure after relocation");
    };
    try { robot.Method_Internal_Boolean_GameInventory_PDM_0(world.Parent); } catch (InvalidOperationException) { }
    Check(supply.Source.Energy == 20 && world.Parent.childItems[0] == feces && world.Parent.childItems[1] == world.Machine && !feces.Destroyed,
        "Failed robot cleanup must preserve source inventory order and the original feces UID after move/destruction");
    Check(!world.Machine.state.dict.ContainsKey("NICOKOBO_FORGE_MACHINE_FAULT"), "Certain out-of-order robot restoration must not quarantine");
    supply.Enabled = false;
    Check((bool)Callback.Invoke("DispatchVacuumNight", [robot, world.Parent])!, "Unclaimed robot night must retain its original callback body");
}
{
    var world = new World("registered-machine"); ForgeMachineRegistrationApi.Registered.Add(world.Machine.identifier);
    var customBattery = new GameSlotInventory(); var source = new GameItemElement { identifier = "actual-custom-battery" }; customBattery.UncheckedAccept(source);
    world.Machine.RegisteredInventory = new(customBattery);
    Check(ForgeExternalPowerApi.CanSupplyMachine(world.Machine) && ForgeExternalPowerApi.GetMachinePowerSource(world.Machine) == source,
        "Registered machines must read their actual inventory resolver rather than require logistics support or a module bay");
    foreach (string excluded in new[] { "recharger", "unknown-third-party" })
    { world.Machine.identifier = excluded; Check(!ForgeExternalPowerApi.CanSupplyMachine(world.Machine), "Only the audited native machines and real Forge registrations qualify"); }
}
{
    var world = new World("turbo_booster_adv"); supply.Reset(world); supply.Source.Energy = 5;
    world.Machine.state.GetTag("CURRENT_CHARGE_TAG").valueInt = 10;
    Callback.Native(world.Prefix, world.Context, () =>
    {
        Check(PowerHelper.DrawPowerSource(world.ReadSlot().childItem!, 5), "Booster must receive the same controlled real-source debit");
        world.Machine.state.GetTag("CURRENT_CHARGE_TAG").valueInt += 5;
        world.Machine.state.GetTag("TURBO_READY_TAG").Enable();
        Callback.Query("BeforeBoosterSprite", () =>
            Check(!PowerHelper.CanDrawPowerSource(world.ReadSlot().childItem!, 5), "Booster's post-Draw sprite query must see remaining energy"), world.Machine, world.ReadSlot());
    });
    Check(supply.Source.Energy == 0 && supply.Debits == 1 && world.Machine.state.GetTag("CURRENT_CHARGE_TAG").valueInt == 15 &&
        world.Machine.state.GetTag("TURBO_READY_TAG").valueEnabled, "Booster charge/readiness and one debit must commit together");
    Check(ForgeExternalPowerApi.CanSupplyDevice(world.Machine) && ForgeExternalPowerApi.GetDevicePowerSource(world.Machine) == world.Battery.childItem,
        "A portable booster naturally qualifies and resolves its real physical battery");
    supply.Reset(world); world.Battery.childItem!.Energy = 0;
    Callback.Query("BeforeBoosterSprite", () =>
    {
        Check(PowerHelper.CanDrawPowerSource(world.Battery.childItem!, 5), "Booster use/insert/remove UpdateSprite must see network power with an empty local source");
        // Using a charged booster can synchronously activate another native
        // machine. A display query must not occupy the native operation scope.
        var furnace = new World("furnace"); var previous = supply.Machine; supply.Machine = furnace.Machine;
        Callback.Native(furnace.Prefix, furnace.Context, () => furnace.Payload.state.GetTag("nested-native-body").Enable());
        supply.Machine = previous;
        Check(furnace.Payload.state.GetTag("nested-native-body").valueEnabled, "Booster sprite routing must not suppress a nested target machine action");
    }, world.Machine, world.Battery);
    Check(supply.Plans == 0 && supply.Source.Energy == 20, "All booster display refreshes must remain read-only power queries");
    world.Machine.state.GetTag("CURRENT_CHARGE_TAG").valueInt = 5;
    try
    {
        Callback.Native(world.Prefix, world.Context, () =>
        {
            PowerHelper.DrawPowerSource(world.ReadSlot().childItem!, 5);
            world.Machine.state.GetTag("CURRENT_CHARGE_TAG").valueInt = 10;
            throw new InvalidOperationException("booster write failure");
        });
    }
    catch (InvalidOperationException) { }
    Check(world.Machine.state.GetTag("CURRENT_CHARGE_TAG").valueInt == 5 && supply.Source.Energy == 20,
        "Failed booster actions must restore charge and actual power together");
}
{
    var world = new World("aug_scanner"); supply.Reset(world); supply.Source.Energy = 30;
    PlayerStore.instance!.currentClientInstance = new();
    var client = PlayerStore.instance.currentClientInstance.storeClient;
    int sounds = 0, nativeCalls = 0;
    Action nativeScan = () =>
    {
        nativeCalls++;
        // Exact native ordering: Draw's result is ignored, then the customer
        // flag is written, followed by an immediate native sound.
        PowerHelper.DrawPowerSource(world.ReadSlot().childItem!, 25);
        client.isAugScanned = true; sounds++;
    };
    Callback.Native(world.Prefix, world.Context, nativeScan);
    Check(client.isAugScanned && supply.Source.Energy == 5 && supply.Debits == 1 && nativeCalls == 1 && sounds == 1,
        "A real native scanner action must confirm its prefix reservation exactly once and scan its current customer");
    Check(ForgeExternalPowerApi.CanSupplyDevice(world.Machine) && ForgeExternalPowerApi.GetDevicePowerSource(world.Machine) == world.Battery.childItem,
        "A native scanner needs no machine registry or logistics modules to qualify");
    foreach (bool rejectReceipt in new[] { false, true })
    {
        supply.Reset(world); supply.Source.Energy = rejectReceipt ? 30 : 24; supply.RejectPlan = rejectReceipt;
        client.isAugScanned = false; nativeCalls = sounds = 0;
        Callback.Native(world.Prefix, world.Context, nativeScan);
        Check(!client.isAugScanned && nativeCalls == 0 && sounds == 0 && supply.Debits == 0 &&
            supply.Source.Energy == (rejectReceipt ? 30 : 24),
            "An insufficient or rejected scanner budget must skip the native customer write and sound entirely");
    }
    foreach (int failVerify in new[] { 2, 3 })
    {
        supply.Reset(world); supply.Source.Energy = 30; supply.FailVerifyAt = failVerify;
        client.isAugScanned = false;
        Callback.Native(world.Prefix, world.Context, nativeScan);
        Check(!client.isAugScanned && supply.Source.Energy == 30 && !supply.Busy,
            "Late scanner receipt failure must compensate the persistent customer result and energy");
    }
    supply.Reset(world); supply.Source.Energy = 30; client.isAugScanned = false;
    try
    {
        Callback.Native(world.Prefix, world.Context, () => { nativeScan(); throw new InvalidOperationException("scanner native exception"); });
    }
    catch (InvalidOperationException) { }
    Check(!client.isAugScanned && supply.Source.Energy == 30, "Native scanner exceptions must restore the captured customer's scan flag");
    supply.Reset(world); supply.Source.Energy = 30; world.Battery.childItem!.Energy = 0;
    Callback.Native("BeforeAugScannerDisplay", world.Context, () =>
        Check(PowerHelper.CanDrawPowerSource(world.ReadSlot().childItem!, 25), "Scanner May/Can checks must see network supply"));
    Check(supply.Plans == 0, "Scanner May/Can checks must not reserve energy");
    Callback.Query("BeforeAugScannerTooltip", () =>
    {
        Check(AugScanner.GetBatterySlot(world.Machine) == world.Battery && ForgeExternalPowerApi.GetDevicePowerSource(world.Machine) == world.Battery.childItem,
            "Tooltip queries must leave native and consumer resolvers observing the physical source");
        Check(PowerHelper.CanDrawPowerSource(AugScanner.GetBatterySlot(world.Machine).childItem!, 25),
            "Scanner tooltip's native real-battery CanDraw must route to its device network");
    }, new RichTextBuilder(), world.Machine);
    Check(supply.Plans == 0 && supply.Source.Energy == 30 && !PowerHelper.CanDrawPowerSource(world.Battery.childItem!, 25),
        "Tooltip query scope must clear afterward without spending or globally changing battery behavior");
    try
    {
        Callback.Query("BeforeAugScannerTooltip", () => throw new InvalidOperationException("tooltip failure"), new RichTextBuilder(), world.Machine);
    }
    catch (InvalidOperationException) { }
    Check(!PowerHelper.CanDrawPowerSource(world.Battery.childItem!, 25), "A throwing tooltip must release its source-query routing");
    world.Machine.identifier = "checks.scanner_clone";
    using var clone = ForgeExternalPowerApi.RegisterConsumer("checks", world.Machine.identifier, item => AugScanner.GetBatterySlot(item).childItem);
    client.isAugScanned = false;
    Callback.Native(world.Prefix, world.Context, nativeScan);
    Check(supply.Debits == 1 && supply.Source.Energy == 5 && client.isAugScanned,
        "A declared clone using a native scanner closure must resolve its owner from the original slot and debit only once");
}
checks += ConsumerChecks.Run();
Console.WriteLine($"Native external power adapter: {checks} assertions passed (production callbacks with offline native-tree ports).");

internal sealed class Supply
{
    internal GameItem Source = new GameItemElement { identifier = "grid-source", Energy = 20 };
    internal GameItem Machine = null!;
    internal bool Busy, RejectPlan, Enabled = true;
    internal int Plans, Debits, Disposals, FailVerifyAt;
    internal void Reset(World world)
    { Machine = world.Machine; Source.Energy = 20; Busy = false; RejectPlan = false; Enabled = true; Plans = Debits = Disposals = FailVerifyAt = 0; }
    internal int Energy(GameItem machine) => !Enabled || machine != Machine ? -1 : PowerHelper.GetAvailableEnergyFromItem(Source, false);
    internal IForgePowerTransaction? Plan(GameItem machine, int cost)
    {
        Plans++;
        if (RejectPlan || Busy || machine != Machine) return null;
        Busy = true; return new Receipt(this, cost);
    }
    private sealed class Receipt(Supply owner, int cost) : IForgePowerTransaction
    {
        private readonly int _before = owner.Source.Energy;
        private bool _debited;
        private int _verifications;
        public bool Validate() => !_debited && owner.Source.Energy == _before;
        public void Debit() { if (!PowerHelper.DrawPowerSource(owner.Source, cost)) throw new Exception("source empty"); _debited = true; owner.Debits++; }
        public bool Verify() => ++_verifications != owner.FailVerifyAt && _debited && owner.Source.Energy == _before - cost;
        public bool Restore() { owner.Source.Energy = _before; _debited = false; return true; }
        public void Fault(string reason) { }
        public void Dispose() { owner.Busy = false; owner.Disposals++; }
    }
}
internal sealed class World
{
    internal readonly GameItemElement Machine, Payload = new() { identifier = "payload" };
    internal readonly GameGridInventory Parent = new(), Output = new();
    internal readonly GameInventory Input;
    internal readonly GameSlotInventory Battery = new();
    internal readonly object Context;
    internal readonly string Prefix;
    internal readonly Func<GameSlotInventory> ReadSlot;
    internal World(string id)
    {
        Machine = new() { identifier = id };
        Input = id == "desequencer" ? new GameSlotInventory() : new GameGridInventory();
        var layout = new GridPixelElement(5, 2); layout.Put(0, 1, Battery); layout.Put(2, 1, Input); layout.Put(3, 1, Output);
        Machine.children.Add(new PixelWindow(layout)); Parent.UncheckedAccept(Machine); Input.UncheckedAccept(Payload); Battery.Owner = Machine;
        Battery.UncheckedAccept(new GameItemElement { identifier = "local-battery", Energy = 9 });
        (Context, Prefix, ReadSlot) = id switch
        {
            "furnace" => Make(new MachineFurnace.__c__DisplayClass0_0 { furnace = Machine, itemInventoryBattery = Battery }, "BeforeFurnace", value => value.itemInventoryBattery),
            "water_purifier" => Make(new MachinePurifier.__c__DisplayClass1_0 { machine = Machine, powerInv = Battery }, "BeforePurifier", value => value.powerInv),
            "moisture_farm" => Make(new MachineMoistureFarm.__c__DisplayClass0_0 { machine = Machine, powerInv = Battery }, "BeforeMoisture", value => value.powerInv),
            "desequencer" => Make(new MachineDesequencer.__c__DisplayClass5_0 { machine = Machine, powerInv = Battery }, "BeforeDesequencer", value => value.powerInv),
            "wine_rack" => Make(new MachineWineRack.__c__DisplayClass3_0 { machine = Machine, itemInventoryBattery = Battery }, "BeforeWine", value => value.itemInventoryBattery),
            "bottle_printer" => Make(new MachineBottlePrinter.__c__DisplayClass6_0 { machine = Machine, itemInventory = Battery }, "BeforeBottle", value => value.itemInventory),
            "box_dispenser" => Make(new MachineBoxDispenser.__c__DisplayClass0_0 { itemInventory = Battery }, "BeforeBox", value => value.itemInventory),
            "fridge" => Make(new MachineFridge.__c__DisplayClass26_0 { machine = Machine, batterySlot = Battery }, "BeforeFridge", value => value.batterySlot),
            "mirage_projector" => Make(new MachineProjector.__c__DisplayClass1_0 { machine = Machine, itemInventory = Battery }, "BeforeProjector", value => value.itemInventory),
            "security_alarm" => Make(new MachineAlarm.__c__DisplayClass1_0 { machine = Machine, itemInventory = Battery }, "BeforeAlarm", value => value.itemInventory),
            "turbo_booster_adv" => Make(new ToolDirectory.__c__DisplayClass25_0 { item = Machine, batterySlot = Battery }, "BeforeBooster", value => value.batterySlot),
            "aug_scanner" => Make(new MiscItemDirectory.__c__DisplayClass75_0 { batterySlot = Battery }, "BeforeAugScanner", value => value.batterySlot),
            _ => Make(new HusbandryDirectory.__c__DisplayClass1_0 { item = Machine, batterySlot = Battery }, "BeforeVacuumIncinerate", value => value.batterySlot)
        };
    }
    private static (object, string, Func<GameSlotInventory>) Make<T>(T context, string prefix, Func<T, GameSlotInventory> slot) => (context!, prefix, () => slot(context));
}
