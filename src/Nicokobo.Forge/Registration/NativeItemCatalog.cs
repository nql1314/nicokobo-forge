using System.Text.RegularExpressions;

namespace Nicokobo.Forge.Registration;

internal enum NativeItemKind { Item, Amenity, Node, Module }

public enum NightShopStockPolicy
{
    None,
    Repeatable,
    Unique
}

[Flags]
public enum NpcTradeStockCategory
{
    None = 0,
    Material = 1,
    Ore = 2,
    Module = 4,
    Machine = 8,
    Household = 16,
    Food = 32,
    Medical = 64
}

/// <summary>Relative weight within a supplier's eligible custom stock.</summary>
public sealed record NpcTradeStockOptions(NpcTradeStockCategory Category, float Weight = 1f)
{
    /// <summary>Exclude this offer while the player owns a positive quantity.</summary>
    public bool SkipWhenOwned { get; init; }
}

public sealed record LocalizedItemText(string Chinese, string English)
{
    public string For(bool english) => english ? English : Chinese;
}

/// <summary>Optional night-shop/NPC stock and localized tooltip text for a native item.
/// Availability is evaluated only when the night shop generates its stock.</summary>
public sealed record NativeItemOptions(
    NightShopStockPolicy NightShop = NightShopStockPolicy.None,
    Func<bool>? IsNightShopAvailable = null,
    LocalizedItemText? Name = null,
    LocalizedItemText? ShortDescription = null,
    LocalizedItemText? FlavorText = null)
{
    // Keep the existing constructor contract for other content Mods.
    public NpcTradeStockOptions? NpcTrade { get; init; }
}

internal sealed record NativeItemDeclaration(string OwnerId, string ItemId,
    NativeItemKind Kind, Delegate Factory, NativeItemOptions Options);
internal sealed record NativeItemBatchEntry(string ItemId, NativeItemKind Kind,
    Delegate Factory, NativeItemOptions? Options = null);

/// <summary>Process-wide ownership of native misc item IDs; no game objects are touched here.</summary>
internal sealed class NativeItemCatalog
{
    private static readonly Regex NamespacedId = new(
        "^[a-z][a-z0-9]*(\\.[a-z][a-z0-9_]*)+$", RegexOptions.CultureInvariant);
    private readonly Dictionary<string, NativeItemDeclaration> _items =
        new(StringComparer.Ordinal);

    internal int Count => _items.Count;

    internal SubmitResult Submit(string ownerId, string itemId, NativeItemKind kind,
        Delegate? factory, NativeItemOptions? options = null)
    {
        var result = Check(ownerId, itemId, kind, factory, options);
        if (result.Status != SubmitStatus.Accepted) return result;
        _items.Add(itemId, new(ownerId, itemId, kind, factory!, options ?? new()));
        return result;
    }

    internal SubmitResult SubmitBatch(string ownerId,
        IReadOnlyList<NativeItemBatchEntry> entries)
    {
        if (entries == null || entries.Count == 0)
            return new(SubmitStatus.Invalid, "Native item batch is empty");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        bool anyAccepted = false;
        foreach (var entry in entries)
        {
            if (!seen.Add(entry.ItemId))
                return new(SubmitStatus.Invalid,
                    $"Duplicate native item ID in batch: {entry.ItemId}");
            var result = Check(ownerId, entry.ItemId, entry.Kind,
                entry.Factory, entry.Options);
            if (result.Status is SubmitStatus.Invalid or SubmitStatus.Conflict)
                return result with { Reason = $"{entry.ItemId}: {result.Reason}" };
            anyAccepted |= result.Status == SubmitStatus.Accepted;
        }
        foreach (var entry in entries)
            if (!_items.ContainsKey(entry.ItemId))
                _items.Add(entry.ItemId, new(ownerId, entry.ItemId, entry.Kind,
                    entry.Factory, entry.Options ?? new()));
        return anyAccepted
            ? new(SubmitStatus.Accepted, "Native item batch staged atomically")
            : new(SubmitStatus.AlreadyPresent, "Native item batch already staged");
    }

    private SubmitResult Check(string ownerId, string itemId, NativeItemKind kind,
        Delegate? factory, NativeItemOptions? options)
    {
        if (string.IsNullOrWhiteSpace(ownerId) || !NamespacedId.IsMatch(ownerId))
            return new(SubmitStatus.Invalid, "Owner ID must be namespaced lowercase ASCII");
        if (string.IsNullOrWhiteSpace(itemId) || !NamespacedId.IsMatch(itemId) ||
            !itemId.StartsWith(ownerId + ".", StringComparison.Ordinal))
            return new(SubmitStatus.Invalid, "Native item ID must belong to its owner");
        if (!Enum.IsDefined(kind))
            return new(SubmitStatus.Invalid, "Native item kind is invalid");
        if (factory == null)
            return new(SubmitStatus.Invalid, "Native item factory is required");
        options ??= new();
        if (!Enum.IsDefined(options.NightShop))
            return new(SubmitStatus.Invalid, "Night-shop stock policy is invalid");
        if (options.NpcTrade is { } npc &&
            (!Enum.IsDefined(npc.Category) || npc.Category == NpcTradeStockCategory.None ||
             !float.IsFinite(npc.Weight) || npc.Weight <= 0))
            return new(SubmitStatus.Invalid, "NPC stock needs one category and a positive finite weight");
        if (_items.TryGetValue(itemId, out var old))
            return old.OwnerId == ownerId && old.Kind == kind && old.Factory.Equals(factory) &&
                   old.Options == options
                ? new(SubmitStatus.AlreadyPresent, "Same native item factory already staged")
                : new(SubmitStatus.Conflict, "Native item ID already reserved");
        return new(SubmitStatus.Accepted, "Native item factory staged; directory application pending");
    }

    internal IReadOnlyList<NativeItemDeclaration> Snapshot() =>
        _items.Values.OrderBy(item => item.ItemId, StringComparer.Ordinal).ToArray();

    internal bool TryGet(string itemId, out NativeItemDeclaration? declaration) =>
        _items.TryGetValue(itemId, out declaration);
}
