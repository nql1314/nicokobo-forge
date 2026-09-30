using Il2Cpp;
using Nicokobo.Forge.Registration;

namespace Nicokobo.Forge;

/// <summary>Machine declarations. Content mods own factories, recipes and
/// numerical rules; native installation and processing stay in adapters.</summary>
public static class ForgeMachineApi
{
    private static readonly MachineCatalog Catalog = new();
    private static Action<string>? _log;

    public static bool RuntimeInstalled => ForgeMachineHooks.Installed;
    public static IReadOnlyList<ForgeMachineView> Snapshot() => Catalog.Snapshot(RuntimeInstalled);

    /// <summary>Add recipes to the native furnace. The optional probe answers
    /// whether the game's own furnace batch can run from the current input;
    /// while it can, these recipes stay idle and the native cycle keeps the day.
    /// A missing probe keeps the earlier feedstock-only rule.</summary>
    public static SubmitResult RegisterExistingMachine(string ownerId,
        string machineId, IReadOnlyList<ForgeMachineRecipe> recipes,
        Func<GameItem, GameInventory, bool>? nativeBatchProbe = null) =>
        Register(ownerId, machineId, null, null, recipes,
            ForgeMachineProcessMode.NightlyItems, true, nativeBatchProbe);

    /// <summary>Stage a new machine item and recipes together.</summary>
    public static SubmitResult RegisterMachine(string ownerId, string machineId,
        Func<GameItem> factory, NativeItemOptions? itemOptions,
        IReadOnlyList<ForgeMachineRecipe> recipes,
        ForgeMachineProcessMode mode = ForgeMachineProcessMode.NightlyItems) =>
        Register(ownerId, machineId, factory, itemOptions, recipes, mode, false);

    /// <summary>Add a content mod's recipes after the target custom machine
    /// is registered. OnLateInitializeMelon runs after all initial declarations.</summary>
    public static SubmitResult RegisterAdditionalItemRecipes(string ownerId,
        string machineId, IReadOnlyList<ForgeMachineRecipe>? recipes)
    {
        var result = Catalog.RegisterAdditional(ownerId, machineId, recipes);
        Log($"[INFO] [NicokoboForge/Machine] contributor={ownerId}; " +
            $"machine={machineId}; additionalRecipes={recipes?.Count ?? 0}; status={result.Status}");
        return result;
    }

    private static SubmitResult Register(string ownerId, string machineId,
        Func<GameItem>? factory, NativeItemOptions? options,
        IReadOnlyList<ForgeMachineRecipe>? recipes, ForgeMachineProcessMode mode,
        bool nativeMachine, Func<GameItem, GameInventory, bool>? nativeBatchProbe = null)
    {
        var result = Catalog.Register(ownerId, machineId, recipes, mode, nativeMachine,
            nativeBatchProbe,
            nativeMachine || factory == null ? null : () => ForgeNativeApi.RegisterAmenity(
                ownerId, machineId, () => ForgeMachineHooks.CreateMachine(factory, mode), options));
        Log($"[INFO] [NicokoboForge/Machine] owner={ownerId}; machine={machineId}; " +
            $"recipes={recipes?.Count ?? 0}; mode={mode}; nativeBatchProbe=" +
            $"{(nativeBatchProbe == null ? "none" : "declared")}; " +
            $"runtime={RuntimeInstalled}; status={result.Status}");
        return result;
    }

    internal static bool HasMachines => Catalog.HasMachines;
    internal static bool TryGet(string machineId, out MachineProfile? profile) =>
        Catalog.TryGet(machineId, out profile);
    internal static void Log(string message) => _log?.Invoke(message);

    internal static bool Install(bool knownBuild, Action<string> log)
    {
        _log = log;
        return ForgeMachineHooks.Install(knownBuild, log);
    }
}
