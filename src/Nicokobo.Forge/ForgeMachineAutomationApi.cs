using Il2Cpp;
using Nicokobo.Forge.Runtime;

namespace Nicokobo.Forge;

/// <summary>One native processing opportunity. Moving inputs here does not
/// process a batch. Selection applies only to this opportunity and machine.</summary>
public sealed class ForgeMachineAutomationContext
{
    internal ForgeMachineAutomationContext(GameItem machine, ForgeMachineInventory inventory)
    { Machine = machine; Inventory = inventory; }
    public GameItem Machine { get; }
    public ForgeMachineInventory Inventory { get; }
    public string? RecipeId { get; private set; }
    public bool Blocked { get; private set; }
    public string? Result { get; internal set; }
    public void SelectRecipe(string recipeId)
    {
        if (string.IsNullOrWhiteSpace(recipeId)) throw new ArgumentException("Recipe ID required");
        if (RecipeId != null && RecipeId != recipeId) Blocked = true;
        RecipeId = recipeId;
    }
    public void Block() => Blocked = true;
}

public enum ForgeMachineAutomationPhase { BeforeBatch, AfterBatch }

public sealed record ForgeRegisteredMachineRecipe(string OwnerId, ForgeMachineRecipe Recipe);

/// <summary>Public recipe observation and per-batch automation boundaries.
/// Does not schedule additional production or modify the machine recipe order.</summary>
public static class ForgeMachineAutomationApi
{
    private static readonly OwnedCallbacks<ForgeMachineAutomationPhase, ForgeMachineAutomationContext> Callbacks = new();
    public static IReadOnlyList<ForgeMachineRecipe> Recipes(string machineId) =>
        ForgeMachineRegistrationApi.TryGet(machineId, out var profile) && profile != null
            ? Array.AsReadOnly(profile.Recipes.Select(entry => entry.Value).ToArray()) : Array.Empty<ForgeMachineRecipe>();
    /// <summary>Includes provider identity. Content mods register their own
    /// recipes through ForgeMachineRegistrationApi.RegisterAdditionalRecipes;
    /// consumers do not need a reference to those content assemblies.</summary>
    public static IReadOnlyList<ForgeRegisteredMachineRecipe> RegisteredRecipes(string machineId) =>
        ForgeMachineRegistrationApi.TryGet(machineId, out var profile) && profile != null
            ? Array.AsReadOnly(profile.Recipes.Select(entry => new ForgeRegisteredMachineRecipe(entry.OwnerId, entry.Value)).ToArray())
            : Array.Empty<ForgeRegisteredMachineRecipe>();
    public static IDisposable Subscribe(string ownerId, string callbackId, ForgeMachineAutomationPhase phase,
        Action<ForgeMachineAutomationContext> callback, int order = 0)
    {
        if (!ForgeMachineRuntimeApi.RuntimeInstalled) throw new InvalidOperationException("Machine runtime unavailable");
        if (!Enum.IsDefined(typeof(ForgeMachineAutomationPhase), phase)) throw new ArgumentException("Invalid phase");
        ArgumentNullException.ThrowIfNull(callback);
        return Callbacks.Add(ownerId, callbackId, phase, context =>
        {
            try { callback(context); }
            catch { context.Block(); throw; }
        }, order);
    }
    internal static void Dispatch(ForgeMachineAutomationPhase phase, ForgeMachineAutomationContext context) =>
        Callbacks.Dispatch(phase, context, ForgeMachineRegistrationApi.Log);
}
