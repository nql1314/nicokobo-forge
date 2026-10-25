using Nicokobo.Forge;
using Nicokobo.Forge.Registration;

internal static class MachineRecipePriorityChecks
{
    internal static void Run(Action<bool, string> expect)
    {
        const string owner = "test.priority";
        var catalog = new MachineCatalog();
        var template = new ForgeMachineTemplate(new(9, 6), new(ForgeMachineOutputKind.Items, new(9, 6)));
        SubmitResult Stage(ForgeMachineDefinition _) => new(SubmitStatus.Accepted, "test");
        var basic = new ForgeMachineRecipe(owner + ".basic", [new("raw_a", 10), new("raw_b", 10), new("raw_c", 10)], new ForgeMachineItemOutput("basic"));
        var advanced = new ForgeMachineRecipe(owner + ".advanced", [new("upstream", 1)], new ForgeMachineItemOutput("advanced"));
        var final = new ForgeMachineRecipe(owner + ".final", [new("upstream_2", 1)], new ForgeMachineItemOutput("final"));
        var machine = new ForgeMachineDefinition(owner + ".machine", template, [basic, advanced, final], new());
        expect(catalog.Register(owner, machine, Stage).Status == SubmitStatus.Accepted, "priority target registration");
        catalog.TryGet(machine.MachineId, out var profile);
        string First(string? preferred = null) => catalog.ProcessingRecipes(profile!, preferred)[0].Value.RecipeId;
        expect(First() == basic.RecipeId, "same-depth priority must prefer more ingredient kinds");
        var upstream = new ForgeMachineRecipe(owner + ".upstream", [new("raw_a", 1)], new ForgeMachineItemOutput("upstream"));
        var source = new ForgeMachineDefinition(owner + ".source", template, [upstream], new());
        expect(catalog.Register(owner, source, Stage).Status == SubmitStatus.Accepted && First() == advanced.RecipeId,
            "registering an upstream machine must refresh cached depth and prioritize its downstream product");
        expect(First(basic.RecipeId) == advanced.RecipeId, "a preferred low-tier variant must not override a higher-tier product");
        var upstream2 = new ForgeMachineRecipe(owner + ".upstream_2", [new("upstream", 1)], new ForgeMachineItemOutput("upstream_2"));
        expect(catalog.RegisterAdditional(owner, source.MachineId, [upstream2]).Status == SubmitStatus.Accepted && First() == final.RecipeId,
            "additional upstream recipes must refresh downstream priority across machines");
        var privateProfile = new MachineProfile(owner, machine with { MachineId = owner + ".private" },
            new[] { basic, advanced, final }.Select(recipe => new RegisteredMachineRecipe(owner, recipe)));
        expect(catalog.ProcessingRecipes(privateProfile, null)[0].Value.RecipeId == final.RecipeId,
            "caller-owned batch processors must share registered upstream chain depth");
        expect(profile!.Recipes.Select(entry => entry.Value.RecipeId).SequenceEqual(new[] { basic.RecipeId, advanced.RecipeId, final.RecipeId }),
            "processing priority must preserve registered handbook order and frozen profiles");
        var one = new ForgeMachineRecipe(owner + ".one", [new("raw", 1)], new ForgeMachineItemOutput("result"));
        var two = one with { RecipeId = owner + ".two", ItemInputs = [new("raw", 2)] };
        var wet = one with { RecipeId = owner + ".wet", LiquidInputs = [new("water", 250)] };
        RegisteredMachineRecipe[] Entries(params ForgeMachineRecipe[] recipes) => recipes.Select(recipe => new RegisteredMachineRecipe(owner, recipe)).ToArray();
        var order = new MachineRecipeOrder([one, two, wet]);
        expect(order.Order(Entries(one, two))[0].Value == two, "same-kind recipes must prefer the larger required amount");
        expect(order.Order(Entries(two, wet))[0].Value == wet, "liquid requirements must contribute to recipe specificity");
        expect(order.Order(Entries(one, two), one.RecipeId)[0].Value == one, "same-tier variant preference must remain available");
        var cycleA = one with { RecipeId = owner + ".cycle_a", ItemInputs = [new("cycle_b", 1)], Output = new ForgeMachineItemOutput("cycle_a") };
        var cycleB = one with { RecipeId = owner + ".cycle_b", ItemInputs = [new("cycle_a", 1)], Output = new ForgeMachineItemOutput("cycle_b") };
        var cyclic = new MachineRecipeOrder([cycleA, cycleB]);
        expect(cyclic.Order(Entries(cycleA, cycleB)).Select(entry => entry.Value.RecipeId).SequenceEqual(new[] { cycleA.RecipeId, cycleB.RecipeId }),
            "reversible recipes must terminate and retain stable order when depth and specificity tie");
    }
}
