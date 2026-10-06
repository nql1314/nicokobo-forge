using System.Reflection;
using Il2Cpp;
using Nicokobo.Forge;
using Nicokobo.Forge.Registration;

internal static class NpcStockChecks
{
    internal static void Run()
    {
        int checks = 0, nextPointer = 1000;
        void Expect(bool condition, string message)
        { checks++; if (!condition) throw new Exception("NPC supply pools: " + message); }
        object? Call(string name, params object?[] arguments) =>
            typeof(NativeNpcStockAdapter).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)!
                .Invoke(null, arguments);
        GameItem Item(string id, int units = 1, string type = "ITEM") => new()
        { Pointer = new(nextPointer++), identifier = id, unitCount = units, GameItemType = type };
        void Offer(string id, NpcTradeStockCategory category, float weight = 1,
            bool skipOwned = false, Func<GameItem>? factory = null, int minimumDay = 0,
            float? minerWeight = null, NpcTradeStockSupplier suppliers = NpcTradeStockSupplier.Any,
            bool nightShop = false)
        {
            NativeItemRegistry.Offers.Add(new("test.pool", id, NativeItemKind.Item,
                factory ?? (() => Item(id)), new() { NpcTrade = new(category, weight)
                    { SkipWhenOwned = skipOwned, MinimumDay = minimumDay, MinerWeight = minerWeight,
                        Suppliers = suppliers, IncludeInNightShop = nightShop } }));
        }
        void Reset()
        {
            NativeItemRegistry.Offers.Clear(); NativeItemRegistry.Outcomes.Clear();
            ForgeInventoryApi.OwnedItems.Clear(); RNG.Samples.Clear(); RNG.Calls = 0;
            StoreStation.Day = 1;
            PlayerStore.instance = new(); EmporiumEntry.Instance = new();
        }
        void Supply(string callback, Action body)
        {
            object?[] arguments = [null];
            Call(callback, arguments);
            try { body(); }
            finally { Call("EndSupply", arguments); }
        }
        void Inventor(bool visiting, Action body)
        {
            object?[] arguments = [visiting, null]; Call("InventorStockPrefix", arguments);
            try { body(); }
            finally { Call("EndSupply", arguments[1]); }
        }
        GameItem Place(GameItem item, bool owned = false)
        {
            object?[] arguments = [item, owned, null];
            if ((bool)Call("PlaceStockPrefix", arguments)!)
            {
                item = (GameItem)arguments[0]!;
                PlayerStore.instance!.AddDirectSellingItemToTable(item, owned, false, false, 0);
                Call("StockPlacedPostfix", arguments[2]);
            }
            return item;
        }
        Il2CppSystem.Collections.Generic.List<LootEntry> Merge(string table,
            Il2CppSystem.Collections.Generic.List<LootEntry> input)
        {
            object?[] context = [table, null]; Call("PoolRollPrefix", context);
            try
            {
                object?[] entries = [input]; Call("PoolPickPrefix", entries);
                return (Il2CppSystem.Collections.Generic.List<LootEntry>)entries[0]!;
            }
            finally { Call("EndPoolRoll", context[1]); }
        }
        GameItem Drawn(string table, string id)
        {
            object?[] context = [table, null]; Call("PoolRollPrefix", context);
            try { Call("PoolRollPostfix", id); }
            finally { Call("EndPoolRoll", context[1]); }
            var item = Item(id); Call("PoolItemPostfix", item); return item;
        }

        Reset(); Offer("test.pool.food", NpcTradeStockCategory.Food);
        NativeNpcStockAdapter.Configure(true, _ => { }); NativeNpcStockAdapter.Update();
        Expect(NativeNpcStockAdapter.Installed, "Fixture did not install the supply callbacks");
        Supply("FoodStockPrefix", () =>
        {
            RNG.Samples.Enqueue(0); RNG.Samples.Enqueue(0.9); RNG.Samples.Enqueue(0.9);
            var kept = Item("native.food"); var replaced = Item("native.food");
            Expect(ReferenceEquals(Place(kept), kept) && kept.DestroyCalls == 0,
                "Selecting native stock destroyed it or forced a Mod offer");
            Expect(Place(replaced).identifier == "test.pool.food" && replaced.DestroyCalls == 1,
                "Selecting a Mod did not replace exactly one stock result");
            Place(Item("native.food"));
            Expect(PlayerStore.instance!.Placements == 3 &&
                EmporiumEntry.Instance!.frontInvinvElement.childItems.Count == 3 &&
                EmporiumEntry.Instance.frontInvinvElement.childItems.Count(x => x.identifier == "test.pool.food") == 2,
                "Three native stock slots did not allow multiple Mod results without extra placements");
        });

        Reset(); Offer("test.pool.food", NpcTradeStockCategory.Food);
        Supply("FoodStockPrefix", () =>
        {
            RNG.Samples.Enqueue(0); RNG.Samples.Enqueue(0.49);
            Place(Item("native.food")); Place(Item("native.food"));
            Expect(EmporiumEntry.Instance!.frontInvinvElement.childItems.All(x => x.identifier == "native.food"),
                "Supply still guaranteed at least one custom item");
        });
        Place(Item("native.food"));
        Expect(RNG.Calls == 2, "Supply state leaked into a later placement");

        Reset(); Offer("test.pool.food", NpcTradeStockCategory.Food);
        Supply("FoodStockPrefix", () =>
        {
            var gift = Item("native.gift");
            Expect(ReferenceEquals(Place(gift, owned: true), gift) && RNG.Calls == 0,
                "A gift or owned-item placement participated in supply draws");
            var blank = Item(""); Call("PoolItemPostfix", blank);
            Expect(ReferenceEquals(Place(blank), blank) && RNG.Calls == 0,
                "Invalid native identifiers entered pool lookup or replacement");
        });

        Reset(); Offer("test.pool.food", NpcTradeStockCategory.Food);
        Supply("MedicalStockPrefix", () => Place(Item("native.medical")));
        Expect(RNG.Calls == 0 && EmporiumEntry.Instance!.frontInvinvElement.childItems[0].identifier == "native.medical",
            "A medical supplier selected an unrelated food candidate");
        NativeItemRegistry.Outcomes["test.pool.food"] = NativeApplicationStatus.Staged;
        Supply("FoodStockPrefix", () => Place(Item("native.food")));
        Expect(RNG.Calls == 0, "An unapplied factory entered supply draws");

        Reset(); Offer("test.pool.failed", NpcTradeStockCategory.Food,
            factory: () => throw new InvalidOperationException("factory failure"));
        Supply("FoodStockPrefix", () =>
        {
            RNG.Samples.Enqueue(0.9); var original = Item("native.food");
            Expect(ReferenceEquals(Place(original), original) && original.DestroyCalls == 0,
                "A failed factory removed native stock");
        });
        Expect(PlayerStore.instance!.Placements == 1, "A failed factory changed the stock count");

        Reset(); Offer("test.pool.gel", NpcTradeStockCategory.Medical, 0.125f);
        Offer("test.pool.food", NpcTradeStockCategory.Food);
        var entries = new Il2CppSystem.Collections.Generic.List<LootEntry>
        {
            new("native.medical", 1, ""), new("test.pool.gel", 0.125f, "test.pool")
        };
        Expect(ReferenceEquals(Merge("medicalTable", entries), entries), "Scavenging loot was merged outside supply");
        Supply("MedicalStockPrefix", () =>
        {
            var merged = Merge("medicalTable", entries);
            Expect(!ReferenceEquals(merged, entries) && entries.Count == 2,
                "Supply mutated the persistent loot list");
            Expect(merged.Count == 2 && merged.Single(x => x.id == "test.pool.gel").weight == 0.25f &&
                merged.Sum(x => x.weight) == 1.25f,
                "An existing custom item did not receive the shared supply budget exactly once");
            var original = Drawn("medicalTable", "native.medical");
            Expect(ReferenceEquals(Place(original), original) && RNG.Calls == 0,
                "A native result from the merged table was drawn again during placement");
            var custom = Drawn("medicalTable", "test.pool.gel");
            Expect(ReferenceEquals(Place(custom), custom) && RNG.Calls == 0,
                "A custom table result was drawn again during placement");
        });
        Expect(ReferenceEquals(Merge("medicalTable", entries), entries), "Pool context survived supply completion");

        Reset(); Offer("test.pool.conflict", NpcTradeStockCategory.Food,
            factory: () => throw new InvalidOperationException("A conflicting factory must not be used"));
        NativeItemRegistry.Outcomes["test.pool.conflict"] = NativeApplicationStatus.Conflict;
        Offer("test.pool.food", NpcTradeStockCategory.Food);
        var conflictingEntries = new Il2CppSystem.Collections.Generic.List<LootEntry>
        { new("native.food", 1, ""), new("test.pool.conflict", 0.7f, "foreign.owner"),
            new("foreign.food", 0.5f, "foreign.owner") };
        Supply("FoodStockPrefix", () =>
        {
            var merged = Merge("packedFoodTable", conflictingEntries);
            Expect(merged.Count == 4 &&
                merged.Single(entry => entry.id == "test.pool.conflict").weight == 0.7f &&
                merged.Single(entry => entry.id == "foreign.food").weight == 0.5f &&
                merged.Single(entry => entry.id == "test.pool.food").weight == 0.25f,
                "A foreign item with a conflicting Forge ID lost its own loot weight");
            var foreign = Item("test.pool.conflict");
            RNG.Samples.Enqueue(0.99);
            Expect(ReferenceEquals(Place(foreign), foreign) && foreign.DestroyCalls == 0 && RNG.Calls == 0,
                "A foreign item with a conflicting Forge ID was replaced during direct supply");
            RNG.Samples.Clear();
        });
        Expect(conflictingEntries.Count == 3 && conflictingEntries[1].weight == 0.7f &&
            ReferenceEquals(Merge("packedFoodTable", conflictingEntries), conflictingEntries),
            "Conflict isolation mutated the global loot table or leaked its supply scope");

        Reset(); Offer("test.pool.component", NpcTradeStockCategory.Material, 0.125f);
        Supply("MaterialStockPrefix", () =>
        {
            var native = new Il2CppSystem.Collections.Generic.List<LootEntry> { new("native.material", 1, "") };
            var merged = Merge("materialTable", native);
            Expect(merged.Any(x => x.id == "test.pool.component") && native.Count == 1,
                "An NPC-only candidate was missing from the supply pool or added to scavenging");
        });

        Reset(); Offer("test.pool.late_component", NpcTradeStockCategory.Material, 0.075f, minimumDay: 22);
        StoreStation.Day = 21;
        Supply("MaterialStockPrefix", () =>
        {
            var original = Item("native.material");
            Expect(ReferenceEquals(Place(original), original) && RNG.Calls == 0,
                "A locked material entered hard-coded stock before its minimum day");
        });
        StoreStation.Day = 22;
        Supply("MaterialStockPrefix", () =>
        {
            RNG.Samples.Enqueue(0.999);
            Expect(Place(Item("native.material")).identifier == "test.pool.late_component",
                "A material did not join hard-coded stock on its minimum day");
        });
        StoreStation.Day = 1;
        Supply("MaterialStockPrefix", () =>
        {
            var original = Item("native.material");
            Expect(ReferenceEquals(Place(original), original) && RNG.Calls == 1,
                "A later day's eligibility leaked into an earlier loaded run");
        });
        var materialEntries = new Il2CppSystem.Collections.Generic.List<LootEntry>
        {
            new("native.material", 1, ""), new("test.pool.late_component", 0.125f, "test.pool")
        };
        StoreStation.Day = 21;
        Supply("MaterialStockPrefix", () =>
            Expect(Merge("materialTable", materialEntries).Count == 1,
                "A table's registered entry bypassed the minimum supply day"));
        StoreStation.Day = 22;
        Supply("MaterialStockPrefix", () =>
        {
            var merged = Merge("materialTable", materialEntries);
            Expect(merged.Count == 2 && merged[1].weight == 0.25f,
                "An unlocked material did not receive the shared supply budget in the table pool");
        });
        Expect(materialEntries.Count == 2 && materialEntries[1].weight == 0.125f,
            "Supply-day filtering changed the global scavenging table");

        Reset();
        for (int index = 0; index < 12; index++)
            Offer("test.pool.food." + index, NpcTradeStockCategory.Food, 0.06f);
        Offer("test.pool.quartz", NpcTradeStockCategory.Ore, 0.3f);
        Offer("test.pool.titanium", NpcTradeStockCategory.Ore, 0.3f);
        Offer("test.pool.gel", NpcTradeStockCategory.Medical, 0.075f);
        for (int index = 0; index < 7; index++)
            Offer("test.pool.late." + index, NpcTradeStockCategory.Material, 0.075f, minimumDay: 22);
        Offer("test.pool.unapplied", NpcTradeStockCategory.Food, 10f);
        NativeItemRegistry.Outcomes["test.pool.unapplied"] = NativeApplicationStatus.Staged;
        Offer("test.pool.card", NpcTradeStockCategory.Household, 0.05f, skipOwned: true);
        ForgeInventoryApi.OwnedItems.Add(new() { identifier = "test.pool.card", Owned = true });
        Supply("GeneralStockPrefix", () =>
        {
            var source = new Il2CppSystem.Collections.Generic.List<LootEntry>
            {
                new("native.food", 2f, ""), new("foreign.food", 0.5f, "foreign.owner"),
                new("test.pool.food.0", 0.1f, "test.pool")
            };
            var merged = Merge("packedFoodTable", source);
            var foods = merged.Where(x => x.id.StartsWith("test.pool.food.")).ToArray();
            Expect(merged.Count == 14 && foods.Length == 12 &&
                foods.All(x => Math.Abs(x.weight - 0.25 / 12) < 1e-6) &&
                Math.Abs(foods.Sum(x => (double)x.weight) - 0.25) < 1e-6,
                "Food-table budget grew with candidate count, used per-item weights, or included unapplied stock");
            Expect(merged[0].weight == 2f && merged[1].weight == 0.5f &&
                source.Count == 3 && source[2].weight == 0.1f,
                "Shared supply weights changed native/foreign entries or the global loot list");
            foreach (int day in new[] { 21, 22, 21 })
            {
                StoreStation.Day = day;
                var materials = Merge("materialTable", new()
                    { new("native.material", 1f, "") }).Where(x => x.id.StartsWith("test.pool.")).ToArray();
                int count = day == 22 ? 9 : 2;
                Expect(materials.Length == count &&
                    materials.All(x => Math.Abs(x.weight - 0.25 / count) < 1e-6) &&
                    Math.Abs(materials.Sum(x => (double)x.weight) - 0.25) < 1e-6,
                    "Material unlock/relock did not redistribute the same table budget on day " + day);
            }
            foreach (int day in new[] { 1, 22 })
            {
                StoreStation.Day = day;
                RNG.Samples.Enqueue(0.799999);
                var original = Item("native.stock");
                Expect(ReferenceEquals(Place(original), original),
                    "More eligible goods increased the direct supply's Mod probability on day " + day);
                int count = day == 22 ? 22 : 15;
                var selected = new HashSet<string>(StringComparer.Ordinal);
                for (int index = 0; index < count; index++)
                {
                    RNG.Samples.Enqueue((1 + 0.25 * (index + 0.5) / count) / 1.25);
                    selected.Add(Place(Item("native.stock")).identifier);
                }
                Expect(selected.Count == count && selected.All(id => id.StartsWith("test.pool.")) &&
                    !selected.Contains("test.pool.unapplied") && !selected.Contains("test.pool.card") &&
                    (day == 22 || selected.All(id => !id.StartsWith("test.pool.late."))),
                    "Direct supply did not divide the budget equally after eligibility filters on day " + day);
            }
        });

        Reset();
        NativeItemRegistry.Offers.Add(new("test.pool", "test.pool.machine", NativeItemKind.Item,
            (Func<GameItem>)(() => Item("test.pool.machine")), new()));
        NativeItemRegistry.Offers.Add(new("test.pool", "test.pool.module", NativeItemKind.Module,
            (Func<GameItem>)(() => Item("test.pool.module")), new()));
        Supply("GeneralStockPrefix", () =>
        {
            foreach (var (table, id) in new[] { ("toolTable", "test.pool.machine"),
                         ("t2moduleTable", "test.pool.module") })
            {
                var source = new Il2CppSystem.Collections.Generic.List<LootEntry>
                    { new("native.item", 1, ""), new(id, 0.04f, "test.pool") };
                var merged = Merge(table, source);
                Expect(merged.Count == 1 && merged[0].id == "native.item" && source.Count == 2,
                    "A Mod without NPC eligibility remained in the native supply table: " + table);
            }
        });

        Reset(); Offer("test.pool.card", NpcTradeStockCategory.Household, 0.05f, skipOwned: true);
        var cardEntries = new Il2CppSystem.Collections.Generic.List<LootEntry>
        { new("native.household", 1, ""), new("test.pool.card", 0.05f, "test.pool") };
        ForgeInventoryApi.OwnedItems.Add(new() { identifier = "test.pool.card", Owned = true });
        Supply("HouseholdStockPrefix", () =>
        {
            Expect(Merge("householdTable", cardEntries).All(x => x.id != "test.pool.card"),
                "An already-owned card remained in the NPC table pool");
            Place(Item("native.household"));
            Expect(RNG.Calls == 0, "Owned card filtering did not apply to hard-coded stock");
            ForgeInventoryApi.OwnedItems.Clear();
            Expect(Merge("householdTable", cardEntries).Any(x => x.id == "test.pool.card"),
                "A lost card did not become eligible during the next live pool merge");
        });

        Reset(); Offer("test.pool.food", NpcTradeStockCategory.Food);
        Supply("FoodStockPrefix", () =>
        {
            object?[] nightContext = [false, null]; Call("InventorStockPrefix", nightContext);
            try
            {
                Place(Item("native.night"));
                Expect(RNG.Calls == 0, "A nested night shop inherited daytime pool selection");
            }
            finally { Call("EndSupply", nightContext[1]); }
            RNG.Samples.Enqueue(0.9);
            Expect(Place(Item("native.food")).identifier == "test.pool.food",
                "Nested supply completion did not restore the outer pool");
        });
        try { Supply("FoodStockPrefix", () => throw new InvalidOperationException("supply failure")); }
        catch (InvalidOperationException) { }
        Place(Item("native.food"));
        Expect(RNG.Calls == 1, "Exception cleanup leaked the supply scope");

        Reset();
        const NpcTradeStockSupplier moduleSuppliers = NpcTradeStockSupplier.Thief | NpcTradeStockSupplier.Inventor;
        Offer("test.pool.basic_module", NpcTradeStockCategory.Module,
            suppliers: moduleSuppliers, nightShop: true);
        Offer("test.pool.advanced_node", NpcTradeStockCategory.Module,
            suppliers: moduleSuppliers, nightShop: true);
        bool neuralAvailable = false;
        NativeItemRegistry.Offers.Add(new("test.pool", "test.pool.neural_interface", NativeItemKind.Node,
            (Func<GameItem>)(() => Item("test.pool.neural_interface", type: "NODE")),
            new(NightShopStockPolicy.Repeatable, () => neuralAvailable)));
        var moduleEntries = new Il2CppSystem.Collections.Generic.List<LootEntry>
        { new("native.module", 1, ""), new("test.pool.basic_module", 0.08f, "test.pool"),
            new("test.pool.neural_interface", 0.04f, "test.pool") };
        Expect(Nicokobo.Forge.Runtime.NativeHookSet.Installed.Single(hook => hook.Method == "_CreateThief_b__47_0").Prefix == "ThiefStockPrefix" &&
            Nicokobo.Forge.Runtime.NativeHookSet.Installed.Single(hook => hook.Method == "_CreatePettyThief_b__48_0").Prefix == "GeneralStockPrefix" &&
            Nicokobo.Forge.Runtime.NativeHookSet.Installed.Single(hook => hook.Method == "_CreateInventorStorage_b__31_0").Prefix == "InventorSupplyStockPrefix",
            "The restricted suppliers were attached to the wrong native callbacks");
        foreach (string callback in new[] { "GeneralStockPrefix", "TechnicalStockPrefix", "MaterialStockPrefix" })
            Supply(callback, () =>
            {
                foreach (string table in new[] { "allModuleTable", "t1moduleTable", "t2moduleTable" })
                {
                    var merged = Merge(table, moduleEntries);
                    Expect(merged.Count == 1 && merged[0].id == "native.module",
                        "Restricted modules or neural interfaces leaked to " + callback + "/" + table);
                }
                var original = Item("native.stock");
                Expect(ReferenceEquals(Place(original), original) && RNG.Calls == 0,
                    "A supplier without eligible offers drew restricted direct stock");
            });
        void CheckModuleSupply()
        {
            var merged = Merge("t2moduleTable", moduleEntries);
            Expect(merged.Count == 3 && merged.Any(entry => entry.id == "test.pool.advanced_node") &&
                merged.Where(entry => entry.sourceModId == "test.pool").All(entry => Math.Abs(entry.weight - 0.125f) < 0.000001f) &&
                merged.All(entry => entry.id != "test.pool.neural_interface"),
                "Authorized module supply did not share the fixed budget or excluded the wrong items");
            RNG.Samples.Enqueue(0.84); RNG.Samples.Enqueue(0.96);
            Expect(Place(Item("native.stock")).identifier == "test.pool.basic_module" &&
                Place(Item("native.stock")).identifier == "test.pool.advanced_node",
                "Authorized direct supply failed to reach basic and advanced modules");
        }
        Supply("ThiefStockPrefix", CheckModuleSupply);
        Supply("InventorSupplyStockPrefix", CheckModuleSupply);
        Inventor(true, CheckModuleSupply);
        Offer("test.pool.food", NpcTradeStockCategory.Food);
        Supply("ThiefStockPrefix", () =>
        {
            RNG.Samples.Enqueue(0.84); RNG.Samples.Enqueue(0.90); RNG.Samples.Enqueue(0.97);
            Expect(new[] { Place(Item("native.stock")).identifier, Place(Item("native.stock")).identifier,
                    Place(Item("native.stock")).identifier }.SequenceEqual(new[] {
                    "test.pool.basic_module", "test.pool.advanced_node", "test.pool.food" }),
                "Modules took an extra budget instead of sharing the supplier's ordinary Mod candidates");
        });
        Offer("test.pool.day_module", NpcTradeStockCategory.Module);
        Inventor(false, () =>
        {
            var merged = Merge("allModuleTable", moduleEntries);
            Expect(merged.Count == 3 && merged.All(entry => entry.id != "test.pool.day_module" &&
                    entry.id != "test.pool.neural_interface") &&
                Math.Abs(merged.Where(entry => entry.sourceModId == "test.pool").Sum(entry => entry.weight) - 0.25f) < 0.000001f,
                "Night module pools ignored explicit eligibility or changed the total budget");
            int before = RNG.Calls;
            foreach (string type in new[] { "ITEM", "MACHINE" })
            {
                var original = Item("native.guaranteed_stock", type: type);
                Expect(ReferenceEquals(Place(original), original) && RNG.Calls == before,
                    "Night module selection replaced guaranteed or non-module stock");
            }
            RNG.Samples.Enqueue(0); RNG.Samples.Enqueue(0.84); RNG.Samples.Enqueue(0.96);
            var kept = Item("native.module", type: "MODULE");
            Expect(ReferenceEquals(Place(kept), kept) && kept.DestroyCalls == 0 &&
                Place(Item("native.module", type: "MODULE")).identifier == "test.pool.basic_module" &&
                Place(Item("native.node", type: "NODE")).identifier == "test.pool.advanced_node",
                "Night-shop native module/node slots did not share the mixed pool");
            var drawn = Drawn("t2moduleTable", "native.module"); drawn.GameItemType = "MODULE";
            Expect(ReferenceEquals(Place(drawn), drawn) && RNG.Calls == before + 3,
                "A night module table result was drawn a second time at placement");
            void GenerateExtras() => typeof(NativeShopAdapter)
                .GetMethod("NightShopStockPostfix", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [false]);
            GenerateExtras();
            Expect(EmporiumEntry.Instance!.frontInvinvElement.childItems.All(item => item.identifier != "test.pool.neural_interface"),
                "The neural interface bypassed its independent night-shop availability rule");
            neuralAvailable = true; GenerateExtras();
            var neural = EmporiumEntry.Instance!.frontInvinvElement.childItems.Single(item => item.identifier == "test.pool.neural_interface");
            object?[] placement = [neural, false, null]; Call("PlaceStockPrefix", placement);
            Expect(ReferenceEquals(placement[0], neural) && RNG.Calls == before + 3,
                "The independent neural-interface night-shop offer was replaced by module supply");
        });
        Expect(ReferenceEquals(Merge("allModuleTable", moduleEntries), moduleEntries) && moduleEntries.Count == 3,
            "Supplier eligibility mutated global scavenging or leaked beyond the supply scope");

        Reset(); Offer("test.pool.quartz", NpcTradeStockCategory.Ore, 0.3f, minerWeight: 0.5f);
        Offer("test.pool.titanium", NpcTradeStockCategory.Ore, 0.3f, minerWeight: 0.5f);
        Supply("MinerStockPrefix", () =>
        {
            RNG.Samples.Enqueue(0.7);
            Expect(Place(Item("common_ore", 5)).identifier == "test.pool.quartz" &&
                Place(Item("common_ore", 7)).identifier == "test.pool.quartz" && RNG.Calls == 1,
                "Miner batch reselected ore for each unit");
            Expect(EmporiumEntry.Instance!.frontInvinvElement.childItems.Select(x => x.unitCount).SequenceEqual(new[] { 5, 7 }),
                "Miner batch replacement changed native quantities");
        });
        Supply("GeneralStockPrefix", () =>
        {
            RNG.Samples.Enqueue(0.6);
            var original = Item("native.material");
            Expect(ReferenceEquals(Place(original), original),
                "The miner's unscaled weight leaked into other suppliers");
        });
        Supply("MinerStockPrefix", () =>
        {
            RNG.Samples.Enqueue(0.85);
            Expect(Place(Item("common_ore", 4)).identifier == "test.pool.titanium",
                "The miner's titanium boundary used the scaled supply weight");
        });
        Reset();
        Console.WriteLine($"NPC supply pool checks passed: {checks} assertions.");
    }
}
