using Nicokobo.Forge.Registration;

namespace Nicokobo.Forge;

/// <summary>Presentation only. Counts come from the real recipe; providers
/// describe predicates and dynamic quantities without executing callbacks.</summary>
public sealed record ForgeRecipeGuideDisplay(string? IllustrationItemId = null,
    LocalizedItemText? Name = null, LocalizedItemText? Requirement = null,
    LocalizedItemText? Quantity = null);

/// <summary>Recipes inherit their machine's category and note unless overridden.
/// Category IDs are open strings; consumers may use their own categories.</summary>
public sealed record ForgeRecipeGuideOptions(string CategoryId = "synthesis",
    LocalizedItemText? Note = null);

public sealed record ForgeRecipeGuideFigure(string ItemId, long Amount,
    LocalizedItemText? Name = null, LocalizedItemText? Requirement = null,
    LocalizedItemText? Quantity = null);
public sealed record ForgeRecipeGuideEntry(string OwnerId, string RecipeId,
    string MachineId, LocalizedItemText? MachineName, string CategoryId,
    IReadOnlyList<ForgeRecipeGuideFigure> Inputs, ForgeRecipeGuideFigure Output,
    LocalizedItemText? Note = null);
/// <summary>A detached view of accepted declarations, not proof of native application.</summary>
public sealed record ForgeRecipeGuideSnapshot(long Revision,
    IReadOnlyList<ForgeRecipeGuideEntry> Recipes);
