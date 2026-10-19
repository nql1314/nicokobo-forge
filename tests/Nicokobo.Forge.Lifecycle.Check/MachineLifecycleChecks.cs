using System.Reflection;
using Il2Cpp;
using Nicokobo.Forge;
using Nicokobo.Forge.Runtime;

internal static class MachineLifecycleChecks
{
    internal static void Run(Action<bool, string> require)
    {
        NativeHookSet.FailTarget = nameof(GameItem.TryFindOneValidInventorySlot);
        require(!ForgeMachineHooks.Install(true, _ => { }), "Missing core drop routing must still reject the machine runtime");
        NativeHookSet.FailTarget = "_CreateTurboBooster_b__4";
        require(ForgeMachineHooks.Install(true, _ => { }), "A missing accelerator must not disable registered machine lifecycle or production");
        require(!NativeHookSet.Installed.ContainsKey("nicokobo.forge.machines.accelerators"), "A failed accelerator must not keep partial hooks");
        NativeHookSet.FailTarget = null;
        var store = PlayerStore.Instance;
        store.DuringNight = () => Boundary.Call(typeof(ModHook), nameof(ModHook.FireOnGoingSleepLate), store, true);
        store.Items.Add(new());
        Boundary.Call(typeof(PlayerStore), nameof(PlayerStore.EndNight), store, true);
        int reads = ForgeMachineUi.Reads, resets = ForgeMachineUi.Resets;
        var quarantine = (HashSet<int>)typeof(ForgeMachineRuntime).GetField("Quarantined", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        quarantine.Add(42);
        Boundary.Call(typeof(PlayerStore), nameof(PlayerStore.LoadGame), store, false);
        require(ForgeMachineUi.Resets == resets && quarantine.Contains(42), "Refused LoadGame cleared live machine UI or quarantine");
        Boundary.Call(typeof(PlayerStore), nameof(PlayerStore.EndNight), store, true);
        require(ForgeMachineUi.Reads == reads, "Refused LoadGame cleared the actual same-day machine attempt ledger");
        Boundary.Call(typeof(PlayerStore), nameof(PlayerStore.LoadGame), store, true);
        require(ForgeMachineUi.Resets == resets + 1 && quarantine.Count == 0, "Executed LoadGame did not reset machine state exactly once");
        require(ForgeMachineUi.Reads == reads + 1, "Executed LoadGame did not rebind live machine slots after reset");
        Boundary.Call(typeof(PlayerStore), nameof(PlayerStore.EndNight), store, true);
        require(ForgeMachineUi.Reads == reads + 2, "Executed LoadGame retained an earlier session's nightly attempt ledger");
        CheckMixedRecipePlanning(require);
        CheckDirectPowerPlanning(require);
        CheckReceiptCleanup(require);
        CheckNightOrder(require);
        store.DuringNight = null;
    }

    private static void CheckNightOrder(Action<bool, string> require)
    {
        var store = PlayerStore.Instance;
        var previous = store.Items.ToArray();
        try
        {
            store.Items.Clear(); ForgeMachineRuntime.BeforeLoadGame();
            foreach (var (id, tier) in new[] { (804, 300), (803, 100), (801, 100), (802, 200) })
            {
                string name = "night-machine-" + id;
                ForgeMachineRegistrationApi.Profiles[name] = new(new(name,
                    new(null, new(ForgeMachineOutputKind.Items, new(1, 1))), [], new()) { NightlyOrder = tier });
                store.Items.Add(new() { Pointer = (IntPtr)id, identifier = name });
            }
            ForgeMachineUi.ReadOrder.Clear();
            bool observedAfterProcessing = false;
            using var observer = ForgeLifecycleApi.Subscribe("test.machine_order", "test.machine_order.after_sleep",
                ForgeLifecyclePhase.AfterSleep, _ => observedAfterProcessing = ForgeMachineUi.ReadOrder.SequenceEqual(new[] { 801, 803, 802, 804 }));
            Boundary.Call(typeof(PlayerStore), nameof(PlayerStore.EndNight), store, false);
            require(ForgeMachineUi.ReadOrder.Count == 0, "A skipped native night must not process registered machines");
            Boundary.Call(typeof(PlayerStore), nameof(PlayerStore.EndNight), store, true);
            require(ForgeMachineUi.ReadOrder.SequenceEqual(new[] { 801, 803, 802, 804 }),
                "Nightly machines must process low tiers first and use instance ID for ties, regardless of inventory order");
            require(observedAfterProcessing, "Registered production must finish before module idle decay observes AfterSleep");
            ForgeMachineUi.ReadOrder.Clear();
            Boundary.Call(typeof(PlayerStore), nameof(PlayerStore.EndNight), store, true);
            require(ForgeMachineUi.ReadOrder.Count == 0, "Ordering must preserve the same-night attempt ledger");
        }
        finally
        {
            store.Items.Clear(); store.Items.AddRange(previous);
            ForgeMachineRegistrationApi.Profiles.Clear(); ForgeMachineRuntime.BeforeLoadGame();
        }
    }

    private sealed class PlannerPowerReceipt : IForgePowerTransaction
    {
        internal bool Valid = true, Disposed;
        internal bool ThrowOnDispose;
        internal int DisposeCalls;
        public bool Validate() => Valid && !Disposed;
        public void Debit() => throw new NotSupportedException("Planning must not draw power");
        public bool Verify() => throw new NotSupportedException("Planning must not commit power");
        public bool Restore() => true;
        public void Fault(string reason) => throw new NotSupportedException(reason);
        public void Dispose()
        {
            Disposed = true; DisposeCalls++;
            if (ThrowOnDispose) throw new InvalidOperationException("injected power cleanup failure");
        }
    }

    private sealed class PlannerBatchReceipt : IForgeBatchTransaction
    {
        internal bool Valid;
        internal int DisposeCalls;
        public bool Validate() => Valid;
        public void Apply(IReadOnlyList<GameItem> products) => throw new NotSupportedException();
        public bool Verify() => throw new NotSupportedException();
        public bool Restore() => true;
        public void Fault(string reason) => throw new NotSupportedException(reason);
        public void Dispose() { DisposeCalls++; throw new InvalidOperationException("injected recipe cleanup failure"); }
    }

    private static void CheckReceiptCleanup(Action<bool, string> require)
    {
        var output = new GameInventory { Pointer = (IntPtr)902 };
        var inventory = new ForgeMachineInventory(null, null, null, output, new Dictionary<string, GameSlotInventory>(), null);
        var machine = new GameItem { Pointer = (IntPtr)901, identifier = "cleanup-machine" };
        var template = new ForgeMachineTemplate(null, new(ForgeMachineOutputKind.Items, new(1, 1)))
            { Battery = null, Modules = null, ManualSlot = false };
        var planner = typeof(ForgeMachineRuntime).GetMethod("TryPlan", BindingFlags.NonPublic | BindingFlags.Static)!;
        try
        {
            foreach (bool powerThrows in new[] { true, false })
            {
                var power = new PlannerPowerReceipt { ThrowOnDispose = powerThrows };
                var participant = new PlannerBatchReceipt();
                var recipe = new ForgeMachineRecipe("cleanup", [], new ForgeMachineItemOutput("result"))
                    { Transaction = _ => participant };
                var profile = new Nicokobo.Forge.Registration.MachineProfile(new(machine.identifier,
                    template, [recipe], new(10)));
                ForgeExternalPowerApi.MachineEnergy = _ => 20;
                ForgeExternalPowerApi.MachinePlan = (_, _) => power;
                object?[] arguments = [machine, profile, inventory, recipe, null, null];
                require(!(bool)planner.Invoke(null, arguments)!, "Rejected recipe participant must reject planning");
                require(power.DisposeCalls == 1 && participant.DisposeCalls == 1,
                    "Planning rejection must release each receipt once even when either release throws");
                require(arguments[4] == null && Equals(arguments[5], "input-changed"),
                    "Cleanup errors must not replace the planning result or expose a disposed plan");
            }
            foreach (string result in new[] { "produced:cleanupx1", "rolled-back" })
            {
                var power = new PlannerPowerReceipt { ThrowOnDispose = true };
                var participant = new PlannerBatchReceipt { Valid = true };
                var recipe = new ForgeMachineRecipe("cleanup", [], new ForgeMachineItemOutput("result"))
                    { Transaction = _ => participant };
                var profile = new Nicokobo.Forge.Registration.MachineProfile(new(machine.identifier,
                    template, [recipe], new(10)));
                ForgeExternalPowerApi.MachinePlan = (_, _) => power;
                ForgeMachineRuntime.ExecuteBoundary = () => result;
                bool observed = false;
                ForgeMachineAutomationApi.Observe = (phase, context) =>
                {
                    if (phase == ForgeMachineAutomationPhase.AfterBatch)
                        observed = context.Result == result && power.DisposeCalls == 1 && participant.DisposeCalls == 1;
                };
                string actual = ForgeMachineRuntime.ProcessBatch(machine, profile, inventory);
                require(actual == result && observed,
                    $"Committed or rolled-back batches must notify automation after both cleanup attempts despite disposal errors: {actual}; {observed}; {power.DisposeCalls}/{participant.DisposeCalls}");
                power = new() { ThrowOnDispose = true }; participant = new() { Valid = true };
                ForgeMachineRuntime.ExecuteBoundary = () => throw new InvalidOperationException("injected execution failure");
                bool originalFailure = false;
                try { ForgeMachineRuntime.ProcessBatch(machine, profile, inventory); }
                catch (InvalidOperationException error) { originalFailure = error.Message == "injected execution failure"; }
                require(originalFailure && power.DisposeCalls == 1 && participant.DisposeCalls == 1,
                    "An execution error must retain its identity while both receipts are released");
            }
        }
        finally
        {
            ForgeExternalPowerApi.MachineEnergy = null; ForgeExternalPowerApi.MachinePlan = null;
            ForgeMachineRuntime.ExecuteBoundary = null; ForgeMachineAutomationApi.Observe = null;
        }
    }

    private static void CheckDirectPowerPlanning(Action<bool, string> require)
    {
        var input = new GameInventory { Pointer = (IntPtr)601 };
        var output = new GameInventory { Pointer = (IntPtr)602 };
        var slot = new GameSlotInventory { Pointer = (IntPtr)603 };
        var battery = new GameItem { Pointer = (IntPtr)604, identifier = "battery", parentInventory = slot };
        var recipe = new ForgeMachineRecipe("powered", [], new ForgeMachineItemOutput("result"));
        var definition = new ForgeMachineDefinition("grid-machine", new(new(1, 1), new(ForgeMachineOutputKind.Items, new(1, 1))),
            [recipe], new(10));
        var profile = new Nicokobo.Forge.Registration.MachineProfile(definition);
        var machine = new GameItem { Pointer = (IntPtr)605, identifier = definition.MachineId };
        var inventory = new ForgeMachineInventory(slot, null, input, output, new Dictionary<string, GameSlotInventory>(), null);
        var planner = typeof(ForgeMachineRuntime).GetMethod("TryPlan", BindingFlags.NonPublic | BindingFlags.Static)!;
        var unchanged = typeof(ForgeMachineRuntime).GetMethod("Unchanged", BindingFlags.NonPublic | BindingFlags.Static)!;
        int energy = 20; string? status = null;
        var receipt = new PlannerPowerReceipt();
        object? Plan()
        {
            object?[] arguments = [machine, profile, inventory, recipe, null, null];
            bool planned = (bool)planner.Invoke(null, arguments)!;
            status = arguments[5]?.ToString(); return planned ? arguments[4] : null;
        }
        try
        {
            ForgeExternalPowerApi.MachineEnergy = _ => energy;
            ForgeExternalPowerApi.MachinePlan = (_, _) => receipt;
            ForgePowerApi.SourceEnergy = item => item.Pointer == battery.Pointer ? 100 : null;
            var plan = Plan();
            require(plan != null && plan.GetType().GetProperty("Battery")!.GetValue(plan) == null && !receipt.Disposed,
                "A required-battery machine must plan directly from the grid with an empty local slot: " + status);
            energy = 0;
            require(!(bool)unchanged.Invoke(null, [plan])!, "A grid outage must invalidate an already planned batch");
            slot.childItem = battery;
            require(Plan() == null && status == "power-unavailable" && ForgePowerApi.ReadSource(battery) == 100,
                "An empty claimed grid must block despite a full local battery");
            energy = -1;
            var ordinary = Plan();
            require(ordinary != null && ordinary.GetType().GetProperty("Battery")!.GetValue(ordinary) == battery,
                "Unclaimed machines must retain ordinary local-battery planning");
            energy = 20; ForgeExternalPowerApi.MachinePlan = (_, _) => null;
            require(Plan() == null && status == "external-power-unavailable", "Grid energy without a power receipt cannot start a batch");
            receipt.Valid = false; ForgeExternalPowerApi.MachinePlan = (_, _) => receipt;
            require(Plan() == null && receipt.Disposed, "A rejected direct-power receipt must be disposed by the planner");
            receipt = new(); slot.childItem = null;
            plan = Plan(); energy = -1;
            require(plan != null && !(bool)unchanged.Invoke(null, [plan])!, "A released grid claim must invalidate its earlier plan");
            receipt.Dispose();
        }
        finally
        {
            ForgeExternalPowerApi.MachineEnergy = null; ForgeExternalPowerApi.MachinePlan = null; ForgePowerApi.SourceEnergy = null;
        }
    }

    private static void CheckMixedRecipePlanning(Action<bool, string> require)
    {
        const string quartzId = "nicokobo.mechcore.synthesis.quartz";
        const string titaniumId = "nicokobo.mechcore.synthesis.titanium_ore";
        var input = new GameInventory { Pointer = (IntPtr)501 };
        var output = new GameInventory { Pointer = (IntPtr)502 };
        var quartz = new GameItem { Pointer = (IntPtr)503, identifier = quartzId, unitCount = 2, parentInventory = input };
        var titanium = new GameItem { Pointer = (IntPtr)504, identifier = titaniumId, parentInventory = input };
        var ingot = new GameItem { Pointer = (IntPtr)505, identifier = "metal_ingot", parentInventory = input };
        ingot.state.dict.Add("INGOT_PURITY_HIGH", new("INGOT_PURITY_HIGH", "High") { enabled = true });
        var siliconRecipe = new ForgeMachineRecipe("silicon", [new(quartzId, 2)], new ForgeMachineItemOutput("silicon"));
        var alloyRecipe = new ForgeMachineRecipe("titanium_alloy", [new(titaniumId, 1),
            new("metal_ingot", 1, item => item.IsTag("INGOT_PURITY_HIGH"))], new ForgeMachineItemOutput("titanium_alloy"));
        var definition = new ForgeMachineDefinition("mixed-refiner", new(new(9, 6), new(ForgeMachineOutputKind.Items, new(9, 6)))
            { Battery = null, Modules = null },
            [siliconRecipe, alloyRecipe], new());
        var profile = new Nicokobo.Forge.Registration.MachineProfile(definition);
        var machine = new GameItem { Pointer = (IntPtr)506, identifier = definition.MachineId };
        var inventory = new ForgeMachineInventory(null, null, input, output, new Dictionary<string, GameSlotInventory>(), null);
        var planner = typeof(ForgeMachineRuntime).GetMethod("TryPlan", BindingFlags.NonPublic | BindingFlags.Static)!;
        string? planningStatus = null;
        ForgeMachineBatchContext? Plan(ForgeMachineRecipe recipe)
        {
            object?[] arguments = [machine, profile, inventory, recipe, null, null];
            bool planned = (bool)planner.Invoke(null, arguments)!;
            planningStatus = arguments[5]?.ToString();
            if (!planned) return null;
            var plan = arguments[4]!;
            return (ForgeMachineBatchContext)plan.GetType().GetProperty("Context")!.GetValue(plan)!;
        }
        foreach (var ordering in new[] { new[] { quartz, titanium, ingot }, new[] { ingot, quartz, titanium }, new[] { titanium, ingot, quartz } })
        {
            input.childItems = [.. ordering];
            var silicon = Plan(siliconRecipe);
            require(silicon?.Items.Count == 1 && silicon.Items[0].Item == quartz && silicon.Items[0].Count == 2,
                "Mixed refiner inputs must plan silicon using only two quartz units: " + planningStatus);
            var alloy = Plan(alloyRecipe);
            require(alloy?.Items.Count == 2 && alloy.Items.All(take => take.Count == 1 && (take.Item == titanium || take.Item == ingot)),
                "Mixed refiner inputs must plan alloy without consuming quartz");
        }
        require(quartz.unitCount == 2 && titanium.unitCount == 1 && ingot.unitCount == 1 &&
            input.childItems.Count == 3 && output.childItems.Count == 0,
            "Planning mixed recipes must not mutate inputs or outputs");
        quartz.unitCount = 1;
        require(Plan(siliconRecipe) == null && Plan(alloyRecipe) != null,
            "Incomplete silicon ingredients must not block a complete alloy recipe");
        quartz.unitCount = 2;
        ingot.state.dict["INGOT_PURITY_HIGH"].enabled = false;
        require(Plan(siliconRecipe) != null && Plan(alloyRecipe) == null,
            "Ineligible alloy ingots must not block a complete silicon recipe");
    }
}
