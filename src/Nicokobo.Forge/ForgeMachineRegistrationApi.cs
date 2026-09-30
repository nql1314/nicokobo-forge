using Il2Cpp;
using Nicokobo.Forge.Registration;

namespace Nicokobo.Forge;

/// <summary>Forge owns native slots and resource transactions. Content mods
/// supply presentation, recipes and read-only numerical rules.</summary>
public static class ForgeMachineRegistrationApi
{
    private static readonly MachineCatalog Catalog = new();
    private static Action<string>? _log;
    public static IReadOnlyList<ForgeMachineView> Snapshot() =>
        Catalog.Snapshot(ForgeMachineRuntimeApi.RuntimeInstalled, ForgeMachineHooks.NativeFurnaceInstalled);

    public static SubmitResult RegisterMachine(string ownerId, ForgeMachineDefinition definition)
    {
        var result = Catalog.Register(ownerId, definition, frozen => NativeItemRegistry.RegisterAmenity(
            ownerId, frozen.MachineId, () => ForgeMachineUi.Create(frozen), frozen.ItemOptions));
        Log($"[INFO] [NicokoboForge/Machine] owner={ownerId}; machine={definition?.MachineId}; status={result.Status}");
        return result;
    }
    public static SubmitResult RegisterAdditionalRecipes(string ownerId, string machineId,
        IReadOnlyList<ForgeMachineRecipe> recipes) => Catalog.RegisterAdditional(ownerId, machineId, recipes);

    /// <summary>Optional vanilla furnace extension. A true or failing probe
    /// preserves native processing. Only this API installs furnace hooks.</summary>
    public static SubmitResult RegisterExistingMachine(string ownerId, string machineId,
        IReadOnlyList<ForgeMachineRecipe> recipes, ForgeMachinePowerRule power,
        Func<GameItem, GameInventory, bool>? nativeBatchProbe = null)
    {
        var template = new ForgeMachineTemplate(new(6, 4), new(ForgeMachineOutputKind.Items, new(6, 4)));
        var result = Catalog.RegisterNative(ownerId, new(machineId, template, recipes, power), nativeBatchProbe);
        if (result.Status == SubmitStatus.Accepted) ForgeMachineHooks.InstallNativeFurnace();
        return result;
    }
    internal static bool HasMachines => Catalog.HasMachines;
    internal static bool TryGet(string machineId, out MachineProfile? profile) => Catalog.TryGet(machineId, out profile);
    internal static void Log(string message) => _log?.Invoke(message);
    internal static bool Install(bool knownBuild, Action<string> log)
    { _log = log; return ForgeMachineHooks.Install(knownBuild, log); }
}
