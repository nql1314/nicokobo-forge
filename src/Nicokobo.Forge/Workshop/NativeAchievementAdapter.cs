using Il2Cpp;

namespace Nicokobo.Forge.Workshop;

internal static class NativeAchievementAdapter
{
    internal static AchievementItem Capture(GameItem item)
    {
        string id = item.identifier;
        bool owned = ForgeInventoryApi.IsPlayerOwned(item);
        if (!owned) return new(item.uniqueId, id, false, item.unitCount);
        // Only a tagged jug has a capacity tag; every other item must not read it.
        bool water = false;
        if (id == "water_jug" && item.IsTag("LIQUID_CONTAINER_TAG"))
        {
            int capacity = RequiredInt(item, "LIQUID_CONTAINER_CAPACITY");
            water = AchievementRules.IsFullWaterJug(WaterHelper.GetTotalVolume(item), capacity) &&
                WaterFeatureHelper.GetPurityArrayIndex(WaterHelper.GetWaterPurity(item)) == 0;
        }
        bool rat = id == "rat" && !HusbandryHelper.IsAnimalDead(item);
        float grams = rat ? RequiredFloat(item, "ANIMAL_WEIGHT_TAG") : 0;
        bool perfect = id == "metal_ingot" &&
            IngotPurityHelper.GetPurity(item) == "INGOT_PURITY_PERFECT";
        bool wine = AchievementRules.IsWineBottle(id) && WineHelper.IsFinishedWine(item) && !item.IsTag("WINE_COUNTERFEIT_TAG") &&
            WineHelper.GetWineQualityTier(item) == ForgeNumbers.Achievements.WineTopTier;
        int age = wine ? AgableHelper.GetAge(item) : 0;
        int scanner = id == "metal_scanner" ? RequiredInt(item, "SCAV_SCANNER_DOUBLE_CHANCE") : 0;
        return new(item.GetUniqueID(), id, owned, item.unitCount, water, rat, grams, perfect, wine, age, scanner);
    }

    // A missing native tag is a contract change, not an empty value: report it
    // explicitly so the caller records the goal as unavailable instead of
    // failing with an opaque NullReferenceException.
    private static int RequiredInt(GameItem item, string name) =>
        (item.GetTagReadonly(name) ?? throw new InvalidOperationException(
            $"Native achievement tag unavailable: {name}")).valueInt;
    private static float RequiredFloat(GameItem item, string name) =>
        (item.GetTagReadonly(name) ?? throw new InvalidOperationException(
            $"Native achievement tag unavailable: {name}")).valueFloat;

    internal static bool Relevant(string id) => id is "water_jug" or "rat" or "metal_ingot" or "wine_bloomberry" or "wine_bottle" || AchievementRules.Tools.Contains(id);
    internal static AchievementItem[] CaptureTools(PlayerStore store) => ForgeInventoryApi.CaptureRunItems(store)
        .Where(item => AchievementRules.Tools.Contains(item.identifier))
        .Select(item => new AchievementItem(item.GetUniqueID(), item.identifier, ForgeInventoryApi.IsPlayerOwned(item), item.unitCount)).ToArray();
    internal static string[] GoalsFor(string id) => id switch
    {
        "water_jug" => ["pure_water_jug"], "rat" => ["rat_800g"], "metal_ingot" => ["quality_overload"],
        "wine_bloomberry" or "wine_bottle" => ["master_brewer"], "metal_scanner" => ["max_scav_scanner", "scavenger_toolset"],
        _ => AchievementRules.Tools.Contains(id) ? ["scavenger_toolset"] : []
    };

    internal static AchievementObservation Capture(PlayerStore store, string ending, Dictionary<string, string> unavailable)
    {
        var items = new List<AchievementItem>();
        foreach (var item in ForgeInventoryApi.CaptureRunItems(store))
        {
            if (!Relevant(item.identifier)) continue;
            try { items.Add(Capture(item)); }
            catch (Exception ex)
            { foreach (string id in GoalsFor(item.identifier)) unavailable[id] = ex.GetType().Name; }
        }
        int wealth = 0, attractiveness = 0; double reputation = 0;
        try { wealth = store.GetTotalEstimatedValue(); }
        catch (Exception ex) { unavailable["wealthy_store"] = ex.GetType().Name; }
        try { attractiveness = store.GetCurrentStoreAttractiveness(); }
        catch (Exception ex) { unavailable["store_attractiveness"] = ex.GetType().Name; }
        try
        {
            if (store.storeReputations == null) throw new InvalidOperationException("Reputation list unavailable");
            foreach (var faction in store.storeReputations)
                if (faction != null && AchievementRules.Factions.Contains(faction.factionId) && StoreReputation.IsFactionValid(faction.factionId))
                    reputation = Math.Max(reputation, faction.GetReputationExact());
        }
        catch (Exception ex) { unavailable["faction_max_rep"] = ex.GetType().Name; }
        return new(items, wealth, attractiveness, reputation, ending);
    }
}
