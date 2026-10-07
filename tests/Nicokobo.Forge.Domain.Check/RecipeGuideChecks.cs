using Nicokobo.Forge;
using Nicokobo.Forge.Registration;

internal static class RecipeGuideChecks
{
    internal static void Run()
    {
        int checks = 0;
        void Expect(bool value, string reason)
        {
            checks++;
            if (!value) throw new InvalidOperationException(reason);
        }
        const string owner = "test.recipes";
        var outputs = Enumerable.Range(0, 4).Select(index => new ForgeRecipeGuideEntry(owner,
            owner + ".output_" + index, "test.machine", new("机器", "Machine"), "synthesis",
            new[] { new ForgeRecipeGuideFigure("source", 2) },
            new("representative_output", 1), new("备注", "Note"))).ToArray();
        var usage = outputs[0] with { RecipeId = owner + ".use", CategoryId = "food_processing",
            Inputs = new[] { new ForgeRecipeGuideFigure("representative_input", 2) }, Output = new("other", 1) };
        var tagged = usage with { RecipeId = owner + ".tagged", CategoryId = "custom",
            Inputs = new[] { new ForgeRecipeGuideFigure("storage_bay", 1) } };
        var declarations = outputs.Select(entry => new ForgeMachineRecipe(entry.RecipeId,
            [new("source", 2, _ => throw new InvalidOperationException("Query ran a production predicate"))],
            new ForgeMachineItemOutput("product", 1, _ => throw new InvalidOperationException("Query resolved output"))))
            .Concat(new[] {
                new ForgeMachineRecipe(usage.RecipeId, [new("product", 2)], new ForgeMachineItemOutput("other", 1)),
                new ForgeMachineRecipe(tagged.RecipeId, [new("", 1, _ => throw new InvalidOperationException("Query ran a tag condition"))
                    { ItemTag = "CONTAINER_TAG" }], new ForgeMachineItemOutput("other", 1))
            }).ToArray();
        var snapshot = new ForgeRecipeGuideSnapshot(1, outputs.Append(usage).Append(tagged).ToArray());
        IReadOnlyList<RecipeGuidePage> Query(string? id, bool machine = false, bool english = false,
            bool storage = false, ForgeRecipeGuideSnapshot? source = null) => RecipeGuideQuery.Build(source ?? snapshot,
                id, machine, english, (item, _) => item, item => item == "test.machine" ? declarations : [],
                tag => storage && tag == "CONTAINER_TAG");
        var related = Query("product");
        Expect(related.Where(page => page.Subtitle.StartsWith("作为产物")).SelectMany(page => page.Recipes).Count() == 4,
            "query failed to match the real product behind a representative icon or paginate its recipes");
        Expect(related.Where(page => page.Subtitle.StartsWith("作为材料")).SelectMany(page => page.Recipes).Single().RecipeId == usage.RecipeId,
            "query failed to use actual input identity across handbook categories");
        Expect(related.SelectMany(page => page.Recipes).Count() == 5, "unrelated recipes leaked into item query");
        Expect(related.All(page => page.Note == "备注"), "provider notes were discarded");
        Expect(Query("a_storage", storage: true).SelectMany(page => page.Recipes).Single().RecipeId == tagged.RecipeId,
            "tagged ingredients or custom categories were omitted");
        Expect(Query("representative_input").Single().Recipes.Count == 0, "solid illustration was mistaken for a real ingredient");
        Expect(Query("representative_output").Single().Recipes.Count == 0, "output illustration was mistaken for the real product");
        Expect(string.Join("|", Query("product", english: true).SelectMany(page => page.Recipes).Select(recipe => recipe.RecipeId)) ==
            string.Join("|", related.SelectMany(page => page.Recipes).Select(recipe => recipe.RecipeId)), "query results changed with language");
        Expect(Query("test.machine", machine: true).SelectMany(page => page.Recipes).Count() == snapshot.Recipes.Count,
            "machine query omitted its registered processing recipes");
        Expect(Query(null).SelectMany(page => page.Recipes).Count() == snapshot.Recipes.Count, "no-target browser lost registered recipes");
        Expect(Query("missing").Single().Note == "当前没有已登记的相关配方。", "empty result did not explain the absence of recipes");

        var catalog = new RecipeGuideCatalog();
        var glass = new ForgeRecipeGuideEntry(owner, owner + ".glass", "furnace", new("熔炉", "Furnace"), "refining",
            [new("empty_beer_bottle", 1)], new("quartz", 1));
        var mutableInputs = new List<ForgeRecipeGuideFigure>(glass.Inputs);
        Expect(catalog.Register(owner, [glass with { Inputs = mutableInputs }]).Status == SubmitStatus.Accepted,
            "native-machine recipe presentation was rejected");
        mutableInputs.Clear();
        var glassSnapshot = catalog.Snapshot();
        Expect(Query("empty_beer_bottle", source: glassSnapshot).Single().Recipes.Single().RecipeId == glass.RecipeId &&
            Query("quartz", source: glassSnapshot).Single().Recipes.Single().RecipeId == glass.RecipeId,
            "native-machine input/output queries failed or retained mutable provider lists");
        Expect(catalog.Register("another.owner", [glass]).Status == SubmitStatus.Invalid &&
            catalog.Snapshot().Revision == glassSnapshot.Revision, "owner mismatch changed the recipe catalog");
        Expect(catalog.Register(owner, [glass, glass]).Status == SubmitStatus.Invalid &&
            catalog.Snapshot().Recipes.Count == 1, "duplicate declaration partially replaced native recipes");
        Expect(catalog.Register(owner, [glass with { Output = new("quartz", 2) }]).Status == SubmitStatus.Accepted &&
            catalog.Snapshot().Revision > glassSnapshot.Revision && glassSnapshot.Recipes[0].Output.Amount == 1,
            "provider refresh mutated an earlier snapshot or failed to invalidate its revision");
        Console.WriteLine($"Forge recipe-guide checks passed: {checks} assertions.");
    }
}
