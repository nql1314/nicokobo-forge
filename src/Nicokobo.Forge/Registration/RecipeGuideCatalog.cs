namespace Nicokobo.Forge.Registration;

// Presentation for recipes processed outside Forge's machine transaction.
// Their content owner remains responsible for production and its real rules.
internal sealed class RecipeGuideCatalog
{
    private readonly Dictionary<string, IReadOnlyList<ForgeRecipeGuideEntry>> _owners = new(StringComparer.Ordinal);
    private long _revision;

    internal SubmitResult Register(string ownerId, IReadOnlyList<ForgeRecipeGuideEntry>? entries)
    {
        if (string.IsNullOrWhiteSpace(ownerId) || entries == null || entries.Any(entry => entry == null ||
            entry.OwnerId != ownerId || string.IsNullOrWhiteSpace(entry.RecipeId) ||
            !entry.RecipeId.StartsWith(ownerId + ".", StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(entry.MachineId) || string.IsNullOrWhiteSpace(entry.CategoryId) ||
            entry.Inputs == null || entry.Inputs.Any(figure => !Valid(figure)) || !Valid(entry.Output)) ||
            entries.Select(entry => entry.RecipeId).Distinct(StringComparer.Ordinal).Count() != entries.Count)
            return new(SubmitStatus.Invalid, "Invalid owned recipe-guide declaration");
        var frozen = Array.AsReadOnly(entries.Select(entry => entry with
            { Inputs = Array.AsReadOnly(entry.Inputs.ToArray()) }).ToArray());
        _owners[ownerId] = frozen;
        _revision++;
        return new(SubmitStatus.Accepted, "Recipe-guide declarations registered");
    }
    internal ForgeRecipeGuideSnapshot Snapshot() => new(_revision,
        Array.AsReadOnly(_owners.OrderBy(owner => owner.Key, StringComparer.Ordinal).SelectMany(owner => owner.Value).ToArray()));
    private static bool Valid(ForgeRecipeGuideFigure? figure) => figure != null &&
        !string.IsNullOrWhiteSpace(figure.ItemId) && figure.Amount > 0;
}
