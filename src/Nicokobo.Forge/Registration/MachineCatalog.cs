using Il2Cpp;

namespace Nicokobo.Forge.Registration;

internal sealed record RegisteredMachineRecipe(string OwnerId, ForgeMachineRecipe Value);

// Profiles are replaced on registration. Runtime readers keep a consistent
// recipe list and admission index without allocating a public snapshot.
internal sealed class MachineProfile
{
    private readonly HashSet<string> _feedstock = new(StringComparer.Ordinal);
    private readonly HashSet<string> _admitted = new(StringComparer.Ordinal);

    internal string MachineId { get; }
    internal bool NativeMachine { get; }
    internal ForgeMachineProcessMode Mode { get; }
    internal IReadOnlyList<RegisteredMachineRecipe> Recipes { get; }
    /// <summary>Native machine only. Reports whether the machine's own batch
    /// can run from the current input, so registered recipes stay idle that
    /// night. Null keeps the conservative feedstock-only rule.</summary>
    internal Func<GameItem, GameInventory, bool>? NativeBatchProbe { get; }

    internal MachineProfile(string machineId, bool nativeMachine,
        ForgeMachineProcessMode mode, IEnumerable<RegisteredMachineRecipe> recipes,
        Func<GameItem, GameInventory, bool>? nativeBatchProbe = null)
    {
        MachineId = machineId;
        NativeMachine = nativeMachine;
        Mode = mode;
        NativeBatchProbe = nativeBatchProbe;
        Recipes = Array.AsReadOnly(recipes.ToArray());
        foreach (var entry in Recipes)
        {
            foreach (var input in entry.Value.Inputs) _feedstock.Add(input.ItemId);
            if (entry.Value.AuxiliaryItemIds != null)
                _admitted.UnionWith(entry.Value.AuxiliaryItemIds);
        }
        _admitted.UnionWith(_feedstock);
    }

    internal bool IsFeedstock(string itemId) => _feedstock.Contains(itemId);
    internal bool Accepts(string itemId) => _admitted.Contains(itemId);
}

internal sealed class MachineCatalog
{
    private readonly object _gate = new();
    private readonly Dictionary<string, MachineProfile> _machines = new(StringComparer.Ordinal);

    internal bool HasMachines { get { lock (_gate) return _machines.Count != 0; } }

    internal bool TryGet(string machineId, out MachineProfile? profile)
    {
        lock (_gate) return _machines.TryGetValue(machineId, out profile);
    }

    internal IReadOnlyList<ForgeMachineView> Snapshot(bool installed)
    {
        lock (_gate)
            return _machines.Values.OrderBy(profile => profile.MachineId, StringComparer.Ordinal)
                .Select(profile => new ForgeMachineView(profile.MachineId,
                    profile.NativeMachine, profile.Mode, profile.Recipes.Count, installed)).ToArray();
    }

    internal SubmitResult Register(string ownerId, string machineId,
        IReadOnlyList<ForgeMachineRecipe>? recipes, ForgeMachineProcessMode mode,
        bool nativeMachine, Func<GameItem, GameInventory, bool>? nativeBatchProbe = null,
        Func<SubmitResult>? registerItem = null)
    {
        if (string.IsNullOrWhiteSpace(machineId) || !Enum.IsDefined(mode) ||
            (nativeMachine ? machineId != "furnace" : registerItem == null ||
                !machineId.StartsWith(ownerId + ".", StringComparison.Ordinal)))
            return new(SubmitStatus.Invalid, "Invalid machine registration");
        if (nativeBatchProbe != null && !nativeMachine)
            return new(SubmitStatus.Invalid, "Native batch probe requires a native machine");
        if (!TryFreeze(ownerId, recipes, mode, out var frozen))
            return new(SubmitStatus.Invalid, "Invalid machine recipe");
        lock (_gate)
        {
            _machines.TryGetValue(machineId, out var existing);
            if (existing != null && (existing.NativeMachine != nativeMachine ||
                existing.Mode != mode || HasConflicts(existing, frozen)))
                return new(SubmitStatus.Conflict, "Machine mode or recipe ID already claimed");
            if (!nativeMachine)
            {
                var result = registerItem!();
                if (result.Status is not (SubmitStatus.Accepted or SubmitStatus.AlreadyPresent))
                    return result;
            }
            Publish(machineId, nativeMachine, mode, existing, frozen, nativeBatchProbe);
            return new(SubmitStatus.Accepted, "Machine recipes staged");
        }
    }

    internal SubmitResult RegisterAdditional(string ownerId, string machineId,
        IReadOnlyList<ForgeMachineRecipe>? recipes)
    {
        if (string.IsNullOrWhiteSpace(machineId) ||
            !TryFreeze(ownerId, recipes, ForgeMachineProcessMode.NightlyItems, out var frozen))
            return new(SubmitStatus.Invalid, "Invalid additional item recipe");
        lock (_gate)
        {
            if (!_machines.TryGetValue(machineId, out var existing))
                return new(SubmitStatus.Invalid, "Target machine has not been registered");
            if (existing.NativeMachine || existing.Mode != ForgeMachineProcessMode.NightlyItems ||
                HasConflicts(existing, frozen))
                return new(SubmitStatus.Conflict, "Target mode or additional recipe ID conflicts");
            Publish(machineId, false, existing.Mode, existing, frozen,
                existing.NativeBatchProbe);
            return new(SubmitStatus.Accepted, "Additional machine recipes staged");
        }
    }

    private static bool HasConflicts(MachineProfile existing,
        RegisteredMachineRecipe[] additions)
    {
        var ids = existing.Recipes.Select(entry => entry.Value.RecipeId)
            .ToHashSet(StringComparer.Ordinal);
        return additions.Any(entry => ids.Contains(entry.Value.RecipeId));
    }

    private void Publish(string machineId, bool nativeMachine,
        ForgeMachineProcessMode mode, MachineProfile? existing,
        RegisteredMachineRecipe[] additions,
        Func<GameItem, GameInventory, bool>? nativeBatchProbe = null) => _machines[machineId] =
        new(machineId, nativeMachine, mode,
            (existing?.Recipes ?? Array.Empty<RegisteredMachineRecipe>()).Concat(additions),
            nativeBatchProbe ?? existing?.NativeBatchProbe);

    private static bool TryFreeze(string ownerId, IReadOnlyList<ForgeMachineRecipe>? recipes,
        ForgeMachineProcessMode mode, out RegisteredMachineRecipe[] frozen)
    {
        frozen = [];
        if (string.IsNullOrWhiteSpace(ownerId) || recipes is not { Count: > 0 }) return false;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var entries = new List<RegisteredMachineRecipe>(recipes.Count);
        foreach (var recipe in recipes)
        {
            if (recipe == null || string.IsNullOrWhiteSpace(recipe.RecipeId) ||
                !recipe.RecipeId.StartsWith(ownerId + ".", StringComparison.Ordinal) ||
                !ids.Add(recipe.RecipeId) || string.IsNullOrWhiteSpace(recipe.OutputItemId) ||
                recipe.OutputCount is < 1 or > 256 || recipe.PowerCost < 0 ||
                recipe.ProgressVersion < 1 ||
                recipe.Inputs is not { Count: > 0 } ||
                recipe.Inputs.Any(input => input == null ||
                    string.IsNullOrWhiteSpace(input.ItemId) || input.Count <= 0) ||
                recipe.AuxiliaryItemIds?.Any(string.IsNullOrWhiteSpace) == true ||
                (mode == ForgeMachineProcessMode.NightlyItems &&
                    (recipe.Water != null || recipe.ResolveWaterMillilitres != null ||
                     recipe.ResolveLifetimeOutputCount != null ||
                     recipe.AcceptsStackedInput ||
                     recipe.ResolveConsumedUnits != null)) ||
                (mode == ForgeMachineProcessMode.NightlyLiquid &&
                    (recipe.Water == null ||
                     (recipe.ResolveWaterMillilitres == null &&
                      recipe.Water.MillilitresPerOutput <= 0) ||
                     recipe.Water.MillilitresPerOutput < 0 ||
                     recipe.Water.MinimumQuality is < 1 or > 3 ||
                     recipe.ResolveLifetimeOutputCount == null ||
                     recipe.MaxOutputsPerNight is < 1 or > 256 ||
                     recipe.Inputs.Count != 1 || recipe.Inputs[0].Count != 1))) return false;
            // Caller-owned lists must not change a staged recipe or its index.
            entries.Add(new(ownerId, recipe with
            {
                Inputs = Array.AsReadOnly(recipe.Inputs.ToArray()),
                AuxiliaryItemIds = recipe.AuxiliaryItemIds == null ? null :
                    Array.AsReadOnly(recipe.AuxiliaryItemIds.ToArray())
            }));
        }
        frozen = entries.ToArray();
        return true;
    }
}
