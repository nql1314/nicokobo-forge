using Nicokobo.Forge.Registration;

namespace Nicokobo.Forge;

/// <summary>Forge owns the G shortcut, item queries and recipe window.
/// Machine recipes are read automatically; native-machine extensions may
/// declare their existing recipes' presentation without adding production.</summary>
public static class ForgeRecipeGuideApi
{
    private static readonly RecipeGuideCatalog Catalog = new();
    public static bool IsVisible => RecipeGuideBrowser.Visible;
    public static void Close() => RecipeGuideBrowser.Close();
    public static SubmitResult RegisterRecipes(string ownerId, IReadOnlyList<ForgeRecipeGuideEntry> recipes) =>
        Catalog.Register(ownerId, recipes);
    public static ForgeRecipeGuideSnapshot Snapshot()
    {
        var machines = ForgeMachineRegistrationApi.GuideSnapshot();
        var additional = Catalog.Snapshot();
        return new(machines.Revision + additional.Revision,
            Array.AsReadOnly(machines.Recipes.Concat(additional.Recipes).ToArray()));
    }
}
