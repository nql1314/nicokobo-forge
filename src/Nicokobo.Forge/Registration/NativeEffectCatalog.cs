using System.Text.RegularExpressions;

namespace Nicokobo.Forge.Registration;

internal sealed record NativeEffectDeclaration(string OwnerId, string EffectId,
    bool RandomEligible, Delegate Factory);
internal sealed record NativeEffectBatchEntry(string EffectId,
    bool RandomEligible, Delegate Factory);

internal sealed class NativeEffectCatalog
{
    private static readonly Regex NamespacedId = new(
        "^[a-z][a-z0-9]*(\\.[a-z][a-z0-9_]*)+$", RegexOptions.CultureInvariant);
    private readonly Dictionary<string, NativeEffectDeclaration> _effects =
        new(StringComparer.Ordinal);

    internal int Count => _effects.Count;

    internal SubmitResult Submit(string ownerId, string effectId,
        bool randomEligible, Delegate? factory)
    {
        var result = Check(ownerId, effectId, randomEligible, factory);
        if (result.Status != SubmitStatus.Accepted) return result;
        _effects.Add(effectId, new(ownerId, effectId, randomEligible, factory!));
        return result;
    }

    internal SubmitResult SubmitBatch(string ownerId,
        IReadOnlyList<NativeEffectBatchEntry> entries)
    {
        if (entries == null || entries.Count == 0)
            return new(SubmitStatus.Invalid, "Native effect batch is empty");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        bool anyAccepted = false;
        foreach (var entry in entries)
        {
            if (!seen.Add(entry.EffectId))
                return new(SubmitStatus.Invalid,
                    $"Duplicate native effect ID in batch: {entry.EffectId}");
            var result = Check(ownerId, entry.EffectId,
                entry.RandomEligible, entry.Factory);
            if (result.Status is SubmitStatus.Invalid or SubmitStatus.Conflict)
                return result with { Reason = $"{entry.EffectId}: {result.Reason}" };
            anyAccepted |= result.Status == SubmitStatus.Accepted;
        }
        foreach (var entry in entries)
            if (!_effects.ContainsKey(entry.EffectId))
                _effects.Add(entry.EffectId, new(ownerId, entry.EffectId,
                    entry.RandomEligible, entry.Factory));
        return anyAccepted
            ? new(SubmitStatus.Accepted, "Native effect batch staged atomically")
            : new(SubmitStatus.AlreadyPresent, "Native effect batch already staged");
    }

    private SubmitResult Check(string ownerId, string effectId,
        bool randomEligible, Delegate? factory)
    {
        if (string.IsNullOrWhiteSpace(ownerId) || !NamespacedId.IsMatch(ownerId))
            return new(SubmitStatus.Invalid, "Owner ID must be namespaced lowercase ASCII");
        if (string.IsNullOrWhiteSpace(effectId) || !NamespacedId.IsMatch(effectId) ||
            !effectId.StartsWith(ownerId + ".", StringComparison.Ordinal))
            return new(SubmitStatus.Invalid, "Effect ID must belong to its owner");
        if (factory == null)
            return new(SubmitStatus.Invalid, "Native effect factory is required");
        if (_effects.TryGetValue(effectId, out var old))
            return old.OwnerId == ownerId && old.RandomEligible == randomEligible &&
                   old.Factory.Equals(factory)
                ? new(SubmitStatus.AlreadyPresent, "Same native effect already staged")
                : new(SubmitStatus.Conflict, "Native effect ID already reserved");
        return new(SubmitStatus.Accepted,
            "Native effect staged; registry application pending");
    }

    internal IReadOnlyList<NativeEffectDeclaration> Snapshot() =>
        _effects.Values.OrderBy(effect => effect.EffectId, StringComparer.Ordinal).ToArray();
}
