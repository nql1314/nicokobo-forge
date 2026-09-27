using System.Text.RegularExpressions;

namespace Nicokobo.Forge.Registration;

public sealed record StartRegistrationView(string OwnerId, string StartId,
    int NativeStartType);

/// <summary>Claims stable start IDs and build-specific native carriers.</summary>
internal sealed class StartCatalog
{
    private static readonly Regex NamespacedId = new(
        "^[a-z][a-z0-9]*(\\.[a-z][a-z0-9_]*)+$", RegexOptions.CultureInvariant);
    private readonly Dictionary<string, StartRegistrationView> _starts =
        new(StringComparer.Ordinal);
    private readonly Dictionary<int, string> _nativeTypes = new();

    internal SubmitResult Submit(string ownerId, string startId, int nativeStartType)
    {
        if (string.IsNullOrWhiteSpace(ownerId) || !NamespacedId.IsMatch(ownerId))
            return new(SubmitStatus.Invalid, "Owner ID must be namespaced lowercase ASCII");
        if (string.IsNullOrWhiteSpace(startId) || !NamespacedId.IsMatch(startId) ||
            !startId.StartsWith(ownerId + ".", StringComparison.Ordinal))
            return new(SubmitStatus.Invalid, "Start ID must belong to its owner");
        if (nativeStartType <= 0)
            return new(SubmitStatus.Invalid, "Native start type must be positive");
        if (_starts.TryGetValue(startId, out var old))
            return old.OwnerId == ownerId && old.NativeStartType == nativeStartType
                ? new(SubmitStatus.AlreadyPresent, "Same start claim already staged")
                : new(SubmitStatus.Conflict, "Start ID already reserved");
        if (_nativeTypes.TryGetValue(nativeStartType, out var otherId))
            return new(SubmitStatus.Conflict,
                $"Native start type {nativeStartType} already reserved by {otherId}");
        _starts.Add(startId, new(ownerId, startId, nativeStartType));
        _nativeTypes.Add(nativeStartType, startId);
        return new(SubmitStatus.Accepted,
            "Start identity claimed; menu, save, and load flow remain caller-owned");
    }

    internal IReadOnlyList<StartRegistrationView> Snapshot() =>
        _starts.Values.OrderBy(value => value.StartId, StringComparer.Ordinal).ToArray();
}
