using Il2Cpp;
using Nicokobo.Forge;
using Nicokobo.Forge.Registration;

// Only delegate identities are required here. No native objects or runtime
// behavior are simulated by registration checks.
namespace Il2Cpp
{
    public sealed class GameItem;
    public class GameInventory;
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
        var inputs = new List<ForgeMachineIngredient> { new("quartz", 2) };
        var auxiliary = new List<string> { "flux_agent" };
        var modules = new List<string> { "MODULE_TYPE_FURNACE" };
        var slots = new List<ForgeMachineContainerSlot> { new("water", new(3, 3)) };
        var liquidInputs = new List<ForgeMachineLiquidIngredient> { new("water", 100) };
        var contents = new List<ForgeMachineLiquidAmount> { new("protein", 100) };
        var recipe = new ForgeMachineRecipe(owner + ".recipe", inputs,
            new ForgeMachineItemOutput("canister", Contents: contents))
            { LiquidInputs = liquidInputs, AuxiliaryItemIds = auxiliary };
        var template = new ForgeMachineTemplate(new(6, 4), new(ForgeMachineOutputKind.Items, new(6, 4)))
            { LiquidInputs = slots, Modules = new(new(4, 4), modules) };
        var definition = new ForgeMachineDefinition(id, template, [recipe], new(8));
        Expect(!catalog.HasMachines, "empty catalog");
        Expect(catalog.Register(owner, definition, Stage).Status == SubmitStatus.Accepted && staged == 1,
            "mixed input registration rejected");
        Expect(catalog.TryGet(id, out var first) && first != null, "profile missing");
        inputs.Clear(); auxiliary.Add("ore"); modules.Clear(); slots.Clear(); liquidInputs.Clear(); contents.Clear();
        Expect(first!.Recipes[0].Value.ItemInputs.Count == 1 && first.Template.LiquidInputs.Count == 1 &&
            first.Template.Modules!.AllowedTypes.Count == 1 && first.Recipes[0].Value.LiquidInputs.Count == 1 &&
            ((ForgeMachineItemOutput)first.Recipes[0].Value.Output).Contents!.Count == 1,
            "caller mutated a nested template or recipe list");
        Expect(first.IsFeedstock("quartz") && first.Accepts("flux_agent") && !first.Accepts("ore"), "admission index");
        var extra = new ForgeMachineRecipe("test.other.recipe", [new("iron", 1)], new ForgeMachineItemOutput("part"));
        Expect(catalog.RegisterAdditional("test.other", id, [extra]).Status == SubmitStatus.Accepted, "contributor rejected");
        Expect(catalog.TryGet(id, out var second) && second!.Recipes.Count == 2 && second.IsFeedstock("iron"), "new profile missing");
        Expect(first.Recipes.Count == 1 && !first.IsFeedstock("iron"), "in-flight profile changed");
        Expect(catalog.RegisterAdditional("test.other", id, [extra with { RecipeId = "test.other.new" }, extra]).Status ==
            SubmitStatus.Conflict && catalog.Snapshot(false, false).Single().RecipeCount == 2, "conflicting batch partially published");
        var stable = first.Definition;
        Expect(catalog.Register(owner, stable, Stage).Status == SubmitStatus.Conflict && staged == 1,
            "duplicate invoked native registration");
        foreach (var bad in new[]
        {
            stable with { MachineId = "foreign.machine" }, stable with { Power = new(-1) },
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
        var mixedContainer = liquidRecipe with { RecipeId = owner + ".mixed_container", ItemInputs = [new("scrap", 2)] };
        Expect(catalog.Register(owner, new(owner + ".mixed_container", liquidTemplate with { ItemInput = new(6, 4) },
            [mixedContainer], new()), Stage).Status == SubmitStatus.Accepted,
            "simultaneous item/liquid inputs to installed container rejected");
        var simple = new ForgeMachineRecipe(owner + ".glass", [new("glass", 2)], new ForgeMachineItemOutput("quartz"));
        var native = new ForgeMachineDefinition("furnace",
            new(new(6, 4), new(ForgeMachineOutputKind.Items, new(6, 4))), [simple], new(8));
        Func<GameItem, GameInventory, bool> probe = (_, _) => false;
        Expect(catalog.RegisterNative(owner, native, probe).Status == SubmitStatus.Accepted, "native furnace rejected");
        Expect(catalog.TryGet("furnace", out var furnace) && ReferenceEquals(furnace!.NativeBatchProbe, probe), "native probe lost");
        Expect(catalog.RegisterNative(owner, native with { Recipes = [simple with { RecipeId = owner + ".glass2" }] }, null).Status ==
            SubmitStatus.Accepted && catalog.TryGet("furnace", out furnace) && ReferenceEquals(furnace!.NativeBatchProbe, probe),
            "later native recipe registration dropped probe");
        Expect(catalog.RegisterNative(owner, native with { MachineId = "other" }, probe).Status == SubmitStatus.Invalid,
            "unsupported native machine accepted");
        Expect(catalog.Register(owner, stable with { MachineId = owner + ".failed" },
            _ => new(SubmitStatus.Conflict, "native collision")).Status == SubmitStatus.Conflict &&
            !catalog.TryGet(owner + ".failed", out _), "native failure published a machine profile");
        Expect(catalog.Snapshot(true, false).Single(view => view.NativeMachine).RuntimeInstalled == false &&
            catalog.Snapshot(true, false).Where(view => !view.NativeMachine).All(view => view.RuntimeInstalled),
            "optional furnace hook failure disabled templates or claimed native availability");
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
        foreach (int parts in new[] { 0, -1, 300_001 })
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
