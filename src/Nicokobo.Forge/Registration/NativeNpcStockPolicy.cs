namespace Nicokobo.Forge.Registration;

internal static class NativeNpcStockPolicy
{
    internal const string NativeMinerOreId = "common_ore";
    internal const NpcTradeStockCategory All = NpcTradeStockCategory.Material |
        NpcTradeStockCategory.Ore | NpcTradeStockCategory.Module |
        NpcTradeStockCategory.Machine | NpcTradeStockCategory.Household |
        NpcTradeStockCategory.Food | NpcTradeStockCategory.Medical;

    // The native miner supplies common_ore. Its configured relative weight and
    // registered ores share one draw for every item in the batch.
    internal static string? SelectMinerOre(IReadOnlyList<NativeItemDeclaration> offers, double sample)
    {
        if (!double.IsFinite(sample) || sample < 0 || sample >= 1) return null;
        double nativeWeight = ForgeNumbers.NpcStock.NativeMinerOreWeight;
        double remaining = sample * (nativeWeight + offers.Where(offer => Matches(offer, NpcTradeStockCategory.Ore, null))
            .Sum(offer => (double)(offer.Options.NpcTrade!.MinerWeight ?? offer.Options.NpcTrade.Weight)));
        if (remaining < nativeWeight) return NativeMinerOreId;
        remaining -= nativeWeight;
        foreach (var offer in offers)
        {
            if (!Matches(offer, NpcTradeStockCategory.Ore, null)) continue;
            remaining -= offer.Options.NpcTrade!.MinerWeight ?? offer.Options.NpcTrade.Weight;
            if (remaining < 0) return offer.ItemId;
        }
        return null;
    }

    // A hard-coded native supply result participates as one weighted candidate.
    // Eligible Mod offers share one fixed budget equally, regardless of count.
    // A null selection preserves the native result instead of adding another item.
    internal static NativeItemDeclaration? SelectFromPool(
        IReadOnlyList<NativeItemDeclaration> offers,
        NpcTradeStockCategory categories, double sample,
        IReadOnlySet<string>? ownedItemIds = null)
    {
        if (!double.IsFinite(sample) || sample < 0 || sample >= 1) return null;
        var candidates = offers.Where(offer => Matches(offer, categories, ownedItemIds)).ToArray();
        if (candidates.Length == 0) return null;
        double nativeWeight = ForgeNumbers.NpcStock.NativeSupplyWeight;
        double total = nativeWeight + ForgeNumbers.NpcStock.ModSupplyWeight;
        double remaining = sample * total;
        if (remaining < nativeWeight) return null;
        remaining -= nativeWeight;
        int index = Math.Min((int)(remaining / ModSupplyItemWeight(candidates.Length)), candidates.Length - 1);
        return candidates[index];
    }

    internal static double ModSupplyItemWeight(int candidateCount) =>
        candidateCount > 0 ? ForgeNumbers.NpcStock.ModSupplyWeight / candidateCount : 0d;

    internal static NpcTradeStockCategory TableCategories(string? tableId) => tableId switch
    {
        "materialTable" => NpcTradeStockCategory.Material | NpcTradeStockCategory.Ore,
        "allModuleTable" or "t1moduleTable" or "t2moduleTable" => NpcTradeStockCategory.Module,
        "toolTable" => NpcTradeStockCategory.Machine,
        "householdTable" => NpcTradeStockCategory.Household,
        "packedFoodTable" => NpcTradeStockCategory.Food,
        "medicalTable" => NpcTradeStockCategory.Medical,
        _ => NpcTradeStockCategory.None
    };

    private static bool Matches(NativeItemDeclaration offer,
        NpcTradeStockCategory categories, IReadOnlySet<string>? ownedItemIds) =>
        offer.Options.NpcTrade is { } stock &&
        (stock.Category & categories) != 0 &&
        (!stock.SkipWhenOwned || ownedItemIds?.Contains(offer.ItemId) != true);
}
