using Il2Cpp;
using Nicokobo.Forge.Registration;

namespace Nicokobo.Forge;

public enum ForgeMachineOutputKind { Items, Container }
public sealed record ForgeMachineGrid(int Width, int Height);

/// <summary>A reusable container slot. Recipes consume its contents, never
/// the container item. Conditions must only read the candidate item.</summary>
public sealed record ForgeMachineContainerSlot(string SlotId,
    ForgeMachineGrid Size, string Label = "Liquid input",
    Func<GameItem, bool>? Condition = null);

public sealed record ForgeMachineBatteryTemplate(ForgeMachineGrid Size, bool Required = true);
public sealed record ForgeMachineModuleTemplate(ForgeMachineGrid Size, IReadOnlyList<string> AllowedTypes);

/// <summary>One output kind per machine. Container output fills an installed
/// container; item output uses a bounded warehouse.</summary>
public sealed record ForgeMachineOutputTemplate(ForgeMachineOutputKind Kind,
    ForgeMachineGrid Size, Func<GameItem, bool>? ContainerCondition = null);

/// <summary>Native furnace UI components with declarative slot extensions.
/// Null ItemInput supports liquid-only machines. Battery/module positions
/// retain the native layout so the native helpers still work.</summary>
public sealed record ForgeMachineTemplate(ForgeMachineGrid? ItemInput,
    ForgeMachineOutputTemplate Output)
{
    public IReadOnlyList<ForgeMachineContainerSlot> LiquidInputs { get; init; } = [];
    public ForgeMachineBatteryTemplate? Battery { get; init; } = new(new(2, 2));
    public ForgeMachineModuleTemplate? Modules { get; init; } =
        new(new(4, 4), ["MODULE_TYPE_FURNACE", "MODULE_TYPE_UNIVERSAL"]);
    public bool ManualSlot { get; init; } = true;
}

/// <summary>Cost is per batch unless PerOutput is true. A resolver receives
/// selected inputs and the resolved output count; it must only read state.</summary>
public sealed record ForgeMachinePowerRule(int Cost = 0,
    Func<ForgeMachineBatchContext, int>? ResolveCost = null, bool PerOutput = false);

public sealed record ForgeMachineIngredient(string ItemId, int Count,
    Func<GameItem, bool>? Condition = null)
{
    /// <summary>Select a whole matching stack, largest unit value first. Its
    /// actual selected count is available in the batch context.</summary>
    public bool WholeStack { get; init; }
}

/// <summary>Draw from a named input slot. LiquidId consumes that component
/// only; null consumes the mixture proportionally.</summary>
public sealed record ForgeMachineLiquidIngredient(string SlotId,
    int Millilitres, string? LiquidId = null, Func<GameItem, bool>? Condition = null,
    Func<ForgeMachineBatchContext, int>? ResolveMillilitres = null);
public sealed record ForgeMachineLiquidAmount(string LiquidId, int Millilitres,
    Func<ForgeMachineBatchContext, int>? ResolveMillilitres = null);

public abstract record ForgeMachineOutput;
/// <summary>Contents optionally fills each newly created container item.</summary>
public sealed record ForgeMachineItemOutput(string ItemId, int Count = 1,
    Func<ForgeMachineBatchContext, int>? ResolveCount = null,
    Action<ForgeMachineBatchContext, GameItem>? PrepareItem = null,
    IReadOnlyList<ForgeMachineLiquidAmount>? Contents = null) : ForgeMachineOutput;
public sealed record ForgeMachineContainerOutput(
    IReadOnlyList<ForgeMachineLiquidAmount> Contents) : ForgeMachineOutput;

public sealed record ForgeMachineRecipe(string RecipeId,
    IReadOnlyList<ForgeMachineIngredient> ItemInputs, ForgeMachineOutput Output)
{
    public IReadOnlyList<ForgeMachineLiquidIngredient> LiquidInputs { get; init; } = [];
    public IReadOnlyList<string> AuxiliaryItemIds { get; init; } = [];
}
public sealed record ForgeMachineDefinition(string MachineId,
    ForgeMachineTemplate Template, IReadOnlyList<ForgeMachineRecipe> Recipes,
    ForgeMachinePowerRule Power, Action<GameItem>? ConfigureItem = null,
    NativeItemOptions? ItemOptions = null);
public sealed record ForgeMachineItemTake(GameItem Item, int Count);
public sealed record ForgeMachineBatchContext(GameItem Machine,
    IReadOnlyList<ForgeMachineItemTake> Items,
    IReadOnlyDictionary<string, GameItem> LiquidContainers, int OutputCount);

/// <summary>Live native inventories, not a persisted or detached snapshot.</summary>
public sealed record ForgeMachineInventory(GameSlotInventory? Battery,
    GameInventory? Modules, GameInventory? ItemInput, GameInventory Output,
    IReadOnlyDictionary<string, GameSlotInventory> LiquidInputs,
    GameSlotInventory? Manual);
public sealed record ForgeMachineView(string MachineId, bool NativeMachine,
    ForgeMachineTemplate Template, int RecipeCount, bool RuntimeInstalled);

public sealed record ForgeMachineLiquidPart(string LiquidId, int Parts);
/// <summary>Native volume uses 1000 parts per millilitre. Recipe declarations
/// use whole millilitres; snapshots preserve every native part for rollback.</summary>
public sealed record ForgeMachineLiquidSnapshot(int CapacityParts,
    IReadOnlyList<ForgeMachineLiquidPart> Contents)
{
    public int TotalParts => checked(Contents.Sum(part => part.Parts));
    public decimal Millilitres => TotalParts / 1000m;
}
