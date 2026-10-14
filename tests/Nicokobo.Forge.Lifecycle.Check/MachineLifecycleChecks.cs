using System.Reflection;
using Il2Cpp;
using Nicokobo.Forge;
using Nicokobo.Forge.Runtime;

internal static class MachineLifecycleChecks
{
    internal static void Run(Action<bool, string> require)
    {
        require(ForgeMachineHooks.Install(true, _ => { }), "Production machine lifecycle installer failed");
        var store = PlayerStore.Instance;
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
