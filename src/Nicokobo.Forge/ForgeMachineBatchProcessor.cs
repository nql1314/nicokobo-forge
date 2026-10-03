using Il2Cpp;
using Nicokobo.Forge.Registration;

namespace Nicokobo.Forge;

/// <summary>A frozen, reusable resource transaction. The content mod decides
/// when to call it and supplies live inventories belonging to the machine.</summary>
public sealed class ForgeMachineBatchProcessor
{
    private readonly MachineProfile _profile;
    internal ForgeMachineBatchProcessor(MachineProfile profile) => _profile = profile;

    /// <summary>Attempt at most one recipe, with resource readback and rollback.
    /// Lifecycle deduplication and native cycle ownership belong to the caller.</summary>
    public string Process(GameItem machine, ForgeMachineInventory inventory) =>
        ForgeMachineRuntime.ProcessBatch(machine, _profile, inventory);
}
