namespace Nicokobo.Forge;

internal sealed record RecipeGuideFigure(string ItemId, string Caption, long Amount,
    string Requirement = "", string QuantityText = "");
internal sealed record RecipeGuideDiagram(string RecipeId, string MachineId, string MachineCaption,
    IReadOnlyList<RecipeGuideFigure> Inputs, RecipeGuideFigure Output)
{
    internal int InputsPerRow { get; init; } = 3;
    internal int Rows => Math.Max(1, (Inputs.Count + InputsPerRow - 1) / InputsPerRow);
}
internal sealed record RecipeGuidePage(string Title, string Subtitle,
    IReadOnlyList<RecipeGuideDiagram> Recipes, string Note);

// Queries declarations only; production conditions and dynamic output resolvers
// must never run while a player reads a recipe.
internal static class RecipeGuideQuery
{
    internal static IReadOnlyList<RecipeGuidePage> Build(ForgeRecipeGuideSnapshot snapshot,
        string? itemId, bool isMachine, bool english, Func<string, bool, string> resolveName,
        Func<string, IReadOnlyList<ForgeMachineRecipe>> recipesForMachine, Func<string, bool> hasTag)
    {
        var declarations = snapshot.Recipes.Select(entry => entry.MachineId).Distinct(StringComparer.Ordinal)
            .SelectMany(machineId => recipesForMachine(machineId).Select(recipe => (machineId, recipe)))
            .ToDictionary(entry => (entry.machineId, entry.recipe.RecipeId), entry => entry.recipe);
        bool Matches(ForgeRecipeGuideEntry entry, bool output)
        {
            if (!declarations.TryGetValue((entry.MachineId, entry.RecipeId), out var recipe))
                return output ? entry.Output.ItemId == itemId : entry.Inputs.Any(input => input.ItemId == itemId);
            if (output) return recipe.Output is ForgeMachineItemOutput product
                ? product.ItemId == itemId : entry.Output.ItemId == itemId;
            return recipe.ItemInputs.Any(input => input.ItemId == itemId ||
                (!string.IsNullOrEmpty(input.ItemTag) && hasTag(input.ItemTag))) ||
                recipe.LiquidInputs.Any(input => input.Guide?.IllustrationItemId == itemId);
        }
        var pages = new List<RecipeGuidePage>();
        if (itemId == null) Add(snapshot.Recipes, english ? "Recipes · one batch" : "合成配方 · 单批用量");
        else
        {
            Add(snapshot.Recipes.Where(entry => Matches(entry, true)), english ? "As output · one batch" : "作为产物 · 单批用量");
            Add(snapshot.Recipes.Where(entry => Matches(entry, false)), english ? "As ingredient · one batch" : "作为材料 · 单批用量");
            if (isMachine) Add(snapshot.Recipes.Where(entry => entry.MachineId == itemId),
                english ? "Machine recipes · one batch" : "机器加工配方 · 单批用量");
        }
        if (pages.Count == 0) pages.Add(new(itemId == null ? (english ? "Recipe Browser" : "合成表") : resolveName(itemId, english),
            english ? "Related recipes" : "相关配方", Array.Empty<RecipeGuideDiagram>(),
            english ? "No related recipes are registered." : "当前没有已登记的相关配方。"));
        return pages.AsReadOnly();

        void Add(IEnumerable<ForgeRecipeGuideEntry> entries, string subtitle)
        {
            foreach (var group in entries.GroupBy(entry => (entry.MachineId, Note: entry.Note?.For(english) ?? ""))
                         .OrderBy(group => group.Key.MachineId, StringComparer.Ordinal))
            {
                string machineName = group.First().MachineName?.For(english) ?? resolveName(group.Key.MachineId, english);
                RecipeGuideFigure Figure(ForgeRecipeGuideFigure figure) => new(figure.ItemId,
                    figure.Name?.For(english) ?? resolveName(figure.ItemId, english), figure.Amount,
                    figure.Requirement?.For(english) ?? "", figure.Quantity?.For(english) ?? "");
                var chunk = new List<RecipeGuideDiagram>();
                int rows = 0;
                foreach (var entry in group)
                {
                    var diagram = new RecipeGuideDiagram(entry.RecipeId, entry.MachineId, machineName,
                        entry.Inputs.Select(Figure).ToArray(), Figure(entry.Output))
                    {
                        InputsPerRow = entry.Inputs.Any(input =>
                            (input.Name?.English ?? resolveName(input.ItemId, true)).Length > 22 ||
                            (input.Name?.Chinese ?? resolveName(input.ItemId, false)).Length > 8) ? 2 : 3
                    };
                    if (chunk.Count != 0 && rows + diagram.Rows > 3) Flush();
                    chunk.Add(diagram);
                    rows += diagram.Rows;
                }
                Flush();
                void Flush()
                {
                    if (chunk.Count == 0) return;
                    pages.Add(new(machineName, subtitle, chunk.ToArray(), group.Key.Note));
                    chunk.Clear();
                    rows = 0;
                }
            }
        }
    }
}
