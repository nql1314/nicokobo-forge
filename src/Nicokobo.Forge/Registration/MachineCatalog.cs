using Il2Cpp;

namespace Nicokobo.Forge.Registration;

internal sealed record RegisteredMachineRecipe(string OwnerId, ForgeMachineRecipe Value);

internal sealed class MachineProfile(string ownerId, ForgeMachineDefinition definition,
    bool nativeMachine, IEnumerable<RegisteredMachineRecipe> recipes,
    Func<GameItem, GameInventory, bool>? nativeBatchProbe = null)
{
    internal string OwnerId { get; } = ownerId;
    internal ForgeMachineDefinition Definition { get; } = definition;
    internal string MachineId => Definition.MachineId;
    internal bool NativeMachine { get; } = nativeMachine;
    internal ForgeMachineTemplate Template => Definition.Template;
    internal ForgeMachinePowerRule Power => Definition.Power;
    internal Func<GameItem, GameInventory, bool>? NativeBatchProbe { get; } = nativeBatchProbe;
    internal IReadOnlyList<RegisteredMachineRecipe> Recipes { get; } = Array.AsReadOnly(recipes.ToArray());
    private readonly HashSet<string> _feedstock = recipes.SelectMany(entry =>
        entry.Value.ItemInputs.Select(input => input.ItemId)).ToHashSet(StringComparer.Ordinal);
    private readonly HashSet<string> _admitted = recipes.SelectMany(entry =>
        entry.Value.ItemInputs.Select(input => input.ItemId).Concat(entry.Value.AuxiliaryItemIds))
        .ToHashSet(StringComparer.Ordinal);
    internal bool IsFeedstock(string id) => _feedstock.Contains(id);
    internal bool Accepts(string id) => _admitted.Contains(id);
}

internal sealed class MachineCatalog
{
    private readonly object _gate = new();
    private readonly Dictionary<string, MachineProfile> _machines = new(StringComparer.Ordinal);
    internal bool HasMachines { get { lock (_gate) return _machines.Count > 0; } }
    internal bool TryGet(string id, out MachineProfile? profile)
    { lock (_gate) return _machines.TryGetValue(id, out profile); }

    internal IReadOnlyList<ForgeMachineView> Snapshot(bool installed, bool nativeInstalled)
    {
        lock (_gate) return _machines.Values.OrderBy(profile => profile.MachineId,
            StringComparer.Ordinal).Select(profile => new ForgeMachineView(profile.MachineId,
                profile.NativeMachine, profile.Template, profile.Recipes.Count,
                installed && (!profile.NativeMachine || nativeInstalled))).ToArray();
    }

    internal SubmitResult Register(string owner, ForgeMachineDefinition? definition,
        Func<ForgeMachineDefinition, SubmitResult> stageItem)
    {
        if (definition == null || !Owned(owner, definition.MachineId) ||
            !TryFreezeDefinition(owner, definition, out var frozen))
            return new(SubmitStatus.Invalid, "Invalid machine template or recipe");
        lock (_gate)
        {
            if (_machines.ContainsKey(frozen.MachineId))
                return new(SubmitStatus.Conflict, "Machine ID already claimed");
            var result = stageItem(frozen);
            if (result.Status is not (SubmitStatus.Accepted or SubmitStatus.AlreadyPresent)) return result;
            _machines.Add(frozen.MachineId, new(owner, frozen, false,
                frozen.Recipes.Select(recipe => new RegisteredMachineRecipe(owner, recipe))));
            return new(SubmitStatus.Accepted, "Machine template and recipes staged");
        }
    }

    internal SubmitResult RegisterNative(string owner, ForgeMachineDefinition definition,
        Func<GameItem, GameInventory, bool>? probe)
    {
        if (definition.MachineId != "furnace" || definition.Template?.Output?.Kind != ForgeMachineOutputKind.Items ||
            definition.Template.LiquidInputs is not { Count: 0 } ||
            !TryFreezeDefinition(owner, definition, out var frozen))
            return new(SubmitStatus.Invalid, "Only native furnace item recipes are supported");
        lock (_gate)
        {
            _machines.TryGetValue("furnace", out var existing);
            if (existing != null && (!existing.NativeMachine ||
                (probe != null && existing.NativeBatchProbe != null && !Equals(probe, existing.NativeBatchProbe)) ||
                !Equals(existing.Power, frozen.Power) || HasConflicts(existing, frozen.Recipes)))
                return new(SubmitStatus.Conflict, "Native machine rules or recipe ID already claimed");
            var additions = frozen.Recipes.Select(recipe => new RegisteredMachineRecipe(owner, recipe));
            _machines["furnace"] = new(owner, existing?.Definition ?? frozen, true,
                (existing?.Recipes ?? []).Concat(additions), probe ?? existing?.NativeBatchProbe);
            return new(SubmitStatus.Accepted, "Native furnace recipes staged");
        }
    }

    internal SubmitResult RegisterAdditional(string owner, string id, IReadOnlyList<ForgeMachineRecipe>? recipes)
    {
        if (string.IsNullOrWhiteSpace(id)) return new(SubmitStatus.Invalid, "Target machine ID is missing");
        lock (_gate)
        {
            if (!_machines.TryGetValue(id, out var existing))
                return new(SubmitStatus.Invalid, "Target machine has not been registered");
            if (existing.NativeMachine)
                return new(SubmitStatus.Conflict, "Use native registration for a native machine");
            if (!TryFreezeRecipes(owner, recipes, existing.Template, out var frozen))
                return new(SubmitStatus.Invalid, "Recipe does not fit the target template");
            if (HasConflicts(existing, frozen)) return new(SubmitStatus.Conflict, "Recipe ID already claimed");
            _machines[id] = new(existing.OwnerId, existing.Definition, false,
                existing.Recipes.Concat(frozen.Select(recipe => new RegisteredMachineRecipe(owner, recipe))));
            return new(SubmitStatus.Accepted, "Additional recipes staged");
        }
    }

    private static bool Owned(string owner, string id) => !string.IsNullOrWhiteSpace(owner) &&
        !string.IsNullOrWhiteSpace(id) && id.StartsWith(owner + ".", StringComparison.Ordinal);
    private static bool Grid(ForgeMachineGrid? size) => size != null &&
        size.Width is >= 1 and <= 32 && size.Height is >= 1 and <= 32;
    private static bool HasConflicts(MachineProfile profile, IEnumerable<ForgeMachineRecipe> recipes) =>
        recipes.Any(recipe => profile.Recipes.Any(entry => entry.Value.RecipeId == recipe.RecipeId));

    private static bool TryFreezeDefinition(string owner, ForgeMachineDefinition definition,
        out ForgeMachineDefinition frozen)
    {
        frozen = definition;
        var t = definition.Template;
        if (string.IsNullOrWhiteSpace(owner) || t == null || t.Output == null ||
            !Enum.IsDefined(t.Output.Kind) || !Grid(t.Output.Size) ||
            (t.ItemInput != null && !Grid(t.ItemInput)) || (t.Battery != null && !Grid(t.Battery.Size)) ||
            (t.Modules != null && (!Grid(t.Modules.Size) || t.Modules.AllowedTypes is not { Count: > 0 } ||
                t.Modules.AllowedTypes.Any(string.IsNullOrWhiteSpace))) ||
            t.LiquidInputs == null || t.LiquidInputs.Count > 8 ||
            t.LiquidInputs.Any(slot => slot == null || string.IsNullOrWhiteSpace(slot.SlotId) ||
                !Grid(slot.Size) || string.IsNullOrWhiteSpace(slot.Label)) ||
            t.LiquidInputs.Select(slot => slot.SlotId).Distinct(StringComparer.Ordinal).Count() != t.LiquidInputs.Count ||
            (t.ItemInput == null && t.LiquidInputs.Count == 0) ||
            (t.Output.Kind == ForgeMachineOutputKind.Items && t.Output.ContainerCondition != null) ||
            definition.Power == null || definition.Power.Cost < 0 ||
            (t.Battery == null && (definition.Power.Cost != 0 || definition.Power.ResolveCost != null)) ||
            !TryFreezeRecipes(owner, definition.Recipes, t, out var recipes)) return false;
        frozen = definition with
        {
            Template = t with
            {
                LiquidInputs = Array.AsReadOnly(t.LiquidInputs.ToArray()),
                Modules = t.Modules == null ? null : t.Modules with
                { AllowedTypes = Array.AsReadOnly(t.Modules.AllowedTypes.ToArray()) }
            }, Recipes = Array.AsReadOnly(recipes)
        };
        return true;
    }

    private static bool TryFreezeRecipes(string owner, IReadOnlyList<ForgeMachineRecipe>? recipes,
        ForgeMachineTemplate template, out ForgeMachineRecipe[] frozen)
    {
        frozen = [];
        if (string.IsNullOrWhiteSpace(owner) || recipes is not { Count: > 0 }) return false;
        var slots = template.LiquidInputs.Select(slot => slot.SlotId).ToHashSet(StringComparer.Ordinal);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var values = new List<ForgeMachineRecipe>();
        foreach (var recipe in recipes)
        {
            if (recipe == null || !Owned(owner, recipe.RecipeId) || !ids.Add(recipe.RecipeId) ||
                recipe.ItemInputs == null || recipe.LiquidInputs == null || recipe.AuxiliaryItemIds == null ||
                (recipe.ItemInputs.Count == 0 && recipe.LiquidInputs.Count == 0) ||
                (template.ItemInput == null && (recipe.ItemInputs.Count > 0 || recipe.AuxiliaryItemIds.Count > 0)) ||
                recipe.ItemInputs.Any(input => input == null || string.IsNullOrWhiteSpace(input.ItemId) ||
                    input.Count < 1 || (input.WholeStack && input.Count != 1)) ||
                recipe.AuxiliaryItemIds.Any(string.IsNullOrWhiteSpace) ||
                recipe.LiquidInputs.Any(input => input == null || !slots.Contains(input.SlotId) ||
                    !Volume(input.Millilitres, input.ResolveMillilitres != null) ||
                    (input.LiquidId != null && string.IsNullOrWhiteSpace(input.LiquidId))) ||
                recipe.LiquidInputs.Select(input => input.SlotId).Distinct(StringComparer.Ordinal).Count() !=
                    recipe.LiquidInputs.Count || !TryFreezeOutput(recipe.Output, template.Output.Kind, out var output)) return false;
            values.Add(recipe with
            {
                ItemInputs = Array.AsReadOnly(recipe.ItemInputs.ToArray()),
                LiquidInputs = Array.AsReadOnly(recipe.LiquidInputs.ToArray()),
                AuxiliaryItemIds = Array.AsReadOnly(recipe.AuxiliaryItemIds.ToArray()), Output = output
            });
        }
        frozen = values.ToArray();
        return true;
    }

    private static bool Volume(int ml, bool resolver) => ml >= (resolver ? 0 : 1) &&
        ml <= int.MaxValue / MachineLiquidMath.PartsPerMillilitre;
    private static bool Contents(IReadOnlyList<ForgeMachineLiquidAmount>? values) => values is { Count: > 0 } &&
        values.All(part => part != null && !string.IsNullOrWhiteSpace(part.LiquidId) &&
            Volume(part.Millilitres, part.ResolveMillilitres != null)) &&
        values.Select(part => part.LiquidId).Distinct(StringComparer.Ordinal).Count() == values.Count;
    private static bool TryFreezeOutput(ForgeMachineOutput? value, ForgeMachineOutputKind kind,
        out ForgeMachineOutput output)
    {
        output = value!;
        if (kind == ForgeMachineOutputKind.Items && value is ForgeMachineItemOutput item &&
            !string.IsNullOrWhiteSpace(item.ItemId) && item.Count is >= 1 and <= 256 &&
            (item.Contents == null || Contents(item.Contents)))
        {
            output = item with { Contents = item.Contents == null ? null : Array.AsReadOnly(item.Contents.ToArray()) };
            return true;
        }
        if (kind == ForgeMachineOutputKind.Container && value is ForgeMachineContainerOutput container &&
            Contents(container.Contents))
        {
            output = container with { Contents = Array.AsReadOnly(container.Contents.ToArray()) };
            return true;
        }
        return false;
    }
}
