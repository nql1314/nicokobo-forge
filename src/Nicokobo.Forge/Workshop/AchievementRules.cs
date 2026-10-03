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
    internal static readonly string[] Tools = ["flashlight", "screwdriver", "wire_cutter", "welder", "metal_scanner"];
    internal static readonly string[] Factions = ["FACTION_LOWER_LEVEL", "FACTION_UPPER_LEVEL",
        "FACTION_SECURITY", "FACTION_BLACK_MARKET", "FACTION_REVOLUTION", "FACTION_CARTEL"];
    internal static readonly AchievementDefinition[] Definitions =
    [
        new("pure_water_jug", "一桶纯水", "A Jug of Pure Water", "持有一桶装满的原生纯水。", "Own a full jug of native pure-grade water.",
            [new("water_filter_adv", "高级滤水器", "Advanced Water Filter")], -.70f, -.76f),
        new("rat_800g", "巨鼠", "Giant Rat", "持有一只存活且实际重量至少 800g 的老鼠。", "Own one live rat weighing at least 800g.",
            [new("serum_green", "牲畜免疫血清", "Livestock Immunity Serum", ForgeNumbers.Achievements.SerumCount),
             new("serum_red", "牲畜类固醇血清", "Livestock Steroid Serum", ForgeNumbers.Achievements.SerumCount)], -.22f, -.76f),
        new("quality_overload", "极限调校", "Perfect Metallurgy", "持有一块原生完美品质金属锭。", "Own a native perfect-quality metal ingot.",
            [new("system_capped_neural_core", "神经核心模组(受限)", "Capped Neural Core Module")], .26f, -.76f),
        new("master_brewer", "酿酒大师", "Master Brewer", "持有一瓶陈化至少 10 天的琼浆玉液荧光酒。", "Own a Master Vintage BloomBerry Wine aged at least 10 days.",
            [new("wine_yeast_infinite", "酵母生物反应器", "Yeast Bioreactor")], .74f, -.76f),
        new("scavenger_toolset", "拾荒达人", "Scavenging Specialist", "同时持有手电筒、螺丝刀、剪线钳、等离子切割机和拾荒扫描仪。", "Own all five native scavenging tools at the same time.",
            [new("backpack_large", "背包(大)", "Large Backpack")], -.65f, -.15f),
        new("max_scav_scanner", "顶配拾荒仪", "Fully Upgraded Scanner", "持有原生双份收获概率达到 100 的拾荒扫描仪。", "Own a native scanner upgraded to 100 double-loot chance.",
            [new("scav_token", "拾荒者信物", "Scavenger Token")], -.65f, .38f),
        new("wealthy_store", "十万家业", "A Hundred Thousand", "原生总资产估值超过 100,000 信用点。", "Native total estimated assets must exceed 100,000 credits.",
            [new("machine_bay_ext", "机器区(扩建)", "Extended Machine Bay")], .68f, -.15f),
        new("store_attractiveness", "千分名店", "A Thousand Attractions", "原生当前店铺吸引力至少 1,000。", "Reach at least 1,000 native store attractiveness.",
            [new("storage_bay_large", "存储箱(扩建)", "Extended Storage Bay")], .68f, .38f),
        new("faction_max_rep", "派系名望", "Faction Renown", "任意一个原生派系实际声望达到上限 200。", "Reach 200 actual reputation with any native faction.",
            [new("smuggler_bay_mod", "走私者暗格(改进)", "Modified Smuggler's Bay")], .68f, .88f),
        new("workshop_master", "立业有成", "Established Proprietor", "当前周目直接买断店铺或还清房贷，触发原版成功结局。", "Trigger this run's native buyout or paid-mortgage victory ending.",
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
            english ? "Full jug + native pure grade" : "满桶容量 + 原生纯水档");
        float grams = owned.Where(x => x.Id == "rat" && x.LiveRat && float.IsFinite(x.RatGrams)).Select(x => x.RatGrams).DefaultIfEmpty().Max();
        ItemGoal("rat_800g", x => x.Id == "rat" && x.LiveRat && float.IsFinite(x.RatGrams) && x.RatGrams >= ForgeNumbers.Achievements.RatGrams,
            $"{grams:0.##} / {ForgeNumbers.Achievements.RatGrams:0}g");
        ItemGoal("quality_overload", x => x.Id == "metal_ingot" && x.PerfectIngot,
            english ? "Native INGOT_PURITY_PERFECT" : "原生完美品质金属锭");
        int age = owned.Where(x => IsWineBottle(x.Id) && x.MasterWine).Select(x => x.WineDays).DefaultIfEmpty().Max();
        ItemGoal("master_brewer", x => IsWineBottle(x.Id) && x.MasterWine && x.WineDays >= ForgeNumbers.Achievements.WineDays,
            english ? $"Master Vintage: {age} / 10 days" : $"琼浆玉液：{age} / 10 天");
        var tools = Tools.Select(id => owned.FirstOrDefault(x => x.Id == id)).Where(x => x != null).ToArray();
        result.Add("scavenger_toolset", new(tools.Length == Tools.Length, $"{tools.Length} / {Tools.Length}", tools.Select(x => x!.Uid).ToArray()));
        int chance = owned.Where(x => x.Id == "metal_scanner").Select(x => x.ScannerChance).DefaultIfEmpty().Max();
        ItemGoal("max_scav_scanner", x => x.Id == "metal_scanner" && x.ScannerChance >= ForgeNumbers.Achievements.ScannerChance, $"{chance} / 100");
        result.Add("wealthy_store", new(observation.Wealth > ForgeNumbers.Achievements.Wealth, $"{observation.Wealth:N0} / >100,000", []));
        result.Add("store_attractiveness", new(observation.Attractiveness >= ForgeNumbers.Achievements.Attractiveness, $"{observation.Attractiveness:N0} / 1,000", []));
        result.Add("faction_max_rep", new(double.IsFinite(observation.Reputation) && observation.Reputation >= ForgeNumbers.Achievements.Reputation,
            $"{observation.Reputation:0.##} / 200", []));
        result.Add("workshop_master", new(IsVictory(observation.Ending), IsVictory(observation.Ending) ? observation.Ending : (english ? "Awaiting native victory" : "等待本周目原版通关"), []));
        return result;
    }

    internal static bool IsVictory(string ending) => ending is "buyout" or "buyout_mortgage";
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
