namespace Nicokobo.Forge.Registration;

internal static class RecipeGuidePresentation
{
    private static readonly LocalizedItemText DynamicQuantity = new("依配方", "By recipe");
    private static readonly LocalizedItemText PredicateRequirement = new("需满足配方条件", "Recipe conditions apply");

    internal static bool Valid(ForgeRecipeGuideOptions? value) => value == null ||
        (!string.IsNullOrWhiteSpace(value.CategoryId) && value.CategoryId == value.CategoryId.Trim() && Text(value.Note));
    internal static bool Valid(ForgeRecipeGuideDisplay? value) => value == null ||
        ((value.IllustrationItemId == null || !string.IsNullOrWhiteSpace(value.IllustrationItemId)) &&
         Text(value.Name) && Text(value.Requirement) && Text(value.Quantity));
    private static bool Text(LocalizedItemText? value) => value == null ||
        (value.Chinese != null && value.English != null);

    internal static ForgeRecipeGuideEntry Build(MachineProfile machine, RegisteredMachineRecipe registered)
    {
        var recipe = registered.Value;
        var options = recipe.Guide ?? machine.Definition.Guide;
        var inputs = recipe.ItemInputs.Select(input => Figure(input.ItemId, input.Count, input.Guide,
                input.ItemTag == null ? null : new(input.ItemTag, input.ItemTag),
                input.Condition == null ? null : PredicateRequirement,
                input.WholeStack ? new("整叠", "Whole stack") : null))
            .Concat(recipe.LiquidInputs.Select(input => Figure(input.LiquidId ?? "", input.Millilitres, input.Guide,
                input.LiquidId == null ? new("液体", "Liquid") : null,
                input.Condition == null ? null : PredicateRequirement,
                input.ResolveMillilitres == null ? new(input.Millilitres + " ml", input.Millilitres + " ml") : DynamicQuantity)))
            .ToArray();
        ForgeRecipeGuideFigure output = recipe.Output switch
        {
            ForgeMachineItemOutput item => Figure(item.ItemId, item.Count, item.Guide,
                quantity: item.ResolveCount == null ? null : DynamicQuantity),
            ForgeMachineContainerOutput container => ContainerFigure(container),
            _ => throw new InvalidOperationException("Unknown registered output")
        };
        return new(registered.OwnerId, recipe.RecipeId, machine.MachineId, machine.Definition.ItemOptions?.Name,
            options.CategoryId, Array.AsReadOnly(inputs), output,
            recipe.Guide?.Note ?? machine.Definition.Guide.Note);
    }

    private static ForgeRecipeGuideFigure ContainerFigure(ForgeMachineContainerOutput output)
    {
        long volume = output.Contents.Sum(part => (long)part.Millilitres);
        var quantity = output.ResolveContents != null || output.Contents.Any(part => part.ResolveMillilitres != null)
            ? DynamicQuantity : new(volume + " ml", volume + " ml");
        return Figure("", volume, output.Guide, new("容器内液体", "Container contents"), quantity: quantity);
    }

    private static ForgeRecipeGuideFigure Figure(string id, long amount, ForgeRecipeGuideDisplay? display,
        LocalizedItemText? name = null, LocalizedItemText? requirement = null, LocalizedItemText? quantity = null) =>
        new(display?.IllustrationItemId ?? id, amount, display?.Name ?? name,
            display?.Requirement ?? requirement, display?.Quantity ?? quantity);
}
