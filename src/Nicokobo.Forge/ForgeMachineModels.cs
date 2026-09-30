using Il2Cpp;

namespace Nicokobo.Forge;

public enum ForgeMachineProcessMode
{
    NightlyItems,
    NightlyLiquid
}

public sealed record ForgeMachineIngredient(string ItemId, int Count,
    Func<GameItem, bool>? Condition = null);

public sealed record ForgeMachineWaterRequirement(int MillilitresPerOutput,
    int MinimumQuality);

public sealed record ForgeMachineRecipe(string RecipeId,
    IReadOnlyList<ForgeMachineIngredient> Inputs, string OutputItemId,
    int OutputCount, int PowerCost,
    Func<GameItem, int>? ResolveOutputCount = null,
    Func<GameItem, int>? ResolvePowerCost = null,
    ForgeMachineWaterRequirement? Water = null,
    Func<GameItem, GameItem, int>? ResolveLifetimeOutputCount = null,
    int MaxOutputsPerNight = 16)
{
    public Action<GameItem, GameInventory, IReadOnlyList<GameItem>, GameItem>?
        PrepareOutput { get; init; }
    public IReadOnlyList<string>? AuxiliaryItemIds { get; init; }
    /// <summary>Liquid recipes only. Accept a merged stack as the single batch
    /// input instead of rejecting it: the whole stack stays in the slot until
    /// the target count completes and is then consumed. The owner's lifetime
    /// callback prices the stack, so it can use the merged value.</summary>
    public bool AcceptsStackedInput { get; init; }
    /// <summary>Liquid recipes only. Units the finished batch takes from the
    /// retained stack; null consumes the whole item. Leftover units stay in
    /// the slot and can feed a later batch.</summary>
    public Func<GameItem, int, int>? ResolveConsumedUnits { get; init; }
    /// <summary>Retained for binary compatibility with older content mods.
    /// Ignored: each machine processes at most one batch per night.</summary>
    public Func<GameItem, int>? ResolveBatchCount { get; init; }
    /// <summary>Liquid recipes only. Replaces <see cref="Water"/>'s fixed
    /// millilitres-per-output with a volume read from the input the batch
    /// actually spends, so a recipe can tie its draw to the product it makes.
    /// Null keeps the fixed rule; a non-positive or failing result fails the
    /// night closed instead of drawing an unpriced volume.</summary>
    public Func<GameItem, GameItem, int>? ResolveWaterMillilitres { get; init; }
    /// <summary>Liquid recipes only. Version of this recipe's batch pricing.
    /// A stored progress record stamped with another version is dropped and the
    /// batch restarts from the input still in the slot, so a content mod can
    /// change how a batch is priced without stranding a machine on the old
    /// target. Bump it whenever the recipe's per-batch maths changes.</summary>
    public int ProgressVersion { get; init; } = 1;
}

public sealed record ForgeMachineView(string MachineId, bool NativeMachine,
    ForgeMachineProcessMode Mode, int RecipeCount, bool RuntimeInstalled);
