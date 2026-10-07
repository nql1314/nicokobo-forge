using System.Text.Json.Nodes;
using Nicokobo.Forge;

internal static class MissingItemSaveChecks
{
    internal static void Run()
    {
        int checks = 0;
        void Check(bool pass, string message) { checks++; if (!pass) throw new Exception("Save compatibility: " + message); }
        void Reject(Action action, string message)
        {
            try { action(); }
            catch (Exception ex) when (ex is InvalidDataException or OverflowException) { checks++; return; }
            throw new Exception("Save compatibility: " + message);
        }
        JsonObject Node(string id, long uuid, int uniqueId, long value = 0, int units = 1,
            bool owned = true, params (long Uuid, int Slot)[] children) => new()
        {
            ["identifier"] = id, ["uuid"] = uuid, ["uniqueId"] = uniqueId,
            ["itemType"] = "GameItem", ["unitValue"] = value, ["unitCount"] = units,
            ["childItems"] = new JsonArray(children.Select(child => JsonValue.Create(child.Uuid) as JsonNode).ToArray()),
            ["childItemInventoryNode"] = new JsonArray(children.Select(child => JsonValue.Create(child.Slot) as JsonNode).ToArray()),
            ["_keys"] = new JsonArray(owned ? [JsonValue.Create("IS_OWNED_TAG"), JsonValue.Create("BATTERY_CHARGE")] : []),
            ["_values"] = new JsonArray(owned ? [new JsonObject(), new JsonObject { ["valueInt"] = 37 }] : []),
            ["itemShape"] = new JsonObject { ["width"] = 2, ["orientation"] = 3 },
            ["itemModifiedShape"] = new JsonObject { ["positionX"] = 4, ["positionY"] = 5 },
            ["name"] = "原名", ["extraFutureField"] = new JsonObject { ["custom"] = 123 }
        };
        string Graph(params JsonObject[] nodes) => new JsonObject
            { ["saveItems"] = new JsonArray(nodes.Select(node => node as JsonNode).ToArray()), ["extraGraphField"] = "kept" }.ToJsonString();
        JsonArray Items(string graph) => JsonNode.Parse(graph)!["saveItems"]!.AsArray();
        bool Available(string id, string type) => !id.StartsWith("missing.") && type is "GameItem" or "GameCharacterItem";
        MissingItemSavePlan Plan(string json, int cash = 100) => MissingItemSaveMigration.Plan(
            new Dictionary<string, string?> { ["mainInvJSON"] = json }, [], cash, Available);

        string vanilla = Graph(Node("save_bag", 0, 100, children: [(9, 1)]), Node("battery", 9, 101, 20));
        var untouched = Plan(vanilla);
        Check(untouched.Inventories.Count == 0 && untouched.Refund == 0 && untouched.Recoveries.Count == 0 && untouched.Cash == 100,
            "Vanilla/registered items changed");
        string graph = Graph(Node("save_bag", 0, 100, children: [(20, 1), (100, 1)]),
            Node("missing.machine", 20, 201, 150, children: [(31, 7), (50, 8), (70, 9)]),
            Node("battery", 31, 202, 25), Node("registered.module", 50, 203, 90),
            Node("missing.food", 70, 204, 58, 3), Node("quartz", 100, 205, 6, 8));
        var plan = Plan(graph);
        Check(plan.Cash == 424 && plan.Refund == 324 && plan.RemovedNodes == 2, "Saved value/count refund is wrong");
        Check(plan.Recoveries.Count == 2, "Registered battery/module were liquidated or lost");
        var kept = Items(plan.Inventories["mainInvJSON"]);
        Check(kept.Count == 2 && kept[1]!["uuid"]!.GetValue<long>() == 100 && kept[1]!["identifier"]!.GetValue<string>() == "quartz",
            "Unrelated items or non-contiguous UUIDs changed");
        Check(kept[0]!["childItems"]!.ToJsonString() == "[100]" && kept[0]!["childItemInventoryNode"]!.ToJsonString() == "[1]",
            "Deleted child links/slots are not aligned");
        foreach (var restored in plan.Recoveries)
        {
            var node = Items(restored)[0]!;
            Check(node["name"]!.GetValue<string>() == "原名" && node["extraFutureField"]!["custom"]!.GetValue<int>() == 123 &&
                node["_values"]![1]!["valueInt"]!.GetValue<int>() == 37 && node["itemShape"]!["orientation"]!.GetValue<int>() == 3,
                "Restored identity/charge/quality/shape/custom state changed");
        }
        var reload = MissingItemSaveMigration.Plan(plan.Inventories.ToDictionary(pair => pair.Key, pair => (string?)pair.Value),
            plan.Recoveries, plan.Cash, Available);
        Check(reload.Cash == plan.Cash && reload.Refund == 0 && reload.Inventories.Count == 0 &&
            reload.Recoveries.SequenceEqual(plan.Recoveries), "Save/reload or full inventory duplicates refunds/recoveries");
        var reinstalled = MissingItemSaveMigration.Plan(new Dictionary<string, string?> { ["mainInvJSON"] = graph }, [], 100, (_, _) => true);
        Check(reinstalled.Inventories.Count == 0 && reinstalled.Recoveries.Count == 0 && reinstalled.Refund == 0,
            "Installed provider's content removed");

        string nested = Graph(Node("save_bag", 0, 100, children: [(10, 1)]),
            Node("missing.machine", 10, 201, 150, children: [(30, 5)]),
            Node("box", 30, 202, 40, children: [(80, 1), (90, 1)]),
            Node("missing.food", 80, 203, 20, children: [(100, 2)]),
            Node("milk", 90, 204, 30, 2), Node("registered.module", 100, 205, 90));
        var nestedPlan = Plan(nested);
        Check(nestedPlan.Refund == 170 && nestedPlan.Recoveries.Count == 2, "Nested normal container subtree recovery failed");
        var box = Items(nestedPlan.Recoveries.Single(json => Items(json)[0]!["identifier"]!.GetValue<string>() == "box"));
        Check(box.Count == 2 && box[0]!["childItems"]!.ToJsonString() == "[90]" && box[0]!["childItemInventoryNode"]!.ToJsonString() == "[1]",
            "Known child's own inventory links/state changed");
        var pendingMissing = MissingItemSaveMigration.Plan(new Dictionary<string, string?>(), nestedPlan.Recoveries,
            nestedPlan.Cash, (id, type) => Available(id, type) && id != "box");
        Check(pendingMissing.Refund == 40 && pendingMissing.Recoveries.Count == 2,
            "A provider removed while normal items are pending loses registered contents");
        Check(pendingMissing.Recoveries.Any(json => Items(json)[0]!["identifier"]!.GetValue<string>() == "milk"),
            "Normal contents of a newly missing pending container lost");
        var sold = MissingItemSaveMigration.Plan(new Dictionary<string, string?> { ["soldInvJSON"] = graph }, [], 100, Available);
        Check(sold.Refund == 0 && sold.Recoveries.Count == 0 && sold.Inventories.Count == 1, "Sold items generated player money/items");
        string unowned = Graph(Node("save_bag", 0, 100, children: [(1, 1)]), Node("missing.food", 1, 201, 20, owned: false));
        Check(Plan(unowned).Refund == 0 && Plan(unowned).RemovedNodes == 1, "Non-owned item refunded");
        string detached = Graph(Node("save_bag", 0, 100), Node("missing.food", 10, 201, 999, children: [(20, 1)]), Node("battery", 20, 202, 25));
        Check(Plan(detached).Refund == 0 && Plan(detached).Recoveries.Count == 0, "Unreachable entries minted credit/items");
        var duplicate = MissingItemSaveMigration.Plan(new Dictionary<string, string?> { ["mainInvJSON"] = graph, ["backInvJSON"] = graph }, [], 100, Available);
        Check(duplicate.Refund == plan.Refund && duplicate.Recoveries.Count == plan.Recoveries.Count,
            "Duplicate saved identity refunded/recovered twice across inventories");
        string badLink = graph.Replace("[20,100]", "[20,999]");
        Reject(() => Plan(badLink), "Dangling saved link accepted");
        var badSlots = JsonNode.Parse(graph)!;
        badSlots["saveItems"]![0]!["childItemInventoryNode"] = new JsonArray(1);
        Reject(() => Plan(badSlots.ToJsonString()), "Misaligned inventory nodes accepted");
        Reject(() => Plan(graph, int.MaxValue), "Credit overflow silently lost value");
        string overflow = Graph(Node("save_bag", 0, 100, children: [(1, 1)]), Node("missing.food", 1, 201, long.MaxValue, 2));
        Reject(() => Plan(overflow), "Long refund overflow accepted");
        var character = JsonNode.Parse(vanilla)!;
        character["saveItems"]![1]!["itemType"] = "GameCharacterItem";
        Check(Plan(character.ToJsonString()).RemovedNodes == 0, "Character directory type discarded");

        var providers = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["missing.machine"] = "RemovedContent", ["missing.food"] = "RemovedContent",
            ["destiny_dice"] = "WagePerks"
        };
        var declarations = new HashSet<string>(StringComparer.Ordinal);
        var installed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Nicokobo.Forge", "WagePerks" };
        bool Keep(string id, string type) => !MissingItemSavePolicy.ConfirmedMissing(id, type,
            Available(id, type) && id != "destiny_dice", providers, declarations, installed, true);
        MissingItemSavePlan SafePlan(string json, Func<string, string, bool>? keep = null) => MissingItemSaveMigration.Plan(
            new Dictionary<string, string?> { ["mainInvJSON"] = json }, [], 100, keep ?? Keep);
        string diceGraph = Graph(Node("save_bag", 0, 100, children: [(1, 1)]), Node("destiny_dice", 1, 74));
        Check(SafePlan(diceGraph).Inventories.Count == 0 && SafePlan(diceGraph).Refund == 0,
            "Installed provider's unregistered die was removed/refunded");
        var absentPlan = SafePlan(graph);
        Check(absentPlan.Cash == plan.Cash && absentPlan.Refund == plan.Refund &&
            absentPlan.Recoveries.SequenceEqual(plan.Recoveries),
            "Confirmed removed provider no longer refunds or preserves normal descendants");
        providers.Remove("destiny_dice");
        installed.Remove("WagePerks");
        Check(SafePlan(diceGraph).Inventories.Count == 0 && SafePlan(diceGraph).Refund == 0,
            "Unattributed third-party die was deleted merely because Has was false");
        var legacy = SafePlan(graph, (id, type) => !MissingItemSavePolicy.ConfirmedMissing(id, type,
            Available(id, type), new Dictionary<string, string>(), declarations, installed, true));
        Check(legacy.Inventories.Count == 0 && legacy.Cash == 100 && legacy.Recoveries.Count == 0,
            "Legacy save without provider evidence was destructively migrated");
        declarations.Add("missing.machine");
        var staged = SafePlan(graph);
        Check(staged.RemovedNodes == 1 && staged.Refund == 174 &&
            Items(staged.Inventories["mainInvJSON"]).Any(node => node!["identifier"]!.GetValue<string>() == "missing.machine"),
            "Current declaration was treated as an uninstalled Mod");
        declarations.Clear();
        installed.Add("RemovedContent");
        Check(SafePlan(graph).Inventories.Count == 0 && SafePlan(graph).Cash == 100,
            "Installed DLL whose Mod failed to load/register was liquidated");
        installed.Remove("RemovedContent");
        var unreadable = SafePlan(graph, (id, type) => !MissingItemSavePolicy.ConfirmedMissing(id, type,
            Available(id, type), providers, declarations, installed, false));
        Check(unreadable.Inventories.Count == 0 && unreadable.Cash == 100,
            "Incomplete installation scan authorized deletion");
        Check(!MissingItemSavePolicy.ConfirmedMissing("missing.machine", "GameItem", true,
            providers, declarations, installed, true), "Existing replacement factory was rejected due to an old provider record");
        Check(!MissingItemSavePolicy.ConfirmedMissing("missing.machine", "GameCharacterItem", false,
            providers, declarations, installed, true), "Ordinary item provider evidence was used to delete a character");
        var progressedDie = Node("destiny_dice", 20, 74);
        progressedDie["_keys"]!.AsArray().Add("destinyDiceValue");
        progressedDie["_values"]!.AsArray().Add(new JsonObject { ["valueInt"] = 2345 });
        string mixedGraph = Graph(Node("save_bag", 0, 100, children: [(10, 1), (20, 1)]),
            Node("missing.food", 10, 201, 58, 3), progressedDie);
        var mixed = SafePlan(mixedGraph);
        var mixedDie = Items(mixed.Inventories["mainInvJSON"])[1]!;
        var originalDie = Items(mixedGraph)[2]!;
        Check(mixed.Refund == 174 && mixed.RemovedNodes == 1 && mixedDie.ToJsonString() == originalDie.ToJsonString(),
            "Cleaning verified missing food changed the unknown die's full saved state");
        var mixedReload = MissingItemSaveMigration.Plan(mixed.Inventories.ToDictionary(pair => pair.Key, pair => (string?)pair.Value),
            mixed.Recoveries, mixed.Cash, Keep);
        Check(mixedReload.Inventories.Count == 0 && mixedReload.Cash == mixed.Cash && mixedReload.Refund == 0,
            "Conservative cleanup repeated refunds or changed the unknown item on reload");
        var parsedProviders = MissingItemSavePolicy.ReadProviders("{\"missing.food\":\"RemovedContent\"}");
        Check(parsedProviders["missing.food"] == "RemovedContent" && MissingItemSavePolicy.ReadProviders(null).Count == 0,
            "Saved provider identity did not round-trip or legacy absence was rejected");
        Reject(() => MissingItemSavePolicy.ReadProviders("{\"missing.food\":\"A\",\"missing.food\":\"B\"}"),
            "Ambiguous provider identity authorized deletion");
        Reject(() => MissingItemSavePolicy.ReadProviders("{\"missing.food\":\"\"}"), "Empty provider was accepted");
        Reject(() => MissingItemSavePolicy.ReadProviders("{\"missing.food\":42}"), "Non-string provider was accepted");
        Reject(() => MissingItemSavePolicy.ReadProviders("[]"), "Non-object provider map was accepted");
        Console.WriteLine($"Missing item save compatibility: {checks} assertions passed (native JSON planning only).");
    }
}
