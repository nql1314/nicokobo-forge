namespace Nicokobo.Forge.Registration;

internal sealed record RegisteredMachineRecipe(string OwnerId, ForgeMachineRecipe Value);

internal sealed class MachineProfile
{
    internal MachineProfile(string ownerId, ForgeMachineDefinition definition,
        IEnumerable<RegisteredMachineRecipe> recipes)
    {
        OwnerId = ownerId;
        Definition = definition;
        Recipes = Array.AsReadOnly(recipes.ToArray());
        _feedstock = Recipes.SelectMany(entry => entry.Value.ItemInputs.Select(input => input.ItemId))
            .ToHashSet(StringComparer.Ordinal);
        _admitted = MachineAdmission.InputIds(Recipes.Select(entry => entry.Value)).ToHashSet(StringComparer.Ordinal);
        _feedstockTags = MachineAdmission.InputTags(Recipes.Select(entry => entry.Value));
    }
    internal string OwnerId { get; }
    internal ForgeMachineDefinition Definition { get; }
    internal string MachineId => Definition.MachineId;
    internal ForgeMachineTemplate Template => Definition.Template;
    internal ForgeMachinePowerRule Power => Definition.Power;
    internal IReadOnlyList<RegisteredMachineRecipe> Recipes { get; }
    private readonly HashSet<string> _feedstock;
    private readonly HashSet<string> _admitted;
    private readonly string[] _feedstockTags;
    internal bool IsFeedstock(string id) => _feedstock.Contains(id);
    internal bool IsFeedstockTag(Func<string, bool> isTag) => _feedstockTags.Any(isTag);
    internal bool Accepts(string id) => _admitted.Contains(id);
}

/// <summary>The single frozen source of the native item-input whitelist. Ordering
/// is stable so the same list can be logged and handed to the native whitelist.</summary>
internal static class MachineAdmission
{
    internal static string[] InputIds(IEnumerable<ForgeMachineRecipe> recipes) => recipes
        .SelectMany(recipe => recipe.ItemInputs.Select(input => input.ItemId)
            .Concat(recipe.AuxiliaryItemIds))
        .Where(id => !string.IsNullOrWhiteSpace(id))
        .Distinct(StringComparer.Ordinal)
        .OrderBy(id => id, StringComparer.Ordinal)
        .ToArray();
    internal static string[] InputTags(IEnumerable<ForgeMachineRecipe> recipes) => recipes
        .SelectMany(recipe => recipe.ItemInputs.Select(input => input.ItemTag))
        .Where(tag => !string.IsNullOrWhiteSpace(tag)).Select(tag => tag!)
        .Distinct(StringComparer.Ordinal).OrderBy(tag => tag, StringComparer.Ordinal).ToArray();
}

internal sealed class MachineCatalog
{
    private readonly object _gate = new();
    private readonly Dictionary<string, MachineProfile> _machines = new(StringComparer.Ordinal);
    private readonly HashSet<string> _reserved = new(StringComparer.Ordinal);
    internal bool HasMachines { get { lock (_gate) return _machines.Count > 0; } }
    internal bool TryGet(string? id, out MachineProfile? profile)
    {
        if (string.IsNullOrWhiteSpace(id)) { profile = null; return false; }
        lock (_gate) return _machines.TryGetValue(id, out profile);
    }

    internal IReadOnlyList<ForgeMachineView> Snapshot(bool installed)
    {
        lock (_gate) return _machines.Values.OrderBy(profile => profile.MachineId,
            StringComparer.Ordinal).Select(profile => new ForgeMachineView(profile.MachineId,
                profile.Template, profile.Recipes.Count, installed)).ToArray();
    }

    internal SubmitResult Register(string owner, ForgeMachineDefinition? definition,
        Func<ForgeMachineDefinition, SubmitResult> stageItem)
    {
        if (definition == null || !Owned(owner, definition.MachineId) ||
            !TryFreezeDefinition(owner, definition, out var frozen))
            return new(SubmitStatus.Invalid, "Invalid machine template or recipe");
        lock (_gate)
        {
            // A machine ID is claimed exactly once: unlike plain items, a repeated
            // submission is reported as a conflict instead of AlreadyPresent.
            if (_machines.ContainsKey(frozen.MachineId))
                return new(SubmitStatus.Conflict, "Machine ID already claimed");
            // Reserve the ID so a concurrent submission cannot stage the same
            // machine twice; the native staging call stays outside the lock.
            if (!_reserved.Add(frozen.MachineId))
                return new(SubmitStatus.Invalid, "Machine ID is already being staged");
        }
        SubmitResult result;
        try { result = stageItem(frozen); }
        catch
        {
            lock (_gate) _reserved.Remove(frozen.MachineId);
            throw;
        }
        lock (_gate)
        {
            _reserved.Remove(frozen.MachineId);
            if (result.Status is not (SubmitStatus.Accepted or SubmitStatus.AlreadyPresent)) return result;
            _machines[frozen.MachineId] = new(owner, frozen,
                frozen.Recipes.Select(recipe => new RegisteredMachineRecipe(owner, recipe)));
            return new(SubmitStatus.Accepted, "Machine template and recipes staged");
        }
    }

    internal SubmitResult RegisterAdditional(string owner, string id, IReadOnlyList<ForgeMachineRecipe>? recipes)
    {
        if (string.IsNullOrWhiteSpace(id)) return new(SubmitStatus.Invalid, "Target machine ID is missing");
        lock (_gate)
        {
            if (!_machines.TryGetValue(id, out var existing))
                return new(SubmitStatus.Invalid, "Target machine has not been registered");
            if (!TryFreezeRecipes(owner, recipes, existing.Template, out var frozen))
                return new(SubmitStatus.Invalid, "Recipe does not fit the target template");
            if (frozen.Any(recipe => existing.Recipes.Any(entry =>
                    entry.Value.RecipeId == recipe.RecipeId)))
                return new(SubmitStatus.Conflict, "Recipe ID already claimed");
            _machines[id] = new(existing.OwnerId, existing.Definition,
                existing.Recipes.Concat(frozen.Select(recipe => new RegisteredMachineRecipe(owner, recipe))));
            return new(SubmitStatus.Accepted, "Additional recipes staged");
        }
    }

    private static bool Owned(string owner, string id) => !string.IsNullOrWhiteSpace(owner) &&
        !string.IsNullOrWhiteSpace(id) && id.StartsWith(owner + ".", StringComparison.Ordinal);
    private static bool Grid(ForgeMachineGrid? size) => size != null &&
        size.Width is >= 1 and <= ForgeNumbers.Machines.MaxGridSide && size.Height is >= 1 and <= ForgeNumbers.Machines.MaxGridSide;

    internal static bool TryFreezeDefinition(string owner, ForgeMachineDefinition definition,
        out ForgeMachineDefinition frozen)
    {
        frozen = definition;
        var t = definition.Template;
        if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(definition.MachineId) || t == null || t.Output == null ||
            !Enum.IsDefined(t.Output.Kind) || !Grid(t.Output.Size) ||
            (t.ItemInput != null && !Grid(t.ItemInput)) || (t.Battery != null && !Grid(t.Battery.Size)) ||
            (t.Modules != null && (!Grid(t.Modules.Size) || t.Modules.AllowedTypes is not { Count: > 0 } ||
                t.Modules.AllowedTypes.Any(string.IsNullOrWhiteSpace))) ||
            t.LiquidInputs == null || t.LiquidInputs.Count > ForgeNumbers.Machines.MaxLiquidInputSlots ||
            t.LiquidInputs.Any(slot => slot == null || string.IsNullOrWhiteSpace(slot.SlotId) ||
                !Grid(slot.Size) || slot.Label == null) ||
            t.LiquidInputs.Select(slot => slot.SlotId).Distinct(StringComparer.Ordinal).Count() != t.LiquidInputs.Count ||
            (t.ItemInput == null && t.LiquidInputs.Count == 0) ||
            (t.Output.Kind == ForgeMachineOutputKind.Items && t.Output.ContainerCondition != null) ||
            definition.Power == null || definition.Power.Cost < 0 ||
            definition.ProductionMarkupPercent is < 0 or > ForgeNumbers.Machines.MaxProductionMarkupPercent ||
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
                recipe.ItemInputs.Any(input => input == null || input.ItemId == null ||
                    (string.IsNullOrWhiteSpace(input.ItemId) == string.IsNullOrWhiteSpace(input.ItemTag)) ||
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
            !string.IsNullOrWhiteSpace(item.ItemId) && item.Count is >= 1 and <= ForgeNumbers.Machines.MaxOutputCount &&
            (item.Contents == null || Contents(item.Contents)))
        {
            output = item with { Contents = item.Contents == null ? null : Array.AsReadOnly(item.Contents.ToArray()) };
            return true;
        }
        if (kind == ForgeMachineOutputKind.Container && value is ForgeMachineContainerOutput container &&
            (container.ResolveContents == null ? Contents(container.Contents) : container.Contents is { Count: 0 }))
        {
            output = container with { Contents = Array.AsReadOnly(container.Contents.ToArray()) };
            return true;
        }
        return false;
    }
}
