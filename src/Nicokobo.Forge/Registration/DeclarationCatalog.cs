using System.Text.RegularExpressions;

namespace Nicokobo.Forge.Registration;

public enum DefinitionKind { Item, Module, Node, Effect, Start, Perk, Status }
public enum SubmitStatus { Accepted, AlreadyPresent, Invalid, Conflict }
public enum RegistrationStatus { DependenciesResolved, Waiting }

public sealed record SubmitResult(SubmitStatus Status, string Reason);
public sealed record RegistrationView(string OwnerId, string Version,
    RegistrationStatus Status, string Reason, IReadOnlyList<string> DefinitionIds);
internal sealed record ContentDefinition(string Id, DefinitionKind Kind,
    string Signature, IReadOnlyList<string> RequiredIds);
internal sealed record ModDependency(string OwnerId, string MinimumVersion);
internal sealed record ModDeclaration(string OwnerId, string Version,
    IReadOnlyList<ModDependency> RequiredOwners, IReadOnlyList<ContentDefinition> Definitions);

public sealed class RegistrationBatch
{
    private readonly List<ModDependency> _requiredOwners = new();
    private readonly List<ContentDefinition> _definitions = new();
    public string OwnerId { get; }
    public string Version { get; }

    public RegistrationBatch(string ownerId, string version)
    {
        OwnerId = ownerId;
        Version = version;
    }

    public RegistrationBatch Require(string ownerId, string minimumVersion = "0.0.0")
    {
        _requiredOwners.Add(new ModDependency(ownerId, minimumVersion));
        return this;
    }

    public RegistrationBatch Define(DefinitionKind kind, string localId,
        string signature, params string[] requiredIds)
    {
        _definitions.Add(new ContentDefinition(
            $"{OwnerId}.{kind.ToString().ToLowerInvariant()}.{localId}",
            kind, signature, Array.AsReadOnly((string[])requiredIds.Clone())));
        return this;
    }

    internal ModDeclaration Freeze() => new(OwnerId, Version,
        Array.AsReadOnly(_requiredOwners.ToArray()),
        Array.AsReadOnly(_definitions.ToArray()));
}

/// <summary>Managed declaration staging only. No native directory is changed here.</summary>
internal sealed class DeclarationCatalog
{
    private static readonly Regex OwnerPattern = new(
        "^[a-z][a-z0-9]*(\\.[a-z][a-z0-9_]*)+$", RegexOptions.CultureInvariant);
    private static readonly Regex LocalPattern = new(
        "^[a-z][a-z0-9_]*$", RegexOptions.CultureInvariant);
    private readonly Dictionary<string, ModDeclaration> _mods = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ContentDefinition> _definitions = new(StringComparer.Ordinal);

    public SubmitResult Submit(ModDeclaration declaration)
    {
        var invalid = Validate(declaration);
        if (invalid != null) return new(SubmitStatus.Invalid, invalid);
        if (_mods.TryGetValue(declaration.OwnerId, out var old))
            return Same(old, declaration)
                ? new(SubmitStatus.AlreadyPresent, "Same declaration already staged")
                : new(SubmitStatus.Conflict, "Owner already submitted different content");

        foreach (var definition in declaration.Definitions)
            if (_definitions.ContainsKey(definition.Id))
                return new(SubmitStatus.Conflict, $"Content ID already reserved: {definition.Id}");

        _mods.Add(declaration.OwnerId, declaration);
        foreach (var definition in declaration.Definitions)
            _definitions.Add(definition.Id, definition);
        return new(SubmitStatus.Accepted, "Declaration staged; no native content applied");
    }

    public IReadOnlyList<RegistrationView> Snapshot()
    {
        var ready = new HashSet<string>(StringComparer.Ordinal);
        var remaining = new HashSet<string>(_mods.Keys, StringComparer.Ordinal);
        bool changed;
        do
        {
            changed = false;
            foreach (var owner in remaining.OrderBy(value => value, StringComparer.Ordinal).ToArray())
            {
                var mod = _mods[owner];
                if (mod.RequiredOwners.All(dependency =>
                        _mods.TryGetValue(dependency.OwnerId, out var targetMod) &&
                        System.Version.Parse(targetMod.Version).CompareTo(
                            System.Version.Parse(dependency.MinimumVersion)) >= 0 &&
                        ready.Contains(dependency.OwnerId)) &&
                    mod.Definitions.SelectMany(d => d.RequiredIds)
                        .All(id => _definitions.TryGetValue(id, out var target) &&
                            (GetOwner(target.Id, target.Kind) == owner ||
                             ready.Contains(GetOwner(target.Id, target.Kind)))))
                {
                    ready.Add(owner);
                    remaining.Remove(owner);
                    changed = true;
                }
            }
        } while (changed);

        return _mods.Values.OrderBy(mod => mod.OwnerId, StringComparer.Ordinal)
            .Select(mod => new RegistrationView(mod.OwnerId, mod.Version,
                ready.Contains(mod.OwnerId) ? RegistrationStatus.DependenciesResolved : RegistrationStatus.Waiting,
                ready.Contains(mod.OwnerId) ? "Dependencies resolved; native application pending" :
                    WaitingReason(mod, ready),
                Array.AsReadOnly(mod.Definitions.Select(def => def.Id).ToArray())))
            .ToArray();
    }

    private string WaitingReason(ModDeclaration mod, HashSet<string> ready)
    {
        var missingOwner = mod.RequiredOwners.FirstOrDefault(dependency =>
            !_mods.ContainsKey(dependency.OwnerId));
        if (missingOwner != null) return $"Missing Mod dependency: {missingOwner.OwnerId}";
        var oldOwner = mod.RequiredOwners.FirstOrDefault(dependency =>
            System.Version.Parse(_mods[dependency.OwnerId].Version).CompareTo(
                System.Version.Parse(dependency.MinimumVersion)) < 0);
        if (oldOwner != null)
            return $"Mod dependency {oldOwner.OwnerId} requires version >= {oldOwner.MinimumVersion}";
        var missingId = mod.Definitions.SelectMany(def => def.RequiredIds)
            .FirstOrDefault(id => !_definitions.ContainsKey(id));
        if (missingId != null) return $"Missing content dependency: {missingId}";
        return "Dependency cycle or dependency waiting: " +
            string.Join(",", mod.RequiredOwners.Select(dependency => dependency.OwnerId).Concat(mod.Definitions
                    .SelectMany(def => def.RequiredIds)
                    .Where(_definitions.ContainsKey)
                    .Select(id => GetOwner(id, _definitions[id].Kind)))
                .Where(owner => !ready.Contains(owner)).Distinct(StringComparer.Ordinal));
    }

    private static string? Validate(ModDeclaration mod)
    {
        if (string.IsNullOrWhiteSpace(mod.OwnerId) || !OwnerPattern.IsMatch(mod.OwnerId))
            return "Owner ID must be namespaced lowercase ASCII";
        if (!System.Version.TryParse(mod.Version, out var parsed) || parsed.Major < 0)
            return "Version must be numeric, for example 0.1.0";
        if (mod.RequiredOwners.Select(dependency => dependency.OwnerId)
                .Distinct(StringComparer.Ordinal).Count() != mod.RequiredOwners.Count)
            return "Duplicate Mod dependency";
        if (mod.RequiredOwners.Any(dependency =>
                dependency.OwnerId == null || !OwnerPattern.IsMatch(dependency.OwnerId) ||
                dependency.OwnerId == mod.OwnerId ||
                !System.Version.TryParse(dependency.MinimumVersion, out _)))
            return "Invalid or self Mod dependency";
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var definition in mod.Definitions)
        {
            string prefix = $"{mod.OwnerId}.{definition.Kind.ToString().ToLowerInvariant()}.";
            if (!definition.Id.StartsWith(prefix, StringComparison.Ordinal) ||
                !LocalPattern.IsMatch(definition.Id[prefix.Length..]))
                return $"Invalid content ID: {definition.Id}";
            if (!seen.Add(definition.Id)) return $"Duplicate content ID: {definition.Id}";
            if (string.IsNullOrWhiteSpace(definition.Signature))
                return $"Empty content signature: {definition.Id}";
            if (definition.RequiredIds.Any(id => string.IsNullOrWhiteSpace(id) || id == definition.Id))
                return $"Invalid or self content dependency: {definition.Id}";
        }
        return null;
    }

    private static bool Same(ModDeclaration left, ModDeclaration right) =>
        left.Version == right.Version &&
        left.RequiredOwners.OrderBy(value => value.OwnerId, StringComparer.Ordinal)
            .SequenceEqual(right.RequiredOwners.OrderBy(value => value.OwnerId, StringComparer.Ordinal)) &&
        left.Definitions.Count == right.Definitions.Count &&
        left.Definitions.OrderBy(value => value.Id, StringComparer.Ordinal)
            .Zip(right.Definitions.OrderBy(value => value.Id, StringComparer.Ordinal))
            .All(pair => pair.First.Id == pair.Second.Id &&
                pair.First.Kind == pair.Second.Kind &&
                pair.First.Signature == pair.Second.Signature &&
                pair.First.RequiredIds.OrderBy(value => value, StringComparer.Ordinal)
                    .SequenceEqual(pair.Second.RequiredIds.OrderBy(value => value, StringComparer.Ordinal)));

    private static string GetOwner(string id, DefinitionKind kind)
    {
        string marker = $".{kind.ToString().ToLowerInvariant()}.";
        int index = id.LastIndexOf(marker, StringComparison.Ordinal);
        return index < 0 ? "" : id[..index];
    }
}
