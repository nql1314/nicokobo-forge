using System.Text;
using System.Text.Json;
using Nicokobo.Forge;
using Nicokobo.Forge.Workshop;
using Nicokobo.Forge.Registration;

internal static class AchievementChecks
{
    private static int _checks;
    private static void Expect(bool value, string reason) { _checks++; if (!value) throw new Exception(reason); }
    private static void Reject(Action action, string reason)
    {
        _checks++;
        try { action(); } catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or JsonException) { return; }
        throw new Exception(reason);
    }
    private static Dictionary<string, AchievementEvidence> Goals(params AchievementItem[] items) =>
        AchievementRules.Evaluate(new(items, 0, 0, 0), false);
    internal static void Run()
    {
        Expect(AchievementRules.Definitions.Length == 10, "Achievement catalog must have ten goals");
        Expect(!Goals(new AchievementItem(1, "water_jug", false, FullPureWater: true))["pure_water_jug"].Met, "Unowned proof qualified");
        Expect(!Goals(new AchievementItem(1, "water_jug", true))["pure_water_jug"].Met, "Water identifier alone qualified");
        Expect(Goals(new AchievementItem(1, "water_jug", true, FullPureWater: true))["pure_water_jug"].Met, "Native full pure water did not qualify");
        Expect(!Goals(new AchievementItem(1, "rat", true, LiveRat: true, RatGrams: 799.999f))["rat_800g"].Met, "Rounded rat weight qualified");
        Expect(!Goals(new AchievementItem(1, "rat", true, LiveRat: true, RatGrams: 400), new(2, "rat", true, LiveRat: true, RatGrams: 400))["rat_800g"].Met, "Rat weights were summed");
        Expect(!Goals(new AchievementItem(1, "rat", true, LiveRat: false, RatGrams: 800))["rat_800g"].Met, "Dead rat qualified");
        Expect(!Goals(new AchievementItem(1, "rat", true, LiveRat: true, RatGrams: float.NaN))["rat_800g"].Met, "Invalid weight qualified");
        Expect(Goals(new AchievementItem(1, "rat", true, LiveRat: true, RatGrams: 800))["rat_800g"].Met, "800g boundary failed");
        Expect(!Goals(new AchievementItem(1, "metal_ingot", true))["quality_overload"].Met, "Imperfect ingot qualified");
        Expect(Goals(new AchievementItem(1, "metal_ingot", true, PerfectIngot: true))["quality_overload"].Met, "Perfect native ingot failed");
        Expect(!Goals(new AchievementItem(1, "wine_bloomberry", true, MasterWine: true, WineDays: 9))["master_brewer"].Met, "Nine-day wine qualified");
        Expect(!Goals(new AchievementItem(1, "wine_bloomberry", true, MasterWine: true, WineDays: 1), new(2, "wine_bloomberry", true, WineDays: 15))["master_brewer"].Met,
            "Quality and age were taken from different bottles");
        Expect(Goals(new AchievementItem(1, "wine_bloomberry", true, MasterWine: true, WineDays: 10))["master_brewer"].Met, "Ten-day master wine failed");
        Expect(Goals(new AchievementItem(1, "wine_bottle", true, MasterWine: true, WineDays: 10))["master_brewer"].Met, "Native self-brewed wine was rejected by unchanged bottle ID");
        Expect(!Goals(new AchievementItem(1, "wine_gloomberry", true, MasterWine: true, WineDays: 10))["master_brewer"].Met, "Wrong wine identity qualified");
        var tools = AchievementRules.Tools.Select((id, i) => new AchievementItem(i + 1, id, true)).ToArray();
        Expect(Goals(tools)["scavenger_toolset"].Met, "Whole tool set failed");
        Expect(!Goals(tools[..4])["scavenger_toolset"].Met, "Incomplete tool set qualified");
        Expect(!Goals(tools.Select(item => item with { Uid = 1 }).ToArray())["scavenger_toolset"].Met, "Duplicate UID qualified as five tools");
        var empty = AchievementState.Empty("run-a", 0);
        var history = empty;
        foreach (var tool in tools) history = AchievementRules.Latch(history, Goals(tool));
        Expect(!history.Completed.ContainsKey("scavenger_toolset"), "Historical tools were unioned");
        Expect(!Goals(new AchievementItem(1, "metal_scanner", true, ScannerChance: 99))["max_scav_scanner"].Met, "Unfinished scanner qualified");
        Expect(Goals(new AchievementItem(1, "metal_scanner", true, ScannerChance: 100))["max_scav_scanner"].Met, "Max scanner failed");
        foreach (int assets in new[] { 99999, 100000, 100001 })
            Expect(AchievementRules.Evaluate(new([], assets, 0, 0), false)["wealthy_store"].Met == (assets > 100000), "Wealth boundary failed");
        Expect(!AchievementRules.Evaluate(new([], 0, 999, 199.999), false)["store_attractiveness"].Met, "Attractiveness below threshold qualified");
        var business = AchievementRules.Evaluate(new([], 0, 1000, 200), false);
        Expect(business["store_attractiveness"].Met && business["faction_max_rep"].Met, "Business boundaries failed");
        Expect(!AchievementRules.Evaluate(new([], 0, 0, 199.999), false)["faction_max_rep"].Met, "Rounded reputation qualified");
        foreach (string ending in new[] { "rent", "health", "cartel", "mortgage", "buyout", "buyout_mortgage" })
        {
            var proof = AchievementRules.Evaluate(new([], 0, 0, 0, ending), false);
            Expect(proof["workshop_master"].Met == (ending is "buyout" or "buyout_mortgage"), "Wrong native ending qualified");
            if (AchievementRules.IsVictory(ending))
                Expect(AchievementRules.Latch(empty, proof).Completed.Keys.SequenceEqual(new[] { "workshop_master" }), "Victory depended on other goals");
        }
        var state = AchievementRules.Latch(empty, Goals(new AchievementItem(5, "rat", true, LiveRat: true, RatGrams: 800)));
        Expect(state.Completed.ContainsKey("rat_800g") && state.Claimed.Count == 0, "Completion was also a claim");
        Expect(AchievementRules.Latch(state, Goals()).Completed.ContainsKey("rat_800g"), "Proof sale revoked completion");
        string json = AchievementRules.Encode(state);
        Expect(AchievementRules.Decode(json, "run-a", 0).Completed.ContainsKey("rat_800g"), "Completion did not round-trip");
        Reject(() => AchievementRules.Decode(json, "run-b", 0), "Cross-run state accepted");
        Reject(() => AchievementRules.Decode(json, "run-a", 1), "Cross-slot state accepted");
        Reject(() => AchievementRules.Decode(json.Replace("\"Schema\":1", "\"Schema\":2"), "run-a", 0), "Unknown schema accepted");
        Reject(() => AchievementRules.Decode(json.Replace("\"Schema\":1", "\"Schema\":1,\"Schema\":1"), "run-a", 0), "Duplicate state key accepted");
        var manifest = new PendingReward("rat_800g", Enumerable.Range(0, 10).Select(i => new RewardReceipt(i < 5 ? "serum_green" : "serum_red", 100 + i, 1)).ToArray());
        var pendingState = state with { Pending = manifest };
        string pendingJson = AchievementRules.Encode(pendingState);
        Expect(AchievementRules.Decode(pendingJson, "run-a", 0).Pending != null, "Interrupted delivery not preserved");
        Expect(AchievementRules.ReceiptsMatch(manifest, manifest.Items), "Complete delivery not recognized");
        Expect(!AchievementRules.ReceiptsMatch(manifest, manifest.Items[..9]), "Partial delivery recognized as complete");
        Expect(!AchievementRules.ReceiptsMatch(manifest, manifest.Items.Concat(manifest.Items[..1])), "Duplicate delivery UID accepted");
        Expect(!AchievementRules.ReceiptsMatch(manifest, manifest.Items.Select(x => x with { Units = 2 })), "Altered units accepted");
        Reject(() => AchievementRules.Decode(AchievementRules.Encode(state with { Pending = manifest with { Items = manifest.Items[..9] } }), "run-a", 0), "Partial manifest accepted");
        var claimed = AchievementRules.Claim(pendingState);
        Expect(claimed.Claimed.SetEquals(new[] { "rat_800g" }) && claimed.Pending == null, "Recovery did not finalize exactly one claim");
        Reject(() => AchievementRules.Claim(claimed), "Repeat claim accepted");
        Reject(() => AchievementRules.Claim(empty with { Pending = new("workshop_master", []) }), "Uncompleted goal claimed");
        var victory = AchievementRules.Latch(empty, AchievementRules.Evaluate(new([], 0, 0, 0, "buyout"), false));
        var badge = AchievementRules.Claim(victory with { Pending = new("workshop_master", []) });
        Expect(badge.Claimed.Contains("workshop_master"), "Inventory-free victory badge failed");
        Expect(AchievementRules.Claim(empty with { Pending = new(AchievementRules.CardNode, [new(AchievementRules.CardId, 50, 1)]) }).CardGranted, "Card recovery failed");
        SaveChecks(pendingJson, manifest);
        var catalog = new NativeItemCatalog(); Func<object> factory = () => new();
        Expect(catalog.Submit("nicokobo.aug", AchievementRules.CardId, NativeItemKind.Item, factory).Status == SubmitStatus.Invalid, "Foreign namespace accepted");
        Expect(catalog.Submit("nicokobo.forge", AchievementRules.CardId, NativeItemKind.Item, factory).Status == SubmitStatus.Accepted &&
            catalog.Snapshot().Single().OwnerId == "nicokobo.forge", "Forge card ownership failed");
        Expect(catalog.Submit("nicokobo.forge", "nicokobo.aug.nico_card", NativeItemKind.Item, factory).Status == SubmitStatus.Invalid, "Legacy ID was accepted as a Forge alias");
        Console.WriteLine($"Achievement checks passed: {_checks}");
    }

    private static void SaveChecks(string stateJson, PendingReward manifest)
    {
        byte[] Save(string run = "run-a", int slot = 0, string? state = null, bool detachLast = false, bool duplicateUid = false)
        {
            var nodes = new List<Dictionary<string, object>> { new() { ["childItems"] = Enumerable.Range(1, detachLast ? 9 : 10).ToArray() } };
            foreach (var item in manifest.Items) nodes.Add(new() { ["identifier"] = item.ItemId, ["uniqueId"] = duplicateUid ? 100 : item.Uid, ["unitCount"] = item.Units });
            return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
            {
                playerStore = new { __type = "PlayerStore,Assembly-CSharp", value = new
                {
                    runID = run, saveSlotId = slot, modData = new Dictionary<string, string> { [AchievementRules.SaveKey] = state ?? stateJson },
                    mainInvJSON = JsonSerializer.Serialize(new { saveItems = nodes })
                } }
            }));
        }
        Expect(AchievementSaveReadback.CompareBytes(Save(), "run-a", 0, stateJson, manifest).Matched, "Complete native slot not confirmed");
        Expect(AchievementSaveReadback.CompareBytes(Encoding.UTF8.GetBytes("\uFEFF").Concat(Save()).ToArray(), "run-a", 0, stateJson, manifest).Matched, "Native UTF-8 BOM not accepted");
        Expect(!AchievementSaveReadback.CompareBytes(Save(run: "run-b"), "run-a", 0, stateJson, manifest).Matched, "Wrong saved run confirmed");
        Expect(!AchievementSaveReadback.CompareBytes(Save(slot: 1), "run-a", 0, stateJson, manifest).Matched, "Wrong saved slot confirmed");
        Expect(!AchievementSaveReadback.CompareBytes(Save(state: "{}"), "run-a", 0, stateJson, manifest).Matched, "Stale saved state confirmed");
        Expect(!AchievementSaveReadback.CompareBytes(Save(detachLast: true), "run-a", 0, stateJson, manifest).Matched, "Detached reward confirmed");
        Expect(!AchievementSaveReadback.CompareBytes(Save(duplicateUid: true), "run-a", 0, stateJson, manifest).Matched, "Duplicated saved UID confirmed");
        Expect(!AchievementSaveReadback.CompareBytes(Encoding.UTF8.GetBytes("{}"), "run-a", 0, stateJson, manifest).Matched, "Malformed slot confirmed");
        Expect(!AchievementSaveReadback.CompareBytes([0xff], "run-a", 0, stateJson, manifest).Matched, "Invalid UTF-8 confirmed");
        string native = Encoding.UTF8.GetString(Save()).Replace("\"saveSlotId\":0", "\"saveSlotId\":0,\"saveSlotId\":0");
        Expect(!AchievementSaveReadback.CompareBytes(Encoding.UTF8.GetBytes(native), "run-a", 0, stateJson, manifest).Matched, "Duplicate native identity key confirmed");
        string path = Path.Combine(Path.GetTempPath(), "forge-achievement-check-" + Guid.NewGuid().ToString("N") + ".es3");
        try
        {
            File.WriteAllBytes(path, Save());
            Expect(AchievementSaveReadback.Compare(path, "run-a", 0, stateJson, manifest).Matched, "Actual file readback failed");
            File.WriteAllBytes(path, Save(state: "{}"));
            Expect(!AchievementSaveReadback.Compare(path, "run-a", 0, stateJson, manifest).Matched, "Memory state replaced actual file bytes");
        }
        finally { File.Delete(path); }
        string normalized = ForgeSaveReadbackApi.Normalize("{12:\"literal\\q\nline\"}");
        using var document = JsonDocument.Parse(normalized);
        Expect(document.RootElement.GetProperty("12").GetString() == "literal\\q\nline", "ES3 normalization changed string contents");
    }
}
