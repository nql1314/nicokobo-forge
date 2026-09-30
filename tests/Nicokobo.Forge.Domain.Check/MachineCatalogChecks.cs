using Il2Cpp;
using Nicokobo.Forge;
using Nicokobo.Forge.Registration;

// Registration tests only need delegate type identities. They never create or
// simulate a native item, run Harmony, or claim inventory/runtime validation.
namespace Il2Cpp
{
    public sealed class GameItem;
    public sealed class GameInventory;
}

internal static class MachineCatalogChecks
{
    internal static void Run()
    {
        static void Expect(bool value, string message)
        {
            if (!value) throw new Exception("Machine catalog: " + message);
        }
        const string owner = "test.owner";
        const string machine = owner + ".machine";
        var catalog = new MachineCatalog();
        int factories = 0;
        SubmitResult StageItem() { factories++; return new(SubmitStatus.Accepted, "test"); }
        var inputs = new List<ForgeMachineIngredient> { new("quartz", 2) };
        var auxiliary = new List<string> { "flux_agent" };
        var recipe = new ForgeMachineRecipe(owner + ".recipe", inputs, "silicon", 1, 12)
            { AuxiliaryItemIds = auxiliary };
        Expect(!catalog.HasMachines, "empty catalog");
        Expect(catalog.Register(owner, machine, [recipe],
            ForgeMachineProcessMode.NightlyItems, false, registerItem: StageItem).Status == SubmitStatus.Accepted,
            "valid machine rejected");
        Expect(factories == 1 && catalog.HasMachines, "factory declaration not staged once");
        Expect(catalog.TryGet(machine, out var first) && first != null, "profile missing");
        Expect(first!.IsFeedstock("quartz") && first.Accepts("flux_agent") &&
            !first.IsFeedstock("flux_agent") && !first.Accepts("ore"), "admission index");
        inputs.Clear(); auxiliary.Add("ore");
        Expect(first.Recipes[0].Value.Inputs.Count == 1 && !first.Accepts("ore") &&
            first.Recipes[0].Value.AuxiliaryItemIds!.Count == 1, "caller mutated staged recipe");
        var addition = new ForgeMachineRecipe("test.other.recipe", [new("iron", 1)], "part", 1, 4);
        Expect(catalog.RegisterAdditional("test.other", machine, [addition]).Status == SubmitStatus.Accepted,
            "contributor rejected");
        Expect(catalog.TryGet(machine, out var second) && second!.Recipes.Count == 2 &&
            second.IsFeedstock("iron"), "contributor index missing");
        Expect(first.Recipes.Count == 1 && !first.IsFeedstock("iron"), "runtime snapshot changed during registration");
        Expect(catalog.RegisterAdditional("test.other", machine, [addition]).Status == SubmitStatus.Conflict,
            "duplicate replaced recipe");
        var third = addition with { RecipeId = "test.other.third" };
        Expect(catalog.RegisterAdditional("test.other", machine, [third, addition]).Status == SubmitStatus.Conflict &&
            catalog.Snapshot(false).Single().RecipeCount == 2, "conflicting batch partially registered");
        Expect(catalog.Register(owner, machine, [recipe], ForgeMachineProcessMode.NightlyItems,
            false, registerItem: StageItem).Status == SubmitStatus.Invalid && factories == 1,
            "invalid input invoked factory");
        var valid = recipe with { Inputs = [new("quartz", 2)] };
        Expect(catalog.Register(owner, machine, [valid], ForgeMachineProcessMode.NightlyItems,
            false, registerItem: StageItem).Status == SubmitStatus.Conflict && factories == 1,
            "duplicate declaration invoked factory");
        foreach (var invalid in new[]
        {
            valid with { RecipeId = "foreign.recipe" },
            valid with { Inputs = [] }, valid with { Inputs = [new("quartz", 0)] },
            valid with { OutputCount = 0 }, valid with { OutputCount = 257 },
            valid with { PowerCost = -1 }, valid with { AuxiliaryItemIds = [" "] },
            valid with { Water = new(100, 1) }, valid with { AcceptsStackedInput = true },
            valid with { ResolveConsumedUnits = (_, _) => 1 },
            valid with { ResolveWaterMillilitres = (_, _) => 100 },
            valid with { ProgressVersion = 0 }
        })
            Expect(catalog.RegisterAdditional(owner, machine, [invalid]).Status == SubmitStatus.Invalid,
                "invalid recipe accepted: " + invalid);
        Expect(catalog.RegisterAdditional("test.other", "missing", [addition]).Status == SubmitStatus.Invalid,
            "unknown target accepted");
        Expect(catalog.Register(owner, "furnace", [valid],
            ForgeMachineProcessMode.NightlyItems, true).Status == SubmitStatus.Accepted,
            "native furnace rejected");
        var glassRecipes = new[]
        {
            ("empty_beer_bottle", 1), ("glass_shard_shiv", 2),
            ("glass_shard", 2), ("wine_bottle", 1)
        }.Select(input => new ForgeMachineRecipe(owner + "." + input.Item1,
            [new(input.Item1, input.Item2)], "quartz", 1, 8)).ToArray();
        Func<GameItem, GameInventory, bool> nativeProbe = (_, _) => false;
        Expect(catalog.Register(owner, "furnace", glassRecipes,
            ForgeMachineProcessMode.NightlyItems, true, nativeProbe).Status == SubmitStatus.Accepted,
            "glass recipes rejected");
        Expect(catalog.Register(owner, machine, [valid],
            ForgeMachineProcessMode.NightlyItems, false,
            nativeProbe, StageItem).Status == SubmitStatus.Invalid,
            "custom machine accepted a native batch probe");
        Expect(catalog.TryGet("furnace", out var furnace) && furnace!.NativeMachine &&
            ReferenceEquals(furnace.NativeBatchProbe, nativeProbe),
            "native furnace profile missing");
        Expect(catalog.Register(owner, "furnace",
            [new(owner + ".extra", [new("glass_shard", 3)], "quartz", 1, 8)],
            ForgeMachineProcessMode.NightlyItems, true).Status == SubmitStatus.Accepted &&
            catalog.TryGet("furnace", out var extended) &&
            ReferenceEquals(extended!.NativeBatchProbe, nativeProbe),
            "later furnace registration dropped the native batch probe");
        foreach (var recipeInput in glassRecipes.SelectMany(entry => entry.Inputs))
            Expect(furnace!.Accepts(recipeInput.ItemId) &&
                furnace.IsFeedstock(recipeInput.ItemId),
                "registered furnace input rejected: " + recipeInput.ItemId);
        Expect(!furnace!.Accepts("unrelated_item"), "unrelated furnace input admitted");
        var configuredInputs = new List<ForgeMachineIngredient> { new("custom_scrap", 2) };
        Expect(catalog.Register(owner, "furnace",
            [new(owner + ".configured", configuredInputs, "quartz", 1, 8)],
            ForgeMachineProcessMode.NightlyItems, true).Status == SubmitStatus.Accepted,
            "configured furnace recipe rejected");
        configuredInputs[0] = new("unrelated_item", 1);
        Expect(catalog.TryGet("furnace", out var configuredFurnace) &&
            configuredFurnace!.Accepts("custom_scrap") &&
            !configuredFurnace.Accepts("unrelated_item"),
            "configured furnace admission must follow frozen recipe inputs");
        Expect(catalog.RegisterAdditional("test.other", "furnace", [addition]).Status == SubmitStatus.Conflict,
            "additional custom recipes accepted on native furnace");
        Expect(catalog.Register(owner, "other-native", [valid],
            ForgeMachineProcessMode.NightlyItems, true).Status == SubmitStatus.Invalid,
            "unsupported native machine accepted");
        var liquid = new ForgeMachineRecipe(owner + ".liquid", [new("meat", 1)], "gel", 1, 8,
            Water: new(100, 2), ResolveLifetimeOutputCount: (_, _) => 4);
        Expect(catalog.Register(owner, owner + ".liquid_machine", [liquid],
            ForgeMachineProcessMode.NightlyLiquid, false, registerItem: StageItem).Status == SubmitStatus.Accepted,
            "valid liquid recipe rejected");
        Expect(catalog.Register(owner, owner + ".stack_machine",
            [liquid with
            {
                RecipeId = owner + ".stacked", AcceptsStackedInput = true,
                ResolveConsumedUnits = (_, _) => 1
            }],
            ForgeMachineProcessMode.NightlyLiquid, false,
            registerItem: StageItem).Status == SubmitStatus.Accepted,
            "stacked liquid recipe rejected");
        // A recipe may price its own draw: the resolver replaces the fixed
        // millilitres-per-output, so a zero floor is allowed with it.
        var metered = liquid with
        {
            RecipeId = owner + ".metered", Water = new(0, 2),
            ResolveWaterMillilitres = (_, _) => 120
        };
        Expect(catalog.Register(owner, owner + ".metered_machine", [metered],
            ForgeMachineProcessMode.NightlyLiquid, false,
            registerItem: StageItem).Status == SubmitStatus.Accepted,
            "metered liquid recipe rejected");
        Expect(catalog.RegisterAdditional("test.other", owner + ".liquid_machine", [addition]).Status == SubmitStatus.Conflict,
            "item recipe appended to liquid machine");
        foreach (var invalid in new[]
        {
            liquid with { Water = new(0, 2) }, liquid with { Water = new(100, 4) },
            liquid with { Inputs = [new("meat", 2)] }, liquid with { MaxOutputsPerNight = 257 },
            liquid with { ResolveLifetimeOutputCount = null },
            metered with { Water = null }
        })
            Expect(catalog.Register(owner, owner + ".bad_liquid", [invalid],
                ForgeMachineProcessMode.NightlyLiquid, false, registerItem: StageItem).Status == SubmitStatus.Invalid,
                "invalid liquid accepted");
        Expect(catalog.Register(owner, owner + ".failed", [valid],
            ForgeMachineProcessMode.NightlyItems, false, registerItem:
            () => new(SubmitStatus.Conflict, "native collision")).Status == SubmitStatus.Conflict &&
            !catalog.TryGet(owner + ".failed", out _), "failed native declaration published profile");
        Expect(catalog.Snapshot(true).All(view => view.RuntimeInstalled), "snapshot lost installation state");
        Console.WriteLine("Machine catalog checks passed: validation, atomicity, frozen profiles, native batch probes and admission indexes.");
    }
}
