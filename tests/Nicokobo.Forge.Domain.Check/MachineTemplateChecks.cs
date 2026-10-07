using Il2Cpp;
using Nicokobo.Forge;
using Nicokobo.Forge.Registration;

// Only delegate identities are required here. No native objects or runtime
// behavior are simulated by registration checks.
namespace Il2Cpp
{
    public sealed partial class GameItem;
    public partial class GameInventory : PixelElement;
    public sealed class GameSlotInventory : GameInventory;
}

internal static class MachineCatalogChecks
{
    internal static void Run()
    {
        int checks = 0, staged = 0;
        void Expect(bool value, string message)
        { checks++; if (!value) throw new Exception("Machine template: " + message); }
        SubmitResult Stage(ForgeMachineDefinition _) { staged++; return new(SubmitStatus.Accepted, "test"); }
        const string owner = "test.owner", id = owner + ".machine";
        var catalog = new MachineCatalog();
        var inputs = new List<ForgeMachineIngredient> { new("raw_material", 2) };
        var auxiliary = new List<string> { "auxiliary_material" };
        var modules = new List<string> { "MODULE_TYPE_FURNACE" };
        var slots = new List<ForgeMachineContainerSlot> { new("water", new(3, 3)) };
        var liquidInputs = new List<ForgeMachineLiquidIngredient> { new("water", 100) };
        var contents = new List<ForgeMachineLiquidAmount> { new("protein", 100) };
        var recipe = new ForgeMachineRecipe(owner + ".recipe", inputs,
            new ForgeMachineItemOutput("canister", Contents: contents))
            { LiquidInputs = liquidInputs, AuxiliaryItemIds = auxiliary };
        var template = new ForgeMachineTemplate(new(6, 4), new(ForgeMachineOutputKind.Items, new(6, 4)))
            { LiquidInputs = slots, Modules = new(new(4, 4), modules) };
        Func<GameItem, int> nightlyCount = _ => 3;
        var definition = new ForgeMachineDefinition(id, template, [recipe], new(8))
            { ProductionMarkupPercent = 25, ResolveNightlyBatchCount = nightlyCount };
        Expect(!catalog.HasMachines, "empty catalog");
        Expect(catalog.Register(owner, definition, Stage).Status == SubmitStatus.Accepted && staged == 1,
            "mixed input registration rejected");
        Expect(catalog.TryGet(id, out var first) && first != null, "profile missing");
        Expect(!catalog.TryGet(null, out var missingNull) && missingNull == null,
            "incomplete native item ID interrupted machine lookup");
        Expect(!catalog.TryGet("", out var missingEmpty) && missingEmpty == null,
            "empty native item ID interrupted machine lookup");
        Expect(!catalog.TryGet(" ", out var missingWhitespace) && missingWhitespace == null,
            "blank native item ID interrupted machine lookup");
        inputs.Clear(); auxiliary.Add("ore"); modules.Clear(); slots.Clear(); liquidInputs.Clear(); contents.Clear();
        Expect(first!.Definition.ProductionMarkupPercent == 25 &&
            first.Recipes[0].Value.ItemInputs.Count == 1 && first.Template.LiquidInputs.Count == 1 &&
            first.Template.Modules!.AllowedTypes.Count == 1 && first.Recipes[0].Value.LiquidInputs.Count == 1 &&
            ((ForgeMachineItemOutput)first.Recipes[0].Value.Output).Contents!.Count == 1,
            "caller mutated a nested template or recipe list");
        Expect(first.IsFeedstock("raw_material") && first.Accepts("auxiliary_material") && !first.Accepts("ore"), "admission index");
        Expect(MachineAdmission.InputIds(first.Recipes.Select(entry => entry.Value))
            .SequenceEqual(new[] { "auxiliary_material", "raw_material" }, StringComparer.Ordinal),
            "native item-input whitelist");
        var extra = new ForgeMachineRecipe("test.other.recipe", [new("iron", 1)], new ForgeMachineItemOutput("part"));
        Expect(catalog.RegisterAdditional("test.other", id, [extra]).Status == SubmitStatus.Accepted, "contributor rejected");
        Expect(catalog.TryGet(id, out var second) && second!.Recipes.Count == 2 && second.IsFeedstock("iron"), "new profile missing");
        Expect(first!.Definition.ResolveNightlyBatchCount == nightlyCount &&
            second!.Definition.ResolveNightlyBatchCount == nightlyCount,
            "freezing or adding recipes lost the machine owner's nightly limit");
        Expect(MachineAdmission.InputIds(second!.Recipes.Select(entry => entry.Value))
            .SequenceEqual(new[] { "auxiliary_material", "iron", "raw_material" }, StringComparer.Ordinal) &&
            MachineAdmission.InputIds(second.Recipes.Select(entry => entry.Value)).All(second.Accepts),
            "extended native item-input whitelist");
        Expect(first.Recipes.Count == 1 && !first.IsFeedstock("iron"), "in-flight profile changed");
        Expect(catalog.RegisterAdditional("test.other", id, [extra with { RecipeId = "test.other.new" }, extra]).Status ==
            SubmitStatus.Conflict && catalog.Snapshot(false).Single().RecipeCount == 2, "conflicting batch partially published");
        var stable = first.Definition;
        var optional = stable with
        {
            Template = stable.Template with { LiquidInputs = [new("water", new(1, 1))] },
            Recipes = [new(owner + ".optional", [new("raw_material", 1)], new ForgeMachineItemOutput("part"))]
        };
        Expect(MachineCatalog.TryFreezeDefinition(owner, optional, out var optionalFrozen) &&
            optionalFrozen.Template.LiquidInputs.Single().Label == "" &&
            optionalFrozen.Recipes.Single().LiquidInputs.Count == 0,
            "native-label optional liquid slot made an item-only recipe require liquid");
        Expect(catalog.Register(owner, stable, Stage).Status == SubmitStatus.Conflict && staged == 1,
            "duplicate invoked native registration");
        foreach (var bad in new[]
        {
            stable with { MachineId = "foreign.machine" }, stable with { Power = new(-1) },
            stable with { ProductionMarkupPercent = -1 },
            stable with { ProductionMarkupPercent = 1001 },
            stable with { Template = stable.Template with { ItemInput = new(0, 4) } },
            stable with { Template = stable.Template with { Output = new((ForgeMachineOutputKind)99, new(2, 2)) } },
            stable with { Template = stable.Template with { Output = new(ForgeMachineOutputKind.Container, new(2, 2)) } },
            stable with { Template = stable.Template with { Battery = null } },
            stable with { Template = stable.Template with { Modules = new(new(4, 4), []) } },
            stable with { Template = stable.Template with { LiquidInputs = [new("water", new(2, 2)), new("water", new(2, 2))] } }
        })
            Expect(catalog.Register(owner, bad with { MachineId = bad.MachineId == "foreign.machine" ? bad.MachineId : owner + ".bad" },
                Stage).Status == SubmitStatus.Invalid && staged == 1, "invalid template reached native registration");
        foreach (var bad in new[]
        {
            extra with { RecipeId = "foreign.recipe" }, extra with { ItemInputs = [] },
            extra with { ItemInputs = [new("iron", 0)] }, extra with { ItemInputs = [new("iron", 2) { WholeStack = true }] },
            extra with { Output = new ForgeMachineItemOutput("part", 257) },
            extra with { Output = new ForgeMachineContainerOutput([new("water", 100)]) },
            extra with { LiquidInputs = [new("missing", 100)] },
            extra with { LiquidInputs = [new("water", 0)] },
            extra with { LiquidInputs = [new("water", 100), new("water", 100)] },
            extra with { Output = new ForgeMachineItemOutput("part", Contents: [new("protein", int.MaxValue)]) },
            extra with { Output = new ForgeMachineItemOutput("part", Contents: [new("protein", 10), new("protein", 10)]) }
        })
            Expect(catalog.RegisterAdditional("test.other", id, [bad]).Status == SubmitStatus.Invalid, "invalid additional recipe accepted");
        Expect(catalog.RegisterAdditional("test.other", "missing", [extra]).Status == SubmitStatus.Invalid, "unknown target accepted");
        var category = extra with { RecipeId = "test.other.category", ItemInputs =
            [new("", 1, _ => true) { ItemTag = "MUSIC_CATEGORY" }] };
        Expect(catalog.RegisterAdditional("test.other", id, [category]).Status == SubmitStatus.Accepted &&
            catalog.TryGet(id, out var categoryProfile) && categoryProfile!.IsFeedstockTag(tag => tag == "MUSIC_CATEGORY") &&
            MachineAdmission.InputTags(categoryProfile.Recipes.Select(entry => entry.Value)).SequenceEqual(["MUSIC_CATEGORY"]),
            "category recipe missing from native tag admission");
        Expect(catalog.RegisterAdditional("test.other", id, [category with { RecipeId = "test.other.bad_category",
            ItemInputs = [new("specific", 1) { ItemTag = "MUSIC_CATEGORY" }] }]).Status == SubmitStatus.Invalid,
            "ambiguous category and item identifier accepted");
        var liquidRecipe = new ForgeMachineRecipe(owner + ".liquid", [], new ForgeMachineContainerOutput([new("water", 90)]))
            { LiquidInputs = [new("source", 100, "water")] };
        var liquidTemplate = new ForgeMachineTemplate(null, new(ForgeMachineOutputKind.Container, new(3, 3)))
            { LiquidInputs = [new("source", new(3, 3))], Battery = null, Modules = null, ManualSlot = false };
        Expect(catalog.Register(owner, new(owner + ".liquid", liquidTemplate, [liquidRecipe], new()), Stage).Status ==
            SubmitStatus.Accepted, "unpowered liquid-only/container-output machine rejected");
        var dynamicRecipe = liquidRecipe with
        {
            RecipeId = owner + ".dynamic", LiquidInputs = [new("source", 0, ResolveMillilitres: _ => 100)],
            Output = new ForgeMachineContainerOutput([new("water", 0, _ => 90)])
        };
        Expect(catalog.RegisterAdditional(owner, owner + ".liquid", [dynamicRecipe]).Status == SubmitStatus.Accepted,
            "resolved volumes rejected");
        Func<ForgeMachineBatchContext, IReadOnlyList<ForgeMachineLiquidPart>> composition =
            context => ForgeLiquidCompositionMath.ReplaceComponent(
                ForgeLiquidCompositionMath.Consumed(context.Liquids[0].Before, context.Liquids[0].After), "water", "protein");
        var convertedRecipe = dynamicRecipe with
        {
            RecipeId = owner + ".composition",
            Output = new ForgeMachineContainerOutput([]) { ResolveContents = composition }
        };
        Expect(catalog.RegisterAdditional(owner, owner + ".liquid", [convertedRecipe]).Status == SubmitStatus.Accepted &&
            catalog.TryGet(owner + ".liquid", out var compositionProfile) &&
            ((ForgeMachineContainerOutput)compositionProfile!.Recipes.Last().Value.Output).ResolveContents == composition,
            "composition resolver was rejected or lost during freezing");
        Expect(catalog.RegisterAdditional(owner, owner + ".liquid", [convertedRecipe with
            { RecipeId = owner + ".ambiguous", Output = new ForgeMachineContainerOutput([new("water", 10)])
                { ResolveContents = composition } }]).Status == SubmitStatus.Invalid,
            "static and resolved contents were both accepted");
        var mixedContainer = liquidRecipe with { RecipeId = owner + ".mixed_container", ItemInputs = [new("scrap", 2)] };
        Expect(catalog.Register(owner, new(owner + ".mixed_container", liquidTemplate with { ItemInput = new(6, 4) },
            [mixedContainer], new()), Stage).Status == SubmitStatus.Accepted,
            "simultaneous item/liquid inputs to installed container rejected");
        var externalInputs = new List<ForgeMachineIngredient> { new("raw_material", 2) };
        var externalRecipes = new List<ForgeMachineRecipe>
        { new(owner + ".external", externalInputs, new ForgeMachineItemOutput("part")) };
        var external = new ForgeMachineDefinition("existing_machine",
            new(new(6, 4), new(ForgeMachineOutputKind.Items, new(6, 4))), externalRecipes, new(8));
        Expect(catalog.Register(owner, external, Stage).Status == SubmitStatus.Invalid &&
            !catalog.TryGet("existing_machine", out _), "external batch rules claimed a machine ID");
        Expect(MachineCatalog.TryFreezeDefinition(owner, external, out var frozenBatch),
            "explicit generic batch definition rejected");
        externalInputs.Clear(); externalRecipes.Clear();
        Expect(frozenBatch.Recipes.Count == 1 && frozenBatch.Recipes[0].ItemInputs.Count == 1 &&
            !catalog.TryGet("existing_machine", out _), "caller changed frozen batch rules or batch was registered");
        Expect(!MachineCatalog.TryFreezeDefinition(owner, frozenBatch with { MachineId = "" }, out _),
            "missing batch machine identity accepted");
        Expect(!MachineCatalog.TryFreezeDefinition("other.owner", frozenBatch, out _),
            "foreign batch recipe ownership accepted");
        Expect(catalog.Register(owner, stable with { MachineId = owner + ".failed" },
            _ => new(SubmitStatus.Conflict, "native collision")).Status == SubmitStatus.Conflict &&
            !catalog.TryGet(owner + ".failed", out _), "native failure published a machine profile");
        Expect(catalog.Snapshot(true).All(view => view.RuntimeInstalled) &&
            catalog.Snapshot(false).All(view => !view.RuntimeInstalled), "template readiness snapshot incorrect");
        int enumerations = 0, tagReads = 0;
        IEnumerable<RegisteredMachineRecipe> RepeatedTags()
        {
            enumerations++;
            for (int index = 0; index < 400; index++)
                yield return new(owner, extra with { RecipeId = owner + ".tag_" + index,
                    ItemInputs = [new("", 1) { ItemTag = "MUSIC_CATEGORY" }] });
        }
        var indexed = new MachineProfile(owner, first.Definition, RepeatedTags());
        bool matchesTag = indexed.IsFeedstockTag(_ => { tagReads++; return false; });
        Console.WriteLine($"Machine profile work: sourceEnumerations={enumerations}; falseTagReads={tagReads}; recipes=400.");
        Expect(!matchesTag && enumerations == 1, "profile enumerated its source more than once");
        Expect(tagReads == 1, "drop routing repeated the same native tag read for every recipe");
        Expect(indexed.IsFeedstockTag(tag => tag == "MUSIC_CATEGORY"), "tag index changed admission");
        var guides = new MachineCatalog();
        int guideCallbacks = 0;
        var guideInputs = new List<ForgeMachineIngredient>
        {
            new("ore", 3, _ => { guideCallbacks++; return true; })
                { Guide = new(Requirement: new("高纯度", "High purity")) },
            new("", 1) { ItemTag = "TEST_TAG", WholeStack = true,
                Guide = new("tag_illustration", new("整叠原料", "Stacked input")) }
        };
        var guideRecipe = new ForgeMachineRecipe(owner + ".guide_recipe", guideInputs,
            new ForgeMachineItemOutput("product", 2, _ => { guideCallbacks++; return 7; })
            {
                ResolveItemId = _ => { guideCallbacks++; return "random_product"; },
                Guide = new(Name: new("随机产物", "Random product"))
            })
        {
            LiquidInputs = [new("water", 50, Condition: _ => { guideCallbacks++; return true; })
                { Guide = new("water_illustration", new("水", "Water")) }]
        };
        var guideMachine = new ForgeMachineDefinition(owner + ".guide_machine", first.Template, [guideRecipe], new())
        { Guide = new("custom_category", new("机器备注", "Machine note")),
            ItemOptions = new(Name: new("扩展机器", "Extension machine")) };
        Expect(guides.GuideSnapshot().Revision == 0 && guides.GuideSnapshot().Recipes.Count == 0,
            "empty handbook has fabricated entries");
        Expect(guides.Register(owner, guideMachine, _ => new(SubmitStatus.Accepted, "test")).Status == SubmitStatus.Accepted,
            "guide metadata registration failed");
        var guideBefore = guides.GuideSnapshot();
        var guideEntry = guideBefore.Recipes.Single();
        guideInputs.Clear();
        Expect(guideCallbacks == 0 && guideEntry.Inputs.Count == 3 && guideEntry.Inputs[0].Amount == 3 &&
            guideEntry.Output.Amount == 2, "handbook evaluated callbacks or lost frozen input quantities");
        Expect(guides.TryGet(guideMachine.MachineId, out var randomProfile) &&
            ((ForgeMachineItemOutput)randomProfile!.Recipes.Single().Value.Output).ResolveItemId ==
                ((ForgeMachineItemOutput)guideRecipe.Output).ResolveItemId &&
            guideEntry.Output.Name?.English == "Random product" && guideEntry.Output.ItemId == "product",
            "freezing lost the output resolver or the handbook rolled a random product");
        Expect(guideEntry.OwnerId == owner && guideEntry.CategoryId == "custom_category" &&
            guideEntry.MachineName!.Chinese == "扩展机器" && guideEntry.Note!.English == "Machine note",
            "handbook did not inherit its machine presentation");
        Expect(guideEntry.Inputs[0].Requirement!.Chinese == "高纯度" &&
            guideEntry.Inputs[1].ItemId == "tag_illustration" && guideEntry.Inputs[1].Quantity!.Chinese == "整叠" &&
            guideEntry.Inputs[2].Quantity!.English == "50 ml" && guideEntry.Output.Quantity!.English == "By recipe",
            "handbook claimed fixed quantities or discarded provider requirements");
        var contributed = extra with { Guide = new("synthesis", new("追加备注", "Contributor note")) };
        Expect(guides.RegisterAdditional("test.other", guideMachine.MachineId, [contributed]).Status == SubmitStatus.Accepted,
            "external handbook contribution rejected");
        var guideAfter = guides.GuideSnapshot();
        Expect(guideAfter.Revision > guideBefore.Revision && guideBefore.Recipes.Count == 1 && guideAfter.Recipes.Count == 2,
            "new recipes did not invalidate handbook cache or mutated an earlier snapshot");
        Expect(guideAfter.Recipes.Last().OwnerId == "test.other" && guideAfter.Recipes.Last().CategoryId == "synthesis" &&
            guideAfter.Recipes.Last().Note!.Chinese == "追加备注", "recipe presentation did not override machine defaults");
        var badGuide = contributed with { RecipeId = "test.other.bad_guide", Guide = new(" ") };
        Expect(guides.RegisterAdditional("test.other", guideMachine.MachineId,
            [contributed with { RecipeId = "test.other.valid_guide" }, badGuide]).Status == SubmitStatus.Invalid &&
            guides.GuideSnapshot().Revision == guideAfter.Revision && guides.GuideSnapshot().Recipes.Count == 2,
            "invalid metadata partially published recipes or invalidated cache");
        Expect(guides.RegisterAdditional("test.other", guideMachine.MachineId, [contributed]).Status == SubmitStatus.Conflict &&
            guides.GuideSnapshot().Revision == guideAfter.Revision, "duplicate submission invalidated handbook cache");
        var containerEntry = catalog.GuideSnapshot().Recipes.Single(entry => entry.RecipeId == owner + ".composition");
        Expect(containerEntry.Output.Amount == 0 && containerEntry.Output.Quantity!.Chinese == "依配方",
            "dynamic liquid composition was falsely displayed as fixed zero output");
        Expect(guideCallbacks == 0, "reading updated handbook executed gameplay rules");
        Console.WriteLine($"Machine template catalog checks passed: {checks} assertions.");
        MachineBatchChecks.Run();
    }
}

internal static class MachineBatchChecks
{
    internal static void Run()
    {
        int checks = 0;
        void Expect(bool value, string message)
        { checks++; if (!value) throw new Exception("Machine batch: " + message); }
        var gate = new MachineBatchGate();
        var firstMachine = new IntPtr(1);
        using (var outer = gate.TryEnter(firstMachine))
        {
            Expect(outer != null && gate.TryEnter(firstMachine) == null, "same-machine nested batch acquired a lease");
            using var independent = gate.TryEnter(new IntPtr(2));
            Expect(independent != null, "independent machine blocked by an active batch");
            int inputs = 2, nestedOutputs = 0;
            var failedOuter = MachineTransaction.Run([new("prepare", () =>
            {
                using var nested = gate.TryEnter(firstMachine);
                if (nested != null) { inputs--; nestedOutputs++; }
                throw new Exception("outer output preparation failed");
            }, () => { inputs = 2; return true; })]);
            Expect(!failedOuter.Committed && failedOuter.Restored && inputs == 2 && nestedOutputs == 0,
                "outer rollback restored the inputs of a committed nested batch");
        }
        var released = gate.TryEnter(firstMachine);
        Expect(released != null, "failed batch retained its lease");
        released!.Dispose(); released.Dispose();
        using (var reused = gate.TryEnter(firstMachine))
            Expect(reused != null, "repeated disposal prevented the next batch");

        // Independent nightly transactions must use the resources left by the
        // previous batch. Rejecting an extra batch preserves earlier production.
        foreach (var (start, expected, stop) in new[]
        {
            (new[] { 6, 300, 24, 3, 0 }, 3, "produced:test"),
            (new[] { 3, 300, 24, 3, 0 }, 1, "input-short-or-condition"),
            (new[] { 6, 150, 24, 3, 0 }, 1, "liquid-source-or-condition:water"),
            (new[] { 6, 300, 12, 3, 0 }, 1, "power-unavailable"),
            (new[] { 6, 300, 24, 1, 0 }, 1, "warehouse-full"),
            (new[] { 1, 300, 24, 3, 0 }, 0, "input-short-or-condition")
        })
        {
            var remaining = start.ToArray();
            int calls = 0;
            var cycle = MachineNightCycle.Run(() => 3, () =>
            {
                calls++;
                if (remaining[0] < 2) return "input-short-or-condition";
                if (remaining[1] < 100) return "liquid-source-or-condition:water";
                if (remaining[2] < 8) return "power-unavailable";
                if (remaining[3] < 1) return "warehouse-full";
                var before = remaining.ToArray();
                var result = MachineTransaction.Run([new("resources", () =>
                { remaining[0] -= 2; remaining[1] -= 100; remaining[2] -= 8; remaining[3]--; remaining[4]++; },
                    () => { remaining = before; return true; })]);
                return result.Committed ? "produced:test" : result.Status;
            });
            Expect(cycle.Completed == expected && cycle.Status == stop &&
                calls == (expected == 3 ? 3 : expected + 1) &&
                remaining.SequenceEqual(new[] { start[0] - 2 * expected, start[1] - 100 * expected,
                    start[2] - 8 * expected, start[3] - expected, expected }),
                "extra batch ignored live resources, retried rejection or lost earlier production");
        }
        int produced = 0, attempted = 0;
        var failedExtra = MachineNightCycle.Run(() => 3, () =>
        {
            attempted++;
            int before = produced;
            var result = MachineTransaction.Run([new("output", () =>
            { produced++; if (attempted == 2) throw new Exception("readback failed"); },
                () => { produced = before; return true; })]);
            return result.Committed ? "produced:test" : result.Status;
        });
        Expect(failedExtra.Completed == 1 && produced == 1 && attempted == 2 &&
            failedExtra.Status == "output:readback failed", "failed extra batch retried or undid an earlier committed batch");
        int liveLimit = 3, liveProduced = 0;
        var lowered = MachineNightCycle.Run(() => liveLimit, () =>
        { liveProduced++; liveLimit = 1; return "produced:test"; });
        Expect(lowered.Completed == 1 && liveProduced == 1 && lowered.Status == "nightly-limit-reached",
            "extra batch used stale performance after a module action");
        liveLimit = 1; liveProduced = 0;
        var increased = MachineNightCycle.Run(() => liveLimit, () =>
        { liveProduced++; liveLimit = 3; return "produced:test"; });
        Expect(increased.Completed == 1 && liveProduced == 1, "nightly work exceeded the initial limit");
        foreach (int invalid in new[] { 0, -1 })
        {
            int calls = 0;
            var rejected = MachineNightCycle.Run(() => invalid, () => { calls++; return "produced:test"; });
            Expect(rejected.Completed == 0 && rejected.Status == "nightly-count-invalid" && calls == 0,
                "invalid nightly limit started processing");
        }

        Expect(ForgeMachinePowerMath.CalculateCost(8, 3) == 8 && ForgeMachinePowerMath.CalculateCost(8, 3, true) == 24,
            "batch/per-output billing");
        Expect(ForgeMachinePowerMath.CalculateCost(0, 256, true) == 0, "zero cost rejected");
        foreach (var (cost, count) in new[] { (-1, 1), (1, 0), (1, 257) })
        {
            try { ForgeMachinePowerMath.CalculateCost(cost, count); throw new Exception("invalid power accepted"); }
            catch (ArgumentOutOfRangeException) { checks++; }
        }
        try { ForgeMachinePowerMath.CalculateCost(int.MaxValue, 2, true); throw new Exception("power overflow accepted"); }
        catch (OverflowException) { checks++; }
        var liquid = new ForgeMachineLiquidSnapshot(500_000, [new("water", 200_001), new("protein", 99_999)]);
        var mixed = MachineBatchMath.Consume(liquid, 100_000, null);
        Expect(mixed.TotalParts == 200_000 && mixed.Contents.Sum(part => part.Parts) == 200_000,
            "mixture rounding lost volume");
        var specific = MachineBatchMath.Consume(liquid, 50_000, "protein");
        Expect(specific.Contents[0].Parts == 200_001 && specific.Contents[1].Parts == 49_999,
            "component draw changed other liquids");
        Expect(MachineBatchMath.Add(specific, [new("protein", 50_000)]).Contents.SequenceEqual(liquid.Contents),
            "refill did not restore exact composition");
        Expect(ReferenceEquals(MachineBatchMath.Consume(liquid, 0, null), liquid), "zero water draw changed the source");
        var feed = new ForgeMachineLiquidSnapshot(2_000_000,
            [new("water", 700_000), new("protein", 200_000) { Value = 100m, QualityBasis = 80m },
             new("salt", 100_000) { Value = 8m }]);
        var feedAfter = MachineBatchMath.Consume(feed, 400_000, null);
        var drawn = ForgeLiquidCompositionMath.Consumed(feed, feedAfter);
        var conversion = ForgeLiquidCompositionMath.ReplaceComponent(drawn, "water", "protein");
        Expect(conversion.Sum(part => part.Parts) == 400_000 && conversion.All(part => part.LiquidId != "water") &&
            conversion.Single(part => part.LiquidId == "protein").Parts == 360_000 &&
            conversion.Single(part => part.LiquidId == "salt") == drawn.Single(part => part.LiquidId == "salt"),
            "component conversion discarded contaminants, lost volume or left source water");
        Expect(ForgeLiquidCompositionMath.BaseValue(conversion) == ForgeLiquidCompositionMath.BaseValue(drawn) &&
            conversion.Single(part => part.LiquidId == "protein").QualityBasis == 43.2m,
            "component conversion lost proportional component value or quality basis");
        var destination = new ForgeMachineLiquidSnapshot(1_000_000,
            [new("water", 200_000), new("protein", 0), new("salt", 0)]);
        var combined = MachineBatchMath.Add(destination, conversion);
        Expect(combined.TotalParts == 600_000 && combined.Contents[0].Parts == 200_000 &&
            combined.Contents[1].Parts == 360_000 && combined.Contents[2].Parts == 40_000,
            "converted mixture replaced existing output liquid instead of mixing");
        Expect(MachineBatchMath.BaseValue(combined) == 62.4m &&
            MachineBatchMath.BaseValue(MachineBatchMath.Consume(combined, 180_000, "protein")) == 36.8m,
            "mixed biomass/water price or partial biomass consumption did not follow component proportions");
        try { MachineBatchMath.Add(destination with { CapacityParts = 599_999 }, conversion);
            throw new Exception("converted mixture overflow accepted"); }
        catch (ArgumentOutOfRangeException) { checks++; }
        // A failed destination readback restores the entire mixed output and
        // source, including contaminant parts and their value ledger.
        var liveSource = feed; var liveDestination = destination;
        var failedConversion = MachineTransaction.Run([
            new("source", () => liveSource = feedAfter, () => { liveSource = feed; return true; }),
            new("destination", () => { liveDestination = combined; throw new Exception("readback failure"); },
                () => { liveDestination = destination; return true; })]);
        Expect(!failedConversion.Committed && failedConversion.Restored && liveSource == feed && liveDestination == destination,
            "conversion rollback failed to restore both original compositions");
        foreach (int parts in new[] { -1, 300_001 })
        {
            try { MachineBatchMath.Consume(liquid, parts, null); throw new Exception("invalid draw accepted"); }
            catch (ArgumentOutOfRangeException) { checks++; }
        }
        foreach (var invalid in new[] { new ForgeMachineLiquidPart("unknown", 1), new("water", 200_001), new("water", -1) })
        {
            try { MachineBatchMath.Add(liquid, [invalid]); throw new Exception("invalid fill accepted"); }
            catch (ArgumentOutOfRangeException) { checks++; }
        }
        // Test all rounding boundaries using uneven mixtures and tiny draws.
        for (int parts = 1; parts <= 97; parts++)
        {
            var before = new ForgeMachineLiquidSnapshot(100, [new("a", 23), new("b", 31), new("c", 43)]);
            var after = MachineBatchMath.Consume(before, parts, null);
            Expect(after.TotalParts == 97 - parts && after.Contents.All(part => part.Parts >= 0), "rounding boundary");
        }
        // Each write fails after partially mutating. Every resource must be
        // restored, and no later stage may execute after the failing write.
        for (int failure = 0; failure < 4; failure++)
        {
            var state = new[] { 2, 300, 100, 0 }; var before = state.ToArray(); var calls = new List<int>();
            var steps = Enumerable.Range(0, 4).Select(index => new MachineTransactionStep("stage" + index, () =>
            { state[index]++; calls.Add(index); if (index == failure) throw new Exception("readback mismatch"); }, () =>
            { state[index] = before[index]; return true; })).ToArray();
            var result = MachineTransaction.Run(steps);
            Expect(!result.Committed && result.Restored && state.SequenceEqual(before) && calls.Count == failure + 1,
                "partial-write failure leaked resources or continued processing");
        }
        var restoredOrder = new List<int>();
        var broken = MachineTransaction.Run([
            new("first", () => { }, () => { restoredOrder.Add(0); return true; }),
            new("second", () => throw new Exception("fail"), () => { restoredOrder.Add(1); throw new Exception("rollback fail"); })]);
        Expect(!broken.Committed && !broken.Restored && restoredOrder.SequenceEqual(new[] { 1, 0 }),
            "failed undo prevented remaining restores");
        restoredOrder.Clear();
        var badLog = MachineTransaction.Run([
            new("first", () => { }, () => { restoredOrder.Add(0); return true; }),
            new("second", () => throw new Exception("fail"), () => { restoredOrder.Add(1); return false; })],
            _ => throw new Exception("logger fail"));
        Expect(!badLog.Restored && restoredOrder.SequenceEqual(new[] { 1, 0 }), "logger prevented remaining restores");
        int applied = 0, rolledBack = 0;
        Expect(MachineTransaction.Run([new("success", () => applied++, () => { rolledBack++; return true; })]).Committed &&
            applied == 1 && rolledBack == 0, "successful transaction rolled back");
        Console.WriteLine($"Machine batch math and injected transaction failure checks passed: {checks} assertions.");
    }
}
