namespace Nicokobo.Forge.Registration;

internal static class NativeNpcStockPolicy
{
    internal const string NativeMinerOreId = "common_ore";
    internal const NpcTradeStockCategory All = NpcTradeStockCategory.Material |
        NpcTradeStockCategory.Ore | NpcTradeStockCategory.Module |
        NpcTradeStockCategory.Machine | NpcTradeStockCategory.Household |
        NpcTradeStockCategory.Food | NpcTradeStockCategory.Medical;

    // The native miner supplies common_ore. Keep that choice at weight 1 and
    // add registered ores to this one draw, shared by every item in the batch.
    internal static string? SelectMinerOre(IReadOnlyList<NativeItemDeclaration> offers, double sample)
    {
        if (!double.IsFinite(sample) || sample < 0 || sample >= 1) return null;
        double remaining = sample * (1 + offers.Where(offer => Matches(offer, NpcTradeStockCategory.Ore, null))
            .Sum(offer => (double)offer.Options.NpcTrade!.Weight));
        if (remaining < 1) return NativeMinerOreId;
        remaining -= 1;
        foreach (var offer in offers)
        {
            if (!Matches(offer, NpcTradeStockCategory.Ore, null)) continue;
            remaining -= offer.Options.NpcTrade!.Weight;
            if (remaining < 0) return offer.ItemId;
        }
        return null;
    }

    internal static NativeItemDeclaration? Select(
        IReadOnlyList<NativeItemDeclaration> offers,
        NpcTradeStockCategory categories, double sample,
        IReadOnlySet<string>? ownedItemIds = null)
    {
        if (!double.IsFinite(sample) || sample < 0 || sample >= 1) return null;
        double total = offers.Where(offer => Matches(offer, categories, ownedItemIds))
            .Sum(offer => (double)offer.Options.NpcTrade!.Weight);
        double remaining = sample * total;
        foreach (var offer in offers)
        {
            if (!Matches(offer, categories, ownedItemIds)) continue;
            remaining -= offer.Options.NpcTrade!.Weight;
            if (remaining < 0) return offer;
        }
        return null;
    }

    private static bool Matches(NativeItemDeclaration offer,
        NpcTradeStockCategory categories, IReadOnlySet<string>? ownedItemIds) =>
        offer.Options.NpcTrade is { } stock &&
        (stock.Category & categories) != 0 &&
        (!stock.SkipWhenOwned || ownedItemIds?.Contains(offer.ItemId) != true);
}
