using Nicokobo.Forge.Registration;

namespace Nicokobo.Forge;

/// <summary>Forge owns native slots and resource transactions. Content mods
/// supply presentation, recipes and read-only numerical rules.</summary>
public static class ForgeMachineRegistrationApi
{
    private static readonly MachineCatalog Catalog = new();
    private static Action<string>? _log;
    public static IReadOnlyList<ForgeMachineView> Snapshot() =>
        Catalog.Snapshot(ForgeMachineRuntimeApi.RuntimeInstalled);
    /// <summary>Includes all accepted machine recipes and their providers.
    /// Consumers filter by the open CategoryId string.</summary>
    public static ForgeRecipeGuideSnapshot GuideSnapshot() => Catalog.GuideSnapshot();

    public static SubmitResult RegisterMachine(string ownerId, ForgeMachineDefinition definition)
    {
        var result = Catalog.Register(ownerId, definition, frozen => NativeItemRegistry.RegisterAmenity(
            ownerId, frozen.MachineId, () => ForgeMachineUi.Create(frozen), frozen.ItemOptions));
        Log($"[INFO] [NicokoboForge/Machine] owner={ownerId}; machine={definition?.MachineId}; status={result.Status}");
        return result;
    }
    public static SubmitResult RegisterAdditionalRecipes(string ownerId, string machineId,
        IReadOnlyList<ForgeMachineRecipe> recipes) => Catalog.RegisterAdditional(ownerId, machineId, recipes);

    /// <summary>Configure the shared terminal's rate before recipe staging.
    /// The first provider claims this setting; other providers cannot overwrite it.
    /// All terminal recipes, including other mods' contributions, inherit the rate.</summary>
    public static SubmitResult ConfigureManufacturingTerminalMarkup(string ownerId, int percent) =>
        Catalog.ConfigureManufacturingTerminalMarkup(ownerId, percent);

    internal static bool HasMachines => Catalog.HasMachines;
    internal static bool TryGet(string machineId, out MachineProfile? profile) => Catalog.TryGet(machineId, out profile);
    internal static void Log(string message) => _log?.Invoke(message);
    internal static bool Install(bool knownBuild, Action<string> log)
    { _log = log; return ForgeMachineHooks.Install(knownBuild, log); }
}
