using Il2Cpp;
using Nicokobo.Forge.Registration;

namespace Nicokobo.Forge;

public enum ForgeMachineOutputKind { Items, Container }
public sealed record ForgeMachineGrid(int Width, int Height);

/// <summary>A reusable container slot. Recipes consume its contents, never
/// the container item. Size (1,1) uses the native slot, which grows to fit its
/// installed item. An empty Label uses the native localized storage-container
/// label. Conditions must only read the candidate item.</summary>
public sealed record ForgeMachineContainerSlot(string SlotId,
    ForgeMachineGrid Size, string Label = "",
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
    public ForgeMachineBatteryTemplate? Battery { get; init; } = new(new(ForgeNumbers.Machines.DefaultBatteryWidth, ForgeNumbers.Machines.DefaultBatteryHeight));
    public ForgeMachineModuleTemplate? Modules { get; init; } =
        new(new(ForgeNumbers.Machines.DefaultModuleWidth, ForgeNumbers.Machines.DefaultModuleHeight), ["MODULE_TYPE_FURNACE", "MODULE_TYPE_UNIVERSAL"]);
    public bool ManualSlot { get; init; } = true;
}

/// <summary>Cost is per batch unless PerOutput is true. A resolver receives
/// selected inputs and the resolved output count; it must only read state.</summary>
public sealed record ForgeMachinePowerRule(int Cost = 0,
    Func<ForgeMachineBatchContext, int>? ResolveCost = null, bool PerOutput = false);

public sealed record ForgeMachineIngredient(string ItemId, int Count,
    Func<GameItem, bool>? Condition = null)
{
    public ForgeRecipeGuideDisplay? Guide { get; init; }
    /// <summary>An alternative to ItemId: accept any item carrying this native
    /// tag. A read-only Condition can narrow the category at batch selection.</summary>
    public string? ItemTag { get; init; }
    /// <summary>Select a whole matching stack, largest unit value first. Its
    /// actual selected count is available in the batch context.</summary>
    public bool WholeStack { get; init; }
}

/// <summary>Draw from a named input slot. LiquidId consumes that component
/// only; null consumes the mixture proportionally.</summary>
public sealed record ForgeMachineLiquidIngredient(string SlotId,
    int Millilitres, string? LiquidId = null, Func<GameItem, bool>? Condition = null,
    Func<ForgeMachineBatchContext, int>? ResolveMillilitres = null)
{
    public ForgeRecipeGuideDisplay? Guide { get; init; }
}
public sealed record ForgeMachineLiquidAmount(string LiquidId, int Millilitres,
    Func<ForgeMachineBatchContext, int>? ResolveMillilitres = null,
    Func<ForgeMachineBatchContext, decimal>? ResolveValue = null)
{
    public Func<ForgeMachineBatchContext, decimal>? ResolveQualityBasis { get; init; }
}

public abstract record ForgeMachineOutput;
/// <summary>Contents optionally fills each newly created container item.</summary>
public sealed record ForgeMachineItemOutput(string ItemId, int Count = ForgeNumbers.Machines.DefaultOutputCount,
    Func<ForgeMachineBatchContext, int>? ResolveCount = null,
    Action<ForgeMachineBatchContext, GameItem>? PrepareItem = null,
    IReadOnlyList<ForgeMachineLiquidAmount>? Contents = null) : ForgeMachineOutput
{
    public ForgeRecipeGuideDisplay? Guide { get; init; }
    /// <summary>Select one registered item ID for the entire batch, after
    /// planning resources and before creating products or debiting inputs.
    /// The resolver must not mutate game state. ItemId remains the guide's
    /// default illustration; every product is checked against the resolved ID.</summary>
    public Func<ForgeMachineBatchContext, string>? ResolveItemId { get; init; }
}
public sealed record ForgeMachineContainerOutput(
    IReadOnlyList<ForgeMachineLiquidAmount> Contents) : ForgeMachineOutput
{
    public ForgeRecipeGuideDisplay? Guide { get; init; }
    /// <summary>Optional read-only composition resolver, evaluated after input
    /// draws are planned. Native parts and component values are preserved.
    /// Use an empty Contents list with this resolver; Forge validates the
    /// resolved mixture and destination capacity before any mutation.</summary>
    public Func<ForgeMachineBatchContext, IReadOnlyList<ForgeMachineLiquidPart>>? ResolveContents { get; init; }
}

public sealed record ForgeMachineRecipe(string RecipeId,
    IReadOnlyList<ForgeMachineIngredient> ItemInputs, ForgeMachineOutput Output)
{
    /// <summary>Stable semantic revision supplied by the recipe owner. Include
    /// configured predicate thresholds and dynamic quantity/output rules; update
    /// it when those rules change. Delegate assembly identity cannot describe
    /// captured configuration. Presentation-only changes need not change it.</summary>
    public string RuleRevision { get; init; } = "";
    public ForgeRecipeGuideOptions? Guide { get; init; }
    public IReadOnlyList<ForgeMachineLiquidIngredient> LiquidInputs { get; init; } = [];
    public IReadOnlyList<string> AuxiliaryItemIds { get; init; } = [];
}
public sealed record ForgeMachineDefinition(string MachineId,
    ForgeMachineTemplate Template, IReadOnlyList<ForgeMachineRecipe> Recipes,
    ForgeMachinePowerRule Power, Action<GameItem>? ConfigureItem = null,
    NativeItemOptions? ItemOptions = null, int ProductionMarkupPercent = 0)
{
    public ForgeRecipeGuideOptions Guide { get; init; } = new();
    /// <summary>Read-only nightly batch limit; null means one batch. Evaluated
    /// before processing and again before each extra batch. A lower live limit
    /// stops the cycle; increases take effect next night. Must return at least one.</summary>
    public Func<GameItem, int>? ResolveNightlyBatchCount { get; init; }
}
public sealed record ForgeMachineItemTake(GameItem Item, int Count)
{
    /// <summary>Selected units times the native intrinsic per-unit value,
    /// captured before any resource is consumed.</summary>
    public decimal Value { get; init; }
}
public sealed record ForgeMachineLiquidTake(string SlotId, GameItem Container,
    ForgeMachineLiquidSnapshot Before, ForgeMachineLiquidSnapshot After, decimal Value)
{
    public decimal QualityBasis { get; init; }
}
public sealed record ForgeMachineBatchContext(GameItem Machine,
    IReadOnlyList<ForgeMachineItemTake> Items,
    IReadOnlyDictionary<string, GameItem> LiquidContainers, int OutputCount)
{
    /// <summary>The content owner's configured production markup for this
    /// machine. Additional recipe contributors inherit the machine's rate.</summary>
    public int ProductionMarkupPercent { get; init; }
    public IReadOnlyList<ForgeMachineLiquidTake> Liquids { get; init; } = [];
}

/// <summary>Live native inventories, not a persisted or detached snapshot.</summary>
public sealed record ForgeMachineInventory(GameSlotInventory? Battery,
    GameInventory? Modules, GameInventory? ItemInput, GameInventory Output,
    IReadOnlyDictionary<string, GameSlotInventory> LiquidInputs,
    GameSlotInventory? Manual);
public sealed record ForgeMachineView(string MachineId,
    ForgeMachineTemplate Template, int RecipeCount, bool RuntimeInstalled);

public sealed record ForgeMachineLiquidPart(string LiquidId, int Parts)
{
    /// <summary>Optional intrinsic value of this component's entire volume.
    /// Null preserves the native volume price. Fractions survive mixing and
    /// partial consumption; integer rounding happens only when pricing the item.</summary>
    public decimal? Value { get; init; }
    /// <summary>Optional value before the producer's quality premium.</summary>
    public decimal? QualityBasis { get; init; }
}
/// <summary>Native volume uses 1000 parts per millilitre. Recipe declarations
/// use whole millilitres; snapshots preserve every native part for rollback.</summary>
public sealed record ForgeMachineLiquidSnapshot(int CapacityParts,
    IReadOnlyList<ForgeMachineLiquidPart> Contents)
{
    public int TotalParts => checked(Contents.Sum(part => part.Parts));
    public decimal Millilitres => TotalParts / (decimal)ForgeNumbers.NativeUnits.LiquidPartsPerMillilitre;
}
