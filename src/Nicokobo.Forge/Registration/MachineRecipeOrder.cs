namespace Nicokobo.Forge.Registration;

/// <summary>Ranks declared production chains without evaluating content callbacks.
/// Reversible chains fall back to ingredient specificity instead of accumulating depth.</summary>
internal sealed class MachineRecipeOrder
{
    private readonly Dictionary<string, int> _itemDepths = new(StringComparer.Ordinal);

    internal MachineRecipeOrder(IEnumerable<ForgeMachineRecipe> recipes)
    {
        var producers = recipes.Where(recipe => recipe.Output is ForgeMachineItemOutput)
            .GroupBy(recipe => ((ForgeMachineItemOutput)recipe.Output).ItemId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        var dependencies = producers.ToDictionary(pair => pair.Key, pair => pair.Value
            .SelectMany(recipe => recipe.ItemInputs).Where(input => input.ItemTag == null && producers.ContainsKey(input.ItemId))
            .Select(input => input.ItemId).Distinct(StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);
        var dependents = producers.Keys.ToDictionary(id => id, _ => new List<string>(), StringComparer.Ordinal);
        foreach (var (output, inputs) in dependencies)
            foreach (string input in inputs) dependents[input].Add(output);
        var remaining = dependencies.ToDictionary(pair => pair.Key, pair => pair.Value.Length, StringComparer.Ordinal);
        var ready = new Queue<string>(remaining.Where(pair => pair.Value == 0).Select(pair => pair.Key));
        while (ready.TryDequeue(out string? output))
        {
            _itemDepths[output] = 1 + dependencies[output].Select(input => _itemDepths[input]).DefaultIfEmpty(0).Max();
            foreach (string dependent in dependents[output])
                if (--remaining[dependent] == 0) ready.Enqueue(dependent);
        }
    }

    internal IReadOnlyList<RegisteredMachineRecipe> Order(IEnumerable<RegisteredMachineRecipe> recipes,
        string? preferredRecipeId = null) => recipes
        .OrderByDescending(entry => Depth(entry.Value))
        .ThenByDescending(entry => entry.Value.RecipeId == preferredRecipeId)
        .ThenByDescending(entry => IngredientKinds(entry.Value))
        .ThenByDescending(entry => entry.Value.ItemInputs.Sum(input => (long)input.Count))
        .ThenByDescending(entry => entry.Value.LiquidInputs.Sum(input => (long)input.Millilitres))
        .ToArray();

    private int Depth(ForgeMachineRecipe recipe) => 1 + recipe.ItemInputs
        .Where(input => input.ItemTag == null).Select(input => _itemDepths.GetValueOrDefault(input.ItemId))
        .DefaultIfEmpty(0).Max();

    private static int IngredientKinds(ForgeMachineRecipe recipe) => recipe.ItemInputs
        .Select(input => input.ItemTag == null ? "item:" + input.ItemId : "tag:" + input.ItemTag)
        .Concat(recipe.LiquidInputs.Select(input => "liquid:" + input.SlotId + ":" + input.LiquidId))
        .Distinct(StringComparer.Ordinal).Count();
}
