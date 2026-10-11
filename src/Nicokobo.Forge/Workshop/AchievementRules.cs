using System.Text.Json;

namespace Nicokobo.Forge.Workshop;

internal sealed record AchievementReward(string ItemId, string Chinese, string English, int Count = 1);
internal sealed record AchievementDefinition(string Id, string Chinese, string English,
    string ConditionChinese, string ConditionEnglish, AchievementReward[] Rewards, float X, float Y);
internal sealed record AchievementItem(int Uid, string Id, bool Owned, int Units = 1,
    bool FullPureWater = false, bool LiveRat = false, float RatGrams = 0,
    bool PerfectIngot = false, bool MasterWine = false, int WineDays = 0, int ScannerChance = 0);
internal sealed record AchievementObservation(IReadOnlyList<AchievementItem> Items,
    int Wealth, int Attractiveness, double Reputation, string Ending = "");
internal sealed record AchievementEvidence(bool Met, string Progress, int[] Uids);
internal sealed record RewardReceipt(string ItemId, int Uid, int Units);
internal sealed record AchievementCompletion(string Proof, int[] Uids);
internal sealed record PendingReward(string NodeId, RewardReceipt[] Items);
internal sealed record AchievementState(int Schema, string RunId, int SlotId,
    Dictionary<string, AchievementCompletion> Completed, HashSet<string> Claimed,
    bool CardGranted = false, PendingReward? Pending = null)
{
    internal static AchievementState Empty(string runId, int slotId) =>
        new(1, runId, slotId, new(StringComparer.Ordinal), new(StringComparer.Ordinal));
}

internal static class AchievementRules
{
    internal const string CardId = "nicokobo.forge.nico_card";
    internal const string CardNode = "$card";
    internal const string SaveKey = "nicokobo.forge.native_achievements";
    internal const string VictoryNode = "workshop_master";
    internal static readonly string[] Tools = ["flashlight", "screwdriver", "wire_cutter", "welder", "metal_scanner"];
    internal static readonly string[] Factions = ["FACTION_LOWER_LEVEL", "FACTION_UPPER_LEVEL",
        "FACTION_SECURITY", "FACTION_BLACK_MARKET", "FACTION_REVOLUTION", "FACTION_CARTEL"];
    internal static readonly AchievementDefinition[] Definitions =
    [
        new("pure_water_jug", "一桶纯水", "A Jug of Pure Water", "持有装满纯水的桶装水，水量须达到容器的最大容量。", "Own a Jug of Water filled to its maximum capacity with pure water.",
            [new("water_filter_adv", "高级滤水器", "Advanced Water Filter")], -.70f, -.76f),
        new("rat_800g", "巨鼠", "Giant Rat", "持有一只活老鼠，单只重量至少 800g。", "Own one live rat weighing at least 800g.",
            [new("serum_green", "牲畜免疫血清", "Livestock Immunity Serum", ForgeNumbers.Achievements.SerumCount),
             new("serum_red", "牲畜类固醇血清", "Livestock Steroid Serum", ForgeNumbers.Achievements.SerumCount)], -.22f, -.76f),
        new("quality_overload", "极限调校", "Perfect Metallurgy", "持有一块完美品质的金属锭，金属种类不限。", "Own one perfect-quality metal ingot of any metal.",
            [new("system_capped_neural_core", "神经核心模组(受限)", "Capped Neural Core Module")], .26f, -.76f),
        new("master_brewer", "酿酒大师", "Master Brewer", "持有一瓶琼浆玉液级荧光酒，须为真酒、完成发酵并陈化至少 10 天。", "Own a genuine Master Vintage BloomBerry Wine that has finished fermenting and aged for at least 10 days.",
            [new("wine_yeast_infinite", "酵母生物反应器", "Yeast Bioreactor")], .74f, -.76f),
        new("scavenger_toolset", "拾荒达人", "Scavenging Specialist", "同时持有手电筒、螺丝刀、剪线钳、等离子切割机和拾荒扫描仪。", "Own all five scavenging tools at the same time.",
            [new("backpack_large", "背包(大)", "Large Backpack")], -.65f, -.15f),
        new("max_scav_scanner", "顶配拾荒仪", "Fully Upgraded Scanner", "持有一台拾荒扫描仪，双份收获概率达到 100%。", "Own a scavenging scanner with a 100% double-loot chance.",
            [new("scav_token", "拾荒者信物", "Scavenger Token")], -.65f, .38f),
        new("wealthy_store", "十万家业", "A Hundred Thousand", "总资产估值超过 100,000 信用点。", "Have total estimated assets worth more than 100,000 credits.",
            [new("machine_bay_ext", "机器区(扩建)", "Extended Machine Bay")], .68f, -.15f),
        new("store_attractiveness", "千分名店", "A Thousand Attractions", "店铺吸引力达到 1,000 分。", "Reach at least 1,000 store attractiveness.",
            [new("storage_bay_large", "存储箱(扩建)", "Extended Storage Bay")], .68f, .38f),
        new("faction_max_rep", "派系名望", "Faction Renown", "下层区、上层区、治安部、黑市、革命军或卡特尔中，任意一个派系的声望达到上限（200）。", "Reach the reputation cap of 200 with the Lower Levels, Upper Levels, Security, Black Market, Revolutionaries or Cartel.",
            [new("smuggler_bay_mod", "走私者暗格(改进)", "Modified Smuggler's Bay")], .68f, .88f),
        new("workshop_master", "立业有成", "Established Proprietor", "在当前周目买断店铺或还清房贷，达成成功结局。", "Achieve a victory ending in this run by buying out the store or paying off its mortgage.",
            [], 0f, .18f)
    ];

    internal static Dictionary<string, AchievementEvidence> Evaluate(AchievementObservation observation, bool english)
    {
        var owned = observation.Items.Where(x => x.Owned && x.Uid > 0 && x.Units > 0)
            .GroupBy(x => x.Uid).Where(group => group.Count() == 1).Select(group => group.Single()).ToArray();
        var result = new Dictionary<string, AchievementEvidence>(StringComparer.Ordinal);
        void ItemGoal(string id, Func<AchievementItem, bool> predicate, string progress)
        {
            var item = owned.FirstOrDefault(predicate);
            result.Add(id, new(item != null, progress, item == null ? [] : [item.Uid]));
        }
        ItemGoal("pure_water_jug", x => x.Id == "water_jug" && x.FullPureWater,
            english ? "Fill a Jug of Water to capacity with pure water" : "桶装水须装满，水质达到纯水等级");
        float grams = owned.Where(x => x.Id == "rat" && x.LiveRat && float.IsFinite(x.RatGrams)).Select(x => x.RatGrams).DefaultIfEmpty().Max();
        ItemGoal("rat_800g", x => x.Id == "rat" && x.LiveRat && float.IsFinite(x.RatGrams) && x.RatGrams >= ForgeNumbers.Achievements.RatGrams,
            (english ? "Heaviest live rat: " : "最重的活鼠：") + $"{grams:0.##} / {ForgeNumbers.Achievements.RatGrams:0}g");
        ItemGoal("quality_overload", x => x.Id == "metal_ingot" && x.PerfectIngot,
            english ? "Own a perfect-quality metal ingot" : "持有一块完美品质金属锭");
        int age = owned.Where(x => IsWineBottle(x.Id) && x.MasterWine).Select(x => x.WineDays).DefaultIfEmpty().Max();
        ItemGoal("master_brewer", x => IsWineBottle(x.Id) && x.MasterWine && x.WineDays >= ForgeNumbers.Achievements.WineDays,
            english ? $"Master Vintage age: {age} / 10 days" : $"琼浆玉液级酒陈化：{age} / 10 天");
        var tools = Tools.Select(id => owned.FirstOrDefault(x => x.Id == id)).Where(x => x != null).ToArray();
        result.Add("scavenger_toolset", new(tools.Length == Tools.Length,
            $"{tools.Length} / {Tools.Length}" + (english ? " tools" : " 种工具"), tools.Select(x => x!.Uid).ToArray()));
        int chance = owned.Where(x => x.Id == "metal_scanner").Select(x => x.ScannerChance).DefaultIfEmpty().Max();
        ItemGoal("max_scav_scanner", x => x.Id == "metal_scanner" && x.ScannerChance >= ForgeNumbers.Achievements.ScannerChance, $"{chance}% / 100%");
        result.Add("wealthy_store", new(observation.Wealth > ForgeNumbers.Achievements.Wealth,
            english ? $"{observation.Wealth:N0} credits; must exceed 100,000" : $"{observation.Wealth:N0} 信用点，须超过 100,000", []));
        result.Add("store_attractiveness", new(observation.Attractiveness >= ForgeNumbers.Achievements.Attractiveness,
            $"{observation.Attractiveness:N0} / 1,000" + (english ? " points" : " 分"), []));
        result.Add("faction_max_rep", new(double.IsFinite(observation.Reputation) && observation.Reputation >= ForgeNumbers.Achievements.Reputation,
            $"{observation.Reputation:0.##} / 200", []));
        result.Add("workshop_master", new(IsVictory(observation.Ending), observation.Ending switch
        {
            "buyout" => english ? "Store bought out" : "已买断店铺",
            "buyout_mortgage" => english ? "Mortgage paid off" : "已还清房贷",
            _ => english ? "Buy out the store or pay off its mortgage in this run" : "等待本周目买断店铺或还清房贷"
        }, []));
        return result;
    }

    internal static bool IsFullWaterJug(int totalVolume, int capacity) =>
        capacity > 0 && totalVolume > 0 &&
        Math.Abs((long)totalVolume - capacity) <= ForgeNumbers.Achievements.WaterJugVolumeTolerance;

    internal static bool IsVictory(string ending) => ending is "buyout" or "buyout_mortgage";
    internal static bool HasVictoryBudgetReward(AchievementState? state) =>
        state?.Claimed.Contains(VictoryNode) == true;
    // Native self-brewing retains wine_bottle even after it becomes finished BloomBerry Wine.
    internal static bool IsWineBottle(string id) => id is "wine_bottle" or "wine_bloomberry";
    internal static AchievementState Latch(AchievementState state, IReadOnlyDictionary<string, AchievementEvidence> evidence)
    {
        var completed = new Dictionary<string, AchievementCompletion>(state.Completed, StringComparer.Ordinal);
        foreach (var (id, proof) in evidence)
            if (proof.Met && !completed.ContainsKey(id)) completed.Add(id, new(proof.Progress, proof.Uids));
        return state with { Completed = completed };
    }
    internal static AchievementState Claim(AchievementState state)
    {
        var pending = state.Pending ?? throw new InvalidOperationException("No pending reward");
        if (pending.NodeId == CardNode) return state with { CardGranted = true, Pending = null };
        if (!state.Completed.ContainsKey(pending.NodeId) || state.Claimed.Contains(pending.NodeId))
            throw new InvalidOperationException("Reward cannot be claimed in this state");
        var claimed = new HashSet<string>(state.Claimed, StringComparer.Ordinal) { pending.NodeId };
        return state with { Claimed = claimed, Pending = null };
    }
    internal static bool ReceiptsMatch(PendingReward pending, IEnumerable<RewardReceipt> owned)
    {
        var snapshot = owned.ToArray();
        return pending.Items.All(expected => snapshot.Count(x => x.Uid == expected.Uid) == 1 && snapshot.Contains(expected));
    }
    internal static string Encode(AchievementState state) => JsonSerializer.Serialize(state);
    internal static AchievementState Decode(string json, string runId, int slotId)
    {
        using var document = JsonDocument.Parse(json);
        RejectDuplicateKeys(document.RootElement);
        var state = JsonSerializer.Deserialize<AchievementState>(json) ?? throw new InvalidDataException("Missing achievement state");
        var ids = Definitions.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
        if (state.Schema != 1 || state.RunId != runId || state.SlotId != slotId || state.Completed == null || state.Claimed == null ||
            state.Completed.Any(x => !ids.Contains(x.Key) || x.Value == null || string.IsNullOrWhiteSpace(x.Value.Proof) ||
                x.Value.Uids == null || x.Value.Uids.Any(id => id <= 0)) ||
            state.Claimed.Any(x => !state.Completed.ContainsKey(x))) throw new InvalidDataException("Invalid achievement state or run identity");
        if (state.Pending is { } pending)
        {
            bool card = pending.NodeId == CardNode;
            var definition = Definitions.FirstOrDefault(x => x.Id == pending.NodeId);
            if (card ? state.CardGranted : definition == null || !state.Completed.ContainsKey(pending.NodeId) || state.Claimed.Contains(pending.NodeId))
                throw new InvalidDataException("Invalid pending reward owner");
            if (pending.Items == null || pending.Items.Length > ForgeNumbers.Achievements.MaxRewardItems ||
                pending.Items.Any(x => x.Uid <= 0 || x.Units != 1 || string.IsNullOrWhiteSpace(x.ItemId)) ||
                pending.Items.Select(x => x.Uid).Distinct().Count() != pending.Items.Length)
                throw new InvalidDataException("Invalid reward identities");
            var rewards = card ? new[] { new AchievementReward(CardId, "", "") } : definition!.Rewards;
            var expected = rewards.SelectMany(x => Enumerable.Repeat(x.ItemId, x.Count)).OrderBy(x => x, StringComparer.Ordinal);
            if (!pending.Items.Select(x => x.ItemId).OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(expected))
                throw new InvalidDataException("Reward manifest mismatch");
        }
        return state;
    }
    private static void RejectDuplicateKeys(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new InvalidDataException("Duplicate achievement key");
                RejectDuplicateKeys(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (var child in value.EnumerateArray()) RejectDuplicateKeys(child);
    }
}
