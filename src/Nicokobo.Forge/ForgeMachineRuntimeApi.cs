using Il2Cpp;
using Nicokobo.Forge.Registration;

namespace Nicokobo.Forge;

/// <summary>Read live machine slots and adapter readiness. Registration and
/// resource arithmetic use their own APIs.</summary>
public static class ForgeMachineRuntimeApi
{
    public static bool RuntimeInstalled => ForgeMachineHooks.Installed;
    public static bool TryGetInventory(GameItem machine, out ForgeMachineInventory? inventory) => ForgeMachineUi.TryGet(machine, out inventory);
    /// <summary>Runs one actual registered batch against the machine's native
    /// slots, using the same catalog, hooks, participants and rollback path as
    /// an activation. Does not create private copies of registered recipes.</summary>
    public static string ProcessBatch(GameItem machine) => machine != null &&
        ForgeMachineRegistrationApi.TryGet(machine.identifier, out var profile) && profile != null
        ? ForgeMachineRuntime.ProcessActivated(machine, profile) : "machine-unregistered";

    /// <summary>Freeze rules for explicit resource batches on caller-owned slots.
    /// This does not register an item, schedule production or install hooks.
    /// The caller owns slot discovery, admission and native behavior.</summary>
    public static ForgeMachineBatchProcessor CreateBatchProcessor(string ownerId, ForgeMachineDefinition definition)
    {
        if (definition == null || !MachineCatalog.TryFreezeDefinition(ownerId, definition, out var frozen))
            throw new ArgumentException("Invalid batch template or recipe", nameof(definition));
        return new(new(ownerId, frozen,
            frozen.Recipes.Select(recipe => new RegisteredMachineRecipe(ownerId, recipe))));
    }
}
