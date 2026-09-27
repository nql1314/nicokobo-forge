using System.Text.RegularExpressions;

namespace Nicokobo.Forge.Registration;

/// <summary>Current availability and localized explanation for an additional unlock requirement.</summary>
public sealed record NetworkUpgradeRequirement(
    bool Satisfied,
    string EnglishMissingText,
    string ChineseMissingText);

/// <summary>Definition for one non-repeatable Wilds Network upgrade.</summary>
public sealed record NetworkUpgradeOptions(
    int Cost,
    string EnglishTitle,
    string ChineseTitle,
    string EnglishSubtitle,
    string ChineseSubtitle,
    string EnglishDescription,
    string ChineseDescription,
    string EnglishAlreadyBought,
    string ChineseAlreadyBought,
    string UiAnchorId = "CRIMINEL_NETWORK",
    float UiOffsetX = 96f,
    float UiOffsetY = 0f,
    IReadOnlyList<string>? Prerequisites = null,
    Func<bool>? IsVisible = null,
    Action? OnUnlocked = null,
    Func<int>? GetCost = null,
    Func<NetworkUpgradeRequirement>? GetRequirement = null,
    Func<bool>? TryConsumeRequirement = null,
    int CreditCost = 0,
    Func<int>? GetCreditCost = null);

internal sealed record NetworkUpgradeDeclaration(string OwnerId, string UpgradeId,
    NetworkUpgradeOptions Options, IReadOnlyList<string> Prerequisites);

/// <summary>Process-wide ownership of custom Wilds Network upgrade IDs.</summary>
internal sealed class NetworkUpgradeCatalog
{
    private static readonly Regex OwnerPattern = new(
        "^[a-z][a-z0-9]*(\\.[a-z][a-z0-9_]*)+$", RegexOptions.CultureInvariant);
    private static readonly Regex UpgradePattern = new(
        "^[A-Z][A-Z0-9_]*$", RegexOptions.CultureInvariant);
    private readonly Dictionary<string, NetworkUpgradeDeclaration> _upgrades =
        new(StringComparer.Ordinal);

    internal SubmitResult Submit(string ownerId, string upgradeId,
        NetworkUpgradeOptions? options)
    {
        var invalid = Validate(ownerId, upgradeId, options);
        if (invalid != null) return new(SubmitStatus.Invalid, invalid);
        var validOptions = options!;
        var prerequisites = Array.AsReadOnly((validOptions.Prerequisites ?? Array.Empty<string>())
            .ToArray());
        var normalized = validOptions with { Prerequisites = prerequisites };
        var declaration = new NetworkUpgradeDeclaration(ownerId, upgradeId,
            normalized, prerequisites);
        if (_upgrades.TryGetValue(upgradeId, out var old))
            return Same(old, declaration)
                ? new(SubmitStatus.AlreadyPresent, "Same network upgrade already staged")
                : new(SubmitStatus.Conflict, "Network upgrade ID already reserved");
        _upgrades.Add(upgradeId, declaration);
        return new(SubmitStatus.Accepted,
            "Network upgrade staged; native store and UI application pending");
    }

    internal bool TryGet(string upgradeId, out NetworkUpgradeDeclaration declaration) =>
        _upgrades.TryGetValue(upgradeId, out declaration!);

    internal IReadOnlyList<NetworkUpgradeDeclaration> Snapshot() =>
        _upgrades.Values.OrderBy(x => x.UpgradeId, StringComparer.Ordinal).ToArray();

    private static string? Validate(string ownerId, string upgradeId,
        NetworkUpgradeOptions? options)
    {
        if (string.IsNullOrWhiteSpace(ownerId) || !OwnerPattern.IsMatch(ownerId))
            return "Owner ID must be namespaced lowercase ASCII";
        string expectedPrefix = ownerId.Replace('.', '_').ToUpperInvariant() + "_";
        if (string.IsNullOrWhiteSpace(upgradeId) ||
            !UpgradePattern.IsMatch(upgradeId) ||
            !upgradeId.StartsWith(expectedPrefix, StringComparison.Ordinal))
            return $"Network upgrade ID must start with {expectedPrefix}";
        if (options == null) return "Network upgrade options are required";
        if (options.Cost < 0) return "Network upgrade cost cannot be negative";
        if (options.CreditCost < 0) return "Network upgrade credit cost cannot be negative";
        if ((options.GetRequirement == null) != (options.TryConsumeRequirement == null))
            return "Requirement check and consumption callbacks must be supplied together";
        if (!float.IsFinite(options.UiOffsetX) || !float.IsFinite(options.UiOffsetY))
            return "Network upgrade UI offset must be finite";
        if (!UpgradePattern.IsMatch(options.UiAnchorId ?? ""))
            return "Network upgrade UI anchor ID is invalid";
        if (new[]
            {
                options.EnglishTitle, options.ChineseTitle,
                options.EnglishSubtitle, options.ChineseSubtitle,
                options.EnglishDescription, options.ChineseDescription,
                options.EnglishAlreadyBought, options.ChineseAlreadyBought
            }.Any(string.IsNullOrWhiteSpace))
            return "Network upgrade localization values are required";
        var prerequisites = options.Prerequisites ?? Array.Empty<string>();
        if (prerequisites.Any(id => string.IsNullOrWhiteSpace(id) ||
                !UpgradePattern.IsMatch(id) || id == upgradeId) ||
            prerequisites.Distinct(StringComparer.Ordinal).Count() != prerequisites.Count)
            return "Network upgrade prerequisites are invalid, duplicated or self-referential";
        return null;
    }

    private static bool Same(NetworkUpgradeDeclaration left,
        NetworkUpgradeDeclaration right)
    {
        var a = left.Options;
        var b = right.Options;
        return left.OwnerId == right.OwnerId && left.UpgradeId == right.UpgradeId &&
            a.Cost == b.Cost && a.EnglishTitle == b.EnglishTitle &&
            a.ChineseTitle == b.ChineseTitle &&
            a.EnglishSubtitle == b.EnglishSubtitle &&
            a.ChineseSubtitle == b.ChineseSubtitle &&
            a.EnglishDescription == b.EnglishDescription &&
            a.ChineseDescription == b.ChineseDescription &&
            a.EnglishAlreadyBought == b.EnglishAlreadyBought &&
            a.ChineseAlreadyBought == b.ChineseAlreadyBought &&
            a.UiAnchorId == b.UiAnchorId && a.UiOffsetX == b.UiOffsetX &&
            a.UiOffsetY == b.UiOffsetY && Equals(a.IsVisible, b.IsVisible) &&
            Equals(a.OnUnlocked, b.OnUnlocked) &&
            Equals(a.GetCost, b.GetCost) &&
            Equals(a.GetRequirement, b.GetRequirement) &&
            Equals(a.TryConsumeRequirement, b.TryConsumeRequirement) &&
            a.CreditCost == b.CreditCost &&
            Equals(a.GetCreditCost, b.GetCreditCost) &&
            left.Prerequisites.SequenceEqual(right.Prerequisites,
                StringComparer.Ordinal);
    }
}
